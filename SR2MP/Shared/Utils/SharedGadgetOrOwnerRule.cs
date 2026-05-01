using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.Actor;

namespace SR2MP.Shared.Utils;

/// <summary>
/// Gadget actors are shared — any connected client may destroy them regardless of ownership.
/// For non-gadget actors the standard owner-only rule applies.
///
/// Used for: ActorDestroy.
/// </summary>
public sealed class SharedGadgetOrOwnerRule : AuthorityRule
{
    public override AuthorityResult Check(PacketEnvelope env)
    {
        if (env.Packet is not IActorPacket actorPacket)
            return AuthorityResult.Allowed;

        var actorId = actorPacket.ActorId.Value;

        // Actor may not be registered yet — let the handler deal with missing actors.
        if (!actorManager.Actors.TryGetValue(actorId, out var actor))
            return AuthorityResult.Allowed;

        // Gadgets are shared: any client may destroy them.
        if (actor.TryCast<GadgetModel>() != null)
            return AuthorityResult.Allowed;

        // Regular actor: the sender must be the registered owner.
        if (!actorManager.TryGetActorOwner(actorId, out var owner))
            return AuthorityResult.Allowed; // no owner record — let handler handle

        return owner == env.PlayerId
            ? AuthorityResult.Allowed
            : AuthorityResult.Reject($"actor {actorId} is owned by {owner}");
    }
}
