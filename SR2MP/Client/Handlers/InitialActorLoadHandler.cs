using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.World;
using MelonLoader;
using SR2MP.Packets.Loading;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.InitialActors)]
public sealed class ActorsLoadHandler : BaseClientPacketHandler<InitialActorsPacket>
{
    public ActorsLoadHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(InitialActorsPacket packet)
    {
        actorManager.Actors.Clear();
        actorManager.ActorOwners.Clear();

        NetworkSessionState.BeginInitialActorLoad();

        try
        {
            ClearLocalActorsBeforeInitialSync();

            SceneContext.Instance.GameModel._actorIdProvider._nextActorId =
                packet.StartingActorID;
            NetworkSessionState.SetAssignedActorIdRange(packet.ActorIdRangeMin, packet.ActorIdRangeMax);

            MelonCoroutines.Start(SpawnActorsBatched(packet.Actors));
        }
        catch
        {
            NetworkSessionState.EndInitialActorLoad();
            throw;
        }
    }

    private static void ClearLocalActorsBeforeInitialSync()
    {
        var toRemove = new CppCollections.Dictionary<ActorId, IdentifiableModel>(
            SceneContext.Instance.GameModel.identifiables
                .Cast<CppCollections.IDictionary<ActorId, IdentifiableModel>>());

        var removedActors = 0;
        var removedGadgets = 0;
        var skippedPlayers = 0;
        var cleanupErrors = 0;

        RunWithHandlingPacket(() =>
        {
            foreach (var actor in toRemove)
            {
                try
                {
                    if (actor.value == null)
                        continue;

                    if (actor.value.ident && actor.value.ident.IsPlayer)
                    {
                        skippedPlayers++;
                        continue;
                    }

                    RemoveLocalActorModel(actor.value, ref removedActors, ref removedGadgets);
                }
                catch (Exception ex)
                {
                    cleanupErrors++;
                    SrLogger.LogWarning(
                        $"Initial actor cleanup failed for local actor {actor.key.Value}: {ex.Message}",
                        SrLogTarget.Both);
                }
            }

            removedGadgets += DestroyRemainingLocalGadgetObjects();
        });

        SrLogger.LogMessage(
            $"Initial actor sync cleared local state: actors={removedActors}, gadgets={removedGadgets}, skippedPlayers={skippedPlayers}, errors={cleanupErrors}",
            SrLogTarget.Both);
    }

    private static void RemoveLocalActorModel(IdentifiableModel actor, ref int removedActors, ref int removedGadgets)
    {
        var gameObject = actor.GetGameObject();
        var gadget = actor.TryCast<GadgetModel>();

        if (gadget != null)
        {
            DeregisterGadgetMapMarker(actor.actorId);
            SceneContext.Instance.GameModel.DestroyGadgetModel(gadget);
            RemoveFromModelIndexes(actor);
            if (gameObject)
                Object.Destroy(gameObject);
            actorManager.Actors.Remove(actor.actorId.Value);
            actorManager.ClearActorOwner(actor.actorId.Value);
            removedGadgets++;
            return;
        }

        if (gameObject)
            Object.Destroy(gameObject);

        RemoveFromModelIndexes(actor);
        SceneContext.Instance.GameModel.DestroyIdentifiableModel(actor);
        actorManager.Actors.Remove(actor.actorId.Value);
        actorManager.ClearActorOwner(actor.actorId.Value);
        removedActors++;
    }

    private static void RemoveFromModelIndexes(IdentifiableModel actor)
    {
        SceneContext.Instance.GameModel.identifiables.Remove(actor.actorId);
        if (actor.ident && SceneContext.Instance.GameModel.identifiablesByIdent.TryGetValue(actor.ident, out var actorsByIdent))
            actorsByIdent.Remove(actor);
    }

    private static int DestroyRemainingLocalGadgetObjects()
    {
        var destroyed = 0;
        foreach (var gadget in Object.FindObjectsOfType<Gadget>())
        {
            if (!gadget || !gadget.gameObject)
                continue;

            var model = gadget.GetModel();
            if (model != null)
                DeregisterGadgetMapMarker(model.actorId);
            Object.Destroy(gadget.gameObject);
            destroyed++;
        }

        return destroyed;
    }

    private static void DeregisterGadgetMapMarker(ActorId actorId)
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

    // Spawns actors in batches across frames to avoid a single-frame instantiation hitch.
    private static IEnumerator SpawnActorsBatched(List<InitialActorsPacket.Actor> actors)
    {
        const int BatchSize = 20;
        var count = 0;
        var spawnedModels = 0;
        var visibleObjects = 0;
        var failed = 0;

        try
        {
            foreach (var actor in actors)
            {
                try
                {
                    if (actorManager.TrySpawnNetworkActor(
                            new ActorId(actor.ActorId),
                            actor.Position,
                            actor.Rotation,
                            actor.ActorType,
                            actor.Scene,
                            actor.IsPrePlaced,
                            out var model)
                        && model != null)
                    {
                        spawnedModels++;
                        if (model.GetGameObject())
                            visibleObjects++;
                    }
                    else
                    {
                        failed++;
                    }

                    ActorUpdateSyncManager.ApplyPendingForActor(actor.ActorId);
                    GardenGrowthSyncManager.ApplyPendingForActor(actor.ActorId);
                    GardenResourceAttachSyncManager.ApplyPendingForActor(actor.ActorId);
                }
                catch (Exception ex)
                {
                    failed++;
                    SrLogger.LogWarning(
                        $"Initial actor spawn failed for actor {actor.ActorId} (type={actor.ActorType}, scene={actor.Scene}): {ex.Message}",
                        SrLogTarget.Both);
                }

                if (++count >= BatchSize)
                {
                    count = 0;
                    yield return null;
                }
            }
        }
        finally
        {
            if (NetworkSessionState.TryGetAssignedActorIdRange(out var minActorId, out var maxActorId))
            {
                var nextActorId = NetworkActorManager.GetNextActorIdInRange(minActorId, maxActorId);
                SceneContext.Instance.GameModel._actorIdProvider._nextActorId = (uint)nextActorId;
                SrLogger.LogMessage(
                    $"Client actor id provider ready: range=[{minActorId}, {maxActorId}), next={nextActorId}",
                    SrLogTarget.Both);
            }

            NetworkSessionState.EndInitialActorLoad();
        }

        SrLogger.LogMessage(
            $"Initial actor sync applied: received={actors.Count}, models={spawnedModels}, visibleNow={visibleObjects}, failed={failed}",
            SrLogTarget.Both);

        MelonCoroutines.Start(actorManager.TakeOwnershipOfNearby());
    }
}
