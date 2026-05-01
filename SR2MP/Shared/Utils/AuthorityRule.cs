namespace SR2MP.Shared.Utils;

/// <summary>
/// Base class for all authority rules.  Each rule encodes one policy:
/// "who is allowed to send this kind of packet?".
/// Rules do NOT apply state changes — they only return Allow/Reject.
/// </summary>
public abstract class AuthorityRule
{
    /// <summary>
    /// Evaluate the rule against <paramref name="env"/>.
    /// Must be called on the main thread (game state may be read).
    /// </summary>
    public abstract AuthorityResult Check(PacketEnvelope env);

    /// <summary>
    /// Seconds between repeated rejection log entries for the same
    /// (player, packetType, reason) key.  0 = always log (default).
    /// Override to suppress log spam for high-frequency unreliable packets.
    /// </summary>
    public virtual float RejectionLogThrottleSeconds => 0f;
}
