using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Economy;

public struct CurrencyPacket : IPacket
{
    public int NewAmount { get; set; }
    public int PreviousAmount { get; set; }
    public int DeltaAmount { get; set; }
    public byte CurrencyType { get; set; }
    public bool ShowUINotification { get; set; }
    public bool HasBaseline { get; private set; }

    public readonly PacketType Type => PacketType.CurrencyAdjust;
    public readonly PacketReliability Reliability => PacketReliability.ReliableOrdered;

    public readonly void Serialise(PacketWriter writer)
    {
        writer.WriteInt(NewAmount);
        writer.WriteByte(CurrencyType);
        writer.WriteBool(ShowUINotification);
        writer.WriteInt(PreviousAmount);
        writer.WriteInt(DeltaAmount);
    }

    public void Deserialise(PacketReader reader)
    {
        NewAmount = reader.ReadInt();
        CurrencyType = reader.ReadByte();
        ShowUINotification = reader.ReadBool();
        HasBaseline = reader.RemainingBytes >= sizeof(int) * 2;
        if (!HasBaseline)
        {
            PreviousAmount = NewAmount;
            DeltaAmount = 0;
            return;
        }

        PreviousAmount = reader.ReadInt();
        DeltaAmount = reader.ReadInt();
    }
}
