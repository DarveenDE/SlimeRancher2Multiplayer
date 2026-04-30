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
        PuzzleStateSyncManager.ApplySlotState(packet.ID, packet.Filled, "server puzzle slot");
        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }
}
