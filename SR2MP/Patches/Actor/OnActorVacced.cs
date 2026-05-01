using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Components.Actor;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Actor;

[HarmonyPatch(typeof(Vacuumable), nameof(Vacuumable.Capture))]
public static class OnActorVacced
{
    public static void Postfix(Vacuumable __instance)
    {
        var networkActor = __instance.GetComponent<NetworkActor>();
        if (!networkActor)
            return;

        networkActor.LocallyOwned = true;

        var actorId = __instance._identifiable.GetActorId();
        if (Main.Client.IsConnected && !actorManager.Actors.ContainsKey(actorId.Value))
        {
            if (!TrySendClientLocalSpawn(__instance, out actorId))
                return;
        }

        if (Main.Server.IsRunning())
            actorManager.SetActorOwner(actorId.Value, LocalID);
        else if (Main.Client.IsConnected)
            actorManager.SetActorOwner(actorId.Value, LocalID);

        var packet = new ActorTransferPacket
        {
            ActorId = actorId,
            OwnerPlayer = LocalID,
        };

        Main.SendToAllOrServer(packet);
    }

    private static bool TrySendClientLocalSpawn(Vacuumable vacuumable, out ActorId actorId)
    {
        actorId = vacuumable._identifiable.GetActorId();

        var identifiable = vacuumable.GetComponent<Identifiable>();
        if (!identifiable || !identifiable.identType)
            return false;

        var sceneGroup = SystemContext.Instance.SceneLoader.CurrentSceneGroup;
        if (SceneContext.Instance.GameModel.identifiables.TryGetValue(actorId, out var model)
            && model != null
            && model.sceneGroup)
        {
            sceneGroup = model.sceneGroup;
        }

        if (!sceneGroup)
            return false;

        var actorType = NetworkActorManager.GetPersistentID(identifiable.identType);
        var sceneGroupId = NetworkSceneManager.GetPersistentID(sceneGroup);

        return ClientLocalActorSpawnHelper.TrySendSpawnForUntrackedClientActor(
            vacuumable.gameObject,
            vacuumable._identifiable,
            actorType,
            sceneGroupId,
            "Vacuumable.Capture",
            out actorId);
    }
}
