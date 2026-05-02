using Il2CppMonomiPark.SlimeRancher.Labyrinth;
using SR2MP.Packets.World;

namespace SR2MP.Shared.Managers;

public static class PrismaDisruptionSyncManager
{
    public static void BroadcastLevel(PrismaDirector.DisruptionArea area, DisruptionLevel level)
    {
        var areaId = GetAreaPersistenceId(area.Definition);
        if (areaId < 0) return;
        Main.SendToAllOrServer(new PrismaDisruptionPacket
        {
            AreaPersistenceId = areaId,
            DisruptionLevel = (byte)level,
        });
    }

    public static void BroadcastAllAreas()
    {
        var dir = GetPrismaDirector();
        if (dir == null) return;

        foreach (var kvp in dir._disruptionAreas)
        {
            var areaId = GetAreaPersistenceId(kvp.Key);
            if (areaId < 0) continue;
            Main.SendToAllOrServer(new PrismaDisruptionPacket
            {
                AreaPersistenceId = areaId,
                DisruptionLevel = (byte)kvp.Value.DisruptionLevel,
            });
        }
    }

    public static void Apply(PrismaDisruptionPacket packet)
    {
        var dir = GetPrismaDirector();
        if (dir == null) return;

        PrismaDirector.DisruptionArea? foundArea = null;
        foreach (var kvp in dir._disruptionAreas)
        {
            if (GetAreaPersistenceId(kvp.Key) == packet.AreaPersistenceId)
            {
                foundArea = kvp.Value;
                break;
            }
        }

        if (foundArea == null) return;

        var level = (DisruptionLevel)packet.DisruptionLevel;
        RunWithHandlingPacket(() => dir.SetDisruptionLevel(foundArea, level, false));
    }

    // Kept for scene-unload cleanup callers.
    public static void Clear() { }

    public static List<(int areaId, byte level)> GetAllAreaLevels()
    {
        var result = new List<(int, byte)>();
        var dir = GetPrismaDirector();
        if (dir == null) return result;

        foreach (var kvp in dir._disruptionAreas)
        {
            var areaId = GetAreaPersistenceId(kvp.Key);
            if (areaId < 0) continue;
            result.Add((areaId, (byte)kvp.Value.DisruptionLevel));
        }

        return result;
    }

    // Accessed via reflection so this compiles and runs on SR2 builds that
    // predate Radiant Slime Sanctuary (1.2) where the PrismaDirector property is absent.
    private static PrismaDirector? GetPrismaDirector()
    {
        var sceneContext = SceneContext.Instance;
        if (!sceneContext) return null;
        return typeof(SceneContext).GetProperty("PrismaDirector")
            ?.GetValue(sceneContext) as PrismaDirector;
    }

    private static int GetAreaPersistenceId(DisruptionAreaDefinition definition)
    {
        var translation = GameContext.Instance?.AutoSaveDirector?._saveReferenceTranslation;
        if (translation == null) return -1;
        return translation.GetPersistenceId(definition);
    }
}
