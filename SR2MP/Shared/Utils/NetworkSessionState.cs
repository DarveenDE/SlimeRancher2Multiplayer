using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Utils;

public static class NetworkSessionState
{
    /// <summary>
    /// Single gate instance that tracks the client's connection phase and owns echo-suppression.
    /// Replaces the scattered <c>handlingPacket</c> flag and <c>initialActorLoadInProgress</c> checks.
    /// </summary>
    public static readonly SessionPhaseGate PhaseGate = new SessionPhaseGate();

    private static int initialActorLoadInProgress;
    private static long assignedActorIdRangeMin;
    private static long assignedActorIdRangeMax;

    public static bool InitialActorLoadInProgress
        => System.Threading.Volatile.Read(ref initialActorLoadInProgress) > 0;

    public static void BeginInitialActorLoad()
        => System.Threading.Interlocked.Increment(ref initialActorLoadInProgress);

    public static void EndInitialActorLoad()
    {
        if (System.Threading.Interlocked.Decrement(ref initialActorLoadInProgress) < 0)
            System.Threading.Volatile.Write(ref initialActorLoadInProgress, 0);
    }

    public static void SetAssignedActorIdRange(long minActorId, long maxActorId)
    {
        System.Threading.Volatile.Write(ref assignedActorIdRangeMin, minActorId);
        System.Threading.Volatile.Write(ref assignedActorIdRangeMax, maxActorId);
    }

    public static bool TryGetAssignedActorIdRange(out long minActorId, out long maxActorId)
    {
        minActorId = System.Threading.Volatile.Read(ref assignedActorIdRangeMin);
        maxActorId = System.Threading.Volatile.Read(ref assignedActorIdRangeMax);
        return minActorId > 0 && maxActorId > minActorId;
    }

    public static bool IsActorIdInAssignedClientRange(long actorId)
        => TryGetAssignedActorIdRange(out var minActorId, out var maxActorId)
           && actorId >= minActorId
           && actorId < maxActorId;

    public static void ClearTransientSyncState()
    {
        PacketDeduplication.Clear();
        PacketChunkManager.Clear();
        ActorUpdateSyncManager.Clear();
        GardenGrowthSyncManager.Clear();
        GardenResourceAttachSyncManager.Clear();
        actorManager.ActorOwners.Clear();
        System.Threading.Volatile.Write(ref initialActorLoadInProgress, 0);
        System.Threading.Volatile.Write(ref assignedActorIdRangeMin, 0L);
        System.Threading.Volatile.Write(ref assignedActorIdRangeMax, 0L);
        PhaseGate.EchoSuppressed = false;
        PhaseGate.TryTransition(SessionPhase.Disconnected, "ClearTransientSyncState");
    }
}
