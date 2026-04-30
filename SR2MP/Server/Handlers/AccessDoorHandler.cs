using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.AccessDoor)]
public sealed class AccessDoorHandler : BasePacketHandler<AccessDoorPacket>
{
    public AccessDoorHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(AccessDoorPacket packet, IPEndPoint senderEndPoint)
    {
        if (!WorldEventStateSyncManager.ApplyAccessDoorState(packet, "server access door"))
            return;

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }
}
