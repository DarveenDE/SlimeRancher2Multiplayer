using System.Net;
using SR2MP.Packets;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ResyncRequest)]
public sealed class ResyncRequestHandler : BasePacketHandler<EmptyPacket>
{
    public ResyncRequestHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(EmptyPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        client.UpdateHeartbeat();

        if (!client.InitialSyncComplete)
        {
            SrLogger.LogWarning(
                $"Ignoring resync request from {client.PlayerId}; initial sync is not complete.",
                SrLogTarget.Both);
            return;
        }

        WorldStateRepairManager.RequestRepairSnapshot($"client {client.PlayerId} requested resync");
    }
}
