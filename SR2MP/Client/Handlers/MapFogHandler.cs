using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.MapFogReveal)]
public sealed class MapFogHandler : BaseClientPacketHandler<MapFogPacket>
{
    public MapFogHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(MapFogPacket packet)
    {
        MapUnlockSyncManager.ApplyFogReveal(packet.Position, packet.Radius);
    }
}
