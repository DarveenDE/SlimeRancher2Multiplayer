using System.Reflection;
using SR2MP.Packets.World;

namespace SR2MP.Shared.Managers;

public static class PrismaDisruptionSyncManager
{
    private static bool _setLevelResolved;
    private static MethodInfo? _setLevelMethod;
    private static bool _modelAccessResolved;
    private static MethodInfo? _getModelMethod;
    private static FieldInfo? _modelField;
    private static PropertyInfo? _levelProp;

    public static byte? GetCurrentDisruptionLevel()
    {
        var dir = GetPrismaDirector();
        if (dir == null) return null;

        var model = GetModel(dir);
        if (model == null) return null;

        _levelProp ??= model.GetType().GetProperty("DisruptionLevel");
        var level = _levelProp?.GetValue(model);
        return level != null ? (byte)Convert.ToInt32(level) : (byte)0;
    }

    public static void Apply(byte level)
    {
        var dir = GetPrismaDirector();
        if (dir == null) return;

        ResolveSetLevel(dir);
        if (_setLevelMethod == null) return;

        var levelType = _setLevelMethod.GetParameters()[0].ParameterType;
        var enumVal = Enum.ToObject(levelType, (int)level);
        var activate = level != 0;

        RunWithHandlingPacket(() =>
        {
            _setLevelMethod.Invoke(dir, new object[] { enumVal, activate });
        });
    }

    public static void BroadcastCurrentLevel()
    {
        var level = GetCurrentDisruptionLevel();
        if (!level.HasValue) return;
        Main.SendToAllOrServer(new PrismaDisruptionPacket { DisruptionLevel = level.Value });
    }

    public static void Clear()
    {
        _setLevelResolved = false;
        _setLevelMethod = null;
        _modelAccessResolved = false;
        _getModelMethod = null;
        _modelField = null;
        _levelProp = null;
    }

    private static void ResolveSetLevel(object dir)
    {
        if (_setLevelResolved) return;
        _setLevelResolved = true;

        var methods = dir.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
        // Try SetPrismaDisruptionLevel(DisruptionLevel, bool) first (Patch 1.2+)
        _setLevelMethod = methods.FirstOrDefault(m =>
            m.Name == "SetPrismaDisruptionLevel" && m.GetParameters().Length == 2);
        // Fall back to SetDisruptionLevel(DisruptionLevel, bool) public 2-param overload
        _setLevelMethod ??= methods.FirstOrDefault(m =>
            m.Name == "SetDisruptionLevel" && m.GetParameters().Length == 2);
    }

    private static object? GetModel(object dir)
    {
        if (!_modelAccessResolved)
        {
            _modelAccessResolved = true;
            _getModelMethod = dir.GetType().GetMethod("GetPrismaDisruptionModel");
            if (_getModelMethod == null)
                _modelField = dir.GetType().GetField("_model",
                    BindingFlags.NonPublic | BindingFlags.Instance);
        }

        if (_getModelMethod != null)
            return _getModelMethod.Invoke(dir, null);
        return _modelField?.GetValue(dir);
    }

    private static object? GetPrismaDirector()
    {
        var sceneContext = SceneContext.Instance;
        if (!sceneContext) return null;
        return typeof(SceneContext).GetProperty("PrismaDirector")?.GetValue(sceneContext);
    }
}

