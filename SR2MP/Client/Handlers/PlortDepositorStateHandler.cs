using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.PlortDepositorState)]
public sealed class PlortDepositorStateHandler : BaseClientPacketHandler<PlortDepositorStatePacket>
{
    public PlortDepositorStateHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(PlortDepositorStatePacket packet)
    {
        PuzzleStateSyncManager.ApplyDepositorState(
            packet.ID,
            packet.AmountDeposited,
            packet.IsRepairSnapshot ? "client repair plort depositor" : "client plort depositor");
    }
}
