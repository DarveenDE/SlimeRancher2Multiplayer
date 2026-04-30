using System.Net;
using SR2MP.Packets.Landplot;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.LandPlotUpdate)]
public sealed class LandPlotUpdateHandler : BasePacketHandler<LandPlotUpdatePacket>
{
    public LandPlotUpdateHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(LandPlotUpdatePacket packet, IPEndPoint clientEp)
    {
        if (!WorldEventStateSyncManager.ApplyLandPlotUpdate(packet, "server land plot update"))
            return;

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
