using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.World.ResourceNode;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.World;

[HarmonyPatch(typeof(ResourceNodeDirector), "SpawnNode")]
public static class BlockClientResourceNodeDirectorSpawn
{
    public static bool Prefix()
        => !Main.Client.IsConnected || Main.Server.IsRunning() || handlingPacket;
}

[HarmonyPatch(typeof(ResourceNodeDirector), "SpawnUpToMaxNodes")]
public static class BlockClientResourceNodeDirectorSpawnUpToMax
{
    public static bool Prefix()
        => !Main.Client.IsConnected || Main.Server.IsRunning() || handlingPacket;
}

[HarmonyPatch(typeof(ResourceNodeSpawner), nameof(ResourceNodeSpawner.SpawnNode))]
public static class OnResourceNodeSpawned
{
    public static void Postfix(ResourceNodeSpawner __instance)
    {
        if (__instance && __instance._model != null)
            ResourceNodeSyncManager.SendSnapshot(__instance._model);
    }
}

[HarmonyPatch(typeof(ResourceNodeSpawner), nameof(ResourceNodeSpawner.DespawnNode))]
public static class OnResourceNodeDespawned
{
    public static void Postfix(ResourceNodeSpawner __instance)
    {
        if (__instance && __instance._model != null)
            ResourceNodeSyncManager.SendSnapshot(__instance._model);
    }
}

[HarmonyPatch(typeof(ResourceNode), "SetStateReady")]
public static class OnResourceNodeReady
{
    public static void Postfix(ResourceNode __instance)
    {
        if (__instance && __instance._model != null)
            ResourceNodeSyncManager.SendSnapshot(__instance._model);
    }
}

[HarmonyPatch(typeof(ResourceNode), "SetStateHarvesting")]
public static class OnResourceNodeHarvesting
{
    public static void Postfix(ResourceNode __instance)
    {
        if (__instance && __instance._model != null)
            ResourceNodeSyncManager.SendSnapshot(__instance._model);
    }
}

[HarmonyPatch(typeof(ResourceNode), "SetStateEmpty")]
public static class OnResourceNodeEmpty
{
    public static void Postfix(ResourceNode __instance)
    {
        if (__instance && __instance._model != null)
            ResourceNodeSyncManager.SendSnapshot(__instance._model);
    }
}
