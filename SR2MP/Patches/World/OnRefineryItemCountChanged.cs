using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.World;

[HarmonyPatch(typeof(GadgetsModel), nameof(GadgetsModel.SetCount))]
public static class OnRefineryItemCountChanged
{
    public static void Postfix(IdentifiableType __0, int __1)
    {
        if (handlingPacket)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        if (!__0)
            return;

        Main.SendToAllOrServer(new RefineryItemCountsPacket
        {
            Items = new Dictionary<int, int>
            {
                [NetworkActorManager.GetPersistentID(__0)] = Math.Max(0, __1)
            }
        });
    }
}
