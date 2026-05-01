using System.Net;
using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ActorUnload)]
public sealed class ActorUnloadHandler : BasePacketHandler<ActorUnloadPacket>
{
    public ActorUnloadHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ActorUnloadPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (!actorManager.Actors.TryGetValue(packet.ActorId.Value, out var actor))
        {
            SrLogger.LogWarning($"Rejected actor unload from {client.PlayerId} ({clientEp}); actor {packet.ActorId.Value} does not exist.", SrLogTarget.Both);
            return;
        }

        if (!actor.TryGetNetworkComponent(out var component))
        {
            SrLogger.LogWarning($"Rejected actor unload from {client.PlayerId} ({clientEp}); actor {packet.ActorId.Value} has no network component.", SrLogTarget.Both);
            return;
        }

        if (!actorManager.IsActorOwnedBy(packet.ActorId.Value, client.PlayerId))
        {
            var owner = actorManager.TryGetActorOwner(packet.ActorId.Value, out var currentOwner)
                ? currentOwner
                : "unknown";
            SrLogger.LogWarning(
                $"Rejected actor unload from {client.PlayerId} ({clientEp}); actor {packet.ActorId.Value} is owned by {owner}.",
                SrLogTarget.Both);
            return;
        }

        if (!component.regionMember)
            return;

        if (!component.regionMember._hibernating)
        {
            component.LocallyOwned = true;
            actorManager.SetActorOwner(packet.ActorId.Value, LocalID);

            var ownershipPacket = new ActorTransferPacket
            {
                ActorId = packet.ActorId,
                OwnerPlayer = LocalID,
            };
            Main.SendToAllOrServer(ownershipPacket);
            return;
        }

        actorManager.ClearActorOwner(packet.ActorId.Value);
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
