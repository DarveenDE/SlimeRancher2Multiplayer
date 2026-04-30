using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.UI.Map;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Map;

[HarmonyPatch(typeof(MapUIFogReveal), nameof(MapUIFogReveal.RevealZonesInMap))]
public static class OnMapFogRevealRefresh
{
    public static void Postfix()
    {
        MapUnlockSyncManager.RefreshVisibleFogElements();
    }
}
