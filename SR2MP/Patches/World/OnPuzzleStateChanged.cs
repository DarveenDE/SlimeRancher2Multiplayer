using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.World;

[HarmonyPatch]
public static class OnPuzzleStateChanged
{
    private const float MissingIdLogIntervalSeconds = 30f;
    private static float lastMissingSlotIdLogAt = -MissingIdLogIntervalSeconds;
    private static float lastMissingDepositorIdLogAt = -MissingIdLogIntervalSeconds;
    private static int suppressedMissingSlotIdLogs;
    private static int suppressedMissingDepositorIdLogs;
    private static readonly Dictionary<IntPtr, bool> LastSlotStates = new();
    private static readonly Dictionary<IntPtr, int> LastDepositorStates = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PuzzleSlotModel), nameof(PuzzleSlotModel.Push))]
    public static void PuzzleSlotFilled(PuzzleSlotModel __instance, bool filled)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        if (!ShouldProcessSlotPush(__instance, filled))
            return;

        if (!PuzzleStateSyncManager.TryGetSlotId(__instance, out var id))
        {
            LogMissingSlotId();
            return;
        }

        Main.SendToAllOrServer(new PuzzleSlotStatePacket
        {
            ID = id,
            Filled = filled,
        });
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlortDepositorModel), nameof(PlortDepositorModel.Push))]
    public static void PlortDeposited(PlortDepositorModel __instance, int amountDeposited)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        if (!ShouldProcessDepositorPush(__instance, amountDeposited))
            return;

        if (!PuzzleStateSyncManager.TryGetDepositorId(__instance, out var id))
        {
            LogMissingDepositorId();
            return;
        }

        Main.SendToAllOrServer(new PlortDepositorStatePacket
        {
            ID = id,
            AmountDeposited = amountDeposited,
        });
    }

    private static bool ShouldProcessSlotPush(PuzzleSlotModel model, bool filled)
    {
        var key = IL2CPP.Il2CppObjectBaseToPtr(model);
        if (key == IntPtr.Zero)
            return true;

        if (LastSlotStates.TryGetValue(key, out var previous) && previous == filled)
            return false;

        LastSlotStates[key] = filled;
        return true;
    }

    private static bool ShouldProcessDepositorPush(PlortDepositorModel model, int amountDeposited)
    {
        var key = IL2CPP.Il2CppObjectBaseToPtr(model);
        if (key == IntPtr.Zero)
            return true;

        if (LastDepositorStates.TryGetValue(key, out var previous) && previous == amountDeposited)
            return false;

        LastDepositorStates[key] = amountDeposited;
        return true;
    }

    private static void LogMissingSlotId()
    {
        var now = UnityEngine.Time.realtimeSinceStartup;
        if (now - lastMissingSlotIdLogAt < MissingIdLogIntervalSeconds)
        {
            suppressedMissingSlotIdLogs++;
            return;
        }

        var suffix = suppressedMissingSlotIdLogs > 0
            ? $" Suppressed {suppressedMissingSlotIdLogs} repeat(s)."
            : string.Empty;
        suppressedMissingSlotIdLogs = 0;
        lastMissingSlotIdLogAt = now;

        SrLogger.LogWarning(
            $"Puzzle slot changed locally but no slot id could be resolved; state was not synced.{suffix}",
            SrLogTarget.Main);
    }

    private static void LogMissingDepositorId()
    {
        var now = UnityEngine.Time.realtimeSinceStartup;
        if (now - lastMissingDepositorIdLogAt < MissingIdLogIntervalSeconds)
        {
            suppressedMissingDepositorIdLogs++;
            return;
        }

        var suffix = suppressedMissingDepositorIdLogs > 0
            ? $" Suppressed {suppressedMissingDepositorIdLogs} repeat(s)."
            : string.Empty;
        suppressedMissingDepositorIdLogs = 0;
        lastMissingDepositorIdLogAt = now;

        SrLogger.LogWarning(
            $"Plort depositor changed locally but no depositor id could be resolved; state was not synced.{suffix}",
            SrLogTarget.Main);
    }
}
