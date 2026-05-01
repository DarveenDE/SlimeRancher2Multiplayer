using System.Collections;
using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.SceneManagement;
using Il2CppMonomiPark.SlimeRancher.World;
using MelonLoader;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.Actor;

[HarmonyPatch(typeof(GadgetDirector), nameof(GadgetDirector.InstantiateGadget))]
public static class OnGadgetPlaced
{
    private const int GadgetModelInitMaxFrames = 60;

    private static IEnumerator SendSpawnAfterModelInit(GameObject gadgetObject)
    {
        Gadget? gadget = null;
        GadgetModel? model = null;

        for (var frame = 0; frame < GadgetModelInitMaxFrames; frame++)
        {
            yield return null;

            if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected) || !gadgetObject)
                yield break;

            gadget = gadgetObject.GetComponent<Gadget>();
            if (!gadget)
                continue;

            model = gadget.GetModel();
            if (model != null && model.actorId.Value != 0 && model.ident != null && model.sceneGroup != null)
                break;
        }

        if (model == null || model.actorId.Value == 0 || model.ident == null || model.sceneGroup == null)
        {
            SrLogger.LogWarning("Not sending gadget spawn; gadget model was not ready after waiting for initialization.", SrLogTarget.Main);
            yield break;
        }

        GadgetModelSyncManager.QueueLocalGadgetSpawn(model, "GadgetDirector.InstantiateGadget");
    }

    public static void Postfix(
        GameObject __result,
        GameObject original,
        SceneGroup sceneGroup,
        Vector3 position,
        Quaternion rotation,
        bool spawnImmediate,
        bool isPrePlaced)
    {
        if (handlingPacket) return;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;
        if (SystemContext.Instance.SceneLoader.IsSceneLoadInProgress) return;
        if (isPrePlaced || !__result || !original || !sceneGroup) return;

        MelonCoroutines.Start(SendSpawnAfterModelInit(__result));
    }
}
