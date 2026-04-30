using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.CommStationPlayed)]
public sealed class CommStationPlayedHandler : BasePacketHandler<CommStationPlayedPacket>
{
    public CommStationPlayedHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(CommStationPlayedPacket packet, IPEndPoint clientEp)
    {
        CommStationSyncManager.Apply(
            packet,
            packet.IsRepairSnapshot ? "server repair comm station" : "server comm station");

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
