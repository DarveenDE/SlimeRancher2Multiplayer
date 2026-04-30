using SR2MP.Packets.Landplot;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.LandPlotUpdate)]
public sealed class LandPlotUpdateHandler : BaseClientPacketHandler<LandPlotUpdatePacket>
{
    public LandPlotUpdateHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(LandPlotUpdatePacket packet)
    {
        WorldEventStateSyncManager.ApplyLandPlotUpdate(
            packet,
            packet.IsRepairSnapshot ? "client repair land plot update" : "client land plot update");
    }
}
