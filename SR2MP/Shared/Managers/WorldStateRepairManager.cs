using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;
using SR2MP.Packets.Gordo;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Loading;
using SR2MP.Packets.Switch;
using SR2MP.Packets.World;
using SR2MP.Shared.Utils;

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
        {
            PerformanceDiagnostics.RecordWorldRepairSkippedNoClients();
            return false;
        }

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
        PerformanceDiagnostics.RecordWorldRepairSnapshot();
        var stats = new RepairSnapshotStats();

        TrySend("refinery", () => SendRefinerySnapshot(stats));
        if (!TryGetGameModel(out var gameModel))
            return;

        TrySend("land plots", () => SendLandPlotSnapshots(gameModel, stats));
        TrySend("switches", () => SendSwitchSnapshots(gameModel, stats));
        TrySend("access doors", () => SendAccessDoorSnapshots(gameModel, stats));
        TrySend("map unlocks", () => SendMapUnlockSnapshots(stats));
        TrySend("gordos", () => SendGordoSnapshots(gameModel, stats));
        TrySend("puzzle slots", () => SendPuzzleSlotSnapshots(gameModel, stats));
        TrySend("plort depositors", () => SendPlortDepositorSnapshots(gameModel, stats));

        SrLogger.LogDebug(
            $"Repair snapshot sent: refinery={stats.RefineryItems}, plotTypes={stats.LandPlotTypes}, plotUpgrades={stats.LandPlotUpgrades}, ammo={stats.AmmoSets}, gardens={stats.GardenStates}, gardenGrowth={stats.GardenGrowthStates}, gardenProduce={stats.GardenProduceStates}, gardenAttach={stats.GardenResourceAttachStates}, feeders={stats.FeederStates}, switches={stats.Switches}, doors={stats.AccessDoors}, maps={stats.MapUnlocks}, gordos={stats.Gordos}, slots={stats.PuzzleSlots}, depositors={stats.PlortDepositors}.",
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

            Main.Server.SendToAll(new LandPlotUpdatePacket
            {
                ID = plotId,
                IsUpgrade = false,
                PlotType = plot.typeId,
                IsRepairSnapshot = true,
            });
            stats.LandPlotTypes++;

            if (plot.upgrades != null && plot.upgrades.Count > 0)
            {
                foreach (var upgrade in plot.upgrades)
                {
                    Main.Server.SendToAll(new LandPlotUpdatePacket
                    {
                        ID = plotId,
                        IsUpgrade = true,
                        PlotUpgrade = upgrade,
                        IsRepairSnapshot = true,
                    });
                    stats.LandPlotUpgrades++;
                }
            }

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

            if (GardenGrowthSyncManager.TryCreateSnapshot(plot, plotId, out var growthPacket))
            {
                growthPacket.IsRepairSnapshot = true;
                Main.Server.SendToAll(growthPacket);
                stats.GardenGrowthStates++;
                stats.GardenProduceStates += growthPacket.ProduceStates.Count;
            }

        }

        foreach (var attachPacket in GardenResourceAttachSyncManager.CreateGardenSnapshots(gameModel))
        {
            Main.Server.SendToAll(attachPacket);
            stats.GardenResourceAttachStates++;
        }
    }

    private static void SendSwitchSnapshots(GameModel gameModel, RepairSnapshotStats stats)
    {
        foreach (var switchEntry in gameModel.switches)
        {
            var switchModel = switchEntry.Value;
            if (switchModel == null)
                continue;

            Main.Server.SendToAll(new WorldSwitchPacket
            {
                ID = switchEntry.Key,
                State = switchModel.state,
                Immediate = true,
                IsRepairSnapshot = true,
            });
            stats.Switches++;
        }
    }

    private static void SendAccessDoorSnapshots(GameModel gameModel, RepairSnapshotStats stats)
    {
        foreach (var doorEntry in gameModel.doors)
        {
            var doorModel = doorEntry.Value;
            if (doorModel == null)
                continue;

            Main.Server.SendToAll(new AccessDoorPacket
            {
                ID = doorEntry.Key,
                State = doorModel.state,
                IsRepairSnapshot = true,
            });
            stats.AccessDoors++;
        }
    }

    private static void SendMapUnlockSnapshots(RepairSnapshotStats stats)
    {
        if (!SceneContext.Instance
            || SceneContext.Instance.eventDirector == null
            || SceneContext.Instance.eventDirector._model == null)
        {
            return;
        }

        var nodeIds = MapUnlockSyncManager.CreateSnapshot();
        if (nodeIds.Count == 0)
            return;

        Main.Server.SendToAll(new InitialMapPacket
        {
            UnlockedNodes = nodeIds,
            IsRepairSnapshot = true,
        });
        stats.MapUnlocks = nodeIds.Count;
    }

    private static void SendGordoSnapshots(GameModel gameModel, RepairSnapshotStats stats)
    {
        foreach (var gordoEntry in gameModel.gordos)
        {
            var gordo = gordoEntry.Value;
            if (gordo == null)
                continue;

            var eatenCount = gordo.GordoEatenCount;
            if (eatenCount == -1)
                eatenCount = gordo.targetCount;

            Main.Server.SendToAll(new GordoFeedPacket
            {
                ID = gordoEntry.Key,
                NewFoodCount = eatenCount,
                RequiredFoodCount = gordo.targetCount,
                GordoType = gordo.identifiableType ? NetworkActorManager.GetPersistentID(gordo.identifiableType) : -1,
                IsRepairSnapshot = true,
            });
            stats.Gordos++;

            if (gordo.GordoEatenCount > gordo.targetCount)
            {
                Main.Server.SendToAll(new GordoBurstPacket
                {
                    ID = gordoEntry.Key,
                    IsRepairSnapshot = true,
                });
            }
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
        public int LandPlotTypes { get; set; }
        public int LandPlotUpgrades { get; set; }
        public int AmmoSets { get; set; }
        public int GardenStates { get; set; }
        public int GardenGrowthStates { get; set; }
        public int GardenProduceStates { get; set; }
        public int GardenResourceAttachStates { get; set; }
        public int FeederStates { get; set; }
        public int Switches { get; set; }
        public int AccessDoors { get; set; }
        public int MapUnlocks { get; set; }
        public int Gordos { get; set; }
        public int PuzzleSlots { get; set; }
        public int PlortDepositors { get; set; }
    }
}
