using SR2MP.Packets;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.HeartbeatAck)]
public sealed class HeartbeatAckHandler : BaseClientPacketHandler<EmptyPacket>
{
    public HeartbeatAckHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(EmptyPacket packet)
    {
        Client.NotifyHeartbeatAck();
    }
}
