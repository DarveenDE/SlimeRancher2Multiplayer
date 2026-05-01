using System.Net;
using SR2MP.Packets.Actor;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ActorUpdate)]
public sealed class ActorUpdateHandler : BasePacketHandler<ActorUpdatePacket>
{
    public ActorUpdateHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ActorUpdatePacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (!CheckAuthority(packet, client.PlayerId, clientEp).IsAllowed)
            return;

        ActorUpdateSyncManager.ApplyOrQueue(
            packet,
            "server actor update",
            appliedPacket => Main.Server.SendToAllExcept(appliedPacket, clientEp));
    }
}
