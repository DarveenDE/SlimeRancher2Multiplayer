using SR2E;

namespace SR2MP.Components.UI;

public sealed partial class MultiplayerUI
{
    private bool viewingSettings = false;
    private bool firstTime = true;
    private bool viewingHelp = false;
    private readonly object pendingStatusUpdatesLock = new();
    private readonly Queue<Action> pendingStatusUpdates = new();

    public MenuState state = MenuState.Hidden;
    private bool chatShown = false;
    private MenuState previousState = MenuState.Hidden;

    private static bool GetIsLoading()
    {
        return SystemContext.Instance.SceneLoader.CurrentSceneGroup.name is "StandaloneStart" or "CompanyLogo" or "LoadScene";
    }

    private MenuState GetState()
    {
        if (multiplayerUIHidden) return MenuState.Hidden;

        var inGame = ContextShortcuts.inGame;
        var loading = GetIsLoading();
        var connected = Main.Client.IsConnected;
        var hosting = Main.Server.IsRunning();

        if (loading) return MenuState.Hidden;
        if (firstTime) return MenuState.SettingsInitial;
        if (viewingSettings) return MenuState.SettingsMain;
        if (viewingHelp) return MenuState.SettingsHelp;
        if (connected) return MenuState.ConnectedClient;
        if (hosting) return MenuState.ConnectedHost;

        return inGame ? MenuState.DisconnectedInGame : MenuState.DisconnectedMainMenu;
    }

    private void UpdateChatVisibility()
    {
        bool isInGame = state is MenuState.DisconnectedInGame or MenuState.ConnectedClient or MenuState.ConnectedHost;

        bool isMainMenu = state == MenuState.DisconnectedMainMenu;

        if (isMainMenu)
        {
            chatHidden = true;
            chatShown = false;
            internalChatToggle = false;
            return;
        }

        if (internalChatToggle) return;

        if (isInGame && !chatShown)
        {
            chatHidden = false;
            chatShown = true;

            if (previousState == MenuState.DisconnectedMainMenu || previousState == MenuState.Hidden)
            {
                ClearAndWelcome();
            }
        }

        previousState = state;
    }

    private void EnqueueStatusUpdate(Action action)
    {
        lock (pendingStatusUpdatesLock)
        {
            pendingStatusUpdates.Enqueue(action);
        }
    }

    private void ProcessPendingStatusUpdates()
    {
        while (true)
        {
            Action? action;
            lock (pendingStatusUpdatesLock)
            {
                if (pendingStatusUpdates.Count == 0)
                    return;

                action = pendingStatusUpdates.Dequeue();
            }

            action?.Invoke();
        }
    }

    private void HandleClientConnectionStarted(string address, int port)
    {
        EnqueueStatusUpdate(() =>
        {
            if (connectionPhase is ConnectionPhase.Idle or ConnectionPhase.ResolvingAddress or ConnectionPhase.Failed)
                SetConnectionStatus(ConnectionPhase.Connecting, $"Connecting to {address}:{port}...");
        });
    }

    private void HandleClientConnected(string playerId)
    {
        EnqueueStatusUpdate(() =>
            SetConnectionStatus(ConnectionPhase.Connected, "Joined hosted world."));
    }

    private void HandleClientConnectionFailed(string message)
    {
        EnqueueStatusUpdate(() =>
            SetConnectionStatus(ConnectionPhase.Failed, message));
    }

    private void HandleClientDisconnected()
    {
        EnqueueStatusUpdate(() =>
        {
            if (connectionPhase != ConnectionPhase.Failed)
                SetConnectionStatus(ConnectionPhase.Idle, "Disconnected from world.");
        });
    }

    private void HandleServerStarted()
    {
        EnqueueStatusUpdate(() =>
            SetConnectionStatus(ConnectionPhase.Hosting, $"Hosting on port {Main.Server.Port}."));
    }

    private void HandleServerStartFailed(string message)
    {
        EnqueueStatusUpdate(() =>
            SetConnectionStatus(ConnectionPhase.Failed, message));
    }

    private void HandleServerStopped()
    {
        EnqueueStatusUpdate(() =>
        {
            if (connectionPhase != ConnectionPhase.Failed)
                SetConnectionStatus(ConnectionPhase.Idle, "Hosted world closed.");
        });
    }
}
