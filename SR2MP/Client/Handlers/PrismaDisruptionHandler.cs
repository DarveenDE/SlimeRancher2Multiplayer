using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.PrismaDisruption)]
public sealed class PrismaDisruptionHandler : BaseClientPacketHandler<PrismaDisruptionPacket>
{
    public PrismaDisruptionHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(PrismaDisruptionPacket packet)
        => PrismaDisruptionSyncManager.Apply(packet);
}
