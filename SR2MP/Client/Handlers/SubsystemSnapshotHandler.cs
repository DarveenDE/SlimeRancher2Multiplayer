using SR2MP.Packets.Sync;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Sync;

namespace SR2MP.Client.Handlers;

/// <summary>
/// Generic client handler for <see cref="SubsystemSnapshotPacket"/>.
/// Routes incoming snapshots to the correct <see cref="ISyncedSubsystem"/> via
/// <see cref="SubsystemRegistry.Instance"/>.
///
/// Replaces dedicated <c>Initial*LoadHandler</c> classes for all subsystems
/// that have been migrated to <see cref="ISyncedSubsystem"/>.
/// </summary>
[PacketHandler((byte)PacketType.SubsystemSnapshot)]
public sealed class SubsystemSnapshotHandler : BaseClientPacketHandler<SubsystemSnapshotPacket>
{
    public SubsystemSnapshotHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(SubsystemSnapshotPacket packet)
        => SubsystemRegistry.Instance.ApplySnapshot(packet);
}
