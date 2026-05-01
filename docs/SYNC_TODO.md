# SR2MP Sync Follow-up TODO

Last updated: 2026-05-01

This document is the working basis for the next multiplayer-sync pass. It is based on the current source review and a clean local build of `SR2MP.csproj`.

## Working Definitions

- **Host-authoritative shared state**: state that affects the world for everyone, such as land plots, refinery counts, gardens, resource nodes, gordos, switches, access doors, map unlocks, weather, world time, puzzle slots, plort depositors, and actor lifetime.
- **Per-player local state**: state that should remain personal to each player, such as the contents of each player's vacpack/inventory.
- **Inventory-backed world transaction**: an action that starts from a player's own inventory but changes shared world state, such as feeding a gordo, depositing plorts, storing items, selling items, planting a crop, or spending refinery resources.

Important clarification: **player inventories should not be globally equalized**. Each player should keep their own inventory. The open question is whether the host merely accepts the resulting world changes from clients, or whether the host also tracks/validates inventory-backed transactions.

## Priority 1: Make Host Authority Explicit

Goal: client packets should be treated as requests for shared-state changes, not as unconditional truth.

Status 2026-05-01:

- Implemented a first code-level client-to-server packet authority gate in `Shared/Utils/PacketAuthority.cs`.
- `Server/Managers/PacketManager.cs` now rejects server-only, reserved, and unknown packet types from clients before chunk merge, deduplication, or handler dispatch.
- Hardened pedia unlocks and player upgrades so duplicate/maxed/unknown requests are not blindly applied or rebroadcast.
- Hardened client-origin actor spawn so the host only rebroadcasts successfully applied spawns from the sender's assigned actor-id range.
- Added actor ownership tracking in `NetworkActorManager.ActorOwners` and basic owner validation for actor update, destroy, transfer, and unload requests.
- This is still an ingress and basic business-rule hardening step. Deeper per-action validation remains open below.

### Tasks

- [x] Create a first central packet authority matrix for client-to-server ingress.
  - Packet type
  - Whether clients may send it to the host
  - Current implementation: `PacketAuthority.GetClientToServerRule(...)`

- [ ] Expand the authority matrix with full metadata.
  - Direction
  - Reliability
  - Whether the host applies the packet directly, validates it, ignores it, or converts it into a host-generated packet
  - Whether the state is covered by initial sync
  - Whether the state is covered by repair snapshots

- [x] Harden client-triggered pedia unlocks against empty, unknown, duplicate, and host-rejected requests.
  - Current code path: `Patches/Pedia/OnEntryUnlocked.cs`, `Server/Handlers/PediaUnlockHandler.cs`
  - Current policy: treated as shared progression because initial sync already applies host pedia state to clients.
  - Implemented: server only rebroadcasts if the host actually unlocked a new pedia entry.

- [ ] Decide long-term pedia policy.
  - Decide whether pedia unlocks are shared world progression or per-player progression.
  - If shared: host should preferably derive the unlock from host-side state where possible.
  - If per-player: stop rebroadcasting pedia unlocks globally.

- [x] Harden client-triggered player upgrades against unknown, maxed, and host-rejected requests.
  - Current code path: `Patches/Player/OnPlayerUpgraded.cs`, `Server/Handlers/PlayerUpgradeHandler.cs`
  - Current policy: treated as shared progression because initial sync already applies host upgrade state to clients.
  - Implemented: server validates upgrade id, current level, next level existence, and `IncrementUpgradeLevel(...)` result before rebroadcast.

- [ ] Finish player-upgrade authority policy.
  - Decide whether upgrades are shared or per-player long term.
  - Validate requirements/costs before accepting, or move upgrade purchase authority entirely to the host.
  - Consider changing future packets from "increment this upgrade" to "host says this upgrade is now level N".

- [ ] Harden currency changes.
  - Current code path: `Patches/Economy/CurrencyPatch.cs`, `Server/Handlers/CurrencyHandler.cs`
  - Decide whether currency is global/shared or per-player.
  - If shared: host should compute accepted deltas from validated actions, not accept arbitrary client `NewAmount`.
  - If per-player: do not broadcast every player's currency amount to everyone.

- [x] Harden client-triggered actor spawn basics.
  - Current code path: `Server/Handlers/ActorSpawnHandler.cs`, `Server/Handlers/ActorDestroyHandler.cs`, `Server/Handlers/ActorTransferHandler.cs`
  - Implemented: validate actor id range against the sender's assigned player-id range.
  - Implemented: do not rebroadcast actor spawn if the host failed to spawn it.
  - Existing `NetworkActorManager.TrySpawnNetworkActor(...)` validates actor type and scene group.

- [x] Harden actor update/destroy/transfer/unload ownership basics.
  - Implemented: track current server-authoritative actor owner in `NetworkActorManager.ActorOwners`.
  - Implemented: client-origin `ActorUpdate`, `ActorDestroy`, `ActorTransfer`, and `ActorUnload` are rejected when the sender does not own the actor.
  - Implemented: host-local spawn, vac, hibernation, and existing-world setup paths maintain host ownership.
  - Implemented: owner data is cleared on actor removal and transient session reset.

- [ ] Add deeper actor action validation.
  - Validate destroy and transfer requests against host visibility/proximity/action rules, not only current ownership.
  - Ensure client-origin gadget placement cannot collide with existing host actors beyond actor-id collision checks.
  - Decide how to resolve legitimate ownership races when two clients interact with the same actor nearly simultaneously.

- [ ] Add explicit rejection logging for invalid client mutations.
  - Status 2026-05-01: invalid packet types are now rejected and logged at server ingress.
  - Status 2026-05-01: pedia unlock, player upgrade, actor spawn, actor update, actor destroy, actor transfer, and actor unload rejections now include endpoint/player context.
  - Remaining: other invalid business mutations still need per-handler validation logs.
  - Include player id, endpoint, packet type, reason, and relevant ids.
  - Keep logs concise enough for two-player QA sessions.

### Acceptance Criteria

- [ ] Every client-origin packet that can mutate shared state has a documented rule.
- [ ] Unknown, stale, impossible, or unauthorized mutations are rejected without changing host state.
- [ ] Rejected mutations are visible in logs with enough detail to debug desyncs.

## Priority 2: Expand Repair Snapshot Coverage

Goal: every host-authoritative shared state that can drift should be restorable from the host without requiring a reconnect.

Current repair coverage is already broad in `Shared/Managers/WorldStateRepairManager.cs`: refinery, land plots, ammo, gardens, garden growth, garden attachments, feeders, switches, access doors, map unlocks, comm station state, resource nodes, gordos, puzzle slots, and plort depositors.

### Remaining Coverage Gaps

- [ ] Currency repair snapshot, if currency is shared/global.
  - Include both regular and rainbow currency.
  - Consider sending as absolute host amount, like current `CurrencyPacket`.

- [ ] Pedia repair snapshot, if pedia is shared/global.
  - Initial sync already sends pedia entries.
  - Periodic repair currently does not.

- [ ] Player upgrade repair snapshot, if upgrades are shared/global.
  - Initial sync already sends upgrade levels.
  - Periodic repair currently does not.

- [ ] Market price repair snapshot.
  - Initial sync sends prices from `ConnectHandler`.
  - Host price resets are broadcast, but periodic repair does not resend market prices.
  - Add this if price drift is observed or if clients can miss a reliable price packet in long sessions.

- [ ] Map navigation markers.
  - Current TODO exists in `Packets/Loading/InitialMapPacket.cs`.
  - Decide whether navigation marker state is shared, per-player, or ignored.

- [ ] Gadget-specific state snapshot audit.
  - Actor spawn/destroy covers gadget lifetime.
  - It does not necessarily cover every internal gadget setting/model field.
  - Build a list of player-placeable gadgets and identify which fields need initial sync, live sync, and repair sync.

- [ ] Optional: host session/player metadata repair.
  - Check whether late join/reconnect leaves remote player markers, names, or player objects stale.

### Acceptance Criteria

- [ ] `WorldStateRepairManager` logs include all intended shared-state categories.
- [ ] For each shared-state category, we know whether repair is implemented, intentionally skipped, or still TODO.
- [ ] A client can recover from a missed non-critical reliable packet via repair snapshot instead of requiring reconnect.

## Priority 3: Replace ReliableOrdered Gap-Skipping

Goal: ordered reliable packets should not permanently skip meaningful state changes when UDP packets are reordered.

Current behavior in `Shared/Managers/ReliabilityManager.cs` accepts newer ordered packets when a sequence gap is detected to keep the stream alive. That avoids a stuck stream, but it is not strict ordered delivery.

### Tasks

- [ ] Implement a per-endpoint, per-packet-type reorder buffer.
  - Key: endpoint + packet type.
  - Buffer newer packets until the missing sequence arrives.
  - Deliver buffered packets in order.
  - Handle sequence wrap-around.

- [ ] Define buffer limits.
  - Maximum buffered packets per stream.
  - Timeout for missing sequence.
  - Behavior when timeout expires: request repair, drop stream, or disconnect depending on packet type.

- [ ] Review which packet types truly need `ReliableOrdered`.
  - Current important examples: chat, refinery counts, resource attach, connect ack, player leave.
  - Some absolute-state packets may not need strict ordering if repair snapshots can correct them.

- [ ] Add simulated loss/reorder testing.
  - Force packet N+1 before N.
  - Drop N once and verify resend unblocks the buffer.
  - Verify duplicates are acknowledged and ignored.

### Acceptance Criteria

- [ ] No ordered packet is applied after a later packet for the same endpoint/type unless explicitly allowed.
- [ ] Reordering does not cause permanent desync.
- [ ] Logs distinguish duplicate, buffered, delivered, timed out, and repair-requested ordered packets.

## Priority 4: Clarify Player Inventory Policy

Goal: each player keeps their own inventory, but inventory-backed world transactions should not create unrepairable world desync.

### Design Decision

- [ ] Choose one of these policies:
  - **Trusted local inventory**: each client owns their vacpack/inventory locally. The host syncs only world effects. This is simpler and probably fine for friendly co-op.
  - **Host-tracked inventory ledger**: the host tracks enough per-player inventory changes to validate deposits, feeding, selling, planting, shooting, and spending.

### If Trusted Local Inventory

- [ ] Document that vacpack contents are intentionally not synchronized globally.
- [ ] Rename or remove the unused `PlayerInventoryTimer` in `Shared/Utils/Timers.cs` if it remains unused.
- [ ] Make sure world effects have strong repair coverage.
- [ ] Accept that the host cannot fully validate whether a client actually had the item they used.

### If Host-Tracked Inventory Ledger

- [ ] Add per-player inventory snapshot/transaction packets.
  - Vac slot type
  - Count
  - Radiant state, if applicable
  - Slot changes
  - Item acquisition/removal transactions

- [ ] Validate inventory-backed world transactions.
  - Gordo feeding
  - Plort depositor deposits
  - Refinery deposits/spends
  - Silo/plort collector storage changes
  - Garden planting
  - Plort market selling
  - Shooting/spawning actors from vacpack

- [ ] Define reconnect behavior.
  - Should the client keep its local inventory?
  - Should the host restore known inventory?
  - Should joining require a fresh client save?

### Acceptance Criteria

- [ ] The project has a clear written policy: inventories are personal, but world effects are host-authoritative.
- [ ] The unused inventory timer is either used intentionally or removed/renamed.
- [ ] QA can distinguish expected per-player inventory differences from actual world desync.

## Priority 5: Finish Gadget Sync

Goal: player-placed gadgets should have correct lifetime, ownership, placement, removal, and state across host/client.

Current status: live gadget/build-mode placement has ActorSpawn support. Gadget destroy/pickup/demolish still needs focused validation.

### Tasks

- [ ] Test live gadget placement from host.
  - Host places gadget.
  - Client sees correct type, position, rotation, scene group.
  - Reconnect client sees it in initial actor snapshot.

- [ ] Test live gadget placement from client.
  - Client places gadget.
  - Host validates actor id/type/scene group.
  - Other clients receive the host-accepted spawn.

- [ ] Test gadget pickup/demolish/destroy.
  - Host removes gadget.
  - Client removes gadget.
  - Reconnect reflects final host state.
  - No duplicate ghost gadget remains in `GameModel.identifiables` or `actorManager.Actors`.

- [ ] Audit gadget-specific model fields.
  - Identify which gadget types have internal state beyond transform/type.
  - Add initial/live/repair sync for stateful gadgets as needed.

- [ ] Add gadget repair snapshot if actor lifetime is not enough.
  - Compare host placed-gadget list to client local list.
  - Correct missing, duplicate, or wrong-type gadgets.

### Acceptance Criteria

- [ ] Gadget placement/removal works from both host and client in a two-player session.
- [ ] Reconnect does not create duplicates or lose placed gadgets.
- [ ] Stateful gadgets either sync correctly or are documented as unsupported.

## Priority 6: Validate Garden And Produce Host Authority

Goal: garden growth and attached produce should be driven by host state, with clients applying host updates cleanly.

Current status: the server ignores client garden growth state, and client-origin garden resource attach is suppressed for garden produce. Host repair snapshots include garden crop state, garden growth state, produce state, and garden resource attachments.

### Tasks

- [ ] Test host planting and harvesting.
  - Client sees crop type.
  - Client sees growth timer effects.
  - Client sees produce attach/harvest state.

- [ ] Test client planting.
  - Host accepts the plant action only if valid.
  - Host broadcasts authoritative crop/growth state.
  - Client local temporary state does not leave ghost produce.

- [ ] Test fast-forward and overnight growth.
  - Host fast-forward updates garden timers and produce.
  - Client fast-forward request does not create client-only produce.
  - Repair snapshot corrects any missed state.

- [ ] Test pending apply paths.
  - Actor update arrives before actor exists.
  - Garden produce state arrives before actor exists.
  - Resource attach arrives before actor/joint exists.
  - Queues apply before timeout under normal join/load timing.

- [ ] Review 10-second pending timeouts.
  - `GardenGrowthSyncManager`
  - `GardenResourceAttachSyncManager`
  - `LandPlotAmmoSyncManager`
  - `GardenPlotSyncManager`
  - `LandPlotFeederSyncManager`

### Acceptance Criteria

- [ ] No client-only garden produce survives after host repair.
- [ ] Repair logs clearly show whether gardens were corrected or already in sync.
- [ ] Long-session garden growth does not flood packets or degrade FPS.

## Priority 7: Run Focused Two-Player Sync QA

Goal: verify the known unstable areas in real gameplay and record findings before implementing more speculative fixes.

### Test Setup

- [ ] Host and client use the same SR2MP build.
- [ ] Enable useful packet/repair diagnostics.
- [ ] Start from a known save state.
- [ ] Record host log and client log.
- [ ] Note exact test time, map area, and involved actor/plot ids where possible.

### Test Matrix

- [ ] Host start and close.
- [ ] Client connect, initial sync, chat, disconnect, reconnect.
- [ ] Bad hostname / unused port / server closed during join / port already in use.
- [ ] Silos.
- [ ] Plort collectors.
- [ ] Refinery deposits.
- [ ] Fabricator spends.
- [ ] Gardens: plant, grow, harvest, destroy crop.
- [ ] Auto-feeders: speed, feeding timer, fast-forward feeding.
- [ ] Plort depositors.
- [ ] Puzzle slots/statues and related doors/gates.
- [ ] Gordo feed and burst.
- [ ] Map unlocks and fog reveal.
- [ ] Resource nodes: spawn, harvest, empty, respawn.
- [ ] Gadget placement.
- [ ] Gadget pickup/demolish.
- [ ] Actor spawn/destroy under load.
- [ ] Actor ownership transfer via vac.
- [ ] Weather update and lightning.
- [ ] Overnight/long-session FPS and packet volume.

### Findings Format

For each finding, record:

- Area:
- Host action:
- Client action:
- Expected:
- Actual:
- Repair snapshot corrected it: yes/no/unknown
- Relevant log lines:
- Repro steps:
- Suspected code path:
- Priority:

### Acceptance Criteria

- [ ] Every known unstable area has at least one host-driven and one client-driven test where applicable.
- [ ] Repair-log findings are added to `docs/NETWORK_REVIEW.md` or a new dated QA note.
- [ ] Next implementation pass is driven by observed failures, not guesses.
