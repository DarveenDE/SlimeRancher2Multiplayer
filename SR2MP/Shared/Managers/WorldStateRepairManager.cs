using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.World;

namespace SR2MP.Shared.Managers;

public static class WorldStateRepairManager
{
    private const float RepairIntervalSeconds = 15f;
    private static bool repairLoopRunning;

    public static void Start()
    {
        if (repairLoopRunning)
            return;

        repairLoopRunning = true;
        MelonCoroutines.Start(RepairLoop());
        SrLogger.LogDebug("World state repair snapshots enabled.", SrLogTarget.Main);
    }

    public static void Stop()
    {
        if (!repairLoopRunning)
            return;

        repairLoopRunning = false;
        SrLogger.LogDebug("World state repair snapshots disabled.", SrLogTarget.Main);
    }

    private static IEnumerator RepairLoop()
    {
        while (repairLoopRunning)
        {
            var nextRepairAt = Time.realtimeSinceStartup + RepairIntervalSeconds;
            while (repairLoopRunning && Time.realtimeSinceStartup < nextRepairAt)
                yield return null;

            if (!repairLoopRunning)
                yield break;

            if (!CanSendRepairSnapshot())
                continue;

            SendRepairSnapshot();
        }
    }

    private static bool CanSendRepairSnapshot()
    {
        if (!Main.Server.IsRunning() || Main.Server.GetClientCount() <= 0)
            return false;

        if (SystemContext.Instance
            && SystemContext.Instance.SceneLoader != null
            && SystemContext.Instance.SceneLoader.IsSceneLoadInProgress)
        {
            return false;
        }

        return SceneContext.Instance && SceneContext.Instance.GameModel;
    }

    private static void SendRepairSnapshot()
    {
        var stats = new RepairSnapshotStats();

        TrySend("refinery", () => SendRefinerySnapshot(stats));
        if (!TryGetGameModel(out var gameModel))
            return;

        TrySend("land plots", () => SendLandPlotSnapshots(gameModel, stats));
        TrySend("puzzle slots", () => SendPuzzleSlotSnapshots(gameModel, stats));
        TrySend("plort depositors", () => SendPlortDepositorSnapshots(gameModel, stats));

        SrLogger.LogDebug(
            $"Repair snapshot sent: refinery={stats.RefineryItems}, ammo={stats.AmmoSets}, gardens={stats.GardenStates}, feeders={stats.FeederStates}, slots={stats.PuzzleSlots}, depositors={stats.PlortDepositors}.",
            SrLogTarget.Main);
    }

    private static void SendRefinerySnapshot(RepairSnapshotStats stats)
    {
        var items = RefinerySyncManager.CreateSnapshot(includeZeroCounts: true, logSummary: false);
        Main.Server.SendToAll(new RefineryItemCountsPacket
        {
            Items = items,
            IsRepairSnapshot = true,
        });

        stats.RefineryItems = items.Count;
    }

    private static void SendLandPlotSnapshots(GameModel gameModel, RepairSnapshotStats stats)
    {
        foreach (var plotEntry in gameModel.landPlots)
        {
            var plotId = plotEntry.Key;
            var plot = plotEntry.Value;
            if (string.IsNullOrEmpty(plotId) || plot == null)
                continue;

            foreach (var ammoSet in LandPlotAmmoSyncManager.CreateAmmoSets(plot))
            {
                Main.Server.SendToAll(new LandPlotAmmoPacket
                {
                    PlotId = plotId,
                    AmmoSet = ammoSet,
                    IsRepairSnapshot = true,
                });
                stats.AmmoSets++;
            }

            Main.Server.SendToAll(new LandPlotFeederPacket
            {
                PlotId = plotId,
                State = LandPlotFeederSyncManager.CreateState(plot),
                IsRepairSnapshot = true,
            });
            stats.FeederStates++;

            if (plot.typeId != LandPlot.Id.GARDEN)
                continue;

            Main.Server.SendToAll(new GardenPlantPacket
            {
                ID = plotId,
                HasCrop = GardenPlotSyncManager.TryGetCurrentCropType(plot, out var cropType),
                ActorType = cropType,
                IsRepairSnapshot = true,
            });
            stats.GardenStates++;
        }
    }

    private static void SendPuzzleSlotSnapshots(GameModel gameModel, RepairSnapshotStats stats)
    {
        foreach (var slotEntry in gameModel.slots)
        {
            var slot = slotEntry.Value;
            if (slot == null)
                continue;

            Main.Server.SendToAll(new PuzzleSlotStatePacket
            {
                ID = slotEntry.Key,
                Filled = slot.filled,
                IsRepairSnapshot = true,
            });
            stats.PuzzleSlots++;
        }
    }

    private static void SendPlortDepositorSnapshots(GameModel gameModel, RepairSnapshotStats stats)
    {
        foreach (var depositorEntry in gameModel.depositors)
        {
            var depositor = depositorEntry.Value;
            if (depositor == null)
                continue;

            Main.Server.SendToAll(new PlortDepositorStatePacket
            {
                ID = depositorEntry.Key,
                AmountDeposited = depositor.AmountDeposited,
                IsRepairSnapshot = true,
            });
            stats.PlortDepositors++;
        }
    }

    private static bool TryGetGameModel(out GameModel gameModel)
    {
        gameModel = null!;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        gameModel = SceneContext.Instance.GameModel;
        return true;
    }

    private static void TrySend(string area, Action send)
    {
        try
        {
            send();
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not send {area} repair snapshot: {ex.Message}", SrLogTarget.Main);
        }
    }

    private sealed class RepairSnapshotStats
    {
        public int RefineryItems { get; set; }
        public int AmmoSets { get; set; }
        public int GardenStates { get; set; }
        public int FeederStates { get; set; }
        public int PuzzleSlots { get; set; }
        public int PlortDepositors { get; set; }
    }
}
