using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Loading;

namespace SR2MP.Shared.Managers;

public static class LandPlotFeederSyncManager
{
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

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(plotId, out var model) || model == null)
        {
            SrLogger.LogWarning($"Skipping feeder state from {source}; plot '{plotId}' was not found.", SrLogTarget.Main);
            return false;
        }

        model.feederCycleSpeed = state.Speed;
        model.nextFeedingTime = state.NextFeedingTime;
        model.remainingFeedOperations = state.RemainingFeedOperations;

        if (model.gameObj)
        {
            var feeder = model.gameObj.GetComponentInChildren<SlimeFeeder>();
            if (feeder)
                feeder.SetFeederSpeed(state.Speed);
        }

        return true;
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
}
