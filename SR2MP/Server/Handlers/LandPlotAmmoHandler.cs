using System.Net;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.LandPlotAmmoUpdate)]
public sealed class LandPlotAmmoHandler : BasePacketHandler<LandPlotAmmoPacket>
{
    public LandPlotAmmoHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(LandPlotAmmoPacket packet, IPEndPoint senderEndPoint)
    {
        handlingPacket = true;
        try
        {
            if (!LandPlotAmmoSyncManager.ApplyAmmoSet(packet.PlotId, packet.AmmoSet, "server land plot ammo"))
                return;
        }
        finally { handlingPacket = false; }

        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }
}
