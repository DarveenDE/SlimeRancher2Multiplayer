using System.Collections;
using MelonLoader;
using SR2MP.Packets.FX;
using SR2MP.Shared.Utils;

namespace SR2MP.Shared.Managers;

public static class PlayerLoopSoundManager
{
    private const float SafetyTimeoutSeconds = 1.75f;
    private const float FullVolumeDistance = 6f;
    private const float MaxAudibleDistance = 60f;
    private const float MinimumAudibleVolume = 0.02f;
    private static readonly Dictionary<string, GameObject> ActiveSounds = new();
    private static readonly Dictionary<string, float> Expirations = new();

    public static void Apply(PlayerLoopSoundPacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.Player) || string.IsNullOrWhiteSpace(packet.CueName))
            return;

        var key = GetKey(packet.Player, packet.CueName);

        if (!packet.IsPlaying)
        {
            Stop(key);
            return;
        }

        if (!playerObjects.TryGetValue(packet.Player, out var playerObject) || !playerObject)
            return;

        if (!fxManager.AllCues.TryGetValue(packet.CueName, out var cue) || !cue)
            return;

        var expiresAt = Time.realtimeSinceStartup + SafetyTimeoutSeconds;
        Expirations[key] = expiresAt;

        var volume = CalculateSpatialVolume(packet, playerObject);
        if (volume <= MinimumAudibleVolume)
        {
            Stop(key);
            MelonCoroutines.Start(StopWhenExpired(key, expiresAt));
            return;
        }

        cue.Spatialization = SECTR_AudioCue.Spatializations.Occludable3D;

        if (ActiveSounds.TryGetValue(key, out var existing) && existing)
        {
            existing.transform.SetParent(playerObject.transform, false);
            existing.transform.localPosition = Vector3.zero;

            var existingSource = existing.GetComponent<SECTR_PointSource>();
            if (existingSource && existingSource.Cue == cue)
            {
                existingSource.instance.Volume = volume;
                MelonCoroutines.Start(StopWhenExpired(key, expiresAt));
                return;
            }

            Stop(key);
        }

        var soundObject = new GameObject($"SR2MP_LoopSound_{packet.CueName}");
        soundObject.transform.SetParent(playerObject.transform, false);
        soundObject.transform.localPosition = Vector3.zero;

        var source = soundObject.AddComponent<SECTR_PointSource>();
        source.instance = new SECTR_AudioCueInstance();
        source.Cue = cue;
        source.Loop = true;
        source.instance.Volume = volume;

        using (NetworkSessionState.PhaseGate.EnterEchoGuard())
        {
            source.Play();
        }

        ActiveSounds[key] = soundObject;
        MelonCoroutines.Start(StopWhenExpired(key, expiresAt));
    }

    private static IEnumerator StopWhenExpired(string key, float expiresAt)
    {
        yield return new WaitForSeconds(SafetyTimeoutSeconds + 0.1f);

        if (Expirations.TryGetValue(key, out var currentExpiry) && currentExpiry <= expiresAt)
            Stop(key);
    }

    private static float CalculateSpatialVolume(PlayerLoopSoundPacket packet, GameObject playerObject)
    {
        if (!IsSameSceneGroup(packet.Player))
            return 0f;

        if (!SceneContext.Instance || !SceneContext.Instance.player)
            return packet.Volume;

        var distance = Vector3.Distance(SceneContext.Instance.player.transform.position, playerObject.transform.position);
        if (distance <= FullVolumeDistance)
            return packet.Volume;

        if (distance >= MaxAudibleDistance)
            return 0f;

        var fade = 1f - Mathf.InverseLerp(FullVolumeDistance, MaxAudibleDistance, distance);
        return packet.Volume * fade * fade;
    }

    private static bool IsSameSceneGroup(string playerId)
    {
        var remotePlayer = playerManager.GetPlayer(playerId);
        if (remotePlayer == null || remotePlayer.SceneGroup < 0)
            return true;

        try
        {
            if (!SystemContext.Instance || !SystemContext.Instance.SceneLoader)
                return true;

            var currentSceneGroup = SystemContext.Instance.SceneLoader.CurrentSceneGroup;
            if (!currentSceneGroup)
                return true;

            return remotePlayer.SceneGroup == NetworkSceneManager.GetPersistentID(currentSceneGroup);
        }
        catch (Exception ex)
        {
            SrLogger.LogDebug($"Could not compare loop sound scene group for {playerId}: {ex.Message}", SrLogTarget.Main);
            return true;
        }
    }

    private static void Stop(string key)
    {
        Expirations.Remove(key);

        if (!ActiveSounds.TryGetValue(key, out var soundObject))
            return;

        ActiveSounds.Remove(key);

        if (!soundObject)
            return;

        using (NetworkSessionState.PhaseGate.EnterEchoGuard())
        {
            Object.Destroy(soundObject);
        }
    }

    private static string GetKey(string playerId, string cueName) => $"{playerId}|{cueName}";
}
