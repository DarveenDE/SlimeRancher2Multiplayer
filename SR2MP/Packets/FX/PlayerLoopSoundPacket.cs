using SR2MP.Packets.Utils;

namespace SR2MP.Packets.FX;

public sealed class PlayerLoopSoundPacket : IPacket
{
    public string Player { get; set; } = string.Empty;
    public string CueName { get; set; } = string.Empty;
    public bool IsPlaying { get; set; }
    public float Volume { get; set; } = 0.8f;

    public PacketType Type => PacketType.PlayerLoopSound;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(Player);
        writer.WriteString(CueName);
        writer.WriteBool(IsPlaying);
        writer.WriteFloat(Volume);
    }

    public void Deserialise(PacketReader reader)
    {
        Player = reader.ReadString();
        CueName = reader.ReadString();
        IsPlaying = reader.ReadBool();
        Volume = reader.ReadFloat();
    }
}
