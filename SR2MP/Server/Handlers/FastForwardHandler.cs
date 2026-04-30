using System.Net;
using SR2MP.Packets.World;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.FastForward)]
public sealed class FastForwardHandler : BasePacketHandler<WorldTimePacket>
{
    private const double MaxClientFastForwardHours = 48d;

    public FastForwardHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(WorldTimePacket packet, IPEndPoint clientEp)
    {
        var currentTime = SceneContext.Instance.TimeDirector._worldModel.worldTime;
        if (!double.IsFinite(packet.Time)
            || packet.Time < currentTime
            || packet.Time - currentTime > MaxClientFastForwardHours)
        {
            SrLogger.LogWarning(
                $"Ignoring invalid client fast-forward request to {packet.Time:0.##}; current time is {currentTime:0.##}.",
                SrLogTarget.Both);
            return;
        }

        RunWithHandlingPacket(() => SceneContext.Instance.TimeDirector.FastForwardTo(packet.Time));

        Main.Server.SendToAllExcept(packet with
        {
            Type = PacketType.BroadcastFastForward
        }, clientEp);
    }
}
