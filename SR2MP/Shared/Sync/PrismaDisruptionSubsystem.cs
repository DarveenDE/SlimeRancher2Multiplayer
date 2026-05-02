using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Sync;

public sealed class PrismaDisruptionSubsystem : ISyncedSubsystem
{
    public static readonly PrismaDisruptionSubsystem Instance = new();

    private PrismaDisruptionSubsystem() { }

    public byte Id => SubsystemIds.PrismaDisruption;
    public string Name => "PrismaDisruption";

    public void CaptureSnapshot(PacketWriter writer)
    {
        writer.WriteByte(PrismaDisruptionSyncManager.GetCurrentDisruptionLevel() ?? 0);
    }

    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var level = reader.ReadByte();
        PrismaDisruptionSyncManager.Apply(level);
    }
}
