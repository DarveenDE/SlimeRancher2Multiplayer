using MelonLoader;
using SR2E.Utils;

namespace SR2MP.Components.UI;

// TODO: Asset bundle
[RegisterTypeInIl2Cpp(false)]
public sealed partial class MultiplayerUI : MonoBehaviour
{
    public static MultiplayerUI Instance { get; private set; }

    private bool didUnfocus = false;
    private int openHubDiagnosticFrames;

    private void Awake()
    {
        firstTime = Main.SetupUI;
        multiplayerUIHidden = !firstTime;
        usernameInput = Main.Username;
        allowCheatsInput = Main.AllowCheats;
        ipInput = Main.SavedConnectIP;
        hostPortInput = Main.SavedHostPort;
        portInput = string.IsNullOrWhiteSpace(Main.SavedConnectPort)
            ? hostPortInput
            : Main.SavedConnectPort;
        LoadRecentServers();

        if (Instance)
        {
            SrLogger.LogError("Tried to create instance of MultiplayerUI, but it already exists!", SrLogTarget.Both);
            Destroy(this);
            return;
        }

        Instance = this;

        Main.Client.OnConnectionStarted += HandleClientConnectionStarted;
        Main.Client.OnConnected += HandleClientConnected;
        Main.Client.OnConnectionFailed += HandleClientConnectionFailed;
        Main.Client.OnDisconnected += HandleClientDisconnected;
        Main.Server.OnServerStarted += HandleServerStarted;
        Main.Server.OnServerStartFailed += HandleServerStartFailed;
        Main.Server.OnServerStopped += HandleServerStopped;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Main.Client.OnConnectionStarted -= HandleClientConnectionStarted;
            Main.Client.OnConnected -= HandleClientConnected;
            Main.Client.OnConnectionFailed -= HandleClientConnectionFailed;
            Main.Client.OnDisconnected -= HandleClientDisconnected;
            Main.Server.OnServerStarted -= HandleServerStarted;
            Main.Server.OnServerStartFailed -= HandleServerStartFailed;
            Main.Server.OnServerStopped -= HandleServerStopped;
            Instance = null!;
        }
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout)
        {
            ProcessPendingStatusUpdates();
            state = GetState();
            UpdateChatVisibility();
        }

        bool sr2eMenuOpen = MenuEUtil.isAnyMenuOpen;
        if (!sr2eMenuOpen || !multiplayerUIHidden)
        {
            var previousDepth = GUI.depth;
            GUI.depth = -10000;
            didUnfocus = false;
            DrawWindow();
            DrawChat();
            GUI.depth = previousDepth;
        }
        else if (!didUnfocus)
        {
            shouldUnfocusChat = true;
            UnfocusChat();
            didUnfocus = true;
        }
    }

    private void DrawWindow()
    {
        if (openHubDiagnosticFrames > 0)
        {
            SrLogger.LogMessage(
                $"Multiplayer UI draw attempt. State: {state}, hidden: {multiplayerUIHidden}, SR2E menu open: {MenuEUtil.isAnyMenuOpen}",
                SrLogTarget.Both);
            openHubDiagnosticFrames = 0;
        }

        if (state == MenuState.Hidden) return;

        var windowRect = CalculateWindowRect();
        GUI.Box(windowRect, "SR2MP Multiplayer");
        ResetWindowLayout(windowRect);

        switch (state)
        {
            case MenuState.SettingsInitial:
                FirstTimeScreen();
                break;
            case MenuState.SettingsMain:
                SettingsScreen();
                break;
            case MenuState.DisconnectedMainMenu:
                MainMenuScreen();
                break;
            case MenuState.DisconnectedInGame:
                InGameScreen();
                break;
            case MenuState.ConnectedClient:
                ConnectedScreen();
                break;
            case MenuState.ConnectedHost:
                HostingScreen();
                break;
            default:
                UnimplementedScreen();
                break;
        }

        AdjustInputValues();
    }

    private static Rect CalculateWindowRect()
    {
        float width = Mathf.Min(WindowWidth, Mathf.Max(260f, Screen.width - 12f));
        float height = Mathf.Min(WindowHeight, Mathf.Max(260f, Screen.height - 12f));
        float x = Mathf.Max(6f, (Screen.width - width) * 0.5f);
        float y = Mathf.Max(6f, (Screen.height - height) * 0.5f);

        return new Rect(x, y, width, height);
    }
}
