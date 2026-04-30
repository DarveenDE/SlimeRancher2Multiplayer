using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.InitialMapEntries)]
public sealed class InitialMapLoadHandler : BaseClientPacketHandler<InitialMapPacket>
{
    public InitialMapLoadHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(InitialMapPacket packet)
    {
        MapUnlockSyncManager.ReplaceSnapshot(
            packet.UnlockedNodes,
            packet.IsRepairSnapshot ? "repair map snapshot" : "initial map entries");
    }
}
