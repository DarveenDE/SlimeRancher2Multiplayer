using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.ActorUpdate)]
public sealed class ActorUpdateHandler : BaseClientPacketHandler<ActorUpdatePacket>
{
    public ActorUpdateHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(ActorUpdatePacket packet)
    {
        ActorUpdateSyncManager.ApplyOrQueue(packet, "client actor update");
    }
}
