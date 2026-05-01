using System.Collections.Concurrent;
using System.Net;
using SR2MP.Packets.Utils;

namespace SR2MP.Shared.Managers;

public enum OrderedPacketStatus
{
    Accepted,
    Duplicate,
    OutOfOrder
}

public sealed class ReliablePacketFailure
{
    public IPEndPoint Destination { get; init; } = null!;
    public ushort PacketId { get; init; }
    public byte PacketType { get; init; }
    public PacketReliability Reliability { get; init; }
    public int SendCount { get; init; }
    public TimeSpan Age { get; init; }
    public ushort SequenceNumber { get; init; }
}

public sealed class ReliabilityManager
{
    private static readonly TimeSpan AckWarningThreshold = TimeSpan.FromSeconds(1);
    private sealed class PendingPacket
    {
        public byte[][] Chunks { get; set; } = null!;
        public IPEndPoint Destination { get; set; } = null!;
        public ushort PacketId { get; set; }
        public byte PacketType { get; set; }
        public PacketReliability Reliability { get; set; }
        public DateTime FirstSendTime { get; set; }
        public DateTime LastSendTime { get; set; }
        public int SendCount { get; set; }
        public ushort SequenceNumber { get; set; }
    }

    private readonly ConcurrentDictionary<string, PendingPacket> pendingPackets = new();
    private readonly ConcurrentDictionary<string, ushort> lastProcessedSequence = new();

    private readonly ConcurrentDictionary<string, int> sequenceNumbersByDestinationAndType = new();

    private readonly Action<byte[], IPEndPoint> sendRawCallback;
    private readonly List<string> _toRemove = new();

    private Thread? resendThread;
    private volatile bool isRunning;

    public event Action<ReliablePacketFailure>? PacketFailed;

    private static readonly TimeSpan ResendInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRetryTime = TimeSpan.FromSeconds(10);
    private const int MaxResendAttempts = 50;

    public ReliabilityManager(Action<byte[], IPEndPoint> sendRawCallback)
    {
        this.sendRawCallback = sendRawCallback;
    }

    public void Start()
    {
        if (isRunning)
            return;

        isRunning = true;
        resendThread = new Thread(new Action(ResendLoop))
        {
            IsBackground = true,
            Name = "ReliabilityResendThread"
        };
        resendThread.Start();

        SrLogger.LogMessage("ReliabilityManager started", SrLogTarget.Both);
    }

    public void Stop()
    {
        if (!isRunning)
            return;

        isRunning = false;
        pendingPackets.Clear();
        lastProcessedSequence.Clear();
        sequenceNumbersByDestinationAndType.Clear();

        SrLogger.LogMessage("ReliabilityManager stopped", SrLogTarget.Both);
    }

    public void TrackPacket(byte[][] chunks, IPEndPoint destination, ushort packetId,
        byte packetType, PacketReliability reliability, ushort sequenceNumber)
    {
        if (reliability == PacketReliability.Unreliable)
            return;

        var key = GetPacketKey(destination, packetId);
        var packet = new PendingPacket
        {
            Chunks = chunks,
            Destination = destination,
            PacketId = packetId,
            PacketType = packetType,
            Reliability = reliability,
            FirstSendTime = DateTime.UtcNow,
            LastSendTime = DateTime.UtcNow,
            SendCount = 1,
            SequenceNumber = sequenceNumber
        };

        pendingPackets[key] = packet;
    }

    public void HandleAck(IPEndPoint sender, ushort packetId, byte packetType)
    {
        var key = GetPacketKey(sender, packetId);

        if (!pendingPackets.TryRemove(key, out var packet))
            return;
        var latency = DateTime.UtcNow - packet.FirstSendTime;
        if (Main.SyncDiagnosticsEnabled
            && (latency >= AckWarningThreshold || packet.SendCount > 1))
        {
            SrLogger.LogWarning(
                $"Delayed ACK from {sender}: packet={packetId}, type={(PacketType)packet.PacketType}, reliability={packet.Reliability}, latency={latency.TotalMilliseconds:0}ms, sends={packet.SendCount}, pendingReliable={pendingPackets.Count}.",
                SrLogTarget.Both);
        }

        SrLogger.LogPacketSize(
            $"ACK received for packet {packetId} (type={packetType}) after {packet.SendCount} sends, latency={latency.TotalMilliseconds:F1}ms",
            SrLogTarget.Both);
    }

    public OrderedPacketStatus AcceptOrderedPacket(IPEndPoint sender, ushort sequenceNumber, byte packetType)
    {
        var key = GetSequenceKey(sender, packetType);

        if (!lastProcessedSequence.TryGetValue(key, out var lastSequence))
        {
            if (sequenceNumber != 1)
            {
                SrLogger.LogWarning(
                    $"Rejected ordered packet with invalid initial sequence: seq={sequenceNumber}, type={packetType}",
                    SrLogTarget.Both);
                return OrderedPacketStatus.OutOfOrder;
            }

            lastProcessedSequence[key] = sequenceNumber;
            return OrderedPacketStatus.Accepted;
        }

        ushort expectedSequence = GetNextExpectedSequence(lastSequence);

        if (sequenceNumber == expectedSequence)
        {
            lastProcessedSequence[key] = sequenceNumber;
            return OrderedPacketStatus.Accepted;
        }

        if (IsSequenceNewer(sequenceNumber, lastSequence))
        {
            SrLogger.LogWarning(
                $"Ordered packet gap detected; accepting newer packet to keep stream alive: expected seq={expectedSequence}, got seq={sequenceNumber}, type={packetType}",
                SrLogTarget.Both);
            lastProcessedSequence[key] = sequenceNumber;
            return OrderedPacketStatus.Accepted;
        }

        return OrderedPacketStatus.Duplicate;
    }

    // Checks if an ordered packet should be processed based on sequence number
    public bool ShouldProcessOrderedPacket(IPEndPoint sender, ushort sequenceNumber, byte packetType)
    {
        return AcceptOrderedPacket(sender, sequenceNumber, packetType) == OrderedPacketStatus.Accepted;
    }

    // Gets the next sequence number for ReliableOrdered packets
    public ushort GetNextSequenceNumber(IPEndPoint destination, byte packetType)
    {
        var key = GetSequenceKey(destination, packetType);
        var seq = sequenceNumbersByDestinationAndType.AddOrUpdate(
            key,
            1,
            (_, current) => (current >= ushort.MaxValue) ? 1 : current + 1
        );

        return (ushort)seq;
    }

    public ushort GetNextSequenceNumber(byte packetType)
    {
        var seq = sequenceNumbersByDestinationAndType.AddOrUpdate(
            $"global_{packetType}",
            1,
            (_, current) => (current >= ushort.MaxValue) ? 1 : current + 1
        );

        return (ushort)seq;
    }

    private void ResendLoop()
    {
        while (isRunning)
        {
            try
            {
                var now = DateTime.UtcNow;
                _toRemove.Clear();

                foreach (var kvp in pendingPackets)
                {
                    var packet = kvp.Value;

                    // Checks if packet has timed out
                    if (now - packet.FirstSendTime > MaxRetryTime || packet.SendCount >= MaxResendAttempts)
                    {
                        SrLogger.LogWarning(
                            $"Packet {packet.PacketId} (type={packet.PacketType}) failed after {packet.SendCount} attempts",
                            SrLogTarget.Both);
                        NotifyPacketFailed(packet, now - packet.FirstSendTime);
                        _toRemove.Add(kvp.Key);
                        continue;
                    }

                    // Checks if packet should be resent
                    if (now - packet.LastSendTime > ResendInterval)
                    {
                        foreach (var chunk in packet.Chunks)
                        {
                            sendRawCallback(chunk, packet.Destination);
                        }

                        packet.LastSendTime = now;
                        packet.SendCount++;

                        if (Main.SyncDiagnosticsEnabled && packet.SendCount == 2)
                        {
                            SrLogger.LogWarning(
                                $"Reliable resend started for {packet.Destination}: packet={packet.PacketId}, type={(PacketType)packet.PacketType}, reliability={packet.Reliability}, age={(now - packet.FirstSendTime).TotalMilliseconds:0}ms, pendingReliable={pendingPackets.Count}.",
                                SrLogTarget.Both);
                        }

                        if (packet.SendCount % 10 == 0)
                        {
                            SrLogger.LogWarning(
                                $"Resending packet {packet.PacketId} (type={packet.PacketType}) attempt #{packet.SendCount}",
                                SrLogTarget.Both);
                        }
                    }
                }

                // Removes timed out packets
                foreach (var key in _toRemove)
                {
                    pendingPackets.TryRemove(key, out _);
                }

                // todo: Should not cause problems, if it does, remove
                Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                SrLogger.LogError($"ResendLoop error: {ex}", SrLogTarget.Both);
            }
        }
    }

    private static string GetPacketKey(IPEndPoint endpoint, ushort packetId)
    {
        return $"{endpoint}_{packetId}";
    }

    private static string GetSequenceKey(IPEndPoint endpoint, byte packetType)
    {
        return $"{endpoint}_{packetType}";
    }

    private static bool IsSequenceNewer(ushort s1, ushort s2)
    {
        return ((s1 > s2) && (s1 - s2 <= 32768)) ||
               ((s1 < s2) && (s2 - s1 > 32768));
    }

    private static ushort GetNextExpectedSequence(ushort current)
        => current == ushort.MaxValue ? (ushort)1 : (ushort)(current + 1);

    private void NotifyPacketFailed(PendingPacket packet, TimeSpan age)
    {
        try
        {
            PacketFailed?.Invoke(new ReliablePacketFailure
            {
                Destination = packet.Destination,
                PacketId = packet.PacketId,
                PacketType = packet.PacketType,
                Reliability = packet.Reliability,
                SendCount = packet.SendCount,
                Age = age,
                SequenceNumber = packet.SequenceNumber,
            });
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Reliable packet failure callback failed: {ex}", SrLogTarget.Both);
        }
    }

    public int GetPendingPacketCount() => pendingPackets.Count;

    public bool IsPacketPending(IPEndPoint destination, ushort packetId)
    {
        return pendingPackets.ContainsKey(GetPacketKey(destination, packetId));
    }
}
