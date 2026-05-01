using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Regions;
using Il2CppMonomiPark.SlimeRancher.Util;
using MelonLoader;
using SR2E.Utils;
using SR2MP.Components.Actor;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Utils;
using UnityEngine.SceneManagement;

namespace SR2MP.Shared.Managers;

public sealed class NetworkActorManager
{
    public readonly Dictionary<long, IdentifiableModel> Actors    = new();
    public readonly Dictionary<int, IdentifiableType> ActorTypes  = new();
    public readonly Dictionary<long, string> ActorOwners = new();

    public static int GetPersistentID(IdentifiableType type)
        => GameContext.Instance.AutoSaveDirector._saveReferenceTranslation.GetPersistenceId(type);

    internal void Initialize(GameContext context)
    {
        ActorTypes.Clear();
        Actors.Clear();
        ActorOwners.Clear();

        foreach (var type in context.AutoSaveDirector._saveReferenceTranslation._identifiableTypeLookup)
        {
            ActorTypes.TryAdd(GetPersistentID(type.value), type.value);
        }

        MelonCoroutines.Start(ZoneLoadingLoop());
    }

    private IEnumerator ZoneLoadingLoop()
    {
        while (true)
        {
            yield return new WaitForSceneGroupLoad(false);
            yield return new WaitForSceneGroupLoad();

            if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
                continue;

            while (NetworkSessionState.InitialActorLoadInProgress)
                yield return null;

            if (!SystemContext.Instance.SceneLoader.IsCurrentSceneGroupGameplay())
                continue;

            var gameModel = SceneContext.Instance?.GameModel;
            if (!gameModel)
                continue;

            var scene = SystemContext.Instance.SceneLoader.CurrentSceneGroup;

            foreach (var actor in gameModel!.identifiables)
            {
                if (actor.value.ident.IsPlayer)
                    continue;

                if (actor.value.TryCast<ActorModel>() == null)
                    continue;

                var obj = actor.value.GetGameObject();
                if (!obj)
                    continue;
                Object.Destroy(obj);
                Actors.Remove(actor.value.actorId.Value);
                ClearActorOwner(actor.value.actorId.Value);
            }

            foreach (var actor2 in gameModel.identifiables)
            {
                if (actor2.value.ident.IsPlayer)
                    continue;

                var model = actor2.value.TryCast<ActorModel>();

                if (model == null)
                    continue;

                if (!model.ident.prefab)
                    continue;

                if (actor2.value.sceneGroup != scene)
                    continue;
                GameObject obj = null!;
                RunWithHandlingPacket(() => obj = InstantiationHelpers.InstantiateActorFromModel(model));

                if (!obj)
                    continue;

                var networkComponent = obj.AddComponent<NetworkActor>();

                networkComponent.previousPosition = model.lastPosition;
                networkComponent.nextPosition = model.lastPosition;
                networkComponent.previousRotation = model.lastRotation;
                networkComponent.nextRotation = model.lastRotation;

                actorManager.Actors[model.actorId.Value] = model;
                if (Main.Server.IsRunning())
                    actorManager.SetActorOwner(model.actorId.Value, LocalID);
            }

            yield return TakeOwnershipOfNearby();
        }
    }

    private static bool ActorIDAlreadyInUse(ActorId id)
    {
        var gameModel = SceneContext.Instance?.GameModel;
        return gameModel && gameModel!.TryGetIdentifiableModel(id, out _);
    }

    public bool TrySpawnNetworkActor(
        ActorId actorId,
        Vector3 position,
        Quaternion rotation,
        int typeId,
        int sceneId,
        bool isPrePlaced,
        out IdentifiableModel? actorModel)
    {
        actorModel = null;

        try
        {
            if (Main.RockPlortBug)
                typeId = 25;

            if (!NetworkSceneManager.TryGetSceneGroup(sceneId, out var scene) || scene == null)
            {
                SrLogger.LogWarning(
                    $"Tried to spawn actor {actorId.Value} with an invalid scene id {sceneId}.",
                    SrLogTarget.Both);
                return false;
            }

            if (!ActorTypes.TryGetValue(typeId, out var type) || !type)
            {
                SrLogger.LogWarning($"Tried to spawn actor with an invalid type!\n\tActor {actorId.Value}: type_{typeId}");
                return false;
            }

            if (!type.prefab)
                return false;

            if (type.isGadget())
            {
                return TrySpawnNetworkGadget(actorId, position, rotation, type, scene, isPrePlaced, out actorModel);
            }

            if (ActorIDAlreadyInUse(actorId))
                return false;

            var spawnedActorModel = SceneContext.Instance.GameModel.CreateActorModel(
                actorId,
                type,
                scene,
                position,
                rotation);

            if (spawnedActorModel == null)
                return false;

            actorModel = spawnedActorModel;

            SceneContext.Instance.GameModel.identifiables[actorId] = spawnedActorModel;
            if (SceneContext.Instance.GameModel.identifiablesByIdent.TryGetValue(type, out var actors))
            {
                actors.Add(spawnedActorModel);
            }
            else
            {
                actors = new CppCollections.List<IdentifiableModel>();
                actors.Add(spawnedActorModel);
                SceneContext.Instance.GameModel.identifiablesByIdent.Add(type, actors);
            }

            GameObject actor = null!;
            RunWithHandlingPacket(() => actor = InstantiationHelpers.InstantiateActorFromModel(spawnedActorModel));

            if (!actor)
            {
                actorManager.Actors[actorId.Value] = spawnedActorModel;
                return true;
            }

            var networkComponent = actor.AddComponent<NetworkActor>();
            networkComponent.previousPosition = position;
            networkComponent.nextPosition = position;
            networkComponent.previousRotation = rotation;
            networkComponent.nextRotation = rotation;
            actor.transform.position = position;
            actorManager.Actors[actorId.Value] = spawnedActorModel;

            return true;
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning(
                $"Failed to spawn network actor {actorId.Value} (type={typeId}, scene={sceneId}): {ex.Message}",
                SrLogTarget.Both);
            return false;
        }
    }

    public bool TrySpawnNetworkActor(
        ActorId actorId,
        Vector3 position,
        Quaternion rotation,
        int typeId,
        int sceneId,
        out IdentifiableModel? actorModel)
        => TrySpawnNetworkActor(actorId, position, rotation, typeId, sceneId, false, out actorModel);

    private static bool TrySpawnNetworkGadget(
        ActorId actorId,
        Vector3 position,
        Quaternion rotation,
        IdentifiableType type,
        Il2CppMonomiPark.SlimeRancher.SceneManagement.SceneGroup scene,
        bool isPrePlaced,
        out IdentifiableModel? gadgetModel)
    {
        gadgetModel = null;

        var gadgetDefinition = type.TryCast<GadgetDefinition>();
        if (gadgetDefinition == null)
        {
            SrLogger.LogWarning($"Tried to spawn gadget with a non-gadget definition!\n\tActor {actorId.Value}: {type.name}");
            return false;
        }

        if (ActorIDAlreadyInUse(actorId))
            return false;

        var model = SceneContext.Instance.GameModel.CreateGadgetModel(
            gadgetDefinition,
            actorId,
            scene,
            position,
            isPrePlaced);

        if (model == null)
            return false;

        model.eulerRotation = rotation.eulerAngles;
        SceneContext.Instance.GameModel.identifiables[actorId] = model;

        if (SceneContext.Instance.GameModel.identifiablesByIdent.TryGetValue(type, out var actors))
        {
            actors.Add(model);
        }
        else
        {
            actors = new CppCollections.List<IdentifiableModel>();
            actors.Add(model);
            SceneContext.Instance.GameModel.identifiablesByIdent.Add(type, actors);
        }

        GameObject gadget = null!;
        RunWithHandlingPacket(() => gadget = GadgetDirector.InstantiateGadgetFromModel(model));

        if (gadget)
        {
            gadget.transform.position = position;
            gadget.transform.rotation = rotation;
        }

        gadgetModel = model;
        actorManager.Actors[actorId.Value] = model;
        return true;
    }

    public static long GetHighestActorIdInRange(long min, long max)
    {
        long result = min;
        foreach (var actor in SceneContext.Instance.GameModel.identifiables)
        {
            var id = actor.value.actorId.Value;
            if (id < min || id >= max)
                continue;
            if (id > result)
            {
                result = id;
            }
        }
        return result;
    }

    public static long GetNextActorIdInRange(long min, long max)
    {
        var foundActor = false;
        var highest = min;

        foreach (var actor in SceneContext.Instance.GameModel.identifiables)
        {
            var id = actor.value.actorId.Value;
            if (id < min || id >= max)
                continue;

            foundActor = true;
            if (id > highest)
                highest = id;
        }

        if (!foundActor)
            return min == 0 ? 1 : min;

        var next = highest + 1;
        return next < max ? next : max - 1;
    }

    public void SetActorOwner(long actorId, string ownerPlayer)
    {
        if (actorId == 0 || string.IsNullOrWhiteSpace(ownerPlayer))
            return;

        ActorOwners[actorId] = ownerPlayer;
    }

    public bool TryGetActorOwner(long actorId, out string? ownerPlayer)
        => ActorOwners.TryGetValue(actorId, out ownerPlayer);

    public bool IsActorOwnedBy(long actorId, string ownerPlayer)
        => TryGetActorOwner(actorId, out var currentOwner)
           && string.Equals(currentOwner, ownerPlayer, StringComparison.Ordinal);

    public void ClearActorOwner(long actorId)
        => ActorOwners.Remove(actorId);

    internal IEnumerator TakeOwnershipOfNearby()
    {
        const int Max = 12;

        var player = SceneContext.Instance.player;
       
        var bounds = new Bounds(player.transform.position, new Vector3(325, 1000, 325));

        int i = 0;
        foreach (var actor in Actors.ToArray())
        {
            if (!Actors.TryGetValue(actor.Key, out var actorModel) || actorModel == null)
                continue;
             
            if (!bounds.Contains(actorModel.lastPosition))
                continue;

            if (!actorModel.TryGetNetworkComponent(out var netActor) || netActor == null)
                continue;

            var actorId = netActor.ActorId;
            if (actorId.Value == 0)
            {
                continue;
            }

            if (Main.Server.IsRunning())
            {
                netActor.LocallyOwned = true;
                actorManager.SetActorOwner(actorId.Value, LocalID);
            }

            var packet = new ActorTransferPacket
            {
                ActorId = actorId,
                OwnerPlayer = LocalID,
            };
            Main.SendToAllOrServer(packet);
            i++;

            if (i > Max)
            {
                yield return null;
                i = 0;
            }
        }
    }
}
