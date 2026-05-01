using System.Net;
using SR2MP.Components.UI;
using SR2MP.Packets;
using SR2MP.Packets.Player;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Server.Models;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Server;

public sealed class Server
{
    private readonly NetworkManager networkManager;
    private readonly ClientManager clientManager;
    private readonly PacketManager packetManager;

    private Timer? timeoutTimer;
    private int closeInProgress;
    private readonly Dictionary<string, float> reliableFailureRepairTimes = new();
    private const float ReliableFailureRepairCooldownSeconds = 5f;

    // Just here so that the port is viewable.
    public int Port { get; private set; }

    public event Action? OnServerStarted;
    public event Action<string>? OnServerStartFailed;
    public event Action? OnServerStopped;

    public Server()
    {
        networkManager = new NetworkManager();
        clientManager = new ClientManager();
        packetManager = new PacketManager(networkManager, clientManager);

        networkManager.OnDataReceived += OnDataReceived;
        networkManager.OnReliablePacketFailed += OnReliablePacketFailed;
        clientManager.OnClientRemoved += OnClientRemoved;
    }

    public int GetClientCount() => clientManager.ClientCount;

    public bool IsRunning() => networkManager.IsRunning;

    public bool Start(int port, bool enableIPv6)
    {
        if (Main.Client.IsConnected)
        {
            const string message = "Disconnect before hosting your own world.";
            SrLogger.LogWarning("You are already connected to a server, restart your game to host your own server");
            OnServerStartFailed?.Invoke(message);
            return false;
        }

        if (networkManager.IsRunning)
        {
            SrLogger.LogMessage("Server is already running!", SrLogTarget.Both);
            return true;
        }

        try
        {
            NetworkSessionState.ClearTransientSyncState();
            MainThreadDispatcher.Clear();

            packetManager.RegisterHandlers();
            Application.quitting += new Action(Close);
            networkManager.Start(port, enableIPv6);
            this.Port = port;
            timeoutTimer = new Timer(
                CheckTimeouts,
                null,
                TimeSpan.FromSeconds(HeartbeatSettings.TimeoutCheckSeconds),
                TimeSpan.FromSeconds(HeartbeatSettings.TimeoutCheckSeconds));
            OnServerStarted?.Invoke();
            if (MultiplayerUI.Instance)
            {
                MultiplayerUI.Instance.RegisterSystemMessage(
                    "The world is now open to others!",
                    $"SYSTEM_HOST_START_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    MultiplayerUI.SystemMessageConnect
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Failed to start server: {ex}", SrLogTarget.Both);
            OnServerStartFailed?.Invoke($"Could not host on port {port}. {DescribeStartFailure(ex)}");
            return false;
        }
    }

    private void OnDataReceived(byte[] data, IPEndPoint clientEp)
    {
        SrLogger.LogPacketSize($"Received {data.Length} bytes from Client!",
            $"Received {data.Length} bytes from {clientEp}.");

        try
        {
            packetManager.HandlePacket(data, clientEp);
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Error handling packet from {clientEp}: {ex}", SrLogTarget.Both);
        }
    }

    private void OnClientRemoved(ClientInfo client)
    {
        reliableFailureRepairTimes.Remove(client.GetClientInfo());

        var leavePacket = new PlayerLeavePacket
        {
            Type = PacketType.BroadcastPlayerLeave,
            PlayerId = client.PlayerId
        };

        SendToAll(leavePacket);

        SrLogger.LogMessage($"Player left broadcast sent for: {client.PlayerId}", SrLogTarget.Both);
    }

    private void OnReliablePacketFailed(ReliablePacketFailure failure)
    {
        MainThreadDispatcher.Enqueue(() => HandleReliablePacketFailed(failure));
    }

    private void HandleReliablePacketFailed(ReliablePacketFailure failure)
    {
        if (!networkManager.IsRunning)
            return;

        if (!clientManager.TryGetClient(failure.Destination, out var client) || client == null)
            return;

        var packetType = (PacketType)failure.PacketType;
        if (!client.InitialSyncComplete || IsInitialSyncPacket(packetType))
        {
            SrLogger.LogWarning(
                $"Reliable initial-sync packet {packetType} failed for {client.PlayerId}; removing client so they can retry a clean join.",
                SrLogTarget.Both);
            clientManager.RemoveClient(client.EndPoint);
            return;
        }

        var clientInfo = client.GetClientInfo();
        if (reliableFailureRepairTimes.TryGetValue(clientInfo, out var lastRepairAt)
            && Time.realtimeSinceStartup - lastRepairAt < ReliableFailureRepairCooldownSeconds)
        {
            SrLogger.LogWarning(
                $"Reliable packet {packetType} failed for {client.PlayerId}; repair snapshot already requested recently.",
                SrLogTarget.Both);
            return;
        }

        reliableFailureRepairTimes[clientInfo] = Time.realtimeSinceStartup;
        if (!WorldStateRepairManager.RequestRepairSnapshot($"reliable packet {packetType} failed for {client.PlayerId}"))
        {
            SrLogger.LogWarning(
                $"Reliable packet {packetType} failed for {client.PlayerId}, but repair snapshot is not available right now.",
                SrLogTarget.Both);
        }
    }

    private void CheckTimeouts(object? state)
    {
        try
        {
            clientManager.RemoveTimedOutClients();
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Error checking client timeouts: {ex}", SrLogTarget.Both);
        }
    }

    public void Close()
    {
        if (System.Threading.Interlocked.Exchange(ref closeInProgress, 1) == 1)
            return;

        try
        {
            if (!networkManager.IsRunning)
                return;

            var closeChatMessage = new ChatMessagePacket
            {
                Username = "SYSTEM",
                Message = "Server closed!",
                MessageID = $"SYSTEM_CLOSE_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                MessageType = MultiplayerUI.SystemMessageClose
            };
            SendToAll(closeChatMessage);

            if (MultiplayerUI.Instance)
            {
                MultiplayerUI.Instance.ClearChatMessages();
                MultiplayerUI.Instance.RegisterSystemMessage("You closed the server!", $"SYSTEM_CLOSE_HOST_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", MultiplayerUI.SystemMessageClose);
            }

            timeoutTimer?.Dispose();
            timeoutTimer = null;

            var closePacket = new ClosePacket();

            try
            {
                SendToAll(closePacket);
            }
            catch (Exception ex)
            {
                SrLogger.LogWarning($"Failed to broadcast server close: {ex}");
            }

            var allPlayerIds = playerManager.GetAllPlayers().Select(p => p.PlayerId).ToList();
            foreach (var playerId in allPlayerIds)
            {
                if (playerObjects.TryGetValue(playerId, out var playerObject))
                {
                    if (playerObject != null)
                    {
                        Object.Destroy(playerObject);
                        SrLogger.LogPacketSize($"Destroyed player object for {playerId}", SrLogTarget.Both);
                    }
                    playerObjects.Remove(playerId);
                }
            }

            NetworkSessionState.ClearTransientSyncState();
            clientManager.Clear();
            playerManager.Clear();
            networkManager.Stop();
            MainThreadDispatcher.Clear();

            SrLogger.LogMessage("Server closed", SrLogTarget.Both);
            OnServerStopped?.Invoke();
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Error during server shutdown: {ex}", SrLogTarget.Both);
        }
        finally
        {
            System.Threading.Volatile.Write(ref closeInProgress, 0);
        }
    }

    public ushort? SendToClient<T>(T packet, IPEndPoint endPoint) where T : IPacket
    {
        var perfStart = PerformanceDiagnostics.IsEnabled ? PerformanceDiagnostics.GetTimestamp() : 0;
        using var writer = new PacketWriter();
        writer.WritePacket(packet);
        var data = writer.ToArray();
        PerformanceDiagnostics.RecordServerSendToClient((byte)packet.Type, data.Length, PerformanceDiagnostics.GetElapsedTicks(perfStart));
        return networkManager.Send(data, endPoint, packet.Reliability);
    }

    public ushort? SendToClient<T>(T packet, ClientInfo client) where T : IPacket
    {
        return SendToClient(packet, client.EndPoint);
    }

    public void SendToAll<T>(T packet) where T : IPacket
    {
        var clientCount = clientManager.ClientCount;
        var perfStart = PerformanceDiagnostics.IsEnabled ? PerformanceDiagnostics.GetTimestamp() : 0;
        using var writer = new PacketWriter();
        writer.WritePacket(packet);
        byte[] data = writer.ToArray();
        PerformanceDiagnostics.RecordServerSendToAll((byte)packet.Type, packet.Reliability, clientCount, data.Length, PerformanceDiagnostics.GetElapsedTicks(perfStart));

        var readyEndpoints = new List<IPEndPoint>();
        foreach (var client in clientManager.GetAllClients())
        {
            if (ShouldQueueForInitialSync(client, data, packet.Reliability))
                continue;

            readyEndpoints.Add(client.EndPoint);
        }

        networkManager.Broadcast(data, readyEndpoints, packet.Reliability);
    }

    public void SendToAllExcept<T>(T packet, string excludedClientInfo) where T : IPacket
    {
        var perfStart = PerformanceDiagnostics.IsEnabled ? PerformanceDiagnostics.GetTimestamp() : 0;
        using var writer = new PacketWriter();
        writer.WritePacket(packet);
        byte[] data = writer.ToArray();
        PerformanceDiagnostics.RecordServerSendToAllExcept((byte)packet.Type, data.Length, PerformanceDiagnostics.GetElapsedTicks(perfStart));

        foreach (var client in clientManager.GetAllClients())
        {
            if (client.GetClientInfo() != excludedClientInfo)
            {
                if (!ShouldQueueForInitialSync(client, data, packet.Reliability))
                    networkManager.Send(data, client.EndPoint, packet.Reliability);
            }
        }
    }

    public void SendToAllExcept<T>(T packet, IPEndPoint excludeEndPoint) where T : IPacket
    {
        string clientInfo = $"{excludeEndPoint.Address}:{excludeEndPoint.Port}";
        SendToAllExcept(packet, clientInfo);
    }

    public int GetPendingReliablePackets() => networkManager.GetPendingReliablePackets();

    public bool AreReliablePacketsPending(IPEndPoint destination, IEnumerable<ushort> packetIds)
    {
        foreach (var packetId in packetIds)
        {
            if (networkManager.IsReliablePacketPending(destination, packetId))
                return true;
        }

        return false;
    }

    public void CompleteInitialSync(IPEndPoint endPoint)
    {
        if (!clientManager.TryGetClient(endPoint, out var client) || client == null)
            return;

        var queuedPackets = client.MarkInitialSyncComplete();
        SrLogger.LogMessage(
            $"Initial sync complete for {client.PlayerId}; flushing {queuedPackets.Count} queued packet(s)",
            SrLogTarget.Both);

        foreach (var packet in queuedPackets)
        {
            networkManager.Send(packet.Data, client.EndPoint, packet.Reliability);
        }
    }

    private static bool ShouldQueueForInitialSync(ClientInfo client, byte[] data, PacketReliability reliability)
    {
        if (client.InitialSyncComplete)
            return false;

        if (data.Length == 0 || data[0] == (byte)PacketType.Close)
            return false;

        if (reliability == PacketReliability.Unreliable)
            return true;

        return client.QueueUntilInitialSyncComplete(data, reliability);
    }

    private static bool IsInitialSyncPacket(PacketType packetType)
    {
        return packetType is PacketType.ConnectAck
            or PacketType.InitialActors
            or PacketType.InitialPlots
            or PacketType.InitialPlayerUpgrades
            or PacketType.InitialPediaEntries
            or PacketType.InitialGordos
            or PacketType.InitialSwitches
            or PacketType.InitialMapEntries
            or PacketType.InitialAccessDoors
            or PacketType.InitialWeather
            or PacketType.InitialPuzzleStates
            or PacketType.InitialSyncComplete;
    }

    private static string DescribeStartFailure(Exception ex)
    {
        if (ex is System.Net.Sockets.SocketException socketException)
        {
            return socketException.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse
                ? "That port is already in use."
                : socketException.Message;
        }

        return "Check the SR2MP log for details.";
    }
}
