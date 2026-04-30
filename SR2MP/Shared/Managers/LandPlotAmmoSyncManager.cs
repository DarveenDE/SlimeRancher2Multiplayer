using Il2CppMonomiPark.SlimeRancher.DataModel;
using MelonLoader;
using SR2MP.Packets.Landplot;
using SR2MP.Packets.Loading;

namespace SR2MP.Shared.Managers;

public static class LandPlotAmmoSyncManager
{
    private static readonly Dictionary<IntPtr, AmmoModel> PendingLocalAmmoModels = new();
    private static bool localAmmoSendRunning;

    public static List<InitialLandPlotsPacket.AmmoSetData> CreateAmmoSets(LandPlotModel model)
    {
        var result = new List<InitialLandPlotsPacket.AmmoSetData>();

        if (model.siloAmmo == null)
            return result;

        foreach (var ammoSet in model.siloAmmo)
        {
            var ammoModel = ammoSet.value;
            if (ammoModel == null || ammoModel.Slots == null)
                continue;

            result.Add(CreateAmmoSet(ammoSet.key, ammoModel));
        }

        return result;
    }

    public static bool TryCreateAmmoSet(AmmoModel ammoModel, out string plotId, out InitialLandPlotsPacket.AmmoSetData ammoSetData)
    {
        plotId = string.Empty;
        ammoSetData = null!;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel || ammoModel == null)
            return false;

        foreach (var plot in SceneContext.Instance.GameModel.landPlots)
        {
            var model = plot.value;
            if (model == null || model.siloAmmo == null)
                continue;

            foreach (var ammoSet in model.siloAmmo)
            {
                if (ammoSet.value == null)
                    continue;

                if (!ReferenceEquals(ammoSet.value, ammoModel) && ammoSet.value.Pointer != ammoModel.Pointer)
                    continue;

                plotId = plot.key;
                ammoSetData = CreateAmmoSet(ammoSet.key, ammoModel);
                return true;
            }
        }

        return false;
    }

    public static void QueueLocalAmmoSet(AmmoModel ammoModel)
    {
        if (ammoModel is null || ammoModel.Pointer == IntPtr.Zero)
            return;

        PendingLocalAmmoModels[ammoModel.Pointer] = ammoModel;

        if (localAmmoSendRunning)
            return;

        localAmmoSendRunning = true;
        MelonCoroutines.Start(SendQueuedLocalAmmoSets());
    }

    public static void ApplyAmmoSets(LandPlotModel model, List<InitialLandPlotsPacket.AmmoSetData>? ammoSets, string plotId)
    {
        if (ammoSets == null || ammoSets.Count == 0 || model.siloAmmo == null)
            return;

        foreach (var ammoSet in ammoSets)
        {
            if (!model.siloAmmo.TryGetValue(ammoSet.Key, out var ammoModel) || ammoModel == null)
            {
                SrLogger.LogDebug($"Skipping unknown ammo set '{ammoSet.Key}' for plot '{plotId}'.", SrLogTarget.Main);
                continue;
            }

            ApplySlots(ammoModel, ammoSet, plotId);
        }
    }

    public static bool ApplyAmmoSet(string plotId, InitialLandPlotsPacket.AmmoSetData? ammoSet, string source)
    {
        if (string.IsNullOrEmpty(plotId) || ammoSet == null)
            return false;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;

        if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(plotId, out var model) || model == null)
        {
            SrLogger.LogWarning($"Skipping land plot ammo update from {source}; plot '{plotId}' was not found.", SrLogTarget.Main);
            return false;
        }

        if (model.siloAmmo == null || !model.siloAmmo.TryGetValue(ammoSet.Key, out var ammoModel) || ammoModel == null)
        {
            SrLogger.LogWarning($"Skipping land plot ammo update from {source}; ammo set '{ammoSet.Key}' on plot '{plotId}' was not found.", SrLogTarget.Main);
            return false;
        }

        ApplySlots(ammoModel, ammoSet, plotId);
        return true;
    }

    private static InitialLandPlotsPacket.AmmoSetData CreateAmmoSet(string key, AmmoModel ammoModel)
    {
        var slots = new List<InitialLandPlotsPacket.AmmoSlotData>();
        foreach (var slot in ammoModel.Slots)
        {
            if (slot == null)
            {
                slots.Add(new InitialLandPlotsPacket.AmmoSlotData());
                continue;
            }

            var id = slot.Id;
            var hasIdentifiable = id != null && id && slot.Count > 0;

            slots.Add(new InitialLandPlotsPacket.AmmoSlotData
            {
                HasIdentifiable = hasIdentifiable,
                IdentifiableType = hasIdentifiable ? NetworkActorManager.GetPersistentID(id!) : -1,
                Count = hasIdentifiable ? slot.Count : 0,
                Radiant = hasIdentifiable && slot.Radiant,
            });
        }

        return new InitialLandPlotsPacket.AmmoSetData
        {
            Key = key,
            Slots = slots,
        };
    }

    private static void ApplySlots(AmmoModel ammoModel, InitialLandPlotsPacket.AmmoSetData ammoSet, string plotId)
    {
        var slots = ammoModel.Slots;
        if (slots == null)
            return;

        var count = Math.Min(slots.Length, ammoSet.Slots.Count);
        for (var i = 0; i < count; i++)
        {
            var targetSlot = slots[i];
            if (targetSlot == null)
                continue;

            var sourceSlot = ammoSet.Slots[i];
            if (!sourceSlot.HasIdentifiable || sourceSlot.Count <= 0)
            {
                targetSlot.Clear();
                continue;
            }

            if (!actorManager.ActorTypes.TryGetValue(sourceSlot.IdentifiableType, out var ident) || !ident)
            {
                SrLogger.LogWarning(
                    $"Skipping unknown stored item type {sourceSlot.IdentifiableType} for ammo set '{ammoSet.Key}' on plot '{plotId}'.",
                    SrLogTarget.Main);
                continue;
            }

            targetSlot.Id = ident;
            targetSlot.Count = sourceSlot.Count;
            targetSlot.Radiant = sourceSlot.Radiant;
        }

        ammoModel.Push(slots);
    }

    private static System.Collections.IEnumerator SendQueuedLocalAmmoSets()
    {
        yield return null;

        while (PendingLocalAmmoModels.Count > 0)
        {
            var pending = PendingLocalAmmoModels.Values.ToList();
            PendingLocalAmmoModels.Clear();

            if (Main.Server.IsRunning() || Main.Client.IsConnected)
            {
                foreach (var ammoModel in pending)
                {
                    if (!TryCreateAmmoSet(ammoModel, out var plotId, out var ammoSet))
                        continue;

                    Main.SendToAllOrServer(new LandPlotAmmoPacket
                    {
                        PlotId = plotId,
                        AmmoSet = ammoSet,
                    });
                }
            }

            yield return null;
        }

        localAmmoSendRunning = false;
    }
}
