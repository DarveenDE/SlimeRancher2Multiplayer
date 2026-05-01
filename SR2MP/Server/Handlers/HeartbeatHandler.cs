using System.Net;
using SR2MP.Packets;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.Heartbeat)]
public sealed class HeartbeatHandler : BasePacketHandler<EmptyPacket>
{
    public HeartbeatHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(EmptyPacket packet, IPEndPoint clientEp)
    {
        var gap = clientManager.UpdateHeartbeat(clientEp);
        if (Main.SyncDiagnosticsEnabled
            && gap.HasValue
            && gap.Value > TimeSpan.FromSeconds(HeartbeatSettings.IntervalSeconds + 3))
        {
            SrLogger.LogWarning(
                $"Heartbeat gap from {DescribeClient(clientEp)}: {gap.Value.TotalSeconds:0.00}s since last heartbeat.",
                SrLogTarget.Both);
        }

        Main.Server.SendToClient(new EmptyPacket
        {
            Type = PacketType.HeartbeatAck,
            Reliability = PacketReliability.Unreliable,
        }, clientEp);
    }

    private string DescribeClient(IPEndPoint clientEp)
        => clientManager.TryGetClient(clientEp, out var client) && client != null
            ? $"{client.PlayerId} ({clientEp})"
            : clientEp.ToString();
}
