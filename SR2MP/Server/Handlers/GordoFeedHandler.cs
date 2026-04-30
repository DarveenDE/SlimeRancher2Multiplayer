using System.Net;
using SR2MP.Packets.Gordo;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.GordoFeed)]
public sealed class GordoFeedHandler : BasePacketHandler<GordoFeedPacket>
{
    public GordoFeedHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(GordoFeedPacket packet, IPEndPoint clientEp)
    {
        if (!WorldEventStateSyncManager.ApplyGordoFeed(packet, "server gordo feed"))
            return;

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
