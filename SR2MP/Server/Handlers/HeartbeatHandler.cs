using System.Net;
using SR2MP.Packets;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.Heartbeat)]
public sealed class HeartbeatHandler : BasePacketHandler<EmptyPacket>
{
    public HeartbeatHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(EmptyPacket packet, IPEndPoint clientEp)
    {
        clientManager.UpdateHeartbeat(clientEp);
        Main.Server.SendToClient(new EmptyPacket
        {
            Type = PacketType.HeartbeatAck,
            Reliability = PacketReliability.Unreliable,
        }, clientEp);
    }
}
