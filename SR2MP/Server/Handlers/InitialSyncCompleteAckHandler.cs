using System.Net;
using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.InitialSyncCompleteAck)]
public sealed class InitialSyncCompleteAckHandler : BasePacketHandler<InitialSyncCompleteAckPacket>
{
    public InitialSyncCompleteAckHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(InitialSyncCompleteAckPacket packet, IPEndPoint clientEp)
    {
        Main.Server.CompleteInitialSync(clientEp);
    }
}
