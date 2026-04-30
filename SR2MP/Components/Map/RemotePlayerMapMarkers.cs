using Il2CppMonomiPark.SlimeRancher.Map;
using Il2CppMonomiPark.SlimeRancher.SceneManagement;
using Il2CppMonomiPark.SlimeRancher.UI;
using Il2CppInterop.Runtime.Attributes;
using MelonLoader;
using SR2MP.Client.Models;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using UnityEngine.UI;

namespace SR2MP.Components.Map;

[RegisterTypeInIl2Cpp(false)]
public sealed class RemotePlayerMapMarkers : MonoBehaviour
{
    private const string MarkerPrefix = "SR2MP_REMOTE_PLAYER_";
    private const float MarkerPositionEpsilonSqr = 0.25f;
    private const int FallbackSpriteSize = 32;

    private readonly Dictionary<string, MapNavigationMarkerData> markerSources = new();
    private readonly Dictionary<string, string> registeredMapMarkerIds = new();
    private readonly Dictionary<string, Vector3> latestMarkerPositions = new();
    private readonly Dictionary<string, MapDefinition> latestMarkerMaps = new();
    private readonly Dictionary<string, GameObject> radarTargets = new();
    private readonly Dictionary<string, Vector3> latestCompassPositions = new();
    private readonly Dictionary<string, int> radarTargetSceneGroups = new();
    private readonly HashSet<string> registeredPlayerIds = new();
    private readonly HashSet<string> activePlayerIds = new();
    private readonly HashSet<string> hiddenRadarTargets = new();

    private MapDirector? currentMapDirector;
    private static Sprite? fallbackRemotePlayerSprite;

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
        }

        activePlayerIds.Clear();

        var players = playerManager.GetAllPlayers();
        PerformanceDiagnostics.RecordMapMarkerUpdate(players.Count);

        foreach (var player in players)
        {
            if (player.PlayerId == LocalID)
                continue;

            activePlayerIds.Add(player.PlayerId);
            RefreshMarker(mapDirector, player);
            RefreshCompassMarker(player);
        }

        RemoveStaleMarkers(mapDirector, activePlayerIds);
    }

    [HideFromIl2Cpp]
    private void RefreshMarker(MapDirector mapDirector, RemotePlayer player)
    {
        if (!TryResolveSceneGroup(player.SceneGroup, out var sceneGroup))
            return;

        if (!TryGetMapForSceneGroup(mapDirector, sceneGroup, out var mapDefinition))
            return;

        var playerId = player.PlayerId;
        var markerSource = GetOrCreateMarkerSource(playerId);
        var positionChanged = !latestMarkerPositions.TryGetValue(playerId, out var lastPosition)
                              || (lastPosition - player.Position).sqrMagnitude >= MarkerPositionEpsilonSqr;
        var mapChanged = !latestMarkerMaps.TryGetValue(playerId, out var lastMap) || lastMap != mapDefinition;

        if (positionChanged || mapChanged)
        {
            markerSource.SetPosition(player.Position, mapDefinition);
            latestMarkerPositions[playerId] = player.Position;
            latestMarkerMaps[playerId] = mapDefinition;
        }

        if (registeredPlayerIds.Add(playerId))
            RegisterMarker(mapDirector, playerId, markerSource);
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
        if (!latestCompassPositions.TryGetValue(player.PlayerId, out var lastPosition)
            || (lastPosition - player.Position).sqrMagnitude >= MarkerPositionEpsilonSqr)
        {
            target.transform.position = player.Position;
            latestCompassPositions[player.PlayerId] = player.Position;
        }

        if (radarTargetSceneGroups.TryGetValue(player.PlayerId, out var registeredSceneGroup)
            && registeredSceneGroup == player.SceneGroup)
            return;

        if (radarTargetSceneGroups.ContainsKey(player.PlayerId))
            RadarRegistry.UnregisterTrackedGameObjectAdvanced(target);

        var markerSource = GetOrCreateMarkerSource(player.PlayerId);
        var sprite = GetMarkerSprite(markerSource);

        RadarRegistry.RegisterTrackedGameObjectAdvanced(target, sceneGroup, sprite, false);
        radarTargetSceneGroups[player.PlayerId] = player.SceneGroup;
        HideRadarTargetVisualsIfNeeded(player.PlayerId, target, force: true);
    }

    private GameObject GetOrCreateRadarTarget(string playerId)
    {
        if (radarTargets.TryGetValue(playerId, out var target) && target)
            return target;

        target = new GameObject($"{MarkerPrefix}COMPASS_{playerId}");
        target.transform.localScale = Vector3.zero;
        Object.DontDestroyOnLoad(target);
        radarTargets[playerId] = target;
        HideRadarTargetVisualsIfNeeded(playerId, target);
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
        latestCompassPositions.Remove(playerId);
        radarTargetSceneGroups.Remove(playerId);
        hiddenRadarTargets.Remove(playerId);
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

    private void RegisterMarker(MapDirector mapDirector, string playerId, MapNavigationMarkerData markerSource)
    {
        if (!registeredMapMarkerIds.TryGetValue(playerId, out var markerId))
            registeredMapMarkerIds[playerId] = markerId = MarkerId(playerId);

        try
        {
            mapDirector.RegisterMarker(markerId, markerSource.Cast<IMapMarkerSource>());
        }
        catch (Exception ex)
        {
            SrLogger.LogDebug($"Could not register map marker for {playerId}: {ex.Message}", SrLogTarget.Main);
        }
    }

    private static Sprite GetMarkerSprite(MapNavigationMarkerData markerSource)
    {
        var descriptor = markerSource.GetMapMarkerDescriptor();
        if (descriptor != null && descriptor.MapIcon)
            return descriptor.MapIcon;

        return GetOrCreateFallbackRemotePlayerSprite();
    }

    private static Sprite GetOrCreateFallbackRemotePlayerSprite()
    {
        if (fallbackRemotePlayerSprite)
            return fallbackRemotePlayerSprite!;

        var texture = new Texture2D(FallbackSpriteSize, FallbackSpriteSize, TextureFormat.RGBA32, false)
        {
            name = "SR2MP_RemotePlayerMarkerTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var center = (FallbackSpriteSize - 1) * 0.5f;
        var outerRadius = FallbackSpriteSize * 0.42f;
        var innerRadius = FallbackSpriteSize * 0.24f;
        var outerRadiusSqr = outerRadius * outerRadius;
        var innerRadiusSqr = innerRadius * innerRadius;
        var outlineColor = new Color(1f, 1f, 1f, 0.95f);
        var fillColor = new Color(0.15f, 0.75f, 1f, 0.95f);
        var clear = new Color(1f, 1f, 1f, 0f);

        for (var y = 0; y < FallbackSpriteSize; y++)
        {
            for (var x = 0; x < FallbackSpriteSize; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distanceSqr = dx * dx + dy * dy;

                texture.SetPixel(
                    x,
                    y,
                    distanceSqr <= innerRadiusSqr
                        ? fillColor
                        : distanceSqr <= outerRadiusSqr
                            ? outlineColor
                            : clear);
            }
        }

        texture.Apply(false, true);

        fallbackRemotePlayerSprite = Sprite.Create(
            texture,
            new Rect(0, 0, FallbackSpriteSize, FallbackSpriteSize),
            new Vector2(0.5f, 0.5f),
            FallbackSpriteSize);

        fallbackRemotePlayerSprite.name = "SR2MP_RemotePlayerMarkerSprite";
        fallbackRemotePlayerSprite.hideFlags = HideFlags.HideAndDontSave;
        return fallbackRemotePlayerSprite;
    }

    private void HideRadarTargetVisualsIfNeeded(string playerId, GameObject target, bool force = false)
    {
        if (!force && hiddenRadarTargets.Contains(playerId))
            return;

        HideRadarTargetVisuals(target);
        hiddenRadarTargets.Add(playerId);
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

    private static string MarkerId(string playerId) => $"{MarkerPrefix}{playerId}";
}
