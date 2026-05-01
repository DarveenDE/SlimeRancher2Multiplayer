using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.ConnectRejected)]
public sealed class ConnectRejectHandler : BaseClientPacketHandler<ConnectRejectPacket>
{
    public ConnectRejectHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(ConnectRejectPacket packet)
    {
        var message = string.IsNullOrWhiteSpace(packet.Message)
            ? "The host rejected the connection."
            : packet.Message;

        SrLogger.LogWarning(
            $"Connection rejected by host: {message} HostProtocol={packet.ServerProtocolVersion} HostMod={packet.ServerModVersion} HostGame={packet.ServerRequiredGameVersion}",
            SrLogTarget.Both);

        Client.RejectConnection(message);
    }
}
