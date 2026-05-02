using SR2MP.Packets.Actor;

namespace SR2MP.Shared.Managers;

public static class ActorFeralSyncManager
{
    private const float PendingTimeoutSeconds = 3f;

    private static readonly PendingApplyQueue<long, PendingFeralApply> _pendingQueue =
        new("ActorFeral", PendingTimeoutSeconds);

    public static void ApplyOrQueue(
        ActorFeralPacket packet,
        string source,
        Action? afterApplied = null)
    {
        if (TryApply(packet))
        {
            afterApplied?.Invoke();
            return;
        }

        var entry = new PendingFeralApply(packet, afterApplied);
        _pendingQueue.EnqueueAndStart(
            packet.ActorId.Value,
            entry,
            source,
            (_, data, _) =>
            {
                if (!TryApply(data.Packet))
                    return false;
                data.AfterApplied?.Invoke();
                return true;
            },
            onRepairNeeded: null);
    }

    public static bool ApplyPendingForActor(long actorId)
        => _pendingQueue.TryDrainForKey(actorId);

    public static void Clear() => _pendingQueue.Clear();

    private static bool TryApply(ActorFeralPacket packet)
    {
        if (!actorManager.Actors.TryGetValue(packet.ActorId.Value, out var model))
            return false;

        var go = model.GetGameObject();
        if (!go) return false;

        RunWithHandlingPacket(() =>
        {
            var feral = go.GetComponent<SlimeFeral>() ?? go.AddComponent<SlimeFeral>();
            feral.SetFeral();
        });
        return true;
    }

    private sealed class PendingFeralApply
    {
        public ActorFeralPacket Packet { get; }
        public Action? AfterApplied { get; }

        public PendingFeralApply(ActorFeralPacket packet, Action? afterApplied)
        {
            Packet = packet;
            AfterApplied = afterApplied;
        }
    }
}
