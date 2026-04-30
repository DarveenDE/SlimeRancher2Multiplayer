using SR2MP.Packets.Switch;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.SwitchActivate)]
public sealed class WorldSwitchHandler : BaseClientPacketHandler<WorldSwitchPacket>
{
    public WorldSwitchHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(WorldSwitchPacket packet)
    {
        WorldEventStateSyncManager.ApplySwitchState(
            packet,
            packet.IsRepairSnapshot ? "client repair world switch" : "client world switch");
    }
}
