using HarmonyLib;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.World;

[HarmonyPatch(typeof(GadgetDirector), nameof(GadgetDirector.TryToSpendItems))]
public static class OnFabricatorSpendItems
{
    public static void Postfix(bool __result)
    {
        if (!__result || handlingPacket)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        var snapshot = RefinerySyncManager.CreateSnapshot(includeZeroCounts: true, logSummary: false);
        if (snapshot.Count == 0)
            return;

        Main.SendToAllOrServer(new RefineryItemCountsPacket
        {
            Items = snapshot,
        });
    }
}
