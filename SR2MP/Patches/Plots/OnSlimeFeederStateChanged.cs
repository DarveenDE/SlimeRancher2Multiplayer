using HarmonyLib;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Plots;

[HarmonyPatch(typeof(SlimeFeeder), nameof(SlimeFeeder.SetFeederSpeed))]
public static class OnSlimeFeederSpeedChanged
{
    public static void Postfix(SlimeFeeder __instance)
    {
        SendState(__instance);
    }

    internal static void SendState(SlimeFeeder feeder)
    {
        if (handlingPacket)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        if (!feeder || feeder._model == null)
            return;

        LandPlotFeederSyncManager.QueueLocalState(feeder._model);
    }
}

[HarmonyPatch(typeof(FeederSpeedSelector), nameof(FeederSpeedSelector.Activate))]
public static class OnFeederSpeedSelectorActivated
{
    public static void Postfix(FeederSpeedSelector __instance)
    {
        if (!__instance || !__instance.Feeder)
            return;

        OnSlimeFeederSpeedChanged.SendState(__instance.Feeder);
    }
}

[HarmonyPatch(typeof(SlimeFeeder), "ProcessFeedOperation")]
public static class OnSlimeFeederProcessedFeed
{
    public static void Postfix(SlimeFeeder __instance)
    {
        OnSlimeFeederSpeedChanged.SendState(__instance);
    }
}

[HarmonyPatch(typeof(SlimeFeeder), nameof(SlimeFeeder.ProcessFeedOperationFastForward))]
public static class OnSlimeFeederProcessedFastForwardFeed
{
    public static void Postfix(SlimeFeeder __instance)
    {
        OnSlimeFeederSpeedChanged.SendState(__instance);
    }
}
