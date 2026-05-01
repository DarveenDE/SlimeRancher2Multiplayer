using System.Collections.Concurrent;
using System.Net;
using SR2MP.Server.Models;
using SR2MP.Shared.Utils;

namespace SR2MP.Server.Managers;

public sealed class ClientManager
{
    private readonly ConcurrentDictionary<string, ClientInfo> clients = new();

    public event Action<ClientInfo>? OnClientAdded;
    public event Action<ClientInfo>? OnClientRemoved;

    public int ClientCount => clients.Count;
    public bool AllInitialSyncComplete => clients.Values.All(client => client.InitialSyncComplete);
    public int InitialSyncIncompleteCount => clients.Values.Count(client => !client.InitialSyncComplete);

    public bool TryGetClient(string clientInfo, out ClientInfo? client)
    {
        return clients.TryGetValue(clientInfo, out client);
    }

    public bool TryGetClient(IPEndPoint endPoint, out ClientInfo? client)
    {
        string clientInfo = GetClientInfo(endPoint);
        return TryGetClient(clientInfo, out client);
    }

    public ClientInfo? GetClient(string clientInfo)
    {
        clients.TryGetValue(clientInfo, out var client);
        return client;
    }

    public ClientInfo AddClient(IPEndPoint endPoint, string playerId)
    {
        if (!TryAddClient(endPoint, playerId, out var client))
            throw new InvalidOperationException($"Could not add client with player id '{playerId}'.");

        return client;
    }

    public bool TryAddClient(IPEndPoint endPoint, string playerId, out ClientInfo client)
    {
        string clientInfo = GetClientInfo(endPoint);
        client = null!;

        if (!PlayerIdGenerator.IsValidPlayerId(playerId))
        {
            SrLogger.LogWarning($"Rejected client with invalid PlayerId: {playerId}", SrLogTarget.Both);
            return false;
        }

        var existingForEndpoint = GetClient(clientInfo);
        if (existingForEndpoint != null)
        {
            if (existingForEndpoint.PlayerId != playerId)
            {
                SrLogger.LogWarning(
                    $"Rejected PlayerId change from {existingForEndpoint.PlayerId} to {playerId} for {clientInfo}.",
                    SrLogTarget.Both);
                return false;
            }

            client = existingForEndpoint;
            SrLogger.LogWarning($"Client already exists! (PlayerId: {playerId})",
                $"Client already exists: {clientInfo} (PlayerId: {playerId})");
            return true;
        }

        foreach (var existing in clients.Values)
        {
            if (existing.PlayerId != playerId)
                continue;

            SrLogger.LogWarning(
                $"Rejected duplicate PlayerId '{playerId}' from {clientInfo}; already used by {existing.GetClientInfo()}.",
                SrLogTarget.Both);
            return false;
        }

        client = new ClientInfo(endPoint, playerId);

        if (clients.TryAdd(clientInfo, client))
        {
            SrLogger.LogMessage($"Client added! (PlayerId: {playerId})",
                $"Client added: {clientInfo} (PlayerId: {playerId})");
            OnClientAdded?.Invoke(client);
            return true;
        }

        if (clients.TryGetValue(clientInfo, out var raceClient) && raceClient.PlayerId == playerId)
        {
            client = raceClient;
            return true;
        }

        SrLogger.LogWarning($"Failed to add client due to concurrent connect: {clientInfo}", SrLogTarget.Both);
        client = null!;
        return false;
    }

    public bool RemoveClient(string clientInfo)
    {
        if (clients.TryRemove(clientInfo, out var client))
        {
            SrLogger.LogMessage("Client removed!",
                $"Client removed: {clientInfo}");
            OnClientRemoved?.Invoke(client);
            return true;
        }
        return false;
    }

    public bool RemoveClient(IPEndPoint endPoint)
    {
        string clientInfo = GetClientInfo(endPoint);
        return RemoveClient(clientInfo);
    }

    public TimeSpan? UpdateHeartbeat(string clientInfo)
    {
        if (clients.TryGetValue(clientInfo, out var client))
        {
            return client.UpdateHeartbeat();
        }

        return null;
    }

    public TimeSpan? UpdateHeartbeat(IPEndPoint endPoint) => UpdateHeartbeat(GetClientInfo(endPoint));

    public List<ClientInfo> GetAllClients()
    {
        return clients.Values.ToList();
    }

    public List<ClientInfo> GetTimedOutClients()
    {
        return clients.Values
            .Where(client => client.IsTimedOut())
            .ToList();
    }

    public void RemoveTimedOutClients()
    {
        var timedOut = GetTimedOutClients();
        foreach (var client in timedOut)
        {
            SrLogger.LogWarning(
                $"Client timed out: {client.PlayerId} ({client.GetClientInfo()})",
                SrLogTarget.Both);
            RemoveClient(client.GetClientInfo());
        }
    }

    public void Clear()
    {
        var allClients = clients.Values.ToList();
        clients.Clear();

        foreach (var client in allClients)
        {
            OnClientRemoved?.Invoke(client);
        }

        SrLogger.LogMessage("All clients cleared", SrLogTarget.Both);
    }

    private static string GetClientInfo(IPEndPoint endPoint) => $"{endPoint.Address}:{endPoint.Port}";
}
