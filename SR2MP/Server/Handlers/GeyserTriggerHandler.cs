using System.Net;
using SR2MP.Packets.Geyser;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.GeyserTrigger)]
public sealed class GeyserTriggerHandler : BasePacketHandler<GeyserTriggerPacket>
{
    public GeyserTriggerHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(GeyserTriggerPacket packet, IPEndPoint clientEp)
    {
        var geyserObject = GameObject.Find(packet.ObjectPath);
        if (!geyserObject)
            return;

        var geyser = geyserObject.GetComponent<Geyser>();
        if (!geyser)
            return;

        RunWithHandlingPacket(() => geyser.StartCoroutine(geyser.RunGeyser(packet.Duration)));

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
