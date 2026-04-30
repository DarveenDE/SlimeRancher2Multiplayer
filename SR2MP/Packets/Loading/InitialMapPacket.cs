using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Loading;

public sealed class InitialMapPacket : IPacket
{
    public List<string> UnlockedNodes { get; set; }
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.InitialMapEntries;
    public PacketReliability Reliability => PacketReliability.Reliable;

    // todo: Add navigation marker data later.

    public void Serialise(PacketWriter writer)
    {
        writer.WriteList(UnlockedNodes, PacketWriterDels.String);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        UnlockedNodes = reader.ReadList(PacketReaderDels.String);
        IsRepairSnapshot = reader.ReadBool();
    }
}
