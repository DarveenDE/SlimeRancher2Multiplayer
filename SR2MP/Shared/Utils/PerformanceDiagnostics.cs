using System.Diagnostics;
using System.Text;
using Il2CppInterop.Runtime.Attributes;
using MelonLoader;
using MelonLoader.Utils;
using SR2MP.Packets.Utils;

namespace SR2MP.Shared.Utils;

public static class PerformanceDiagnostics
{
    private const float FlushIntervalSeconds = 5f;
    private const int PacketTypeCount = 256;

    private static readonly object WriterLock = new();

    private static readonly long[] SendToAllOrServerCallsByType = new long[PacketTypeCount];
    private static readonly long[] ServerSendToAllCallsByType = new long[PacketTypeCount];
    private static readonly long[] ServerSendToAllZeroClientCallsByType = new long[PacketTypeCount];
    private static readonly long[] ServerSendToAllBytesByType = new long[PacketTypeCount];
    private static readonly long[] ServerSendToAllSerializeTicksByType = new long[PacketTypeCount];
    private static readonly long[] PacketSplitCallsByType = new long[PacketTypeCount];
    private static readonly long[] PacketSplitChunksByType = new long[PacketTypeCount];

    private static readonly long[] PreviousSendToAllOrServerCallsByType = new long[PacketTypeCount];
    private static readonly long[] PreviousServerSendToAllCallsByType = new long[PacketTypeCount];
    private static readonly long[] PreviousServerSendToAllZeroClientCallsByType = new long[PacketTypeCount];
    private static readonly long[] PreviousServerSendToAllBytesByType = new long[PacketTypeCount];
    private static readonly long[] PreviousServerSendToAllSerializeTicksByType = new long[PacketTypeCount];
    private static readonly long[] PreviousPacketSplitCallsByType = new long[PacketTypeCount];
    private static readonly long[] PreviousPacketSplitChunksByType = new long[PacketTypeCount];

    private static StreamWriter? writer;
    private static int enabled;
    private static bool initialized;
    private static float nextFlushAt;
    private static double lastFlushRealtime;

    private static long frameCount;
    private static long networkActorUpdates;
    private static long networkActorLocalUpdates;
    private static long networkActorRemoteUpdates;
    private static long networkActorLocalTicks;
    private static long networkActorRemoteRetargets;
    private static long networkActorPacketsCreated;
    private static long networkActorUnchangedTicks;
    private static long networkActorInvalidTicks;
    private static long networkPlayerUpdates;
    private static long networkPlayerLocalUpdates;
    private static long networkPlayerLocalTicks;
    private static long networkTimeUpdates;
    private static long networkTimeSends;
    private static long networkWeatherUpdates;
    private static long networkWeatherSends;
    private static long mapMarkerUpdates;
    private static long mapMarkerPlayerSnapshots;
    private static long worldRepairSnapshots;
    private static long worldRepairSkippedNoClients;
    private static long mainThreadActionsProcessed;
    private static long mainThreadBacklogSamples;
    private static long mainThreadBacklogTotal;
    private static long mainThreadDispatcherTicks;
    private static long serverSendToClientCalls;
    private static long serverSendToClientBytes;
    private static long serverSendToClientSerializeTicks;
    private static long serverSendToAllExceptCalls;
    private static long serverSendToAllExceptBytes;
    private static long serverSendToAllExceptSerializeTicks;
    private static long networkBroadcastCalls;
    private static long networkBroadcastZeroEndpointCalls;
    private static long networkBroadcastEndpoints;
    private static long networkSendCalls;
    private static long networkSendChunks;
    private static long udpSendPackets;
    private static long udpSendBytes;
    private static long compressionAttempts;
    private static long compressionUsed;
    private static long compressionTicks;

    private static Snapshot previousSnapshot;

    public static bool IsEnabled => System.Threading.Volatile.Read(ref enabled) == 1;

    public static string LogPath { get; private set; } = string.Empty;

    public static void Initialize(bool startEnabled)
    {
        if (initialized)
        {
            SetEnabled(startEnabled, writeMarker: false);
            return;
        }

        initialized = true;

        var folderPath = Path.Combine(MelonEnvironment.UserDataDirectory, "SR2MP");
        Directory.CreateDirectory(folderPath);
        LogPath = Path.Combine(folderPath, "perf-diagnostics.log");

        CaptureBaseline();
        SetEnabled(startEnabled, writeMarker: false);
    }

    public static void EnsureRunner()
    {
        if (PerformanceDiagnosticsRunner.Instance)
            return;

        var obj = new GameObject("SR2MP_PerformanceDiagnostics");
        Object.DontDestroyOnLoad(obj);
        obj.AddComponent<PerformanceDiagnosticsRunner>();
    }

    public static void SetEnabled(bool value, bool writeMarker = true)
    {
        InitializeIfNeeded();

        var newValue = value ? 1 : 0;
        var oldValue = System.Threading.Interlocked.Exchange(ref enabled, newValue);
        if (oldValue == newValue)
            return;

        if (value)
        {
            OpenWriterIfNeeded();
            CaptureBaseline();
            WriteLine("=== Performance diagnostics enabled ===");
            WriteLine($"LogPath={LogPath}");
            SrLogger.LogMessage($"Performance diagnostics enabled. Log: {LogPath}", SrLogTarget.Both);
        }
        else
        {
            if (writeMarker)
                WriteLine("=== Performance diagnostics disabled ===");
            SrLogger.LogMessage("Performance diagnostics disabled.", SrLogTarget.Both);
        }
    }

    public static void Reset()
    {
        InitializeIfNeeded();
        CaptureBaseline();
        WriteLine("=== Performance diagnostics baseline reset ===");
    }

    public static void WriteSnapshot(string reason)
    {
        if (!IsEnabled)
        {
            SrLogger.LogMessage($"Performance diagnostics are disabled. Log path: {LogPath}", SrLogTarget.Both);
            return;
        }

        Flush(reason);
    }

    public static void Update()
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref frameCount);

        if (Time.realtimeSinceStartup < nextFlushAt)
            return;

        Flush("interval");
    }

    public static long GetTimestamp() => Stopwatch.GetTimestamp();

    public static long GetElapsedTicks(long startTimestamp)
        => startTimestamp == 0 ? 0 : Stopwatch.GetTimestamp() - startTimestamp;

    public static void RecordSendToAllOrServer(byte packetType, bool clientConnected, bool serverRunning)
    {
        if (!IsEnabled)
            return;

        Increment(SendToAllOrServerCallsByType, packetType);
    }

    public static void RecordServerSendToAll(byte packetType, PacketReliability reliability, int clientCount, int byteCount, long serializeTicks)
    {
        if (!IsEnabled)
            return;

        Increment(ServerSendToAllCallsByType, packetType);
        if (clientCount <= 0)
            Increment(ServerSendToAllZeroClientCallsByType, packetType);

        Add(ServerSendToAllBytesByType, packetType, byteCount);
        Add(ServerSendToAllSerializeTicksByType, packetType, serializeTicks);
    }

    public static void RecordServerSendToAllExcept(byte packetType, int byteCount, long serializeTicks)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref serverSendToAllExceptCalls);
        System.Threading.Interlocked.Add(ref serverSendToAllExceptBytes, byteCount);
        System.Threading.Interlocked.Add(ref serverSendToAllExceptSerializeTicks, serializeTicks);
    }

    public static void RecordServerSendToClient(byte packetType, int byteCount, long serializeTicks)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref serverSendToClientCalls);
        System.Threading.Interlocked.Add(ref serverSendToClientBytes, byteCount);
        System.Threading.Interlocked.Add(ref serverSendToClientSerializeTicks, serializeTicks);
    }

    public static void RecordNetworkBroadcast(byte packetType, int endpointCount)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref networkBroadcastCalls);
        System.Threading.Interlocked.Add(ref networkBroadcastEndpoints, endpointCount);
        if (endpointCount <= 0)
            System.Threading.Interlocked.Increment(ref networkBroadcastZeroEndpointCalls);
    }

    public static void RecordNetworkSend(byte packetType, int chunkCount)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref networkSendCalls);
        System.Threading.Interlocked.Add(ref networkSendChunks, chunkCount);
    }

    public static void RecordUdpSend(int byteCount)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref udpSendPackets);
        System.Threading.Interlocked.Add(ref udpSendBytes, byteCount);
    }

    public static void RecordPacketSplit(
        byte packetType,
        PacketReliability reliability,
        int originalBytes,
        int finalBytes,
        int chunkCount,
        bool compressionAttempted,
        bool wasCompressed,
        long compressTicks)
    {
        if (!IsEnabled)
            return;

        Increment(PacketSplitCallsByType, packetType);
        Add(PacketSplitChunksByType, packetType, chunkCount);

        if (compressionAttempted)
        {
            System.Threading.Interlocked.Increment(ref compressionAttempts);
            System.Threading.Interlocked.Add(ref compressionTicks, compressTicks);
        }

        if (wasCompressed)
            System.Threading.Interlocked.Increment(ref compressionUsed);
    }

    public static void RecordNetworkActorUpdate(bool locallyOwned)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref networkActorUpdates);
        if (locallyOwned)
            System.Threading.Interlocked.Increment(ref networkActorLocalUpdates);
        else
            System.Threading.Interlocked.Increment(ref networkActorRemoteUpdates);
    }

    public static void RecordNetworkActorLocalTick()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkActorLocalTicks);
    }

    public static void RecordNetworkActorRemoteRetarget()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkActorRemoteRetargets);
    }

    public static void RecordNetworkActorPacketCreated()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkActorPacketsCreated);
    }

    public static void RecordNetworkActorUnchangedTick()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkActorUnchangedTicks);
    }

    public static void RecordNetworkActorInvalidTick()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkActorInvalidTicks);
    }

    public static void RecordNetworkPlayerUpdate(bool isLocal)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref networkPlayerUpdates);
        if (isLocal)
            System.Threading.Interlocked.Increment(ref networkPlayerLocalUpdates);
    }

    public static void RecordNetworkPlayerLocalTick()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkPlayerLocalTicks);
    }

    public static void RecordNetworkTimeUpdate()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkTimeUpdates);
    }

    public static void RecordNetworkTimeSend()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkTimeSends);
    }

    public static void RecordNetworkWeatherUpdate()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkWeatherUpdates);
    }

    public static void RecordNetworkWeatherSend()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref networkWeatherSends);
    }

    public static void RecordMapMarkerUpdate(int playerSnapshotCount)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref mapMarkerUpdates);
        System.Threading.Interlocked.Add(ref mapMarkerPlayerSnapshots, playerSnapshotCount);
    }

    public static void RecordWorldRepairSkippedNoClients()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref worldRepairSkippedNoClients);
    }

    public static void RecordWorldRepairSnapshot()
    {
        if (IsEnabled)
            System.Threading.Interlocked.Increment(ref worldRepairSnapshots);
    }

    public static void RecordMainThreadDispatcher(int processed, int backlog)
    {
        if (!IsEnabled)
            return;

        System.Threading.Interlocked.Increment(ref mainThreadDispatcherTicks);
        System.Threading.Interlocked.Add(ref mainThreadActionsProcessed, processed);
        System.Threading.Interlocked.Increment(ref mainThreadBacklogSamples);
        System.Threading.Interlocked.Add(ref mainThreadBacklogTotal, backlog);
    }

    private static void InitializeIfNeeded()
    {
        if (!initialized)
            Initialize(startEnabled: false);
    }

    private static void Flush(string reason)
    {
        InitializeIfNeeded();
        OpenWriterIfNeeded();

        var nowRealtime = Time.realtimeSinceStartupAsDouble;
        var elapsedSeconds = Math.Max(0.001, nowRealtime - lastFlushRealtime);
        nextFlushAt = Time.realtimeSinceStartup + FlushIntervalSeconds;

        var current = CaptureSnapshot();
        var delta = current - previousSnapshot;
        previousSnapshot = current;

        var fps = delta.FrameCount / elapsedSeconds;
        var actorUpdatesPerFrame = delta.FrameCount > 0
            ? (double)delta.NetworkActorUpdates / delta.FrameCount
            : 0d;
        var localActorUpdatesPerFrame = delta.FrameCount > 0
            ? (double)delta.NetworkActorLocalUpdates / delta.FrameCount
            : 0d;
        var remoteActorUpdatesPerFrame = delta.FrameCount > 0
            ? (double)delta.NetworkActorRemoteUpdates / delta.FrameCount
            : 0d;

        var actorCount = actorManager?.Actors?.Count ?? 0;
        var remotePlayerCount = playerManager?.PlayerCount ?? 0;
        var server = Main.Server;
        var serverRunning = server?.IsRunning() == true;
        var clientConnected = Main.Client?.IsConnected == true;
        var serverClientCount = serverRunning ? server!.GetClientCount() : 0;

        var builder = new StringBuilder(2048);
        builder.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ");
        builder.Append("reason=").Append(reason);
        builder.Append(" window=").Append(elapsedSeconds.ToString("0.00")).Append('s');
        builder.Append(" fps~=").Append(fps.ToString("0.0"));
        builder.Append(" server=").Append(serverRunning);
        builder.Append(" clients=").Append(serverClientCount);
        builder.Append(" clientConnected=").Append(clientConnected);
        builder.Append(" remotePlayers=").Append(remotePlayerCount);
        builder.Append(" actorManagerActors=").Append(actorCount);
        builder.AppendLine();

        builder.Append("  actors: updates=").Append(delta.NetworkActorUpdates);
        builder.Append(" (~").Append(actorUpdatesPerFrame.ToString("0.0")).Append("/frame)");
        builder.Append(" local=").Append(delta.NetworkActorLocalUpdates);
        builder.Append(" (~").Append(localActorUpdatesPerFrame.ToString("0.0")).Append("/frame)");
        builder.Append(" remote=").Append(delta.NetworkActorRemoteUpdates);
        builder.Append(" (~").Append(remoteActorUpdatesPerFrame.ToString("0.0")).Append("/frame)");
        builder.Append(" localTicks=").Append(delta.NetworkActorLocalTicks);
        builder.Append(" remoteRetargets=").Append(delta.NetworkActorRemoteRetargets);
        builder.Append(" packetsCreated=").Append(delta.NetworkActorPacketsCreated);
        builder.Append(" unchangedTicks=").Append(delta.NetworkActorUnchangedTicks);
        builder.Append(" invalidTicks=").Append(delta.NetworkActorInvalidTicks);
        builder.AppendLine();

        builder.Append("  hostLoops: playerUpdates=").Append(delta.NetworkPlayerUpdates);
        builder.Append(" localPlayerUpdates=").Append(delta.NetworkPlayerLocalUpdates);
        builder.Append(" localPlayerTicks=").Append(delta.NetworkPlayerLocalTicks);
        builder.Append(" timeUpdates=").Append(delta.NetworkTimeUpdates);
        builder.Append(" timeSends=").Append(delta.NetworkTimeSends);
        builder.Append(" weatherUpdates=").Append(delta.NetworkWeatherUpdates);
        builder.Append(" weatherSends=").Append(delta.NetworkWeatherSends);
        builder.Append(" mapUpdates=").Append(delta.MapMarkerUpdates);
        builder.Append(" mapPlayerSnapshots=").Append(delta.MapMarkerPlayerSnapshots);
        builder.Append(" repairSnapshots=").Append(delta.WorldRepairSnapshots);
        builder.Append(" repairSkippedNoClients=").Append(delta.WorldRepairSkippedNoClients);
        builder.AppendLine();

        builder.Append("  sends: mainCalls=").Append(SumDelta(SendToAllOrServerCallsByType, PreviousSendToAllOrServerCallsByType));
        builder.Append(" serverSendToAll=").Append(SumDelta(ServerSendToAllCallsByType, PreviousServerSendToAllCallsByType));
        builder.Append(" zeroClientSendToAll=").Append(SumDelta(ServerSendToAllZeroClientCallsByType, PreviousServerSendToAllZeroClientCallsByType));
        builder.Append(" sendToAllBytes=").Append(SumDelta(ServerSendToAllBytesByType, PreviousServerSendToAllBytesByType));
        builder.Append(" sendToAllSerializeMs=").Append(TicksToMilliseconds(SumDelta(ServerSendToAllSerializeTicksByType, PreviousServerSendToAllSerializeTicksByType)).ToString("0.###"));
        builder.Append(" sendToAllExcept=").Append(delta.ServerSendToAllExceptCalls);
        builder.Append(" sendToClient=").Append(delta.ServerSendToClientCalls);
        builder.Append(" broadcasts=").Append(delta.NetworkBroadcastCalls);
        builder.Append(" zeroEndpointBroadcasts=").Append(delta.NetworkBroadcastZeroEndpointCalls);
        builder.Append(" broadcastEndpoints=").Append(delta.NetworkBroadcastEndpoints);
        builder.Append(" networkSends=").Append(delta.NetworkSendCalls);
        builder.Append(" networkChunks=").Append(delta.NetworkSendChunks);
        builder.Append(" udpPackets=").Append(delta.UdpSendPackets);
        builder.Append(" udpBytes=").Append(delta.UdpSendBytes);
        builder.AppendLine();

        builder.Append("  packetWork: splits=").Append(SumDelta(PacketSplitCallsByType, PreviousPacketSplitCallsByType));
        builder.Append(" splitChunks=").Append(SumDelta(PacketSplitChunksByType, PreviousPacketSplitChunksByType));
        builder.Append(" compressionAttempts=").Append(delta.CompressionAttempts);
        builder.Append(" compressionUsed=").Append(delta.CompressionUsed);
        builder.Append(" compressionMs=").Append(TicksToMilliseconds(delta.CompressionTicks).ToString("0.###"));
        builder.Append(" dispatcherActions=").Append(delta.MainThreadActionsProcessed);
        builder.Append(" dispatcherAvgBacklog=").Append(delta.MainThreadBacklogSamples > 0
            ? ((double)delta.MainThreadBacklogTotal / delta.MainThreadBacklogSamples).ToString("0.0")
            : "0");
        builder.AppendLine();

        AppendTopPacketTypes(builder, "main packet calls", SendToAllOrServerCallsByType, PreviousSendToAllOrServerCallsByType);
        AppendTopPacketTypes(builder, "zero-client serialized", ServerSendToAllZeroClientCallsByType, PreviousServerSendToAllZeroClientCallsByType);
        AppendTopPacketTypes(builder, "server serialized bytes", ServerSendToAllBytesByType, PreviousServerSendToAllBytesByType);

        CopySnapshot(SendToAllOrServerCallsByType, PreviousSendToAllOrServerCallsByType);
        CopySnapshot(ServerSendToAllCallsByType, PreviousServerSendToAllCallsByType);
        CopySnapshot(ServerSendToAllZeroClientCallsByType, PreviousServerSendToAllZeroClientCallsByType);
        CopySnapshot(ServerSendToAllBytesByType, PreviousServerSendToAllBytesByType);
        CopySnapshot(ServerSendToAllSerializeTicksByType, PreviousServerSendToAllSerializeTicksByType);
        CopySnapshot(PacketSplitCallsByType, PreviousPacketSplitCallsByType);
        CopySnapshot(PacketSplitChunksByType, PreviousPacketSplitChunksByType);

        lastFlushRealtime = nowRealtime;
        WriteLine(builder.ToString().TrimEnd());
    }

    private static void CaptureBaseline()
    {
        previousSnapshot = CaptureSnapshot();
        CopySnapshot(SendToAllOrServerCallsByType, PreviousSendToAllOrServerCallsByType);
        CopySnapshot(ServerSendToAllCallsByType, PreviousServerSendToAllCallsByType);
        CopySnapshot(ServerSendToAllZeroClientCallsByType, PreviousServerSendToAllZeroClientCallsByType);
        CopySnapshot(ServerSendToAllBytesByType, PreviousServerSendToAllBytesByType);
        CopySnapshot(ServerSendToAllSerializeTicksByType, PreviousServerSendToAllSerializeTicksByType);
        CopySnapshot(PacketSplitCallsByType, PreviousPacketSplitCallsByType);
        CopySnapshot(PacketSplitChunksByType, PreviousPacketSplitChunksByType);
        lastFlushRealtime = Time.realtimeSinceStartupAsDouble;
        nextFlushAt = Time.realtimeSinceStartup + FlushIntervalSeconds;
    }

    private static Snapshot CaptureSnapshot()
    {
        return new Snapshot
        {
            FrameCount = Read(ref frameCount),
            NetworkActorUpdates = Read(ref networkActorUpdates),
            NetworkActorLocalUpdates = Read(ref networkActorLocalUpdates),
            NetworkActorRemoteUpdates = Read(ref networkActorRemoteUpdates),
            NetworkActorLocalTicks = Read(ref networkActorLocalTicks),
            NetworkActorRemoteRetargets = Read(ref networkActorRemoteRetargets),
            NetworkActorPacketsCreated = Read(ref networkActorPacketsCreated),
            NetworkActorUnchangedTicks = Read(ref networkActorUnchangedTicks),
            NetworkActorInvalidTicks = Read(ref networkActorInvalidTicks),
            NetworkPlayerUpdates = Read(ref networkPlayerUpdates),
            NetworkPlayerLocalUpdates = Read(ref networkPlayerLocalUpdates),
            NetworkPlayerLocalTicks = Read(ref networkPlayerLocalTicks),
            NetworkTimeUpdates = Read(ref networkTimeUpdates),
            NetworkTimeSends = Read(ref networkTimeSends),
            NetworkWeatherUpdates = Read(ref networkWeatherUpdates),
            NetworkWeatherSends = Read(ref networkWeatherSends),
            MapMarkerUpdates = Read(ref mapMarkerUpdates),
            MapMarkerPlayerSnapshots = Read(ref mapMarkerPlayerSnapshots),
            WorldRepairSnapshots = Read(ref worldRepairSnapshots),
            WorldRepairSkippedNoClients = Read(ref worldRepairSkippedNoClients),
            MainThreadActionsProcessed = Read(ref mainThreadActionsProcessed),
            MainThreadBacklogSamples = Read(ref mainThreadBacklogSamples),
            MainThreadBacklogTotal = Read(ref mainThreadBacklogTotal),
            MainThreadDispatcherTicks = Read(ref mainThreadDispatcherTicks),
            ServerSendToClientCalls = Read(ref serverSendToClientCalls),
            ServerSendToClientBytes = Read(ref serverSendToClientBytes),
            ServerSendToClientSerializeTicks = Read(ref serverSendToClientSerializeTicks),
            ServerSendToAllExceptCalls = Read(ref serverSendToAllExceptCalls),
            ServerSendToAllExceptBytes = Read(ref serverSendToAllExceptBytes),
            ServerSendToAllExceptSerializeTicks = Read(ref serverSendToAllExceptSerializeTicks),
            NetworkBroadcastCalls = Read(ref networkBroadcastCalls),
            NetworkBroadcastZeroEndpointCalls = Read(ref networkBroadcastZeroEndpointCalls),
            NetworkBroadcastEndpoints = Read(ref networkBroadcastEndpoints),
            NetworkSendCalls = Read(ref networkSendCalls),
            NetworkSendChunks = Read(ref networkSendChunks),
            UdpSendPackets = Read(ref udpSendPackets),
            UdpSendBytes = Read(ref udpSendBytes),
            CompressionAttempts = Read(ref compressionAttempts),
            CompressionUsed = Read(ref compressionUsed),
            CompressionTicks = Read(ref compressionTicks),
        };
    }

    private static void AppendTopPacketTypes(StringBuilder builder, string label, long[] current, long[] previous)
    {
        Span<(int Type, long Count)> top = stackalloc (int Type, long Count)[5];

        for (var type = 0; type < PacketTypeCount; type++)
        {
            var delta = ReadArray(current, type) - previous[type];
            if (delta <= 0)
                continue;

            for (var i = 0; i < top.Length; i++)
            {
                if (delta <= top[i].Count)
                    continue;

                for (var j = top.Length - 1; j > i; j--)
                    top[j] = top[j - 1];

                top[i] = (type, delta);
                break;
            }
        }

        if (top[0].Count <= 0)
            return;

        builder.Append("  top ").Append(label).Append(':');
        foreach (var entry in top)
        {
            if (entry.Count <= 0)
                continue;

            builder.Append(' ')
                .Append(PacketTypeName(entry.Type))
                .Append('=')
                .Append(entry.Count);
        }
        builder.AppendLine();
    }

    private static string PacketTypeName(int packetType)
    {
        var value = (byte)packetType;
        return Enum.IsDefined(typeof(PacketType), value)
            ? ((PacketType)value).ToString()
            : packetType.ToString();
    }

    private static void OpenWriterIfNeeded()
    {
        lock (WriterLock)
        {
            if (writer != null)
                return;

            writer = new StreamWriter(LogPath, append: true, Encoding.UTF8)
            {
                AutoFlush = true
            };

            writer.WriteLine();
            writer.WriteLine($"=== SR2MP performance diagnostics session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        }
    }

    private static void WriteLine(string line)
    {
        if (string.IsNullOrEmpty(LogPath))
            return;

        OpenWriterIfNeeded();

        lock (WriterLock)
        {
            writer?.WriteLine(line);
        }
    }

    private static void Increment(long[] values, byte packetType)
        => System.Threading.Interlocked.Increment(ref values[packetType]);

    private static void Add(long[] values, byte packetType, long amount)
        => System.Threading.Interlocked.Add(ref values[packetType], amount);

    private static long Read(ref long value)
        => System.Threading.Volatile.Read(ref value);

    private static long ReadArray(long[] values, int index)
        => System.Threading.Volatile.Read(ref values[index]);

    private static long SumDelta(long[] current, long[] previous)
    {
        long total = 0;
        for (var i = 0; i < current.Length; i++)
            total += ReadArray(current, i) - previous[i];
        return total;
    }

    private static void CopySnapshot(long[] source, long[] target)
    {
        for (var i = 0; i < source.Length; i++)
            target[i] = ReadArray(source, i);
    }

    private static double TicksToMilliseconds(long ticks)
        => ticks <= 0 ? 0d : ticks * 1000d / Stopwatch.Frequency;

    private readonly struct Snapshot
    {
        public long FrameCount { get; init; }
        public long NetworkActorUpdates { get; init; }
        public long NetworkActorLocalUpdates { get; init; }
        public long NetworkActorRemoteUpdates { get; init; }
        public long NetworkActorLocalTicks { get; init; }
        public long NetworkActorRemoteRetargets { get; init; }
        public long NetworkActorPacketsCreated { get; init; }
        public long NetworkActorUnchangedTicks { get; init; }
        public long NetworkActorInvalidTicks { get; init; }
        public long NetworkPlayerUpdates { get; init; }
        public long NetworkPlayerLocalUpdates { get; init; }
        public long NetworkPlayerLocalTicks { get; init; }
        public long NetworkTimeUpdates { get; init; }
        public long NetworkTimeSends { get; init; }
        public long NetworkWeatherUpdates { get; init; }
        public long NetworkWeatherSends { get; init; }
        public long MapMarkerUpdates { get; init; }
        public long MapMarkerPlayerSnapshots { get; init; }
        public long WorldRepairSnapshots { get; init; }
        public long WorldRepairSkippedNoClients { get; init; }
        public long MainThreadActionsProcessed { get; init; }
        public long MainThreadBacklogSamples { get; init; }
        public long MainThreadBacklogTotal { get; init; }
        public long MainThreadDispatcherTicks { get; init; }
        public long ServerSendToClientCalls { get; init; }
        public long ServerSendToClientBytes { get; init; }
        public long ServerSendToClientSerializeTicks { get; init; }
        public long ServerSendToAllExceptCalls { get; init; }
        public long ServerSendToAllExceptBytes { get; init; }
        public long ServerSendToAllExceptSerializeTicks { get; init; }
        public long NetworkBroadcastCalls { get; init; }
        public long NetworkBroadcastZeroEndpointCalls { get; init; }
        public long NetworkBroadcastEndpoints { get; init; }
        public long NetworkSendCalls { get; init; }
        public long NetworkSendChunks { get; init; }
        public long UdpSendPackets { get; init; }
        public long UdpSendBytes { get; init; }
        public long CompressionAttempts { get; init; }
        public long CompressionUsed { get; init; }
        public long CompressionTicks { get; init; }

        public static Snapshot operator -(Snapshot left, Snapshot right)
        {
            return new Snapshot
            {
                FrameCount = left.FrameCount - right.FrameCount,
                NetworkActorUpdates = left.NetworkActorUpdates - right.NetworkActorUpdates,
                NetworkActorLocalUpdates = left.NetworkActorLocalUpdates - right.NetworkActorLocalUpdates,
                NetworkActorRemoteUpdates = left.NetworkActorRemoteUpdates - right.NetworkActorRemoteUpdates,
                NetworkActorLocalTicks = left.NetworkActorLocalTicks - right.NetworkActorLocalTicks,
                NetworkActorRemoteRetargets = left.NetworkActorRemoteRetargets - right.NetworkActorRemoteRetargets,
                NetworkActorPacketsCreated = left.NetworkActorPacketsCreated - right.NetworkActorPacketsCreated,
                NetworkActorUnchangedTicks = left.NetworkActorUnchangedTicks - right.NetworkActorUnchangedTicks,
                NetworkActorInvalidTicks = left.NetworkActorInvalidTicks - right.NetworkActorInvalidTicks,
                NetworkPlayerUpdates = left.NetworkPlayerUpdates - right.NetworkPlayerUpdates,
                NetworkPlayerLocalUpdates = left.NetworkPlayerLocalUpdates - right.NetworkPlayerLocalUpdates,
                NetworkPlayerLocalTicks = left.NetworkPlayerLocalTicks - right.NetworkPlayerLocalTicks,
                NetworkTimeUpdates = left.NetworkTimeUpdates - right.NetworkTimeUpdates,
                NetworkTimeSends = left.NetworkTimeSends - right.NetworkTimeSends,
                NetworkWeatherUpdates = left.NetworkWeatherUpdates - right.NetworkWeatherUpdates,
                NetworkWeatherSends = left.NetworkWeatherSends - right.NetworkWeatherSends,
                MapMarkerUpdates = left.MapMarkerUpdates - right.MapMarkerUpdates,
                MapMarkerPlayerSnapshots = left.MapMarkerPlayerSnapshots - right.MapMarkerPlayerSnapshots,
                WorldRepairSnapshots = left.WorldRepairSnapshots - right.WorldRepairSnapshots,
                WorldRepairSkippedNoClients = left.WorldRepairSkippedNoClients - right.WorldRepairSkippedNoClients,
                MainThreadActionsProcessed = left.MainThreadActionsProcessed - right.MainThreadActionsProcessed,
                MainThreadBacklogSamples = left.MainThreadBacklogSamples - right.MainThreadBacklogSamples,
                MainThreadBacklogTotal = left.MainThreadBacklogTotal - right.MainThreadBacklogTotal,
                MainThreadDispatcherTicks = left.MainThreadDispatcherTicks - right.MainThreadDispatcherTicks,
                ServerSendToClientCalls = left.ServerSendToClientCalls - right.ServerSendToClientCalls,
                ServerSendToClientBytes = left.ServerSendToClientBytes - right.ServerSendToClientBytes,
                ServerSendToClientSerializeTicks = left.ServerSendToClientSerializeTicks - right.ServerSendToClientSerializeTicks,
                ServerSendToAllExceptCalls = left.ServerSendToAllExceptCalls - right.ServerSendToAllExceptCalls,
                ServerSendToAllExceptBytes = left.ServerSendToAllExceptBytes - right.ServerSendToAllExceptBytes,
                ServerSendToAllExceptSerializeTicks = left.ServerSendToAllExceptSerializeTicks - right.ServerSendToAllExceptSerializeTicks,
                NetworkBroadcastCalls = left.NetworkBroadcastCalls - right.NetworkBroadcastCalls,
                NetworkBroadcastZeroEndpointCalls = left.NetworkBroadcastZeroEndpointCalls - right.NetworkBroadcastZeroEndpointCalls,
                NetworkBroadcastEndpoints = left.NetworkBroadcastEndpoints - right.NetworkBroadcastEndpoints,
                NetworkSendCalls = left.NetworkSendCalls - right.NetworkSendCalls,
                NetworkSendChunks = left.NetworkSendChunks - right.NetworkSendChunks,
                UdpSendPackets = left.UdpSendPackets - right.UdpSendPackets,
                UdpSendBytes = left.UdpSendBytes - right.UdpSendBytes,
                CompressionAttempts = left.CompressionAttempts - right.CompressionAttempts,
                CompressionUsed = left.CompressionUsed - right.CompressionUsed,
                CompressionTicks = left.CompressionTicks - right.CompressionTicks,
            };
        }
    }
}

[RegisterTypeInIl2Cpp(false)]
public sealed class PerformanceDiagnosticsRunner : MonoBehaviour
{
    public static PerformanceDiagnosticsRunner Instance { get; private set; }

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        PerformanceDiagnostics.Update();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null!;
    }
}
