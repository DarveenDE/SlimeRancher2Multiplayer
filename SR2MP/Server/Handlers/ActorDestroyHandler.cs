using System.Net;
using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using Il2CppMonomiPark.SlimeRancher.DataModel;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ActorDestroy)]
public sealed class ActorDestroyHandler : BasePacketHandler<ActorDestroyPacket>
{
    public ActorDestroyHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ActorDestroyPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (!actorManager.Actors.TryGetValue(packet.ActorId.Value, out var actor))
        {
            SrLogger.LogPacketSize($"Actor {packet.ActorId.Value} doesn't exist (already destroyed?)", SrLogTarget.Both);
            return;
        }

        if (!actorManager.IsActorOwnedBy(packet.ActorId.Value, client.PlayerId))
        {
            var owner = actorManager.TryGetActorOwner(packet.ActorId.Value, out var currentOwner)
                ? currentOwner
                : "unknown";
            SrLogger.LogWarning(
                $"Rejected actor destroy from {client.PlayerId} ({clientEp}); actor {packet.ActorId.Value} is owned by {owner}.",
                SrLogTarget.Both);
            return;
        }

        var gadget = actor.TryCast<GadgetModel>();
        if (gadget != null)
        {
            RunWithHandlingPacket(() => SceneContext.Instance.GameModel.DestroyGadgetModel(gadget));
            actorManager.Actors.Remove(packet.ActorId.Value);
            actorManager.ClearActorOwner(packet.ActorId.Value);
            Main.Server.SendToAllExcept(packet, clientEp);
            return;
        }

        SceneContext.Instance.GameModel.identifiables.Remove(packet.ActorId);
        if (SceneContext.Instance.GameModel.identifiablesByIdent.TryGetValue(actor.ident, out var actorsByIdent))
            actorsByIdent.Remove(actor);

        SceneContext.Instance.GameModel.DestroyIdentifiableModel(actor);
        actorManager.Actors.Remove(packet.ActorId.Value);
        actorManager.ClearActorOwner(packet.ActorId.Value);

        var obj = actor.GetGameObject();
        if (obj)
            RunWithHandlingPacket(() => Destroyer.DestroyActor(obj, "SR2MP.ActorDestroyHandler"));

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
