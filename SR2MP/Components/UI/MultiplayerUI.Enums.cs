namespace SR2MP.Components.UI;

public sealed partial class MultiplayerUI
{
    public enum MenuState : byte
    {
        Hidden,
        DisconnectedMainMenu,
        DisconnectedInGame,
        ConnectedClient,
        ConnectedHost,
        SettingsInitial,
        SettingsMain,
        SettingsHelp,
        Kicked,
        Error,
    }

    public enum ErrorType : byte
    {
        None,
        UnknownError,
        InvalidIP,
        IPNotFound,
    }

    public enum HelpTopic : byte
    {
        Root,
        PlayIt,
        SyncState,
        DiscordSupport,
    }

    private enum MultiplayerTab : byte
    {
        Join,
        Host,
        Players,
        Settings,
    }

    private enum ConnectionPhase : byte
    {
        Idle,
        ResolvingAddress,
        Connecting,
        Synchronizing,
        Connected,
        StartingHost,
        Hosting,
        Stopping,
        Failed,
    }
}
