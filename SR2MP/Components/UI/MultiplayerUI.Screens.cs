using SR2MP.Shared.Utils;

namespace SR2MP.Components.UI;

public sealed partial class MultiplayerUI
{
    private bool multiplayerUIHidden;
    private string usernameInput = "Player";
    private string ipInput = string.Empty;
    private string portInput = string.Empty;
    private string hostPortInput = "1919";
    private bool allowCheatsInput;
    private string uiStatus = string.Empty;
    private MultiplayerTab activeTab = MultiplayerTab.Join;
    private ConnectionPhase connectionPhase = ConnectionPhase.Idle;
    private bool confirmingJoin;
    private ServerAddress pendingJoinAddress;
    private readonly List<ServerAddress> recentServers = new();

    private void FirstTimeScreen()
    {
        bool valid = true;

        DrawText("Choose the name other players will see.");

        DrawText("Username:", 2);
        usernameInput = DrawTextInput(CalculateInputLayout(6, 2, 1), usernameInput, 32, UsernameInputName);

        if (string.IsNullOrWhiteSpace(usernameInput))
        {
            DrawText("You must set an Username first.");
            valid = false;
        }

        if (!valid) return;
        if (!GUI.Button(CalculateButtonLayout(6), "Save settings")) return;

        firstTime = false;
        activeTab = MultiplayerTab.Join;
        Main.SetConfigValue("internal_setup_ui", false);
        Main.SetConfigValue("username", usernameInput);
    }

    private void SettingsScreen()
    {
        bool validUsername = true;

        DrawText("Settings");
        DrawSettingsFields();

        if (string.IsNullOrWhiteSpace(usernameInput))
        {
            DrawText("You must set an Username.");
            validUsername = false;
        }

        if (DrawEnabledButton("Save", validUsername, 2, 0))
        {
            SaveSettings();
            viewingSettings = false;
        }

        if (GUI.Button(CalculateButtonLayout(6, 2, 1), "Back"))
            viewingSettings = false;
    }

    private void MainMenuScreen()
    {
        if (GUI.Button(CalculateButtonLayout(6, 2, 0), "Settings"))
            viewingSettings = true;

        if (GUI.Button(CalculateButtonLayout(6, 2, 1), "Hide"))
            HideHub();

        DrawText("Load into a save to host or join a world.");
        DrawText("When joining, use an empty client save because the joining save is reset.");
    }

    private void InGameScreen()
    {
        DrawHubScreen();
    }

    private void UnimplementedScreen()
    {
        DrawText("This screen hasn't been implemented yet.");
    }

    private void HostingScreen()
    {
        DrawHubScreen();
    }

    private void ConnectedScreen()
    {
        DrawHubScreen();
    }

    private void DrawHubScreen()
    {
        DrawHubActions();
        DrawConnectionSummary();
        DrawTabBar();

        switch (activeTab)
        {
            case MultiplayerTab.Join:
                DrawJoinTab();
                break;
            case MultiplayerTab.Host:
                DrawHostTab();
                break;
            case MultiplayerTab.Players:
                DrawPlayersTab();
                break;
            case MultiplayerTab.Settings:
                DrawSettingsTab();
                break;
        }
    }

    private void DrawHubActions()
    {
        if (GUI.Button(CalculateButtonLayout(6, 2, 0), "Hide"))
            HideHub();

        if (Main.Server.IsRunning())
        {
            if (GUI.Button(CalculateButtonLayout(6, 2, 1), "Close Host"))
            {
                SetConnectionStatus(ConnectionPhase.Stopping, "Closing hosted world...");
                Main.Server.Close();
            }
        }
        else if (Main.Client.IsConnected)
        {
            string label = Main.Client.IsConnectionPending ? "Cancel" : "Disconnect";
            if (GUI.Button(CalculateButtonLayout(6, 2, 1), label))
            {
                SetConnectionStatus(ConnectionPhase.Stopping,
                    Main.Client.IsConnectionPending ? "Cancelling connection..." : "Disconnecting...");
                Main.Client.Disconnect(Main.Client.IsConnectionPending
                    ? "Connection cancelled."
                    : "You disconnected from the world.");
            }
        }
        else
        {
            if (GUI.Button(CalculateButtonLayout(6, 2, 1), "Settings"))
            {
                confirmingJoin = false;
                activeTab = MultiplayerTab.Settings;
            }
        }
    }

    private void DrawConnectionSummary()
    {
        DrawText(GetConnectionStatusText());

        if (!string.IsNullOrWhiteSpace(uiStatus))
            DrawText(uiStatus);
    }

    private void DrawTabBar()
    {
        if (GUI.Button(CalculateButtonLayout(6, 4, 0), GetTabText(MultiplayerTab.Join, "Join")))
            activeTab = MultiplayerTab.Join;
        if (GUI.Button(CalculateButtonLayout(6, 4, 1), GetTabText(MultiplayerTab.Host, "Host")))
        {
            confirmingJoin = false;
            activeTab = MultiplayerTab.Host;
        }
        if (GUI.Button(CalculateButtonLayout(6, 4, 2), GetTabText(MultiplayerTab.Players, "Players")))
        {
            confirmingJoin = false;
            activeTab = MultiplayerTab.Players;
        }
        if (GUI.Button(CalculateButtonLayout(6, 4, 3), GetTabText(MultiplayerTab.Settings, "Settings")))
        {
            confirmingJoin = false;
            activeTab = MultiplayerTab.Settings;
        }
    }

    private void DrawJoinTab()
    {
        if (confirmingJoin)
        {
            DrawJoinConfirmation();
            return;
        }

        DrawText("Join a world");

        DrawText("Server", 2);
        ipInput = DrawTextInput(CalculateInputLayout(6, 2, 1), ipInput, 128, IpInputName);

        DrawText("Port", 2);
        portInput = DrawTextInput(CalculateInputLayout(6, 2, 1), portInput, 5, PortInputName);

        bool validAddress = ServerAddressParser.TryParse(ipInput, portInput, out var serverAddress, out var addressError);
        bool canConnect = validAddress && !Main.Server.IsRunning() && !Main.Client.IsConnected && !IsBusy();

        if (!validAddress)
            DrawText(addressError);
        else if (Main.Server.IsRunning())
            DrawText("Close your hosted world before joining another one.");
        else if (Main.Client.IsConnected)
            DrawText("Disconnect before joining another world.");
        else if (IsBusy())
            DrawText("Please wait until the current multiplayer action finishes.");
        else
            DrawText($"Ready to join {serverAddress.Display}.");

        if (DrawEnabledButton("Continue", canConnect))
            BeginJoinConfirmation(serverAddress);

        DrawRecentServers();
    }

    private void DrawJoinConfirmation()
    {
        DrawText("Join hosted world");
        DrawText($"Server: {pendingJoinAddress.Display}");
        DrawText("Use an empty client save. Joining resets this save to match the hosted world.");

        bool canJoin = !Main.Server.IsRunning() && !Main.Client.IsConnected && !IsBusy();
        if (DrawEnabledButton("Join", canJoin, 2, 0))
        {
            confirmingJoin = false;
            Connect(pendingJoinAddress);
        }

        if (GUI.Button(CalculateButtonLayout(6, 2, 1), "Back"))
        {
            confirmingJoin = false;
        }
    }

    private void DrawRecentServers()
    {
        if (recentServers.Count == 0 || Main.Server.IsRunning() || Main.Client.IsConnected || IsBusy())
            return;

        DrawText("Recent servers");

        int shownServers = Math.Min(recentServers.Count, 3);
        for (int i = 0; i < shownServers; i++)
        {
            var recentServer = recentServers[i];
            if (GUI.Button(CalculateButtonLayout(6), recentServer.Display))
            {
                ipInput = recentServer.Host;
                portInput = recentServer.Port.ToString();
                BeginJoinConfirmation(recentServer);
            }
        }
    }

    private void DrawHostTab()
    {
        DrawText("Host this save");

        DrawText("Port", 2);
        hostPortInput = DrawTextInput(CalculateInputLayout(6, 2, 1), hostPortInput, 5, HostPortInputName);

        bool validHostPort = ushort.TryParse(hostPortInput, out var hostPort) && hostPort > 0;
        bool canHost = validHostPort && !Main.Server.IsRunning() && !Main.Client.IsConnected && !IsBusy();

        if (!validHostPort)
        {
            DrawText("Invalid port. Must be a number from 1 to 65535.");
            DrawText("Make sure your pc doesn't use the port anywhere else.");
        }
        else if (Main.Server.IsRunning())
        {
            DrawText($"Already hosting on port {Main.Server.Port}.");
        }
        else if (Main.Client.IsConnected)
        {
            DrawText("Disconnect before hosting your own world.");
        }
        else if (IsBusy())
        {
            DrawText("Please wait until the current multiplayer action finishes.");
        }

        if (DrawEnabledButton("Host", canHost))
            Host(hostPort);
    }

    private void DrawPlayersTab()
    {
        var players = playerManager.GetAllPlayers();

        DrawText("Players");
        DrawText($"{Main.Username} (you)");

        if (players.Count == 0)
        {
            DrawText("No other players connected.");
            return;
        }

        int shownPlayers = 0;
        foreach (var player in players)
        {
            if (shownPlayers >= 8)
            {
                DrawText($"+ {players.Count - shownPlayers} more");
                break;
            }

            DrawText(!string.IsNullOrWhiteSpace(player.Username) ? player.Username : "Invalid username.");
            shownPlayers++;
        }
    }

    private void DrawSettingsTab()
    {
        DrawText("Settings");
        DrawSettingsFields();

        bool validUsername = !string.IsNullOrWhiteSpace(usernameInput);
        if (!validUsername)
            DrawText("You must set an Username.");

        if (DrawEnabledButton("Save Settings", validUsername))
            SaveSettings();
    }

    private void DrawSettingsFields()
    {
        DrawText("Username:", 2);
        usernameInput = DrawTextInput(CalculateInputLayout(6, 2, 1), usernameInput, 32, UsernameInputName);

        DrawText("Allow Cheats:", 2);
        if (GUI.Button(CalculateButtonLayout(6, 2, 1), allowCheatsInput.ToStringYesOrNo()))
            allowCheatsInput = !allowCheatsInput;
    }

    private void SaveSettings()
    {
        Main.SetConfigValue("username", usernameInput);
        Main.SetConfigValue("allow_cheats", allowCheatsInput);
        uiStatus = "Settings saved.";
    }

    private void HideHub()
    {
        multiplayerUIHidden = true;
        focusedTextInput = string.Empty;
    }

    private bool DrawEnabledButton(string label, bool enabled, int horizontalShare = 1, int horizontalIndex = 0)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && enabled;
        bool clicked = GUI.Button(CalculateButtonLayout(6, horizontalShare, horizontalIndex), label);
        GUI.enabled = wasEnabled;

        return clicked;
    }

    private string GetTabText(MultiplayerTab tab, string label)
    {
        return tab == activeTab ? $"[{label}]" : label;
    }

    private static string GetConnectionStatusText()
    {
        if (Main.Server.IsRunning())
            return $"Hosting on port {Main.Server.Port} - {Main.Server.GetClientCount()} client(s) connected.";

        if (Main.Client.IsConnectionPending)
            return "Joining hosted world...";

        if (Main.Client.IsConnected)
            return "Connected to a hosted world.";

        return "Ready to host or join.";
    }

    private bool IsBusy()
    {
        return connectionPhase is ConnectionPhase.ResolvingAddress
            or ConnectionPhase.Connecting
            or ConnectionPhase.Synchronizing
            or ConnectionPhase.StartingHost
            or ConnectionPhase.Stopping;
    }

    private void SetConnectionStatus(ConnectionPhase phase, string message)
    {
        connectionPhase = phase;
        uiStatus = message;
    }
}
