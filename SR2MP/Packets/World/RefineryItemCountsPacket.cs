using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public sealed class RefineryItemCountsPacket : IPacket
{
    public Dictionary<int, int> Items { get; set; } = new();

    public PacketType Type => PacketType.RefineryItemCounts;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteDictionary(
            Items,
            static (packetWriter, itemType) => packetWriter.WriteInt(itemType),
            static (packetWriter, count) => packetWriter.WriteInt(count));
    }

    public void Deserialise(PacketReader reader)
    {
        Items = reader.ReadDictionary(
            static packetReader => packetReader.ReadInt(),
            static packetReader => packetReader.ReadInt());
    }
}
