using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Loading;

public sealed class ConnectPacket : IPacket
{
    public string PlayerId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; }
    public string ModVersion { get; set; } = string.Empty;
    public string RequiredGameVersion { get; set; } = string.Empty;

    public PacketType Type => PacketType.Connect;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(PlayerId);
        writer.WriteString(Username);
        writer.WriteInt(ProtocolVersion);
        writer.WriteString(ModVersion);
        writer.WriteString(RequiredGameVersion);
    }

    public void Deserialise(PacketReader reader)
    {
        PlayerId = reader.ReadString();
        Username = reader.ReadString();
        ProtocolVersion = reader.RemainingBytes > 0 ? reader.ReadInt() : 0;
        ModVersion = reader.RemainingBytes > 0 ? reader.ReadString() : string.Empty;
        RequiredGameVersion = reader.RemainingBytes > 0 ? reader.ReadString() : string.Empty;
    }
}
