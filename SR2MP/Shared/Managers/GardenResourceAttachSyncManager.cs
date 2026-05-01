using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;
using SR2MP.Packets.Actor;

namespace SR2MP.Shared.Managers;

public enum ResourceAttachApplyResult
{
    Applied,
    Queued,
    Failed
}

public static class GardenResourceAttachSyncManager
{
    private const float PendingRemoteApplyTimeoutSeconds = 10f;
    private static readonly Dictionary<long, PendingResourceAttach> PendingResourceAttaches = new();
    private static bool pendingApplyRunning;

    public static bool TryCreatePacket(ResourceCycle resourceCycle, Joint joint, out ResourceAttachPacket packet)
    {
        packet = null!;

        if (!resourceCycle || resourceCycle._model == null || !joint)
            return false;

        var spawner = joint.gameObject.GetComponentInParent<SpawnResource>();
        if (!spawner || spawner._model == null || spawner.SpawnJoints == null)
            return false;

        var index = spawner.SpawnJoints.IndexOf(joint);
        if (index < 0)
            return false;

        packet = CreatePacket(resourceCycle._model.actorId, spawner, index);
        return true;
    }

    public static bool IsGardenJoint(Joint joint)
    {
        if (!joint || !SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        var spawner = joint.gameObject.GetComponentInParent<SpawnResource>();
        if (!spawner)
            return false;

        var plotId = spawner.GetComponentInParent<LandPlotLocation>()?._id;
        return !string.IsNullOrWhiteSpace(plotId)
               && SceneContext.Instance.GameModel.landPlots.TryGetValue(plotId, out var plot)
               && plot != null
               && plot.typeId == LandPlot.Id.GARDEN;
    }

    public static bool IsGardenAttachPacket(ResourceAttachPacket packet)
    {
        if (packet == null
            || string.IsNullOrWhiteSpace(packet.PlotID)
            || !SceneContext.Instance
            || !SceneContext.Instance.GameModel)
        {
            return false;
        }

        return SceneContext.Instance.GameModel.landPlots.TryGetValue(packet.PlotID, out var plot)
               && plot != null
               && plot.typeId == LandPlot.Id.GARDEN;
    }

    public static bool IsAttachedGardenResource(GameObject actor)
    {
        if (!actor)
            return false;

        var cycle = actor.GetComponent<ResourceCycle>();
        return TryFindAttachedGardenJoint(cycle, out _);
    }

    public static bool DestroyLocalResource(ResourceCycle resourceCycle, string reason)
    {
        if (!resourceCycle)
            return false;

        if (resourceCycle._model != null)
        {
            DestroyActorModel(resourceCycle._model.actorId, resourceCycle._model, reason);
            return true;
        }

        RunWithHandlingPacket(() => Destroyer.DestroyActor(resourceCycle.gameObject, reason));
        return true;
    }

    public static List<ResourceAttachPacket> CreateGardenSnapshots(GameModel gameModel)
    {
        var result = new List<ResourceAttachPacket>();
        if (gameModel == null)
            return result;

        foreach (var plotEntry in gameModel.landPlots)
        {
            var plotId = plotEntry.Key;
            var plot = plotEntry.Value;
            if (string.IsNullOrWhiteSpace(plotId)
                || plot == null
                || plot.typeId != LandPlot.Id.GARDEN
                || !plot.gameObj)
            {
                continue;
            }

            var spawner = plot.gameObj.GetComponentInChildren<SpawnResource>();
            if (!spawner || spawner._model == null || spawner.SpawnJoints == null)
                continue;

            AddSpawnerAttachments(result, plotId, spawner);
        }

        return result;
    }

    public static ResourceAttachApplyResult ApplyOrQueue(ResourceAttachPacket packet, string source)
    {
        if (packet == null || packet.ActorId.Value == 0)
            return ResourceAttachApplyResult.Failed;

        if (ApplyNow(packet, source, out var shouldRetry))
            return ResourceAttachApplyResult.Applied;

        if (!shouldRetry)
            return ResourceAttachApplyResult.Failed;

        Queue(packet, source);
        return ResourceAttachApplyResult.Queued;
    }

    public static bool ApplyPendingForActor(long actorId)
    {
        if (!PendingResourceAttaches.TryGetValue(actorId, out var pending))
            return false;

        if (ApplyNow(pending.Packet, $"{pending.Source} retry", out var shouldRetry))
        {
            PendingResourceAttaches.Remove(actorId);
            SrLogger.LogDebug($"Applied queued garden resource attach for actor {actorId}.", SrLogTarget.Main);
            return true;
        }

        if (!shouldRetry)
        {
            PendingResourceAttaches.Remove(actorId);
            SrLogger.LogDebug($"Dropped queued garden resource attach for actor {actorId}; target is invalid.", SrLogTarget.Main);
        }

        return false;
    }

    public static void Clear()
    {
        PendingResourceAttaches.Clear();
        pendingApplyRunning = false;
    }

    private static void AddSpawnerAttachments(
        List<ResourceAttachPacket> result,
        string plotId,
        SpawnResource spawner)
    {
        for (var index = 0; index < spawner.SpawnJoints.Count; index++)
        {
            var joint = spawner.SpawnJoints[index];
            if (!joint || !joint.connectedBody)
                continue;

            var resourceCycle = joint.connectedBody.GetComponent<ResourceCycle>();
            if (!resourceCycle || resourceCycle._model == null)
                continue;

            result.Add(CreatePacket(resourceCycle._model.actorId, spawner, index, plotId));
        }
    }

    private static bool TryFindAttachedGardenJoint(ResourceCycle resourceCycle, out Joint joint)
    {
        joint = null!;

        if (!resourceCycle || !SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        var body = resourceCycle.GetComponent<Rigidbody>();
        if (!body)
            return false;

        foreach (var plotEntry in SceneContext.Instance.GameModel.landPlots)
        {
            var plot = plotEntry.Value;
            if (plot == null || plot.typeId != LandPlot.Id.GARDEN || !plot.gameObj)
                continue;

            var spawner = plot.gameObj.GetComponentInChildren<SpawnResource>();
            if (!spawner || spawner.SpawnJoints == null)
                continue;

            for (var index = 0; index < spawner.SpawnJoints.Count; index++)
            {
                var candidate = spawner.SpawnJoints[index];
                if (candidate && candidate.connectedBody == body)
                {
                    joint = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static ResourceAttachPacket CreatePacket(
        ActorId actorId,
        SpawnResource spawner,
        int jointIndex,
        string? plotId = null)
    {
        return new ResourceAttachPacket
        {
            ActorId = actorId,
            Joint = jointIndex,
            PlotID = plotId ?? spawner.GetComponentInParent<LandPlotLocation>()?._id ?? string.Empty,
            SpawnerID = spawner.transform.position,
            Model = spawner._model,
        };
    }

    private static bool ApplyNow(ResourceAttachPacket packet, string source, out bool shouldRetry)
    {
        shouldRetry = false;

        if (!TryGetActorModel(packet.ActorId, out var model))
        {
            shouldRetry = true;
            return false;
        }

        if (!TryResolveJoint(packet, source, out var spawner, out var joint, out shouldRetry))
            return false;

        ApplySpawnerState(spawner._model, packet.Model);

        if (!ClearJointForIncomingActor(joint, packet.ActorId, out var alreadyAttached))
            return false;

        if (alreadyAttached)
            return true;

        return AttachResource(model, joint, out shouldRetry);
    }

    private static bool TryGetActorModel(ActorId actorId, out IdentifiableModel model)
    {
        model = null!;

        if (actorManager.Actors.TryGetValue(actorId.Value, out var actorModel) && actorModel != null)
        {
            model = actorModel;
            return true;
        }

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        if (!SceneContext.Instance.GameModel.identifiables.TryGetValue(actorId, out var identifiable) || identifiable == null)
            return false;

        model = identifiable;
        actorManager.Actors[actorId.Value] = identifiable;
        return true;
    }

    private static bool TryResolveJoint(
        ResourceAttachPacket packet,
        string source,
        out SpawnResource spawner,
        out Joint joint,
        out bool shouldRetry)
    {
        spawner = null!;
        joint = null!;
        shouldRetry = false;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
        {
            shouldRetry = true;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(packet.PlotID))
        {
            if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(packet.PlotID, out var plot) || plot == null)
            {
                shouldRetry = true;
                return false;
            }

            if (!plot.gameObj)
            {
                shouldRetry = true;
                return false;
            }

            spawner = plot.gameObj.GetComponentInChildren<SpawnResource>();
            if (!spawner || spawner._model == null)
            {
                shouldRetry = true;
                return false;
            }

            return TryGetSpawnJoint(spawner, packet.Joint, packet.ActorId, source, out joint);
        }

        if (!SceneContext.Instance.GameModel.resourceSpawners.TryGetValue(packet.SpawnerID, out var spawnerModel)
            || spawnerModel == null
            || spawnerModel.part == null)
        {
            shouldRetry = true;
            return false;
        }

        try
        {
            spawner = spawnerModel.part.Cast<SpawnResource>();
        }
        catch
        {
            spawner = null!;
        }

        if (!spawner || spawner._model == null)
        {
            shouldRetry = true;
            return false;
        }

        return TryGetSpawnJoint(spawner, packet.Joint, packet.ActorId, source, out joint);
    }

    private static bool TryGetSpawnJoint(
        SpawnResource spawner,
        int index,
        ActorId actorId,
        string source,
        out Joint joint)
    {
        joint = null!;

        if (!spawner || spawner.SpawnJoints == null || index < 0 || index >= spawner.SpawnJoints.Count)
        {
            SrLogger.LogWarning(
                $"Ignoring resource attach for actor {actorId.Value} from {source}; joint index {index} is invalid.",
                SrLogTarget.Main);
            return false;
        }

        joint = spawner.SpawnJoints[index];
        if (!joint)
        {
            SrLogger.LogWarning(
                $"Ignoring resource attach for actor {actorId.Value} from {source}; joint index {index} is not loaded.",
                SrLogTarget.Main);
            return false;
        }

        return joint;
    }

    private static void ApplySpawnerState(SpawnResourceModel target, SpawnResourceModel source)
    {
        if (target == null || source == null)
            return;

        target.nextSpawnRipens = source.nextSpawnRipens;
        target.nextSpawnTime = source.nextSpawnTime;
        target.storedWater = source.storedWater;
        target.wasPreviouslyPlanted = source.wasPreviouslyPlanted;
        target.NotifyParticipants();
    }

    private static bool ClearJointForIncomingActor(Joint joint, ActorId incomingActorId, out bool alreadyAttached)
    {
        alreadyAttached = false;

        if (!joint.connectedBody)
            return true;

        var connectedCycle = joint.connectedBody.GetComponent<ResourceCycle>();
        if (!connectedCycle || connectedCycle._model == null)
            return false;

        var existingModel = connectedCycle._model;
        if (existingModel.actorId.Value == incomingActorId.Value)
        {
            alreadyAttached = true;
            return true;
        }

        DestroyActorModel(existingModel.actorId, existingModel, "SR2MP.GardenResourceAttachSyncManager.OccupiedJoint");
        joint.connectedBody = null;
        return true;
    }

    private static bool AttachResource(IdentifiableModel model, Joint joint, out bool shouldRetry)
    {
        shouldRetry = false;

        var resourceCycle = model.GetGameObject()?.GetComponent<ResourceCycle>();
        if (!resourceCycle)
        {
            shouldRetry = true;
            return false;
        }

        RunWithHandlingPacket(() => resourceCycle!.Attach(joint));
        return true;
    }

    private static void DestroyActorModel(ActorId actorId, IdentifiableModel model, string reason)
    {
        if (SceneContext.Instance && SceneContext.Instance.GameModel)
        {
            SceneContext.Instance.GameModel.identifiables.Remove(actorId);
            if (SceneContext.Instance.GameModel.identifiablesByIdent.TryGetValue(model.ident, out var actorsByIdent))
                actorsByIdent.Remove(model);

            SceneContext.Instance.GameModel.DestroyIdentifiableModel(model);
        }

        actorManager.Actors.Remove(actorId.Value);
        actorManager.ClearActorOwner(actorId.Value);

        var obj = model.GetGameObject();
        if (obj)
            RunWithHandlingPacket(() => Destroyer.DestroyActor(obj, reason));
    }

    private static void Queue(ResourceAttachPacket packet, string source)
    {
        PendingResourceAttaches[packet.ActorId.Value] = new PendingResourceAttach(
            packet,
            source,
            Time.realtimeSinceStartup + PendingRemoteApplyTimeoutSeconds);

        SrLogger.LogDebug(
            $"Queued garden resource attach from {source}; actor {packet.ActorId.Value} or its joint is not ready yet.",
            SrLogTarget.Main);

        if (pendingApplyRunning)
            return;

        pendingApplyRunning = true;
        MelonCoroutines.Start(ApplyPendingWhenReady());
    }

    private static IEnumerator ApplyPendingWhenReady()
    {
        while (PendingResourceAttaches.Count > 0)
        {
            var now = Time.realtimeSinceStartup;
            var pending = PendingResourceAttaches.Values.ToList();
            foreach (var item in pending)
            {
                if (ApplyPendingForActor(item.ActorId))
                    continue;

                if (now < item.TimeoutAt)
                    continue;

                PendingResourceAttaches.Remove(item.ActorId);
                SrLogger.LogDebug(
                    $"Dropped queued garden resource attach for actor {item.ActorId}; target did not become ready within {PendingRemoteApplyTimeoutSeconds:0.#}s.",
                    SrLogTarget.Main);
            }

            if (PendingResourceAttaches.Count > 0)
                yield return null;
        }

        pendingApplyRunning = false;
    }

    private sealed class PendingResourceAttach
    {
        public PendingResourceAttach(ResourceAttachPacket packet, string source, float timeoutAt)
        {
            Packet = packet;
            Source = source;
            TimeoutAt = timeoutAt;
        }

        public long ActorId => Packet.ActorId.Value;
        public ResourceAttachPacket Packet { get; }
        public string Source { get; }
        public float TimeoutAt { get; }
    }
}
