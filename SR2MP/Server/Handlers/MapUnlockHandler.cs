using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.MapUnlock)]
public sealed class MapUnlockHandler : BasePacketHandler<MapUnlockPacket>
{
    public MapUnlockHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(MapUnlockPacket packet, IPEndPoint clientEp)
    {
        if (!WorldEventStateSyncManager.ApplyMapUnlock(packet, "server map unlock"))
            return;

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
