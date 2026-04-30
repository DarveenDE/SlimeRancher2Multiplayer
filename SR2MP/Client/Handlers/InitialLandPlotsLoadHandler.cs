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
            var model = SceneContext.Instance.GameModel.landPlots[plot.ID];

            if (model.gameObj)
            {
                handlingPacket = true;
                var location = model.gameObj.GetComponent<LandPlotLocation>();
                var landPlotComponent = model.gameObj.GetComponentInChildren<LandPlot>();
                location.Replace(landPlotComponent, GameContext.Instance.LookupDirector._plotPrefabDict[plot.Type]);

                var landPlotComponent2 = model.gameObj.GetComponentInChildren<LandPlot>();
                landPlotComponent2.ApplyUpgrades(plot.Upgrades.Cast<CppCollections.IEnumerable<LandPlot.Upgrade>>(), false);
                handlingPacket = false;
            }

            model.typeId = plot.Type;
            model.upgrades = plot.Upgrades;

            switch (plot.Data)
            {
                case InitialLandPlotsPacket.GardenData garden:
                    handlingPacket = true;
                    try
                    {
                        GardenPlotSyncManager.ApplyRemoteState(plot.ID, garden.HasCrop, garden.Crop, "initial garden plot");
                    }
                    finally { handlingPacket = false; }
                    break;
                case InitialLandPlotsPacket.SiloData silo: break; // todo
            }

            handlingPacket = true;
            try
            {
                LandPlotAmmoSyncManager.ApplyAmmoSets(model, plot.AmmoSets, plot.ID);
            }
            finally { handlingPacket = false; }

            handlingPacket = true;
            try
            {
                LandPlotFeederSyncManager.ApplyState(plot.ID, plot.FeederState, "initial feeder state");
            }
            finally { handlingPacket = false; }
        }
    }
}
