using System.Net;
using SR2MP.Packets.Gordo;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.GordoBurst)]
public sealed class GordoBurstHandler : BasePacketHandler<GordoBurstPacket>
{
    public GordoBurstHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(GordoBurstPacket packet, IPEndPoint clientEp)
    {
        if (!WorldEventStateSyncManager.ApplyGordoBurst(packet, "server gordo burst"))
            return;

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
