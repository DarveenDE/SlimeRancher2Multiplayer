using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Gordo;

public sealed class GordoBurstPacket : IPacket
{
    public string ID { get; set; }
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.GordoBurst;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(ID);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        ID = reader.ReadString();
        IsRepairSnapshot = reader.ReadBool();
    }
}
