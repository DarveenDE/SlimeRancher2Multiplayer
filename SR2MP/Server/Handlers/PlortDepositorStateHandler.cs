using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PlortDepositorState)]
public sealed class PlortDepositorStateHandler : BasePacketHandler<PlortDepositorStatePacket>
{
    public PlortDepositorStateHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PlortDepositorStatePacket packet, IPEndPoint senderEndPoint)
    {
        if (!PuzzleStateSyncManager.TryGetDepositorAmount(packet.ID, out var currentAmount))
        {
            SrLogger.LogWarning(
                $"Rejected plort depositor update from {DescribeClient(senderEndPoint)}: depositor='{packet.ID}' is unknown.",
                SrLogTarget.Both);
            return;
        }

        if (packet.AmountDeposited < currentAmount)
        {
            SrLogger.LogWarning(
                $"Rejected stale plort depositor update from {DescribeClient(senderEndPoint)}: depositor='{packet.ID}', attempted {packet.AmountDeposited} below host {currentAmount}.",
                SrLogTarget.Both);
            Main.Server.SendToClient(new PlortDepositorStatePacket
            {
                ID = packet.ID,
                AmountDeposited = currentAmount,
            }, senderEndPoint);
            return;
        }

        if (!PuzzleStateSyncManager.ApplyDepositorState(packet.ID, packet.AmountDeposited, "server plort depositor"))
        {
            SrLogger.LogWarning(
                $"Rejected plort depositor update from {DescribeClient(senderEndPoint)}: depositor='{packet.ID}', amount={packet.AmountDeposited}.",
                SrLogTarget.Both);
            return;
        }

        SrLogger.LogMessage(
            $"Accepted plort depositor update from {DescribeClient(senderEndPoint)}: depositor='{packet.ID}', {currentAmount}->{packet.AmountDeposited}.",
            SrLogTarget.Both);

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }

    private string DescribeClient(IPEndPoint senderEndPoint)
        => clientManager.TryGetClient(senderEndPoint, out var client) && client != null
            ? $"{client.PlayerId} ({senderEndPoint})"
            : senderEndPoint.ToString();
}
