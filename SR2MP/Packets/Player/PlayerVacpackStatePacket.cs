using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Player;

public sealed class PlayerVacpackStatePacket : IPacket
{
    public string PlayerId { get; set; }
    /// <summary>Persistent IdentifiableType ID of the held item; 0 = nothing held.</summary>
    public int HeldIdentType { get; set; }
    /// <summary>Active ammo slot index (0-based).</summary>
    public int ActiveSlot { get; set; }
    /// <summary>Water tank fill level (0-100, clamped).</summary>
    public byte WaterLevel { get; set; }

    public PacketType Type => PacketType.PlayerVacpackState;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(PlayerId);
        writer.WriteInt(HeldIdentType);
        writer.WriteInt(ActiveSlot);
        writer.WriteByte(WaterLevel);
    }

    public void Deserialise(PacketReader reader)
    {
        PlayerId = reader.ReadString();
        HeldIdentType = reader.ReadInt();
        ActiveSlot = reader.ReadInt();
        WaterLevel = reader.ReadByte();
    }
}
