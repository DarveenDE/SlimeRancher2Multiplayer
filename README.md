# Slime Rancher 2 Multiplayer - SR2 1.2 patched fork

This fork is a Slime Rancher 2 `1.2.0` compatibility patch of the original
[pyeight/SlimeRancher2Multiplayer](https://github.com/pyeight/SlimeRancher2Multiplayer)
mod. It also contains extra multiplayer sync fixes and quality-of-life work that are not in the original repo.

Current local test target:

- Slime Rancher 2: `1.2.0`
- MelonLoader: `0.7.3-ci.2494 Open-Beta`
- SR2E: `3.7.0`
- SR2MP protocol: actively changing during development

Packet layouts have changed in this fork, so mixing this build with the original mod or an older local test build can cause broken joins, empty snapshots, or save-state desync.

## Save Warning

Back up your saves before testing. This fork is intended for active development and private playtesting, not stable public use.

## Current Status

Legend:

- ✅: works in the current fork or has been live-tested successfully.
- 🟡: implemented or improved, but still needs more playtesting.
- ⏳: active work; expect bugs.
- ❌: not implemented.

| Feature | Status | Notes |
| --- | --- | --- |
| Player movement | ✅ | Remote players move in-world. |
| Player animation | ✅ | Includes improved remote-player state handling. |
| Player sound/visual FX | 🟡 | Several common effects sync; some water/vac-specific effects may still be ❌. |
| Chat | ✅ | Includes server identity hardening. |
| Host/join UI | 🟡 | More native in-game UI flow than the original, but still a development UI. |
| Initial join sync | ✅ | Reliable snapshot barrier added so live packets do not interleave with the initial load. |
| Actors/items | 🟡 | Spawn/update/destroy sync works better; actor-id allocation and scene id handling were hardened. More edge cases are expected. |
| Slimes | 🟡 | Base sync works through actor sync; behavior/ownership edge cases can still desync. |
| Gardens | 🟡 | Initial crop/tree state, live plant/remove, growth/harvest state, and attached resource state are synced. Needs broader playtesting. |
| Silos | 🟡 | Initial and live ammo/storage slot sync added. Needs broader playtesting. |
| Plort collectors | 🟡 | Initial and live storage count sync added. Needs broader playtesting. |
| Auto-feeders | 🟡 | Storage state and feeder speed/state sync added. Needs broader playtesting. |
| Landplot upgrades | ✅ | Existing upgrade sync retained. |
| Refinery item counts | ⏳ | Initial/live count sync and repair snapshots are present, but refinery/fabricator counts still need focused two-player validation. |
| Plort puzzle statues/depositors | 🟡 | Initial and live slot/depositor state sync added. Needs more door/puzzle coverage testing. |
| Access doors | 🟡 | Initial and live door state sync improved. |
| Gordos | ✅ | Seen/eaten state sync improved so hidden map markers should stay hidden until discovered. |
| Map reveal | ✅ | Fog/map-node sync retained and remote snapshots now refresh visible fog immediately. |
| Remote player map markers | 🟡 | Remote player marker added for the full map and compass; full-map live refresh is still less reliable than the compass marker. |
| Slimepedia | ✅ | Initial sync retained. |
| Upgrades | ✅ | Initial sync retained. |
| Money | ✅ | Initial sync retained. |
| Time/weather | ✅ | Initial weather sync retained; weather packet participates in the initial sync barrier. |
| Market prices | ✅ | Includes price-change indicators. |
| Decorations/furniture/gadgets | 🟡 | Initial/live placement and demolish sync are present. Full gadget behavior/state sync is not complete. |
| Comm station / terminal messages | 🟡 | Conversation played/read state now syncs live, on join, and through repair snapshots. |
| Resource nodes / ores | 🟡 | Host-authoritative resource node state sync added for initial join, live spawn/harvest/despawn, and repair snapshots. Needs two-player validation. |
| Player inventory | ❌ | Not implemented. |

## Notable Changes In This Fork

- Updated the mod to load against Slime Rancher 2 `1.2.0`.
- Added a safer initial-sync completion flow for joining clients.
- Hardened reliable ordered packet handling, packet chunk validation, and server-side endpoint checks.
- Added remote player map and compass markers.
- Added or improved sync for gardens, landplot ammo/storage, auto-feeders, refinery item counts, puzzle statues/depositors, access doors, and Gordo seen state.
- Added live placement and demolish sync for player-placed gadgets/build-mode objects.
- Added comm station conversation played/read sync.
- Added host-authoritative resource node/ore state sync.
- Added immediate map-fog visual refresh after remote map snapshots are applied.
- Added local PowerShell helper scripts for syncing game references, building, and installing a test DLL.
- Added development notes in `docs/` for current bugs and networking follow-ups.

## Local Development

The helper scripts assume a local Steam install by default:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slime Rancher 2
```

You can override it with:

```powershell
$env:SR2_GAME_DIR = "D:\SteamLibrary\steamapps\common\Slime Rancher 2"
```

Build a local test DLL:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Mod.ps1 -Configuration Debug
```

The built DLL is written to:

```text
artifacts\SR2MP-Debug.dll
```

Install a local test build into the configured game folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Install-TestBuild.ps1 -Configuration Debug
```

Generated artifacts, downloaded dependencies, build output, and copied game reference DLLs are ignored by Git.

## Acknowledgements

Special thanks to:

- [pyeight](https://github.com/pyeight) and the original SR2MP contributors for the base multiplayer mod.
- [ThatFinn](https://github.com/ThatFinnDev) for developing and maintaining [Slime Rancher 2 Essentials](https://github.com/ThatFinnDev/SR2E).
- [Lachee](https://github.com/Lachee/) for the Discord RPC C# library.
