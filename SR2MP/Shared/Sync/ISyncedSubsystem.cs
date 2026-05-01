using SR2MP.Packets.Utils;

namespace SR2MP.Shared.Sync;

/// <summary>
/// The single extension point for adding a new sync system.
/// One implementation = one sync domain (map, comm stations, plots, …).
///
/// The same <see cref="CaptureSnapshot"/> / <see cref="ApplySnapshot"/> pair is
/// used for <b>both</b> Initial-Sync and Repair-Snapshot — eliminating the
/// parallel implementations that existed before Phase 3.
/// </summary>
public interface ISyncedSubsystem
{
    /// <summary>
    /// Stable numeric ID, unique across all subsystems.
    /// Used as the routing key in <see cref="SubsystemSnapshotPacket"/>.
    /// </summary>
    byte Id { get; }

    /// <summary>Human-readable name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Serialises the complete current state into <paramref name="writer"/>.
    /// Called on the host.  Must be deterministic and produce bytes that
    /// <see cref="ApplySnapshot"/> can read back without additional framing.
    /// </summary>
    void CaptureSnapshot(PacketWriter writer);

    /// <summary>
    /// Applies a snapshot received from the host (Initial-Sync or Repair).
    /// Must be idempotent — calling it twice with the same data must leave
    /// the state identical to calling it once.
    /// </summary>
    void ApplySnapshot(PacketReader reader, SyncSource source);
}
