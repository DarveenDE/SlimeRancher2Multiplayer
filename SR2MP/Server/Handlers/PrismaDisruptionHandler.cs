using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PrismaDisruption)]
public sealed class PrismaDisruptionHandler : BasePacketHandler<PrismaDisruptionPacket>
{
    public PrismaDisruptionHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PrismaDisruptionPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (!CheckAuthority(packet, client.PlayerId, clientEp).IsAllowed)
            return;

        PrismaDisruptionSyncManager.Apply(packet.DisruptionLevel);
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
