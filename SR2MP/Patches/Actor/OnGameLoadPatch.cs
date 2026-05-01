using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Components.Actor;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.Actor;

[HarmonyPatch(typeof(SceneContext), nameof(SceneContext.Start))]
public static class OnGameLoadPatch
{
    private static bool serverStartHandlerRegistered;

    public static void Postfix()
    {
        if (serverStartHandlerRegistered)
            return;

        Main.Server.OnServerStarted += HandleServerStarted;
        serverStartHandlerRegistered = true;
    }

    private static void HandleServerStarted()
    {
        foreach (var actor in SceneContext.Instance.GameModel.identifiables)
        {
            if (actor.value.TryCast<ActorModel>() == null) continue;

            var transform = actor.value.Transform;

            if (!transform)
                continue;
            var networkComponent = transform.GetComponent<NetworkActor>();

            if (networkComponent) continue;

            transform.gameObject.AddComponent<NetworkActor>().LocallyOwned = true;

            actorManager.Actors[actor.value.actorId.Value] = actor.value;
            actorManager.SetActorOwner(actor.value.actorId.Value, LocalID);
        }

        SceneContext.Instance.GameModel._actorIdProvider._nextActorId =
            (uint)NetworkActorManager.GetNextActorIdInRange(
                PlayerIdGenerator.HostActorIdRangeMin,
                PlayerIdGenerator.HostActorIdRangeMax);
    }
}
