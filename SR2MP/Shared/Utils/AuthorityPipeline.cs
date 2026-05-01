using SR2MP.Packets.Utils;

namespace SR2MP.Shared.Utils;

/// <summary>
/// Central authority-check registry.  One rule per <see cref="PacketType"/> is
/// evaluated before the handler's Apply logic runs.
///
/// Call <see cref="Register"/> during server startup (PacketManager.RegisterHandlers).
/// Call <see cref="Check"/> from <c>BasePacketHandler.CheckAuthority()</c>.
///
/// Rejection logging is centralised here with an optional per-key throttle,
/// replacing the ad-hoc per-handler log suppression that existed before.
/// </summary>
public sealed class AuthorityPipeline
{
    public static readonly AuthorityPipeline Instance = new();

    private readonly Dictionary<PacketType, AuthorityRule> _rules = new();

    // Throttle state keyed by "{playerId}|{packetType}|{reason}"
    private readonly Dictionary<string, ThrottleState> _throttle = new();
    private readonly object _throttleLock = new();

    private AuthorityPipeline() { }

    // ── Registration ──────────────────────────────────────────────────────

    /// <summary>Register a rule for <paramref name="type"/>. Replaces any previous rule.</summary>
    public void Register(PacketType type, AuthorityRule rule) => _rules[type] = rule;

    // ── Evaluation ────────────────────────────────────────────────────────

    /// <summary>
    /// Run the registered rule for <paramref name="env"/>.
    /// Returns <see cref="AuthorityResult.Allowed"/> if no rule is registered.
    /// Logs rejections with a unified format; throttles high-frequency spam.
    /// </summary>
    public AuthorityResult Check(PacketEnvelope env)
    {
        if (!_rules.TryGetValue(env.PacketType, out var rule))
            return AuthorityResult.Allowed;

        var result = rule.Check(env);

        if (!result.IsAllowed)
            LogRejection(env, result.RejectionReason ?? "no reason given", rule.RejectionLogThrottleSeconds);

        return result;
    }

    // ── Logging ───────────────────────────────────────────────────────────

    private void LogRejection(PacketEnvelope env, string reason, float throttleSeconds)
    {
        if (throttleSeconds <= 0f)
        {
            SrLogger.LogWarning(FormatRejection(env, reason), SrLogTarget.Both);
            return;
        }

        var key = $"{env.PlayerId}|{env.PacketType}|{reason}";
        var now = Time.realtimeSinceStartup;

        lock (_throttleLock)
        {
            if (!_throttle.TryGetValue(key, out var state))
            {
                _throttle[key] = new ThrottleState(now);
                SrLogger.LogWarning(FormatRejection(env, reason), SrLogTarget.Both);
                return;
            }

            if (now - state.LastLogAt < throttleSeconds)
            {
                state.Suppressed++;
                return;
            }

            var suffix = state.Suppressed > 0 ? $" ({state.Suppressed} similar suppressed)" : string.Empty;
            state.LastLogAt = now;
            state.Suppressed = 0;
            SrLogger.LogWarning(FormatRejection(env, reason) + suffix, SrLogTarget.Both);
        }
    }

    private static string FormatRejection(PacketEnvelope env, string reason)
        => $"[Authority] Rejected {env.PacketType} from {env.PlayerId} ({env.SenderEndPoint}): {reason}";

    private sealed class ThrottleState
    {
        public float LastLogAt { get; set; }
        public int Suppressed { get; set; }

        public ThrottleState(float now) { LastLogAt = now; }
    }
}
