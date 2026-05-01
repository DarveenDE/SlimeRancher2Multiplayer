using HarmonyLib;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Actor;

[HarmonyPatch(typeof(ResourceCycle), nameof(ResourceCycle.Attach))]
public static class OnResourceAttach
{
    public static bool Prefix(ResourceCycle __instance, Joint joint)
    {
        if (handlingPacket) return true;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return true;
        if (SystemContext.Instance.SceneLoader.IsSceneLoadInProgress) return true;
        if (!__instance || !joint) return true;

        if (!Main.Server.IsRunning() && GardenResourceAttachSyncManager.IsGardenJoint(joint))
        {
            GardenResourceAttachSyncManager.DestroyLocalResource(
                __instance,
                "SR2MP.OnResourceAttach.ClientGardenAttachSuppressed");
            return false;
        }

        if (joint.connectedBody)
        {
            var other = joint.connectedBody.GetComponent<ResourceCycle>();
            if (!other || other == __instance || other._model == null)
                return true;

            SceneContext.Instance.GameModel.identifiables.Remove(other._model.actorId);
            if (SceneContext.Instance.GameModel.identifiablesByIdent.TryGetValue(other._model.ident, out var actorsByIdent))
                actorsByIdent.Remove(other._model);

            SceneContext.Instance.GameModel.DestroyIdentifiableModel(other._model);
            actorManager.Actors.Remove(other._model.actorId.Value);
            actorManager.ClearActorOwner(other._model.actorId.Value);

            RunWithHandlingPacket(() => Destroyer.DestroyActor(other.gameObject, "SR2MP.OnResourceAttach"));
            joint.connectedBody = null;
            return true;
        }

        return true;
    }

    public static void Postfix(ResourceCycle __instance, Joint joint)
    {
        if (handlingPacket) return;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;
        if (SystemContext.Instance.SceneLoader.IsSceneLoadInProgress) return;
        if (!__instance || !joint) return;
        if (!Main.Server.IsRunning() && GardenResourceAttachSyncManager.IsGardenJoint(joint)) return;

        var rigidbody = __instance.GetComponent<Rigidbody>();
        if (!rigidbody || joint.connectedBody != rigidbody)
            return;

        if (GardenResourceAttachSyncManager.TryCreatePacket(__instance, joint, out var packet))
            Main.SendToAllOrServer(packet);
    }
}
