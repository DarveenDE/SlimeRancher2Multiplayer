namespace SR2MP.Shared.Utils;

public static class NetworkProtocol
{
    public const int ProtocolVersion = 1;

    public static string ModVersion => global::BuildInfo.Version;
    public static string RequiredGameVersion => global::BuildInfo.ExactRequiredGameVersion;

    public static bool TryValidatePeer(
        string localLabel,
        string remoteLabel,
        int remoteProtocolVersion,
        string? remoteRequiredGameVersion,
        out string message)
    {
        if (remoteProtocolVersion <= 0)
        {
            message =
                $"{remoteLabel} is running an older SR2MP build without a protocol handshake. Install the same current SR2MP test DLL on both machines.";
            return false;
        }

        if (remoteProtocolVersion != ProtocolVersion)
        {
            message =
                $"SR2MP protocol mismatch: {localLabel} uses protocol {ProtocolVersion}, {remoteLabel} uses protocol {remoteProtocolVersion}. Install the same SR2MP test DLL on both machines.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(remoteRequiredGameVersion)
            && !string.Equals(remoteRequiredGameVersion, RequiredGameVersion, StringComparison.OrdinalIgnoreCase))
        {
            message =
                $"Slime Rancher 2 version target mismatch: {localLabel} targets {RequiredGameVersion}, {remoteLabel} targets {remoteRequiredGameVersion}. Use matching game and SR2MP builds.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
