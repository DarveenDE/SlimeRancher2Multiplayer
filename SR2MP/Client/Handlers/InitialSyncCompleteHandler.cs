using SR2MP.Packets.Loading;
using SR2MP.Packets.Player;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.InitialSyncComplete)]
public sealed class InitialSyncCompleteHandler : BaseClientPacketHandler<InitialSyncCompletePacket>
{
    public InitialSyncCompleteHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(InitialSyncCompletePacket packet)
    {
        SendPacket(new InitialSyncCompleteAckPacket());

        var joinPacket = new PlayerJoinPacket
        {
            Type = PacketType.PlayerJoin,
            PlayerId = Client.OwnPlayerId,
            PlayerName = Main.Username
        };

        SendPacket(joinPacket);

        Client.StartHeartbeat();
        Client.NotifyConnected();

        SrLogger.LogMessage("Initial sync complete; joined world", SrLogTarget.Both);
    }
}
