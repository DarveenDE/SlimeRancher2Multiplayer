using SR2MP.Packets.World;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.BroadcastFastForward)]
public sealed class FastForwardHandler : BaseClientPacketHandler<WorldTimePacket>
{
    public FastForwardHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(WorldTimePacket packet)
    {
        if (!double.IsFinite(packet.Time))
            return;

        RunWithHandlingPacket(() => SceneContext.Instance.TimeDirector.FastForwardTo(packet.Time));
    }
}
