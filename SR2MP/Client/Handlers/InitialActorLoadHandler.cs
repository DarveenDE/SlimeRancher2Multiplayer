using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
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

        var toRemove = new CppCollections.Dictionary<ActorId, IdentifiableModel>(
            SceneContext.Instance.GameModel.identifiables
                .Cast<CppCollections.IDictionary<ActorId, IdentifiableModel>>());

        foreach (var actor in toRemove)
        {
            if (actor.value.ident.IsPlayer) continue;

            var gadget = actor.value.TryCast<GadgetModel>();
            if (gadget != null)
            {
                SceneContext.Instance.GameModel.DestroyGadgetModel(gadget);
                continue;
            }

            var gameObject = actor.value.GetGameObject();
            if (gameObject)
                Object.Destroy(gameObject);

            SceneContext.Instance.GameModel.DestroyIdentifiableModel(actor.value);
        }

        SceneContext.Instance.GameModel._actorIdProvider._nextActorId =
            packet.StartingActorID;

        NetworkSessionState.BeginInitialActorLoad();
        MelonCoroutines.Start(SpawnActorsBatched(packet.Actors));
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
            NetworkSessionState.EndInitialActorLoad();
        }

        SrLogger.LogMessage(
            $"Initial actor sync applied: received={actors.Count}, models={spawnedModels}, visibleNow={visibleObjects}, failed={failed}",
            SrLogTarget.Both);

        MelonCoroutines.Start(actorManager.TakeOwnershipOfNearby());
    }
}
