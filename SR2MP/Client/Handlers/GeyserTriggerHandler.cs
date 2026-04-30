using SR2MP.Packets.Geyser;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.GeyserTrigger)]
public sealed class GeyserTriggerHandler : BaseClientPacketHandler<GeyserTriggerPacket>
{
    public GeyserTriggerHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(GeyserTriggerPacket packet)
    {
        var geyserObject = GameObject.Find(packet.ObjectPath);
        if (!geyserObject)
            return;

        var geyser = geyserObject.GetComponent<Geyser>();
        if (!geyser)
            return;

        RunWithHandlingPacket(() => geyser.StartCoroutine(geyser.RunGeyser(packet.Duration)));
    }
}
