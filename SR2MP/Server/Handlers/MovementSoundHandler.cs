using System.Net;
using SR2MP.Packets.FX;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.MovementSound)]
public sealed class MovementSoundHandler : BasePacketHandler<MovementSoundPacket>
{
    public MovementSoundHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(MovementSoundPacket packet, IPEndPoint clientEp)
    {
        if (string.IsNullOrWhiteSpace(packet.CueName)
            || !fxManager.AllCues.TryGetValue(packet.CueName, out var cue)
            || !cue)
        {
            SrLogger.LogWarning($"Ignoring movement sound with unknown cue '{packet.CueName}'.", SrLogTarget.Both);
            return;
        }

        RemoteFXManager.PlayTransientAudio(cue, packet.Position, 0.45f);

        Main.Server.SendToAllExcept(packet, clientEp);
    }
}
