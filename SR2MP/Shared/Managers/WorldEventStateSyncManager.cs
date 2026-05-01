using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Event;
using Il2CppMonomiPark.SlimeRancher.UI.Map;
using Il2CppMonomiPark.SlimeRancher.World;
using Il2CppMonomiPark.World;
using SR2MP.Packets.Gordo;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Switch;
using SR2MP.Packets.World;

namespace SR2MP.Shared.Managers;

public static class WorldEventStateSyncManager
{
    public static bool ApplyLandPlotUpdate(LandPlotUpdatePacket packet, string source)
    {
        if (!TryGetGameModel(out var gameModel))
            return false;

        if (!gameModel.landPlots.TryGetValue(packet.ID, out var model) || model == null)
        {
            SrLogger.LogWarning($"Skipping land plot update from {source}; plot '{packet.ID}' was not found.", SrLogTarget.Main);
            return false;
        }

        return packet.IsUpgrade
            ? ApplyLandPlotUpgrade(model, packet.ID, packet.PlotUpgrade, source)
            : ApplyLandPlotType(model, packet.ID, packet.PlotType, source);
    }

    public static bool ApplySwitchState(WorldSwitchPacket packet, string source)
    {
        if (!TryGetGameModel(out var gameModel))
            return false;

        var isRepairSnapshot = IsRepairSource(source);
        var existed = gameModel.switches.TryGetValue(packet.ID, out var switchModel);
        var beforeState = existed ? switchModel.state : default;

        if (!existed)
        {
            switchModel = new WorldSwitchModel
            {
                gameObj = null,
                state = packet.State,
            };

            gameModel.switches.Add(packet.ID, switchModel);
            LogRepairCorrection(isRepairSnapshot, $"switch '{packet.ID}'", existed, beforeState, true, packet.State);
            return true;
        }

        if (isRepairSnapshot && beforeState == packet.State)
            return true;

        switchModel.state = packet.State;

        if (switchModel.gameObj)
        {
            var switchComponentBase = switchModel.gameObj.GetComponent<WorldSwitchModel.Participant>();
            if (switchComponentBase != null)
            {
                switchComponentBase.SetModel(switchModel);

                var primary = switchComponentBase.TryCast<WorldStatePrimarySwitch>();
                var secondary = switchComponentBase.TryCast<WorldStateSecondarySwitch>();
                var invisible = switchComponentBase.TryCast<WorldStateInvisibleSwitch>();

                handlingPacket = true;
                try
                {
                    primary?.SetStateForAll(packet.State, packet.Immediate);
                    secondary?.SetState(packet.State, packet.Immediate);
                    invisible?.SetStateForAll(packet.State, packet.Immediate);
                }
                finally { handlingPacket = false; }
            }
        }

        LogRepairCorrection(isRepairSnapshot, $"switch '{packet.ID}'", true, beforeState, true, packet.State);
        return true;
    }

    public static bool ApplyAccessDoorState(AccessDoorPacket packet, string source)
    {
        if (!TryGetGameModel(out var gameModel))
            return false;

        var isRepairSnapshot = IsRepairSource(source);
        var existed = gameModel.doors.TryGetValue(packet.ID, out var doorModel);
        var beforeState = existed ? doorModel.state : default;

        if (!existed)
        {
            doorModel = new AccessDoorModel
            {
                gameObj = null,
                state = packet.State,
            };

            gameModel.doors.Add(packet.ID, doorModel);
            LogRepairCorrection(isRepairSnapshot, $"access door '{packet.ID}'", existed, beforeState, true, packet.State);
            return true;
        }

        if (isRepairSnapshot && beforeState == packet.State)
            return true;

        doorModel.state = packet.State;

        if (doorModel.gameObj)
        {
            var door = doorModel.gameObj.GetComponent<AccessDoor>();
            if (door)
            {
                handlingPacket = true;
                try
                {
                    door.CurrState = packet.State;
                }
                finally { handlingPacket = false; }
            }
        }

        LogRepairCorrection(isRepairSnapshot, $"access door '{packet.ID}'", true, beforeState, true, packet.State);
        return true;
    }

    public static bool ApplyMapUnlock(MapUnlockPacket packet, string source)
        => MapUnlockSyncManager.ApplyUnlock(packet, source);

    public static bool ApplyGordoFeed(GordoFeedPacket packet, string source)
    {
        if (!TryGetGameModel(out var gameModel))
            return false;

        var isRepairSnapshot = IsRepairSource(source);
        var existed = gameModel.gordos.TryGetValue(packet.ID, out var gordo);
        var beforeCount = existed ? gordo.GordoEatenCount : -1;
        var beforeHash = existed ? HashGordo(gordo) : 0;

        if (!existed)
        {
            if (!TryCreateGordoModel(packet, out gordo))
                return false;

            gameModel.gordos.Add(packet.ID, gordo);
        }
        else if (!isRepairSnapshot && packet.NewFoodCount <= beforeCount)
        {
            SrLogger.LogDebug(
                $"Skipping stale gordo feed for '{packet.ID}' from {source}; local count is {beforeCount}, packet count is {packet.NewFoodCount}.",
                SrLogTarget.Main);
            return false;
        }

        gordo.GordoEatenCount = packet.NewFoodCount;
        gordo.targetCount = packet.RequiredFoodCount;

        if (packet.GordoType >= 0 && actorManager.ActorTypes.TryGetValue(packet.GordoType, out var type) && type)
            gordo.identifiableType = type;

        if (gordo.gameObj)
        {
            var gordoComponent = gordo.gameObj.GetComponent<GordoEat>();
            if (gordoComponent)
                gordoComponent.SetModel(gordo);

            gordo.gameObj.SetActive(!IsGordoPopped(gordo));
            PlayGordoFeedSound(gordo, beforeCount, isRepairSnapshot);
        }

        var afterHash = HashGordo(gordo);
        if (isRepairSnapshot && beforeHash != afterHash)
        {
            SrLogger.LogMessage(
                $"Repair corrected gordo '{packet.ID}' feed state ({FormatHash(beforeHash)} -> {FormatHash(afterHash)}).",
                SrLogTarget.Main);
        }

        return true;
    }

    public static bool ApplyGordoBurst(GordoBurstPacket packet, string source)
    {
        if (!TryGetGameModel(out var gameModel))
            return false;

        var isRepairSnapshot = IsRepairSource(source);
        var existed = gameModel.gordos.TryGetValue(packet.ID, out var gordo);
        var beforeHash = existed ? HashGordo(gordo) : 0;

        if (!existed)
        {
            gordo = new GordoModel
            {
                fashions = new CppCollections.List<IdentifiableType>(0),
                gordoEatCount = 999999,
                gordoSeen = false,
                gameObj = null,
                targetCount = 50,
            };

            gameModel.gordos.Add(packet.ID, gordo);
        }

        var alreadyPopped = IsGordoPopped(gordo);
        gordo.GordoEatenCount = gordo.targetCount + 1;

        var gameObj = gordo.gameObj;
        if (gameObj)
        {
            if (!isRepairSnapshot && !alreadyPopped)
            {
                handlingPacket = true;
                try
                {
                    gameObj!.GetComponent<GordoEat>()?.ImmediateReachedTarget();
                }
                catch (Exception ex)
                {
                    SrLogger.LogWarning(
                        $"Failed to play gordo burst rewards for '{packet.ID}' from {source}: {ex.Message}",
                        SrLogTarget.Both);
                }
                finally { handlingPacket = false; }
            }

            gameObj!.SetActive(false);
        }

        var afterHash = HashGordo(gordo);
        if (isRepairSnapshot && beforeHash != afterHash)
        {
            SrLogger.LogMessage(
                $"Repair corrected gordo '{packet.ID}' popped state ({FormatHash(beforeHash)} -> {FormatHash(afterHash)}).",
                SrLogTarget.Main);
        }

        return true;
    }

    private static bool ApplyLandPlotType(LandPlotModel model, string plotId, LandPlot.Id plotType, string source)
    {
        var isRepairSnapshot = IsRepairSource(source);
        var beforeType = model.typeId;
        if (isRepairSnapshot && beforeType == plotType)
            return true;

        model.typeId = plotType;

        if (model.gameObj)
        {
            var location = model.gameObj.GetComponent<LandPlotLocation>();
            var landPlotComponent = model.gameObj.GetComponentInChildren<LandPlot>();
            if (location && landPlotComponent && GameContext.Instance.LookupDirector._plotPrefabDict.TryGetValue(plotType, out var prefab))
            {
                handlingPacket = true;
                try
                {
                    location.Replace(landPlotComponent, prefab);
                }
                finally { handlingPacket = false; }
            }
        }

        LogRepairCorrection(isRepairSnapshot, $"land plot '{plotId}' type", true, beforeType, true, plotType);
        return true;
    }

    private static bool ApplyLandPlotUpgrade(LandPlotModel model, string plotId, LandPlot.Upgrade upgrade, string source)
    {
        var isRepairSnapshot = IsRepairSource(source);
        var alreadyHadUpgrade = HasUpgrade(model, upgrade);
        if (isRepairSnapshot && alreadyHadUpgrade)
            return true;

        if (!alreadyHadUpgrade)
            model.upgrades.Add(upgrade);

        if (model.gameObj)
        {
            var landPlotComponent = model.gameObj.GetComponentInChildren<LandPlot>();
            if (landPlotComponent)
            {
                handlingPacket = true;
                try
                {
                    landPlotComponent.AddUpgrade(upgrade);
                }
                finally { handlingPacket = false; }
            }
        }

        if (isRepairSnapshot && !alreadyHadUpgrade)
            SrLogger.LogMessage($"Repair corrected land plot '{plotId}' upgrade '{upgrade}'.", SrLogTarget.Main);

        return true;
    }

    private static bool HasUpgrade(LandPlotModel model, LandPlot.Upgrade upgrade)
    {
        if (model.upgrades == null || model.upgrades.Count <= 0)
            return false;

        foreach (var existing in model.upgrades)
        {
            if (existing == upgrade)
                return true;
        }

        return false;
    }

    private static bool TryCreateGordoModel(GordoFeedPacket packet, out GordoModel gordo)
    {
        gordo = null!;

        if (!actorManager.ActorTypes.TryGetValue(packet.GordoType, out var type) || !type)
        {
            SrLogger.LogWarning($"Skipping gordo feed from repair/client state; unknown gordo type {packet.GordoType}.", SrLogTarget.Main);
            return false;
        }

        gordo = new GordoModel
        {
            fashions = new CppCollections.List<IdentifiableType>(0),
            gordoEatCount = packet.NewFoodCount,
            gordoSeen = false,
            gameObj = null,
            targetCount = packet.RequiredFoodCount,
            identifiableType = type,
        };

        return true;
    }

    private static bool IsGordoPopped(GordoModel gordo)
        => gordo.GordoEatenCount > gordo.targetCount;

    private static void PlayGordoFeedSound(GordoModel gordo, int beforeCount, bool isRepairSnapshot)
    {
        if (isRepairSnapshot || gordo.GordoEatenCount <= beforeCount || !gordo.gameObj)
            return;

        if (!fxManager.WorldAudioCueMap.TryGetValue(WorldFXType.GordoFoodEatenSound, out var cue))
            return;

        var volume = WorldSoundVolumeDictionary.TryGetValue(WorldFXType.GordoFoodEatenSound, out var configuredVolume)
            ? configuredVolume
            : 1f;

        RemoteFXManager.PlayTransientAudio(cue, gordo.gameObj.transform.position, volume);
    }

    private static int HashGordo(GordoModel gordo)
    {
        var hash = 0;
        hash = AddHash(hash, gordo.GordoEatenCount);
        hash = AddHash(hash, gordo.targetCount);
        hash = AddHash(hash, gordo.identifiableType ? NetworkActorManager.GetPersistentID(gordo.identifiableType) : -1);
        return AddHash(hash, IsGordoPopped(gordo) ? 1 : 0);
    }

    private static void LogRepairCorrection<T>(
        bool isRepairSnapshot,
        string label,
        bool beforeExists,
        T beforeValue,
        bool targetExists,
        T targetValue)
    {
        if (!isRepairSnapshot)
            return;

        if (beforeExists == targetExists && EqualityComparer<T>.Default.Equals(beforeValue, targetValue))
            return;

        SrLogger.LogMessage(
            $"Repair corrected {label} ({beforeValue} -> {targetValue}).",
            SrLogTarget.Main);
    }

    private static bool TryGetGameModel(out GameModel gameModel)
    {
        gameModel = null!;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        gameModel = SceneContext.Instance.GameModel;
        return true;
    }

    private static bool IsRepairSource(string source)
        => source.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static int AddHash(int hash, int value)
    {
        unchecked
        {
            return (hash * 397) ^ value;
        }
    }

    private static string FormatHash(int hash)
        => $"0x{hash:X8}";
}
