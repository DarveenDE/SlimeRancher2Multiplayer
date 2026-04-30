using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.ActorDestroy)]
public sealed class ActorDestroyHandler : BaseClientPacketHandler<ActorDestroyPacket>
{
    public ActorDestroyHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(ActorDestroyPacket packet)
    {
        if (!actorManager.Actors.TryGetValue(packet.ActorId.Value, out var actor))
        {
            SrLogger.LogPacketSize($"Actor {packet.ActorId.Value} doesn't exist (already destroyed?)", SrLogTarget.Both);
            return;
        }

        SceneContext.Instance.GameModel.identifiables.Remove(packet.ActorId);
        if (SceneContext.Instance.GameModel.identifiablesByIdent.TryGetValue(actor.ident, out var actorsByIdent))
            actorsByIdent.Remove(actor);

        SceneContext.Instance.GameModel.DestroyIdentifiableModel(actor);
        actorManager.Actors.Remove(packet.ActorId.Value);

        var obj = actor.GetGameObject();
        if (obj)
            RunWithHandlingPacket(() => Destroyer.DestroyActor(obj, "SR2MP.ActorDestroyHandler"));
    }
}
