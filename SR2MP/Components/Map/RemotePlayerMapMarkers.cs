using Il2CppMonomiPark.SlimeRancher.Map;
using Il2CppMonomiPark.SlimeRancher.SceneManagement;
using Il2CppMonomiPark.SlimeRancher.UI;
using Il2CppInterop.Runtime.Attributes;
using MelonLoader;
using SR2MP.Client.Models;
using SR2MP.Shared.Managers;
using UnityEngine.UI;

namespace SR2MP.Components.Map;

[RegisterTypeInIl2Cpp(false)]
public sealed class RemotePlayerMapMarkers : MonoBehaviour
{
    private const string MarkerPrefix = "SR2MP_REMOTE_PLAYER_";
    private const float MarkerUiRefreshInterval = 0.5f;

    private readonly Dictionary<string, MapNavigationMarkerData> markerSources = new();
    private readonly Dictionary<string, string> registeredMapMarkerIds = new();
    private readonly Dictionary<string, Vector3> latestMarkerPositions = new();
    private readonly Dictionary<string, MapDefinition> latestMarkerMaps = new();
    private readonly Dictionary<string, GameObject> radarTargets = new();
    private readonly Dictionary<string, int> radarTargetSceneGroups = new();
    private readonly HashSet<string> registeredPlayerIds = new();

    private MapDirector? currentMapDirector;
    private float nextMarkerUiRefreshTime;
    private int nextMarkerVersion;

    private void OnDestroy()
    {
        ClearAllMarkers();
    }

    private void Update()
    {
        if (!Main.Client.IsConnected && !Main.Server.IsRunning())
        {
            ClearAllMarkers();
            return;
        }

        if (!TryGetMapDirector(out var mapDirector))
            return;

        if (currentMapDirector != mapDirector)
        {
            ClearRegisteredMarkers();
            currentMapDirector = mapDirector;
            nextMarkerUiRefreshTime = 0f;
        }

        var activePlayerIds = new HashSet<string>();

        foreach (var player in playerManager.GetAllPlayers())
        {
            if (player.PlayerId == LocalID)
                continue;

            activePlayerIds.Add(player.PlayerId);
            RefreshMarker(mapDirector, player);
            RefreshCompassMarker(player);
        }

        RemoveStaleMarkers(mapDirector, activePlayerIds);
        RefreshMapUiIfNeeded(mapDirector);
    }

    [HideFromIl2Cpp]
    private void RefreshMarker(MapDirector mapDirector, RemotePlayer player)
    {
        if (!TryResolveSceneGroup(player.SceneGroup, out var sceneGroup))
            return;

        if (!TryGetMapForSceneGroup(mapDirector, sceneGroup, out var mapDefinition))
            return;

        var markerSource = GetOrCreateMarkerSource(player.PlayerId);
        markerSource.SetPosition(player.Position, mapDefinition);
        latestMarkerPositions[player.PlayerId] = player.Position;
        latestMarkerMaps[player.PlayerId] = mapDefinition;

        if (registeredPlayerIds.Add(player.PlayerId))
            RegisterMarker(mapDirector, player.PlayerId, markerSource, forceNewId: true);
    }

    private MapNavigationMarkerData GetOrCreateMarkerSource(string playerId)
    {
        if (markerSources.TryGetValue(playerId, out var markerSource))
            return markerSource;

        markerSource = new MapNavigationMarkerData();
        markerSources[playerId] = markerSource;
        return markerSource;
    }

    [HideFromIl2Cpp]
    private void RemoveStaleMarkers(MapDirector mapDirector, HashSet<string> activePlayerIds)
    {
        foreach (var playerId in registeredPlayerIds.ToArray())
        {
            if (activePlayerIds.Contains(playerId))
                continue;

            DeregisterMarker(mapDirector, playerId);
            DeregisterCompassMarker(playerId);
            registeredPlayerIds.Remove(playerId);
            markerSources.Remove(playerId);
            latestMarkerPositions.Remove(playerId);
            latestMarkerMaps.Remove(playerId);
        }
    }

    private void RefreshMapUiIfNeeded(MapDirector mapDirector)
    {
        if (UnityEngine.Time.unscaledTime < nextMarkerUiRefreshTime)
            return;

        nextMarkerUiRefreshTime = UnityEngine.Time.unscaledTime + MarkerUiRefreshInterval;

        foreach (var playerId in registeredPlayerIds.ToArray())
        {
            if (!latestMarkerPositions.TryGetValue(playerId, out var position)
                || !latestMarkerMaps.TryGetValue(playerId, out var mapDefinition))
                continue;

            var markerSource = CreateMarkerSource(position, mapDefinition);
            markerSources[playerId] = markerSource;

            DeregisterMarker(mapDirector, playerId);
            RegisterMarker(mapDirector, playerId, markerSource, forceNewId: true);
        }
    }

    private void ClearAllMarkers()
    {
        ClearRegisteredMarkers();
        markerSources.Clear();
        latestMarkerPositions.Clear();
        latestMarkerMaps.Clear();
        ClearCompassMarkers();
        currentMapDirector = null;
    }

    private void ClearRegisteredMarkers()
    {
        var mapDirector = currentMapDirector;
        if (mapDirector != null && mapDirector)
        {
            foreach (var playerId in registeredPlayerIds.ToArray())
                DeregisterMarker(mapDirector, playerId);
        }

        registeredPlayerIds.Clear();
        registeredMapMarkerIds.Clear();
    }

    [HideFromIl2Cpp]
    private void RefreshCompassMarker(RemotePlayer player)
    {
        if (!TryResolveSceneGroup(player.SceneGroup, out var sceneGroup))
            return;

        var target = GetOrCreateRadarTarget(player.PlayerId);
        target.transform.position = player.Position;
        HideRadarTargetVisuals(target);

        if (radarTargetSceneGroups.TryGetValue(player.PlayerId, out var registeredSceneGroup)
            && registeredSceneGroup == player.SceneGroup)
            return;

        if (radarTargetSceneGroups.ContainsKey(player.PlayerId))
            RadarRegistry.UnregisterTrackedGameObjectAdvanced(target);

        var markerSource = GetOrCreateMarkerSource(player.PlayerId);
        var sprite = markerSource.GetMapMarkerDescriptor()?.MapIcon;

        RadarRegistry.RegisterTrackedGameObjectAdvanced(target, sceneGroup, sprite, false);
        radarTargetSceneGroups[player.PlayerId] = player.SceneGroup;
        HideRadarTargetVisuals(target);
    }

    private GameObject GetOrCreateRadarTarget(string playerId)
    {
        if (radarTargets.TryGetValue(playerId, out var target) && target)
            return target;

        target = new GameObject($"{MarkerPrefix}COMPASS_{playerId}");
        target.transform.localScale = Vector3.zero;
        Object.DontDestroyOnLoad(target);
        radarTargets[playerId] = target;
        return target;
    }

    private void DeregisterCompassMarker(string playerId)
    {
        if (radarTargets.TryGetValue(playerId, out var target) && target)
        {
            try
            {
                RadarRegistry.UnregisterTrackedGameObjectAdvanced(target);
            }
            catch (Exception ex)
            {
                SrLogger.LogDebug($"Could not unregister compass marker for {playerId}: {ex.Message}", SrLogTarget.Main);
            }

            Object.Destroy(target);
        }

        radarTargets.Remove(playerId);
        radarTargetSceneGroups.Remove(playerId);
    }

    private void ClearCompassMarkers()
    {
        foreach (var playerId in radarTargets.Keys.ToArray())
            DeregisterCompassMarker(playerId);
    }

    private void DeregisterMarker(MapDirector mapDirector, string playerId)
    {
        if (registeredMapMarkerIds.TryGetValue(playerId, out var markerId))
        {
            DeregisterMarkerById(mapDirector, markerId, playerId);
            registeredMapMarkerIds.Remove(playerId);
            return;
        }

        DeregisterMarkerById(mapDirector, LegacyMarkerId(playerId), playerId);
    }

    private static void DeregisterMarkerById(MapDirector mapDirector, string markerId, string playerId)
    {
        try
        {
            mapDirector.DeregisterMarker(markerId);
        }
        catch (Exception ex)
        {
            SrLogger.LogDebug($"Could not deregister map marker for {playerId}: {ex.Message}", SrLogTarget.Main);
        }
    }

    private void RegisterMarker(MapDirector mapDirector, string playerId, MapNavigationMarkerData markerSource, bool forceNewId = false)
    {
        if (forceNewId || !registeredMapMarkerIds.TryGetValue(playerId, out var markerId))
        {
            markerId = MarkerId(playerId, ++nextMarkerVersion);
            registeredMapMarkerIds[playerId] = markerId;
        }

        try
        {
            mapDirector.RegisterMarker(markerId, markerSource.Cast<IMapMarkerSource>());
        }
        catch (Exception ex)
        {
            SrLogger.LogDebug($"Could not register map marker for {playerId}: {ex.Message}", SrLogTarget.Main);
        }
    }

    private static MapNavigationMarkerData CreateMarkerSource(Vector3 position, MapDefinition mapDefinition)
    {
        var markerSource = new MapNavigationMarkerData();
        markerSource.SetPosition(position, mapDefinition);
        return markerSource;
    }

    private static void HideRadarTargetVisuals(GameObject target)
    {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;

        foreach (var canvas in target.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = false;

        foreach (var image in target.GetComponentsInChildren<Image>(true))
            image.enabled = false;

        target.transform.localScale = Vector3.zero;
    }

    private static bool TryGetMapDirector(out MapDirector mapDirector)
    {
        mapDirector = null!;

        if (!SceneContext.Instance)
            return false;

        mapDirector = SceneContext.Instance.MapDirector;
        return mapDirector;
    }

    private static bool TryResolveSceneGroup(int sceneGroupId, out SceneGroup sceneGroup)
    {
        if (NetworkSceneManager.TryGetSceneGroup(sceneGroupId, out var remoteSceneGroup)
            && remoteSceneGroup != null
            && remoteSceneGroup)
        {
            sceneGroup = remoteSceneGroup;
            return true;
        }

        if (SystemContext.Instance)
        {
            var currentSceneGroup = SystemContext.Instance.SceneLoader.CurrentSceneGroup;
            if (currentSceneGroup)
            {
                sceneGroup = currentSceneGroup;
                return true;
            }
        }

        sceneGroup = null!;
        return false;
    }

    private static bool TryGetMapForSceneGroup(MapDirector mapDirector, SceneGroup sceneGroup, out MapDefinition mapDefinition)
    {
        foreach (var mapping in mapDirector.ZoneMappings)
        {
            var map = mapping.Map;
            if (!map || map.RelatedScenes == null)
                continue;

            foreach (var relatedScene in map.RelatedScenes._gameplaySceneGroups)
            {
                if (relatedScene == sceneGroup)
                {
                    mapDefinition = map;
                    return true;
                }
            }
        }

        mapDefinition = mapDirector.DefaultMap;
        return mapDefinition;
    }

    private static string LegacyMarkerId(string playerId) => $"{MarkerPrefix}{playerId}";

    private static string MarkerId(string playerId, int version) => $"{MarkerPrefix}{playerId}_{version}";
}
