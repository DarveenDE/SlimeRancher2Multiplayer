using HarmonyLib;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Plots;

[HarmonyPatch(typeof(LandPlot), nameof(LandPlot.DestroyAttached))]
public static class OnDestroyCrop
{
    public static void Postfix(LandPlot __instance)
    {
        if (!__instance)
            return;

        GardenPlotSyncManager.QueueLocalState(__instance.GetComponentInParent<LandPlotLocation>());
    }
}
