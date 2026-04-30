using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.AccessDoor)]
public sealed class AccessDoorHandler : BaseClientPacketHandler<AccessDoorPacket>
{
    public AccessDoorHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(AccessDoorPacket packet)
    {
        WorldEventStateSyncManager.ApplyAccessDoorState(
            packet,
            packet.IsRepairSnapshot ? "client repair access door" : "client access door");
    }
}
