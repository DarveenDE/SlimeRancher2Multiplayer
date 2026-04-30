using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Plots;

[HarmonyPatch(typeof(SpawnResourceModel), nameof(SpawnResourceModel.Push))]
public static class OnGardenSpawnResourcePushed
{
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

        foreach (var entry in SceneContext.Instance.GameModel.landPlots)
        {
            var candidate = entry.value;
            if (candidate == null || candidate.typeId != LandPlot.Id.GARDEN || !candidate.gameObj)
                continue;

            var spawnResource = candidate.gameObj.GetComponentInChildren<SpawnResource>();
            if (!spawnResource || spawnResource._model == null)
                continue;

            if (ReferenceEquals(spawnResource._model, spawnModel) || spawnResource._model.Pointer == spawnModel.Pointer)
            {
                plot = candidate;
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
