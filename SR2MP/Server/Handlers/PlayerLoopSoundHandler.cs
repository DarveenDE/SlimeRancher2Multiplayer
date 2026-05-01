using System.Net;
using SR2MP.Packets.FX;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PlayerLoopSound)]
public sealed class PlayerLoopSoundHandler : BasePacketHandler<PlayerLoopSoundPacket>
{
    public PlayerLoopSoundHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PlayerLoopSoundPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        packet.Player = client.PlayerId;
        PlayerLoopSoundManager.Apply(packet);

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
