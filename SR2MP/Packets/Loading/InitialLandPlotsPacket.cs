using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Loading;

public sealed class InitialLandPlotsPacket : IPacket
{
    public sealed class BasePlot : INetObject
    {
        private static readonly Dictionary<LandPlot.Id, Type> DataTypes = new()
        {
            { LandPlot.Id.GARDEN, typeof(GardenData) },
            { LandPlot.Id.SILO,   typeof(SiloData)   }
        };

        public string ID { get; set; }
        public LandPlot.Id  Type { get; set; }
        public CppCollections.HashSet<LandPlot.Upgrade> Upgrades { get; set; }
        public List<AmmoSetData> AmmoSets { get; set; } = new();
        public FeederStateData FeederState { get; set; } = new();

        public INetObject? Data { get; set; }

        public void Serialise(PacketWriter writer)
        {
            writer.WriteString(ID);
            writer.WriteEnum(Type);
            writer.WriteCppSet(Upgrades, PacketWriterDels.Enum<LandPlot.Upgrade>.Func);
            writer.WriteList(AmmoSets, PacketWriterDels.NetObject<AmmoSetData>.Func);
            writer.WriteNetObject(FeederState);

            Data?.Serialise(writer);
        }

        public void Deserialise(PacketReader reader)
        {
            ID = reader.ReadString();
            Type = reader.ReadEnum<LandPlot.Id>();
            Upgrades = reader.ReadCppSet(PacketReaderDels.Enum<LandPlot.Upgrade>.Func);
            AmmoSets = reader.ReadList(PacketReaderDels.NetObject<AmmoSetData>.Func);
            FeederState = reader.ReadNetObject<FeederStateData>();

            if (!DataTypes.TryGetValue(Type, out var dataType))
                return;

            Data = (INetObject)Activator.CreateInstance(dataType)!;
            Data.Deserialise(reader);
        }
    }

    public sealed class AmmoSetData : INetObject
    {
        public string Key { get; set; } = string.Empty;
        public List<AmmoSlotData> Slots { get; set; } = new();

        public void Serialise(PacketWriter writer)
        {
            writer.WriteString(Key);
            writer.WriteList(Slots, PacketWriterDels.NetObject<AmmoSlotData>.Func);
        }

        public void Deserialise(PacketReader reader)
        {
            Key = reader.ReadString();
            Slots = reader.ReadList(PacketReaderDels.NetObject<AmmoSlotData>.Func);
        }
    }

    public sealed class AmmoSlotData : INetObject
    {
        public bool HasIdentifiable { get; set; }
        public int IdentifiableType { get; set; }
        public int Count { get; set; }
        public bool Radiant { get; set; }

        public void Serialise(PacketWriter writer)
        {
            writer.WriteBool(HasIdentifiable);
            if (HasIdentifiable)
                writer.WriteInt(IdentifiableType);
            writer.WriteInt(Count);
            writer.WriteBool(Radiant);
        }

        public void Deserialise(PacketReader reader)
        {
            HasIdentifiable = reader.ReadBool();
            IdentifiableType = HasIdentifiable ? reader.ReadInt() : -1;
            Count = reader.ReadInt();
            Radiant = reader.ReadBool();
        }
    }

    public sealed class FeederStateData : INetObject
    {
        public SlimeFeeder.FeedSpeed Speed { get; set; }
        public double NextFeedingTime { get; set; }
        public int RemainingFeedOperations { get; set; }

        public void Serialise(PacketWriter writer)
        {
            writer.WriteEnum(Speed);
            writer.WriteDouble(NextFeedingTime);
            writer.WriteInt(RemainingFeedOperations);
        }

        public void Deserialise(PacketReader reader)
        {
            Speed = reader.ReadEnum<SlimeFeeder.FeedSpeed>();
            NextFeedingTime = reader.ReadDouble();
            RemainingFeedOperations = reader.ReadInt();
        }
    }

    public struct GardenData : INetObject
    {
        public bool HasCrop { get; set; }
        public int Crop { get; set; }

        public readonly void Serialise(PacketWriter writer)
        {
            writer.WriteBool(HasCrop);
            if (HasCrop)
                writer.WriteInt(Crop);
        }

        public void Deserialise(PacketReader reader)
        {
            HasCrop = reader.ReadBool();
            Crop = HasCrop ? reader.ReadInt() : -1;
        }
    }

    public struct SiloData : INetObject
    {
        // todo
        public readonly void Serialise(PacketWriter writer) { }
        public void Deserialise(PacketReader reader) { }
    }

    public List<BasePlot> Plots { get; set; }

    public PacketType Type => PacketType.InitialPlots;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer) => writer.WriteList(Plots, PacketWriterDels.NetObject<BasePlot>.Func);

    public void Deserialise(PacketReader reader) => Plots = reader.ReadList(PacketReaderDels.NetObject<BasePlot>.Func);
}
