using SR2MP.Packets.Actor;
using SR2MP.Shared.Utils;

namespace SR2MP.Shared.Utils;

/// <summary>
/// The actor ID carried in the packet must fall within the ID range
/// assigned to the sending client.
///
/// Used for: ActorSpawn — clients may only spawn actors whose IDs were
/// allocated to them by the host.
/// </summary>
public sealed class RangeOnlyRule : AuthorityRule
{
    public override AuthorityResult Check(PacketEnvelope env)
    {
        if (env.Packet is not IActorPacket actorPacket)
            return AuthorityResult.Allowed;

        var actorId = actorPacket.ActorId.Value;
        PlayerIdGenerator.GetClientActorIdRange(env.PlayerId, out var minId, out var maxId);

        return actorId >= minId && actorId < maxId
            ? AuthorityResult.Allowed
            : AuthorityResult.Reject($"actor id {actorId} is outside assigned range [{minId}, {maxId})");
    }
}
