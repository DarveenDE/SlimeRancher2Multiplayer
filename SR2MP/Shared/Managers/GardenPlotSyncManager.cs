using System.Collections;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;
using SR2MP.Packets.Landplot;

namespace SR2MP.Shared.Managers;

public static class GardenPlotSyncManager
{
    private static readonly HashSet<string> PendingPlotIds = new();

    public static void QueueLocalState(LandPlotLocation? location)
    {
        if (handlingPacket)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        if (SystemContext.Instance && SystemContext.Instance.SceneLoader.IsSceneLoadInProgress)
            return;

        if (location == null || !location)
            return;

        var plotId = location._id;
        if (string.IsNullOrWhiteSpace(plotId))
            return;

        if (!PendingPlotIds.Add(plotId))
            return;

        MelonCoroutines.Start(SendLocalStateNextFrame(plotId));
    }

    public static bool TryGetCurrentCropType(LandPlotModel model, out int cropType)
    {
        cropType = -1;

        var attachedCrop = TryGetAttachedCropType(model);
        if (attachedCrop != null && attachedCrop)
        {
            try
            {
                cropType = NetworkActorManager.GetPersistentID(attachedCrop);
                return true;
            }
            catch (Exception ex)
            {
                SrLogger.LogWarning($"Could not resolve attached garden crop type for sync: {ex.Message}", SrLogTarget.Main);
            }
        }

        var resourceGrower = model.resourceGrowerDefinition;
        if (resourceGrower == null)
            return false;

        var primaryResourceType = resourceGrower._primaryResourceType;
        if (!primaryResourceType)
            return false;

        try
        {
            cropType = NetworkActorManager.GetPersistentID(primaryResourceType);
            return true;
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not resolve garden crop type for sync: {ex.Message}", SrLogTarget.Main);
            return false;
        }
    }

    private static IdentifiableType? TryGetAttachedCropType(LandPlotModel model)
    {
        if (!model.gameObj)
            return null;

        var landPlot = model.gameObj.GetComponentInChildren<LandPlot>();
        if (!landPlot || !landPlot.HasAttached())
            return null;

        var crop = landPlot.GetAttachedCropId();
        return crop ? crop : null;
    }

    public static bool ApplyRemoteState(string plotId, bool hasCrop, int cropType, string source)
    {
        if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(plotId, out var model))
        {
            SrLogger.LogWarning($"Ignoring garden state for unknown plot '{plotId}' from {source}.", SrLogTarget.Main);
            return false;
        }

        if (model.typeId != LandPlot.Id.GARDEN)
        {
            SrLogger.LogDebug($"Ignoring garden state for non-garden plot '{plotId}' from {source}.", SrLogTarget.Main);
            return false;
        }

        if (!hasCrop)
        {
            model.resourceGrowerDefinition = null;
            ClearAttachedCrop(model);
            model.NotifyParticipants();
            return true;
        }

        if (!TryGetCropDefinition(cropType, out var actor, out var resourceGrowerDefinition))
        {
            SrLogger.LogWarning($"Ignoring garden state for plot '{plotId}' with unknown crop type {cropType} from {source}.", SrLogTarget.Main);
            return false;
        }

        return PlantCrop(model, actor, resourceGrowerDefinition, plotId, source);
    }

    private static IEnumerator SendLocalStateNextFrame(string plotId)
    {
        yield return null;

        PendingPlotIds.Remove(plotId);

        if (handlingPacket)
            yield break;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            yield break;

        if (!SceneContext.Instance)
            yield break;

        if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(plotId, out var model))
            yield break;

        if (model.typeId != LandPlot.Id.GARDEN)
            yield break;

        var packet = new GardenPlantPacket
        {
            ID = plotId,
            HasCrop = TryGetCurrentCropType(model, out var cropType),
            ActorType = cropType
        };

        Main.SendToAllOrServer(packet);
    }

    private static bool TryGetCropDefinition(
        int cropType,
        out IdentifiableType actor,
        out ResourceGrowerDefinition resourceGrowerDefinition)
    {
        actor = null!;
        resourceGrowerDefinition = null!;

        if (!actorManager.ActorTypes.TryGetValue(cropType, out var foundActor) || !foundActor)
            return false;

        actor = foundActor;

        foreach (var entry in GameContext.Instance.AutoSaveDirector._saveReferenceTranslation
                     ._resourceGrowerTranslation.RawLookupDictionary._entries)
        {
            var candidate = entry.value;
            if (candidate != null && candidate._primaryResourceType == actor)
            {
                resourceGrowerDefinition = candidate;
                return true;
            }
        }

        return false;
    }

    private static void ClearAttachedCrop(LandPlotModel model)
    {
        if (!model.gameObj)
            return;

        var landPlot = model.gameObj.GetComponentInChildren<LandPlot>();
        if (landPlot && landPlot.HasAttached())
            landPlot.DestroyAttached();
    }

    private static bool PlantCrop(
        LandPlotModel model,
        IdentifiableType actor,
        ResourceGrowerDefinition resourceGrowerDefinition,
        string plotId,
        string source)
    {
        if (!model.gameObj)
            return false;

        var landPlot = model.gameObj.GetComponentInChildren<LandPlot>();
        if (landPlot && landPlot.HasAttached() && landPlot.GetAttachedCropId() == actor)
        {
            model.resourceGrowerDefinition = resourceGrowerDefinition;
            model.NotifyParticipants();
            return true;
        }

        var garden = model.gameObj.GetComponentInChildren<GardenCatcher>();
        if (!garden)
        {
            SrLogger.LogWarning($"Could not find GardenCatcher for plot '{plotId}' while applying {source}.", SrLogTarget.Main);
            return false;
        }

        if (!landPlot || !landPlot.HasAttached())
            model.resourceGrowerDefinition = null;

        if (!garden.CanAccept(actor))
        {
            SrLogger.LogWarning($"Garden plot '{plotId}' cannot accept crop '{actor.name}' while applying {source}.", SrLogTarget.Main);
            return false;
        }

        var planted = garden.Plant(actor, true);
        if (!planted)
        {
            SrLogger.LogWarning($"Garden plot '{plotId}' did not spawn crop '{actor.name}' while applying {source}.", SrLogTarget.Main);
            return false;
        }

        model.resourceGrowerDefinition = resourceGrowerDefinition;
        model.NotifyParticipants();
        return true;
    }
}
