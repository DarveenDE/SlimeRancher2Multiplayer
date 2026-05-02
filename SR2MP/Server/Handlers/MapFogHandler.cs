using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.MapFogReveal)]
public sealed class MapFogHandler : BasePacketHandler<MapFogPacket>
{
    public MapFogHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(MapFogPacket packet, IPEndPoint clientEp)
    {
        MapUnlockSyncManager.ApplyFogReveal(packet.Position, packet.Radius);
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
