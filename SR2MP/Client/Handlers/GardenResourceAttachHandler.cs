using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.ResourceAttach)]
public sealed class GardenResourceAttachHandler : BaseClientPacketHandler<ResourceAttachPacket>
{
    public GardenResourceAttachHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(ResourceAttachPacket packet)
    {
        GardenResourceAttachSyncManager.ApplyOrQueue(packet, "client resource attach");
    }
}
