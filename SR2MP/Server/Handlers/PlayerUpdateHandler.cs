using System.Net;
using SR2MP.Packets.Player;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PlayerUpdate)]
public sealed class PlayerUpdateHandler : BasePacketHandler<PlayerUpdatePacket>
{
    public PlayerUpdateHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PlayerUpdatePacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        var playerId = client.PlayerId;

        if (playerId == "HOST" || playerManager.GetPlayer(playerId) == null)
            return;

        packet.PlayerId = playerId;

        playerManager.UpdatePlayer(
            playerId,
            packet.Position,
            packet.SceneGroup,
            packet.Rotation,
            packet.HorizontalMovement,
            packet.ForwardMovement,
            packet.Yaw,
            packet.AirborneState,
            packet.Moving,
            packet.HorizontalSpeed,
            packet.ForwardSpeed,
            packet.Sprinting,
            packet.LookY
        );

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
