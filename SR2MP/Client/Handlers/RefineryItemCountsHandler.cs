using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.RefineryItemCounts)]
public sealed class RefineryItemCountsHandler : BaseClientPacketHandler<RefineryItemCountsPacket>
{
    public RefineryItemCountsHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(RefineryItemCountsPacket packet)
    {
        try
        {
            handlingPacket = true;
            RefinerySyncManager.ApplyCounts(
                packet.Items,
                packet.IsRepairSnapshot ? "client repair refinery counts" : "client refinery counts");
        }
        finally
        {
            handlingPacket = false;
        }
    }
}
