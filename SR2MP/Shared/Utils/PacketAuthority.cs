using SR2MP.Packets.Utils;

namespace SR2MP.Shared.Utils;

public enum ClientPacketRule
{
    Allowed,
    ServerOnly,
    InternalOnly,
    Reserved,
    Unknown
}

public enum PacketDirection
{
    Reserved,
    Internal,
    ClientToServer,
    ServerToClient,
    ServerToAllClients,
    ServerToOtherClients,
    Bidirectional
}

public enum PacketReliabilityProfile
{
    None,
    Unreliable,
    Reliable,
    ReliableOrdered,
    Dynamic
}

public enum HostPacketAction
{
    None,
    RejectAtIngress,
    InternalTransport,
    ValidateAndReply,
    ValidateAndApply,
    ApplyAndBroadcast,
    ValidateApplyAndBroadcast,
    ConvertToHostBroadcast,
    IgnoreClientState
}

public enum PacketStateCoverage
{
    NotApplicable,
    Covered,
    Partial,
    NotCovered,
    PolicyPending
}

public sealed record PacketAuthorityDefinition(
    PacketType Type,
    ClientPacketRule ClientToServerRule,
    PacketDirection Direction,
    PacketReliabilityProfile Reliability,
    HostPacketAction HostAction,
    PacketStateCoverage InitialSync,
    PacketStateCoverage RepairSnapshot);

public static class PacketAuthority
{
    private static readonly IReadOnlyDictionary<PacketType, PacketAuthorityDefinition> Definitions =
        new[]
        {
            Define(PacketType.None, ClientPacketRule.Reserved, PacketDirection.Reserved, PacketReliabilityProfile.None, HostPacketAction.RejectAtIngress, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.Connect, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateAndReply, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.ConnectAck, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.None, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.Close, ClientPacketRule.ServerOnly, PacketDirection.ServerToAllClients, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.PlayerJoin, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.Reliable, HostPacketAction.ConvertToHostBroadcast, PacketStateCoverage.NotCovered, PacketStateCoverage.NotCovered),
            Define(PacketType.BroadcastPlayerJoin, ClientPacketRule.ServerOnly, PacketDirection.ServerToOtherClients, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.NotCovered, PacketStateCoverage.NotCovered),
            Define(PacketType.PlayerLeave, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.ConvertToHostBroadcast, PacketStateCoverage.NotCovered, PacketStateCoverage.NotCovered),
            Define(PacketType.BroadcastPlayerLeave, ClientPacketRule.ServerOnly, PacketDirection.ServerToOtherClients, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.None, PacketStateCoverage.NotCovered, PacketStateCoverage.NotCovered),
            Define(PacketType.PlayerUpdate, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.Unreliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.NotCovered, PacketStateCoverage.NotCovered),
            Define(PacketType.ChatMessage, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.ConvertToHostBroadcast, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.BroadcastChatMessage, ClientPacketRule.ServerOnly, PacketDirection.ServerToAllClients, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.None, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.Heartbeat, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.Unreliable, HostPacketAction.ValidateAndReply, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.HeartbeatAck, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Unreliable, HostPacketAction.None, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.WorldTime, ClientPacketRule.ServerOnly, PacketDirection.ServerToAllClients, PacketReliabilityProfile.Unreliable, HostPacketAction.None, PacketStateCoverage.NotCovered, PacketStateCoverage.NotCovered),
            Define(PacketType.FastForward, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.Unreliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.NotCovered, PacketStateCoverage.NotCovered),
            Define(PacketType.BroadcastFastForward, ClientPacketRule.ServerOnly, PacketDirection.ServerToAllClients, PacketReliabilityProfile.Unreliable, HostPacketAction.None, PacketStateCoverage.NotCovered, PacketStateCoverage.NotCovered),
            Define(PacketType.PlayerFX, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Unreliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.MovementSound, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Unreliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.CurrencyAdjust, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.ActorDestroy, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.ActorSpawn, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.ActorUpdate, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Unreliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.ActorTransfer, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.InitialActors, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.LandPlotUpdate, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.InitialPlots, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotApplicable),
            Define(PacketType.WorldFX, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Unreliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.InitialPlayerUpgrades, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.PlayerUpgrade, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.InitialPediaEntries, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.PediaUnlock, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.MarketPriceChange, ClientPacketRule.ServerOnly, PacketDirection.ServerToAllClients, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.GordoFeed, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.GordoBurst, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.InitialGordos, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotApplicable),
            Define(PacketType.InitialSwitches, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotApplicable),
            Define(PacketType.SwitchActivate, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.ActorUnload, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.GeyserTrigger, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.MapUnlock, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.InitialMapEntries, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.GardenPlant, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.AccessDoor, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.InitialAccessDoors, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotApplicable),
            Define(PacketType.ResourceAttach, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Partial, PacketStateCoverage.Partial),
            Define(PacketType.WeatherUpdate, ClientPacketRule.ServerOnly, PacketDirection.ServerToAllClients, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotCovered),
            Define(PacketType.InitialWeather, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotApplicable),
            Define(PacketType.LightningStrike, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Unreliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.PuzzleSlotState, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.PlortDepositorState, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.InitialPuzzleStates, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.NotApplicable),
            Define(PacketType.InitialSyncComplete, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.InitialSyncCompleteAck, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateAndApply, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.RefineryItemCounts, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.LandPlotAmmoUpdate, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.LandPlotFeederState, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.GardenGrowthState, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Dynamic, HostPacketAction.IgnoreClientState, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.CommStationPlayed, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.ResourceNodeState, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateApplyAndBroadcast, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.ConnectRejected, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.ResyncRequest, ClientPacketRule.Allowed, PacketDirection.ClientToServer, PacketReliabilityProfile.Reliable, HostPacketAction.ValidateAndReply, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.SubsystemSnapshot, ClientPacketRule.ServerOnly, PacketDirection.ServerToClient, PacketReliabilityProfile.Reliable, HostPacketAction.None, PacketStateCoverage.Covered, PacketStateCoverage.Covered),
            Define(PacketType.PlayerLoopSound, ClientPacketRule.Allowed, PacketDirection.Bidirectional, PacketReliabilityProfile.ReliableOrdered, HostPacketAction.ApplyAndBroadcast, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.ReservedAck, ClientPacketRule.InternalOnly, PacketDirection.Internal, PacketReliabilityProfile.Unreliable, HostPacketAction.InternalTransport, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
            Define(PacketType.ReservedDoNotUse, ClientPacketRule.Reserved, PacketDirection.Reserved, PacketReliabilityProfile.None, HostPacketAction.RejectAtIngress, PacketStateCoverage.NotApplicable, PacketStateCoverage.NotApplicable),
        }.ToDictionary(definition => definition.Type);

    private static PacketAuthorityDefinition Define(
        PacketType type,
        ClientPacketRule clientToServerRule,
        PacketDirection direction,
        PacketReliabilityProfile reliability,
        HostPacketAction hostAction,
        PacketStateCoverage initialSync,
        PacketStateCoverage repairSnapshot)
        => new(type, clientToServerRule, direction, reliability, hostAction, initialSync, repairSnapshot);

    public static IEnumerable<PacketAuthorityDefinition> GetDefinitions()
        => Definitions.Values;

    public static bool TryGetDefinition(PacketType packetType, out PacketAuthorityDefinition definition)
        => Definitions.TryGetValue(packetType, out definition!);

    public static PacketAuthorityDefinition GetDefinition(PacketType packetType)
    {
        if (Definitions.TryGetValue(packetType, out var definition))
            return definition;

        throw new ArgumentOutOfRangeException(nameof(packetType), packetType, "Packet type has no authority definition.");
    }

    public static ClientPacketRule GetClientToServerRule(byte packetType)
    {
        if (!Enum.IsDefined(typeof(PacketType), packetType))
            return ClientPacketRule.Unknown;

        var type = (PacketType)packetType;
        return Definitions.TryGetValue(type, out var definition)
            ? definition.ClientToServerRule
            : ClientPacketRule.Unknown;
    }

    public static bool CanClientSendToServer(byte packetType)
        => GetClientToServerRule(packetType) is ClientPacketRule.Allowed or ClientPacketRule.InternalOnly;

    public static string FormatPacketType(byte packetType)
        => Enum.IsDefined(typeof(PacketType), packetType)
            ? $"{(PacketType)packetType} ({packetType})"
            : $"Unknown ({packetType})";
}
