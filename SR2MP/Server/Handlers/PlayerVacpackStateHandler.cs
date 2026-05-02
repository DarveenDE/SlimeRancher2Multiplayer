using System.Net;
using SR2MP.Packets.Player;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PlayerVacpackState)]
public sealed class PlayerVacpackStateHandler : BasePacketHandler<PlayerVacpackStatePacket>
{
    public PlayerVacpackStateHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PlayerVacpackStatePacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        packet.PlayerId = client.PlayerId;

        playerManager.UpdateVacpackState(
            client.PlayerId,
            packet.HeldIdentType,
            packet.ActiveSlot,
            packet.WaterLevel);

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
