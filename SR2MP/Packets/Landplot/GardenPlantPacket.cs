using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Landplot;

public sealed class GardenPlantPacket : IPacket
{
    public string ID { get; set; }
    public bool HasCrop { get; set; }
    public int ActorType { get; set; }
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.GardenPlant;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(ID);
        writer.WriteBool(HasCrop);
        if (HasCrop)
            writer.WriteInt(ActorType);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        ID = reader.ReadString();
        HasCrop = reader.ReadBool();
        ActorType = HasCrop ? reader.ReadInt() : -1;
        IsRepairSnapshot = reader.ReadBool();
    }
}
