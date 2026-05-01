using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu.Model;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.UI;

[HarmonyPatch(typeof(LoadGameBehaviorModel), nameof(LoadGameBehaviorModel.InvokeBehavior))]
public static class OnMainMenuLoadGame
{
    public static void Prefix(LoadGameBehaviorModel __instance)
    {
        if (!MultiplayerLaunchCoordinator.IsSaveSelectionArmed)
            return;

        try
        {
            var summary = __instance.GameDataSummary;
            bool prepared = MultiplayerLaunchCoordinator.IsHostSaveSelectionArmed
                ? MultiplayerLaunchCoordinator.TryPrepareHostFromSelectedSave(summary)
                : MultiplayerLaunchCoordinator.TryPrepareJoinFromSelectedSave(summary);

            if (prepared)
            {
                SrLogger.LogMessage(
                    $"Selected save '{FormatSummary(summary)}' for main-menu multiplayer launch.",
                    SrLogTarget.Both);
            }
        }
        catch (Exception ex)
        {
            MultiplayerLaunchCoordinator.Cancel("Could not prepare the selected save for multiplayer.");
            SrLogger.LogWarning($"Failed to prepare main-menu multiplayer launch: {ex}", SrLogTarget.Both);
        }
    }

    private static string FormatSummary(Il2CppMonomiPark.SlimeRancher.Persist.Summary? summary)
    {
        if (summary == null)
            return "unknown save";

        if (!string.IsNullOrWhiteSpace(summary.DisplayName))
            return summary.DisplayName;

        if (!string.IsNullOrWhiteSpace(summary.Name))
            return summary.Name;

        return summary.SaveName ?? "unknown save";
    }
}
