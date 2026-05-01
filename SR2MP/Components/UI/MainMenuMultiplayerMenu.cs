using Il2CppInterop.Runtime.Attributes;
using Il2CppTMPro;
using Il2CppMonomiPark.SlimeRancher.UI.ButtonBehavior;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu.Model;
using MelonLoader;
using SR2E.Utils;
using SR2MP.Shared.Utils;
using UnityEngine.UI;

namespace SR2MP.Components.UI;

[RegisterTypeInIl2Cpp(false)]
public sealed class MainMenuMultiplayerMenu : MonoBehaviour
{
    private enum MainMenuTab : byte
    {
        Home,
        Host,
        Join,
        Settings,
    }

    private const string MenuName = "SR2MP_MainMenuMultiplayerMenu";
    private const float PanelWidth = 940f;
    private const float PanelHeight = 700f;
    private const float ActionButtonWidth = 280f;
    private const int NativeLoadGameOpenMaxAttempts = 8;

    public static MainMenuMultiplayerMenu Instance { get; private set; }

    private readonly List<GameObject> contentItems = new();

    private static Sprite? transparentMainMenuIcon;

    private Transform contentRoot = null!;
    private TextMeshProUGUI statusText = null!;
    private TMP_InputField? joinAddressField;
    private TMP_InputField? hostPortField;
    private TMP_InputField? usernameField;
    private MainMenuTab activeTab = MainMenuTab.Home;
    private string joinAddressInput = "127.0.0.1:1919";
    private string hostPortInput = "1919";
    private string usernameInput = "Player";
    private ServerAddress pendingJoinAddress;
    private bool confirmingJoin;
    private bool allowCheatsInput;
    private string feedback = string.Empty;

    public static bool EnsureCreated()
    {
        if (Instance)
            return true;

        try
        {
            var menuObject = new GameObject(MenuName);
            Instance = menuObject.AddComponent<MainMenuMultiplayerMenu>();
            SrLogger.LogMessage("SR2MP main-menu multiplayer shell initialized.", SrLogTarget.Both);
            return true;
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Failed to initialize SR2MP main-menu multiplayer shell: {ex}", SrLogTarget.Both);
            return false;
        }
    }

    public static void OpenFromMainMenu()
    {
        if (!EnsureCreated())
            return;

        Instance.Open();
    }

    public static Sprite GetTransparentMainMenuIcon()
    {
        if (transparentMainMenuIcon)
            return transparentMainMenuIcon!;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "SR2MP_TransparentMainMenuIconTexture",
            hideFlags = HideFlags.HideAndDontSave,
        };

        texture.SetPixels(new[]
        {
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0f),
        });
        texture.Apply();

        transparentMainMenuIcon = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        transparentMainMenuIcon.name = "SR2MP_TransparentMainMenuIcon";
        transparentMainMenuIcon.hideFlags = HideFlags.HideAndDontSave;
        return transparentMainMenuIcon!;
    }

    private void Awake()
    {
        Instance = this;
        BuildMenuUi();
        gameObject.SetActive(false);
    }

    [HideFromIl2Cpp]
    private void Open()
    {
        SyncInputsFromPreferences();
        activeTab = MainMenuTab.Home;
        confirmingJoin = false;
        feedback = string.Empty;
        gameObject.SetActive(true);
        RefreshContent();
    }

    [HideFromIl2Cpp]
    private void Close()
    {
        confirmingJoin = false;
        feedback = string.Empty;
        ClearContent();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!gameObject.activeSelf || !statusText)
            return;

        SyncInputFields();
        statusText.text = GetStatusText();
    }

    [HideFromIl2Cpp]
    private void BuildMenuUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20020;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        var background = new GameObject("Background");
        background.transform.SetParent(transform, false);
        Stretch(background.AddComponent<RectTransform>());
        background.AddComponent<Image>().color = new Color(0.02f, 0.025f, 0.035f, 0.58f);

        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.105f, 0.135f, 0.18f, 0.97f);

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

        var title = titleObject.AddComponent<TextMeshProUGUI>();
        title.text = "SR2MP Multiplayer";
        title.fontSize = 34f;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.enableWordWrapping = false;

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

        AddTabButton(tabs.transform, MainMenuTab.Home, "Home");
        AddTabButton(tabs.transform, MainMenuTab.Host, "Host Save");
        AddTabButton(tabs.transform, MainMenuTab.Join, "Join Server");
        AddTabButton(tabs.transform, MainMenuTab.Settings, "Settings");
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
        layout.spacing = 12f;
        layout.padding = new RectOffset(0, 0, 8, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        contentRoot = content.transform;
    }

    [HideFromIl2Cpp]
    private void AddTabButton(Transform parent, MainMenuTab tab, string label)
    {
        CreateButton(parent, $"Tab_{label}", label, () =>
        {
            SyncInputFields();
            activeTab = tab;
            confirmingJoin = false;
            feedback = string.Empty;
            RefreshContent();
        }, ButtonVisual.Secondary, 0f, true);
    }

    [HideFromIl2Cpp]
    private void RefreshContent()
    {
        ClearContent();
        statusText.text = GetStatusText();

        switch (activeTab)
        {
            case MainMenuTab.Home:
                DrawHome();
                break;
            case MainMenuTab.Host:
                DrawHost();
                break;
            case MainMenuTab.Join:
                DrawJoin();
                break;
            case MainMenuTab.Settings:
                DrawSettings();
                break;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
            AddText(feedback, 22f, new Color(1f, 0.9f, 0.62f, 1f), 72f);
    }

    [HideFromIl2Cpp]
    private void DrawHome()
    {
        AddText("Multiplayer", 28f, Color.white, 42f);
        AddText("Choose a save to host or prepare a client save before joining a hosted world.",
            21f,
            new Color(0.78f, 0.88f, 0.95f, 1f),
            64f);

        var row = AddRow(58f);
        CreateButton(row.transform, "HostSaveButton", "Host Save", () =>
        {
            activeTab = MainMenuTab.Host;
            RefreshContent();
        }, ButtonVisual.Primary);
        CreateButton(row.transform, "JoinServerButton", "Join Server", () =>
        {
            activeTab = MainMenuTab.Join;
            RefreshContent();
        }, ButtonVisual.Secondary);
        AddFlexibleSpace(row.transform);

        DrawRecentServers();
    }

    [HideFromIl2Cpp]
    private void DrawHost()
    {
        AddText("Host Save", 28f, Color.white, 42f);
        AddText("Choose host settings here, then select the save through SR2's normal Load Game menu. SR2MP will start hosting after the save loads.",
            21f,
            new Color(0.78f, 0.88f, 0.95f, 1f),
            86f);

        hostPortField = AddInputRow("Port", hostPortInput, "1919", 5);

        var cheatsRow = AddRow(56f);
        AddRowLabel(cheatsRow.transform, "Allow Cheats");
        CreateButton(cheatsRow.transform,
            "HostAllowCheatsButton",
            allowCheatsInput ? "Yes" : "No",
            () =>
            {
                SyncInputFields();
                allowCheatsInput = !allowCheatsInput;
                RefreshContent();
            },
            ButtonVisual.Secondary,
            170f);
        AddFlexibleSpace(cheatsRow.transform);

        bool validPort = ushort.TryParse(hostPortInput, out ushort hostPort) && hostPort > 0;
        if (!validPort)
            AddText("Invalid port. Use a number from 1 to 65535.", 21f, new Color(1f, 0.76f, 0.6f, 1f), 38f);
        else if (!CanStartNetworkAction())
            AddText(GetUnavailableActionText(), 21f, new Color(1f, 0.76f, 0.6f, 1f), 38f);
        else if (MultiplayerLaunchCoordinator.IsHostSaveSelectionArmed)
            AddText("Host save selection is armed. Select a save in SR2's Load Game menu.", 21f, new Color(0.74f, 0.94f, 0.9f, 1f), 38f);
        else
            AddText($"Ready to host on port {hostPort}.", 21f, new Color(0.74f, 0.94f, 0.9f, 1f), 38f);

        var row = AddRow(58f);
        if (MultiplayerLaunchCoordinator.IsHostSaveSelectionArmed)
        {
            CreateButton(row.transform, "CancelHostSelectionButton", "Cancel", CancelPendingSelection, ButtonVisual.Secondary);
        }
        else
        {
            CreateButton(row.transform, "SelectHostSaveButton", "Select Save", BeginHostSaveSelection, ButtonVisual.Primary);
        }

        AddFlexibleSpace(row.transform);
    }

    [HideFromIl2Cpp]
    private void BeginHostSaveSelection()
    {
        SyncInputFields();
        if (!ushort.TryParse(hostPortInput, out ushort hostPort) || hostPort == 0)
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

        Main.SetConfigValue("host_port", hostPort.ToString());
        Main.SetConfigValue("allow_cheats", allowCheatsInput);

        var settings = new ServerLaunchSettings
        {
            AllowCheats = allowCheatsInput,
        };

        if (!MultiplayerLaunchCoordinator.BeginHostSaveSelection(hostPort, settings))
        {
            feedback = MultiplayerLaunchCoordinator.StatusMessage;
            RefreshContent();
            return;
        }

        Close();
        OpenNativeLoadGameMenuAfterClose("host");
    }

    [HideFromIl2Cpp]
    private void DrawJoin()
    {
        if (confirmingJoin)
        {
            DrawJoinConfirmation();
            return;
        }

        AddText("Join Server", 28f, Color.white, 42f);
        AddText("Enter a server address, then choose a client save through SR2's normal Load Game menu.",
            21f,
            new Color(0.78f, 0.88f, 0.95f, 1f),
            86f);

        joinAddressField = AddInputRow("Server address", joinAddressInput, "127.0.0.1:1919", 160);

        bool validAddress = ServerAddressParser.TryParse(joinAddressInput, hostPortInput, out var address, out var addressError);
        if (!validAddress)
            AddText(addressError, 21f, new Color(1f, 0.76f, 0.6f, 1f), 38f);
        else if (!CanStartNetworkAction())
            AddText(GetUnavailableActionText(), 21f, new Color(1f, 0.76f, 0.6f, 1f), 38f);
        else if (MultiplayerLaunchCoordinator.IsJoinSaveSelectionArmed)
            AddText("Join save selection is armed. Select a client save in SR2's Load Game menu.", 21f, new Color(0.74f, 0.94f, 0.9f, 1f), 38f);
        else
            AddText($"Ready to join {address.Display}.", 21f, new Color(0.74f, 0.94f, 0.9f, 1f), 38f);

        var row = AddRow(58f);
        if (MultiplayerLaunchCoordinator.IsJoinSaveSelectionArmed)
        {
            CreateButton(row.transform, "CancelJoinSelectionButton", "Cancel", CancelPendingSelection, ButtonVisual.Secondary);
        }
        else
        {
            CreateButton(row.transform, "SelectJoinSaveButton", "Select Client Save", HandleJoinPressed, ButtonVisual.Primary, 330f);
        }

        AddFlexibleSpace(row.transform);

        DrawRecentServers();
    }

    [HideFromIl2Cpp]
    private void DrawJoinConfirmation()
    {
        AddText("Use an empty client save", 28f, Color.white, 42f);
        AddText($"Server: {pendingJoinAddress.Display}", 22f, new Color(0.86f, 0.92f, 1f, 1f), 38f);
        AddText("Joining will sync the selected save with the hosted world. Use a fresh client save to avoid losing local progress.",
            21f,
            new Color(1f, 0.9f, 0.62f, 1f),
            76f);

        var row = AddRow(58f);
        CreateButton(row.transform, "ConfirmJoinSaveButton", "Select Save", BeginJoinSaveSelection, ButtonVisual.Primary, 260f);
        CreateButton(row.transform, "BackJoinButton", "Back", () =>
        {
            confirmingJoin = false;
            feedback = string.Empty;
            RefreshContent();
        }, ButtonVisual.Secondary, 190f);
        AddFlexibleSpace(row.transform);
    }

    [HideFromIl2Cpp]
    private void DrawSettings()
    {
        AddText("Settings", 28f, Color.white, 42f);
        usernameField = AddInputRow("Username", usernameInput, "Player", 32);

        var cheatsRow = AddRow(56f);
        AddRowLabel(cheatsRow.transform, "Allow Cheats");
        CreateButton(cheatsRow.transform,
            "SettingsAllowCheatsButton",
            allowCheatsInput ? "Yes" : "No",
            () =>
            {
                SyncInputFields();
                allowCheatsInput = !allowCheatsInput;
                RefreshContent();
            },
            ButtonVisual.Secondary,
            170f);
        AddFlexibleSpace(cheatsRow.transform);

        AddText($"Default host port: {hostPortInput}", 21f, new Color(0.86f, 0.92f, 1f, 1f), 36f);

        var saveRow = AddRow(58f);
        CreateButton(saveRow.transform, "SaveSettingsButton", "Save Settings", HandleSaveSettingsPressed, ButtonVisual.Primary);
        AddFlexibleSpace(saveRow.transform);
    }

    [HideFromIl2Cpp]
    private void DrawRecentServers()
    {
        int maxServers = activeTab == MainMenuTab.Join ? 2 : 3;
        var recentServers = RecentServerService.Load().Take(maxServers).ToList();
        if (recentServers.Count == 0)
            return;

        AddText("Recent servers", 22f, new Color(0.86f, 0.92f, 1f, 1f), 34f);
        foreach (var recentServer in recentServers)
        {
            var row = AddRow(48f);
            CreateButton(row.transform, $"Recent_{recentServer.Display}", recentServer.Display, () =>
            {
                joinAddressInput = recentServer.Display;
                activeTab = MainMenuTab.Join;
                confirmingJoin = false;
                feedback = string.Empty;
                RefreshContent();
            }, ButtonVisual.Secondary, 420f);
            AddFlexibleSpace(row.transform);
        }
    }

    [HideFromIl2Cpp]
    private static string GetStatusText()
    {
        if (MultiplayerLaunchCoordinator.State is not MultiplayerLaunchState.Idle)
            return MultiplayerLaunchCoordinator.StatusMessage;

        return "Main-menu multiplayer setup is ready.";
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
    private void BeginJoinSaveSelection()
    {
        if (!CanStartNetworkAction())
        {
            feedback = GetUnavailableActionText();
            confirmingJoin = false;
            RefreshContent();
            return;
        }

        if (!MultiplayerLaunchCoordinator.BeginJoinSaveSelection(pendingJoinAddress))
        {
            feedback = MultiplayerLaunchCoordinator.StatusMessage;
            confirmingJoin = false;
            RefreshContent();
            return;
        }

        confirmingJoin = false;
        Close();
        OpenNativeLoadGameMenuAfterClose("join");
    }

    [HideFromIl2Cpp]
    private static void OpenNativeLoadGameMenuAfterClose(string flow)
    {
        SrLogger.LogMessage($"SR2MP {flow} save selection armed; opening SR2 Load Game menu.", SrLogTarget.Both);
        ActionsEUtil.ExecuteInTicks((Action)(() => TryOpenNativeLoadGameMenu(flow, 1)), 1);
    }

    [HideFromIl2Cpp]
    private static void TryOpenNativeLoadGameMenu(string flow, int attempt)
    {
        try
        {
            if (TryInvokeNativeLoadGameBehavior())
            {
                SrLogger.LogMessage($"Opened SR2 Load Game menu for SR2MP {flow} save selection.", SrLogTarget.Both);
                return;
            }
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Failed to open SR2 Load Game menu for SR2MP {flow} save selection: {ex}", SrLogTarget.Both);
            return;
        }

        if (attempt < NativeLoadGameOpenMaxAttempts)
        {
            ActionsEUtil.ExecuteInTicks((Action)(() => TryOpenNativeLoadGameMenu(flow, attempt + 1)), 1);
            return;
        }

        SrLogger.LogWarning(
            $"Could not find SR2's native Load Game button for SR2MP {flow} save selection. Save selection remains armed; open Load Game manually.",
            SrLogTarget.Both);
    }

    [HideFromIl2Cpp]
    private static bool TryInvokeNativeLoadGameBehavior()
    {
        var landingRoot = Object.FindObjectOfType<MainMenuLandingRootUI>();
        if (!landingRoot)
            return false;

        var models = landingRoot._models;
        if (models == null || models.Count == 0)
            return false;

        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            if (model == null)
                continue;

            if (model.TryCast<ContinueGameBehaviorModel>() != null)
                continue;

            var loadGameModel = model.TryCast<LoadGameBehaviorModel>();
            if (loadGameModel == null || IsSr2eCustomMainMenuDefinition(loadGameModel.Definition))
                continue;

            loadGameModel.InvokeBehavior();
            return true;
        }

        return false;
    }

    [HideFromIl2Cpp]
    private static bool IsSr2eCustomMainMenuDefinition(ButtonBehaviorDefinition? definition)
    {
        if (definition == null)
            return false;

        string? il2CppTypeName = null;
        try
        {
            il2CppTypeName = definition.GetIl2CppType()?.FullName;
        }
        catch (Exception ex)
        {
            SrLogger.LogDebug($"Could not resolve main-menu definition IL2CPP type: {ex.GetType().Name}", SrLogTarget.Sensitive);
        }

        var managedTypeName = definition.GetType().FullName;
        return IsSr2eCustomMainMenuDefinitionType(il2CppTypeName)
            || IsSr2eCustomMainMenuDefinitionType(managedTypeName);
    }

    [HideFromIl2Cpp]
    private static bool IsSr2eCustomMainMenuDefinitionType(string? typeName)
        => typeName is "SR2E.Buttons.CustomMainMenuItemDefinition" or "SR2E.Buttons.CustomMainMenuSubItemDefinition";

    [HideFromIl2Cpp]
    private void CancelPendingSelection()
    {
        MultiplayerLaunchCoordinator.Cancel("Main-menu multiplayer save selection cancelled.");
        confirmingJoin = false;
        feedback = "Save selection cancelled.";
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
        if (ushort.TryParse(hostPortInput, out ushort hostPort) && hostPort > 0)
            Main.SetConfigValue("host_port", hostPort.ToString());

        feedback = "Settings saved.";
        RefreshContent();
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
            joinAddressInput = joinAddressField.text?.Trim() ?? string.Empty;

        if (hostPortField is not null)
            hostPortInput = hostPortField.text?.Trim() ?? string.Empty;

        if (usernameField is not null)
            usernameInput = usernameField.text?.Trim() ?? string.Empty;
    }

    [HideFromIl2Cpp]
    private void ClearInputReferences()
    {
        joinAddressField = null;
        hostPortField = null;
        usernameField = null;
    }

    [HideFromIl2Cpp]
    private static bool CanStartNetworkAction()
    {
        return !Main.Server.IsRunning() && !Main.Client.IsConnected && !Main.Client.IsConnectionPending;
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
    private GameObject AddRow(float height)
    {
        var row = new GameObject("Row");
        row.transform.SetParent(contentRoot, false);
        row.AddComponent<RectTransform>();
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        var layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;

        contentItems.Add(row);
        return row;
    }

    [HideFromIl2Cpp]
    private void AddText(string text, float fontSize, Color color, float height)
    {
        var textObject = new GameObject("Text");
        textObject.transform.SetParent(contentRoot, false);
        textObject.AddComponent<RectTransform>();

        var tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = true;

        var layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;

        contentItems.Add(textObject);
    }

    [HideFromIl2Cpp]
    private TMP_InputField AddInputRow(string label, string value, string placeholder, int characterLimit)
    {
        var row = AddRow(58f);
        AddRowLabel(row.transform, label);
        return CreateInput(row.transform, $"Input_{label}", value, placeholder, characterLimit);
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
        Stretch(textObject.AddComponent<RectTransform>());
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 23f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        input.textComponent = text;

        var placeholderObject = new GameObject("Placeholder");
        placeholderObject.transform.SetParent(viewportObject.transform, false);
        Stretch(placeholderObject.AddComponent<RectTransform>());
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
            _ => new Color(0.17f, 0.23f, 0.31f, 1f),
        };

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = CreateButtonColors(visual);
        button.onClick.AddListener((Action)onClick);

        var layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = flexibleWidth ? 0f : Math.Min(preferredWidth, ActionButtonWidth);
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.minHeight = 52f;
        layoutElement.preferredHeight = 52f;
        layoutElement.flexibleWidth = flexibleWidth ? 1f : 0f;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);
        Stretch(textObject.AddComponent<RectTransform>());

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
    private void ClearContent()
    {
        ClearInputReferences();

        foreach (var item in contentItems)
        {
            if (item)
                Object.Destroy(item);
        }

        contentItems.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null!;
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

    private enum ButtonVisual : byte
    {
        Primary,
        Secondary,
    }
}
