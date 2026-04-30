using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Loading;

namespace SR2MP.Shared.Managers;

public static class LandPlotFeederSyncManager
{
    private static readonly Dictionary<string, PendingFeederState> PendingRemoteStates = new();
    private const float PendingRemoteApplyTimeoutSeconds = 10f;
    private static bool remoteStateApplyRunning;

    public static InitialLandPlotsPacket.FeederStateData CreateState(LandPlotModel model)
        => new()
        {
            Speed = model.feederCycleSpeed,
            NextFeedingTime = model.nextFeedingTime,
            RemainingFeedOperations = model.remainingFeedOperations,
        };

    public static bool TryCreateState(LandPlotModel model, out string plotId, out InitialLandPlotsPacket.FeederStateData state)
    {
        plotId = string.Empty;
        state = null!;

        if (!TryFindPlotId(model, out plotId))
            return false;

        state = CreateState(model);
        return true;
    }

    public static void QueueLocalState(LandPlotModel model)
    {
        if (model == null)
            return;

        if (!TryCreateState(model, out var plotId, out var state))
            return;

        Main.SendToAllOrServer(new LandPlotFeederPacket
        {
            PlotId = plotId,
            State = state,
        });
    }

    public static bool ApplyState(string plotId, InitialLandPlotsPacket.FeederStateData? state, string source)
    {
        if (string.IsNullOrEmpty(plotId) || state == null)
            return false;

        if (ApplyStateNow(plotId, state, source))
            return true;

        QueueRemoteState(plotId, state, source);
        return false;
    }

    private static bool ApplyStateNow(string plotId, InitialLandPlotsPacket.FeederStateData state, string source)
    {
        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(plotId, out var model) || model == null)
        {
            SrLogger.LogWarning($"Skipping feeder state from {source}; plot '{plotId}' was not found.", SrLogTarget.Main);
            return false;
        }

        var isRepairSnapshot = IsRepairSource(source);
        var beforeHash = isRepairSnapshot ? HashState(model) : 0;
        var targetHash = isRepairSnapshot ? HashState(state) : 0;
        var changed = model.feederCycleSpeed != state.Speed
                      || model.nextFeedingTime != state.NextFeedingTime
                      || model.remainingFeedOperations != state.RemainingFeedOperations;

        model.feederCycleSpeed = state.Speed;
        model.nextFeedingTime = state.NextFeedingTime;
        model.remainingFeedOperations = state.RemainingFeedOperations;

        if (model.gameObj)
        {
            var feeder = model.gameObj.GetComponentInChildren<SlimeFeeder>();
            if (feeder)
                feeder.SetFeederSpeed(state.Speed);
        }

        if (isRepairSnapshot && changed)
        {
            SrLogger.LogMessage(
                $"Repair corrected feeder state on plot '{plotId}' ({FormatHash(beforeHash)} -> {FormatHash(targetHash)}).",
                SrLogTarget.Main);
        }

        return true;
    }

    private static bool IsRepairSource(string source)
        => source.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static int HashState(LandPlotModel model)
    {
        var hash = 0;
        hash = AddHash(hash, (int)model.feederCycleSpeed);
        hash = AddHash(hash, model.nextFeedingTime.GetHashCode());
        return AddHash(hash, model.remainingFeedOperations);
    }

    private static int HashState(InitialLandPlotsPacket.FeederStateData state)
    {
        var hash = 0;
        hash = AddHash(hash, (int)state.Speed);
        hash = AddHash(hash, state.NextFeedingTime.GetHashCode());
        return AddHash(hash, state.RemainingFeedOperations);
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

    private static void QueueRemoteState(string plotId, InitialLandPlotsPacket.FeederStateData state, string source)
    {
        PendingRemoteStates[plotId] = new PendingFeederState(plotId, state, source);
        SrLogger.LogDebug($"Queued feeder state for plot '{plotId}' from {source}; target is not ready yet.", SrLogTarget.Main);

        if (remoteStateApplyRunning)
            return;

        remoteStateApplyRunning = true;
        MelonCoroutines.Start(ApplyPendingRemoteStatesWhenReady());
    }

    private static System.Collections.IEnumerator ApplyPendingRemoteStatesWhenReady()
    {
        var timeoutAt = UnityEngine.Time.realtimeSinceStartup + PendingRemoteApplyTimeoutSeconds;
        while (PendingRemoteStates.Count > 0 && UnityEngine.Time.realtimeSinceStartup < timeoutAt)
        {
            var pending = PendingRemoteStates.Values.ToList();
            foreach (var item in pending)
            {
                handlingPacket = true;
                try
                {
                    if (ApplyStateNow(item.PlotId, item.State, $"{item.Source} retry"))
                        PendingRemoteStates.Remove(item.PlotId);
                }
                finally { handlingPacket = false; }
            }

            if (PendingRemoteStates.Count > 0)
                yield return null;
        }

        if (PendingRemoteStates.Count > 0)
        {
            SrLogger.LogWarning(
                $"Could not apply {PendingRemoteStates.Count} queued feeder state update(s); target models never became ready.",
                SrLogTarget.Both);
            PendingRemoteStates.Clear();
        }

        remoteStateApplyRunning = false;
    }

    private static bool TryFindPlotId(LandPlotModel model, out string plotId)
    {
        plotId = string.Empty;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel || model == null)
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

    private sealed class PendingFeederState
    {
        public PendingFeederState(string plotId, InitialLandPlotsPacket.FeederStateData state, string source)
        {
            PlotId = plotId;
            State = state;
            Source = source;
        }

        public string PlotId { get; }
        public InitialLandPlotsPacket.FeederStateData State { get; }
        public string Source { get; }
    }
}
