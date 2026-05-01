using System.Collections;
using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.SceneManagement;
using Il2CppMonomiPark.SlimeRancher.World;
using MelonLoader;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.Actor;

[HarmonyPatch(typeof(GadgetDirector), nameof(GadgetDirector.InstantiateGadget))]
public static class OnGadgetPlaced
{
    private static IEnumerator SendSpawnAfterModelInit(GameObject gadgetObject)
    {
        yield return null;

        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected) || !gadgetObject)
            yield break;

        var gadget = gadgetObject.GetComponent<Gadget>();
        if (!gadget)
            yield break;

        var model = gadget.GetModel();
        if (model == null || model.actorId.Value == 0 || model.ident == null || model.sceneGroup == null)
            yield break;

        if (Main.Client.IsConnected
            && NetworkSessionState.TryGetAssignedActorIdRange(out var minActorId, out var maxActorId)
            && (model.actorId.Value < minActorId || model.actorId.Value >= maxActorId))
        {
            SrLogger.LogWarning(
                $"Not sending gadget spawn for actor {model.actorId.Value}; local id is outside assigned range [{minActorId}, {maxActorId}).",
                SrLogTarget.Both);
            yield break;
        }

        actorManager.Actors[model.actorId.Value] = model;
        if (Main.Server.IsRunning())
            actorManager.SetActorOwner(model.actorId.Value, LocalID);

        Main.SendToAllOrServer(new ActorSpawnPacket
        {
            ActorId = model.actorId,
            ActorType = NetworkActorManager.GetPersistentID(model.ident),
            SceneGroup = NetworkSceneManager.GetPersistentID(model.sceneGroup),
            Position = model.GetPos(),
            Rotation = model.GetRot()
        });
    }

    public static void Postfix(
        GameObject __result,
        GameObject original,
        SceneGroup sceneGroup,
        Vector3 position,
        Quaternion rotation,
        bool spawnImmediate,
        bool isPrePlaced)
    {
        if (handlingPacket) return;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;
        if (SystemContext.Instance.SceneLoader.IsSceneLoadInProgress) return;
        if (isPrePlaced || !__result || !original || !sceneGroup) return;

        MelonCoroutines.Start(SendSpawnAfterModelInit(__result));
    }
}
