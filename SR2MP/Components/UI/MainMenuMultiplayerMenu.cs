using Il2CppInterop.Runtime.Attributes;
using Il2CppTMPro;
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
    private const float PanelHeight = 610f;
    private const float ActionButtonWidth = 280f;

    public static MainMenuMultiplayerMenu Instance { get; private set; }

    private readonly List<GameObject> contentItems = new();

    private static Sprite? transparentMainMenuIcon;

    private Transform contentRoot = null!;
    private TextMeshProUGUI statusText = null!;
    private MainMenuTab activeTab = MainMenuTab.Home;
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
        activeTab = MainMenuTab.Home;
        feedback = string.Empty;
        gameObject.SetActive(true);
        RefreshContent();
    }

    [HideFromIl2Cpp]
    private void Close()
    {
        feedback = string.Empty;
        ClearContent();
        gameObject.SetActive(false);
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
        layout.spacing = 16f;
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
            activeTab = tab;
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
        AddText("Select a save through SR2's normal Load Game menu. SR2MP will start hosting automatically after that save finishes loading.",
            21f,
            new Color(0.78f, 0.88f, 0.95f, 1f),
            86f);
        AddText($"Default port: {Main.SavedHostPort}", 21f, new Color(0.86f, 0.92f, 1f, 1f), 38f);

        var row = AddRow(58f);
        if (MultiplayerLaunchCoordinator.IsHostSaveSelectionArmed)
        {
            CreateButton(row.transform, "CancelHostSelectionButton", "Cancel", () =>
            {
                MultiplayerLaunchCoordinator.Cancel("Host save selection cancelled.");
                feedback = "Host save selection cancelled.";
                RefreshContent();
            }, ButtonVisual.Secondary);
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
        if (!ushort.TryParse(Main.SavedHostPort, out ushort hostPort) || hostPort == 0)
        {
            feedback = "Default host port is invalid. Set a valid host port in the in-game multiplayer settings first.";
            RefreshContent();
            return;
        }

        if (!MultiplayerLaunchCoordinator.BeginHostSaveSelection(hostPort))
        {
            feedback = MultiplayerLaunchCoordinator.StatusMessage;
            RefreshContent();
            return;
        }

        Close();
    }

    [HideFromIl2Cpp]
    private void DrawJoin()
    {
        AddText("Join Server", 28f, Color.white, 42f);
        AddText("Joining from the main menu will load a client save first, then connect and sync it with the host.",
            21f,
            new Color(0.78f, 0.88f, 0.95f, 1f),
            86f);

        DrawRecentServers();

        var row = AddRow(58f);
        CreateButton(row.transform, "JoinPlaceholderButton", "Enter Address", () =>
        {
            feedback = "Address entry and client-save selection are the next pieces to connect.";
            RefreshContent();
        }, ButtonVisual.Primary);
        AddFlexibleSpace(row.transform);
    }

    [HideFromIl2Cpp]
    private void DrawSettings()
    {
        AddText("Settings", 28f, Color.white, 42f);
        AddText($"Username: {Main.Username}", 21f, new Color(0.86f, 0.92f, 1f, 1f), 36f);
        AddText($"Allow cheats: {Main.AllowCheats.ToStringYesOrNo()}", 21f, new Color(0.86f, 0.92f, 1f, 1f), 36f);
        AddText($"Default host port: {Main.SavedHostPort}", 21f, new Color(0.86f, 0.92f, 1f, 1f), 36f);
    }

    [HideFromIl2Cpp]
    private void DrawRecentServers()
    {
        var recentServers = RecentServerService.Load().Take(3).ToList();
        if (recentServers.Count == 0)
            return;

        AddText("Recent servers", 22f, new Color(0.86f, 0.92f, 1f, 1f), 34f);
        foreach (var recentServer in recentServers)
        {
            var row = AddRow(48f);
            CreateButton(row.transform, $"Recent_{recentServer.Display}", recentServer.Display, () =>
            {
                activeTab = MainMenuTab.Join;
                feedback = $"Selected {recentServer.Display}. Client-save selection is not connected yet.";
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
