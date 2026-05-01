using System.Collections;
using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Player.CharacterController;
using Il2CppMonomiPark.SlimeRancher.SceneManagement;
using MelonLoader;
using SR2MP.Components.Actor;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.Actor;

[HarmonyPatch(typeof(InstantiationHelpers), nameof(InstantiationHelpers.InstantiateActor))]
public static class OnActorSpawn
{
    private static IEnumerator SpawnOverNetwork(int actorType, int sceneGroup, GameObject actor)
    {
        yield return null;

        if (!actor || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            yield break;

        var identifiableActor = actor.GetComponent<IdentifiableActor>();
        if (!identifiableActor)
            yield break;

        var id = identifiableActor.GetActorId();

        if (!Main.Server.IsRunning() && GardenResourceAttachSyncManager.IsAttachedGardenResource(actor))
        {
            GardenResourceAttachSyncManager.DestroyLocalResource(
                actor.GetComponent<ResourceCycle>(),
                "SR2MP.OnActorSpawn.ClientGardenSpawnSuppressed");
            yield break;
        }

        if (Main.Client.IsConnected
            && NetworkSessionState.TryGetAssignedActorIdRange(out var minActorId, out var maxActorId)
            && (id.Value < minActorId || id.Value >= maxActorId))
        {
            SrLogger.LogWarning(
                $"Not sending actor spawn for actor {id.Value}; local id is outside assigned range [{minActorId}, {maxActorId}).",
                SrLogTarget.Both);
            yield break;
        }

        if (Main.Client.IsConnected && actorManager.Actors.ContainsKey(id.Value))
            yield break;

        var packet = ClientLocalActorSpawnHelper.CreateSpawnPacket(actor, id, actorType, sceneGroup);

        Main.SendToAllOrServer(packet);

        actorManager.Actors[id.Value] = identifiableActor._model;
        if (Main.Server.IsRunning())
            actorManager.SetActorOwner(id.Value, LocalID);
        else if (Main.Client.IsConnected)
            actorManager.SetActorOwner(id.Value, LocalID);
    }

    public static void Postfix(
        GameObject __result,
        GameObject original,
        SceneGroup sceneGroup)
    {
        if (handlingPacket) return;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;
        if (SystemContext.Instance.SceneLoader.IsSceneLoadInProgress) return;
        if (!__result || !original || !sceneGroup) return;
        if (__result.GetComponent<SRCharacterController>()) return;

        var identifiable = original.GetComponent<Identifiable>();
        if (!identifiable || !identifiable.identType || identifiable.identType.IsPlayer) return;

        var identifiableActor = __result.GetComponent<IdentifiableActor>();
        if (!identifiableActor) return;

        if (!ClientLocalActorSpawnHelper.TryPrepareForLocalNetworkSpawn(
                __result,
                identifiableActor,
                "InstantiationHelpers.InstantiateActor",
                out _,
                out var shouldSendSpawn)
            || !shouldSendSpawn)
        {
            return;
        }

        var networkActor = __result.GetComponent<NetworkActor>();
        if (!networkActor)
            networkActor = __result.AddComponent<NetworkActor>();

        networkActor.LocallyOwned = true;

        var actorType = NetworkActorManager.GetPersistentID(identifiable.identType);
        var sceneGroupId = NetworkSceneManager.GetPersistentID(sceneGroup);

        MelonCoroutines.Start(SpawnOverNetwork(actorType, sceneGroupId, __result));
    }
}
