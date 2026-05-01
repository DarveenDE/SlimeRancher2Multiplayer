using System.Net;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.LandPlotFeederState)]
public sealed class LandPlotFeederHandler : BasePacketHandler<LandPlotFeederPacket>
{
    public LandPlotFeederHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(LandPlotFeederPacket packet, IPEndPoint senderEndPoint)
    {
        handlingPacket = true;
        try
        {
            if (!LandPlotFeederSyncManager.ApplyState(packet.PlotId, packet.State, "server feeder state"))
            {
                SrLogger.LogWarning(
                    $"Rejected land plot feeder update from {DescribeClient(senderEndPoint)}: plot='{packet.PlotId}', speed={packet.State.Speed}, next={packet.State.NextFeedingTime}, remaining={packet.State.RemainingFeedOperations}.",
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
