using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Player;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Plots;

[HarmonyPatch(typeof(AmmoModel), "OnOnAmmoChanged")]
public static class OnLandPlotAmmoChanged
{
    public static void Postfix(AmmoModel __instance)
    {
        SendState(__instance);
    }

    internal static void SendState(AmmoModel ammoModel)
    {
        if (handlingPacket)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        LandPlotAmmoSyncManager.QueueLocalAmmoSet(ammoModel);
    }
}

[HarmonyPatch(typeof(AmmoModel), nameof(AmmoModel.Push))]
public static class OnLandPlotAmmoPushed
{
    public static void Postfix(AmmoModel __instance)
    {
        OnLandPlotAmmoChanged.SendState(__instance);
    }
}

[HarmonyPatch(typeof(AmmoSlotManager), nameof(AmmoSlotManager.MaybeAddToAnySlot), new Type[] { typeof(AmmoSlot.AmmoMetadata), typeof(bool) })]
public static class OnLandPlotAmmoManagerAddedAnySlot
{
    public static void Postfix(AmmoSlotManager __instance, bool __result)
    {
        if (__result)
            SendManagerState(__instance);
    }

    internal static void SendManagerState(AmmoSlotManager manager)
    {
        if (manager == null || manager._ammoModel == null)
            return;

        OnLandPlotAmmoChanged.SendState(manager._ammoModel);
    }
}

[HarmonyPatch(typeof(AmmoSlotManager), nameof(AmmoSlotManager.MaybeAddToSpecificSlot), new Type[] { typeof(AmmoSlot.AmmoMetadata), typeof(int), typeof(int), typeof(bool) })]
public static class OnLandPlotAmmoManagerAddedSpecificSlot
{
    public static void Postfix(AmmoSlotManager __instance, bool __result)
    {
        if (__result)
            OnLandPlotAmmoManagerAddedAnySlot.SendManagerState(__instance);
    }
}

[HarmonyPatch(typeof(AmmoSlotManager), nameof(AmmoSlotManager.PopFromSelectedSlot), new Type[] { })]
public static class OnLandPlotAmmoManagerPoppedSlot
{
    public static void Postfix(AmmoSlotManager __instance)
    {
        OnLandPlotAmmoManagerAddedAnySlot.SendManagerState(__instance);
    }
}
