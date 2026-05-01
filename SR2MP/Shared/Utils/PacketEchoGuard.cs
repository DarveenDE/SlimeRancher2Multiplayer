namespace SR2MP.Shared.Utils;

public static class PacketEchoGuard
{
    /// <summary>
    /// Runs <paramref name="action"/> with echo-suppression active so that Harmony patches
    /// triggered by the state mutation do not re-broadcast the change.
    /// Delegates to <see cref="SessionPhaseGate.EnterEchoGuard"/> on the shared
    /// <see cref="NetworkSessionState.PhaseGate"/>; properly nestable.
    /// </summary>
    public static void RunWithHandlingPacket(Action action)
    {
        using var _ = NetworkSessionState.PhaseGate.EnterEchoGuard();
        action();
    }
}
