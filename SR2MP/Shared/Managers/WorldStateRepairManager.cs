using System.Collections;
using System.Diagnostics;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Economy;
using Il2CppMonomiPark.SlimeRancher.Pedia;
using MelonLoader;
using SR2MP.Packets.Economy;
using SR2MP.Packets.Gordo;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Loading;
using SR2MP.Packets.Switch;
using SR2MP.Packets.World;
using SR2MP.Shared.Utils;

namespace SR2MP.Shared.Managers;

public static class WorldStateRepairManager
{
    private const float RepairIntervalSeconds = 120f;
    private const float ManualRepairCooldownSeconds = 30f;
    private static bool repairLoopRunning;
    private static float nextManualRepairAt;
    private static long repairSnapshotSequence;

    public static void Start()
    {
        if (!Main.PeriodicWorldRepairEnabled)
        {
            SrLogger.LogMessage("Periodic full world state repair snapshots disabled by configuration.", SrLogTarget.Main);
            return;
        }

        if (repairLoopRunning)
            return;

        repairLoopRunning = true;
        MelonCoroutines.Start(RepairLoop());
        SrLogger.LogMessage($"Periodic full world state repair snapshots enabled every {RepairIntervalSeconds:0.#}s.", SrLogTarget.Main);
    }

    public static void Stop()
    {
        if (!repairLoopRunning)
            return;

        repairLoopRunning = false;
        nextManualRepairAt = 0f;
        SrLogger.LogDebug("World state repair snapshots disabled.", SrLogTarget.Main);
    }

    public static bool RequestRepairSnapshot(string reason)
    {
        if (!CanSendRepairSnapshot())
            return false;

        if (Time.realtimeSinceStartup < nextManualRepairAt)
        {
            SrLogger.LogDebug($"Repair snapshot request skipped during cooldown: {reason}", SrLogTarget.Main);
            return true;
        }

        nextManualRepairAt = Time.realtimeSinceStartup + ManualRepairCooldownSeconds;
        SrLogger.LogWarning($"Sending repair snapshot due to {reason}.", SrLogTarget.Both);
        SendRepairSnapshot(reason);
        return true;
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

            SendRepairSnapshot("periodic");
        }
    }

    private static bool CanSendRepairSnapshot()
    {
        if (!Main.Server.IsRunning() || Main.Server.GetClientCount() <= 0)
        {
            PerformanceDiagnostics.RecordWorldRepairSkippedNoClients();
            return false;
        }

        // Guard via both the legacy per-client flag and the new PhaseGate so that
        // repair snapshots are never sent while any client is still in InitialSync.
        // (Fixes the 14:46-freeze pattern described in SYNC_ARCHITECTURE.md §1.3.)
        if (!Main.Server.AllClientsInitialSyncComplete()
            || SR2MP.Shared.Utils.NetworkSessionState.PhaseGate.ShouldQueueReliable)
        {
            if (Main.SyncDiagnosticsEnabled)
            {
                SrLogger.LogWarning(
                    $"Repair snapshot skipped: {Main.Server.GetInitialSyncIncompleteClientCount()} client(s) are still in initial sync.",
                    SrLogTarget.Main);
            }

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

    private static void SendRepairSnapshot(string reason)
    {
        PerformanceDiagnostics.RecordWorldRepairSnapshot();
        var snapshotId = System.Threading.Interlocked.Increment(ref repairSnapshotSequence);
        var snapshotStart = Stopwatch.GetTimestamp();
        var pendingBefore = Main.Server.GetPendingReliablePackets();
        var stats = new RepairSnapshotStats();
        void TimedSend(string area, Action send) => TrySend(area, send, stats);

        TimedSend("currency", () => SendCurrencySnapshot(stats));
        TimedSend("refinery", () => BroadcastSubsystemSnapshot(SR2MP.Shared.Sync.SubsystemIds.Refinery));
        if (!TryGetGameModel(out var gameModel))
        {
            LogRepairSnapshot(reason, snapshotId, snapshotStart, pendingBefore, stats);
            return;
        }

        TimedSend("pedia", () => SendPediaSnapshot(stats));
        TimedSend("player upgrades", () => SendPlayerUpgradeSnapshot(stats));
        TimedSend("land plots", () => BroadcastSubsystemSnapshot(SR2MP.Shared.Sync.SubsystemIds.LandPlots));
        TimedSend("garden attach", () => BroadcastSubsystemSnapshot(SR2MP.Shared.Sync.SubsystemIds.GardenResourceAttach));
        TimedSend("switches", () => SendSwitchSnapshots(gameModel, stats));
        TimedSend("access doors", () => SendAccessDoorSnapshots(gameModel, stats));
        TimedSend("map unlocks", () => SendMapUnlockSnapshots(stats));
        TimedSend("comm station", () => BroadcastSubsystemSnapshot(SR2MP.Shared.Sync.SubsystemIds.CommStation));
        TimedSend("resource nodes", () => BroadcastSubsystemSnapshot(SR2MP.Shared.Sync.SubsystemIds.ResourceNode));
        TimedSend("gordos", () => SendGordoSnapshots(gameModel, stats));
        TimedSend("puzzle state", () => BroadcastSubsystemSnapshot(SR2MP.Shared.Sync.SubsystemIds.PuzzleState));
        TimedSend("prisma disruption", () => BroadcastSubsystemSnapshot(SR2MP.Shared.Sync.SubsystemIds.PrismaDisruption));

        SrLogger.LogDebug(
            $"Repair snapshot sent: currency={stats.CurrencyValues}, pedia={stats.PediaEntries}, playerUpgrades={stats.PlayerUpgrades}, switches={stats.Switches}, doors={stats.AccessDoors}, maps={stats.MapUnlocks}, gordos={stats.Gordos}.",
            SrLogTarget.Main);
        LogRepairSnapshot(reason, snapshotId, snapshotStart, pendingBefore, stats);
    }

    private static void LogRepairSnapshot(
        string reason,
        long snapshotId,
        long snapshotStart,
        int pendingBefore,
        RepairSnapshotStats stats)
    {
        if (!Main.SyncDiagnosticsEnabled)
            return;

        var elapsedMs = TicksToMilliseconds(Stopwatch.GetTimestamp() - snapshotStart);
        var pendingAfter = Main.Server.GetPendingReliablePackets();
        var message =
            $"World repair snapshot #{snapshotId} reason={reason}, duration={elapsedMs:0.0}ms, packets~={stats.EstimatedPackets}, pendingReliable={pendingBefore}->{pendingAfter}, counts=[currency={stats.CurrencyValues}, pedia={stats.PediaEntries}, upgrades={stats.PlayerUpgrades}, switch={stats.Switches}, door={stats.AccessDoors}, map={stats.MapUnlocks}, gordo={stats.Gordos}, burst={stats.GordoBursts}], categoryMs=[{stats.CategoryTimings}].";

        if (elapsedMs >= 50d || stats.EstimatedPackets >= 100 || pendingAfter - pendingBefore >= 100)
            SrLogger.LogWarning(message, SrLogTarget.Both);
        else
            SrLogger.LogMessage(message, SrLogTarget.Both);
    }

    private static void SendCurrencySnapshot(RepairSnapshotStats stats)
    {
        var currencies = GameContext.Instance.LookupDirector._currencyList._currencies;
        for (var i = 0; i < currencies.Count; i++)
        {
            var currency = currencies[i];
            if (!currency)
                continue;

            var currencyDefinition = currency.Cast<ICurrency>();
            var amount = SceneContext.Instance.PlayerState.GetCurrency(currencyDefinition);
            Main.Server.SendToAll(new CurrencyPacket
            {
                CurrencyType = (byte)(i + 1),
                NewAmount = amount,
                PreviousAmount = amount,
                DeltaAmount = 0,
                ShowUINotification = false,
            });
            stats.CurrencyValues++;
        }
    }

    private static void SendPediaSnapshot(RepairSnapshotStats stats)
    {
        var unlocked = SceneContext.Instance.PediaDirector._pediaModel.unlocked;
        var unlockedArray = Il2CppSystem.Linq.Enumerable
            .ToArray(unlocked.Cast<CppCollections.IEnumerable<PediaEntry>>());

        var entries = unlockedArray
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.PersistenceId))
            .Select(entry => entry.PersistenceId)
            .ToList();

        Main.Server.SendToAll(new InitialPediaPacket
        {
            Entries = entries,
        });
        stats.PediaEntries = entries.Count;
    }

    private static void SendPlayerUpgradeSnapshot(RepairSnapshotStats stats)
    {
        var upgrades = new Dictionary<byte, sbyte>();
        var model = SceneContext.Instance.PlayerState._model.upgradeModel;

        foreach (var upgrade in GameContext.Instance.LookupDirector._upgradeDefinitions.items)
        {
            if (upgrade == null)
                continue;

            upgrades[(byte)upgrade._uniqueId] = (sbyte)model.GetUpgradeLevel(upgrade);
        }

        Main.Server.SendToAll(new InitialUpgradesPacket
        {
            Upgrades = upgrades,
        });
        stats.PlayerUpgrades = upgrades.Count;
    }

    private static void SendRefinerySnapshot(RepairSnapshotStats stats)
    {
        // Kept unused — see BroadcastSubsystemSnapshot(Refinery)
    }

    private static void SendLandPlotSnapshots(GameModel gameModel, RepairSnapshotStats stats)
    {
        // Kept unused — see BroadcastSubsystemSnapshot(LandPlots)
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

        // Delegate to the SubsystemRegistry — same CaptureSnapshot path as Initial-Sync.
        var nodeIds = MapUnlockSyncManager.CreateSnapshot();
        if (nodeIds.Count == 0)
            return;

        SR2MP.Shared.Sync.SubsystemRegistry.Instance
            .BroadcastSnapshot(SR2MP.Shared.Sync.SubsystemIds.MapUnlock, isRepair: true);
        stats.MapUnlocks = nodeIds.Count;
    }

    /// <summary>Broadcasts a <see cref="SR2MP.Shared.Sync.SubsystemSnapshotPacket"/> repair for the given subsystem.</summary>
    private static void BroadcastSubsystemSnapshot(byte subsystemId)
    {
        SR2MP.Shared.Sync.SubsystemRegistry.Instance.BroadcastSnapshot(subsystemId, isRepair: true);
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
                stats.GordoBursts++;
            }
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

    private static void TrySend(string area, Action send, RepairSnapshotStats stats)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            send();
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not send {area} repair snapshot: {ex.Message}", SrLogTarget.Main);
        }
        finally
        {
            stats.AddCategoryTiming(area, TicksToMilliseconds(Stopwatch.GetTimestamp() - start));
        }
    }

    private static double TicksToMilliseconds(long ticks)
        => ticks <= 0 ? 0d : ticks * 1000d / Stopwatch.Frequency;

    private sealed class RepairSnapshotStats
    {
        private readonly List<string> categoryTimings = new();

        public int CurrencyValues { get; set; }
        public int PediaEntries { get; set; }
        public int PlayerUpgrades { get; set; }
        public int Switches { get; set; }
        public int AccessDoors { get; set; }
        public int MapUnlocks { get; set; }
        public int Gordos { get; set; }
        public int GordoBursts { get; set; }

        public int EstimatedPackets =>
            CurrencyValues
            + 6 // subsystem snapshots (refinery, landplots, gardenattach, commstation, resourcenodes, puzzlestate)
            + 1 // pedia snapshot
            + 1 // player upgrade snapshot
            + Switches
            + AccessDoors
            + (MapUnlocks > 0 ? 1 : 0)
            + Gordos
            + GordoBursts;

        public string CategoryTimings => string.Join(", ", categoryTimings);

        public void AddCategoryTiming(string area, double elapsedMs)
            => categoryTimings.Add($"{area}={elapsedMs:0.0}");
    }
}
