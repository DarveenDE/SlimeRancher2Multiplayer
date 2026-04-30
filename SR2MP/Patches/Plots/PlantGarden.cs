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

        GardenPlotSyncManager.QueueLocalState(__instance.GetComponentInParent<LandPlotLocation>());
    }
}
