using System.Net;
using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ActorFeral)]
public sealed class ActorFeralHandler : BasePacketHandler<ActorFeralPacket>
{
    public ActorFeralHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ActorFeralPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (!CheckAuthority(packet, client.PlayerId, clientEp).IsAllowed)
            return;

        ActorFeralSyncManager.ApplyOrQueue(
            packet,
            "server actor feral",
            afterApplied: () => Main.Server.SendToAllExcept(packet, clientEp));
    }
}
