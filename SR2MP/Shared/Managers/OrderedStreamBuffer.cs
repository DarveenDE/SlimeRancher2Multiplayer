using System.Collections.Concurrent;
using System.Net;
using SR2MP.Packets.Utils;

namespace SR2MP.Shared.Managers;

/// <summary>
/// Result of receiving a ReliableOrdered packet through the stream buffer.
/// </summary>
public enum StreamReceiveResult
{
    /// <summary>Packet was in order; toProcess contains this packet plus any newly-consecutive buffered packets.</summary>
    Delivered,
    /// <summary>Packet arrived ahead of a gap; stored in buffer, toProcess is empty.</summary>
    Buffered,
    /// <summary>Packet is older than the last accepted sequence; discard silently.</summary>
    Duplicate,
}

/// <summary>
/// Per-(endpoint, packetType) ordered delivery buffer.
/// Replaces the gap-skipping behaviour in <see cref="ReliabilityManager"/> with true in-order delivery:
/// out-of-order packets are held in the buffer until the missing sequences arrive, after which all
/// consecutive ready packets are returned for dispatch in a single call.
/// <para>
/// When a gap remains unfilled for longer than <see cref="GapTimeout"/>, the stream is reset and
/// <see cref="StreamGapTimedOut"/> fires so that the appropriate subsystem can request a repair snapshot.
/// </para>
/// </summary>
public sealed class OrderedStreamBuffer
{
    private sealed class StreamState
    {
        /// <summary>Next sequence number we expect to receive.  0 = stream not yet initialised.</summary>
        public ushort NextExpected;

        /// <summary>Buffered future packets keyed by their sequence number.</summary>
        public readonly Dictionary<ushort, byte[]> Buffer = new();

        /// <summary>Set when a gap is first detected; cleared when the gap closes.</summary>
        public DateTime? GapSince;
    }

    private readonly ConcurrentDictionary<string, StreamState> _streams = new();

    /// <summary>Maximum packets buffered per stream before the stream is reset (overflow defence).</summary>
    public const int MaxBufferSize = 64;

    /// <summary>How long a gap may remain open before the stream is reset and repair is requested.</summary>
    public static readonly TimeSpan GapTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Fired (from the resend background thread) when a gap times out.
    /// Argument is the raw <see cref="PacketType"/> byte for the affected stream.
    /// Listeners should enqueue repair work on the main thread.
    /// </summary>
    public event Action<byte>? StreamGapTimedOut;

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Receive a <c>ReliableOrdered</c> packet.
    /// </summary>
    /// <param name="sender">Source endpoint.</param>
    /// <param name="packetType">Raw packet-type byte — used as the stream discriminator.</param>
    /// <param name="sequenceNumber">The sequence number stamped in the packet header.</param>
    /// <param name="data">Fully-merged, decompressed packet payload.</param>
    /// <returns>
    /// <c>(Delivered, toProcess)</c> — caller should dispatch every element of <c>toProcess</c> in order.<br/>
    /// <c>(Buffered,  null)</c>     — packet stored; caller should send ACK but skip dispatch.<br/>
    /// <c>(Duplicate, null)</c>     — caller should send ACK but skip dispatch.
    /// </returns>
    public (StreamReceiveResult result, IReadOnlyList<byte[]>? toProcess) Receive(
        IPEndPoint sender, byte packetType, ushort sequenceNumber, byte[] data)
    {
        var key = GetKey(sender, packetType);
        var state = _streams.GetOrAdd(key, _ => new StreamState());

        lock (state)
        {
            // ── First packet ever on this stream ──────────────────────────
            if (state.NextExpected == 0)
            {
                if (sequenceNumber != 1)
                {
                    SrLogger.LogWarning(
                        $"[OrderedStream] {sender} type={(PacketType)packetType}: first packet has seq={sequenceNumber} (expected seq=1); treated as duplicate.",
                        SrLogTarget.Both);
                    return (StreamReceiveResult.Duplicate, null);
                }

                state.NextExpected = GetNext(1);
                return (StreamReceiveResult.Delivered, new[] { data });
            }

            // ── In-order ──────────────────────────────────────────────────
            if (sequenceNumber == state.NextExpected)
            {
                state.NextExpected = GetNext(sequenceNumber);
                state.GapSince = null;
                return (StreamReceiveResult.Delivered, DrainConsecutive(state, data));
            }

            // ── Future (gap) ──────────────────────────────────────────────
            if (IsSequenceNewer(sequenceNumber, state.NextExpected))
            {
                if (state.Buffer.Count >= MaxBufferSize)
                {
                    SrLogger.LogWarning(
                        $"[OrderedStream] {sender} type={(PacketType)packetType}: buffer overflow at seq={sequenceNumber}; resetting stream.",
                        SrLogTarget.Both);
                    state.Buffer.Clear();
                    state.NextExpected = GetNext(sequenceNumber);
                    state.GapSince = null;
                    return (StreamReceiveResult.Delivered, new[] { data });
                }

                state.Buffer[sequenceNumber] = data;
                state.GapSince ??= DateTime.UtcNow;

                if (Main.SyncDiagnosticsEnabled)
                {
                    SrLogger.LogDebug(
                        $"[OrderedStream] {sender} type={(PacketType)packetType}: gap — buffering seq={sequenceNumber}, waiting for seq={state.NextExpected} ({state.Buffer.Count} buffered).",
                        SrLogTarget.Both);
                }

                return (StreamReceiveResult.Buffered, null);
            }

            // ── Past (duplicate) ──────────────────────────────────────────
            return (StreamReceiveResult.Duplicate, null);
        }
    }

    /// <summary>
    /// Called periodically (from the reliability resend thread) to detect and handle timed-out gaps.
    /// </summary>
    public void CheckTimeouts()
    {
        var now = DateTime.UtcNow;

        foreach (var kvp in _streams)
        {
            var state = kvp.Value;
            byte packetType;

            lock (state)
            {
                if (!state.GapSince.HasValue || now - state.GapSince.Value <= GapTimeout)
                    continue;

                // Extract the packetType byte from the key "<endpoint>:<packetType>"
                var colonIdx = kvp.Key.LastIndexOf(':');
                if (colonIdx < 0 || !byte.TryParse(kvp.Key.AsSpan(colonIdx + 1), out packetType))
                    packetType = 0;

                SrLogger.LogWarning(
                    $"[OrderedStream] Gap timeout for type={(PacketType)packetType} from {kvp.Key[..colonIdx]}; " +
                    $"dropped {state.Buffer.Count} buffered packet(s), resetting stream and requesting repair.",
                    SrLogTarget.Both);

                state.Buffer.Clear();
                state.GapSince = null;
                state.NextExpected = 0; // require seq=1 on reconnect/resync
            }

            // Fire outside the lock to avoid deadlock if handler re-enters buffer
            try { StreamGapTimedOut?.Invoke(packetType); }
            catch (Exception ex)
            {
                SrLogger.LogError($"[OrderedStream] StreamGapTimedOut handler threw: {ex}", SrLogTarget.Both);
            }
        }
    }

    /// <summary>Remove all stream state (call on disconnect / session reset).</summary>
    public void Clear() => _streams.Clear();

    // ── private helpers ────────────────────────────────────────────────────

    private static IReadOnlyList<byte[]> DrainConsecutive(StreamState state, byte[] justDelivered)
    {
        var result = new List<byte[]> { justDelivered };

        while (state.Buffer.TryGetValue(state.NextExpected, out var buffered))
        {
            result.Add(buffered);
            state.Buffer.Remove(state.NextExpected);
            state.NextExpected = GetNext(state.NextExpected);
        }

        if (state.Buffer.Count == 0)
            state.GapSince = null;

        return result;
    }

    private static string GetKey(IPEndPoint ep, byte packetType) => $"{ep}:{packetType}";

    private static ushort GetNext(ushort seq) =>
        seq == ushort.MaxValue ? (ushort)1 : (ushort)(seq + 1);

    /// <summary>Returns true if <paramref name="s1"/> is strictly newer than <paramref name="s2"/> in the circular sequence space.</summary>
    private static bool IsSequenceNewer(ushort s1, ushort s2) =>
        ((s1 > s2) && (s1 - s2 <= 32768)) ||
        ((s1 < s2) && (s2 - s1 > 32768));
}
