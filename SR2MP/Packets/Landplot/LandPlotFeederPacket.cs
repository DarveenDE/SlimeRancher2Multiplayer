using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Landplot;

public sealed class LandPlotFeederPacket : IPacket
{
    public string PlotId { get; set; } = string.Empty;
    public InitialLandPlotsPacket.FeederStateData State { get; set; } = new();

    public PacketType Type => PacketType.LandPlotFeederState;
    public PacketReliability Reliability => PacketReliability.Reliable;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(PlotId);
        writer.WriteNetObject(State);
    }

    public void Deserialise(PacketReader reader)
    {
        PlotId = reader.ReadString();
        State = reader.ReadNetObject<InitialLandPlotsPacket.FeederStateData>();
    }
}
