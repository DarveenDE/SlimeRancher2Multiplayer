namespace SR2MP.Shared.Utils;

/// <summary>
/// Represents the phase of a client's connection lifecycle.
/// Transitions are: Disconnected → Connecting → InitialSync → Live ↔ Repairing → Disconnecting → Disconnected.
/// </summary>
public enum SessionPhase
{
    /// <summary>No active connection. Clean state.</summary>
    Disconnected,

    /// <summary>ConnectPacket sent; awaiting ConnectAck (handshake + protocol validation).</summary>
    Connecting,

    /// <summary>ConnectAck received; receiving bulk initial-state packets from host.</summary>
    InitialSync,

    /// <summary>Initial sync complete; normal gameplay, all sync is event-driven.</summary>
    Live,

    /// <summary>Host is sending a repair snapshot to correct drift; echo guard applies.</summary>
    Repairing,

    /// <summary>Disconnect in progress; cleanup underway.</summary>
    Disconnecting,
}
