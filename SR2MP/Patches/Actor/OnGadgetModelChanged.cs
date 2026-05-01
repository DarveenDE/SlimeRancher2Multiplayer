using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.SceneManagement;
using Il2CppMonomiPark.SlimeRancher.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Actor;

[HarmonyPatch]
public static class OnGadgetModelChanged
{
    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(GameModel),
        nameof(GameModel.CreateGadgetModel),
        typeof(GadgetDefinition),
        typeof(ActorId),
        typeof(SceneGroup),
        typeof(Vector3),
        typeof(bool))]
    public static void Created(GadgetModel __result, bool isPrePlaced)
    {
        if (isPrePlaced)
            return;

        GadgetModelSyncManager.QueueLocalGadgetSpawn(__result, "GameModel.CreateGadgetModel");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameModel), nameof(GameModel.DestroyGadgetModel), typeof(GadgetModel))]
    public static void DestroyedByModel(GadgetModel model)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(model, "GameModel.DestroyGadgetModel(model)");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameModel), nameof(GameModel.DestroyGadgetModel), typeof(ActorId), typeof(GadgetDefinition))]
    public static void DestroyedByActorId(ActorId actorId)
    {
        GadgetModelSyncManager.SendLocalGadgetDestroy(actorId, "GameModel.DestroyGadgetModel(actorId)");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameModel), nameof(GameModel.DestroyGadgetModel), typeof(GameObject))]
    public static void DestroyedByGameObject(GameObject gameObj)
    {
        if (!gameObj)
            return;

        var gadget = gameObj.GetComponent<Gadget>();
        var model = gadget ? gadget.GetModel() : null;
        GadgetModelSyncManager.SendLocalGadgetDestroy(model, "GameModel.DestroyGadgetModel(gameObject)");
    }
}
