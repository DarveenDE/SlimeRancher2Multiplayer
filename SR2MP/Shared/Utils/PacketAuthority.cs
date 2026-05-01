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

public static class PacketAuthority
{
    private static readonly HashSet<PacketType> ClientToServerPackets = new()
    {
        PacketType.Connect,
        PacketType.PlayerJoin,
        PacketType.PlayerLeave,
        PacketType.PlayerUpdate,
        PacketType.ChatMessage,
        PacketType.Heartbeat,
        PacketType.FastForward,
        PacketType.PlayerFX,
        PacketType.MovementSound,
        PacketType.CurrencyAdjust,
        PacketType.ActorDestroy,
        PacketType.ActorSpawn,
        PacketType.ActorUpdate,
        PacketType.ActorTransfer,
        PacketType.ActorUnload,
        PacketType.LandPlotUpdate,
        PacketType.WorldFX,
        PacketType.PlayerUpgrade,
        PacketType.PediaUnlock,
        PacketType.GordoFeed,
        PacketType.GordoBurst,
        PacketType.SwitchActivate,
        PacketType.GeyserTrigger,
        PacketType.MapUnlock,
        PacketType.GardenPlant,
        PacketType.AccessDoor,
        PacketType.ResourceAttach,
        PacketType.LightningStrike,
        PacketType.PuzzleSlotState,
        PacketType.PlortDepositorState,
        PacketType.InitialSyncCompleteAck,
        PacketType.RefineryItemCounts,
        PacketType.LandPlotAmmoUpdate,
        PacketType.LandPlotFeederState,
        PacketType.GardenGrowthState,
        PacketType.CommStationPlayed,
        PacketType.ResourceNodeState,
        PacketType.ResyncRequest,
    };

    public static ClientPacketRule GetClientToServerRule(byte packetType)
    {
        if (!Enum.IsDefined(typeof(PacketType), packetType))
            return ClientPacketRule.Unknown;

        var type = (PacketType)packetType;
        if (type == PacketType.ReservedAck)
            return ClientPacketRule.InternalOnly;

        if (type is PacketType.ReservedDoNotUse or PacketType.None)
            return ClientPacketRule.Reserved;

        if (ClientToServerPackets.Contains(type))
            return ClientPacketRule.Allowed;

        return ClientPacketRule.ServerOnly;
    }

    public static bool CanClientSendToServer(byte packetType)
        => GetClientToServerRule(packetType) is ClientPacketRule.Allowed or ClientPacketRule.InternalOnly;

    public static string FormatPacketType(byte packetType)
        => Enum.IsDefined(typeof(PacketType), packetType)
            ? $"{(PacketType)packetType} ({packetType})"
            : $"Unknown ({packetType})";
}
