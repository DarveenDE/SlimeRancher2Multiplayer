# Network Review Notes

Collected: 2026-04-29

These are follow-up notes from a source review of the current SR2MP networking code. Some targeted fixes have been implemented locally; unresolved items remain marked below.

## 2026-04-29 follow-up: observed desync examples

- Symptom: a slime toy bought by the host appeared as a different item for the client.
- Likely cause found: actor id allocation used the highest existing id as the next id instead of the next free id. That can cause a newly spawned actor to reuse an existing id, so later packets can apply to the wrong object.
- Local status: `NetworkActorManager.GetNextActorIdInRange(...)` now chooses the next free id, and both host and joining client setup use it.
- Extra hardening: live `ActorSpawnPacket` now sends the scene group id as an `int` instead of truncating it to a `byte`.

- Symptom: plort puzzle statues do not sync, so the other player still sees the related door/gate as closed.
- Cause found: SR2MP only synchronized `WorldSwitchModel` and `AccessDoorModel`. The game has separate `PuzzleSlotModel` and `PlortDepositorModel` dictionaries for these puzzles, and they had no packets/handlers.
- Local status: added initial and live sync for `PuzzleSlotModel.filled` and `PlortDepositorModel.AmountDeposited`.
- Remaining risk: some puzzle doors are driven by local animation/activation components, so this still needs live testing on the exact statue/door type.

## 2026-04-29 follow-up: netcode hardening pass

- Fixed: `ReliableOrdered` packets are no longer ACKed before the ordered sequence check. Newer out-of-order packets are deferred without ACK so the sender keeps retrying; old duplicates are ACKed and ignored.
- Fixed: the server now rejects non-connect, non-ACK packets from unknown endpoints before chunk merge/handler dispatch.
- Fixed: server-side player handlers now use the endpoint-bound `ClientInfo.PlayerId` instead of trusting packet-supplied player ids/names for player updates, joins, leaves, chat, player sound FX, and actor ownership transfer broadcasts.
- Fixed: `PlayerJoinHandler` now excludes the joining endpoint correctly when sending the system join chat message.
- Fixed: leaving players no longer generate a duplicate `BroadcastPlayerLeave`; `ClientManager.OnClientRemoved` already sends it.
- Hardened: client and server packet managers now reject packets whose chunk header type does not match the reconstructed payload type.
- Hardened: chunk merge now rejects invalid chunk counts, over-large chunks, inconsistent chunk metadata, over-large merged packets, and zip-bomb-style decompression growth.
- Fixed: outgoing `ReliableOrdered` sequence numbers are now tracked per destination and packet type. This prevents `SendToAllExcept` broadcasts, especially chat and leave packets, from creating permanent sequence gaps for clients that were intentionally excluded from earlier packets.
- Hardened: packet dedupe, incomplete chunk, and queued main-thread network actions are cleared on fresh client/server sessions and disconnect/close, reducing stale work after repeated test runs.
- Performance: `MainThreadDispatcher` now processes network handler work with a per-frame budget and logs backlog instead of draining an unlimited packet burst in one frame.
- Fixed: joining clients now go through an initial-sync barrier. The server tracks reliable initial snapshot packets, sends `InitialSyncComplete` only after they are ACKed, and queues reliable live broadcasts for that client until the client replies with `InitialSyncCompleteAck`.

## P1: ReliableOrdered packets can be ACKed and then dropped

- Files:
  - `SR2MP/Server/Managers/PacketManager.cs`
  - `SR2MP/Client/Managers/ClientPacketManager.cs`
  - `SR2MP/Shared/Managers/ReliabilityManager.cs`
- Problem: reliable ordered packets are acknowledged before the sequence check. If packet N+1 arrives before N, it can be ACKed and then dropped as out-of-order. Because the sender received an ACK, it will not resend N+1.
- Impact: normal UDP reordering can permanently lose ordered packets.
- Local status: fixed by only ACKing accepted ordered packets, while duplicate ordered packets are still ACKed to stop resends. This does not yet buffer out-of-order packets; it relies on the sender retrying them after the missing earlier packet arrives.

## P1: Server trusts packet sender identity

- Files:
  - `SR2MP/Server/Managers/PacketManager.cs`
  - `SR2MP/Server/Handlers/*`
- Problem: after chunk merge and deduplication, packets are dispatched without consistently checking that the endpoint belongs to a known/active client. Many handlers trust packet fields such as `PlayerId`.
- Impact: a client can potentially send updates or actions claiming another player id; unknown endpoints can reach handlers beyond initial connect.
- Local status: fixed for packet dispatch and the high-risk player identity handlers. Remaining follow-up: continue auditing world/actor handlers for game-state authority rules, because they can still ask the server to mutate shared world state after the sender is known.

## P1: Initial sync can interleave with live world packets

- File: `SR2MP/Server/Handlers/ConnectHandler.cs`
- Problem: the server adds the client before sending the initial snapshot, then sends many separate reliable packets without a clear sync barrier. Live actor/plot/world packets can reach the joining client during that window, and initial snapshot packets are not globally ordered as one transaction.
- Impact: clients can load a mixed state: part snapshot, part live updates.
- Local status: fixed with a joining/syncing state. Reliable live broadcasts are queued per syncing client and flushed after the client receives the initial snapshot and replies with `InitialSyncCompleteAck`; unreliable live updates are skipped during sync and resume naturally afterward.

## P2: Dead-client timeout

- Files:
  - `SR2MP/Client/Client.cs`
  - `SR2MP/Server/Server.cs`
  - `SR2MP/Server/Managers/ClientManager.cs`
- Problem: heartbeat sending and server timeout checks are currently disabled.
- Impact: if a player crashes or the tunnel dies, the server can keep stale client/player objects indefinitely.
- Possible direction: restore heartbeat/ack handling with a short timeout and clean leave broadcast on timeout.
- Local status: fixed with client heartbeat sends, server heartbeat ACKs, server-side dead-client removal, and client-side lost-host timeout handling.

## P2: Reliable send failure only logs and forgets

- File: `SR2MP/Shared/Managers/ReliabilityManager.cs`
- Problem: when a reliable packet exhausts retries, it is removed from pending packets and only logs a warning.
- Impact: state-changing reliable packets can silently desync sender and receiver.
- Possible direction: surface a failure event to client/server code. For critical packets, disconnect or trigger a resync instead of continuing silently.

## P2: Protocol/version handshake

- File: `SR2MP/Packets/Loading/ConnectPacket.cs`
- Problem: connect only carries player id and username.
- Impact: mismatched DLLs can deserialize invalid packet layouts or fail unclearly, especially during active development.
- Possible direction: include mod version, protocol version, required game version, and possibly feature flags in connect/ack. Reject incompatible clients with a readable UI message.
- Local status: fixed with a protocol/game-target handshake on `ConnectPacket` and `ConnectAckPacket`, plus a `ConnectRejectPacket` path for readable host-side rejection.

## Smaller follow-ups

- Add per-sender rate limiting for malformed packets if noisy tunnels or hostile endpoints become a problem.
- Add backpressure or per-frame limits to `MainThreadDispatcher` so network bursts cannot stall a frame indefinitely.
- Review `PlayerJoinHandler`: `SendToAllExcept(joinChatPacket, playerId)` passes a player id to a method that appears to expect an endpoint-info string.
- Consider central packet metadata so reliability, ordering, and sender rules are defined in one place instead of spread across packet classes and handlers.
- Bump `NetworkProtocol.ProtocolVersion` whenever packet layouts or initial-sync expectations become incompatible.
