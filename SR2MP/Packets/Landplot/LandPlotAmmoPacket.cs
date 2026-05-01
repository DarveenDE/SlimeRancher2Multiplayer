using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Landplot;

public sealed class LandPlotAmmoPacket : IPacket
{
    public string PlotId { get; set; } = string.Empty;
    public InitialLandPlotsPacket.AmmoSetData AmmoSet { get; set; } = new();
    public bool IsRepairSnapshot { get; set; }

    public PacketType Type => PacketType.LandPlotAmmoUpdate;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(PlotId);
        writer.WriteNetObject(AmmoSet);
        writer.WriteBool(IsRepairSnapshot);
    }

    public void Deserialise(PacketReader reader)
    {
        PlotId = reader.ReadString();
        AmmoSet = reader.ReadNetObject<InitialLandPlotsPacket.AmmoSetData>();
        IsRepairSnapshot = reader.ReadBool();
    }
}
