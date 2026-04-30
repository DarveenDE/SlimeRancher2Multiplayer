# Test Bugs

## Client cannot see host furniture / placed gadgets

- Reported: 2026-04-29 during live host/client test.
- Symptom: furniture/placed decoration that is visible for the host is not visible for the client.
- Repro detail: the furniture already existed before the client joined; this affects initial join/loading sync, not only live placement during a session.
- Current diagnosis: placed gadgets are included in the host's `GameModel.identifiables`, but `NetworkActorManager.TrySpawnNetworkActor` explicitly skipped `type.isGadget()` with a "not implemented yet" warning. During initial actor load, the client removes local non-player identifiables and then fails to recreate gadget actors, so placed furniture/decor does not appear.
- Local status: initial gadget spawn support has been implemented in the repo and builds successfully. Pending live test with both players on the same new SR2MP DLL.
- Relevant code:
  - `SR2MP/Shared/Managers/NetworkActorManager.cs`
  - `SR2MP/Client/Handlers/InitialActorLoadHandler.cs`
  - `SR2MP/Server/Handlers/ConnectHandler.cs`
- Likely fix: add a dedicated gadget sync path instead of treating gadgets as normal actors. Use `GadgetDirector.InstantiateGadgetFromModel` / `InstantiateGadget` and a packet that carries actor id, type id, scene group, position, rotation, preplaced flag, and gadget-specific state as needed.

## Client cannot see planted crop/tree fields

- Reported: 2026-04-29 during live host/client test.
- Symptom: fields/plots where trees or other crops are planted are not shown for the other player.
- Follow-up report: with the first test DLL, a cuberry tree still did not appear on the client. The client UI said nothing was planted; when the client threw in a berry locally, trees grew.
- Repro detail: reported after furniture visibility fix work; exact timing still needs confirmation. It may affect live planting, initial join state, or both.
- Current diagnosis:
  - Do not simply uncomment the old patches. Commit history explicitly labels this as broken / half-broken garden synchronization:
    - `c68505e` - first garden synchronization implementation.
    - `3959c36` - "half-broken garden synchronisation", added resource attach sync.
    - `fb56dad` / `3b4f568` - temporarily disabled garden/resource synchronization.
  - The existing `GardenPlantPacket` path only syncs "this plot has this primary crop actor type". It does not sync the full `ResourceGrowerDefinition` identity, grower state, attached resource actors, ripeness, water, or ordering against actor spawn packets.
  - `Patches/Plots/PlantGarden.cs` and `Patches/Plots/OnDestroyCrop.cs` are commented out, so live planting/removal currently does not send `GardenPlantPacket` at all.
  - `Patches/Actor/OnResourceAttach.cs` is also effectively disabled; it builds `ResourceAttachPacket` but does not send it. This means resources/fruits attached to growers are not synchronized through that path.
- Risks in the old implementation:
  - `GardenCatcher.Plant` postfix would send even if the original plant call failed unless it checks `__result`.
  - `DestroyAttached` can be triggered as part of replacement/demolition, so a clear-crop packet can race with plot replacement.
  - `ResourceAttachPacket` can arrive before the spawned resource actor exists on the receiver, and the handler currently drops it instead of queueing/retrying.
  - The initial join sync uses hard-coded crop id `9` as "empty crop"; that should become an explicit nullable/empty flag.
  - Initial plot sync and initial actor sync are separate packets; a planted tree/grower visual or attached resource actor may be affected by ordering during join.
- Safer direction:
  - First split the problem into two layers: plot crop/grower selection, then spawned/attached resources.
  - Sync a stable resource grower id, not just the primary resource actor id.
  - Make planting/removal packets idempotent and ordered with land-plot replacement.
  - Queue `ResourceAttachPacket` until the referenced actor and plot/spawner exist.
  - Only then re-enable Harmony patches, with `handlingPacket` guarded by `try/finally`.
- Local status:
  - First-pass crop/garden state sync has been implemented in the repo and builds successfully.
  - It does not re-enable `ResourceAttachPacket`; attached fruits/resources remain a separate follow-up.
  - The old hard-coded `9 = empty crop` marker was replaced with an explicit `HasCrop` flag.
  - Live plant/remove patches now debounce for one frame and send the final plot model state instead of sending immediately from the Harmony postfix.
  - Follow-up fix: crop detection now checks the actual attached crop on the `LandPlot` before falling back to `LandPlotModel.resourceGrowerDefinition`.
  - Follow-up fix: remote apply no longer pre-fills `resourceGrowerDefinition` before calling `GardenCatcher.Plant`, because that can make the game reject the visible tree spawn as already planted.
  - Pending live test with both players on the same new SR2MP DLL.
- Relevant code to inspect first:
  - `SR2MP/Packets/Loading/InitialLandPlotsPacket.cs`
  - `SR2MP/Client/Handlers/InitialLandPlotsLoadHandler.cs`
  - `SR2MP/Server/Handlers/ConnectHandler.cs`
  - `SR2MP/Packets/Landplot/LandPlotUpdatePacket.cs`
  - `SR2MP/Client/Handlers/LandplotUpdateHandler.cs`

## Bought toy appears as the wrong object for the other player

- Reported: 2026-04-29 during live host/client test.
- Symptom: host bought a fox toy; client saw an "instabile Frucht" instead, and slimes treated it as food.
- Current diagnosis:
  - The mod relied on shared actor ids for live spawned objects.
  - Host/client setup set the next actor id to the highest existing id in a range, not the next free id. That can reuse an existing id and make later spawn/update/destroy packets target the wrong local object.
  - Live `ActorSpawnPacket` also truncated the scene group id to one byte, which is risky for newer/larger scene id sets.
- Local status:
  - Actor id setup now uses a new `GetNextActorIdInRange(...)` helper.
  - `ActorSpawnPacket.SceneGroup` now serializes as `int`.
  - Build succeeds. Pending live test with both players on the same new SR2MP DLL.
- Relevant code:
  - `SR2MP/Shared/Managers/NetworkActorManager.cs`
  - `SR2MP/Patches/Actor/OnGameLoadPatch.cs`
  - `SR2MP/Server/Handlers/ConnectHandler.cs`
  - `SR2MP/Packets/Actor/ActorSpawnPacket.cs`

## Plort puzzle statues / doors do not sync

- Reported: 2026-04-29 during live host/client test.
- Symptom: putting a plort into a statue/puzzle works for one player, but the other player does not see the puzzle as solved and the door remains closed.
- Current diagnosis:
  - Existing sync covered `WorldSwitchModel` and `AccessDoorModel`, but the game also stores puzzle statue state in `PuzzleSlotModel` and `PlortDepositorModel`.
  - Those model dictionaries (`GameModel.slots` and `GameModel.depositors`) were not included in initial join sync and had no live packets.
- Local status:
  - Added reliable live packets for puzzle slot filled state and plort depositor amount.
  - Added initial join snapshot for all puzzle slots and plort depositors.
  - Added guarded remote application through the game's own `OnFilledChangedFromModel()` methods.
  - Build succeeds. Pending live test with both players on the same new SR2MP DLL.
- Relevant code:
  - `SR2MP/Patches/World/OnPuzzleStateChanged.cs`
  - `SR2MP/Shared/Managers/PuzzleStateSyncManager.cs`
  - `SR2MP/Packets/World/PuzzleSlotStatePacket.cs`
  - `SR2MP/Packets/World/PlortDepositorStatePacket.cs`
  - `SR2MP/Packets/Loading/InitialPuzzleStatesPacket.cs`

## Map player marker missing / all Gordos visible

- Reported: 2026-04-29 during live test of the first map-marker DLL.
- Symptoms:
  - Own player position no longer appeared on the map.
  - Other player position also did not appear.
  - The map showed all Gordos, including not-yet-discovered ones.
- Current diagnosis:
  - Remote player markers used the game's built-in `PlayerMapMarkerSource`, which appears to interfere with the normal single-player map player marker path.
  - Initial Gordo sync carried `WasSeen`, but the client ignored it and left/created Gordo models without applying the host's seen state.
- Local status:
  - Remote player markers now use `MapNavigationMarkerData` instead of `PlayerMapMarkerSource`, so they should no longer compete with the game's own player marker.
  - Follow-up fix: remote player markers now re-register with the map UI every 0.5 seconds, because the marker source position updated but the open map view only refreshed after reopening the menu.
  - Follow-up feature: remote player markers are now also registered with the game's `RadarRegistry`, so they should appear and update on the HUD compass as well.
  - Initial Gordo load now applies `gordoModel.SetSeen(gordo.WasSeen)` and initializes new models with `gordoSeen = gordo.WasSeen`.
  - Build succeeds. Pending live test with both players on the same new SR2MP DLL.
- Relevant code:
  - `SR2MP/Components/Map/RemotePlayerMapMarkers.cs`
  - `SR2MP/Client/Handlers/InitialGordoLoadHandler.cs`

## Plort collector storage count does not match on join

- Reported: 2026-04-29 during live test.
- Symptom: host's plort collector showed 27 stored plorts; the joining client saw 1.
- Current diagnosis:
  - `InitialLandPlotsPacket` synchronized plot type and upgrades, plus garden-specific data, but did not include the plot ammo/storage data stored in `LandPlotModel.siloAmmo`.
  - Plort collectors use the same land-plot ammo model as silo-style storage, so the client kept its local/default storage state when joining.
- Local status:
  - Initial plot sync now includes per-plot ammo sets and slot contents.
  - Client applies those ammo slots after replacing/upgrading the plot during initial load.
  - Build succeeds. Pending live test with both players on the same new SR2MP DLL.
- Relevant code:
  - `SR2MP/Packets/Loading/InitialLandPlotsPacket.cs`
  - `SR2MP/Shared/Managers/LandPlotAmmoSyncManager.cs`
  - `SR2MP/Server/Handlers/ConnectHandler.cs`
  - `SR2MP/Client/Handlers/InitialLandPlotsLoadHandler.cs`
