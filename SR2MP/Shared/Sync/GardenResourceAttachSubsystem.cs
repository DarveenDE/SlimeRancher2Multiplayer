using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Sync;

/// <summary>
/// <see cref="ISyncedSubsystem"/> for garden resource attach state.
///
/// Replaces:
/// - <c>SendGardenResourceAttachments</c> (ConnectHandler Initial-Sync)
/// - The inline loop in <c>SendLandPlotSnapshots</c> (WorldStateRepairManager Repair)
///
/// Live events (<c>ResourceAttachPacket</c>) still flow through existing handlers.
/// </summary>
public sealed class GardenResourceAttachSubsystem : ISyncedSubsystem
{
    public static readonly GardenResourceAttachSubsystem Instance = new();

    private GardenResourceAttachSubsystem() { }

    public byte Id => SubsystemIds.GardenResourceAttach;
    public string Name => "GardenResourceAttach";

    /// <summary>Serialises all current garden resource attach states.</summary>
    public void CaptureSnapshot(PacketWriter writer)
    {
        var packets = new List<ResourceAttachPacket>();

        if (SceneContext.Instance && SceneContext.Instance.GameModel)
            packets = GardenResourceAttachSyncManager.CreateGardenSnapshots(SceneContext.Instance.GameModel);

        writer.WriteInt(packets.Count);
        foreach (var packet in packets)
            packet.Serialise(writer);
    }

    /// <summary>
    /// Deserialises and applies all garden resource attachments.
    /// </summary>
    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var count = reader.ReadInt();
        var sourceStr = source.ToSourceString();

        for (var i = 0; i < count; i++)
        {
            var packet = new ResourceAttachPacket();
            packet.Deserialise(reader);
            GardenResourceAttachSyncManager.ApplyOrQueue(packet, $"{sourceStr} garden resource attach");
        }
    }
}
