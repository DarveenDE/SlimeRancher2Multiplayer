using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Player.PlayerItems;
using Il2CppMonomiPark.SlimeRancher.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Actor;

[HarmonyPatch]
public static class OnGadgetRemovalAction
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GadgetItem), nameof(GadgetItem.StoreTargettedGadget), typeof(Gadget))]
    public static void StoreTargettedGadget(Gadget gadget)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(gadget, "GadgetItem.StoreTargettedGadget");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GadgetItem), nameof(GadgetItem.PickupGadget), typeof(Gadget))]
    public static void PickupGadget(Gadget gadgetToPickup)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(gadgetToPickup, "GadgetItem.PickupGadget");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GadgetItem), nameof(GadgetItem.PickupOrStoreStackedGadgets), typeof(Gadget), typeof(bool))]
    public static void PickupOrStoreStackedGadgets(Gadget baseGadget)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(baseGadget, "GadgetItem.PickupOrStoreStackedGadgets");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GadgetItem), nameof(GadgetItem.DestroyGadget), typeof(Gadget))]
    public static void DestroyGadgetItem(Gadget gadget)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(gadget, "GadgetItem.DestroyGadget");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Gadget), nameof(Gadget.PickUp), typeof(GadgetModel), typeof(GameObject), typeof(SECTR_AudioCue))]
    public static void PickUp(GadgetModel gadgetModel)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(gadgetModel, "Gadget.PickUp");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Gadget), nameof(Gadget.Demolish), typeof(GadgetModel), typeof(GameObject), typeof(SECTR_AudioCue))]
    public static void Demolish(GadgetModel gadgetModel)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(gadgetModel, "Gadget.Demolish");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Gadget), nameof(Gadget.DemolishPair), typeof(GadgetModel), typeof(GameObject), typeof(SECTR_AudioCue))]
    public static void DemolishPair(GadgetModel gadgetModel)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(gadgetModel, "Gadget.DemolishPair");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Gadget), nameof(Gadget.DestroyGadget), typeof(GameObject), typeof(SECTR_AudioCue), typeof(bool))]
    public static void DestroyGadgetWorld(Gadget __instance)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(__instance, "Gadget.DestroyGadget");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Gadget), nameof(Gadget.GadgetPickedUpOrStored))]
    public static void GadgetPickedUpOrStored(Gadget __instance)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(__instance, "Gadget.GadgetPickedUpOrStored");
    }
}
