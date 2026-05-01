using SR2MP.Packets.Landplot;
using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Shared.Sync;

/// <summary>
/// <see cref="ISyncedSubsystem"/> for land-plot state:
/// plot type, upgrades, silo ammo, feeder state, garden crop, and garden growth.
///
/// Replaces:
/// - <c>SendPlotsPacket</c> (ConnectHandler Initial-Sync)
/// - <c>SendLandPlotSnapshots</c> (WorldStateRepairManager Repair)
///
/// Live events (<c>LandPlotUpdatePacket</c>, <c>LandPlotAmmoPacket</c>,
/// <c>LandPlotFeederPacket</c>, <c>GardenPlantPacket</c>, <c>GardenGrowthPacket</c>)
/// still flow through the existing handlers — unchanged.
/// </summary>
public sealed class LandPlotsSubsystem : ISyncedSubsystem
{
    public static readonly LandPlotsSubsystem Instance = new();

    private LandPlotsSubsystem() { }

    public byte Id => SubsystemIds.LandPlots;
    public string Name => "LandPlots";

    /// <summary>Serialises all land plot states including gardens and growth.</summary>
    public void CaptureSnapshot(PacketWriter writer)
    {
        var plots = new List<InitialLandPlotsPacket.BasePlot>();

        if (SceneContext.Instance && SceneContext.Instance.GameModel)
        {
            foreach (var plotEntry in SceneContext.Instance.GameModel.landPlots)
            {
                var plot = plotEntry.Value;
                var id = plotEntry.Key;

                INetObject? data = plot.typeId switch
                {
                    LandPlot.Id.GARDEN => new InitialLandPlotsPacket.GardenData
                    {
                        HasCrop = GardenPlotSyncManager.TryGetCurrentCropType(plot, out var crop),
                        Crop = crop,
                    },
                    LandPlot.Id.SILO => new InitialLandPlotsPacket.SiloData(),
                    _ => null,
                };

                plots.Add(new InitialLandPlotsPacket.BasePlot
                {
                    ID = id,
                    Type = plot.typeId,
                    Upgrades = plot.upgrades,
                    AmmoSets = LandPlotAmmoSyncManager.CreateAmmoSets(plot),
                    FeederState = LandPlotFeederSyncManager.CreateState(plot),
                    Data = data,
                });
            }
        }

        writer.WriteList(plots, PacketWriterDels.NetObject<InitialLandPlotsPacket.BasePlot>.Func);

        // Garden growth per garden plot
        var growthPackets = new List<GardenGrowthPacket>();
        if (SceneContext.Instance && SceneContext.Instance.GameModel)
        {
            foreach (var plotEntry in SceneContext.Instance.GameModel.landPlots)
            {
                if (GardenGrowthSyncManager.TryCreateSnapshot(plotEntry.Value, plotEntry.Key, out var growthPacket))
                    growthPackets.Add(growthPacket);
            }
        }

        writer.WriteInt(growthPackets.Count);
        foreach (var gp in growthPackets)
            gp.Serialise(writer);
    }

    /// <summary>
    /// Deserialises and applies all land-plot states and garden growth states.
    /// Mirrors <c>PlotsLoadHandler</c> logic.
    /// </summary>
    public void ApplySnapshot(PacketReader reader, SyncSource source)
    {
        var plots = reader.ReadList(PacketReaderDels.NetObject<InitialLandPlotsPacket.BasePlot>.Func);
        var sourceStr = source.ToSourceString();

        foreach (var plot in plots)
        {
            if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
                continue;

            if (!SceneContext.Instance.GameModel.landPlots.TryGetValue(plot.ID, out var model) || model == null)
                continue;

            if (model.gameObj)
            {
                RunWithHandlingPacket(() =>
                {
                    var location = model.gameObj.GetComponent<LandPlotLocation>();
                    var landPlotComponent = model.gameObj.GetComponentInChildren<LandPlot>();
                    if (location && landPlotComponent && GameContext.Instance.LookupDirector._plotPrefabDict.TryGetValue(plot.Type, out var prefab))
                        location.Replace(landPlotComponent, prefab);

                    var landPlotComponent2 = model.gameObj.GetComponentInChildren<LandPlot>();
                    if (landPlotComponent2 && plot.Upgrades != null)
                        landPlotComponent2.ApplyUpgrades(plot.Upgrades.Cast<CppCollections.IEnumerable<LandPlot.Upgrade>>(), false);
                });
            }

            model.typeId = plot.Type;
            model.upgrades = plot.Upgrades;

            switch (plot.Data)
            {
                case InitialLandPlotsPacket.GardenData garden:
                    RunWithHandlingPacket(() =>
                        GardenPlotSyncManager.ApplyRemoteState(plot.ID, garden.HasCrop, garden.Crop, $"{sourceStr} garden plot"));
                    break;
            }

            RunWithHandlingPacket(() => LandPlotAmmoSyncManager.ApplyAmmoSets(model, plot.AmmoSets, plot.ID));
            RunWithHandlingPacket(() => LandPlotFeederSyncManager.ApplyState(plot.ID, plot.FeederState, $"{sourceStr} feeder state"));
        }

        // Garden growth
        var growthCount = reader.ReadInt();
        for (var i = 0; i < growthCount; i++)
        {
            var growthPacket = new GardenGrowthPacket();
            growthPacket.Deserialise(reader);
            growthPacket.IsRepairSnapshot = source == SyncSource.Repair;
            RunWithHandlingPacket(() => GardenGrowthSyncManager.ApplyState(growthPacket, $"{sourceStr} garden growth"));
        }
    }
}
