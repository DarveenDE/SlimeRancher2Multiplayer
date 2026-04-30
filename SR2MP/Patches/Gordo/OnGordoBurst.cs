using HarmonyLib;
using SR2MP.Packets.Gordo;

namespace SR2MP.Patches.Gordo;

[HarmonyPatch(typeof(GordoEat), nameof(GordoEat.ImmediateReachedTarget))]
public static class OnGordoBurst
{
    public static void Prefix(GordoEat __instance)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        var packet = new GordoBurstPacket
        {
            ID = __instance.Id,
        };
        Main.SendToAllOrServer(packet);
    }
}
