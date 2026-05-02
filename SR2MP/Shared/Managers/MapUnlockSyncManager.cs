using System.Collections;
using Il2CppMonomiPark.SlimeRancher.Event;
using Il2CppMonomiPark.SlimeRancher.UI.Map;
using MelonLoader;
using SR2MP.Packets.World;

namespace SR2MP.Shared.Managers;

public static class MapUnlockSyncManager
{
    private static bool refreshQueued;

    public static List<string> CreateSnapshot()
    {
        var nodes = new HashSet<string>(StringComparer.Ordinal);

        if (TryGetMapUnlockTable(out var table))
        {
            foreach (var entry in table)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value.count > 0)
                    nodes.Add(entry.Key);
            }
        }

        foreach (var element in Resources.FindObjectsOfTypeAll<FogMapElement>())
        {
            if (!element || !TryGetFogRevealNode(element, out var nodeId))
                continue;

            if (IsUnlocked(nodeId))
                nodes.Add(nodeId);
        }

        return nodes.ToList();
    }

    public static int ReplaceSnapshot(IEnumerable<string> nodeIds, string source)
    {
        if (!TryGetMapUnlockTable(out var table))
            return 0;

        var desiredNodes = new HashSet<string>(
            nodeIds.Where(nodeId => !string.IsNullOrWhiteSpace(nodeId)),
            StringComparer.Ordinal);

        var currentNodes = GetUnlockedNodes(table);
        if (currentNodes.SetEquals(desiredNodes))
        {
            QueueVisualRefresh();
            return 0;
        }

        table.Clear();
        var applied = 0;
        foreach (var nodeId in desiredNodes)
        {
            table[nodeId] = CreateEntry(nodeId, null);
            applied++;
        }

        NotifyEventParticipants();
        RefreshVisibleFogElements();
        QueueVisualRefresh();

        if (IsRepairSource(source))
            SrLogger.LogMessage($"Repair refreshed map unlock snapshot ({applied} node(s)).", SrLogTarget.Main);

        return applied;
    }

    public static int ApplySnapshot(IEnumerable<string> nodeIds, string source)
    {
        var applied = 0;
        foreach (var nodeId in nodeIds)
        {
            if (ApplyUnlock(nodeId, source))
                applied++;
        }

        QueueVisualRefresh();
        return applied;
    }

    public static bool ApplyUnlock(MapUnlockPacket packet, string source)
        => ApplyUnlock(packet.NodeID, source);

    // Cached at first call; null means the method doesn't exist in the current SR2 build.
    private static System.Reflection.MethodInfo? _revealFogMethod;
    private static bool _revealFogMethodResolved;

    public static void ApplyFogReveal(Vector3 position, float radius)
    {
        var mapDirector = SceneContext.Instance?.MapDirector;
        if (!mapDirector) return;

        if (!_revealFogMethodResolved)
        {
            _revealFogMethod = typeof(MapDirector).GetMethod("RevealFog",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            _revealFogMethodResolved = true;
        }

        if (_revealFogMethod != null)
        {
            RunWithHandlingPacket(() => _revealFogMethod.Invoke(mapDirector, new object[] { position, radius }));
        }
        else
        {
            // RevealFog not in Il2Cpp bindings for this SR2 build — fall back to visual refresh.
            // LIMITATION: position/radius are ignored; only already-unlocked zones are re-rendered.
            RefreshVisibleFogElements();
            QueueVisualRefresh();
        }
    }

    public static bool ApplyUnlock(string nodeId, string source)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return false;

        if (!TryGetMapUnlockTable(out var table))
            return false;

        var wasUnlocked = table.TryGetValue(nodeId, out var existing) && existing.count > 0;
        table[nodeId] = CreateEntry(nodeId, existing);

        var isSilentSource = IsSilentSource(source);
        var animate = !wasUnlocked && !isSilentSource;

        var gameEvent = FindFogRevealEvent(nodeId);
        if (gameEvent && !wasUnlocked && !isSilentSource)
        {
            RunWithHandlingPacket(() =>
            {
                SceneContext.Instance.MapDirector?.NotifyZoneUnlocked(gameEvent, false, 0);
            });
        }

        NotifyEventParticipants();
        RefreshVisibleFogElement(nodeId, animate);
        RefreshVisibleFogElements();
        QueueVisualRefresh();

        if (IsRepairSource(source) && !wasUnlocked)
            SrLogger.LogMessage($"Repair corrected map unlock '{nodeId}'.", SrLogTarget.Main);

        return true;
    }

    public static void RefreshVisibleFogElements()
    {
        if (!SceneContext.Instance || SceneContext.Instance.eventDirector == null)
            return;

        foreach (var element in Resources.FindObjectsOfTypeAll<FogMapElement>())
        {
            if (!element)
                continue;

            var shouldReveal = element.ShouldBeRevealed(SceneContext.Instance.eventDirector);
            RunWithHandlingPacket(() => element.SetRevealState(shouldReveal));
        }
    }

    public static bool TryGetNodeId(StaticGameEvent gameEvent, out string nodeId)
    {
        nodeId = string.Empty;

        if (!gameEvent
            || gameEvent._eventKey != MapEventKey
            || string.IsNullOrWhiteSpace(gameEvent._dataKey))
        {
            return false;
        }

        nodeId = gameEvent._dataKey;
        return true;
    }

    private static void RefreshVisibleFogElement(string nodeId, bool animate)
    {
        foreach (var element in Resources.FindObjectsOfTypeAll<FogMapElement>())
        {
            if (!element || !TryGetFogRevealNode(element, out var elementNodeId) || elementNodeId != nodeId)
                continue;

            RunWithHandlingPacket(() =>
            {
                element.SetRevealState(true);
                if (animate)
                    element.PlayReveal();
            });
        }
    }

    private static void QueueVisualRefresh()
    {
        if (refreshQueued)
            return;

        refreshQueued = true;
        MelonCoroutines.Start(RefreshVisibleFogWhenReady());
    }

    private static IEnumerator RefreshVisibleFogWhenReady()
    {
        for (var i = 0; i < 5; i++)
            yield return null;

        RefreshVisibleFogElements();
        refreshQueued = false;
    }

    private static bool TryGetFogRevealNode(FogMapElement element, out string nodeId)
    {
        nodeId = string.Empty;

        var fogRevealEvent = element.FogRevealEvent;
        return TryGetNodeId(fogRevealEvent, out nodeId);
    }

    private static StaticGameEvent? FindFogRevealEvent(string nodeId)
        => Resources.FindObjectsOfTypeAll<StaticGameEvent>()
            .FirstOrDefault(x => TryGetNodeId(x, out var eventNodeId) && eventNodeId == nodeId);

    private static HashSet<string> GetUnlockedNodes(CppCollections.Dictionary<string, EventRecordModel.Entry> table)
    {
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in table)
        {
            if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value.count > 0)
                nodes.Add(entry.Key);
        }

        return nodes;
    }

    private static bool IsUnlocked(string nodeId)
    {
        return TryGetMapUnlockTable(out var table)
            && table.TryGetValue(nodeId, out var entry)
            && entry.count > 0;
    }

    private static EventRecordModel.Entry CreateEntry(string nodeId, EventRecordModel.Entry? existing)
    {
        var gameTime = SceneContext.Instance && SceneContext.Instance.TimeDirector
            ? SceneContext.Instance.TimeDirector._worldModel.worldTime
            : 0d;
        var realTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new EventRecordModel.Entry
        {
            count = Math.Max(1, existing?.count ?? 1),
            createdRealTime = existing?.createdRealTime > 0 ? existing.createdRealTime : realTime,
            createdGameTime = existing?.createdGameTime > 0 ? existing.createdGameTime : gameTime,
            dataKey = nodeId,
            eventKey = MapEventKey,
            updatedRealTime = realTime,
            updatedGameTime = gameTime,
        };
    }

    private static bool TryGetMapUnlockTable(out CppCollections.Dictionary<string, EventRecordModel.Entry> table)
    {
        table = null!;

        if (!SceneContext.Instance || SceneContext.Instance.eventDirector == null)
            return false;

        var eventDirModel = SceneContext.Instance.eventDirector._model;
        if (eventDirModel == null)
            return false;

        if (!eventDirModel.table.TryGetValue(MapEventKey, out table))
        {
            table = new CppCollections.Dictionary<string, EventRecordModel.Entry>();
            eventDirModel.table.Add(MapEventKey, table);
        }

        return true;
    }

    private static void NotifyEventParticipants()
    {
        if (!SceneContext.Instance || SceneContext.Instance.eventDirector == null)
            return;

        SceneContext.Instance.eventDirector._model?.NotifyParticipants();
    }

    private static bool IsRepairSource(string source)
        => source.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static bool IsSilentSource(string source)
        => IsRepairSource(source) || source.Contains("initial", StringComparison.OrdinalIgnoreCase);
}
