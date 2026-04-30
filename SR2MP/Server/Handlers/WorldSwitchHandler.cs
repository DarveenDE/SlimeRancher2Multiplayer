using System.Net;
using SR2MP.Packets.Switch;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.SwitchActivate)]
public sealed class WorldSwitchHandler : BasePacketHandler<WorldSwitchPacket>
{
    public WorldSwitchHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(WorldSwitchPacket packet, IPEndPoint clientEp)
    {
        if (!WorldEventStateSyncManager.ApplySwitchState(packet, "server world switch"))
            return;

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
