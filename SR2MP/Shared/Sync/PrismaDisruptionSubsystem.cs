using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
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
        var areas = PrismaDisruptionSyncManager.GetAllAreaLevels();
        writer.WriteInt(areas.Count);
        foreach (var (areaId, level) in areas)
        {
            writer.WriteInt(areaId);
            writer.WriteByte(level);
        }
    }

    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var count = reader.ReadInt();
        for (var i = 0; i < count; i++)
        {
            var areaId = reader.ReadInt();
            var level = reader.ReadByte();
            PrismaDisruptionSyncManager.Apply(new PrismaDisruptionPacket
            {
                AreaPersistenceId = areaId,
                DisruptionLevel = level,
            });
        }
    }
}
