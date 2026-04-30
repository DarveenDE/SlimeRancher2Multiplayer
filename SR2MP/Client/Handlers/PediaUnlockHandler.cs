using SR2MP.Packets.Pedia;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.PediaUnlock)]
public sealed class PediaUnlockHandler : BaseClientPacketHandler<PediaUnlockPacket>
{
    public PediaUnlockHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(PediaUnlockPacket packet)
    {
        var lookup = GameContext.Instance.AutoSaveDirector._saveReferenceTranslation._pediaEntryLookup;
        if (!lookup.TryGetValue(packet.ID, out var entry) || !entry)
        {
            SrLogger.LogWarning($"Ignoring pedia unlock with unknown id {packet.ID}.", SrLogTarget.Both);
            return;
        }

        RunWithHandlingPacket(() => SceneContext.Instance.PediaDirector.Unlock(entry, packet.Popup));
    }
}
