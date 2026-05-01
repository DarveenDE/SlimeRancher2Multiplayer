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

        SrLogger.LogWarning($"Gadget at {source} has no model; falling back to GetActorId().", SrLogTarget.Main);
        try
        {
            SendLocalGadgetDestroy(gadget.GetActorId(), $"{source}:gadgetActorId");
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not read gadget actor id for destroy from {source}: {ex.Message}", SrLogTarget.Main);
        }
    }

    /// <summary>
    /// Marks <paramref name="actorId"/> as already-handled so that Harmony patches fired
    /// as a side-effect of processing an incoming <c>ActorDestroyPacket</c> do not echo the
    /// packet back to the network.  Call this in <c>ActorDestroyHandler</c> before
    /// calling <c>DestroyGadgetModel</c>.
    /// </summary>
    public static void MarkDestroyHandled(long actorId) => SentDestroys.Add(actorId);

    public static void SendLocalGadgetDestroy(ActorId actorId, string source)
    {
        if (actorId.Value == 0)
            return;

        // NOTE: we intentionally do NOT check handlingPacket here.
        // Gadget destroys can legitimately fire as side-effects while a different packet is
        // being handled (e.g. the game's stacking logic removes an existing gadget when a
        // new one is instantiated via TrySpawnNetworkGadget).  Suppressing them would leave
        // the gadget visible on the remote side.
        // Echo-prevention is handled by MarkDestroyHandled + SentDestroys in ActorDestroyHandler.
        if (!ShouldSendLocalGadgetDestroy())
            return;

        if (!SentDestroys.Add(actorId.Value))
        {
            SrLogger.LogMessage(
                $"Gadget destroy dedup-suppressed actor={actorId.Value} from {source} (already sent this session).",
                SrLogTarget.Main);
            return;
        }

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

    /// <summary>
    /// Clears session-scoped dedup state. Call when a new game context loads (save reloaded,
    /// new session started) so actor IDs from the previous session do not suppress destroy or
    /// spawn packets for identically-numbered actors in the new session.
    /// </summary>
    public static void Reset()
    {
        QueuedOrSentSpawns.Clear();
        SentDestroys.Clear();
    }

    private static bool ShouldSendLocalGadgetMutation()
    {
        if (handlingPacket || NetworkSessionState.InitialActorLoadInProgress)
            return false;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return false;

        return !SystemContext.Instance.SceneLoader.IsSceneLoadInProgress;
    }

    // Destroys intentionally omit the handlingPacket check — see SendLocalGadgetDestroy.
    private static bool ShouldSendLocalGadgetDestroy()
    {
        if (NetworkSessionState.InitialActorLoadInProgress)
            return false;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return false;

        return !SystemContext.Instance.SceneLoader.IsSceneLoadInProgress;
    }
}
