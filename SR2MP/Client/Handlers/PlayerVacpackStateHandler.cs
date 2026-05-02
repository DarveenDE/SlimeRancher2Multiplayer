using SR2MP.Packets.Player;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.PlayerVacpackState)]
public sealed class PlayerVacpackStateHandler : BaseClientPacketHandler<PlayerVacpackStatePacket>
{
    public PlayerVacpackStateHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(PlayerVacpackStatePacket packet)
    {
        if (packet.PlayerId == Client.OwnPlayerId)
            return;

        PlayerManager.UpdateVacpackState(
            packet.PlayerId,
            packet.HeldIdentType,
            packet.ActiveSlot,
            packet.WaterLevel);
    }
}
