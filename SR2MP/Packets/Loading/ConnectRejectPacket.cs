using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Loading;

public sealed class ConnectRejectPacket : IPacket
{
    public string Message { get; set; } = string.Empty;
    public int ServerProtocolVersion { get; set; }
    public string ServerModVersion { get; set; } = string.Empty;
    public string ServerRequiredGameVersion { get; set; } = string.Empty;

    public PacketType Type => PacketType.ConnectRejected;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(Message);
        writer.WriteInt(ServerProtocolVersion);
        writer.WriteString(ServerModVersion);
        writer.WriteString(ServerRequiredGameVersion);
    }

    public void Deserialise(PacketReader reader)
    {
        Message = reader.ReadString();
        ServerProtocolVersion = reader.RemainingBytes > 0 ? reader.ReadInt() : 0;
        ServerModVersion = reader.RemainingBytes > 0 ? reader.ReadString() : string.Empty;
        ServerRequiredGameVersion = reader.RemainingBytes > 0 ? reader.ReadString() : string.Empty;
    }
}
