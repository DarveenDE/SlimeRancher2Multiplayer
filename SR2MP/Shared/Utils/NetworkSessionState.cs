using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Utils;

public static class NetworkSessionState
{
    private static int initialActorLoadInProgress;

    public static bool InitialActorLoadInProgress
        => System.Threading.Volatile.Read(ref initialActorLoadInProgress) > 0;

    public static void BeginInitialActorLoad()
        => System.Threading.Interlocked.Increment(ref initialActorLoadInProgress);

    public static void EndInitialActorLoad()
    {
        if (System.Threading.Interlocked.Decrement(ref initialActorLoadInProgress) < 0)
            System.Threading.Volatile.Write(ref initialActorLoadInProgress, 0);
    }

    public static void ClearTransientSyncState()
    {
        PacketDeduplication.Clear();
        PacketChunkManager.Clear();
        ActorUpdateSyncManager.Clear();
        GardenGrowthSyncManager.Clear();
        GardenResourceAttachSyncManager.Clear();
        actorManager.ActorOwners.Clear();
        System.Threading.Volatile.Write(ref initialActorLoadInProgress, 0);
        handlingPacket = false;
    }
}
