using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.UI.Map;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Map;

[HarmonyPatch(typeof(MapNodeActivator), nameof(MapNodeActivator.Activate))]
public static class OnMapUnlocked
{
    public static void Postfix(MapNodeActivator __instance)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        if (!MapUnlockSyncManager.TryGetNodeId(__instance._fogRevealEvent, out var nodeId))
            return;

        var packet = new MapUnlockPacket
        {
            NodeID = nodeId
        };
        Main.SendToAllOrServer(packet);
    }
}

[HarmonyPatch(typeof(MapGeneralActivator), nameof(MapGeneralActivator.Activate))]
public static class OnMapGeneralUnlocked
{
    public static void Postfix(MapGeneralActivator __instance)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        if (!MapUnlockSyncManager.TryGetNodeId(__instance._fogRevealEvent, out var nodeId))
            return;

        Main.SendToAllOrServer(new MapUnlockPacket
        {
            NodeID = nodeId
        });
    }
}
