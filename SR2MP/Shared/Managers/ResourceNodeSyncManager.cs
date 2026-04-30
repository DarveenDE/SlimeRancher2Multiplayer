using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.World;

namespace SR2MP.Shared.Managers;

public static class ResourceNodeSyncManager
{
    public static List<ResourceNodeStatePacket.NodeStateData> CreateSnapshot()
    {
        var nodes = new List<ResourceNodeStatePacket.NodeStateData>();

        if (!TryGetGameModel(out var gameModel))
            return nodes;

        foreach (var entry in gameModel.resourceNodeSpawnerModels)
        {
            var model = entry.Value;
            if (TryCreateState(model, out var state))
                nodes.Add(state);
        }

        return nodes;
    }

    public static void SendSnapshot(ResourceNodeSpawnerModel model)
    {
        if (handlingPacket || model == null)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        if (SystemContext.Instance.SceneLoader.IsSceneLoadInProgress)
            return;

        if (!TryCreateState(model, out var state))
            return;

        Main.SendToAllOrServer(new ResourceNodeStatePacket
        {
            Nodes = new List<ResourceNodeStatePacket.NodeStateData> { state }
        });
    }

    public static int Apply(ResourceNodeStatePacket packet, string source)
    {
        var applied = 0;
        foreach (var node in packet.Nodes)
        {
            if (Apply(node, source))
                applied++;
        }

        return applied;
    }

    private static bool TryCreateState(ResourceNodeSpawnerModel model, out ResourceNodeStatePacket.NodeStateData state)
    {
        state = default;

        if (model == null || string.IsNullOrWhiteSpace(model.nodeId))
            return false;

        state = new ResourceNodeStatePacket.NodeStateData
        {
            NodeId = model.nodeId,
            DefinitionIndex = GetDefinitionIndex(model),
            VariantIndex = model.resourceNodeVariantIndex,
            State = model.nodeState,
            DespawnAtWorldTime = model.despawnAtWorldTime,
            ResourceTypeIds = GetResourceTypeIds(model),
        };

        return true;
    }

    private static bool Apply(ResourceNodeStatePacket.NodeStateData state, string source)
    {
        if (string.IsNullOrWhiteSpace(state.NodeId))
            return false;

        if (!TryGetModel(state.NodeId, out var model))
            return false;

        var previousDefinition = model.resourceNodeDefinition;
        var definition = ResolveDefinition(model, state.DefinitionIndex);
        var spawner = FindSpawner(state.NodeId);
        var shouldShowNode = state.State == ResourceNode.NodeState.READY;

        RunWithHandlingPacket(() =>
        {
            model.resourceNodeDefinition = definition;
            model.resourceNodeVariantIndex = state.VariantIndex;
            model.resourcesToSpawn = CreateResourcesToSpawn(state.ResourceTypeIds);
            model.despawnAtWorldTime = state.DespawnAtWorldTime;

            if (spawner != null && spawner)
            {
                var hasDifferentNode = previousDefinition != definition;
                if (spawner.HasAttachedNode && (!shouldShowNode || hasDifferentNode))
                    spawner.DespawnNode();

                if (shouldShowNode && definition != null && !spawner.HasAttachedNode)
                    spawner.SpawnNode(definition);
            }

            model.nodeState = state.State;
        });

        if (source.Contains("repair", StringComparison.OrdinalIgnoreCase))
            SrLogger.LogDebug($"Repair applied resource node '{state.NodeId}' ({state.State}).", SrLogTarget.Main);

        return true;
    }

    private static List<int> GetResourceTypeIds(ResourceNodeSpawnerModel model)
    {
        var resources = new List<int>();
        if (model.resourcesToSpawn == null)
            return resources;

        foreach (var ident in model.resourcesToSpawn)
        {
            if (!ident)
                continue;

            resources.Add(NetworkActorManager.GetPersistentID(ident));
        }

        return resources;
    }

    private static CppCollections.List<IdentifiableType> CreateResourcesToSpawn(List<int> resourceTypeIds)
    {
        var resources = new CppCollections.List<IdentifiableType>();
        if (resourceTypeIds == null)
            return resources;

        foreach (var typeId in resourceTypeIds)
        {
            if (actorManager.ActorTypes.TryGetValue(typeId, out var ident) && ident)
                resources.Add(ident);
        }

        return resources;
    }

    private static int GetDefinitionIndex(ResourceNodeSpawnerModel model)
    {
        if (model.resourceNodeDefinition == null || model.resourceNodeDefinitions == null)
            return -1;

        var definitions = GetDefinitions(model);
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (definition == model.resourceNodeDefinition)
                return index;
        }

        return -1;
    }

    private static ResourceNodeDefinition? ResolveDefinition(ResourceNodeSpawnerModel model, int definitionIndex)
    {
        if (definitionIndex < 0 || model.resourceNodeDefinitions == null)
            return model.resourceNodeDefinition;

        var definitions = GetDefinitions(model);
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (index == definitionIndex)
                return definition;
        }

        return model.resourceNodeDefinition;
    }

    private static List<ResourceNodeDefinition> GetDefinitions(ResourceNodeSpawnerModel model)
    {
        if (model.resourceNodeDefinitions == null)
            return new List<ResourceNodeDefinition>();

        try
        {
            return Il2CppSystem.Linq.Enumerable
                .ToArray(model.resourceNodeDefinitions.Cast<CppCollections.IEnumerable<ResourceNodeDefinition>>())
                .Where(definition => definition != null)
                .ToList();
        }
        catch (Exception ex)
        {
            SrLogger.LogDebug($"Could not enumerate resource node definitions for {model.nodeId}: {ex.Message}", SrLogTarget.Main);
            return new List<ResourceNodeDefinition>();
        }
    }

    private static bool TryGetModel(string nodeId, out ResourceNodeSpawnerModel model)
    {
        model = null!;

        if (!TryGetGameModel(out var gameModel))
            return false;

        if (gameModel.resourceNodeSpawnerModels.TryGetValue(nodeId, out model) && model != null)
            return true;

        var spawner = FindSpawner(nodeId);
        if (spawner != null && spawner && spawner._model != null)
        {
            model = spawner._model;
            gameModel.resourceNodeSpawnerModels[nodeId] = model;
            return true;
        }

        model = gameModel.InitializeResourceNodeSpawnerModel(nodeId);
        return model != null;
    }

    private static ResourceNodeSpawner? FindSpawner(string nodeId)
    {
        foreach (var spawner in Resources.FindObjectsOfTypeAll<ResourceNodeSpawner>())
        {
            if (!spawner || spawner._model == null)
                continue;

            if (string.Equals(spawner._model.nodeId, nodeId, StringComparison.Ordinal))
                return spawner;
        }

        return null;
    }

    private static bool TryGetGameModel(out GameModel gameModel)
    {
        gameModel = null!;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        gameModel = SceneContext.Instance.GameModel;
        return true;
    }
}
