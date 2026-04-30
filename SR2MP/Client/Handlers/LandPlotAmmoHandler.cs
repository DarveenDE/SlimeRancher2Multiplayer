using SR2MP.Packets.Landplot;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.LandPlotAmmoUpdate)]
public sealed class LandPlotAmmoHandler : BaseClientPacketHandler<LandPlotAmmoPacket>
{
    public LandPlotAmmoHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(LandPlotAmmoPacket packet)
    {
        handlingPacket = true;
        try
        {
            LandPlotAmmoSyncManager.ApplyAmmoSet(packet.PlotId, packet.AmmoSet, "client land plot ammo");
        }
        finally { handlingPacket = false; }
    }
}
