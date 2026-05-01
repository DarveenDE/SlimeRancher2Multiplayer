using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.Actor;

[HarmonyPatch(typeof(GadgetModel), nameof(GadgetModel.DestroyGadget), typeof(GameObject), typeof(bool))]
public static class OnGadgetDestroyed
{
    public static void Prefix(GadgetModel __instance)
    {
        if (handlingPacket || NetworkSessionState.InitialActorLoadInProgress || __instance == null)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        if (SystemContext.Instance.SceneLoader.IsSceneLoadInProgress)
            return;

        var actorId = __instance.actorId;
        if (actorId.Value == 0)
            return;

        Main.SendToAllOrServer(new ActorDestroyPacket
        {
            ActorId = actorId
        });
    }

    public static void Postfix(GadgetModel __instance)
    {
        if (__instance == null)
            return;

        actorManager.Actors.Remove(__instance.actorId.Value);
        actorManager.ClearActorOwner(__instance.actorId.Value);
    }
}
