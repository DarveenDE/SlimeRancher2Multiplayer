using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public sealed class MapFogPacket : IPacket
{
    public Vector3 Position { get; set; }
    public float Radius { get; set; }

    public PacketType Type => PacketType.MapFogReveal;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteVector3(Position);
        writer.WriteFloat(Radius);
    }

    public void Deserialise(PacketReader reader)
    {
        Position = reader.ReadVector3();
        Radius = reader.ReadFloat();
    }
}
