using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public sealed class PlortDepositorStatePacket : IPacket
{
    public string ID { get; set; } = string.Empty;
    public int AmountDeposited { get; set; }
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.PlortDepositorState;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(ID);
        writer.WriteInt(AmountDeposited);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        ID = reader.ReadString();
        AmountDeposited = reader.ReadInt();
        IsRepairSnapshot = reader.ReadBool();
    }
}
