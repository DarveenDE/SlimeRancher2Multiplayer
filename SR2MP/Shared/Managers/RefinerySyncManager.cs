using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;

namespace SR2MP.Shared.Managers;

public static class RefinerySyncManager
{
    private static readonly Dictionary<int, int> PendingCounts = new();
    private static bool pendingApplyRunning;

    public static Dictionary<int, int> CreateSnapshot(bool includeZeroCounts, bool logSummary = true)
    {
        var result = new Dictionary<int, int>();

        if (!TryGetGadgetDirector(out var gadgetDirector))
        {
            SrLogger.LogWarning("Could not create refinery snapshot because GadgetDirector is not ready.", SrLogTarget.Main);
            return result;
        }

        var modelItemCounts = 0;
        if (TryGetGadgetsModel(out var model))
        {
            foreach (var itemCount in model._itemCounts)
            {
                var ident = itemCount.key;
                if (!ident)
                    continue;

                var count = Math.Max(0, itemCount.value);
                if (includeZeroCounts || count > 0)
                {
                    result[NetworkActorManager.GetPersistentID(ident)] = count;
                    modelItemCounts++;
                }
            }
        }

        foreach (var ident in GetKnownRefineryItems(gadgetDirector))
        {
            if (!ident)
                continue;

            var count = Math.Max(0, gadgetDirector.GetItemCount(ident));
            if (includeZeroCounts || count > 0)
                result[NetworkActorManager.GetPersistentID(ident)] = count;
        }

        if (logSummary)
        {
            var nonZeroCount = result.Count(item => item.Value > 0);
            SrLogger.LogMessage($"Created refinery snapshot with {result.Count} entries ({nonZeroCount} non-zero, {modelItemCounts} from model counts).", SrLogTarget.Main);
        }

        return result;
    }

    public static bool ApplyCounts(Dictionary<int, int>? items, string source)
    {
        if (items == null)
            return false;

        if (ApplyCountsNow(items, source))
            return true;

        QueuePendingCounts(items, source);
        return false;
    }

    public static bool ApplyPendingCounts(string source)
    {
        if (PendingCounts.Count == 0)
            return false;

        handlingPacket = true;
        try
        {
            return ApplyCountsNow(new Dictionary<int, int>(PendingCounts), source);
        }
        finally { handlingPacket = false; }
    }

    private static bool ApplyCountsNow(Dictionary<int, int> items, string source)
    {
        if (items.Count == 0)
            return true;

        if (!TryGetGadgetsModel(out var model))
            return false;

        var isRepairSnapshot = IsRepairSource(source);
        var beforeHash = 0;
        var targetHash = 0;
        var changedItems = 0;
        var applied = 0;
        foreach (var item in items)
        {
            if (!actorManager.ActorTypes.TryGetValue(item.Key, out var ident) || !ident)
            {
                SrLogger.LogWarning($"Skipping unknown refinery item type {item.Key} while applying {source}.", SrLogTarget.Main);
                continue;
            }

            var targetCount = Math.Max(0, item.Value);
            if (isRepairSnapshot)
            {
                var beforeCount = GetModelCount(model, ident);
                beforeHash = AddHash(AddHash(beforeHash, item.Key), beforeCount);
                targetHash = AddHash(AddHash(targetHash, item.Key), targetCount);
                if (beforeCount != targetCount)
                    changedItems++;
            }

            model.SetCount(ident, targetCount);
            applied++;
        }

        if (applied > 0)
        {
            try
            {
                model.NotifyParticipants();
            }
            catch (Exception ex)
            {
                SrLogger.LogWarning($"Could not notify refinery participants after applying {source}: {ex.Message}", SrLogTarget.Main);
            }
        }

        if (isRepairSnapshot && changedItems > 0)
        {
            SrLogger.LogMessage(
                $"Repair corrected refinery counts ({changedItems} item(s), {FormatHash(beforeHash)} -> {FormatHash(targetHash)}).",
                SrLogTarget.Main);
        }

        SrLogger.LogMessage($"Applied {applied}/{items.Count} refinery counts from {source}.", SrLogTarget.Main);
        if (applied > 0 || items.Count == 0)
        {
            PendingCounts.Clear();
            return true;
        }

        return false;
    }

    public static bool IsRefineryItem(IdentifiableType ident)
    {
        if (!ident || !TryGetGadgetDirector(out var gadgetDirector))
            return false;

        try
        {
            var refineryTypeGroup = gadgetDirector.RefineryTypeGroup;
            if (refineryTypeGroup && refineryTypeGroup.IsMember(ident))
                return true;

            return gadgetDirector.IsStorable(ident);
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not check refinery item type '{ident.name}': {ex.Message}", SrLogTarget.Main);
            return false;
        }
    }

    private static IEnumerable<IdentifiableType> GetKnownRefineryItems(GadgetDirector gadgetDirector)
    {
        var seen = new HashSet<int>();

        var refineryItems = new List<IdentifiableType>();
        try
        {
            if (gadgetDirector.RefineryTypeGroup)
            {
                var members = Il2CppSystem.Linq.Enumerable.ToArray(gadgetDirector.RefineryTypeGroup.GetAllMembers());
                foreach (IdentifiableType ident in members)
                    refineryItems.Add(ident);
            }
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not enumerate refinery type group: {ex.Message}", SrLogTarget.Main);
        }

        foreach (var ident in refineryItems)
        {
            var id = NetworkActorManager.GetPersistentID(ident);
            if (seen.Add(id))
                yield return ident;
        }

        foreach (var actorType in actorManager.ActorTypes)
        {
            var ident = actorType.Value;
            if (!ident || !IsRefineryItem(ident) || !seen.Add(actorType.Key))
                continue;

            yield return ident;
        }
    }

    private static int GetModelCount(GadgetsModel model, IdentifiableType ident)
    {
        foreach (var itemCount in model._itemCounts)
        {
            if (itemCount.key == ident)
                return Math.Max(0, itemCount.value);
        }

        return 0;
    }

    private static bool IsRepairSource(string source)
        => source.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static int AddHash(int hash, int value)
    {
        unchecked
        {
            return (hash * 397) ^ value;
        }
    }

    private static string FormatHash(int hash)
        => $"0x{hash:X8}";

    private static void QueuePendingCounts(Dictionary<int, int> items, string source)
    {
        foreach (var item in items)
            PendingCounts[item.Key] = item.Value;

        SrLogger.LogWarning($"Queued {items.Count} refinery counts from {source}; model is not ready yet.", SrLogTarget.Main);

        if (pendingApplyRunning)
            return;

        pendingApplyRunning = true;
        MelonCoroutines.Start(ApplyPendingCountsWhenReady(source));
    }

    private static System.Collections.IEnumerator ApplyPendingCountsWhenReady(string source)
    {
        var timeoutAt = Time.realtimeSinceStartup + 10f;
        while (PendingCounts.Count > 0 && Time.realtimeSinceStartup < timeoutAt)
        {
            if (ApplyPendingCounts($"{source} retry"))
                break;

            yield return null;
        }

        if (PendingCounts.Count > 0)
        {
            SrLogger.LogWarning($"Could not apply {PendingCounts.Count} queued refinery counts from {source}; model never became ready.", SrLogTarget.Both);
        }

        pendingApplyRunning = false;
    }

    private static bool TryGetGadgetDirector(out GadgetDirector gadgetDirector)
    {
        gadgetDirector = null!;

        if (!SceneContext.Instance)
            return false;

        gadgetDirector = SceneContext.Instance.GadgetDirector;
        return gadgetDirector != null;
    }

    private static bool TryGetGadgetsModel(out GadgetsModel model)
    {
        model = null!;

        if (TryGetGadgetDirector(out var gadgetDirector) && gadgetDirector._model != null)
        {
            model = gadgetDirector._model;
            return true;
        }

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        model = SceneContext.Instance.GameModel.GadgetsModel;
        return model != null;
    }
}
