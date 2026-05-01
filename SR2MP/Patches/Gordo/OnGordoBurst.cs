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

        // Mark the gordo as popped in our tracking model so that if a remote burst
        // packet arrives for the same gordo (race: both players feeding the last plort
        // simultaneously), ApplyGordoBurst sees alreadyPopped=true and does NOT call
        // ImmediateReachedTarget a second time.
        if (SceneContext.Instance?.GameModel?.gordos.TryGetValue(__instance.Id, out var gordoModel) == true
            && gordoModel != null)
        {
            gordoModel.GordoEatenCount = gordoModel.targetCount + 1;
        }

        var packet = new GordoBurstPacket
        {
            ID = __instance.Id,
        };
        Main.SendToAllOrServer(packet);
    }
}
