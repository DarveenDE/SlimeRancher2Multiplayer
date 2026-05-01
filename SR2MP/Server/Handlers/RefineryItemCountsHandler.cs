using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.RefineryItemCounts)]
public sealed class RefineryItemCountsHandler : BasePacketHandler<RefineryItemCountsPacket>
{
    public RefineryItemCountsHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(RefineryItemCountsPacket packet, IPEndPoint senderEndPoint)
    {
        bool applied;
        try
        {
            handlingPacket = true;
            applied = RefinerySyncManager.ApplyCounts(packet.Items, "server refinery counts");
        }
        finally
        {
            handlingPacket = false;
        }

        if (!applied)
        {
            SrLogger.LogWarning(
                $"Rejected refinery item count update from {DescribeClient(senderEndPoint)}: items={packet.Items.Count}.",
                SrLogTarget.Both);
            return;
        }

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }

    private string DescribeClient(IPEndPoint senderEndPoint)
        => clientManager.TryGetClient(senderEndPoint, out var client) && client != null
            ? $"{client.PlayerId} ({senderEndPoint})"
            : senderEndPoint.ToString();
}
