using System.Net;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Economy;
using Il2CppMonomiPark.SlimeRancher.Event;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using Il2CppMonomiPark.SlimeRancher.Pedia;
using Il2CppMonomiPark.SlimeRancher.Weather;
using MelonLoader;
using SR2MP.Packets.Economy;
using SR2MP.Packets.Loading;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.Connect)]
public sealed class ConnectHandler : BasePacketHandler<ConnectPacket>
{
    public ConnectHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ConnectPacket packet, IPEndPoint clientEp)
    {
        SrLogger.LogMessage($"Connect request received with PlayerId: {packet.PlayerId}",
            $"Connect request from {clientEp} with PlayerId: {packet.PlayerId}");

        if (!clientManager.TryAddClient(clientEp, packet.PlayerId, out _))
            return;

        MelonCoroutines.Start(HandleInitialSync(packet.PlayerId, clientEp));
    }

    // Spreads data collection across frames so no single frame is overloaded on the host.
    private static System.Collections.IEnumerator HandleInitialSync(string playerId, IPEndPoint clientEp)
    {
        var pendingInitialPackets = new List<ushort>();

        var money = SceneContext.Instance.PlayerState.GetCurrency(GameContext.Instance.LookupDirector._currencyList[0]
            .Cast<ICurrency>());
        var rainbowMoney =
            SceneContext.Instance.PlayerState.GetCurrency(GameContext.Instance.LookupDirector._currencyList[1]
                .Cast<ICurrency>());

        var ackPacket = new ConnectAckPacket
        {
            PlayerId = playerId,
            OtherPlayers = Array.ConvertAll(playerManager.GetAllPlayers().ToArray(), input => (input.PlayerId, input.Username)),
            Money = money,
            RainbowMoney = rainbowMoney,
            AllowCheats = Main.AllowCheats
        };
        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(ackPacket, clientEp));

        yield return null;

        SendGordosPacket(clientEp, pendingInitialPackets);

        yield return null;

        SendSwitchesPacket(clientEp, pendingInitialPackets);
        SendPuzzleStatesPacket(clientEp, pendingInitialPackets);

        yield return null;

        SendPlotsPacket(clientEp, pendingInitialPackets);

        yield return null;

        // Most expensive: iterates all identifiables in the game model
        SendActorsPacket(clientEp, PlayerIdGenerator.GetPlayerIDNumber(playerId), pendingInitialPackets);

        yield return null;

        SendGardenResourceAttachments(clientEp, pendingInitialPackets);

        yield return null;

        SendUpgradesPacket(clientEp, pendingInitialPackets);
        SendRefineryItemsPacket(clientEp, pendingInitialPackets);
        SendPediaPacket(clientEp, pendingInitialPackets);
        SendMapPacket(clientEp, pendingInitialPackets);
        SendAccessDoorsPacket(clientEp, pendingInitialPackets);
        SendPricesPacket(clientEp, pendingInitialPackets);

        SrLogger.LogMessage($"Initial sync started for player {playerId}",
            $"Initial sync started for player {playerId} from {clientEp}");

        yield return SendWeatherAndCompleteInitialSync(clientEp, pendingInitialPackets);
    }

    private static void SendUpgradesPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var upgrades = new Dictionary<byte, sbyte>();

        foreach (var upgrade in GameContext.Instance.LookupDirector._upgradeDefinitions.items)
        {
            upgrades.Add((byte)upgrade._uniqueId, (sbyte)SceneContext.Instance.PlayerState._model.upgradeModel.GetUpgradeLevel(upgrade));
        }

        var upgradesPacket = new InitialUpgradesPacket
        {
            Upgrades = upgrades,
        };
        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(upgradesPacket, client));
    }

    private static void SendRefineryItemsPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var packet = new RefineryItemCountsPacket
        {
            Items = RefinerySyncManager.CreateSnapshot(includeZeroCounts: true),
        };

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(packet, client));
    }

    private static System.Collections.IEnumerator SendWeatherAndCompleteInitialSync(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var weatherRegistry = Resources.FindObjectsOfTypeAll<WeatherRegistry>().FirstOrDefault();
        if (weatherRegistry != null && weatherRegistry._model != null)
        {
            bool weatherPacketCreated = false;
            MelonCoroutines.Start(
                WeatherPacket.CreateFromModel(
                    weatherRegistry._model,
                    PacketType.InitialWeather,
                    packet =>
                    {
                        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(packet, client));
                        weatherPacketCreated = true;
                    }
                )
            );

            while (!weatherPacketCreated)
                yield return null;
        }
        else
        {
            SrLogger.LogError("WeatherRegistry or model not found!", SrLogTarget.Both);
        }

        var timeoutAt = UnityEngine.Time.realtimeSinceStartup + 12f;
        while (Main.Server.AreReliablePacketsPending(client, pendingInitialPackets)
               && UnityEngine.Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        if (Main.Server.AreReliablePacketsPending(client, pendingInitialPackets))
        {
            SrLogger.LogWarning(
                $"Initial sync for {client} timed out waiting for {pendingInitialPackets.Count} reliable packet ACK(s); sending completion anyway",
                SrLogTarget.Both);
        }

        Main.Server.SendToClient(new InitialSyncCompletePacket(), client);
    }

    private static void SendPediaPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var unlocked = SceneContext.Instance.PediaDirector._pediaModel.unlocked;

        var unlockedArray = Il2CppSystem.Linq.Enumerable
            .ToArray(unlocked.Cast<CppCollections.IEnumerable<PediaEntry>>());

        var unlockedIDs = unlockedArray.Select(entry => entry.PersistenceId).ToList();

        var pediasPacket = new InitialPediaPacket
        {
            Entries = unlockedIDs
        };

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(pediasPacket, client));
    }

    private static void SendMapPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var mapPacket = new InitialMapPacket
        {
            UnlockedNodes = MapUnlockSyncManager.CreateSnapshot()
        };

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(mapPacket, client));
    }

    private static void SendAccessDoorsPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var doorsList = new List<InitialAccessDoorsPacket.Door>();

        foreach (var door in SceneContext.Instance.GameModel.doors)
        {
            doorsList.Add(new InitialAccessDoorsPacket.Door
            {
                ID = door.Key,
                State = door.Value.state
            });
        }

        var accessDoorsPacket = new InitialAccessDoorsPacket
        {
            Doors = doorsList
        };

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(accessDoorsPacket, client));
    }

    private static void SendActorsPacket(IPEndPoint client, ushort playerIndex, List<ushort> pendingInitialPackets)
    {
        var actorsList = new List<InitialActorsPacket.Actor>();
        var skipped = 0;

        foreach (var actorKeyValuePair in SceneContext.Instance.GameModel.identifiables)
        {
            var actor = actorKeyValuePair.Value;
            if (!TryCreateInitialActorEntry(actor, out var entry))
            {
                skipped++;
                continue;
            }

            actorsList.Add(entry);
        }

        var actorsPacket = new InitialActorsPacket
        {
            StartingActorID = (uint)NetworkActorManager.GetNextActorIdInRange(playerIndex * 10000, (playerIndex * 10000) + 10000),
            Actors = actorsList
        };

        SrLogger.LogMessage(
            $"Initial actor snapshot prepared for {client}: actors={actorsList.Count}, skipped={skipped}",
            SrLogTarget.Both);

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(actorsPacket, client));
    }

    private static bool TryCreateInitialActorEntry(
        IdentifiableModel actor,
        out InitialActorsPacket.Actor entry)
    {
        entry = default;

        try
        {
            if (actor == null || !actor.ident || actor.ident.IsPlayer || !actor.sceneGroup)
                return false;

            var model = actor.TryCast<ActorModel>();
            var gadgetModel = actor.TryCast<GadgetModel>();
            var rotation = model?.lastRotation ?? gadgetModel?.GetRot() ?? Quaternion.identity;
            var position = gadgetModel?.GetPos() ?? actor.lastPosition;

            entry = new InitialActorsPacket.Actor
            {
                ActorId = actor.actorId.Value,
                ActorType = NetworkActorManager.GetPersistentID(actor.ident),
                Position = position,
                Rotation = rotation,
                Scene = NetworkSceneManager.GetPersistentID(actor.sceneGroup),
                IsPrePlaced = gadgetModel?.IsPrePlaced ?? false
            };

            return true;
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning(
                $"Skipped actor during initial snapshot: {ex.Message}",
                SrLogTarget.Both);
            return false;
        }
    }

    private static void SendGardenResourceAttachments(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        foreach (var packet in GardenResourceAttachSyncManager.CreateGardenSnapshots(SceneContext.Instance.GameModel))
            TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(packet, client));
    }

    private static void SendPuzzleStatesPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var slots = new List<InitialPuzzleStatesPacket.PuzzleSlot>();
        foreach (var slot in SceneContext.Instance.GameModel.slots)
        {
            slots.Add(new InitialPuzzleStatesPacket.PuzzleSlot
            {
                ID = slot.Key,
                Filled = slot.Value.filled,
            });
        }

        var depositors = new List<InitialPuzzleStatesPacket.PlortDepositor>();
        foreach (var depositor in SceneContext.Instance.GameModel.depositors)
        {
            depositors.Add(new InitialPuzzleStatesPacket.PlortDepositor
            {
                ID = depositor.Key,
                AmountDeposited = depositor.Value.AmountDeposited,
            });
        }

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(new InitialPuzzleStatesPacket
        {
            Slots = slots,
            Depositors = depositors,
        }, client));
    }

    private static void SendSwitchesPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var switchesList = new List<InitialSwitchesPacket.Switch>();

        foreach (var switchKeyValuePair in SceneContext.Instance.GameModel.switches)
        {
            switchesList.Add(new InitialSwitchesPacket.Switch
            {
                ID = switchKeyValuePair.key,
                State = switchKeyValuePair.value.state,
            });
        }

        var switchesPacket = new InitialSwitchesPacket
        {
            Switches = switchesList
        };

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(switchesPacket, client));
    }

    private static void SendGordosPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var gordosList = new List<InitialGordosPacket.Gordo>();

        foreach (var gordo in SceneContext.Instance.GameModel.gordos)
        {
            var eatCount = gordo.value.GordoEatenCount;
            if (eatCount == -1)
                eatCount = gordo.value.targetCount;

            gordosList.Add(new InitialGordosPacket.Gordo
            {
                Id = gordo.key,
                EatenCount = eatCount,
                RequiredEatCount = gordo.value.targetCount,
                GordoType = NetworkActorManager.GetPersistentID(gordo.value.identifiableType),
                WasSeen = gordo.value.GordoSeen
                //Popped = gordo.value.GordoEatenCount > gordo.value.gordoEatCount
            });
        }

        var gordosPacket = new InitialGordosPacket
        {
            Gordos = gordosList
        };

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(gordosPacket, client));
    }

    private static void SendPlotsPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var plotsList = new List<InitialLandPlotsPacket.BasePlot>();

        foreach (var plotKeyValuePair in SceneContext.Instance.GameModel.landPlots)
        {
            var plot = plotKeyValuePair.Value;
            var id = plotKeyValuePair.Key;

            INetObject? data = plot.typeId switch
            {
                LandPlot.Id.GARDEN => new InitialLandPlotsPacket.GardenData
                {
                    HasCrop = GardenPlotSyncManager.TryGetCurrentCropType(plot, out var crop),
                    Crop = crop
                },
                LandPlot.Id.SILO => new InitialLandPlotsPacket.SiloData
                    {},
                _ => null
            };

            plotsList.Add(new InitialLandPlotsPacket.BasePlot
            {
                ID = id,
                Type = plot.typeId,
                Upgrades = plot.upgrades,
                AmmoSets = LandPlotAmmoSyncManager.CreateAmmoSets(plot),
                FeederState = LandPlotFeederSyncManager.CreateState(plot),
                Data = data
            });
        }

        var plotsPacket = new InitialLandPlotsPacket
        {
            Plots = plotsList
        };

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(plotsPacket, client));
    }

    private static void SendPricesPacket(IPEndPoint client, List<ushort> pendingInitialPackets)
    {
        var pricesPacket = new MarketPricePacket
        {
            Prices = MarketPricesArray!
        };

        TrackPendingPacket(pendingInitialPackets, Main.Server.SendToClient(pricesPacket, client));
    }

    private static void TrackPendingPacket(List<ushort> pendingInitialPackets, ushort? packetId)
    {
        if (packetId.HasValue)
            pendingInitialPackets.Add(packetId.Value);
    }
}
