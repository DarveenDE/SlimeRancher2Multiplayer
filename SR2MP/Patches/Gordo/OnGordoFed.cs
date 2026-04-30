using HarmonyLib;
using SR2MP.Packets.Gordo;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Gordo;

[HarmonyPatch(typeof(GordoEat), nameof(GordoEat.DoEat))]
public static class OnGordoFed
{
    public static void Postfix(GordoEat __instance)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        var packet = new GordoFeedPacket
        {
            ID = __instance.Id,
            NewFoodCount = __instance.GordoModel.GordoEatenCount,
            RequiredFoodCount = __instance.GordoModel.targetCount,
            GordoType = NetworkActorManager.GetPersistentID(__instance.GordoModel.identifiableType)
        };
        Main.SendToAllOrServer(packet);
    }
}
