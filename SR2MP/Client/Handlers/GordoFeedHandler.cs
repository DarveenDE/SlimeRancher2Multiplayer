using SR2MP.Packets.Gordo;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.GordoFeed)]
public sealed class GordoFeedHandler : BaseClientPacketHandler<GordoFeedPacket>
{
    public GordoFeedHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(GordoFeedPacket packet)
    {
        WorldEventStateSyncManager.ApplyGordoFeed(
            packet,
            packet.IsRepairSnapshot ? "client repair gordo feed" : "client gordo feed");
    }
}
