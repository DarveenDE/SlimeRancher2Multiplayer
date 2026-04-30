using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.CommStationPlayed)]
public sealed class CommStationPlayedHandler : BaseClientPacketHandler<CommStationPlayedPacket>
{
    public CommStationPlayedHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(CommStationPlayedPacket packet)
    {
        CommStationSyncManager.Apply(
            packet,
            packet.IsRepairSnapshot ? "client repair comm station" : "client comm station");
    }
}
