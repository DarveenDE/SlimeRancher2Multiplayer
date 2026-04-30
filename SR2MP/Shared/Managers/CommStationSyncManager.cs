using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Dialogue.CommStation;
using SR2MP.Packets.World;

namespace SR2MP.Shared.Managers;

public static class CommStationSyncManager
{
    public const byte TargetConversation = 0;
    public const byte TargetProvider = 1;
    public const byte TargetRancher = 2;

    private static readonly System.Reflection.MethodInfo? RancherRecordPlayed =
        AccessTools.Method(typeof(RancherDefinition), "RecordPlayed");

    public static List<CommStationPlayedPacket.Entry> CreateSnapshot()
    {
        var entries = new List<CommStationPlayedPacket.Entry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var conversation in Resources.FindObjectsOfTypeAll<FixedConversation>())
        {
            if (!conversation)
                continue;

            try
            {
                var id = conversation.GetId();
                if (string.IsNullOrWhiteSpace(id) || !conversation.HasBeenPlayed())
                    continue;

                var key = EntryKey(TargetConversation, id);
                if (!seen.Add(key))
                    continue;

                entries.Add(new CommStationPlayedPacket.Entry
                {
                    Id = id,
                    TargetType = TargetConversation,
                });
            }
            catch (Exception ex)
            {
                SrLogger.LogDebug($"Skipped comm station played snapshot entry: {ex.Message}", SrLogTarget.Main);
            }
        }

        return entries;
    }

    public static void SendPlayed(string id, byte targetType)
    {
        if (handlingPacket || string.IsNullOrWhiteSpace(id))
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        Main.SendToAllOrServer(new CommStationPlayedPacket
        {
            Entries = new List<CommStationPlayedPacket.Entry>
            {
                new()
                {
                    Id = id,
                    TargetType = targetType,
                }
            }
        });
    }

    public static int Apply(CommStationPlayedPacket packet, string source)
    {
        var applied = 0;
        foreach (var entry in packet.Entries)
        {
            if (Apply(entry, source))
                applied++;
        }

        return applied;
    }

    private static bool Apply(CommStationPlayedPacket.Entry entry, string source)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
            return false;

        var applied = entry.TargetType switch
        {
            TargetConversation => ApplyConversation(entry.Id),
            TargetProvider => ApplyProvider(entry.Id),
            TargetRancher => ApplyRancher(entry.Id),
            _ => false
        };

        if (applied && source.Contains("repair", StringComparison.OrdinalIgnoreCase))
            SrLogger.LogDebug($"Repair applied comm station played state '{entry.Id}'.", SrLogTarget.Main);

        return applied;
    }

    private static bool ApplyConversation(string id)
    {
        var applied = false;

        foreach (var conversation in Resources.FindObjectsOfTypeAll<FixedConversation>())
        {
            if (!conversation || !IsId(conversation, id))
                continue;

            try
            {
                RunWithHandlingPacket(() => conversation.RecordPlayed());
                applied = true;
            }
            catch (Exception ex)
            {
                SrLogger.LogDebug($"Could not apply comm station conversation '{id}': {ex.Message}", SrLogTarget.Main);
            }
        }

        return applied;
    }

    private static bool ApplyProvider(string id)
    {
        var applied = false;

        foreach (var provider in Resources.FindObjectsOfTypeAll<ConversationListProvider>())
        {
            if (!provider || !IsId(provider, id))
                continue;

            try
            {
                RunWithHandlingPacket(() => provider.RecordPlayed());
                applied = true;
            }
            catch (Exception ex)
            {
                SrLogger.LogDebug($"Could not apply comm station provider '{id}': {ex.Message}", SrLogTarget.Main);
            }
        }

        return applied;
    }

    private static bool ApplyRancher(string id)
    {
        if (RancherRecordPlayed == null)
            return false;

        var applied = false;

        foreach (var rancher in Resources.FindObjectsOfTypeAll<RancherDefinition>())
        {
            if (!rancher || !IsId(rancher, id))
                continue;

            try
            {
                RunWithHandlingPacket(() => RancherRecordPlayed.Invoke(rancher, null));
                applied = true;
            }
            catch (Exception ex)
            {
                SrLogger.LogDebug($"Could not apply comm station rancher '{id}': {ex.Message}", SrLogTarget.Main);
            }
        }

        return applied;
    }

    private static bool IsId(FixedConversation conversation, string id)
    {
        try { return string.Equals(conversation.GetId(), id, StringComparison.Ordinal); }
        catch { return false; }
    }

    private static bool IsId(ConversationListProvider provider, string id)
    {
        try { return string.Equals(provider.GetId(), id, StringComparison.Ordinal); }
        catch { return false; }
    }

    private static bool IsId(RancherDefinition rancher, string id)
    {
        try { return string.Equals(rancher.GetId(), id, StringComparison.Ordinal); }
        catch { return false; }
    }

    private static string EntryKey(byte targetType, string id)
        => $"{targetType}:{id}";
}
