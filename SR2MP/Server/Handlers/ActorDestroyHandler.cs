using System.Net;
using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;
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

        // Authority: SharedGadgetOrOwnerRule — gadgets are shared, regular actors require ownership.
        if (!CheckAuthority(packet, client.PlayerId, clientEp).IsAllowed)
            return;

        var gadget = actor.TryCast<GadgetModel>();
        if (gadget != null)
        {
            var owner = actorManager.TryGetActorOwner(packet.ActorId.Value, out var currentOwner)
                ? currentOwner
                : "unknown";
            SrLogger.LogMessage(
                $"Accepted gadget destroy from {client.PlayerId} ({clientEp}); actor {packet.ActorId.Value} owner={owner}.",
                SrLogTarget.Both);

            // Pre-populate SentDestroys so that Harmony patches fired during DestroyGadgetModel
            // do not echo the packet back to the network.
            GadgetModelSyncManager.MarkDestroyHandled(packet.ActorId.Value);
            var gameObject = actor.GetGameObject();
            RunWithHandlingPacket(() => SceneContext.Instance.GameModel.DestroyGadgetModel(gadget));
            if (gameObject)
                RunWithHandlingPacket(() => Object.Destroy(gameObject));
        }
        else
        {
            SceneContext.Instance.GameModel.identifiables.Remove(packet.ActorId);
            if (SceneContext.Instance.GameModel.identifiablesByIdent.TryGetValue(actor.ident, out var actorsByIdent))
                actorsByIdent.Remove(actor);

            SceneContext.Instance.GameModel.DestroyIdentifiableModel(actor);

            var obj = actor.GetGameObject();
            if (obj)
                RunWithHandlingPacket(() => Destroyer.DestroyActor(obj, "SR2MP.ActorDestroyHandler"));
        }

        actorManager.Actors.Remove(packet.ActorId.Value);
        actorManager.ClearActorOwner(packet.ActorId.Value);
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
