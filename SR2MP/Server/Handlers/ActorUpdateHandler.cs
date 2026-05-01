using System.Net;
using SR2MP.Packets.Actor;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ActorUpdate)]
public sealed class ActorUpdateHandler : BasePacketHandler<ActorUpdatePacket>
{
    private const float AuthorityRejectionLogIntervalSeconds = 5f;
    private static readonly Dictionary<string, RejectionLogState> AuthorityRejectionLogs = new();

    public ActorUpdateHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ActorUpdatePacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (actorManager.TryGetActorOwner(packet.ActorId.Value, out var owner)
            && owner != client.PlayerId)
        {
            LogAuthorityRejection(client.PlayerId, clientEp, packet.ActorId.Value, owner);
            return;
        }

        ActorUpdateSyncManager.ApplyOrQueue(
            packet,
            "server actor update",
            appliedPacket => Main.Server.SendToAllExcept(appliedPacket, clientEp));
    }

    private static void LogAuthorityRejection(string playerId, IPEndPoint clientEp, long actorId, string? owner)
    {
        owner ??= "unknown";
        var key = $"{playerId}|{actorId}|{owner}";
        var now = Time.realtimeSinceStartup;

        if (!AuthorityRejectionLogs.TryGetValue(key, out var state))
        {
            AuthorityRejectionLogs[key] = new RejectionLogState(now);
            SrLogger.LogWarning(
                $"Rejected actor update from {playerId} ({clientEp}); actor {actorId} is owned by {owner}.",
                SrLogTarget.Both);
            return;
        }

        if (now - state.LastLogAt < AuthorityRejectionLogIntervalSeconds)
        {
            state.Suppressed++;
            return;
        }

        var suffix = state.Suppressed > 0
            ? $" Suppressed {state.Suppressed} similar rejection(s)."
            : string.Empty;

        state.LastLogAt = now;
        state.Suppressed = 0;

        SrLogger.LogWarning(
            $"Rejected actor update from {playerId} ({clientEp}); actor {actorId} is owned by {owner}.{suffix}",
            SrLogTarget.Both);
    }

    private sealed class RejectionLogState
    {
        public RejectionLogState(float lastLogAt)
        {
            LastLogAt = lastLogAt;
        }

        public float LastLogAt { get; set; }
        public int Suppressed { get; set; }
    }
}
