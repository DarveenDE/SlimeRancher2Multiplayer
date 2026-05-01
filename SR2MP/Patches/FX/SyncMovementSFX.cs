using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Player.CharacterController;
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

    private static bool IsPlayerLoopSound(string cueName)
        => cueName.Contains("Jet", StringComparison.OrdinalIgnoreCase)
           || cueName.Contains("Thrust", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCueName(string cueName) => cueName.Replace(' ', '_');

    public static void Postfix(SRCharacterController __instance, SECTR_AudioCue cue)
    {
        if (handlingPacket || !cue)
            return;

        if (IsPlayerLoopSound(cue.name))
        {
            SendPlayerLoopSound(cue, true);
            return;
        }

        // Do not change "Player", same reason as above
        if (!IsMovementSound(cue.name))
            return;

        var packet = new MovementSoundPacket
        {
            CueName = NormalizeCueName(cue.name),
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

    internal static void SendPlayerLoopSound(SECTR_AudioCue cue, bool isPlaying)
    {
        if (!cue)
            return;

        var packet = new PlayerLoopSoundPacket
        {
            Player = LocalID,
            CueName = NormalizeCueName(cue.name),
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

        SyncMovementSfx.SendPlayerLoopSound(__instance.Cue, false);
    }

    private static bool IsLocalPlayerSource(SECTR_PointSource source)
    {
        if (!SceneContext.Instance || !SceneContext.Instance.player)
            return false;

        var playerTransform = SceneContext.Instance.player.transform;
        return source.transform == playerTransform || source.transform.IsChildOf(playerTransform);
    }
}
