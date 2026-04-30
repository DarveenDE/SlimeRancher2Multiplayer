using Il2CppInterop.Runtime.Attributes;
using Il2CppTMPro;
using MelonLoader;
using SR2E;
using SR2E.Enums;
using SR2E.Storage;
using SR2E.Utils;
using SR2MP.Shared.Utils;
using UnityEngine.UI;

namespace SR2MP.Components.UI;

[RegisterTypeInIl2Cpp(false)]
public sealed class NativeMultiplayerMenu : SR2EMenu
{
    private enum NativeTab : byte
    {
        Home,
        Join,
        Host,
        Session,
        Settings,
    }

    private enum NativeConnectionStep : byte
    {
        Idle,
        ResolvingAddress,
        Connecting,
        Synchronizing,
        Connected,
        Failed,
    }

    private const string RootName = "SR2MP_NativeMenuRoot";
    private const string MenuName = "SR2MP_NativeMultiplayerMenu";
    private const float PanelWidth = 980f;
    private const float PanelHeight = 650f;
    private const float ActionButtonWidth = 270f;
    private const float RecentButtonWidth = 420f;
    private const int NativeRecentServers = 5;

    public static NativeMultiplayerMenu Instance { get; private set; }

    private readonly Dictionary<NativeTab, Button> tabButtons = new();
    private readonly List<GameObject> contentItems = new();

    private Transform contentRoot = null!;
    private TextMeshProUGUI titleText = null!;
    private TextMeshProUGUI statusText = null!;
    private CanvasGroup canvasGroup = null!;

    private TMP_InputField? joinAddressField;
    private TMP_InputField? hostPortField;
    private TMP_InputField? usernameField;

    private NativeTab activeTab = NativeTab.Home;
    private string joinAddressInput = "127.0.0.1:1919";
    private string hostPortInput = "1919";
    private string usernameInput = "Player";
    private ServerAddress pendingJoinAddress;
    private bool confirmingJoin;
    private bool allowCheatsInput;
    private string feedback = string.Empty;
    private NativeConnectionStep connectionStep = NativeConnectionStep.Idle;
    private bool started;
    private float nextPlayerRefresh;

    public override bool createCommands => false;
    public override bool inGameOnly => true;

    public static new MenuIdentifier GetMenuIdentifier()
    {
        return new MenuIdentifier("sr2mp.multiplayer", SR2EMenuFont.SR2, SR2EMenuTheme.SR2E, "SR2MPMultiplayer");
    }

    public static bool EnsureCreated()
    {
        if (Instance)
            return true;

        try
        {
            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);

            CreateMenuBlock(root.transform);
            CreatePopupBlock(root.transform);

            var menuObject = new GameObject(MenuName);
            menuObject.transform.SetParent(root.transform, false);
            Instance = menuObject.AddComponent<NativeMultiplayerMenu>();

            SrLogger.LogMessage("SR2MP native multiplayer menu initialized.", SrLogTarget.Both);
            return true;
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Failed to initialize SR2MP native multiplayer menu: {ex}", SrLogTarget.Both);
            return false;
        }
    }

    public static void OpenFromPauseMenu()
    {
        if (!EnsureCreated())
        {
            OpenLegacyHub();
            return;
        }

        if (MultiplayerUI.Instance)
            MultiplayerUI.Instance.CloseHub();

        try
        {
            MenuEUtil.CloseOpenMenu();

            int delayTicks = Instance.started ? 2 : 4;
            ActionsEUtil.ExecuteInTicks((Action)(() =>
            {
                if (!Instance)
                {
                    OpenLegacyHub();
                    return;
                }

                Instance.Open();
                if (!Instance.isOpen)
                {
                    SrLogger.LogWarning("Native SR2MP menu did not open; falling back to the classic hub.", SrLogTarget.Both);
                    OpenLegacyHub();
                }
            }), delayTicks);
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not open native SR2MP menu: {ex.Message}", SrLogTarget.Both);
            OpenLegacyHub();
        }
    }

    protected override void OnAwake()
    {
        Instance = this;
        SyncInputsFromPreferences();
        Main.Client.OnConnectionStarted += HandleClientConnectionStarted;
        Main.Client.OnConnected += HandleClientConnected;
        Main.Client.OnConnectionFailed += HandleClientConnectionFailed;
        Main.Client.OnDisconnected += HandleClientDisconnected;
        Main.Server.OnServerStarted += HandleServerStarted;
        Main.Server.OnServerStartFailed += HandleServerStartFailed;
        Main.Server.OnServerStopped += HandleServerStopped;
        BuildMenuUi();
    }

    protected override void OnStart()
    {
        started = true;
        if (canvasGroup)
            canvasGroup.alpha = 1f;
    }

    protected override void OnOpen()
    {
        SyncInputsFromPreferences();
        activeTab = Main.Server.IsRunning() || Main.Client.IsConnected
            ? NativeTab.Session
            : NativeTab.Home;
        confirmingJoin = false;
        feedback = string.Empty;
        UpdateConnectionStepFromState();
        RefreshContent();
        UpdateStatusText();
    }

    protected override void OnClose()
    {
        ClearInputReferences();
        feedback = string.Empty;
    }

    protected override void OnUpdate()
    {
        if (!isOpen)
            return;

        SyncInputFields();
        UpdateStatusText();

        if (activeTab == NativeTab.Session && UnityEngine.Time.unscaledTime >= nextPlayerRefresh)
        {
            nextPlayerRefresh = UnityEngine.Time.unscaledTime + 1f;
            RefreshContent();
        }
    }

    public override void OnCloseUIPressed()
    {
        Close();
    }

    private static void CreateMenuBlock(Transform root)
    {
        var block = new GameObject("blockRec");
        block.transform.SetParent(root, false);

        var canvas = block.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 19990;
        var scaler = block.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        block.AddComponent<GraphicRaycaster>();

        var background = new GameObject("Background");
        background.transform.SetParent(block.transform, false);
        Stretch(background.AddComponent<RectTransform>());
        background.AddComponent<Image>().color = new Color(0.02f, 0.025f, 0.035f, 0.54f);

        block.SetActive(false);
    }

    private static void CreatePopupBlock(Transform root)
    {
        var popup = new GameObject("blockPopUpRec");
        popup.transform.SetParent(root, false);
        Stretch(popup.AddComponent<RectTransform>());
    }

    [HideFromIl2Cpp]
    private void BuildMenuUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.105f, 0.135f, 0.18f, 0.96f);

        BuildHeader(panel.transform);
        BuildTabs(panel.transform);
        BuildContentRoot(panel.transform);
    }

    [HideFromIl2Cpp]
    private void BuildHeader(Transform panel)
    {
        var titleObject = new GameObject("Title");
        titleObject.transform.SetParent(panel, false);
        var titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(40f, -78f);
        titleRect.offsetMax = new Vector2(-110f, -22f);

        titleText = titleObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "SR2MP Multiplayer";
        titleText.fontSize = 34f;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.enableWordWrapping = false;

        var closeButton = CreateButton(panel, "CloseButton", "X", Close, ButtonVisual.Secondary, 62f);
        var closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(62f, 52f);
        closeRect.anchoredPosition = new Vector2(-36f, -24f);

        var statusObject = new GameObject("Status");
        statusObject.transform.SetParent(panel, false);
        var statusRect = statusObject.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.offsetMin = new Vector2(40f, -118f);
        statusRect.offsetMax = new Vector2(-40f, -82f);

        statusText = statusObject.AddComponent<TextMeshProUGUI>();
        statusText.fontSize = 22f;
        statusText.color = new Color(0.78f, 0.88f, 0.95f, 1f);
        statusText.alignment = TextAlignmentOptions.MidlineLeft;
        statusText.enableWordWrapping = false;
    }

    [HideFromIl2Cpp]
    private void BuildTabs(Transform panel)
    {
        var tabs = new GameObject("Tabs");
        tabs.transform.SetParent(panel, false);
        var tabsRect = tabs.AddComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 1f);
        tabsRect.anchorMax = new Vector2(1f, 1f);
        tabsRect.pivot = new Vector2(0.5f, 1f);
        tabsRect.offsetMin = new Vector2(36f, -180f);
        tabsRect.offsetMax = new Vector2(-36f, -126f);

        var layout = tabs.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        AddTabButton(tabs.transform, NativeTab.Home, "Home");
        AddTabButton(tabs.transform, NativeTab.Join, "Join");
        AddTabButton(tabs.transform, NativeTab.Host, "Host");
        AddTabButton(tabs.transform, NativeTab.Session, "Session");
    }

    [HideFromIl2Cpp]
    private void BuildContentRoot(Transform panel)
    {
        var content = new GameObject("Content");
        content.transform.SetParent(panel, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(40f, 42f);
        contentRect.offsetMax = new Vector2(-40f, -205f);

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.padding = new RectOffset(0, 0, 8, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        contentRoot = content.transform;
    }

    [HideFromIl2Cpp]
    private void AddTabButton(Transform parent, NativeTab tab, string label)
    {
        var button = CreateButton(parent, $"Tab_{label}", label, () =>
        {
            SyncInputFields();
            activeTab = tab;
            feedback = string.Empty;
            RefreshContent();
        }, ButtonVisual.Secondary);

        var layout = button.gameObject.GetComponent<LayoutElement>();
        layout.minWidth = 0f;
        layout.preferredWidth = 0f;
        layout.minHeight = 52f;
        layout.preferredHeight = 52f;
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 0f;
        tabButtons[tab] = button;
    }

    [HideFromIl2Cpp]
    private void RefreshContent()
    {
        ClearContent();
        UpdateTabButtons();
        ClearInputReferences();

        switch (activeTab)
        {
            case NativeTab.Home:
                DrawHomeTab();
                break;
            case NativeTab.Join:
                DrawJoinTab();
                break;
            case NativeTab.Host:
                DrawHostTab();
                break;
            case NativeTab.Session:
                DrawSessionTab();
                break;
            case NativeTab.Settings:
                DrawSettingsTab();
                break;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
            AddText(feedback, 22f, new Color(1f, 0.9f, 0.62f, 1f), 64f);
    }

    [HideFromIl2Cpp]
    private void DrawHomeTab()
    {
        if (Main.Server.IsRunning() || Main.Client.IsConnected)
        {
            DrawSessionTab();
            return;
        }

        AddText("Multiplayer", 28f, Color.white, 42f);
        AddText("Start a world for friends or join a hosted world.", 21f, new Color(0.78f, 0.88f, 0.95f, 1f), 42f);

        var primaryRow = AddRow(64f);
        CreateButton(primaryRow.transform, "HomeHostButton", "Host World", () =>
        {
            activeTab = NativeTab.Host;
            feedback = string.Empty;
            RefreshContent();
        }, ButtonVisual.Primary, 300f);
        CreateButton(primaryRow.transform, "HomeJoinButton", "Join World", () =>
        {
            activeTab = NativeTab.Join;
            feedback = string.Empty;
            RefreshContent();
        }, ButtonVisual.Secondary, 300f);
        AddFlexibleSpace(primaryRow.transform);

        DrawRecentServers();

        var secondaryRow = AddRow(54f);
        CreateButton(secondaryRow.transform, "HomeSettingsButton", "Settings", () =>
        {
            activeTab = NativeTab.Settings;
            feedback = string.Empty;
            RefreshContent();
        }, ButtonVisual.Secondary, 210f);
        CreateButton(secondaryRow.transform, "HomeClassicButton", "Classic UI", OpenLegacyFromNative, ButtonVisual.Secondary, 210f);
        AddFlexibleSpace(secondaryRow.transform);
    }

    [HideFromIl2Cpp]
    private void DrawJoinTab()
    {
        if (confirmingJoin)
        {
            DrawJoinConfirmation();
            return;
        }

        AddText("Join a hosted world", 28f, Color.white, 42f);
        joinAddressField = AddInputRow("Server address", joinAddressInput, "127.0.0.1:1919", 160);

        bool validAddress = ServerAddressParser.TryParse(joinAddressInput, hostPortInput, out var address, out var addressError);

        if (!validAddress)
            AddText(addressError, 21f, new Color(1f, 0.76f, 0.6f, 1f), 38f);
        else if (!CanStartNetworkAction())
            AddText(GetUnavailableActionText(), 21f, new Color(1f, 0.76f, 0.6f, 1f), 38f);
        else
            AddText($"Ready to join {address.Display}.", 21f, new Color(0.74f, 0.94f, 0.9f, 1f), 38f);

        var buttonRow = AddRow(58f);
        var joinButton = CreateButton(buttonRow.transform, "JoinButton", "Join", HandleJoinPressed, ButtonVisual.Primary);
        AddFlexibleSpace(buttonRow.transform);

        DrawRecentServers();
    }

    [HideFromIl2Cpp]
    private void DrawJoinConfirmation()
    {
        AddText("Use an empty save", 28f, Color.white, 42f);
        AddText($"Server: {pendingJoinAddress.Display}", 22f, new Color(0.86f, 0.92f, 1f, 1f), 38f);
        AddText("Joining will sync this save with the hosted world. Use a fresh client save to avoid losing local progress.",
            21f,
            new Color(1f, 0.9f, 0.62f, 1f),
            76f);

        var row = AddRow(58f);
        CreateButton(row.transform, "ConfirmJoinButton", "Join Anyway", HandleConfirmedJoinPressed, ButtonVisual.Primary, 300f);
        CreateButton(row.transform, "BackJoinButton", "Back", () =>
        {
            confirmingJoin = false;
            feedback = string.Empty;
            RefreshContent();
        }, ButtonVisual.Secondary, 190f);
        AddFlexibleSpace(row.transform);
    }

    [HideFromIl2Cpp]
    private void DrawRecentServers()
    {
        var recentServers = Main.SavedRecentServers
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => ServerAddressParser.TryParse(entry, string.Empty, out var address, out _) ? address : default(ServerAddress?))
            .Where(address => address.HasValue)
            .Select(address => address!.Value)
            .Take(3)
            .ToList();

        if (recentServers.Count == 0 || !CanStartNetworkAction())
            return;

        AddText("Recent servers", 22f, new Color(0.86f, 0.92f, 1f, 1f), 34f);
        foreach (var recentServer in recentServers)
        {
            var row = AddRow(48f);
            CreateButton(row.transform, $"Recent_{recentServer.Display}", recentServer.Display, () =>
            {
                joinAddressInput = recentServer.Display;
                activeTab = NativeTab.Join;
                feedback = string.Empty;
                RefreshContent();
            }, ButtonVisual.Secondary, RecentButtonWidth);
            AddFlexibleSpace(row.transform);
        }
    }

    [HideFromIl2Cpp]
    private void DrawHostTab()
    {
        if (Main.Server.IsRunning())
        {
            DrawSessionTab();
            return;
        }

        AddText("Host this save", 28f, Color.white, 42f);
        hostPortField = AddInputRow("Port", hostPortInput, "1919", 5);

        bool validPort = ushort.TryParse(hostPortInput, out var hostPort) && hostPort > 0;

        if (!validPort)
            AddText("Invalid port. Use a number from 1 to 65535.", 21f, new Color(1f, 0.76f, 0.6f, 1f), 38f);
        else if (!CanStartNetworkAction())
            AddText(GetUnavailableActionText(), 21f, new Color(1f, 0.76f, 0.6f, 1f), 38f);
        else
            AddText($"Ready to host on port {hostPort}.", 21f, new Color(0.74f, 0.94f, 0.9f, 1f), 38f);

        var row = AddRow(58f);
        var hostButton = CreateButton(row.transform, "HostButton", "Host", HandleHostPressed, ButtonVisual.Primary);
        AddFlexibleSpace(row.transform);
    }

    [HideFromIl2Cpp]
    private void DrawSessionTab()
    {
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
        {
            AddText("No active session", 28f, Color.white, 42f);
            AddText("Host a world or join one to see session details here.",
                21f,
                new Color(0.78f, 0.88f, 0.95f, 1f),
                42f);

            if (connectionStep == NativeConnectionStep.Failed && !string.IsNullOrWhiteSpace(feedback))
                AddText(feedback, 21f, new Color(1f, 0.76f, 0.6f, 1f), 78f);

            var sessionActionRow = AddRow(58f);
            CreateButton(sessionActionRow.transform, "SessionJoinButton", "Join World", () =>
            {
                activeTab = NativeTab.Join;
                RefreshContent();
            }, ButtonVisual.Primary);
            CreateButton(sessionActionRow.transform, "SessionHostButton", "Host World", () =>
            {
                activeTab = NativeTab.Host;
                RefreshContent();
            }, ButtonVisual.Secondary);
            AddFlexibleSpace(sessionActionRow.transform);
            return;
        }

        AddText(Main.Server.IsRunning() ? "Your world is open" : "Connected to a hosted world", 28f, Color.white, 42f);
        if (!Main.Server.IsRunning())
            DrawConnectionProgress();

        if (Main.Server.IsRunning())
        {
            AddText($"Port: {Main.Server.Port}", 22f, new Color(0.86f, 0.92f, 1f, 1f), 34f);
            AddText($"Players connected: {Main.Server.GetClientCount()}", 22f, new Color(0.86f, 0.92f, 1f, 1f), 34f);
        }

        AddText($"{Main.Username} (you)", 22f, new Color(0.86f, 0.92f, 1f, 1f), 34f);

        var players = playerManager.GetAllPlayers();
        if (players.Count == 0)
        {
            AddText("No other players connected.", 21f, new Color(0.74f, 0.82f, 0.9f, 1f), 36f);
        }
        else
        {
            foreach (var player in players.Take(8))
            {
                AddText(!string.IsNullOrWhiteSpace(player.Username) ? player.Username : "Invalid username.",
                    21f,
                    new Color(0.74f, 0.82f, 0.9f, 1f),
                    34f);
            }
        }

        var row = AddRow(58f);
        string label = Main.Server.IsRunning()
            ? "Close Host"
            : Main.Client.IsConnectionPending ? "Cancel Join" : "Disconnect";
        CreateButton(row.transform, "StopSessionButton", label, HandleStopSessionPressed, ButtonVisual.Danger);
        AddFlexibleSpace(row.transform);
    }

    [HideFromIl2Cpp]
    private void DrawSettingsTab()
    {
        AddText("Settings", 28f, Color.white, 42f);
        usernameField = AddInputRow("Username", usernameInput, "Player", 32);

        var cheatsRow = AddRow(56f);
        AddRowLabel(cheatsRow.transform, "Allow Cheats");
        CreateButton(cheatsRow.transform,
            "AllowCheatsButton",
            allowCheatsInput ? "Yes" : "No",
            () =>
            {
                SyncInputFields();
                allowCheatsInput = !allowCheatsInput;
                RefreshContent();
            },
            ButtonVisual.Secondary,
            170f);

        var saveRow = AddRow(58f);
        CreateButton(saveRow.transform, "SaveSettingsButton", "Save Settings", HandleSaveSettingsPressed, ButtonVisual.Primary);
        CreateButton(saveRow.transform, "ClassicHubButton", "Classic UI", OpenLegacyFromNative, ButtonVisual.Secondary);
    }

    [HideFromIl2Cpp]
    private void DrawConnectionProgress()
    {
        UpdateConnectionStepFromState();

        var row = AddRow(44f);
        AddProgressPill(row.transform, "Address", NativeConnectionStep.ResolvingAddress);
        AddProgressPill(row.transform, "Connect", NativeConnectionStep.Connecting);
        AddProgressPill(row.transform, "Sync", NativeConnectionStep.Synchronizing);
        AddProgressPill(row.transform, "Done", NativeConnectionStep.Connected);
        AddFlexibleSpace(row.transform);
    }

    [HideFromIl2Cpp]
    private void AddProgressPill(Transform row, string label, NativeConnectionStep step)
    {
        var item = new GameObject($"Progress_{label}");
        item.transform.SetParent(row, false);
        item.AddComponent<RectTransform>();

        var layoutElement = item.AddComponent<LayoutElement>();
        layoutElement.minWidth = 135f;
        layoutElement.preferredWidth = 135f;
        layoutElement.minHeight = 38f;
        layoutElement.preferredHeight = 38f;

        var image = item.AddComponent<Image>();
        image.color = GetProgressColor(step);
        if (whitePillBg)
        {
            image.sprite = whitePillBg;
            image.type = Image.Type.Sliced;
        }

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(item.transform, false);
        Stretch(textObject.AddComponent<RectTransform>());

        var tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
    }

    private Color GetProgressColor(NativeConnectionStep step)
    {
        if (connectionStep == NativeConnectionStep.Failed)
            return new Color(0.72f, 0.23f, 0.28f, 1f);

        return (int)connectionStep >= (int)step
            ? new Color(0.15f, 0.58f, 0.78f, 1f)
            : new Color(0.17f, 0.23f, 0.31f, 1f);
    }

    [HideFromIl2Cpp]
    private TMP_InputField AddInputRow(string label, string value, string placeholder, int characterLimit)
    {
        var row = AddRow(58f);
        AddRowLabel(row.transform, label);
        return CreateInput(row.transform, $"Input_{label}", value, placeholder, characterLimit);
    }

    [HideFromIl2Cpp]
    private GameObject AddRow(float height)
    {
        var row = new GameObject("Row");
        row.transform.SetParent(contentRoot, false);
        contentItems.Add(row);

        var rect = row.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, height);

        var layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return row;
    }

    [HideFromIl2Cpp]
    private TextMeshProUGUI AddText(string text, float fontSize, Color color, float height)
    {
        var item = new GameObject("Text");
        item.transform.SetParent(contentRoot, false);
        contentItems.Add(item);

        var rect = item.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, height);

        var layoutElement = item.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;

        var tmp = item.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    [HideFromIl2Cpp]
    private void AddRowLabel(Transform row, string label)
    {
        var labelObject = new GameObject($"Label_{label}");
        labelObject.transform.SetParent(row, false);
        labelObject.AddComponent<RectTransform>();

        var layoutElement = labelObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = 190f;
        layoutElement.preferredWidth = 190f;

        var tmp = labelObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.color = new Color(0.86f, 0.92f, 1f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
    }

    [HideFromIl2Cpp]
    private TMP_InputField CreateInput(Transform parent, string name, string value, string placeholder, int characterLimit)
    {
        var inputObject = new GameObject(name);
        inputObject.transform.SetParent(parent, false);
        inputObject.AddComponent<RectTransform>();

        var layoutElement = inputObject.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;
        layoutElement.minHeight = 52f;
        layoutElement.preferredHeight = 52f;

        var image = inputObject.AddComponent<Image>();
        image.color = new Color(0.045f, 0.06f, 0.085f, 0.92f);

        var input = inputObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.characterLimit = characterLimit;
        input.lineType = TMP_InputField.LineType.SingleLine;

        var viewportObject = new GameObject("TextViewport");
        viewportObject.transform.SetParent(inputObject.transform, false);
        var viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(18f, 4f);
        viewportRect.offsetMax = new Vector2(-18f, -4f);
        input.textViewport = viewportRect;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(viewportObject.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        Stretch(textRect);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 23f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        input.textComponent = text;

        var placeholderObject = new GameObject("Placeholder");
        placeholderObject.transform.SetParent(viewportObject.transform, false);
        var placeholderRect = placeholderObject.AddComponent<RectTransform>();
        Stretch(placeholderRect);
        var placeholderText = placeholderObject.AddComponent<TextMeshProUGUI>();
        placeholderText.text = placeholder;
        placeholderText.fontSize = 23f;
        placeholderText.color = new Color(1f, 1f, 1f, 0.42f);
        placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
        placeholderText.enableWordWrapping = false;
        input.placeholder = placeholderText;

        input.text = value;
        return input;
    }

    [HideFromIl2Cpp]
    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Action onClick,
        ButtonVisual visual,
        float preferredWidth = ActionButtonWidth,
        bool flexibleWidth = false)
    {
        var buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        buttonObject.AddComponent<RectTransform>();

        var image = buttonObject.AddComponent<Image>();
        image.color = visual switch
        {
            ButtonVisual.Primary => new Color(0.15f, 0.58f, 0.78f, 1f),
            ButtonVisual.Danger => new Color(0.72f, 0.23f, 0.28f, 1f),
            _ => new Color(0.17f, 0.23f, 0.31f, 1f),
        };

        if (whitePillBg)
        {
            image.sprite = whitePillBg;
            image.type = Image.Type.Sliced;
        }

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = CreateButtonColors(visual);
        button.onClick.AddListener((Action)onClick);

        var layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = Math.Min(preferredWidth, ActionButtonWidth);
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.minHeight = 52f;
        layoutElement.preferredHeight = 52f;
        layoutElement.flexibleWidth = flexibleWidth ? 1f : 0f;
        layoutElement.flexibleHeight = 0f;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        Stretch(textRect);

        var tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = label.Length > 18 ? 19f : 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;

        return button;
    }

    [HideFromIl2Cpp]
    private void AddFlexibleSpace(Transform parent)
    {
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(parent, false);
        spacer.AddComponent<RectTransform>();
        var layoutElement = spacer.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;
    }

    [HideFromIl2Cpp]
    private void UpdateTabButtons()
    {
        foreach (var pair in tabButtons)
        {
            if (!pair.Value)
                continue;

            var image = pair.Value.GetComponent<Image>();
            image.color = pair.Key == activeTab
                ? new Color(0.15f, 0.58f, 0.78f, 1f)
                : new Color(0.17f, 0.23f, 0.31f, 1f);
        }
    }

    [HideFromIl2Cpp]
    private void HandleJoinPressed()
    {
        SyncInputFields();
        if (!ServerAddressParser.TryParse(joinAddressInput, hostPortInput, out var address, out var error))
        {
            feedback = error;
            RefreshContent();
            return;
        }

        if (!CanStartNetworkAction())
        {
            feedback = GetUnavailableActionText();
            RefreshContent();
            return;
        }

        pendingJoinAddress = address;
        confirmingJoin = true;
        feedback = string.Empty;
        RefreshContent();
    }

    [HideFromIl2Cpp]
    private void HandleConfirmedJoinPressed()
    {
        if (!CanStartNetworkAction())
        {
            feedback = GetUnavailableActionText();
            confirmingJoin = false;
            RefreshContent();
            return;
        }

        confirmingJoin = false;
        activeTab = NativeTab.Session;
        connectionStep = NativeConnectionStep.ResolvingAddress;
        feedback = $"Checking {pendingJoinAddress.Display}...";
        RefreshContent();

        if (!ServerAddressParser.TryResolve(pendingJoinAddress, out var resolvedAddress, out var resolveError))
        {
            connectionStep = NativeConnectionStep.Failed;
            feedback = FormatConnectionFailure(resolveError);
            RefreshContent();
            return;
        }

        connectionStep = NativeConnectionStep.Synchronizing;
        feedback = $"Waiting for world sync from {pendingJoinAddress.Display}...";
        RefreshContent();

        bool started = Main.Client.Connect(resolvedAddress.Host, resolvedAddress.Port);
        if (!started)
        {
            connectionStep = NativeConnectionStep.Failed;
            feedback = FormatConnectionFailure(Main.Client.LastConnectionError);
            RefreshContent();
            return;
        }

        connectionStep = NativeConnectionStep.Synchronizing;
        feedback = $"Waiting for world sync from {pendingJoinAddress.Display}...";
        Main.SetConfigValue("recent_ip", pendingJoinAddress.Host);
        Main.SetConfigValue("recent_port", pendingJoinAddress.Port.ToString());
        RememberRecentServer(pendingJoinAddress);
        RefreshContent();
    }

    [HideFromIl2Cpp]
    private void HandleHostPressed()
    {
        SyncInputFields();
        if (!ushort.TryParse(hostPortInput, out var port) || port == 0)
        {
            feedback = "Invalid port. Use a number from 1 to 65535.";
            RefreshContent();
            return;
        }

        if (!CanStartNetworkAction())
        {
            feedback = GetUnavailableActionText();
            RefreshContent();
            return;
        }

        feedback = $"Starting host on port {port}...";
        activeTab = NativeTab.Session;
        RefreshContent();

        bool startedHost = Main.Server.Start(port, true);
        if (startedHost)
        {
            Main.SetConfigValue("host_port", hostPortInput);
            connectionStep = NativeConnectionStep.Connected;
            feedback = $"Your world is open on port {port}.";
        }
        else
        {
            connectionStep = NativeConnectionStep.Failed;
            feedback = "Could not host on that port. Try another port, or check whether another app is already using it.";
        }

        RefreshContent();
    }

    [HideFromIl2Cpp]
    private void HandleStopSessionPressed()
    {
        if (Main.Server.IsRunning())
        {
            feedback = "Closing hosted world...";
            Main.Server.Close();
        }
        else if (Main.Client.IsConnected)
        {
            feedback = Main.Client.IsConnectionPending ? "Cancelling connection..." : "Disconnecting...";
            Main.Client.Disconnect(Main.Client.IsConnectionPending
                ? "Connection cancelled."
                : "You disconnected from the world.");
        }

        RefreshContent();
    }

    [HideFromIl2Cpp]
    private void HandleSaveSettingsPressed()
    {
        SyncInputFields();
        if (string.IsNullOrWhiteSpace(usernameInput))
        {
            feedback = "You must set a username.";
            RefreshContent();
            return;
        }

        Main.SetConfigValue("username", usernameInput.Trim());
        Main.SetConfigValue("allow_cheats", allowCheatsInput);
        feedback = "Settings saved.";
        RefreshContent();
    }

    [HideFromIl2Cpp]
    private void OpenLegacyFromNative()
    {
        Close();
        ActionsEUtil.ExecuteInTicks((Action)OpenLegacyHub, 1);
    }

    [HideFromIl2Cpp]
    private static void OpenLegacyHub()
    {
        if (MultiplayerUI.Instance)
            MultiplayerUI.Instance.OpenHub();
        else
            SrLogger.LogWarning("Classic multiplayer hub is not available yet.", SrLogTarget.Both);
    }

    [HideFromIl2Cpp]
    private void SyncInputsFromPreferences()
    {
        usernameInput = Main.Username;
        allowCheatsInput = Main.AllowCheats;
        hostPortInput = string.IsNullOrWhiteSpace(Main.SavedHostPort) ? "1919" : Main.SavedHostPort;
        string savedHost = string.IsNullOrWhiteSpace(Main.SavedConnectIP) ? "127.0.0.1" : Main.SavedConnectIP;
        string savedPort = string.IsNullOrWhiteSpace(Main.SavedConnectPort) ? hostPortInput : Main.SavedConnectPort;
        joinAddressInput = new ServerAddress(savedHost, ushort.TryParse(savedPort, out var port) && port > 0 ? port : (ushort)1919).Display;
    }

    [HideFromIl2Cpp]
    private void SyncInputFields()
    {
        if (joinAddressField is not null)
        {
            var joinAddress = joinAddressField;
            joinAddressInput = joinAddress.text?.Trim() ?? string.Empty;
        }

        if (hostPortField is not null)
        {
            var hostPort = hostPortField;
            hostPortInput = hostPort.text?.Trim() ?? string.Empty;
        }

        if (usernameField is not null)
        {
            var username = usernameField;
            usernameInput = username.text?.Trim() ?? string.Empty;
        }
    }

    [HideFromIl2Cpp]
    private void ClearInputReferences()
    {
        joinAddressField = null;
        hostPortField = null;
        usernameField = null;
    }

    [HideFromIl2Cpp]
    private void ClearContent()
    {
        foreach (var item in contentItems)
        {
            if (item)
                Object.Destroy(item);
        }

        contentItems.Clear();
    }

    [HideFromIl2Cpp]
    private void UpdateStatusText()
    {
        if (!statusText)
            return;

        statusText.text = GetStatusText();
    }

    [HideFromIl2Cpp]
    private static string GetStatusText()
    {
        if (Main.Server.IsRunning())
            return $"Hosting on port {Main.Server.Port} - {Main.Server.GetClientCount()} client(s) connected.";

        if (Main.Client.IsConnectionPending)
            return "Joining hosted world...";

        if (Main.Client.IsConnected)
            return "Connected to a hosted world.";

        return string.IsNullOrWhiteSpace(Main.Client.LastConnectionError)
            ? "Ready to host or join."
            : Main.Client.LastConnectionError;
    }

    [HideFromIl2Cpp]
    private static bool CanStartNetworkAction()
    {
        return !Main.Server.IsRunning() && !Main.Client.IsConnected;
    }

    [HideFromIl2Cpp]
    private static string GetUnavailableActionText()
    {
        if (Main.Server.IsRunning())
            return "Close your hosted world before joining or hosting another one.";

        if (Main.Client.IsConnectionPending)
            return "Please wait until the current join attempt finishes, or cancel it first.";

        if (Main.Client.IsConnected)
            return "Disconnect before joining or hosting another world.";

        return "Please wait until the current multiplayer action finishes.";
    }

    [HideFromIl2Cpp]
    private void RememberRecentServer(ServerAddress address)
    {
        var recentServers = Main.SavedRecentServers
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => ServerAddressParser.TryParse(entry, string.Empty, out var recent, out _) ? recent : default(ServerAddress?))
            .Where(recent => recent.HasValue)
            .Select(recent => recent!.Value)
            .Where(recent => recent.Port != address.Port ||
                !string.Equals(recent.Host, address.Host, StringComparison.OrdinalIgnoreCase))
            .ToList();

        recentServers.Insert(0, address);
        if (recentServers.Count > NativeRecentServers)
            recentServers.RemoveRange(NativeRecentServers, recentServers.Count - NativeRecentServers);

        Main.SetConfigValue("recent_servers", string.Join("|", recentServers.Select(recent => recent.Display)));
    }

    [HideFromIl2Cpp]
    private void UpdateConnectionStepFromState()
    {
        if (Main.Client.IsConnected && !Main.Client.IsConnectionPending)
        {
            connectionStep = NativeConnectionStep.Connected;
            return;
        }

        if (Main.Client.IsConnectionPending && (int)connectionStep < (int)NativeConnectionStep.Synchronizing)
        {
            connectionStep = NativeConnectionStep.Synchronizing;
            return;
        }

        if (Main.Server.IsRunning())
        {
            connectionStep = NativeConnectionStep.Connected;
            return;
        }

        if (connectionStep != NativeConnectionStep.Failed)
            connectionStep = NativeConnectionStep.Idle;
    }

    [HideFromIl2Cpp]
    private static string FormatConnectionFailure(string? message)
    {
        string baseMessage = string.IsNullOrWhiteSpace(message)
            ? "Could not connect to the hosted world."
            : message.Trim();

        return $"{baseMessage} Check that the host is running, the address and port are correct, and firewall or tunnel settings allow the connection.";
    }

    private void HandleClientConnectionStarted(string address, int port)
    {
        QueueNativeRefresh(() =>
        {
            activeTab = NativeTab.Session;
            connectionStep = NativeConnectionStep.Connecting;
            feedback = $"Connecting to {ServerAddressParser.FormatHost(address)}:{port}...";
        });
    }

    private void HandleClientConnected(string playerId)
    {
        QueueNativeRefresh(() =>
        {
            activeTab = NativeTab.Session;
            connectionStep = NativeConnectionStep.Connected;
            feedback = "Joined hosted world.";
        });
    }

    private void HandleClientConnectionFailed(string message)
    {
        QueueNativeRefresh(() =>
        {
            activeTab = NativeTab.Session;
            connectionStep = NativeConnectionStep.Failed;
            feedback = FormatConnectionFailure(message);
        });
    }

    private void HandleClientDisconnected()
    {
        QueueNativeRefresh(() =>
        {
            if (connectionStep != NativeConnectionStep.Failed)
            {
                connectionStep = NativeConnectionStep.Idle;
                feedback = "Disconnected from world.";
            }
        });
    }

    private void HandleServerStarted()
    {
        QueueNativeRefresh(() =>
        {
            activeTab = NativeTab.Session;
            connectionStep = NativeConnectionStep.Connected;
            feedback = $"Your world is open on port {Main.Server.Port}.";
        });
    }

    private void HandleServerStartFailed(string message)
    {
        QueueNativeRefresh(() =>
        {
            activeTab = NativeTab.Host;
            connectionStep = NativeConnectionStep.Failed;
            feedback = $"{message} Try another port, or check whether another app is already using it.";
        });
    }

    private void HandleServerStopped()
    {
        QueueNativeRefresh(() =>
        {
            connectionStep = NativeConnectionStep.Idle;
            feedback = "Hosted world closed.";
        });
    }

    private void QueueNativeRefresh(Action update)
    {
        MainThreadDispatcher.Enqueue(() =>
        {
            update();
            if (!this || !isOpen)
                return;

            RefreshContent();
            UpdateStatusText();
        });
    }

    private static ColorBlock CreateButtonColors(ButtonVisual visual)
    {
        var block = new ColorBlock
        {
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };

        switch (visual)
        {
            case ButtonVisual.Primary:
                block.normalColor = new Color(0.15f, 0.58f, 0.78f, 1f);
                block.highlightedColor = new Color(0.21f, 0.7f, 0.9f, 1f);
                block.pressedColor = new Color(0.1f, 0.42f, 0.62f, 1f);
                break;
            case ButtonVisual.Danger:
                block.normalColor = new Color(0.72f, 0.23f, 0.28f, 1f);
                block.highlightedColor = new Color(0.9f, 0.32f, 0.38f, 1f);
                block.pressedColor = new Color(0.52f, 0.16f, 0.2f, 1f);
                break;
            default:
                block.normalColor = new Color(0.17f, 0.23f, 0.31f, 1f);
                block.highlightedColor = new Color(0.23f, 0.32f, 0.43f, 1f);
                block.pressedColor = new Color(0.1f, 0.15f, 0.22f, 1f);
                break;
        }

        block.selectedColor = block.highlightedColor;
        block.disabledColor = new Color(0.18f, 0.19f, 0.21f, 0.55f);
        return block;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        Main.Client.OnConnectionStarted -= HandleClientConnectionStarted;
        Main.Client.OnConnected -= HandleClientConnected;
        Main.Client.OnConnectionFailed -= HandleClientConnectionFailed;
        Main.Client.OnDisconnected -= HandleClientDisconnected;
        Main.Server.OnServerStarted -= HandleServerStarted;
        Main.Server.OnServerStartFailed -= HandleServerStartFailed;
        Main.Server.OnServerStopped -= HandleServerStopped;

        if (Instance == this)
            Instance = null!;
    }

    private enum ButtonVisual : byte
    {
        Primary,
        Secondary,
        Danger,
    }
}
