using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.PuzzleSlotState)]
public sealed class PuzzleSlotStateHandler : BaseClientPacketHandler<PuzzleSlotStatePacket>
{
    public PuzzleSlotStateHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(PuzzleSlotStatePacket packet)
    {
        PuzzleStateSyncManager.ApplySlotState(packet.ID, packet.Filled, "client puzzle slot");
    }
}
