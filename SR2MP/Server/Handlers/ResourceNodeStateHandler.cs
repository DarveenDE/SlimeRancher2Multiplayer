using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ResourceNodeState)]
public sealed class ResourceNodeStateHandler : BasePacketHandler<ResourceNodeStatePacket>
{
    public ResourceNodeStateHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ResourceNodeStatePacket packet, IPEndPoint clientEp)
    {
        ResourceNodeSyncManager.Apply(
            packet,
            packet.IsRepairSnapshot ? "server repair resource nodes" : "server resource nodes");

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
