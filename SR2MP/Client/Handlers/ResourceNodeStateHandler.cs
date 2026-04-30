using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.ResourceNodeState)]
public sealed class ResourceNodeStateHandler : BaseClientPacketHandler<ResourceNodeStatePacket>
{
    public ResourceNodeStateHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(ResourceNodeStatePacket packet)
    {
        ResourceNodeSyncManager.Apply(
            packet,
            packet.IsRepairSnapshot ? "client repair resource nodes" : "client resource nodes");
    }
}
