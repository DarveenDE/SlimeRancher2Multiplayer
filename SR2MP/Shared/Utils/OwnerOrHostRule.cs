using SR2MP.Packets.Actor;

namespace SR2MP.Shared.Utils;

/// <summary>
/// An actor transfer is allowed only if the current registered owner is either
/// the host itself (<see cref="LocalID"/> == "HOST") or the requesting client.
/// This permits a client to claim an actor that the host holds, or to re-claim
/// one it already owns.
///
/// Used for: ActorTransfer.
/// </summary>
public sealed class OwnerOrHostRule : AuthorityRule
{
    public override AuthorityResult Check(PacketEnvelope env)
    {
        if (env.Packet is not IActorPacket actorPacket)
            return AuthorityResult.Allowed;

        var actorId = actorPacket.ActorId.Value;

        if (!actorManager.TryGetActorOwner(actorId, out var currentOwner))
            return AuthorityResult.Allowed; // no owner — let handler validate existence

        if (currentOwner == LocalID || currentOwner == env.PlayerId)
            return AuthorityResult.Allowed;

        return AuthorityResult.Reject($"actor {actorId} is owned by {currentOwner}");
    }
}
