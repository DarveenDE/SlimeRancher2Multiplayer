using SR2MP.Packets.Actor;

namespace SR2MP.Shared.Utils;

/// <summary>
/// The sending client must be the registered owner of the actor.
/// If the actor has no owner record (unknown or unregistered), the check passes —
/// the handler is responsible for rejecting unknown actors.
///
/// Used for: ActorUpdate, ActorUnload.
/// (For destroy see <see cref="SharedGadgetOrOwnerRule"/> which adds the gadget exception.)
/// </summary>
public sealed class OwnerOnlyRule : AuthorityRule
{
    // ActorUpdate is unreliable and high-frequency; throttle rejection spam.
    public override float RejectionLogThrottleSeconds { get; }

    public OwnerOnlyRule(float rejectionLogThrottleSeconds = 0f)
    {
        RejectionLogThrottleSeconds = rejectionLogThrottleSeconds;
    }

    public override AuthorityResult Check(PacketEnvelope env)
    {
        if (env.Packet is not IActorPacket actorPacket)
            return AuthorityResult.Allowed;

        var actorId = actorPacket.ActorId.Value;

        if (!actorManager.TryGetActorOwner(actorId, out var owner))
            return AuthorityResult.Allowed; // no owner record — let handler deal with it

        return owner == env.PlayerId
            ? AuthorityResult.Allowed
            : AuthorityResult.Reject($"actor {actorId} is owned by {owner}");
    }
}
