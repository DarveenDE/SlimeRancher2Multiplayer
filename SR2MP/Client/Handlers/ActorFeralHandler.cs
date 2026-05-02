using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.ActorFeral)]
public sealed class ActorFeralHandler : BaseClientPacketHandler<ActorFeralPacket>
{
    public ActorFeralHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(ActorFeralPacket packet)
    {
        ActorFeralSyncManager.ApplyOrQueue(packet, "client actor feral");
    }
}
