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
            TrackKnownActorsFromGameModel(gameModel!);

            foreach (var actor in gameModel!.identifiables)
            {
                if (!IsNetworkedActorModel(actor.value, includeGadgets: false))
                    continue;
                if (actor.value.sceneGroup == scene)
                    continue;

                var obj = actor.value.GetGameObject();
                if (!obj)
                    continue;
                Object.Destroy(obj);
            }

            foreach (var actor2 in gameModel.identifiables)
            {
                if (!IsNetworkedActorModel(actor2.value, includeGadgets: false))
                    continue;

                var model = actor2.value.TryCast<ActorModel>();

                if (model == null)
                    continue;

                if (!model.ident.prefab)
                    continue;

                if (actor2.value.sceneGroup != scene)
                    continue;

                var existingObject = model.GetGameObject();
                if (existingObject)
                {
                    EnsureNetworkActor(model, existingObject);
                    continue;
                }

                GameObject obj = null!;
                RunWithHandlingPacket(() => obj = InstantiationHelpers.InstantiateActorFromModel(model));

                if (!obj)
                    continue;

                EnsureNetworkActor(model, obj);
            }

            yield return TakeOwnershipOfNearby();
        }
    }

    public void RefreshKnownActorsFromGameModel()
    {
        var gameModel = SceneContext.Instance?.GameModel;
        if (!gameModel)
            return;

        TrackKnownActorsFromGameModel(gameModel!);
    }

    private static void EnsureNetworkActor(ActorModel model, GameObject obj)
    {
        var networkComponent = obj.GetComponent<NetworkActor>();
        if (!networkComponent)
            networkComponent = obj.AddComponent<NetworkActor>();

        networkComponent.previousPosition = model.lastPosition;
        networkComponent.nextPosition = model.lastPosition;
        networkComponent.previousRotation = model.lastRotation;
        networkComponent.nextRotation = model.lastRotation;

        actorManager.Actors[model.actorId.Value] = model;
        if (Main.Server.IsRunning())
            actorManager.SetActorOwner(model.actorId.Value, LocalID);
    }

    private static void TrackKnownActorsFromGameModel(GameModel gameModel)
    {
        var knownActorIds = new HashSet<long>();

        foreach (var actor in gameModel.identifiables)
        {
            if (!IsNetworkedActorModel(actor.value, includeGadgets: true))
                continue;

            var actorId = actor.value.actorId.Value;
            if (actorId == 0)
                continue;

            knownActorIds.Add(actorId);
            actorManager.Actors[actorId] = actor.value;
        }

        foreach (var actorId in actorManager.Actors.Keys.ToArray())
        {
            if (knownActorIds.Contains(actorId))
                continue;

            actorManager.Actors.Remove(actorId);
            actorManager.ClearActorOwner(actorId);
        }
    }

    private static bool IsNetworkedActorModel(IdentifiableModel? actor, bool includeGadgets)
    {
        if (actor == null || !actor.ident || actor.ident.IsPlayer)
            return false;

        if (actor.TryCast<ActorModel>() != null)
            return true;

        return includeGadgets && actor.TryCast<GadgetModel>() != null;
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

        DeregisterStaleGadgetMapMarker(actorId);

        GadgetModel? model = null;
        RunWithHandlingPacket(() =>
        {
            model = SceneContext.Instance.GameModel.CreateGadgetModel(
                gadgetDefinition,
                actorId,
                scene,
                position,
                isPrePlaced);
        });

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
        else
        {
            SrLogger.LogWarning(
                $"Gadget spawn: InstantiateGadgetFromModel returned null for actor={actorId.Value} (type={type.name}). Model actorId={model.actorId.Value}. Physical gadget NOT created — client cannot interact with it.",
                SrLogTarget.Both);
        }

        gadgetModel = model;
        actorManager.Actors[actorId.Value] = model;
        return true;
    }

    private static void DeregisterStaleGadgetMapMarker(ActorId actorId)
    {
        if (actorId.Value == 0 || !SceneContext.Instance || !SceneContext.Instance.MapDirector)
            return;

        var markerId = actorId.Value.ToString();
        try
        {
            var mapDirector = SceneContext.Instance.MapDirector;
            if (mapDirector.Markers != null && mapDirector.Markers.ContainsKey(markerId))
                mapDirector.DeregisterMarker(markerId);
        }
        catch (Exception ex)
        {
            SrLogger.LogDebug($"Failed to deregister stale gadget map marker {markerId}: {ex.GetType().Name}", SrLogTarget.Sensitive);
        }
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
