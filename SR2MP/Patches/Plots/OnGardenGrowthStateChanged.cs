using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Plots;

[HarmonyPatch(typeof(SpawnResourceModel), nameof(SpawnResourceModel.Push))]
public static class OnGardenSpawnResourcePushed
{
    // Cache SpawnResourceModel native pointer → owning LandPlotModel.
    // Built lazily on first lookup; invalidated on plot-replace and scene/game-context reload.
    // Replaces the O(n-plots × GetComponentInChildren) scan that ran on every growth tick.
    private static readonly Dictionary<IntPtr, LandPlotModel> _spawnModelToPlot = new();

    /// <summary>Clears the cache. Call on scene/game-context reload and on plot replacement.</summary>
    public static void ResetCache() => _spawnModelToPlot.Clear();

    public static void Postfix(SpawnResourceModel __instance)
    {
        if (!TryFindGardenPlot(__instance, out var plot))
            return;

        GardenGrowthSyncManager.QueueLocalPlotState(plot);
    }

    private static bool TryFindGardenPlot(SpawnResourceModel spawnModel, out LandPlotModel plot)
    {
        plot = null!;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel || spawnModel == null)
            return false;

        var ptr = spawnModel.Pointer;

        // Fast path: already cached.
        if (ptr != IntPtr.Zero && _spawnModelToPlot.TryGetValue(ptr, out plot!))
            return true;

        // Slow path: linear scan — only runs until the cache is warm (once per garden plot).
        foreach (var entry in SceneContext.Instance.GameModel.landPlots)
        {
            var candidate = entry.value;
            if (candidate == null || candidate.typeId != LandPlot.Id.GARDEN || !candidate.gameObj)
                continue;

            var spawnResource = candidate.gameObj.GetComponentInChildren<SpawnResource>();
            if (!spawnResource || spawnResource._model == null)
                continue;

            if (ReferenceEquals(spawnResource._model, spawnModel) || spawnResource._model.Pointer == ptr)
            {
                plot = candidate;
                // Populate cache so subsequent calls for the same spawnModel are O(1).
                if (ptr != IntPtr.Zero)
                    _spawnModelToPlot[ptr] = candidate;
                return true;
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(ProduceModel), nameof(ProduceModel.Push))]
public static class OnGardenProducePushed
{
    public static void Postfix(ProduceModel __instance)
    {
        GardenGrowthSyncManager.QueueLocalProduceState(__instance);
    }
}
