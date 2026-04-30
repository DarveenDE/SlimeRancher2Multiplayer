using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.MapUnlock)]
public sealed class MapUnlockHandler : BaseClientPacketHandler<MapUnlockPacket>
{
    public MapUnlockHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(MapUnlockPacket packet)
    {
        WorldEventStateSyncManager.ApplyMapUnlock(
            packet,
            packet.IsRepairSnapshot ? "client repair map unlock" : "client map unlock");
    }
}
