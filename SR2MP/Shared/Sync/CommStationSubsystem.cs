using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Sync;

/// <summary>
/// <see cref="ISyncedSubsystem"/> for comm-station played state.
///
/// Replaces the separate Initial-Sync path (<c>SendCommStationPacket</c> in ConnectHandler)
/// and Repair path (<c>SendCommStationSnapshots</c> in WorldStateRepairManager) with a
/// single <see cref="CaptureSnapshot"/> / <see cref="ApplySnapshot"/> pair.
///
/// Live events (<c>CommStationPlayedPacket</c> with a single entry) still flow through
/// the existing <c>CommStationPlayedHandler</c> — that is unchanged.
/// </summary>
public sealed class CommStationSubsystem : ISyncedSubsystem
{
    public static readonly CommStationSubsystem Instance = new();

    private CommStationSubsystem() { }

    public byte Id => SubsystemIds.CommStation;
    public string Name => "CommStation";

    /// <summary>Serialises the complete set of played comm-station entries.</summary>
    public void CaptureSnapshot(PacketWriter writer)
    {
        var entries = CommStationSyncManager.CreateSnapshot();
        writer.WriteList(entries, PacketWriterDels.NetObject<CommStationPlayedPacket.Entry>.Func);
    }

    /// <summary>
    /// Deserialises and applies all played comm-station entries.
    /// Idempotent — playing an already-played entry is a no-op.
    /// </summary>
    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var entries = reader.ReadList(PacketReaderDels.NetObject<CommStationPlayedPacket.Entry>.Func);
        CommStationSyncManager.Apply(new CommStationPlayedPacket
        {
            Entries = entries,
            IsRepairSnapshot = source == SyncSource.Repair,
        }, source.ToSourceString());
    }
}
