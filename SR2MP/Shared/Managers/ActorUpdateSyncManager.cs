using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Slime;
using MelonLoader;
using SR2MP.Packets.Actor;

namespace SR2MP.Shared.Managers;

public static class ActorUpdateSyncManager
{
    private const float PendingUpdateTimeoutSeconds = 3f;
    private static readonly Dictionary<long, PendingActorUpdate> PendingUpdates = new();
    private static bool pendingApplyRunning;

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
        if (!PendingUpdates.TryGetValue(actorId, out var pending))
            return false;

        if (!TryApply(pending.Packet, $"{pending.Source} retry"))
            return false;

        PendingUpdates.Remove(actorId);
        pending.AfterApplied?.Invoke(pending.Packet);
        SrLogger.LogDebug($"Applied queued actor update for actor {actorId}.", SrLogTarget.Main);
        return true;
    }

    public static void Clear()
    {
        PendingUpdates.Clear();
        pendingApplyRunning = false;
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
            networkComponent.GetComponent<SlimeEmotions>().SetAll(packet.Emotions);

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

        if (PendingUpdates.TryGetValue(actorId, out var existing))
        {
            existing.Update(packet, afterApplied);
            return;
        }

        PendingUpdates[actorId] = new PendingActorUpdate(
            packet,
            source,
            Time.realtimeSinceStartup + PendingUpdateTimeoutSeconds,
            afterApplied);

        SrLogger.LogDebug($"Queued actor update from {source}; actor {actorId} does not exist yet.", SrLogTarget.Main);

        if (pendingApplyRunning)
            return;

        pendingApplyRunning = true;
        MelonCoroutines.Start(ApplyPendingUpdatesWhenReady());
    }

    private static IEnumerator ApplyPendingUpdatesWhenReady()
    {
        while (PendingUpdates.Count > 0)
        {
            var now = Time.realtimeSinceStartup;
            var pending = PendingUpdates.Values.ToList();
            foreach (var item in pending)
            {
                if (ApplyPendingForActor(item.ActorId))
                    continue;

                if (now < item.TimeoutAt)
                    continue;

                PendingUpdates.Remove(item.ActorId);
                SrLogger.LogDebug(
                    $"Dropped queued actor update for actor {item.ActorId}; actor did not spawn within {PendingUpdateTimeoutSeconds:0.#}s.",
                    SrLogTarget.Main);
            }

            if (PendingUpdates.Count > 0)
                yield return null;
        }

        pendingApplyRunning = false;
    }

    private sealed class PendingActorUpdate
    {
        public PendingActorUpdate(
            ActorUpdatePacket packet,
            string source,
            float timeoutAt,
            Action<ActorUpdatePacket>? afterApplied)
        {
            Packet = packet;
            Source = source;
            TimeoutAt = timeoutAt;
            AfterApplied = afterApplied;
        }

        public long ActorId => Packet.ActorId.Value;
        public ActorUpdatePacket Packet { get; private set; }
        public string Source { get; }
        public float TimeoutAt { get; }
        public Action<ActorUpdatePacket>? AfterApplied { get; private set; }

        public void Update(ActorUpdatePacket packet, Action<ActorUpdatePacket>? afterApplied)
        {
            Packet = packet;
            AfterApplied = afterApplied;
        }
    }
}
