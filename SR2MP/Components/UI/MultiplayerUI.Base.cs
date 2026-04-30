using MelonLoader;
using SR2E.Utils;

namespace SR2MP.Components.UI;

// TODO: Asset bundle
[RegisterTypeInIl2Cpp(false)]
public sealed partial class MultiplayerUI : MonoBehaviour
{
    public static MultiplayerUI Instance { get; private set; }

    private bool didUnfocus = false;

    private void Awake()
    {
        firstTime = Main.SetupUI;
        multiplayerUIHidden = !firstTime;
        usernameInput = Main.Username;
        allowCheatsInput = Main.AllowCheats;
        ipInput = Main.SavedConnectIP;
        portInput = Main.SavedConnectPort;
        hostPortInput = Main.SavedHostPort;

        if (Instance)
        {
            SrLogger.LogError("Tried to create instance of MultiplayerUI, but it already exists!", SrLogTarget.Both);
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        Instance = null!;
    }

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout)
        {
            state = GetState();
            UpdateChatVisibility();
        }

        if (!MenuEUtil.isAnyMenuOpen)
        {
            didUnfocus = false;
            DrawWindow();
            DrawChat();
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
