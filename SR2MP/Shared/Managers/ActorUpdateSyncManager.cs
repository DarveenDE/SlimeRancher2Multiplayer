using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Slime;
using SR2MP.Packets.Actor;

namespace SR2MP.Shared.Managers;

public static class ActorUpdateSyncManager
{
    private const float PendingUpdateTimeoutSeconds = 3f;

    // Centralised pending queue — replaces the per-manager Dictionary + bool + coroutine pattern.
    private static readonly PendingApplyQueue<long, PendingActorUpdate> _pendingQueue =
        new("ActorUpdate", PendingUpdateTimeoutSeconds);

    public static bool ApplyOrQueue(
        ActorUpdatePacket packet,
        string source,
        Action<ActorUpdatePacket>? afterApplied = null)
    {
        if (TryApply(packet, source))
        {
            afterApplied?.Invoke(packet);
            return true;
        }

        Queue(packet, source, afterApplied);
        return false;
    }

    public static bool ApplyPendingForActor(long actorId)
    {
        return _pendingQueue.TryDrainForKey(actorId);
    }

    public static void Clear()
    {
        _pendingQueue.Clear();
    }

    private static bool TryApply(ActorUpdatePacket packet, string source)
    {
        if (!actorManager.Actors.TryGetValue(packet.ActorId.Value, out var model))
            return false;

        var actor = model.TryCast<ActorModel>();
        if (actor == null)
        {
            SrLogger.LogWarning($"Skipping actor update from {source}; actor {packet.ActorId.Value} is not an ActorModel.", SrLogTarget.Main);
            return false;
        }

        actor.lastPosition = packet.Position;
        actor.lastRotation = packet.Rotation;

        var slime = actor.TryCast<SlimeModel>();
        if (slime != null)
            slime.Emotions = packet.Emotions;

        if (!actor.TryGetNetworkComponent(out var networkComponent))
            return true;

        networkComponent.SavedVelocity = packet.Velocity;
        networkComponent.nextPosition = packet.Position;
        networkComponent.nextRotation = packet.Rotation;

        if (networkComponent.regionMember?._hibernating == true)
        {
            networkComponent.transform.position = packet.Position;
            networkComponent.transform.rotation = packet.Rotation;
        }

        if (slime != null)
        {
            var slimeEmotions = networkComponent.GetComponent<SlimeEmotions>();
            if (slimeEmotions != null)
            {
                slimeEmotions.SetAll(packet.Emotions);
                ApplyMoodFlags(slimeEmotions, packet.MoodFlags);
            }
        }

        return true;
    }

    private static void Queue(
        ActorUpdatePacket packet,
        string source,
        Action<ActorUpdatePacket>? afterApplied)
    {
        var actorId = packet.ActorId.Value;
        if (actorId == 0)
            return;

        SrLogger.LogDebug($"Queued actor update from {source}; actor {actorId} does not exist yet.", SrLogTarget.Main);

        var entry = new PendingActorUpdate(packet, source, afterApplied);
        _pendingQueue.EnqueueAndStart(
            actorId,
            entry,
            source,
            (key, data, src) =>
            {
                if (!TryApply(data.Packet, src))
                    return false;

                data.AfterApplied?.Invoke(data.Packet);
                return true;
            },
            // Do NOT request a repair snapshot on timeout: the repair snapshot does not include
            // actor spawns, so it never resolves a missing actor. Triggering a repair here only
            // causes periodic ~70 ms main-thread spikes and a burst of 11 reliable packets to the
            // client every ~15 s, which is the primary cause of the observed 10-second client freeze.
            onRepairNeeded: null);
    }

    private static void ApplyMoodFlags(SlimeEmotions slimeEmotions, byte moodFlags)
    {
        // Bit-flags provide discrete state hints for animation triggers beyond the continuous
        // emotion floats. Currently we use them as an extra nudge to override the emotion model
        // so that threshold-based animations (e.g. hunger bark) reflect the owner's state.
        var e = slimeEmotions._model.Emotions;
        if ((moodFlags & 0x01) != 0 && e.x < 0.5f) e.x = 0.5f;   // hungry
        if ((moodFlags & 0x02) != 0 && e.y < 0.5f) e.y = 0.5f;   // agitated
        if ((moodFlags & 0x04) != 0 && e.z < 0.7f) e.z = 0.7f;   // sleeping
        slimeEmotions._model.Emotions = e;
    }

    private sealed class PendingActorUpdate
    {
        public PendingActorUpdate(
            ActorUpdatePacket packet,
            string source,
            Action<ActorUpdatePacket>? afterApplied)
        {
            Packet = packet;
            Source = source;
            AfterApplied = afterApplied;
        }

        public ActorUpdatePacket Packet { get; set; }
        public string Source { get; }
        public Action<ActorUpdatePacket>? AfterApplied { get; set; }
    }
}

