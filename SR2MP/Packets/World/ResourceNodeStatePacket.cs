using SR2MP.Packets.Utils;

namespace SR2MP.Packets.World;

public sealed class ResourceNodeStatePacket : IPacket
{
    public struct NodeStateData : INetObject
    {
        public string NodeId { get; set; }
        public int DefinitionIndex { get; set; }
        public int VariantIndex { get; set; }
        public ResourceNode.NodeState State { get; set; }
        public double DespawnAtWorldTime { get; set; }
        public List<int> ResourceTypeIds { get; set; }

        public readonly void Serialise(PacketWriter writer)
        {
            writer.WriteString(NodeId);
            writer.WriteInt(DefinitionIndex);
            writer.WriteInt(VariantIndex);
            writer.WriteEnum(State);
            writer.WriteDouble(DespawnAtWorldTime);
            writer.WriteList(ResourceTypeIds, static (packetWriter, value) => packetWriter.WriteInt(value));
        }

        public void Deserialise(PacketReader reader)
        {
            NodeId = reader.ReadString();
            DefinitionIndex = reader.ReadInt();
            VariantIndex = reader.ReadInt();
            State = reader.ReadEnum<ResourceNode.NodeState>();
            DespawnAtWorldTime = reader.ReadDouble();
            ResourceTypeIds = reader.ReadList(static packetReader => packetReader.ReadInt());
        }
    }

    public List<NodeStateData> Nodes { get; set; } = new();
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.ResourceNodeState;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteList(Nodes, PacketWriterDels.NetObject<NodeStateData>.Func);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        Nodes = reader.ReadList(PacketReaderDels.NetObject<NodeStateData>.Func);
        IsRepairSnapshot = reader.ReadBool();
    }
}
