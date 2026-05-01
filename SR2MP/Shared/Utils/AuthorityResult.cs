namespace SR2MP.Shared.Utils;

/// <summary>Result of an <see cref="AuthorityRule.Check"/> call.</summary>
public sealed class AuthorityResult
{
    public bool IsAllowed { get; private init; }

    /// <summary>Human-readable reason, set only when <see cref="IsAllowed"/> is false.</summary>
    public string? RejectionReason { get; private init; }

    public static readonly AuthorityResult Allowed = new() { IsAllowed = true };

    public static AuthorityResult Reject(string reason) =>
        new() { IsAllowed = false, RejectionReason = reason };
}
