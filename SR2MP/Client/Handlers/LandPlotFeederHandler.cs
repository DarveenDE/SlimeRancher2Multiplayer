using SR2MP.Packets.Landplot;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.LandPlotFeederState)]
public sealed class LandPlotFeederHandler : BaseClientPacketHandler<LandPlotFeederPacket>
{
    public LandPlotFeederHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(LandPlotFeederPacket packet)
    {
        handlingPacket = true;
        try
        {
            LandPlotFeederSyncManager.ApplyState(packet.PlotId, packet.State, "client feeder state");
        }
        finally { handlingPacket = false; }
    }
}
