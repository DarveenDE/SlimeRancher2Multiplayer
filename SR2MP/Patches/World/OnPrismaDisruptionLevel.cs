using HarmonyLib;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.World;

// Uses string-based type patching so the patch silently skips on SR2 builds that
// predate Radiant Slime Sanctuary (Patch 1.2) where PrismaDirector does not exist.
[HarmonyPatch("Il2CppMonomiPark.SlimeRancher.Labyrinth.PrismaDirector", "SetPrismaDisruptionLevel")]
public static class OnPrismaDisruptionLevel
{
    public static void Postfix()
    {
        if (handlingPacket) return;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;
        PrismaDisruptionSyncManager.BroadcastCurrentLevel();
    }
}
