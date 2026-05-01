using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Loading;

namespace SR2MP.Shared.Managers;

public static class LandPlotAmmoSyncManager
{
    private static readonly Dictionary<IntPtr, AmmoModel> PendingLocalAmmoModels = new();
    private const float PendingRemoteApplyTimeoutSeconds = 10f;
    private static bool localAmmoSendRunning;

    // Centralised pending queue — key is "plotId|ammoSetKey" (compound) to handle per-silo entries.
    private static readonly PendingApplyQueue<string, PendingRemoteAmmoSet> _pendingQueue =
        new("LandPlotAmmo", PendingRemoteApplyTimeoutSeconds);

    public static List<InitialLandPlotsPacket.AmmoSetData> CreateAmmoSets(LandPlotModel model)
    {
        var result = new List<InitialLandPlotsPacket.AmmoSetData>();

        if (model.siloAmmo == null)
            return result;

        foreach (var ammoSet in model.siloAmmo)
        {
            var ammoModel = ammoSet.value;
            if (ammoModel == null || ammoModel.Slots == null)
                continue;

            result.Add(CreateAmmoSet(ammoSet.key, ammoModel));
        }

        return result;
    }

    public static bool TryCreateAmmoSet(AmmoModel ammoModel, out string plotId, out InitialLandPlotsPacket.AmmoSetData ammoSetData)
    {
        plotId = string.Empty;
        ammoSetData = null!;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel || ammoModel == null)
            return false;

        foreach (var plot in SceneContext.Instance.GameModel.landPlots)
        {
            var model = plot.value;
            if (model == null || model.siloAmmo == null)
                continue;

            foreach (var ammoSet in model.siloAmmo)
            {
                if (ammoSet.value == null)
                    continue;

                if (!ReferenceEquals(ammoSet.value, ammoModel) && ammoSet.value.Pointer != ammoModel.Pointer)
                    continue;

                plotId = plot.key;
                ammoSetData = CreateAmmoSet(ammoSet.key, ammoModel);
                return true;
            }
        }

        return false;
    }

    public static void QueueLocalAmmoSet(AmmoModel ammoModel)
    {
        if (ammoModel is null || ammoModel.Pointer == IntPtr.Zero)
            return;

        PendingLocalAmmoModels[ammoModel.Pointer] = ammoModel;

        if (localAmmoSendRunning)
            return;

        localAmmoSendRunning = true;
        MelonCoroutines.Start(SendQueuedLocalAmmoSets());
    }

    public static void ApplyAmmoSets(LandPlotModel model, List<InitialLandPlotsPacket.AmmoSetData>? ammoSets, string plotId)
    {
        if (ammoSets == null || ammoSets.Count == 0)
            return;

        if (model.siloAmmo == null)
        {
            foreach (var ammoSet in ammoSets)
                QueueRemoteAmmoSet(plotId, ammoSet, "initial land plot ammo");

            return;
        }

        foreach (var ammoSet in ammoSets)
        {
            if (!model.siloAmmo.TryGetValue(ammoSet.Key, out var ammoModel) || ammoModel == null)
            {
                SrLogger.LogDebug($"Skipping unknown ammo set '{ammoSet.Key}' for plot '{plotId}'.", SrLogTarget.Main);
                QueueRemoteAmmoSet(plotId, ammoSet, "initial land plot ammo");
                continue;
            }

            if (!ApplySlots(ammoModel, ammoSet, plotId, "initial land plot ammo"))
                QueueRemoteAmmoSet(plotId, ammoSet, "initial land plot ammo");
        }
    }

    public static bool ApplyAmmoSet(string plotId, InitialLandPlotsPacket.AmmoSetData? ammoSet, string source)
    {
        if (string.IsNullOrEmpty(plotId) || ammoSet == null)
            return false;

        if (ApplyAmmoSetNow(plotId, ammoSet, source))
            return true;

        QueueRemoteAmmoSet(plotId, ammoSet, source);
        return false;
    }

    private static bool ApplyAmmoSetNow(string plotId, InitialLandPlotsPacket.AmmoSetData ammoSet, string source)
    {
        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(plotId, out var model) || model == null)
        {
            SrLogger.LogWarning($"Skipping land plot ammo update from {source}; plot '{plotId}' was not found.", SrLogTarget.Main);
            return false;
        }

        if (model.siloAmmo == null || !model.siloAmmo.TryGetValue(ammoSet.Key, out var ammoModel) || ammoModel == null)
        {
            SrLogger.LogWarning($"Skipping land plot ammo update from {source}; ammo set '{ammoSet.Key}' on plot '{plotId}' was not found.", SrLogTarget.Main);
            return false;
        }

        return ApplySlots(ammoModel, ammoSet, plotId, source);
    }

    private static InitialLandPlotsPacket.AmmoSetData CreateAmmoSet(string key, AmmoModel ammoModel)
    {
        var slots = new List<InitialLandPlotsPacket.AmmoSlotData>();
        foreach (var slot in ammoModel.Slots)
        {
            if (slot == null)
            {
                slots.Add(new InitialLandPlotsPacket.AmmoSlotData());
                continue;
            }

            var id = slot.Id;
            var hasIdentifiable = id != null && id && slot.Count > 0;

            slots.Add(new InitialLandPlotsPacket.AmmoSlotData
            {
                HasIdentifiable = hasIdentifiable,
                IdentifiableType = hasIdentifiable ? NetworkActorManager.GetPersistentID(id!) : -1,
                Count = hasIdentifiable ? slot.Count : 0,
                Radiant = hasIdentifiable && slot.Radiant,
            });
        }

        return new InitialLandPlotsPacket.AmmoSetData
        {
            Key = key,
            Slots = slots,
        };
    }

    private static bool ApplySlots(
        AmmoModel ammoModel,
        InitialLandPlotsPacket.AmmoSetData ammoSet,
        string plotId,
        string source)
    {
        var slots = ammoModel.Slots;
        if (slots == null || ammoSet.Slots == null)
            return false;

        var isRepairSnapshot = IsRepairSource(source);
        var beforeHash = 0;
        var targetHash = 0;
        var changedSlots = 0;
        var count = Math.Min(slots.Length, ammoSet.Slots.Count);
        var appliedAllSlots = true;
        for (var i = 0; i < count; i++)
        {
            var targetSlot = slots[i];
            if (targetSlot == null)
                continue;

            var sourceSlot = ammoSet.Slots[i];
            if (isRepairSnapshot)
            {
                var beforeHasIdentifiable = targetSlot.Id != null && targetSlot.Id && targetSlot.Count > 0;
                var beforeIdentifiable = beforeHasIdentifiable && TryGetPersistentId(targetSlot.Id, out var slotIdent)
                    ? slotIdent
                    : -1;
                var beforeCount = beforeHasIdentifiable ? Math.Max(0, targetSlot.Count) : 0;
                var beforeRadiant = beforeHasIdentifiable && targetSlot.Radiant;

                var targetHasIdentifiable = sourceSlot.HasIdentifiable && sourceSlot.Count > 0;
                var targetIdentifiable = targetHasIdentifiable ? sourceSlot.IdentifiableType : -1;
                var targetCount = targetHasIdentifiable ? Math.Max(0, sourceSlot.Count) : 0;
                var targetRadiant = targetHasIdentifiable && sourceSlot.Radiant;

                beforeHash = AddSlotHash(beforeHash, beforeHasIdentifiable, beforeIdentifiable, beforeCount, beforeRadiant);
                targetHash = AddSlotHash(targetHash, targetHasIdentifiable, targetIdentifiable, targetCount, targetRadiant);

                if (beforeHasIdentifiable != targetHasIdentifiable
                    || beforeIdentifiable != targetIdentifiable
                    || beforeCount != targetCount
                    || beforeRadiant != targetRadiant)
                {
                    changedSlots++;
                }
            }

            if (!sourceSlot.HasIdentifiable || sourceSlot.Count <= 0)
            {
                targetSlot.Clear();
                continue;
            }

            if (!actorManager.ActorTypes.TryGetValue(sourceSlot.IdentifiableType, out var ident) || !ident)
            {
                SrLogger.LogWarning(
                    $"Skipping unknown stored item type {sourceSlot.IdentifiableType} for ammo set '{ammoSet.Key}' on plot '{plotId}'.",
                    SrLogTarget.Main);
                appliedAllSlots = false;
                continue;
            }

            targetSlot.Id = ident;
            targetSlot.Count = sourceSlot.Count;
            targetSlot.Radiant = sourceSlot.Radiant;
        }

        ammoModel.Push(slots);
        if (isRepairSnapshot && changedSlots > 0)
        {
            var result = appliedAllSlots ? "corrected" : "partially corrected";
            SrLogger.LogMessage(
                $"Repair {result} ammo set '{ammoSet.Key}' on plot '{plotId}' ({changedSlots} slot(s), {FormatHash(beforeHash)} -> {FormatHash(targetHash)}).",
                SrLogTarget.Main);
        }

        return appliedAllSlots;
    }

    private static bool TryGetPersistentId(IdentifiableType? ident, out int persistentId)
    {
        persistentId = -1;
        if (ident == null || !ident)
            return false;

        try
        {
            persistentId = NetworkActorManager.GetPersistentID(ident);
            return true;
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not resolve stored item type while hashing ammo repair state: {ex.Message}", SrLogTarget.Main);
            return false;
        }
    }

    private static bool IsRepairSource(string source)
        => source.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static int AddSlotHash(int hash, bool hasIdentifiable, int identifiableType, int count, bool radiant)
    {
        hash = AddHash(hash, hasIdentifiable ? 1 : 0);
        hash = AddHash(hash, identifiableType);
        hash = AddHash(hash, count);
        return AddHash(hash, radiant ? 1 : 0);
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

    private static System.Collections.IEnumerator SendQueuedLocalAmmoSets()
    {
        yield return null;

        while (PendingLocalAmmoModels.Count > 0)
        {
            var pending = PendingLocalAmmoModels.Values.ToList();
            PendingLocalAmmoModels.Clear();

            if (Main.Server.IsRunning() || Main.Client.IsConnected)
            {
                foreach (var ammoModel in pending)
                {
                    if (!TryCreateAmmoSet(ammoModel, out var plotId, out var ammoSet))
                        continue;

                    Main.SendToAllOrServer(new LandPlotAmmoPacket
                    {
                        PlotId = plotId,
                        AmmoSet = ammoSet,
                    });
                }
            }

            yield return null;
        }

        localAmmoSendRunning = false;
    }

    private static void QueueRemoteAmmoSet(
        string plotId,
        InitialLandPlotsPacket.AmmoSetData ammoSet,
        string source)
    {
        if (string.IsNullOrWhiteSpace(plotId) || ammoSet == null)
            return;

        var key = $"{plotId}|{ammoSet.Key}";
        var entry = new PendingRemoteAmmoSet(plotId, ammoSet, source);
        SrLogger.LogDebug($"Queued land plot ammo set '{ammoSet.Key}' for plot '{plotId}' from {source}; target is not ready yet.", SrLogTarget.Main);

        _pendingQueue.EnqueueAndStart(
            key,
            entry,
            source,
            (k, data, src) =>
            {
                bool result = false;
                RunWithHandlingPacket(() => result = ApplyAmmoSetNow(data.PlotId, data.AmmoSet, src));
                return result;
            },
            onRepairNeeded: () => WorldStateRepairManager.RequestRepairSnapshot("land plot ammo apply timeout"));
    }

    private sealed class PendingRemoteAmmoSet
    {
        public PendingRemoteAmmoSet(string plotId, InitialLandPlotsPacket.AmmoSetData ammoSet, string source)
        {
            PlotId = plotId;
            AmmoSet = ammoSet;
            Source = source;
        }

        public string PlotId { get; }
        public InitialLandPlotsPacket.AmmoSetData AmmoSet { get; }
        public string Source { get; }
    }
}
