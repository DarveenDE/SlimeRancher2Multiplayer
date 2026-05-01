using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu.Model;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.UI;

[HarmonyPatch(typeof(LoadGameBehaviorModel), nameof(LoadGameBehaviorModel.InvokeBehavior))]
public static class OnMainMenuLoadGame
{
    public static void Prefix(LoadGameBehaviorModel __instance)
    {
        if (!MultiplayerLaunchCoordinator.IsHostSaveSelectionArmed)
            return;

        try
        {
            var summary = __instance.GameDataSummary;
            if (MultiplayerLaunchCoordinator.TryPrepareHostFromSelectedSave(summary))
            {
                string displayName = !string.IsNullOrWhiteSpace(summary.DisplayName)
                    ? summary.DisplayName
                    : summary.Name;
                SrLogger.LogMessage(
                    $"Selected save '{displayName}' for main-menu hosting.",
                    SrLogTarget.Both);
            }
        }
        catch (Exception ex)
        {
            MultiplayerLaunchCoordinator.Cancel("Could not prepare the selected save for hosting.");
            SrLogger.LogWarning($"Failed to prepare main-menu host launch: {ex}", SrLogTarget.Both);
        }
    }
}
