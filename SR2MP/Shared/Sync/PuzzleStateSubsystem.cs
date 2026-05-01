using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Sync;

/// <summary>
/// <see cref="ISyncedSubsystem"/> for puzzle slot and plort-depositor state.
///
/// Replaces <c>SendPuzzleStatesPacket</c> (ConnectHandler Initial-Sync),
/// <c>SendPuzzleSlotSnapshots</c> and <c>SendPlortDepositorSnapshots</c>
/// (WorldStateRepairManager Repair) with a single snapshot pair.
///
/// Live events (<c>PuzzleSlotStatePacket</c>, <c>PlortDepositorStatePacket</c>)
/// still flow through the existing handlers — unchanged.
/// </summary>
public sealed class PuzzleStateSubsystem : ISyncedSubsystem
{
    public static readonly PuzzleStateSubsystem Instance = new();

    private PuzzleStateSubsystem() { }

    public byte Id => SubsystemIds.PuzzleState;
    public string Name => "PuzzleState";

    /// <summary>Serialises all puzzle slots and plort depositors from the game model.</summary>
    public void CaptureSnapshot(PacketWriter writer)
    {
        var slots = new List<InitialPuzzleStatesPacket.PuzzleSlot>();
        var depositors = new List<InitialPuzzleStatesPacket.PlortDepositor>();

        if (SceneContext.Instance && SceneContext.Instance.GameModel)
        {
            foreach (var slot in SceneContext.Instance.GameModel.slots)
            {
                slots.Add(new InitialPuzzleStatesPacket.PuzzleSlot
                {
                    ID = slot.Key,
                    Filled = slot.Value.filled,
                });
            }

            foreach (var depositor in SceneContext.Instance.GameModel.depositors)
            {
                depositors.Add(new InitialPuzzleStatesPacket.PlortDepositor
                {
                    ID = depositor.Key,
                    AmountDeposited = depositor.Value.AmountDeposited,
                });
            }
        }

        writer.WriteList(slots, PacketWriterDels.NetObject<InitialPuzzleStatesPacket.PuzzleSlot>.Func);
        writer.WriteList(depositors, PacketWriterDels.NetObject<InitialPuzzleStatesPacket.PlortDepositor>.Func);
    }

    /// <summary>
    /// Deserialises and applies all puzzle slot and plort-depositor states.
    /// Idempotent — applying the same state twice produces no visible change.
    /// </summary>
    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var slots = reader.ReadList(PacketReaderDels.NetObject<InitialPuzzleStatesPacket.PuzzleSlot>.Func);
        var depositors = reader.ReadList(PacketReaderDels.NetObject<InitialPuzzleStatesPacket.PlortDepositor>.Func);

        var sourceStr = source.ToSourceString();
        foreach (var slot in slots)
            PuzzleStateSyncManager.ApplySlotState(slot.ID, slot.Filled, sourceStr);

        foreach (var depositor in depositors)
            PuzzleStateSyncManager.ApplyDepositorState(depositor.ID, depositor.AmountDeposited, sourceStr);
    }
}
