using SR2MP.Packets.FX;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.PlayerLoopSound)]
public sealed class PlayerLoopSoundHandler : BaseClientPacketHandler<PlayerLoopSoundPacket>
{
    public PlayerLoopSoundHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(PlayerLoopSoundPacket packet)
        => PlayerLoopSoundManager.Apply(packet);
}
