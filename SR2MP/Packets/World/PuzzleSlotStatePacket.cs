using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public sealed class PuzzleSlotStatePacket : IPacket
{
    public string ID { get; set; } = string.Empty;
    public bool Filled { get; set; }
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.PuzzleSlotState;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(ID);
        writer.WriteBool(Filled);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        ID = reader.ReadString();
        Filled = reader.ReadBool();
        IsRepairSnapshot = reader.ReadBool();
    }
}
