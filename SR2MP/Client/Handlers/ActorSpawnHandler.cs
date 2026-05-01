using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;
using Il2CppMonomiPark.SlimeRancher.DataModel;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.ActorSpawn)]
public sealed class ActorSpawnHandler : BaseClientPacketHandler<ActorSpawnPacket>
{
    public ActorSpawnHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(ActorSpawnPacket packet)
    {
        if (actorManager.Actors.ContainsKey(packet.ActorId.Value))
        {
            SrLogger.LogPacketSize($"Actor {packet.ActorId.Value} already exists", SrLogTarget.Both);
            return;
        }

        if (actorManager.TrySpawnNetworkActor(packet.ActorId, packet.Position, packet.Rotation, packet.ActorType, packet.SceneGroup, out _)
            && IsGadgetType(packet.ActorType))
        {
            SrLogger.LogMessage(
                $"Applied gadget spawn from host; actor={packet.ActorId.Value}, type={packet.ActorType}, scene={packet.SceneGroup}.",
                SrLogTarget.Main);
        }

        ActorUpdateSyncManager.ApplyPendingForActor(packet.ActorId.Value);
        GardenGrowthSyncManager.ApplyPendingForActor(packet.ActorId.Value);
        GardenResourceAttachSyncManager.ApplyPendingForActor(packet.ActorId.Value);
    }

    private static bool IsGadgetType(int typeId)
        => actorManager.ActorTypes.TryGetValue(typeId, out var type)
           && type
           && type.TryCast<GadgetDefinition>() != null;
}
