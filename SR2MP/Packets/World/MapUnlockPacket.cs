using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public sealed class MapUnlockPacket : IPacket
{
    public string NodeID { get; set; }
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.MapUnlock;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(NodeID);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        NodeID = reader.ReadString();
        IsRepairSnapshot = reader.ReadBool();
    }
}
