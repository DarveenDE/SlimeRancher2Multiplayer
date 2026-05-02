using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public struct PrismaDisruptionPacket : IPacket
{
    public int AreaPersistenceId { get; set; }
    public byte DisruptionLevel { get; set; }

    public PacketType Type => PacketType.PrismaDisruption;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public readonly void Serialise(PacketWriter writer)
    {
        writer.WriteInt(AreaPersistenceId);
        writer.WriteByte(DisruptionLevel);
    }

    public void Deserialise(PacketReader reader)
    {
        AreaPersistenceId = reader.ReadInt();
        DisruptionLevel = reader.ReadByte();
    }
}
