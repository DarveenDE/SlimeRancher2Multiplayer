using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Sync;

/// <summary>
/// Generic snapshot packet used for both Initial-Sync and Repair-Snapshot delivery.
/// Replaces the per-subsystem <c>Initial*Packet</c> types as subsystems migrate to
/// <see cref="SR2MP.Shared.Sync.ISyncedSubsystem"/>.
///
/// The <see cref="SubsystemId"/> routes the payload to the correct subsystem
/// on the client via <see cref="SR2MP.Shared.Sync.SubsystemRegistry"/>.
/// </summary>
public sealed class SubsystemSnapshotPacket : IPacket
{
    /// <summary>Identifies the target subsystem.  See <c>SubsystemIds</c>.</summary>
    public byte SubsystemId { get; set; }

    /// <summary>
    /// <c>true</c> if this is a repair snapshot (periodic drift correction);
    /// <c>false</c> for the initial bulk snapshot on join.
    /// </summary>
    public bool IsRepair { get; set; }

    /// <summary>Raw bytes produced by <c>ISyncedSubsystem.CaptureSnapshot</c>.</summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    public PacketType Type => PacketType.SubsystemSnapshot;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteByte(SubsystemId);
        writer.WriteBool(IsRepair);
        writer.WriteInt(Payload.Length);
        foreach (var b in Payload)
            writer.WriteByte(b);
    }

    public void Deserialise(PacketReader reader)
    {
        SubsystemId = reader.ReadByte();
        IsRepair = reader.ReadBool();
        var length = reader.ReadInt();
        Payload = new byte[length];
        for (var i = 0; i < length; i++)
            Payload[i] = reader.ReadByte();
    }
}
