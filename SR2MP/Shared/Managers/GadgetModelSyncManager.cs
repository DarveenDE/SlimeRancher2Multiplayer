using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.World;
using MelonLoader;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Utils;

namespace SR2MP.Shared.Managers;

public static class GadgetModelSyncManager
{
    private const int SpawnReadinessMaxFrames = 60;
    private static readonly HashSet<long> QueuedOrSentSpawns = new();
    private static readonly HashSet<long> SentDestroys = new();

    public static void QueueLocalGadgetSpawn(GadgetModel? model, string source)
    {
        if (!ShouldSendLocalGadgetMutation() || model == null)
            return;

        var actorId = model.actorId.Value;
        if (actorId == 0)
            return;

        if (!QueuedOrSentSpawns.Add(actorId))
            return;

        MelonCoroutines.Start(SendLocalGadgetSpawnWhenReady(model, source));
    }

    public static void SendLocalGadgetDestroy(GadgetModel? model, string source)
    {
        if (model == null)
            return;

        SendLocalGadgetDestroy(model.actorId, source);
        SendLocalLinkedAndStackedGadgetDestroys(model, source);
    }

    public static void SendLocalGadgetDestroy(Gadget? gadget, string source)
    {
        if (gadget == null)
            return;

        GadgetModel? model = null;
        try
        {
            model = gadget.GetModel();
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not read gadget model for destroy from {source}: {ex.Message}", SrLogTarget.Main);
        }

        if (model != null)
        {
            SendLocalGadgetDestroy(model, source);
            return;
        }

        try
        {
            SendLocalGadgetDestroy(gadget.GetActorId(), $"{source}:gadgetActorId");
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not read gadget actor id for destroy from {source}: {ex.Message}", SrLogTarget.Main);
        }
    }

    public static void SendLocalGadgetDestroy(ActorId actorId, string source)
    {
        if (!ShouldSendLocalGadgetMutation() || actorId.Value == 0)
            return;

        if (!SentDestroys.Add(actorId.Value))
            return;

        SrLogger.LogMessage($"Sending gadget destroy actor={actorId.Value} from {source}.", SrLogTarget.Main);
        Main.SendToAllOrServer(new ActorDestroyPacket
        {
            ActorId = actorId
        });
    }

    private static void SendLocalLinkedAndStackedGadgetDestroys(GadgetModel model, string source)
    {
        try
        {
            var linkedModel = model.TryCast<TeleporterGadgetModel>()?.GetLinkedGadget();
            if (linkedModel != null && linkedModel.actorId.Value != model.actorId.Value)
                SendLocalGadgetDestroy(linkedModel.actorId, $"{source}:linked");
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not inspect linked gadget destroy from {source}: {ex.Message}", SrLogTarget.Main);
        }

        try
        {
            if (model.StackedGadgetsIds == null)
                return;

            foreach (var stackedActorId in model.StackedGadgetsIds)
            {
                if (stackedActorId.Value != model.actorId.Value)
                    SendLocalGadgetDestroy(stackedActorId, $"{source}:stacked");
            }
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not inspect stacked gadget destroys from {source}: {ex.Message}", SrLogTarget.Main);
        }
    }

    private static IEnumerator SendLocalGadgetSpawnWhenReady(GadgetModel model, string source)
    {
        for (var frame = 0; frame < SpawnReadinessMaxFrames; frame++)
        {
            yield return null;

            if (!ShouldSendLocalGadgetMutation())
                yield break;

            if (model.actorId.Value != 0 && model.ident != null && model.sceneGroup != null)
            {
                SendLocalGadgetSpawn(model, source);
                yield break;
            }
        }

        QueuedOrSentSpawns.Remove(model.actorId.Value);
        SrLogger.LogWarning(
            $"Not sending gadget spawn from {source}; gadget model was not ready after {SpawnReadinessMaxFrames} frame(s).",
            SrLogTarget.Main);
    }

    private static void SendLocalGadgetSpawn(GadgetModel model, string source)
    {
        var actorId = model.actorId.Value;
        if (Main.Client.IsConnected
            && NetworkSessionState.TryGetAssignedActorIdRange(out var minActorId, out var maxActorId)
            && (actorId < minActorId || actorId >= maxActorId))
        {
            SrLogger.LogWarning(
                $"Not sending gadget spawn from {source} for actor {actorId}; local id is outside assigned range [{minActorId}, {maxActorId}).",
                SrLogTarget.Both);
            return;
        }

        if (Main.Client.IsConnected && !NetworkSessionState.TryGetAssignedActorIdRange(out _, out _))
        {
            SrLogger.LogWarning($"Not sending gadget spawn from {source} for actor {actorId}; no client actor-id range has been assigned yet.", SrLogTarget.Both);
            return;
        }

        actorManager.Actors[actorId] = model;
        if (Main.Server.IsRunning())
            actorManager.SetActorOwner(actorId, LocalID);

        var actorType = NetworkActorManager.GetPersistentID(model.ident);
        var sceneGroup = NetworkSceneManager.GetPersistentID(model.sceneGroup);

        SrLogger.LogMessage(
            $"Sending gadget spawn actor={actorId}, type={actorType}, scene={sceneGroup}, source={source}.",
            SrLogTarget.Main);

        Main.SendToAllOrServer(new ActorSpawnPacket
        {
            ActorId = model.actorId,
            ActorType = actorType,
            SceneGroup = sceneGroup,
            Position = model.GetPos(),
            Rotation = model.GetRot()
        });
    }

    private static bool ShouldSendLocalGadgetMutation()
    {
        if (handlingPacket || NetworkSessionState.InitialActorLoadInProgress)
            return false;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return false;

        return !SystemContext.Instance.SceneLoader.IsSceneLoadInProgress;
    }
}
