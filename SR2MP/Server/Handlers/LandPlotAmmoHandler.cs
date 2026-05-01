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
            {
                SrLogger.LogWarning(
                    $"Rejected land plot ammo update from {DescribeClient(senderEndPoint)}: plot='{packet.PlotId}', ammoSet='{packet.AmmoSet?.Key}', slots={packet.AmmoSet?.Slots?.Count ?? 0}.",
                    SrLogTarget.Both);
                return;
            }
        }
        finally { handlingPacket = false; }

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }

    private string DescribeClient(IPEndPoint senderEndPoint)
        => clientManager.TryGetClient(senderEndPoint, out var client) && client != null
            ? $"{client.PlayerId} ({senderEndPoint})"
            : senderEndPoint.ToString();
}
