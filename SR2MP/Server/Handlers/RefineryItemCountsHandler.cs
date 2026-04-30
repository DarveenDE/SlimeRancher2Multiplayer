using System.Net;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.RefineryItemCounts)]
public sealed class RefineryItemCountsHandler : BasePacketHandler<RefineryItemCountsPacket>
{
    public RefineryItemCountsHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(RefineryItemCountsPacket packet, IPEndPoint senderEndPoint)
    {
        try
        {
            handlingPacket = true;
            RefinerySyncManager.ApplyCounts(packet.Items, "server refinery counts");
        }
        finally
        {
            handlingPacket = false;
        }

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }
}
