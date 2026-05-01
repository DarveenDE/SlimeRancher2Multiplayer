using Il2CppMonomiPark.SlimeRancher.Persist;

namespace SR2MP.Shared.Utils;

public enum PendingMultiplayerLaunchType : byte
{
    None,
    Host,
    Join,
}

public enum MultiplayerLaunchState : byte
{
    Idle,
    WaitingForSaveSelection,
    WaitingForSettings,
    LoadingSave,
    WaitingForGameContext,
    StartingHost,
    ResolvingAddress,
    Connecting,
    Synchronizing,
    ActiveSession,
    VersionMismatch,
    Failed,
    Cancelled,
}

public sealed class PendingMultiplayerLaunch
{
    public PendingMultiplayerLaunchType Type { get; init; }
    public string? GameName { get; init; }
    public string? SaveName { get; init; }
    public ushort HostPort { get; init; }
    public ServerAddress? JoinAddress { get; init; }
    public ServerLaunchSettings Settings { get; init; } = ServerLaunchSettings.FromPreferences();
}

public sealed class ServerLaunchSettings
{
    public bool AllowCheats { get; init; }
    public bool? TarrEnabled { get; init; }
    public bool? FeralEnabled { get; init; }
    public int? MaxPlayers { get; init; }
    public string? Password { get; init; }
    public bool ListedInBrowser { get; init; }

    public static ServerLaunchSettings FromPreferences()
        => new()
        {
            AllowCheats = Main.AllowCheats,
        };
}

public static class MultiplayerLaunchCoordinator
{
    private static PendingMultiplayerLaunch? pendingLaunch;
    private static ServerLaunchSettings? hostSaveSelectionSettings;
    private static ushort hostSaveSelectionPort;
    private static bool hostSaveSelectionArmed;
    private static bool executingPendingLaunch;

    public static PendingMultiplayerLaunch? PendingLaunch => pendingLaunch;
    public static bool IsHostSaveSelectionArmed => hostSaveSelectionArmed;
    public static MultiplayerLaunchState State { get; private set; } = MultiplayerLaunchState.Idle;
    public static string StatusMessage { get; private set; } = string.Empty;

    public static event Action<MultiplayerLaunchState, string>? StateChanged;

    public static void PrepareHost(
        string? gameName,
        string? saveName,
        ushort hostPort,
        ServerLaunchSettings? settings = null)
    {
        hostSaveSelectionArmed = false;
        hostSaveSelectionPort = 0;
        hostSaveSelectionSettings = null;
        pendingLaunch = new PendingMultiplayerLaunch
        {
            Type = PendingMultiplayerLaunchType.Host,
            GameName = gameName,
            SaveName = saveName,
            HostPort = hostPort,
            Settings = settings ?? ServerLaunchSettings.FromPreferences(),
        };
        executingPendingLaunch = false;
        SetState(MultiplayerLaunchState.WaitingForGameContext, "Host will start after the selected save finishes loading.");
    }

    public static bool BeginHostSaveSelection(ushort hostPort, ServerLaunchSettings? settings = null)
    {
        if (hostPort == 0)
        {
            Fail("Invalid host port.");
            return false;
        }

        pendingLaunch = null;
        executingPendingLaunch = false;
        hostSaveSelectionArmed = true;
        hostSaveSelectionPort = hostPort;
        hostSaveSelectionSettings = settings ?? ServerLaunchSettings.FromPreferences();
        SetState(MultiplayerLaunchState.WaitingForSaveSelection, "Select a save in the normal Load Game menu to host it.");
        return true;
    }

    public static bool TryPrepareHostFromSelectedSave(Summary? summary)
    {
        if (!hostSaveSelectionArmed)
            return false;

        if (summary == null || summary.IsInvalid)
        {
            Fail("Selected save is invalid and cannot be hosted.");
            return false;
        }

        var identifier = summary.SaveIdentifier;
        string? gameName = identifier?.GameName ?? summary.Name;
        string? saveName = identifier?.SaveName ?? summary.SaveName;
        PrepareHost(gameName, saveName, hostSaveSelectionPort, hostSaveSelectionSettings);
        SetState(
            MultiplayerLaunchState.LoadingSave,
            $"Loading {GetSaveDisplayName(summary)}. Host will start after the save is ready.");
        return true;
    }

    public static void PrepareJoin(
        string? gameName,
        string? saveName,
        ServerAddress joinAddress,
        ServerLaunchSettings? settings = null)
    {
        pendingLaunch = new PendingMultiplayerLaunch
        {
            Type = PendingMultiplayerLaunchType.Join,
            GameName = gameName,
            SaveName = saveName,
            JoinAddress = joinAddress,
            Settings = settings ?? ServerLaunchSettings.FromPreferences(),
        };
        executingPendingLaunch = false;
        SetState(MultiplayerLaunchState.WaitingForGameContext, "Join will start after the selected client save finishes loading.");
    }

    public static void Clear()
    {
        pendingLaunch = null;
        hostSaveSelectionArmed = false;
        hostSaveSelectionPort = 0;
        hostSaveSelectionSettings = null;
        executingPendingLaunch = false;
        SetState(MultiplayerLaunchState.Idle, string.Empty);
    }

    public static void Cancel(string message)
    {
        pendingLaunch = null;
        hostSaveSelectionArmed = false;
        hostSaveSelectionPort = 0;
        hostSaveSelectionSettings = null;
        executingPendingLaunch = false;
        SetState(MultiplayerLaunchState.Cancelled, message);
    }

    public static void TryExecutePendingLaunch(GameContext gameContext)
    {
        _ = gameContext;

        if (pendingLaunch is null || executingPendingLaunch)
            return;

        if (!SceneContext.Instance)
        {
            SetState(MultiplayerLaunchState.WaitingForGameContext, "Waiting for the save to finish loading.");
            return;
        }

        executingPendingLaunch = true;

        switch (pendingLaunch.Type)
        {
            case PendingMultiplayerLaunchType.Host:
                ExecuteHostLaunch(pendingLaunch);
                break;
            case PendingMultiplayerLaunchType.Join:
                ExecuteJoinLaunch(pendingLaunch);
                break;
            default:
                Clear();
                break;
        }
    }

    private static void ExecuteHostLaunch(PendingMultiplayerLaunch launch)
    {
        if (launch.HostPort == 0)
        {
            Fail("Invalid host port.");
            return;
        }

        SetState(MultiplayerLaunchState.StartingHost, $"Starting host on port {launch.HostPort}...");
        bool started = Main.Server.Start(launch.HostPort, true);
        if (!started)
        {
            Fail(Main.Server.IsRunning()
                ? "Server is already running."
                : "Could not start host from the loaded save.");
            return;
        }

        pendingLaunch = null;
        SetState(MultiplayerLaunchState.ActiveSession, $"Hosting on port {launch.HostPort}.");
    }

    private static void ExecuteJoinLaunch(PendingMultiplayerLaunch launch)
    {
        if (launch.JoinAddress is not { } address)
        {
            Fail("Missing server address.");
            return;
        }

        SetState(MultiplayerLaunchState.ResolvingAddress, $"Checking {address.Display}...");
        if (!ServerAddressParser.TryResolve(address, out var resolvedAddress, out var resolveError))
        {
            Fail(resolveError);
            return;
        }

        SetState(MultiplayerLaunchState.Connecting, $"Connecting to {address.Display}...");
        bool started = Main.Client.Connect(resolvedAddress.Host, resolvedAddress.Port);
        if (!started)
        {
            Fail(Main.Client.LastConnectionError);
            return;
        }

        Main.SetConfigValue("recent_ip", address.Host);
        Main.SetConfigValue("recent_port", address.Port.ToString());
        RecentServerService.Remember(address);

        pendingLaunch = null;
        SetState(MultiplayerLaunchState.Synchronizing, $"Waiting for world sync from {address.Display}...");
    }

    private static void Fail(string? message)
    {
        executingPendingLaunch = false;
        SetState(ClassifyFailure(message), string.IsNullOrWhiteSpace(message) ? "Multiplayer launch failed." : message.Trim());
    }

    private static MultiplayerLaunchState ClassifyFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return MultiplayerLaunchState.Failed;

        return message.Contains("protocol", StringComparison.OrdinalIgnoreCase)
            || message.Contains("version target mismatch", StringComparison.OrdinalIgnoreCase)
            || message.Contains("same current SR2MP test DLL", StringComparison.OrdinalIgnoreCase)
            ? MultiplayerLaunchState.VersionMismatch
            : MultiplayerLaunchState.Failed;
    }

    private static string GetSaveDisplayName(Summary summary)
    {
        if (!string.IsNullOrWhiteSpace(summary.DisplayName))
            return summary.DisplayName;

        if (!string.IsNullOrWhiteSpace(summary.Name))
            return summary.Name;

        return "selected save";
    }

    private static void SetState(MultiplayerLaunchState state, string message)
    {
        State = state;
        StatusMessage = message;
        StateChanged?.Invoke(state, message);
    }
}
