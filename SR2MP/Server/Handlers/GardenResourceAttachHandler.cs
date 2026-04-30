using System.Net;
using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ResourceAttach)]
public sealed class GardenResourceAttachHandler : BasePacketHandler<ResourceAttachPacket>
{
    public GardenResourceAttachHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ResourceAttachPacket packet, IPEndPoint clientEp)
    {
        if (GardenResourceAttachSyncManager.IsGardenAttachPacket(packet))
        {
            SrLogger.LogDebug("Ignored client garden resource attach; the host is authoritative for garden produce.", SrLogTarget.Main);
            return;
        }

        var result = GardenResourceAttachSyncManager.ApplyOrQueue(packet, "server resource attach");
        if (result == ResourceAttachApplyResult.Failed)
            return;

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
