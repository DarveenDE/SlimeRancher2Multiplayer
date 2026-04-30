using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public sealed class CommStationPlayedPacket : IPacket
{
    public struct Entry : INetObject
    {
        public string Id { get; set; }
        public byte TargetType { get; set; }

        public readonly void Serialise(PacketWriter writer)
        {
            writer.WriteString(Id);
            writer.WriteByte(TargetType);
        }

        public void Deserialise(PacketReader reader)
        {
            Id = reader.ReadString();
            TargetType = reader.ReadByte();
        }
    }

    public List<Entry> Entries { get; set; } = new();
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.CommStationPlayed;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteList(Entries, PacketWriterDels.NetObject<Entry>.Func);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        Entries = reader.ReadList(PacketReaderDels.NetObject<Entry>.Func);
        IsRepairSnapshot = reader.ReadBool();
    }
}
