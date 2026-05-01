namespace SR2MP.Shared.Utils;

public static class RecentServerService
{
    public const int DefaultMaxServers = 5;

    public static List<ServerAddress> Load(int maxServers = DefaultMaxServers)
    {
        var servers = new List<ServerAddress>();

        foreach (var entry in Main.SavedRecentServers.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ServerAddressParser.TryParse(entry, string.Empty, out var address, out _))
                continue;

            AddOrPromote(servers, address);
        }

        Trim(servers, maxServers);
        return servers;
    }

    public static void Remember(ServerAddress address, int maxServers = DefaultMaxServers)
    {
        var servers = Load(int.MaxValue);
        AddOrPromote(servers, address);
        Save(servers, maxServers);
    }

    public static void Save(IEnumerable<ServerAddress> servers, int maxServers = DefaultMaxServers)
    {
        var normalized = new List<ServerAddress>();
        foreach (var server in servers.Reverse())
            AddOrPromote(normalized, server);

        Trim(normalized, maxServers);
        Main.SetConfigValue("recent_servers", string.Join("|", normalized.Select(server => server.Display)));
    }

    private static void AddOrPromote(List<ServerAddress> servers, ServerAddress address)
    {
        for (int i = servers.Count - 1; i >= 0; i--)
        {
            var existing = servers[i];
            if (existing.Port == address.Port &&
                string.Equals(existing.Host, address.Host, StringComparison.OrdinalIgnoreCase))
            {
                servers.RemoveAt(i);
            }
        }

        servers.Insert(0, address);
    }

    private static void Trim(List<ServerAddress> servers, int maxServers)
    {
        int limit = Math.Max(0, maxServers);
        if (servers.Count > limit)
            servers.RemoveRange(limit, servers.Count - limit);
    }
}
