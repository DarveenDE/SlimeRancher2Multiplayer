using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Loading;

public sealed class InitialSyncCompleteAckPacket : IPacket
{
    public PacketType Type => PacketType.InitialSyncCompleteAck;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer) { }

    public void Deserialise(PacketReader reader) { }
}
