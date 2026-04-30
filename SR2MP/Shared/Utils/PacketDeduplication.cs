using System.Collections.Concurrent;

namespace SR2MP.Shared.Utils;

public static class PacketDeduplication
{
    // Key format: "PacketType_UniqueId"
    private static readonly ConcurrentDictionary<string, DateTime> ProcessedPackets = new();

    private static readonly TimeSpan PacketMemoryDuration = TimeSpan.FromSeconds(30);

    private static int processCounter = 0;
    private const int CleanupInterval = 100;
    private static readonly List<string> _toRemove = new();

    public static bool IsDuplicate(string packetType, string uniqueId)
    {
        var key = $"{packetType}_{uniqueId}";

        if (++processCounter >= CleanupInterval)
        {
            processCounter = 0;
            Cleanup();
        }

        return !ProcessedPackets.TryAdd(key, DateTime.UtcNow);
    }

    public static void MarkProcessed(string packetType, string uniqueId)
    {
        var key = $"{packetType}_{uniqueId}";
        ProcessedPackets[key] = DateTime.UtcNow;
    }

    public static void Clear()
    {
        ProcessedPackets.Clear();
        processCounter = 0;
        SrLogger.LogPacketSize("Packet deduplication cache cleared", SrLogTarget.Both);
        Cleanup();
    }

    private static void Cleanup()
    {
        var now = DateTime.UtcNow;
        _toRemove.Clear();

        foreach (var kvp in ProcessedPackets)
        {
            if (now - kvp.Value > PacketMemoryDuration)
                _toRemove.Add(kvp.Key);
        }

        foreach (var key in _toRemove)
            ProcessedPackets.TryRemove(key, out _);

        if (_toRemove.Count > 0)
            SrLogger.LogPacketSize($"Cleaned up {_toRemove.Count} old packet records", SrLogTarget.Both);
    }

    public static int GetTrackedPacketCount() => ProcessedPackets.Count;
}
