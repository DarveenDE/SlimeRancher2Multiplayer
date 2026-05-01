using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Sync;

/// <summary>
/// <see cref="ISyncedSubsystem"/> for refinery item counts.
///
/// Replaces:
/// - <c>SendRefineryItemsPacket</c> (ConnectHandler Initial-Sync)
/// - <c>SendRefinerySnapshot</c> (WorldStateRepairManager Repair)
///
/// Live events (<c>RefineryItemCountsPacket</c>) still flow through existing handlers.
/// </summary>
public sealed class RefinerySubsystem : ISyncedSubsystem
{
    public static readonly RefinerySubsystem Instance = new();

    private RefinerySubsystem() { }

    public byte Id => SubsystemIds.Refinery;
    public string Name => "Refinery";

    /// <summary>Serialises the complete refinery item count map (including zero counts).</summary>
    public void CaptureSnapshot(PacketWriter writer)
    {
        var items = RefinerySyncManager.CreateSnapshot(includeZeroCounts: true, logSummary: false);
        writer.WriteDictionary(
            items,
            static (w, key) => w.WriteInt(key),
            static (w, val) => w.WriteInt(val));
    }

    /// <summary>
    /// Deserialises and applies all refinery item counts.
    /// Idempotent — setting counts to the current values is a no-op.
    /// </summary>
    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var items = reader.ReadDictionary(
            static r => r.ReadInt(),
            static r => r.ReadInt());
        RefinerySyncManager.ApplyCounts(items, source.ToSourceString());
    }
}
