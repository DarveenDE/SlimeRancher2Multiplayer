using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.World;

[HarmonyPatch]
public static class OnPuzzleStateChanged
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PuzzleSlotModel), nameof(PuzzleSlotModel.Push))]
    public static void PuzzleSlotFilled(PuzzleSlotModel __instance, bool filled)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        if (!PuzzleStateSyncManager.TryGetSlotId(__instance, out var id))
            return;

        Main.SendToAllOrServer(new PuzzleSlotStatePacket
        {
            ID = id,
            Filled = filled,
        });
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlortDepositorModel), nameof(PlortDepositorModel.Push))]
    public static void PlortDeposited(PlortDepositorModel __instance, int amountDeposited)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        if (!PuzzleStateSyncManager.TryGetDepositorId(__instance, out var id))
            return;

        Main.SendToAllOrServer(new PlortDepositorStatePacket
        {
            ID = id,
            AmountDeposited = amountDeposited,
        });
    }
}
