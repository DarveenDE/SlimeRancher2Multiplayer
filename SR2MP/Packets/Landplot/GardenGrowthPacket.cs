using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Landplot;

public sealed class GardenGrowthPacket : IPacket
{
    public sealed class ProduceStateData : INetObject
    {
        public long ActorId { get; set; }
        public ResourceCycle.State State { get; set; }
        public double ProgressTime { get; set; }

        public void Serialise(PacketWriter writer)
        {
            writer.WriteLong(ActorId);
            writer.WriteEnum(State);
            writer.WriteDouble(ProgressTime);
        }

        public void Deserialise(PacketReader reader)
        {
            ActorId = reader.ReadLong();
            State = reader.ReadEnum<ResourceCycle.State>();
            ProgressTime = reader.ReadDouble();
        }
    }

    public string PlotId { get; set; } = string.Empty;
    public bool HasSpawnerState { get; set; }
    public float StoredWater { get; set; }
    public double NextSpawnTime { get; set; }
    public bool WasPreviouslyPlanted { get; set; }
    public bool NextSpawnRipens { get; set; }
    public List<ProduceStateData> ProduceStates { get; set; } = new();
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.GardenGrowthState;
    public PacketReliability Reliability => IsRepairSnapshot
        ? PacketReliability.Reliable
        : PacketReliability.Unreliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(PlotId);
        writer.WriteBool(HasSpawnerState);
        if (HasSpawnerState)
        {
            writer.WriteFloat(StoredWater);
            writer.WriteDouble(NextSpawnTime);
            writer.WriteBool(WasPreviouslyPlanted);
            writer.WriteBool(NextSpawnRipens);
        }

        writer.WriteList(ProduceStates, PacketWriterDels.NetObject<ProduceStateData>.Func);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        PlotId = reader.ReadString();
        HasSpawnerState = reader.ReadBool();
        if (HasSpawnerState)
        {
            StoredWater = reader.ReadFloat();
            NextSpawnTime = reader.ReadDouble();
            WasPreviouslyPlanted = reader.ReadBool();
            NextSpawnRipens = reader.ReadBool();
        }

        ProduceStates = reader.ReadList(PacketReaderDels.NetObject<ProduceStateData>.Func);
        IsRepairSnapshot = reader.ReadBool();
    }

    public static GardenGrowthPacket ForProduce(long actorId, ResourceCycle.State state, double progressTime)
        => new()
        {
            ProduceStates = new List<ProduceStateData>
            {
                new ProduceStateData
                {
                    ActorId = actorId,
                    State = state,
                    ProgressTime = progressTime,
                }
            },
        };
}
