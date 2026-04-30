using SR2MP.Packets.Landplot;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.GardenGrowthState)]
public sealed class GardenGrowthHandler : BaseClientPacketHandler<GardenGrowthPacket>
{
    public GardenGrowthHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(GardenGrowthPacket packet)
    {
        handlingPacket = true;
        try
        {
            GardenGrowthSyncManager.ApplyState(
                packet,
                packet.IsRepairSnapshot ? "client repair garden growth" : "client garden growth");
        }
        finally { handlingPacket = false; }
    }
}
