# SR2MP Sync Follow-up TODO

Last updated: 2026-05-01

This document is the working basis for the next multiplayer-sync pass. It is based on the current source review and a clean local build of `SR2MP.csproj`.

## Working Definitions

- **Host-authoritative shared state**: state that affects the world for everyone, such as land plots, refinery counts, gardens, resource nodes, gordos, switches, access doors, map unlocks, weather, world time, puzzle slots, plort depositors, and actor lifetime.
- **Per-player local state**: state that should remain personal to each player, such as the contents of each player's vacpack/inventory.
- **Inventory-backed world transaction**: an action that starts from a player's own inventory but changes shared world state, such as feeding a gordo, depositing plorts, storing items, selling items, planting a crop, or spending refinery resources.

Important clarification: **player inventories should not be globally equalized**. Each player should keep their own inventory. The open question is whether the host merely accepts the resulting world changes from clients, or whether the host also tracks/validates inventory-backed transactions.

## Next 4 Work Items

Current order for the next implementation pass:

### Freeze Investigation Update 2026-05-01

Observed logs from the 14:46 two-player test show a clear correlation between periodic full world repair and the client stalls:

- Server sent periodic full repair snapshots at roughly 15-second intervals with about 190-209 reliable packets per snapshot.
- The first repair snapshot was produced while the client was still in initial sync, then 214 queued reliable packets were flushed immediately after initial sync completed.
- Client log showed repeated repair corrections for refinery/garden/gordo state, 49 initial actor spawn failures from `SpawnResourceModel.NotifyParticipants`, and 1047 queued actor-update debug entries for actors that never existed locally.
- Startup log also showed a real Harmony failure in `CurrencyPatch.AddCurrency` because the SR2 method parameter is named `adjust`, not `amount`.

Mitigation implemented 2026-05-01:

- [x] Fixed `CurrencyPatch.AddCurrency` Harmony parameter from `amount` to `adjust`.
- [x] Periodic full repair is now opt-in via `periodic_full_world_repair` and defaults to off, so old saved `periodic_world_repair=true` values no longer keep the heavy repair loop alive.
- [x] If periodic full repair is explicitly enabled, it now waits 120 seconds between snapshots instead of 15 seconds.
- [x] Repair snapshots are skipped while any client is still in initial sync, preventing repair packets from being added to the initial-sync flush queue.
- [x] Verbose debug log spam is gated behind `verbose_sync_diagnostics`, default off. Summary sync diagnostics remain available via `sync_diagnostics`.
- [x] Repeated actor updates for an already pending missing actor now refresh the queued packet without extending the timeout or logging every packet.
- [x] Release build verified after the mitigation.
- [x] Follow-up 2026-05-01: rejected host-owned actor update spam was addressed by making client actor ownership wait for the server's accepted `ActorTransferPacket` broadcast before sending actor updates. Server-side actor-update rejection logging is now rate-limited and reports suppressed duplicate rejections.

Gadget placement update 2026-05-01:

- Logs from the 15:12-15:15 two-player test explain the failed refinery-link/connector placement in the client-to-host direction: the host rejected client `ActorSpawnPacket`s because the client generated actor ids `185890010` through `185890027`, just outside the assigned range `[185880000, 185890000)`.
- Implemented a shared client actor-id range helper so initial connect assignment and server validation use the same calculation.
- Increased the non-overlapping host/client actor-id ranges to 32,000 ids and moved client ranges away from the host range.
- The client now remembers its assigned actor-id range and resets the game's local actor-id provider again after the initial actor batch finishes, preventing initial-load spawn work from drifting the provider out of range.
- Client local actor/gadget spawn patches now log and suppress network sends if a future local spawn still falls outside the assigned range.
- Fixed the remaining startup Harmony failure in `CurrencyPatch.SpendCurrency` by using the SR2 method parameter name `adjust`.
- Release build verified after this fix and copied to the host/client `Mods/SR2MP.dll` paths at 15:20. Next retest should check live gadget placement from host and client again; if host-to-client placement is still missing, the next focus is gadget instantiation/visibility on the receiving client rather than actor-id authority.

Join cleanup update 2026-05-01:

- Follow-up test showed newly placed gadgets are now visible cross-client, but a previously client-local refinery connector from the failed actor-id test could remain visible on the joining client even though the host did not have it.
- Logs showed the joining client queued `ActorDestroyPacket`s during initial sync cleanup, and the host rejected them for actor `10130` as host-owned. This confirmed local cleanup was leaking network destroy events while the host snapshot was being applied.
- Implemented initial-actor cleanup under the packet echo guard and marked initial actor load before local model removal starts.
- Gadget cleanup now explicitly destroys leftover local gadget `GameObject`s before spawning the host snapshot, so orphaned client-only gadgets from earlier failed sessions should disappear on join.
- `OnActorDestroy` and `OnGadgetDestroyed` now also suppress network sends while initial actor load is active.
- Release build verified after this cleanup fix. Host and client `Mods/SR2MP.dll` were both replaced with the 15:34 build after the client game was closed.

Gadget destroy authority update 2026-05-01:

- Follow-up test showed host-initiated refinery connector removal syncs to the client, but client-initiated removal left the connector on the host.
- Logs confirmed the host rejected client `ActorDestroyPacket`s for connector actors such as `13434` because they were owned by `HOST`.
- Policy decision: player-placeable gadgets are shared world objects. Any connected client may request gadget removal from the host; normal non-gadget actors still require actor ownership.
- Implemented server-side gadget destroy acceptance before the generic actor ownership check, with explicit acceptance logging.
- Fixed `NetworkActorManager.TakeOwnershipOfNearby()` to iterate over a snapshot of actor entries, avoiding the observed `Collection was modified` coroutine crash during/after initial actor sync.
- Release build verified and copied to both host/client `Mods/SR2MP.dll` paths at 15:43.

Current gadget lifecycle matrix:

- Host places gadget: host creates local gadget, broadcasts `ActorSpawn`, client spawns it.
- Client places gadget: client creates local gadget, host validates actor id/type/scene, host creates it, other peers receive host-accepted `ActorSpawn`.
- Host removes gadget: host removes local gadget, broadcasts `ActorDestroy`, client removes model and GameObject.
- Client removes gadget: client removes local gadget, host now accepts gadget `ActorDestroy` even when owner is `HOST`, removes it, and broadcasts to other peers.
- Client joins with stale local-only gadget: initial actor cleanup removes local models/GameObjects under packet echo guard, then applies host snapshot.
- Reconnect after gadget changes: host snapshot remains the source of truth; orphaned local gadgets should be cleared before snapshot spawn.
- Still open: add a focused gadget repair snapshot if missed live packets can leave duplicate/missing gadgets without reconnect.

Puzzle slot / plort statue update 2026-05-01:

- The plort-statue bug is not covered by actor/gadget sync; it uses the dedicated `PuzzleSlotState` and `PlortDepositorState` packets.
- Current code already had live sync hooks, initial join sync, and repair snapshot coverage for puzzle slots and plort depositors, so the area was partially covered before this pass.
- Risk found: failed local ID resolution was silent, so a client could fill a local puzzle slot without any useful log if the model could not be mapped back to a host-known slot/depositor id.
- Risk found: the host accepted client slot/depositor states too loosely, including unknown ids and stale/decreasing states.
- Implemented diagnostics when a local puzzle slot or plort depositor changes but no network id can be resolved.
- Implemented host-side rejection for unknown puzzle slot/depositor ids, clearing already filled slots, and decreasing plort depositor amounts; stale clients are corrected back to host state.
- `PuzzleStateSyncManager` now uses the packet echo guard when applying visible slot/depositor changes and logs when live updates only reach the model because the visible target GameObject is missing.
- Release build verified after this hardening and copied to both host/client `Mods/SR2MP.dll` paths at 15:50.

Current status: ready for a targeted retest with periodic full repair left off. If the freeze remains, the next likely focus is the 50 failed initial actor spawns / missing actor ids, especially `SpawnResourceModel.NotifyParticipants` and the repeated actor updates for non-existent actor ids.

1. Harden shared currency changes. **Status: done 2026-05-01.**
   - Decide and document currency as shared host-authoritative save state for the current architecture.
   - Stop accepting arbitrary client absolute `NewAmount` as truth.
   - Require client deltas to match the host's current currency baseline before the host applies and rebroadcasts them.

2. Finish player-upgrade cost/requirement authority. **Status: scoped 2026-05-01; implementation depends on a correlated purchase transaction.**
   - Use the currency decision above as the baseline.
   - Keep live upgrade sync as absolute host target levels.
   - Add the best available host-side validation for upgrade costs/requirements, or document why full validation must wait for inventory-backed transaction authority.

3. Add repair snapshots for shared progression/economy. **Status: done 2026-05-01 for currency, pedia, and player upgrades.**
   - Currency repair snapshot.
   - Pedia repair snapshot.
   - Player upgrade repair snapshot.
   - Include these categories in `WorldStateRepairManager` logging.
   - Diagnostic note 2026-05-01: `WorldStateRepairManager` now logs each repair snapshot with reason, host-side duration, estimated packet count, reliable backlog before/after, per-category counts, and per-category timings.
   - Runtime switch: periodic full repair is now opt-in via `periodic_full_world_repair`; manual and failure-triggered repair requests remain available.

4. Add explicit rejection logging for the remaining invalid business mutations. **Status: broad first pass done 2026-05-01.**
   - Prioritize currency, land plot, refinery/ammo/feeder, world switches/doors, gordos, resource nodes, puzzle slots, and plort depositors.
   - Include player id, endpoint, packet type, reason, and relevant ids where available.

## Priority 1: Make Host Authority Explicit

Goal: client packets should be treated as requests for shared-state changes, not as unconditional truth.

Status 2026-05-01:

- Implemented a first code-level client-to-server packet authority gate in `Shared/Utils/PacketAuthority.cs`.
- `Server/Managers/PacketManager.cs` now rejects server-only, reserved, and unknown packet types from clients before chunk merge, deduplication, or handler dispatch.
- Hardened pedia unlocks and player upgrades so duplicate/maxed/unknown requests are not blindly applied or rebroadcast.
- Hardened client-origin actor spawn so the host only rebroadcasts successfully applied spawns from the sender's assigned actor-id range.
- Added actor ownership tracking in `NetworkActorManager.ActorOwners` and basic owner validation for actor update, destroy, transfer, and unload requests.
- Expanded `Shared/Utils/PacketAuthority.cs` from a client-allowed set into a packet metadata matrix with direction, reliability profile, host action, initial sync coverage, and repair snapshot coverage.
- Decided long-term pedia policy: pedia unlocks remain shared world progression for this co-op model. Clients may request a pedia unlock, but the host validates the entry and only rebroadcasts host-accepted new unlocks.
- Decided current player-upgrade policy: upgrades remain shared save/player progression in the current architecture. `PlayerUpgradePacket` now carries the host-accepted target level so receivers set absolute host state instead of blindly incrementing.
- Decided current currency policy: currency remains shared host-authoritative save state. Client currency packets now include a delta and expected previous amount; the host rejects stale/malformed updates instead of accepting arbitrary absolute `NewAmount`.
- Added repair snapshot coverage for shared currency, pedia entries, and player upgrade levels.
- Added rejection logging with player/endpoint context for currency, land plot, garden plant, access door, world switch, gordo feed/burst, puzzle slot, plort depositor, refinery, land plot ammo, and feeder updates.
- Added sync diagnostics for freeze investigation: repair snapshot burst summaries, delayed reliable ACK warnings, first-resend warnings, and heartbeat-gap warnings.
- This is still an ingress and basic business-rule hardening step. Deeper per-action validation remains open below.

### Tasks

- [x] Create a first central packet authority matrix for client-to-server ingress.
  - Packet type
  - Whether clients may send it to the host
  - Current implementation: `PacketAuthority.GetClientToServerRule(...)`

- [x] Expand the authority matrix with full metadata.
  - Direction
  - Reliability
  - Whether the host applies the packet directly, validates it, ignores it, or converts it into a host-generated packet
  - Whether the state is covered by initial sync
  - Whether the state is covered by repair snapshots
  - Current implementation: `PacketAuthorityDefinition` entries in `Shared/Utils/PacketAuthority.cs`

- [x] Harden client-triggered pedia unlocks against empty, unknown, duplicate, and host-rejected requests.
  - Current code path: `Patches/Pedia/OnEntryUnlocked.cs`, `Server/Handlers/PediaUnlockHandler.cs`
  - Current policy: treated as shared progression because initial sync already applies host pedia state to clients.
  - Implemented: server only rebroadcasts if the host actually unlocked a new pedia entry.

- [x] Decide long-term pedia policy.
  - Decide whether pedia unlocks are shared world progression or per-player progression.
  - If shared: host should preferably derive the unlock from host-side state where possible.
  - If per-player: stop rebroadcasting pedia unlocks globally.
  - Decision 2026-05-01: shared world progression.
  - Current enforcement: clients send unlock requests; the host validates known pedia ids, rejects duplicates/unknown ids, applies the unlock to host pedia state, and only then rebroadcasts.
  - Remaining follow-up: add a pedia repair snapshot under Priority 2 so missed reliable unlock packets can be corrected without reconnecting.

- [x] Harden client-triggered player upgrades against unknown, maxed, and host-rejected requests.
  - Current code path: `Patches/Player/OnPlayerUpgraded.cs`, `Server/Handlers/PlayerUpgradeHandler.cs`
  - Current policy: treated as shared progression because initial sync already applies host upgrade state to clients.
  - Implemented: server validates upgrade id, current level, next level existence, and `IncrementUpgradeLevel(...)` result before rebroadcast.

- [ ] Finish player-upgrade authority policy.
  - [x] Decide whether upgrades are shared or per-player long term.
  - Decision 2026-05-01: shared save/player progression for the current architecture, matching initial sync behavior.
  - [x] Change live packets from "increment this upgrade" to "host says this upgrade is now level N".
  - Implemented: `PlayerUpgradePacket.TargetLevel`; clients send their requested target after a successful local purchase, the host accepts only the next valid level, and receivers set the absolute host level.
  - [ ] Validate requirements/costs before accepting, or move upgrade purchase authority entirely to the host.
  - Status 2026-05-01: direct cost validation is not safe yet because currency spend and upgrade-level request are separate reliable packets and can arrive in either order. Full validation should be implemented as a single host-authoritative purchase transaction or with a correlated pending-spend ledger.
  - Remaining dependency: inventory-backed transaction authority.

- [x] Harden currency changes.
  - Current code path: `Patches/Economy/CurrencyPatch.cs`, `Server/Handlers/CurrencyHandler.cs`
  - Decision 2026-05-01: shared/global in the current architecture because initial sync already applies host currency to joining clients and upgrade/fabricator economy is shared save state.
  - Implemented: client packets now include `PreviousAmount` and `DeltaAmount`; the host applies the delta only when `PreviousAmount` matches the current host amount and the resulting amount is valid.
  - Implemented: stale, malformed, legacy-without-baseline, negative-result, and invalid-currency updates are rejected and the sender receives the host amount for correction.
  - Remaining follow-up: replace trusted client deltas with validated host-derived economy transactions where possible.
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
  - Status 2026-05-01: currency rejections now include endpoint/player context plus reason for invalid type, missing baseline, stale baseline, malformed delta, or negative result.
  - Status 2026-05-01: land plot, garden plant, access door, world switch, gordo feed/burst, puzzle slot, plort depositor, refinery, land plot ammo, and feeder apply failures now log endpoint/player context and relevant ids.
  - Remaining: resource node handling still applies snapshot batches without per-entry rejection results, so deeper resource-node rejection reasons require changes in `ResourceNodeSyncManager.Apply(...)`.
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

- [x] Currency repair snapshot.
  - Include both regular and rainbow currency.
  - Consider sending as absolute host amount, like current `CurrencyPacket`.
  - Implemented: repair sends host currency amounts as absolute `CurrencyPacket`s with zero delta/baseline.

- [x] Pedia repair snapshot.
  - Initial sync already sends pedia entries.
  - Periodic repair currently does not.
  - Policy 2026-05-01: pedia is shared/global, so this should be implemented.
  - Implemented: repair reuses `InitialPediaPacket` with host unlocked pedia entries.

- [x] Player upgrade repair snapshot.
  - Initial sync already sends upgrade levels.
  - Periodic repair currently does not.
  - Policy 2026-05-01: upgrades are shared/global in the current architecture, so this should be implemented after live authority hardening.
  - Implemented: repair reuses `InitialUpgradesPacket` with absolute host upgrade levels.

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
