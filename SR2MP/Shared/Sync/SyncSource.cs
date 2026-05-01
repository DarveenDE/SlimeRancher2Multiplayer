namespace SR2MP.Shared.Sync;

/// <summary>
/// Describes the origin of a snapshot apply call so that subsystems can tailor
/// their behaviour (logging detail, animation suppression, etc.) without
/// inspecting arbitrary source strings.
/// </summary>
public enum SyncSource
{
    /// <summary>First bulk snapshot sent by the host when a client joins.</summary>
    Initial,

    /// <summary>Single live-event packet received during normal gameplay.</summary>
    Live,

    /// <summary>Periodic or triggered repair snapshot that corrects drift.</summary>
    Repair,

    /// <summary>Re-run of initial sync after a disconnect/reconnect cycle.</summary>
    Reconnect,
}

public static class SyncSourceExtensions
{
    /// <summary>
    /// Returns the legacy source string that the existing sync managers expect.
    /// Keeps string-matching logic in one place while the migration is in progress.
    /// </summary>
    public static string ToSourceString(this SyncSource source) => source switch
    {
        SyncSource.Initial    => "initial snapshot",
        SyncSource.Repair     => "repair snapshot",
        SyncSource.Reconnect  => "reconnect snapshot",
        SyncSource.Live       => "live",
        _                     => "unknown",
    };

    public static bool IsSilent(this SyncSource source)
        => source is SyncSource.Initial or SyncSource.Repair or SyncSource.Reconnect;
}
