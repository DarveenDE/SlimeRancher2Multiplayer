using SR2MP.Packets.Landplot;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.GardenPlant)]
public sealed class GardenPlantHandler : BaseClientPacketHandler<GardenPlantPacket>
{
    public GardenPlantHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(GardenPlantPacket packet)
    {
        handlingPacket = true;
        try
        {
            GardenPlotSyncManager.ApplyRemoteState(
                packet.ID,
                packet.HasCrop,
                packet.ActorType,
                packet.IsRepairSnapshot ? "client repair garden plant" : "client garden plant");
        }
        finally { handlingPacket = false; }
    }
}
