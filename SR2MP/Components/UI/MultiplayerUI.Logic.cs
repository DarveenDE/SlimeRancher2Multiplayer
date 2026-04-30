using SR2E.Utils;
using SR2MP.Shared.Utils;
using UnityEngine.InputSystem.Utilities;

namespace SR2MP.Components.UI;

public sealed partial class MultiplayerUI
{
    public void OpenHub()
    {
        SrLogger.LogMessage("Opening multiplayer UI hub.", SrLogTarget.Both);
        multiplayerUIHidden = false;
        viewingSettings = false;
        viewingHelp = false;
        confirmingJoin = false;
        openHubDiagnosticFrames = 120;
        activeTab = Main.Server.IsRunning() || Main.Client.IsConnected
            ? MultiplayerTab.Players
            : MultiplayerTab.Join;

        try
        {
            MenuEUtil.CloseOpenMenu();
            NativeEUtil.TryHideMenus();
            NativeEUtil.TryUnPauseGame(true);
        }
        catch (Exception ex)
        {
            SrLogger.LogWarning($"Could not close native menu while opening multiplayer UI: {ex.Message}", SrLogTarget.Both);
        }
    }

    public void CloseHub()
    {
        HideHub();
    }

    public void Host(ushort port)
    {
        SetConnectionStatus(ConnectionPhase.StartingHost, $"Starting host on port {port}...");
        SrLogger.LogMessage($"Host button pressed. Starting server on port {port}.", SrLogTarget.Both);

        MenuEUtil.CloseOpenMenu();
        bool started = Main.Server.Start(port, true);
        if (started)
        {
            Main.SetConfigValue("host_port", hostPortInput);
            SetConnectionStatus(ConnectionPhase.Hosting, $"Hosting on port {port}.");
        }
        else if (connectionPhase != ConnectionPhase.Failed)
        {
            SetConnectionStatus(ConnectionPhase.Failed, "Host did not start. Check SR2MP log.");
        }
    }

    public void Connect(ServerAddress address)
    {
        SetConnectionStatus(ConnectionPhase.ResolvingAddress, $"Resolving {address.Host}...");
        SrLogger.LogMessage($"Connect button pressed. Connecting to {address.Display}.", SrLogTarget.Both);

        MenuEUtil.CloseOpenMenu();

        if (!ServerAddressParser.TryResolve(address, out var resolvedAddress, out var resolveError))
        {
            SrLogger.LogWarning(resolveError, SrLogTarget.Both);
            SetConnectionStatus(ConnectionPhase.Failed, resolveError);
            return;
        }

        bool started = Main.Client.Connect(resolvedAddress.Host, resolvedAddress.Port);
        if (!started)
        {
            if (connectionPhase != ConnectionPhase.Failed)
                SetConnectionStatus(ConnectionPhase.Failed, "Could not start connection. Check SR2MP log.");
            return;
        }

        ipInput = address.Host;
        portInput = address.Port.ToString();

        Main.SetConfigValue("recent_ip", ipInput);
        Main.SetConfigValue("recent_port", portInput);
        RememberRecentServer(address);

        SetConnectionStatus(ConnectionPhase.Synchronizing, $"Waiting for world sync from {address.Display}...");
    }

    private void BeginJoinConfirmation(ServerAddress address)
    {
        pendingJoinAddress = address;
        confirmingJoin = true;
        focusedTextInput = string.Empty;
        uiStatus = string.Empty;
    }

    private void LoadRecentServers()
    {
        recentServers.Clear();

        foreach (var entry in Main.SavedRecentServers.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ServerAddressParser.TryParse(entry, string.Empty, out var address, out _))
                continue;

            recentServers.Add(address);
        }

        TrimRecentServers();
    }

    private void RememberRecentServer(ServerAddress address)
    {
        AddRecentServer(address);
        SaveRecentServers();
    }

    private void AddRecentServer(ServerAddress address)
    {
        for (int i = recentServers.Count - 1; i >= 0; i--)
        {
            var existing = recentServers[i];
            if (existing.Port == address.Port &&
                string.Equals(existing.Host, address.Host, StringComparison.OrdinalIgnoreCase))
            {
                recentServers.RemoveAt(i);
            }
        }

        recentServers.Insert(0, address);
        TrimRecentServers();
    }

    private void TrimRecentServers()
    {
        while (recentServers.Count > MaxRecentServers)
        {
            recentServers.RemoveAt(recentServers.Count - 1);
        }
    }

    private void SaveRecentServers()
    {
        Main.SetConfigValue("recent_servers", string.Join("|", recentServers.Select(server => server.Display)));
    }

    public static void Kick(string player)
    {
        // TODO: Implement kick functionality
    }

    private void Update()
    {
        HandleUIToggle();
        HandleChatToggle();
        HandleChatInput();
    }

    private static void DisableInput()
    {
        GameContext.Instance.InputDirector._mainGame.Map.Disable();
    }

    private static void EnableInput()
    {
        GameContext.Instance.InputDirector._mainGame.Map.Enable();
    }

    private void HandleUIToggle()
    {
        if (KeyCode.F4.OnKeyDown() && !isChatFocused)
        {
            multiplayerUIHidden = !multiplayerUIHidden;
        }
    }

    private void HandleChatToggle()
    {
        if (!KeyCode.F5.OnKeyDown())
            return;
        if (isChatFocused)
        {
            UnfocusChat();
        }

        chatHidden = !chatHidden;
        internalChatToggle = true;

        if (!chatHidden || !disabledInput)
            return;
        EnableInput();
        disabledInput = false;
    }

    private void HandleChatInput()
    {
        if (chatHidden || state == MenuState.DisconnectedMainMenu) return;

        bool enterPressed = KeyCode.Return.OnKeyDown() || KeyCode.KeypadEnter.OnKeyDown();
        bool escapePressed = KeyCode.Escape.OnKeyDown();

        if (isChatFocused)
        {
            if (enterPressed)
            {
                if (!string.IsNullOrWhiteSpace(chatInput))
                {
                    SendChatMessage(chatInput.Trim());
                }
                ClearChatInput();
                UnfocusChat();
            }
            else if (escapePressed)
            {
                ClearChatInput();
                UnfocusChat();
            }
        }
        else
        {
            if (enterPressed)
            {
                FocusChat();
            }
        }
    }

    private void AdjustInputValues()
    {
        ipInput = ipInput.WithAllWhitespaceStripped();
        portInput = portInput.WithAllWhitespaceStripped();
        hostPortInput = hostPortInput.WithAllWhitespaceStripped();
    }
}
