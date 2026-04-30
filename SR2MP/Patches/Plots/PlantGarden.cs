using HarmonyLib;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Plots;

[HarmonyPatch(typeof(GardenCatcher), nameof(GardenCatcher.Plant))]
public static class PlantGarden
{
    public static void Postfix(GardenCatcher __instance, GameObject __result)
    {
        if (!__result)
            return;

        var location = __instance.GetComponentInParent<LandPlotLocation>();
        GardenPlotSyncManager.QueueLocalState(location);

        if (location
            && SceneContext.Instance
            && SceneContext.Instance.GameModel
            && SceneContext.Instance.GameModel.landPlots.TryGetValue(location._id, out var model))
        {
            GardenGrowthSyncManager.QueueLocalPlotState(model);
        }
    }
}
