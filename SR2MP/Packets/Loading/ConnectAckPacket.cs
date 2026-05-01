using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Loading;

public sealed class ConnectAckPacket : IPacket
{
    public string PlayerId { get; set; } = string.Empty;
    public (string ID, string Username)[] OtherPlayers { get; set; } = Array.Empty<(string ID, string Username)>();

    public int Money { get; set; }
    public int RainbowMoney { get; set; }
    public bool AllowCheats { get; set; }
    public int ProtocolVersion { get; set; }
    public string ModVersion { get; set; } = string.Empty;
    public string RequiredGameVersion { get; set; } = string.Empty;

    public PacketType Type => PacketType.ConnectAck;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(PlayerId);
        writer.WriteArray(OtherPlayers, PacketWriterDels.Tuple<string, string>.Func);

        writer.WriteInt(Money);
        writer.WriteInt(RainbowMoney);
        writer.WriteBool(AllowCheats);
        writer.WriteInt(ProtocolVersion);
        writer.WriteString(ModVersion);
        writer.WriteString(RequiredGameVersion);
    }

    public void Deserialise(PacketReader reader)
    {
        PlayerId = reader.ReadString();
        OtherPlayers = reader.ReadArray(PacketReaderDels.Tuple<string, string>.Func);
        Money = reader.ReadInt();
        RainbowMoney = reader.ReadInt();
        AllowCheats = reader.ReadBool();
        ProtocolVersion = reader.RemainingBytes > 0 ? reader.ReadInt() : 0;
        ModVersion = reader.RemainingBytes > 0 ? reader.ReadString() : string.Empty;
        RequiredGameVersion = reader.RemainingBytes > 0 ? reader.ReadString() : string.Empty;
    }
}
