using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;
using SR2MP.Packets.Landplot;

namespace SR2MP.Shared.Managers;

public static class GardenGrowthSyncManager
{
    private const float PendingRemoteApplyTimeoutSeconds = 10f;
    private const float LocalPlotStateMinIntervalSeconds = 1f;
    private const float LocalProduceStateMinIntervalSeconds = 1f;
    private static readonly HashSet<string> PendingLocalPlotIds = new();
    private static readonly Dictionary<string, float> LastLocalPlotSendTimes = new();
    private static readonly Dictionary<string, int> LastLocalPlotStateHashes = new();
    private static readonly Dictionary<long, GardenGrowthPacket.ProduceStateData> PendingLocalProduceStates = new();
    private static readonly Dictionary<long, float> LastLocalProduceSendTimes = new();
    private static readonly Dictionary<long, int> LastLocalProduceStateHashes = new();
    private static readonly Dictionary<long, PendingProduceState> PendingProduceStates = new();
    private static bool remoteProduceApplyRunning;
    private static bool localProduceSendRunning;

    public static bool TryCreateSnapshot(LandPlotModel model, string plotId, out GardenGrowthPacket packet)
    {
        packet = new GardenGrowthPacket
        {
            PlotId = plotId,
        };

        if (model == null || model.typeId != LandPlot.Id.GARDEN)
            return false;

        if (TryGetSpawnResourceModel(model, out var spawnModel))
        {
            packet.HasSpawnerState = true;
            packet.StoredWater = spawnModel.storedWater;
            packet.NextSpawnTime = spawnModel.nextSpawnTime;
            packet.WasPreviouslyPlanted = spawnModel.wasPreviouslyPlanted;
            packet.NextSpawnRipens = spawnModel.nextSpawnRipens;
        }

        AddTrackedProduceStates(model, packet.ProduceStates);
        return packet.HasSpawnerState || packet.ProduceStates.Count > 0;
    }

    public static void QueueLocalPlotState(LandPlotModel model)
    {
        if (handlingPacket || model == null)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        if (!Main.Server.IsRunning())
            return;

        if (SystemContext.Instance && SystemContext.Instance.SceneLoader.IsSceneLoadInProgress)
            return;

        if (!TryFindPlotId(model, out var plotId))
            return;

        if (!PendingLocalPlotIds.Add(plotId))
            return;

        MelonCoroutines.Start(SendLocalPlotStateWhenReady(plotId));
    }

    public static void QueueLocalProduceState(ProduceModel model)
    {
        if (handlingPacket || model == null)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        if (!Main.Server.IsRunning())
            return;

        if (SystemContext.Instance && SystemContext.Instance.SceneLoader.IsSceneLoadInProgress)
            return;

        PendingLocalProduceStates[model.actorId.Value] = new GardenGrowthPacket.ProduceStateData
        {
            ActorId = model.actorId.Value,
            State = model.state,
            ProgressTime = model.progressTime,
        };

        if (localProduceSendRunning)
            return;

        localProduceSendRunning = true;
        MelonCoroutines.Start(SendLocalProduceStatesWhenReady());
    }

    public static bool ApplyState(GardenGrowthPacket packet, string source)
    {
        var appliedSpawner = true;
        if (packet.HasSpawnerState && !ApplySpawnerState(packet, source))
            appliedSpawner = false;

        var appliedProduce = true;
        foreach (var produce in packet.ProduceStates)
        {
            if (!ApplyProduceStateNow(produce, source))
            {
                QueueProduceState(produce, source);
                appliedProduce = false;
            }
        }

        return appliedSpawner && appliedProduce;
    }

    public static bool ApplyPendingForActor(long actorId)
    {
        if (!PendingProduceStates.TryGetValue(actorId, out var pending))
            return false;

        if (!ApplyProduceStateNow(pending.State, $"{pending.Source} retry"))
            return false;

        PendingProduceStates.Remove(actorId);
        SrLogger.LogDebug($"Applied queued garden produce state for actor {actorId}.", SrLogTarget.Main);
        return true;
    }

    public static void Clear()
    {
        PendingLocalPlotIds.Clear();
        LastLocalPlotSendTimes.Clear();
        LastLocalPlotStateHashes.Clear();
        PendingLocalProduceStates.Clear();
        LastLocalProduceSendTimes.Clear();
        LastLocalProduceStateHashes.Clear();
        PendingProduceStates.Clear();
        remoteProduceApplyRunning = false;
        localProduceSendRunning = false;
    }

    private static bool ApplySpawnerState(GardenGrowthPacket packet, string source)
    {
        if (string.IsNullOrWhiteSpace(packet.PlotId))
            return false;

        if (!SceneContext.Instance
            || !SceneContext.Instance.GameModel
            || !SceneContext.Instance.GameModel.landPlots.TryGetValue(packet.PlotId, out var model)
            || model == null)
        {
            return false;
        }

        if (model.typeId != LandPlot.Id.GARDEN)
            return true;

        if (!TryGetSpawnResourceModel(model, out var spawnModel))
            return false;

        var isRepairSnapshot = IsRepairSource(source);
        var beforeHash = isRepairSnapshot ? HashSpawnerState(spawnModel) : 0;

        RunSuppressingLocalBroadcast(() =>
        {
            spawnModel.storedWater = packet.StoredWater;
            spawnModel.nextSpawnTime = packet.NextSpawnTime;
            spawnModel.wasPreviouslyPlanted = packet.WasPreviouslyPlanted;
            spawnModel.nextSpawnRipens = packet.NextSpawnRipens;
            spawnModel.NotifyParticipants();
        });

        var spawnResource = model.gameObj ? model.gameObj.GetComponentInChildren<SpawnResource>() : null;
        if (spawnResource)
            RunSuppressingLocalBroadcast(() => spawnResource!.SetModel(spawnModel));

        var afterHash = isRepairSnapshot ? HashSpawnerState(spawnModel) : 0;
        if (isRepairSnapshot && beforeHash != afterHash)
        {
            SrLogger.LogMessage(
                $"Repair corrected garden growth timer on plot '{packet.PlotId}' ({FormatHash(beforeHash)} -> {FormatHash(afterHash)}).",
                SrLogTarget.Main);
        }

        return true;
    }

    private static bool ApplyProduceStateNow(GardenGrowthPacket.ProduceStateData state, string source)
    {
        if (!TryGetProduceModel(state.ActorId, out var model))
            return false;

        var isRepairSnapshot = IsRepairSource(source);
        var beforeHash = isRepairSnapshot ? HashProduceState(model) : 0;

        RunSuppressingLocalBroadcast(() => model.Push(state.State, state.ProgressTime));

        var gameObject = model.GetGameObject();
        if (gameObject)
        {
            var cycle = gameObject.GetComponent<ResourceCycle>();
            if (cycle)
                RunSuppressingLocalBroadcast(() => cycle.SetModel(model));
        }

        var afterHash = isRepairSnapshot ? HashProduceState(model) : 0;
        if (isRepairSnapshot && beforeHash != afterHash)
        {
            SrLogger.LogMessage(
                $"Repair corrected garden produce actor {state.ActorId} ({FormatHash(beforeHash)} -> {FormatHash(afterHash)}).",
                SrLogTarget.Main);
        }

        return true;
    }

    private static IEnumerator SendLocalPlotStateWhenReady(string plotId)
    {
        while (GetRemainingCooldown(LastLocalPlotSendTimes, plotId, LocalPlotStateMinIntervalSeconds) > 0f)
            yield return null;

        PendingLocalPlotIds.Remove(plotId);

        if (handlingPacket)
            yield break;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            yield break;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            yield break;

        if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(plotId, out var model))
            yield break;

        if (!TryCreateSnapshot(model, plotId, out var packet))
            yield break;

        var stateHash = HashPacketForLocalSend(packet);
        if (LastLocalPlotStateHashes.TryGetValue(plotId, out var lastHash) && lastHash == stateHash)
            yield break;

        LastLocalPlotStateHashes[plotId] = stateHash;
        LastLocalPlotSendTimes[plotId] = Time.realtimeSinceStartup;
        Main.SendToAllOrServer(packet);
    }

    private static IEnumerator SendLocalProduceStatesWhenReady()
    {
        while (PendingLocalProduceStates.Count > 0)
        {
            var dueStates = new List<GardenGrowthPacket.ProduceStateData>();
            var pending = PendingLocalProduceStates.Values.ToList();

            foreach (var state in pending)
            {
                if (GetRemainingCooldown(LastLocalProduceSendTimes, state.ActorId, LocalProduceStateMinIntervalSeconds) > 0f)
                    continue;

                PendingLocalProduceStates.Remove(state.ActorId);

                var stateHash = HashProduceStateForLocalSend(state);
                if (LastLocalProduceStateHashes.TryGetValue(state.ActorId, out var lastHash) && lastHash == stateHash)
                    continue;

                LastLocalProduceStateHashes[state.ActorId] = stateHash;
                LastLocalProduceSendTimes[state.ActorId] = Time.realtimeSinceStartup;
                dueStates.Add(state);
            }

            if (dueStates.Count > 0)
            {
                Main.SendToAllOrServer(new GardenGrowthPacket
                {
                    ProduceStates = dueStates,
                });
            }

            if (PendingLocalProduceStates.Count > 0)
                yield return null;
        }

        localProduceSendRunning = false;
    }

    private static void AddTrackedProduceStates(
        LandPlotModel model,
        List<GardenGrowthPacket.ProduceStateData> produceStates)
    {
        if (model.trackedActors == null
            || model.trackedActors.trackedActorIds == null
            || model.trackedActors.trackedActorIds.Count <= 0)
        {
            return;
        }

        foreach (var actorId in model.trackedActors.trackedActorIds)
        {
            if (!TryGetProduceModel(actorId.Value, out var produceModel))
                continue;

            produceStates.Add(new GardenGrowthPacket.ProduceStateData
            {
                ActorId = actorId.Value,
                State = produceModel.state,
                ProgressTime = produceModel.progressTime,
            });
        }
    }

    private static bool TryGetSpawnResourceModel(LandPlotModel model, out SpawnResourceModel spawnModel)
    {
        spawnModel = null!;

        if (!model.gameObj)
            return false;

        var spawnResource = model.gameObj.GetComponentInChildren<SpawnResource>();
        if (!spawnResource || spawnResource._model == null)
            return false;

        spawnModel = spawnResource._model;
        return true;
    }

    private static bool TryGetProduceModel(long actorId, out ProduceModel model)
    {
        model = null!;

        if (actorManager.Actors.TryGetValue(actorId, out var actorModel))
        {
            var produceModel = actorModel.TryCast<ProduceModel>();
            if (produceModel != null)
            {
                model = produceModel;
                return true;
            }
        }

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        if (!SceneContext.Instance.GameModel.identifiables.TryGetValue(new ActorId(actorId), out var identifiable))
            return false;

        var foundModel = identifiable.TryCast<ProduceModel>();
        if (foundModel == null)
            return false;

        model = foundModel;
        return true;
    }

    private static bool TryFindPlotId(LandPlotModel model, out string plotId)
    {
        plotId = string.Empty;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        foreach (var plot in SceneContext.Instance.GameModel.landPlots)
        {
            if (plot.value == null)
                continue;

            if (ReferenceEquals(plot.value, model) || plot.value.Pointer == model.Pointer)
            {
                plotId = plot.key;
                return true;
            }
        }

        return false;
    }

    private static void QueueProduceState(GardenGrowthPacket.ProduceStateData state, string source)
    {
        PendingProduceStates[state.ActorId] = new PendingProduceState(
            state,
            source,
            Time.realtimeSinceStartup + PendingRemoteApplyTimeoutSeconds);

        SrLogger.LogDebug($"Queued garden produce state from {source}; actor {state.ActorId} is not ready yet.", SrLogTarget.Main);

        if (remoteProduceApplyRunning)
            return;

        remoteProduceApplyRunning = true;
        MelonCoroutines.Start(ApplyPendingProduceStatesWhenReady());
    }

    private static IEnumerator ApplyPendingProduceStatesWhenReady()
    {
        while (PendingProduceStates.Count > 0)
        {
            var now = Time.realtimeSinceStartup;
            var pending = PendingProduceStates.Values.ToList();
            foreach (var item in pending)
            {
                if (ApplyPendingForActor(item.ActorId))
                    continue;

                if (now < item.TimeoutAt)
                    continue;

                PendingProduceStates.Remove(item.ActorId);
                SrLogger.LogDebug(
                    $"Dropped queued garden produce state for actor {item.ActorId}; actor did not become ready within {PendingRemoteApplyTimeoutSeconds:0.#}s.",
                    SrLogTarget.Main);
            }

            if (PendingProduceStates.Count > 0)
                yield return null;
        }

        remoteProduceApplyRunning = false;
    }

    private static bool IsRepairSource(string source)
        => source.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static int HashSpawnerState(SpawnResourceModel model)
    {
        var hash = 0;
        hash = AddHash(hash, model.storedWater.GetHashCode());
        hash = AddHash(hash, model.nextSpawnTime.GetHashCode());
        hash = AddHash(hash, model.wasPreviouslyPlanted ? 1 : 0);
        return AddHash(hash, model.nextSpawnRipens ? 1 : 0);
    }

    private static int HashProduceState(ProduceModel model)
    {
        var hash = 0;
        hash = AddHash(hash, (int)model.state);
        return AddHash(hash, model.progressTime.GetHashCode());
    }

    private static int AddHash(int hash, int value)
    {
        unchecked
        {
            return (hash * 397) ^ value;
        }
    }

    private static string FormatHash(int hash)
        => $"0x{hash:X8}";

    private static float GetRemainingCooldown<TKey>(
        Dictionary<TKey, float> lastSendTimes,
        TKey key,
        float minInterval) where TKey : notnull
    {
        return lastSendTimes.TryGetValue(key, out var lastSendTime)
            ? Math.Max(0f, lastSendTime + minInterval - Time.realtimeSinceStartup)
            : 0f;
    }

    private static int HashPacketForLocalSend(GardenGrowthPacket packet)
    {
        var hash = 0;
        hash = AddHash(hash, packet.HasSpawnerState ? 1 : 0);
        if (packet.HasSpawnerState)
        {
            hash = AddHash(hash, packet.StoredWater.GetHashCode());
            hash = AddHash(hash, packet.NextSpawnTime.GetHashCode());
            hash = AddHash(hash, packet.WasPreviouslyPlanted ? 1 : 0);
            hash = AddHash(hash, packet.NextSpawnRipens ? 1 : 0);
        }

        foreach (var state in packet.ProduceStates)
        {
            hash = AddHash(hash, HashProduceStateForLocalSend(state));
        }

        return hash;
    }

    private static int HashProduceStateForLocalSend(GardenGrowthPacket.ProduceStateData state)
    {
        var hash = state.ActorId.GetHashCode();
        hash = AddHash(hash, (int)state.State);
        return AddHash(hash, state.ProgressTime.GetHashCode());
    }

    private static void RunSuppressingLocalBroadcast(Action action)
    {
        var wasHandlingPacket = handlingPacket;
        handlingPacket = true;
        try
        {
            action();
        }
        finally
        {
            handlingPacket = wasHandlingPacket;
        }
    }

    private sealed class PendingProduceState
    {
        public PendingProduceState(
            GardenGrowthPacket.ProduceStateData state,
            string source,
            float timeoutAt)
        {
            State = state;
            Source = source;
            TimeoutAt = timeoutAt;
        }

        public long ActorId => State.ActorId;
        public GardenGrowthPacket.ProduceStateData State { get; }
        public string Source { get; }
        public float TimeoutAt { get; }
    }
}
