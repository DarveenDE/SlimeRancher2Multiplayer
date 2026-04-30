using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Loading;

public sealed class InitialPuzzleStatesPacket : IPacket
{
    public sealed class PuzzleSlot : INetObject
    {
        public string ID { get; set; } = string.Empty;
        public bool Filled { get; set; }

        public void Serialise(PacketWriter writer)
        {
            writer.WriteString(ID);
            writer.WriteBool(Filled);
        }

        public void Deserialise(PacketReader reader)
        {
            ID = reader.ReadString();
            Filled = reader.ReadBool();
        }
    }

    public sealed class PlortDepositor : INetObject
    {
        public string ID { get; set; } = string.Empty;
        public int AmountDeposited { get; set; }

        public void Serialise(PacketWriter writer)
        {
            writer.WriteString(ID);
            writer.WriteInt(AmountDeposited);
        }

        public void Deserialise(PacketReader reader)
        {
            ID = reader.ReadString();
            AmountDeposited = reader.ReadInt();
        }
    }

    public List<PuzzleSlot> Slots { get; set; } = new();
    public List<PlortDepositor> Depositors { get; set; } = new();

    public PacketType Type => PacketType.InitialPuzzleStates;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteList(Slots, PacketWriterDels.NetObject<PuzzleSlot>.Func);
        writer.WriteList(Depositors, PacketWriterDels.NetObject<PlortDepositor>.Func);
    }

    public void Deserialise(PacketReader reader)
    {
        Slots = reader.ReadList(PacketReaderDels.NetObject<PuzzleSlot>.Func);
        Depositors = reader.ReadList(PacketReaderDels.NetObject<PlortDepositor>.Func);
    }
}
