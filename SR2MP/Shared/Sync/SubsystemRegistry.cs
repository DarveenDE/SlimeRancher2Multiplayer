using System.Net;
using SR2MP.Packets.Sync;
using SR2MP.Packets.Utils;

namespace SR2MP.Shared.Sync;

/// <summary>
/// Holds all registered <see cref="ISyncedSubsystem"/> implementations and provides
/// the server-side helpers for sending snapshots (Initial-Sync and Repair).
///
/// Subsystems register themselves during startup via <see cref="Register"/>.
/// The client routes incoming <see cref="SubsystemSnapshotPacket"/>s through
/// <see cref="ApplySnapshot"/>.
/// </summary>
public sealed class SubsystemRegistry
{
    public static readonly SubsystemRegistry Instance = new();

    private readonly Dictionary<byte, ISyncedSubsystem> _subsystems = new();

    private SubsystemRegistry() { }

    // ── Registration ─────────────────────────────────────────────────────

    public void Register(ISyncedSubsystem subsystem)
    {
        _subsystems[subsystem.Id] = subsystem;
        SrLogger.LogMessage(
            $"[SubsystemRegistry] Registered subsystem '{subsystem.Name}' (id={subsystem.Id})",
            SrLogTarget.Main);
    }

    public bool TryGet(byte id, out ISyncedSubsystem subsystem)
        => _subsystems.TryGetValue(id, out subsystem!);

    // ── Client side ───────────────────────────────────────────────────────

    /// <summary>
    /// Called by <c>SubsystemSnapshotHandler</c> when a snapshot packet arrives.
    /// Routes to the correct subsystem by ID.
    /// </summary>
    public void ApplySnapshot(SubsystemSnapshotPacket packet)
    {
        if (!_subsystems.TryGetValue(packet.SubsystemId, out var subsystem))
        {
            SrLogger.LogWarning(
                $"[SubsystemRegistry] No subsystem registered for id={packet.SubsystemId}; snapshot ignored.",
                SrLogTarget.Main);
            return;
        }

        var source = packet.IsRepair ? SyncSource.Repair : SyncSource.Initial;

        try
        {
            using var reader = new PacketReader(packet.Payload);
            subsystem.ApplySnapshot(reader, source);
            SrLogger.LogMessage(
                $"[SubsystemRegistry] Applied {source} snapshot for '{subsystem.Name}'.",
                SrLogTarget.Main);
        }
        catch (Exception ex)
        {
            SrLogger.LogError(
                $"[SubsystemRegistry] Failed to apply {source} snapshot for '{subsystem.Name}': {ex}",
                SrLogTarget.Both);
        }
    }

    // ── Server side ───────────────────────────────────────────────────────

    /// <summary>
    /// Captures a snapshot and sends it to a single client (used during Initial-Sync).
    /// </summary>
    public void SendSnapshotToClient(byte subsystemId, IPEndPoint client, bool isRepair = false)
    {
        if (!_subsystems.TryGetValue(subsystemId, out var subsystem))
        {
            SrLogger.LogWarning(
                $"[SubsystemRegistry] SendSnapshotToClient: subsystem {subsystemId} not found.",
                SrLogTarget.Main);
            return;
        }

        var packet = BuildPacket(subsystem, isRepair);
        Main.Server.SendToClient(packet, client);
    }

    /// <summary>
    /// Captures a snapshot and broadcasts it to all connected clients (used for Repair).
    /// </summary>
    public void BroadcastSnapshot(byte subsystemId, bool isRepair = true)
    {
        if (!_subsystems.TryGetValue(subsystemId, out var subsystem))
        {
            SrLogger.LogWarning(
                $"[SubsystemRegistry] BroadcastSnapshot: subsystem {subsystemId} not found.",
                SrLogTarget.Main);
            return;
        }

        var packet = BuildPacket(subsystem, isRepair);
        Main.Server.SendToAll(packet);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static SubsystemSnapshotPacket BuildPacket(ISyncedSubsystem subsystem, bool isRepair)
    {
        using var writer = new PacketWriter();
        subsystem.CaptureSnapshot(writer);

        return new SubsystemSnapshotPacket
        {
            SubsystemId = subsystem.Id,
            IsRepair    = isRepair,
            Payload     = writer.ToArray(),
        };
    }
}
