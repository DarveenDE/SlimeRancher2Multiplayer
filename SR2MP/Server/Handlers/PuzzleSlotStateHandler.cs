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
        if (!PuzzleStateSyncManager.TryGetSlotState(packet.ID, out var currentFilled))
        {
            SrLogger.LogWarning(
                $"Rejected puzzle slot update from {DescribeClient(senderEndPoint)}: slot='{packet.ID}' is unknown.",
                SrLogTarget.Both);
            return;
        }

        if (currentFilled && !packet.Filled)
        {
            SrLogger.LogWarning(
                $"Rejected stale puzzle slot update from {DescribeClient(senderEndPoint)}: slot='{packet.ID}' attempted to clear an already filled slot.",
                SrLogTarget.Both);
            Main.Server.SendToClient(new PuzzleSlotStatePacket
            {
                ID = packet.ID,
                Filled = currentFilled,
            }, senderEndPoint);
            return;
        }

        if (!PuzzleStateSyncManager.ApplySlotState(packet.ID, packet.Filled, "server puzzle slot"))
        {
            SrLogger.LogWarning(
                $"Rejected puzzle slot update from {DescribeClient(senderEndPoint)}: slot='{packet.ID}', filled={packet.Filled}.",
                SrLogTarget.Both);
            return;
        }

        SrLogger.LogMessage(
            $"Accepted puzzle slot update from {DescribeClient(senderEndPoint)}: slot='{packet.ID}', {currentFilled}->{packet.Filled}.",
            SrLogTarget.Both);

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }

    private string DescribeClient(IPEndPoint senderEndPoint)
        => clientManager.TryGetClient(senderEndPoint, out var client) && client != null
            ? $"{client.PlayerId} ({senderEndPoint})"
            : senderEndPoint.ToString();
}
