using System.Collections;
using MelonLoader;

namespace SR2MP.Shared.Managers;

/// <summary>
/// Centralised retry queue that replaces the per-manager <c>ApplyPendingXWhenReady</c> coroutine pattern.
/// <para>
/// Each manager holds a static <c>PendingApplyQueue&lt;TKey, TData&gt;</c> instance.  When an apply call
/// fails because the target game model is not yet loaded, the caller enqueues the packet via
/// <see cref="EnqueueAndStart"/> together with the apply delegate.  The queue then runs a single
/// MelonCoroutine that retries every frame until all pending entries are resolved or their per-entry
/// timeout elapses.
/// </para>
/// <para>
/// On timeout, <see cref="EnqueueAndStart"/> optionally accepts an <c>onRepairNeeded</c> callback that
/// fires so the caller can request a targeted repair snapshot instead of silently discarding the entry.
/// </para>
/// </summary>
/// <typeparam name="TKey">Key that uniquely identifies the pending entry (e.g. actor-id or plot-id).</typeparam>
/// <typeparam name="TData">Data type stored per key (the packet or derived state).</typeparam>
public sealed class PendingApplyQueue<TKey, TData>
    where TKey : notnull
    where TData : class
{
    private sealed class PendingEntry
    {
        public TData Data { get; set; } = null!;
        public string Source { get; set; } = "";
        public float TimeoutAt { get; set; }
    }

    private readonly Dictionary<TKey, PendingEntry> _pending = new();
    private readonly float _timeoutSeconds;
    private readonly string _name;

    /// <summary>Whether the retry coroutine is currently running.</summary>
    private bool _coroutineRunning;

    // The apply + repair delegates are captured once when the coroutine is started.
    // This avoids per-frame delegate allocation in the hot path.
    private Func<TKey, TData, string, bool>? _tryApply;
    private Action? _onRepairNeeded;

    /// <param name="name">Human-readable queue name used in log messages.</param>
    /// <param name="timeoutSeconds">Seconds before a queued entry is discarded and repair is requested.</param>
    public PendingApplyQueue(string name, float timeoutSeconds = 10f)
    {
        _name = name;
        _timeoutSeconds = timeoutSeconds;
    }

    /// <summary>Number of entries currently waiting to be applied.</summary>
    public int Count => _pending.Count;

    /// <summary>True when the queue holds no pending entries.</summary>
    public bool IsEmpty => _pending.Count == 0;

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Add or overwrite a pending entry for <paramref name="key"/>, then start the retry coroutine
    /// (if it is not already running).
    /// </summary>
    /// <param name="key">Unique key for this entry.</param>
    /// <param name="data">Data to retry with.</param>
    /// <param name="source">Human-readable source string for log messages.</param>
    /// <param name="tryApply">
    ///   Called every frame with <c>(key, data, source+" retry")</c>.<br/>
    ///   Return <c>true</c> to remove the entry from the queue (either success or intentional discard).<br/>
    ///   Return <c>false</c> to keep retrying.
    /// </param>
    /// <param name="onRepairNeeded">
    ///   Called when an entry's timeout elapses.  Use this to request a targeted repair snapshot.
    ///   May be <c>null</c> if no repair action is needed.
    /// </param>
    public void EnqueueAndStart(
        TKey key,
        TData data,
        string source,
        Func<TKey, TData, string, bool> tryApply,
        Action? onRepairNeeded = null)
    {
        _pending[key] = new PendingEntry
        {
            Data = data,
            Source = source,
            TimeoutAt = UnityEngine.Time.realtimeSinceStartup + _timeoutSeconds,
        };

        SrLogger.LogDebug($"[{_name}] Queued entry for key={key} from {source}.", SrLogTarget.Main);

        // Capture delegates once; subsequent EnqueueAndStart calls on the same queue while the
        // coroutine runs will just add entries — the existing coroutine handles them.
        _tryApply = tryApply;
        _onRepairNeeded = onRepairNeeded;

        if (_coroutineRunning)
            return;

        _coroutineRunning = true;
        MelonCoroutines.Start(RetryLoop());
    }

    /// <summary>
    /// Immediately attempt to apply (and remove) the entry for <paramref name="key"/>.
    /// Used when an entity that was previously unavailable has just become ready (e.g. actor spawned).
    /// </summary>
    /// <returns><c>true</c> if the entry existed and was successfully applied.</returns>
    public bool TryDrainForKey(TKey key)
    {
        if (_tryApply == null || !_pending.TryGetValue(key, out var entry))
            return false;

        if (!_tryApply(key, entry.Data, $"{entry.Source} retry"))
            return false;

        _pending.Remove(key);
        SrLogger.LogDebug($"[{_name}] Drained queued entry for key={key} on demand.", SrLogTarget.Main);
        return true;
    }

    /// <summary>Clear all pending entries and stop the retry loop (e.g. on disconnect).</summary>
    public void Clear()
    {
        _pending.Clear();
        _coroutineRunning = false;
    }

    // ──────────────────────────────────────────────────────────────────────────

    private IEnumerator RetryLoop()
    {
        while (_pending.Count > 0 && _tryApply != null)
        {
            var now = UnityEngine.Time.realtimeSinceStartup;

            // Snapshot keys to avoid modifying the dict while iterating
            var keys = new List<TKey>(_pending.Keys);

            foreach (var key in keys)
            {
                if (!_pending.TryGetValue(key, out var entry))
                    continue;

                if (_tryApply(key, entry.Data, $"{entry.Source} retry"))
                {
                    _pending.Remove(key);
                    SrLogger.LogDebug($"[{_name}] Applied queued entry for key={key} on retry.", SrLogTarget.Main);
                    continue;
                }

                if (now >= entry.TimeoutAt)
                {
                    _pending.Remove(key);
                    SrLogger.LogWarning(
                        $"[{_name}] Timed out waiting for key={key} after {_timeoutSeconds:0.#}s; " +
                        $"discarding and requesting repair.",
                        SrLogTarget.Main);

                    try { _onRepairNeeded?.Invoke(); }
                    catch (Exception ex)
                    {
                        SrLogger.LogError($"[{_name}] onRepairNeeded threw: {ex}", SrLogTarget.Main);
                    }
                }
            }

            if (_pending.Count > 0)
                yield return null;
        }

        _coroutineRunning = false;
    }
}
