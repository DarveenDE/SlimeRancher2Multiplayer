using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PuzzleSlotState)]
public sealed class PuzzleSlotStateHandler : BasePacketHandler<PuzzleSlotStatePacket>
{
    public PuzzleSlotStateHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PuzzleSlotStatePacket packet, IPEndPoint senderEndPoint)
    {
        if (!PuzzleStateSyncManager.ApplySlotState(packet.ID, packet.Filled, "server puzzle slot"))
        {
            SrLogger.LogWarning(
                $"Rejected puzzle slot update from {DescribeClient(senderEndPoint)}: slot='{packet.ID}', filled={packet.Filled}.",
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
