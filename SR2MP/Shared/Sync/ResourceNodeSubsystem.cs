using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Sync;

/// <summary>
/// <see cref="ISyncedSubsystem"/> for resource-node spawner state.
///
/// Replaces:
/// - <c>SendResourceNodesPacket</c> (ConnectHandler Initial-Sync)
/// - <c>SendResourceNodeSnapshots</c> (WorldStateRepairManager Repair)
///
/// Live events (<c>ResourceNodeStatePacket</c>) still flow through existing handlers.
/// </summary>
public sealed class ResourceNodeSubsystem : ISyncedSubsystem
{
    public static readonly ResourceNodeSubsystem Instance = new();

    private ResourceNodeSubsystem() { }

    public byte Id => SubsystemIds.ResourceNode;
    public string Name => "ResourceNode";

    /// <summary>Serialises all current resource-node states.</summary>
    public void CaptureSnapshot(PacketWriter writer)
    {
        var nodes = ResourceNodeSyncManager.CreateSnapshot();
        writer.WriteList(nodes, PacketWriterDels.NetObject<ResourceNodeStatePacket.NodeStateData>.Func);
    }

    /// <summary>
    /// Deserialises and applies all resource-node states.
    /// Idempotent.
    /// </summary>
    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var nodes = reader.ReadList(PacketReaderDels.NetObject<ResourceNodeStatePacket.NodeStateData>.Func);
        ResourceNodeSyncManager.Apply(new ResourceNodeStatePacket
        {
            Nodes = nodes,
            IsRepairSnapshot = source == SyncSource.Repair,
        }, source.ToSourceString());
    }
}
