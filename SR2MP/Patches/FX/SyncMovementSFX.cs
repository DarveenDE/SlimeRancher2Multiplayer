using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Player.CharacterController;
using Il2CppMonomiPark.SlimeRancher.Player.CharacterController.Abilities;
using SR2MP.Packets.FX;

namespace SR2MP.Patches.FX;

[HarmonyPatch(typeof(SRCharacterController), nameof(SRCharacterController.Play), typeof(SECTR_AudioCue), typeof(bool))]
public static class SyncMovementSfx
{
    private static bool IsMovementSound(string cueName) // Jump, Run, Step and Land are specific values, do not change, they are the names used in the game
        => cueName.Contains("Jump")
           || cueName.Contains("Run")
           || cueName.Contains("Step")
           || cueName.Contains("Land");

    public static void Postfix(SRCharacterController __instance, SECTR_AudioCue cue)
    {
        if (handlingPacket || !cue)
            return;

        if (JetpackLoopSoundSync.IsJetpackCue(cue))
        {
            JetpackLoopSoundSync.SendState(cue, true);
            return;
        }

        // Do not change "Player", same reason as above
        if (!IsMovementSound(cue.name))
            return;

        var packet = new MovementSoundPacket
        {
            CueName = JetpackLoopSoundSync.NormalizeCueName(cue.name),
            Position = __instance.Position,
        };

        if (Main.Server.IsRunning())
        {
            Main.Server.SendToAll(packet);
        }
        else if (Main.Client.IsConnected)
        {
            Main.Client.SendPacket(packet);
        }
    }
}

[HarmonyPatch(typeof(JetpackAbilityBehavior), "UpdateJetpackRunAudio")]
public static class SyncJetpackLoopSoundState
{
    public static void Postfix(JetpackAbilityBehavior __instance)
        => SyncFromInstance(__instance);

    internal static void SyncFromInstance(JetpackAbilityBehavior __instance)
    {
        if (handlingPacket || __instance == null)
            return;

        var cue = __instance._runCue;
        if (!cue)
            return;

        var instance = __instance._runCueInstance;
        var isPlaying = instance != null && instance.Active && !instance.Paused;
        JetpackLoopSoundSync.SendState(cue, isPlaying);
    }
}

[HarmonyPatch(typeof(JetpackAbilityBehavior), nameof(JetpackAbilityBehavior.Update), typeof(float))]
public static class SyncJetpackLoopSoundUpdateAbility
{
    public static void Postfix(JetpackAbilityBehavior __instance)
        => SyncJetpackLoopSoundState.SyncFromInstance(__instance);
}

[HarmonyPatch(typeof(JetpackAbilityBehavior), nameof(JetpackAbilityBehavior.Stop))]
public static class SyncJetpackLoopSoundStopAbility
{
    public static void Postfix(JetpackAbilityBehavior __instance)
        => JetpackLoopSoundSync.SendStopped(__instance?._runCue);
}

[HarmonyPatch(typeof(JetpackAbilityBehavior), nameof(JetpackAbilityBehavior.OnDisable))]
public static class SyncJetpackLoopSoundDisableAbility
{
    public static void Postfix(JetpackAbilityBehavior __instance)
        => JetpackLoopSoundSync.SendStopped(__instance?._runCue);
}

[HarmonyPatch(typeof(SECTR_PointSource), nameof(SECTR_PointSource.Stop))]
public static class SyncPlayerLoopSoundStop
{
    public static void Prefix(SECTR_PointSource __instance)
    {
        if (handlingPacket || !__instance || !__instance.Cue)
            return;

        if (!__instance.Cue.name.Contains("Jet", StringComparison.OrdinalIgnoreCase)
            && !__instance.Cue.name.Contains("Thrust", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!IsLocalPlayerSource(__instance))
            return;

        JetpackLoopSoundSync.SendStopped(__instance.Cue);
    }

    private static bool IsLocalPlayerSource(SECTR_PointSource source)
    {
        if (!SceneContext.Instance || !SceneContext.Instance.player)
            return false;

        var playerTransform = SceneContext.Instance.player.transform;
        return source.transform == playerTransform || source.transform.IsChildOf(playerTransform);
    }
}

internal static class JetpackLoopSoundSync
{
    private const float RefreshIntervalSeconds = 0.75f;
    private static bool lastKnownPlaying;
    private static string lastCueName = string.Empty;
    private static float nextRefreshAt;

    internal static bool IsJetpackCue(SECTR_AudioCue cue)
        => cue
           && (cue.name.Contains("Jet", StringComparison.OrdinalIgnoreCase)
               || cue.name.Contains("Thrust", StringComparison.OrdinalIgnoreCase));

    internal static string NormalizeCueName(string cueName) => cueName.Replace(' ', '_');

    internal static void SendState(SECTR_AudioCue? cue, bool isPlaying, bool force = false)
    {
        if (handlingPacket || !cue)
            return;

        var cueName = NormalizeCueName(cue!.name);
        var now = UnityEngine.Time.realtimeSinceStartup;
        var cueChanged = !string.Equals(lastCueName, cueName, StringComparison.Ordinal);
        var shouldRefresh = isPlaying && now >= nextRefreshAt;

        if (!force && !cueChanged && lastKnownPlaying == isPlaying && !shouldRefresh)
            return;

        lastCueName = cueName;
        lastKnownPlaying = isPlaying;
        nextRefreshAt = isPlaying ? now + RefreshIntervalSeconds : 0f;

        SendPacket(cueName, isPlaying);
    }

    internal static void SendStopped(SECTR_AudioCue? cue)
        => SendState(cue, false, force: true);

    private static void SendPacket(string cueName, bool isPlaying)
    {
        if (string.IsNullOrWhiteSpace(cueName))
            return;

        var packet = new PlayerLoopSoundPacket
        {
            Player = LocalID,
            CueName = cueName,
            IsPlaying = isPlaying,
            Volume = 0.8f,
        };

        if (Main.Server.IsRunning())
        {
            Main.Server.SendToAll(packet);
        }
        else if (Main.Client.IsConnected)
        {
            Main.Client.SendPacket(packet);
        }
    }
}
