using SR2MP.Packets.Loading;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.InitialPlots)]
public sealed class PlotsLoadHandler : BaseClientPacketHandler<InitialLandPlotsPacket>
{
    public PlotsLoadHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(InitialLandPlotsPacket packet)
    {
        foreach (var plot in packet.Plots)
        {
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
                        GardenPlotSyncManager.ApplyRemoteState(plot.ID, garden.HasCrop, garden.Crop, "initial garden plot"));
                    break;
                case InitialLandPlotsPacket.SiloData silo: break; // todo
            }

            RunWithHandlingPacket(() => LandPlotAmmoSyncManager.ApplyAmmoSets(model, plot.AmmoSets, plot.ID));

            RunWithHandlingPacket(() => LandPlotFeederSyncManager.ApplyState(plot.ID, plot.FeederState, "initial feeder state"));
        }
    }
}
