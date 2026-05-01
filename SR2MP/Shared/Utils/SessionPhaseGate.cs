namespace SR2MP.Shared.Utils;

/// <summary>
/// Tracks the <see cref="SessionPhase"/> for one connection and owns the echo-suppression flag
/// (formerly <c>GlobalVariables.handlingPacket</c>).
///
/// One instance lives in <see cref="NetworkSessionState"/>; it is reset by
/// <see cref="NetworkSessionState.ClearTransientSyncState"/>.
/// </summary>
public sealed class SessionPhaseGate
{
    private int _echoDepth;   // > 0 means echo is suppressed (nestable); accessed only via Interlocked
    private SessionPhase _current = SessionPhase.Disconnected;
    private readonly object _lock = new();

    // ── Phase ──────────────────────────────────────────────────────────────

    /// <summary>Current connection phase.</summary>
    public SessionPhase Current => _current;

    /// <summary>
    /// Attempts to move to <paramref name="next"/>. Logs the transition and returns
    /// <c>true</c> if the transition was valid; <c>false</c> if it was rejected.
    /// Invalid transitions (e.g. going backwards) are logged as warnings and allowed
    /// in Phase 1 to avoid breaking existing behaviour – stricter enforcement comes later.
    /// </summary>
    public bool TryTransition(SessionPhase next, string reason)
    {
        SessionPhase prev;
        lock (_lock)
        {
            prev = _current;
            _current = next;
        }

        if (prev == next)
            return true;

        if (IsForwardTransition(prev, next))
        {
            SrLogger.LogMessage(
                $"[SessionPhase] {prev} → {next} ({reason})",
                SrLogTarget.Main);
        }
        else
        {
            SrLogger.LogWarning(
                $"[SessionPhase] unexpected transition {prev} → {next} ({reason})",
                SrLogTarget.Main);
        }

        return true;
    }

    // ── Echo-suppression ───────────────────────────────────────────────────

    /// <summary>
    /// <c>true</c> while the current thread is inside a
    /// <see cref="EnterEchoGuard"/> scope.  Replaces <c>GlobalVariables.handlingPacket</c>.
    /// </summary>
    public bool ShouldSuppressEcho => System.Threading.Volatile.Read(ref _echoDepth) > 0;

    /// <summary>
    /// Directly read/write the echo-suppression flag.
    /// Exists only to support legacy <c>handlingPacket = true/false</c> assignments
    /// during the Phase-1 migration; prefer <see cref="EnterEchoGuard"/> instead.
    /// </summary>
    internal bool EchoSuppressed
    {
        get => System.Threading.Volatile.Read(ref _echoDepth) > 0;
        set
        {
            // Preserve old bool assignment semantics. Several legacy call sites save
            // and restore handlingPacket; incrementing here can permanently pin the
            // echo guard when they restore a previous true value.
            System.Threading.Volatile.Write(ref _echoDepth, value ? 1 : 0);
        }
    }

    /// <summary>
    /// Opens a scope that sets <see cref="ShouldSuppressEcho"/> to <c>true</c>.
    /// Properly nested – disposing restores the previous state.
    /// Replaces the try/finally in <c>PacketEchoGuard.RunWithHandlingPacket</c>.
    /// </summary>
    public EchoGuardScope EnterEchoGuard()
    {
        System.Threading.Interlocked.Increment(ref _echoDepth);
        return new EchoGuardScope(this);
    }

    // ── Derived convenience ────────────────────────────────────────────────

    /// <summary>
    /// <c>true</c> during <see cref="SessionPhase.InitialSync"/>; reliable packets sent
    /// while this is true should be queued rather than dropped.
    /// </summary>
    public bool ShouldQueueReliable => _current == SessionPhase.InitialSync;

    // ── Internals ─────────────────────────────────────────────────────────

    private static bool IsForwardTransition(SessionPhase from, SessionPhase to) =>
        (from, to) switch
        {
            (SessionPhase.Disconnected,  SessionPhase.Connecting)     => true,
            (SessionPhase.Connecting,    SessionPhase.InitialSync)     => true,
            (SessionPhase.InitialSync,   SessionPhase.Live)            => true,
            (SessionPhase.Live,          SessionPhase.Repairing)       => true,
            (SessionPhase.Repairing,     SessionPhase.Live)            => true,
            (SessionPhase.Live,          SessionPhase.Disconnecting)   => true,
            (SessionPhase.Repairing,     SessionPhase.Disconnecting)   => true,
            (SessionPhase.Connecting,    SessionPhase.Disconnecting)   => true,
            (SessionPhase.InitialSync,   SessionPhase.Disconnecting)   => true,
            (SessionPhase.Disconnecting, SessionPhase.Disconnected)    => true,
            // ClearTransientSyncState resets directly to Disconnected from any phase
            (_,                          SessionPhase.Disconnected)    => true,
            _                                                           => false,
        };

    // ── Nested disposable scope ────────────────────────────────────────────

    /// <summary>Returned by <see cref="EnterEchoGuard"/>. Dispose to leave the scope.</summary>
    public readonly struct EchoGuardScope : System.IDisposable
    {
        private readonly SessionPhaseGate _gate;

        internal EchoGuardScope(SessionPhaseGate gate) => _gate = gate;

        public void Dispose()
        {
            // Clamp at 0 to guard against accidental double-dispose
            var prev = System.Threading.Interlocked.Decrement(ref _gate._echoDepth);
            if (prev < 0)
                System.Threading.Volatile.Write(ref _gate._echoDepth, 0);
        }
    }
}
