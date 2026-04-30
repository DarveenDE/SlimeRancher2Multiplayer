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
        PuzzleStateSyncManager.ApplyDepositorState(packet.ID, packet.AmountDeposited, "server plort depositor");
        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }
}
