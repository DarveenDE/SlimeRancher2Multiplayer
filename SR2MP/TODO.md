# SR2MP TODO

Last updated: 2026-05-01

## Build and Tooling

- [x] Install .NET SDK 6.0.428 so `dotnet build SR2MP.csproj` can run locally.
- [ ] Evaluate upgrading SR2MP away from `net6.0` because .NET 6 is out of support.
  - Candidate targets: `net8.0` for nearest LTS compatibility, or `net10.0` only after checking MelonLoader/SR2E compatibility.
- [ ] Keep build verification in the normal loop after code changes.

## Multiplayer UI

- [x] Add a clearer host/join status layer for the existing IMGUI hub.
- [x] Add shared server-address parsing for the Join UI and `connect` command.
- [x] Add join confirmation and recent-server storage.
- [x] Add the first SR2E-native Multiplayer menu opened from the pause menu, with IMGUI kept as fallback.
- [x] Add a beginner-friendly native UI home screen with the primary choices "host" and "join".
- [x] Collapse Join into a single server-address field that accepts `host:port`.
- [x] Add a hosted-world result/session screen with port, player count, and close-host action.
- [x] Show connection and host errors with concrete next-step hints.
- [x] Avoid disabled primary buttons; explain blocked actions on click instead.
- [x] Show join progress as address check, connect, world sync, and done.
- [x] Move Settings out of the primary navigation.
- [x] Treat Players as a session overview for host/client state.
- [ ] Replace the IMGUI hub with native Unity UI / SR2-styled screens.
- [ ] Add a proper join flow with saved recent server picker, syncing state, timeout state, and failed-join state.
- [ ] Add a proper host flow with port validation, player count, close-host state, and clear port-in-use errors.
- [ ] Move chat from IMGUI into a native HUD panel.
- [ ] Add host player-management actions once the networking side supports them.

## Multiplayer Stability

- [x] Add retry queues for late-applied silo, plort collector, garden, and feeder states.
- [x] Add host-authoritative periodic repair snapshots for refinery counts, land plot ammo, garden crop state, feeder state, and puzzle/plort depositor counts.
- [x] Add lightweight desync diagnostics that log changed state hashes before/after repair snapshots.
- [x] Prefer host state as authoritative for world storage counts while still accepting client interaction events.
- [x] Add host repair snapshots for event-only world states: land plot type/upgrades, switches, access doors, map unlocks, and gordos.
- [x] Queue actor movement updates that arrive before the referenced actor exists locally instead of dropping them silently.
- [x] Harden refinery/fabricator count sync with ordered packets, apply-before-rebroadcast, and post-fabrication full snapshots.
- [ ] Run a focused two-player sync test and record repair-log findings for the known unstable areas.
- [x] Add garden growth and harvest state sync beyond planted crop type.
- [x] Add live gadget/build-mode placement sync for player-placed gadgets.
- [x] Throttle/coalesce garden growth sync and suppress unchanged actor updates to avoid long-session packet floods.
- [x] Remove remote player map/compass marker re-registration churn that could drag down FPS over time.
- [x] Harden packet handlers against stale client input, missing Unity components, and stuck echo-suppression flags.
- [x] Add a protocol/game-target handshake so mismatched SR2MP test DLLs fail with a clear join error.
- [ ] Define stricter host-authoritative validation for client-triggered pedia unlocks, player upgrades, and fast-forward requests.
- [ ] Replace ReliableOrdered gap-skipping with a real reorder buffer if strict ordered delivery becomes important for more packet types.
- [ ] Test known desync areas with two players: silos, refinery/fabricator counts, gardens, auto-feeders, plort collectors, plort depositors, gadget placement, and overnight FPS stability.
- [ ] Add and test gadget pickup/demolish sync after live gadget placement is verified.

## Manual QA

- [x] Build the project after the SDK install.
- [ ] Test host start and close in-game.
- [ ] Test client connect, initial sync, chat, disconnect, and reconnect with two game instances.
- [ ] Test failure cases: bad hostname, unused port, server closed during join, and port already in use.
