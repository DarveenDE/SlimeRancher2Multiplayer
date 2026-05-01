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

        if (actorManager.TryGetActorOwner(packet.ActorId.Value, out var owner)
            && owner != client.PlayerId)
        {
            SrLogger.LogWarning(
                $"Rejected actor update from {client.PlayerId} ({clientEp}); actor {packet.ActorId.Value} is owned by {owner}.",
                SrLogTarget.Both);
            return;
        }

        ActorUpdateSyncManager.ApplyOrQueue(
            packet,
            "server actor update",
            appliedPacket => Main.Server.SendToAllExcept(appliedPacket, clientEp));
    }
}
