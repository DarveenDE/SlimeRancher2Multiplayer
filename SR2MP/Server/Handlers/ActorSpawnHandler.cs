using System.Net;
using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using Il2CppMonomiPark.SlimeRancher.DataModel;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ActorSpawn)]
public sealed class ActorSpawnHandler : BasePacketHandler<ActorSpawnPacket>
{
    public ActorSpawnHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ActorSpawnPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (!IsActorIdInClientRange(packet.ActorId.Value, client.PlayerId, out var minActorId, out var maxActorId))
        {
            SrLogger.LogWarning(
                $"Rejected actor spawn from {client.PlayerId} ({clientEp}); actor id {packet.ActorId.Value} is outside assigned range [{minActorId}, {maxActorId}).",
                SrLogTarget.Both);
            return;
        }

        if (actorManager.Actors.ContainsKey(packet.ActorId.Value))
        {
            SrLogger.LogWarning($"Rejected actor spawn from {client.PlayerId} ({clientEp}); actor {packet.ActorId.Value} already exists.", SrLogTarget.Both);
            return;
        }

        if (!actorManager.TrySpawnNetworkActor(packet.ActorId, packet.Position, packet.Rotation, packet.ActorType, packet.SceneGroup, out _))
        {
            SrLogger.LogWarning(
                $"Rejected actor spawn from {client.PlayerId} ({clientEp}); host could not spawn actor {packet.ActorId.Value} type={packet.ActorType} scene={packet.SceneGroup}.",
                SrLogTarget.Both);
            return;
        }

        actorManager.SetActorOwner(packet.ActorId.Value, client.PlayerId);
        if (IsGadgetType(packet.ActorType))
        {
            SrLogger.LogMessage(
                $"Accepted gadget spawn from {client.PlayerId} ({clientEp}); actor={packet.ActorId.Value}, type={packet.ActorType}, scene={packet.SceneGroup}.",
                SrLogTarget.Main);
        }

        Main.Server.SendToAllExcept(packet, clientEp);
        ActorUpdateSyncManager.ApplyPendingForActor(packet.ActorId.Value);
        GardenGrowthSyncManager.ApplyPendingForActor(packet.ActorId.Value);
        GardenResourceAttachSyncManager.ApplyPendingForActor(packet.ActorId.Value);
    }

    private static bool IsActorIdInClientRange(long actorId, string playerId, out long minActorId, out long maxActorId)
    {
        PlayerIdGenerator.GetClientActorIdRange(playerId, out minActorId, out maxActorId);
        return actorId >= minActorId && actorId < maxActorId;
    }

    private static bool IsGadgetType(int typeId)
        => actorManager.ActorTypes.TryGetValue(typeId, out var type)
           && type
           && type.TryCast<GadgetDefinition>() != null;
}
