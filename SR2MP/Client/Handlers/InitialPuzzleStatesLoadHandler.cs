using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.InitialPuzzleStates)]
public sealed class InitialPuzzleStatesLoadHandler : BaseClientPacketHandler<InitialPuzzleStatesPacket>
{
    public InitialPuzzleStatesLoadHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(InitialPuzzleStatesPacket packet)
    {
        foreach (var slot in packet.Slots)
            PuzzleStateSyncManager.ApplySlotState(slot.ID, slot.Filled, "initial puzzle slot");

        foreach (var depositor in packet.Depositors)
            PuzzleStateSyncManager.ApplyDepositorState(depositor.ID, depositor.AmountDeposited, "initial plort depositor");
    }
}
