using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Loading;

public sealed class InitialActorsPacket : IPacket
{
    public struct Actor : INetObject
    {
        public long ActorId { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public int ActorType { get; set; }
        public int Scene { get; set; }
        public bool IsPrePlaced { get; set; }
        public bool IsFeral { get; set; }

        public readonly void Serialise(PacketWriter writer)
        {
            writer.WriteVector3(Position);
            writer.WriteQuaternion(Rotation);
            writer.WriteLong(ActorId);
            writer.WriteInt(ActorType);
            writer.WriteInt(Scene);
            writer.WriteBool(IsPrePlaced);
            writer.WriteBool(IsFeral);
        }

        public void Deserialise(PacketReader reader)
        {
            Position = reader.ReadVector3();
            Rotation = reader.ReadQuaternion();
            ActorId = reader.ReadLong();
            ActorType = reader.ReadInt();
            Scene = reader.ReadInt();
            IsPrePlaced = reader.ReadBool();
            IsFeral = reader.ReadBool();
        }
    }

    public uint StartingActorID { get; set; } = 10000;
    public long ActorIdRangeMin { get; set; }
    public long ActorIdRangeMax { get; set; }
    public List<Actor> Actors { get; set; }

    public PacketType Type => PacketType.InitialActors;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteUInt(StartingActorID);
        writer.WriteLong(ActorIdRangeMin);
        writer.WriteLong(ActorIdRangeMax);
        writer.WriteList(Actors, PacketWriterDels.NetObject<Actor>.Func);
    }

    public void Deserialise(PacketReader reader)
    {
        StartingActorID = reader.ReadUInt();
        ActorIdRangeMin = reader.ReadLong();
        ActorIdRangeMax = reader.ReadLong();
        Actors = reader.ReadList(PacketReaderDels.NetObject<Actor>.Func);
    }
}
