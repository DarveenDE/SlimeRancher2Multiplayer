using System.Net;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.GardenGrowthState)]
public sealed class GardenGrowthHandler : BasePacketHandler<GardenGrowthPacket>
{
    public GardenGrowthHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(GardenGrowthPacket packet, IPEndPoint senderEndPoint)
    {
        SrLogger.LogDebug("Ignored client garden growth state; the host is authoritative for garden growth.", SrLogTarget.Main);
    }
}
