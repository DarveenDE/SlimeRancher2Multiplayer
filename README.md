# Slime Rancher 2 Multiplayer - SR2 1.2 patched fork

This fork is a Slime Rancher 2 `1.2.0` compatibility patch of the original
[pyeight/SlimeRancher2Multiplayer](https://github.com/pyeight/SlimeRancher2Multiplayer)
mod. It also contains extra multiplayer sync fixes and quality-of-life work that are not in the original repo.

Current local test target:

- Slime Rancher 2: `1.2.0`
- MelonLoader: `0.7.3-ci.2494 Open-Beta`
- SR2E: `3.7.0`
- SR2MP protocol: actively changing during development

Both players must use the exact same `SR2MP.dll`. Packet layouts have changed in this fork, so mixing this build with the original mod or an older local test build can cause broken joins, empty snapshots, or save-state desync.

## Save Warning

Back up your saves before testing. This fork is intended for active development and private playtesting, not stable public use.

## Current Status

Legend:

- Done: works in the current fork or has been live-tested successfully.
- Partial: implemented or improved, but still needs more playtesting.
- In progress: active work; expect bugs.
- Missing: not implemented.

| Feature | Status | Notes |
| --- | --- | --- |
| Player movement | Done | Remote players move in-world. |
| Player animation | Done | Includes improved remote-player state handling. |
| Player sound/visual FX | Partial | Several common effects sync; some water/vac-specific effects may still be missing. |
| Chat | Done | Includes server identity hardening. |
| Host/join UI | Partial | More native in-game UI flow than the original, but still a development UI. |
| Initial join sync | Done | Reliable snapshot barrier added so live packets do not interleave with the initial load. |
| Actors/items | Partial | Spawn/update/destroy sync works better; actor-id allocation and scene id handling were hardened. More edge cases are expected. |
| Slimes | Partial | Base sync works through actor sync; behavior/ownership edge cases can still desync. |
| Gardens | Partial | Initial crop/tree state and live plant/remove are synced in this fork. Attached fruit/resource state remains a follow-up. |
| Silos | Partial | Initial and live ammo/storage slot sync added. Needs broader playtesting. |
| Plort collectors | Partial | Initial and live storage count sync added. Needs broader playtesting. |
| Auto-feeders | Partial | Storage state and feeder speed/state sync added. Needs broader playtesting. |
| Landplot upgrades | Done | Existing upgrade sync retained. |
| Refinery item counts | In progress | Initial/live count sync added, but current playtest still reports empty refinery snapshots for the joining client. |
| Plort puzzle statues/depositors | Partial | Initial and live slot/depositor state sync added. Needs more door/puzzle coverage testing. |
| Access doors | Partial | Initial and live door state sync improved. |
| Gordos | Done | Seen/eaten state sync improved so hidden map markers should stay hidden until discovered. |
| Map reveal | Done | Fog/map-node sync retained. |
| Remote player map markers | Partial | Remote player marker added for the full map and compass; full-map live refresh is still less reliable than the compass marker. |
| Slimepedia | Done | Initial sync retained. |
| Upgrades | Done | Initial sync retained. |
| Money | Done | Initial sync retained. |
| Time/weather | Done | Initial weather sync retained; weather packet participates in the initial sync barrier. |
| Market prices | Done | Includes price-change indicators. |
| Decorations/furniture/gadgets | Partial | Initial placed gadget visibility was improved, but full gadget behavior/state sync is not complete. |
| Resource nodes | Missing | Not implemented. |
| Player inventory | Missing | Not implemented. |

## Notable Changes In This Fork

- Updated the mod to load against Slime Rancher 2 `1.2.0`.
- Added a safer initial-sync completion flow for joining clients.
- Hardened reliable ordered packet handling, packet chunk validation, and server-side endpoint checks.
- Added remote player map and compass markers.
- Added or improved sync for gardens, landplot ammo/storage, auto-feeders, refinery item counts, puzzle statues/depositors, access doors, and Gordo seen state.
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
