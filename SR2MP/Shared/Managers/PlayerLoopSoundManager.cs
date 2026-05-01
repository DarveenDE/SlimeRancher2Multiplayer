using System.Collections;
using MelonLoader;
using SR2MP.Packets.FX;
using SR2MP.Shared.Utils;

namespace SR2MP.Shared.Managers;

public static class PlayerLoopSoundManager
{
    private const float SafetyTimeoutSeconds = 12f;
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

        if (ActiveSounds.TryGetValue(key, out var existing) && existing)
        {
            existing.transform.SetParent(playerObject.transform, false);
            existing.transform.localPosition = Vector3.zero;

            var existingSource = existing.GetComponent<SECTR_PointSource>();
            if (existingSource && existingSource.Cue == cue)
            {
                existingSource.instance.Volume = packet.Volume;
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
        source.instance.Volume = packet.Volume;

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
