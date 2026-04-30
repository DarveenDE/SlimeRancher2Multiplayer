using SR2MP.Packets.Gordo;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.GordoBurst)]
public sealed class GordoBurstHandler : BaseClientPacketHandler<GordoBurstPacket>
{
    public GordoBurstHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(GordoBurstPacket packet)
    {
        WorldEventStateSyncManager.ApplyGordoBurst(
            packet,
            packet.IsRepairSnapshot ? "client repair gordo burst" : "client gordo burst");
    }
}
