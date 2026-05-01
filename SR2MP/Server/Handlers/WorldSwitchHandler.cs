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
        {
            SrLogger.LogWarning(
                $"Rejected world switch update from {DescribeClient(clientEp)}: switch='{packet.ID}', state={packet.State}, immediate={packet.Immediate}.",
                SrLogTarget.Both);
            return;
        }

        packet.IsRepairSnapshot = false;
        Main.Server.SendToAllExcept(packet, clientEp);
    }

    private string DescribeClient(IPEndPoint clientEp)
        => clientManager.TryGetClient(clientEp, out var client) && client != null
            ? $"{client.PlayerId} ({clientEp})"
            : clientEp.ToString();
}
