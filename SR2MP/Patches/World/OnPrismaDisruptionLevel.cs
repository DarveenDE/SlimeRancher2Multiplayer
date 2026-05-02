using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Labyrinth;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.World;

// String-based type patching so the patch silently skips on SR2 builds that
// predate Radiant Slime Sanctuary (Patch 1.2) where PrismaDirector does not exist.
[HarmonyPatch("Il2CppMonomiPark.SlimeRancher.Labyrinth.PrismaDirector", "SetDisruptionLevel")]
public static class OnPrismaDisruptionLevel
{
    public static void Postfix(PrismaDirector.DisruptionArea area, DisruptionLevel level)
    {
        if (handlingPacket) return;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;
        PrismaDisruptionSyncManager.BroadcastLevel(area, level);
    }
}
