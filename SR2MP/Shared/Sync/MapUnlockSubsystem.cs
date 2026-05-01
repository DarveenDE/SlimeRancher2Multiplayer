using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Sync;

/// <summary>
/// <see cref="ISyncedSubsystem"/> pilot for map-unlock state.
///
/// Replaces the separate Initial-Sync path (<c>SendMapPacket</c> in ConnectHandler)
/// and Repair path (<c>SendMapUnlockSnapshots</c> in WorldStateRepairManager) with a
/// single <see cref="CaptureSnapshot"/> / <see cref="ApplySnapshot"/> pair.
///
/// Live events (<c>MapUnlockPacket</c>) still flow through the existing
/// <c>MapUnlockHandler</c> — that is unchanged.
/// </summary>
public sealed class MapUnlockSubsystem : ISyncedSubsystem
{
    public static readonly MapUnlockSubsystem Instance = new();

    private MapUnlockSubsystem() { }

    public byte Id => SubsystemIds.MapUnlock;
    public string Name => "MapUnlock";

    /// <summary>Serialises the complete set of unlocked map node IDs.</summary>
    public void CaptureSnapshot(PacketWriter writer)
    {
        var nodes = MapUnlockSyncManager.CreateSnapshot();
        writer.WriteList(nodes, PacketWriterDels.String);
    }

    /// <summary>
    /// Deserialises a node-ID list and replaces the local map-unlock table.
    /// Idempotent — running it twice produces the same final state.
    /// </summary>
    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var nodes = reader.ReadList(PacketReaderDels.String);
        MapUnlockSyncManager.ReplaceSnapshot(nodes, source.ToSourceString());
    }
}
