using System.Net;
using SR2MP.Packets.Pedia;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PediaUnlock)]
public sealed class PediaUnlockHandler : BasePacketHandler<PediaUnlockPacket>
{
    public PediaUnlockHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PediaUnlockPacket packet, IPEndPoint senderEndPoint)
    {
        var lookup = GameContext.Instance.AutoSaveDirector._saveReferenceTranslation._pediaEntryLookup;
        if (!lookup.TryGetValue(packet.ID, out var entry) || !entry)
        {
            SrLogger.LogWarning($"Ignoring pedia unlock with unknown id {packet.ID}.", SrLogTarget.Both);
            return;
        }

        RunWithHandlingPacket(() => SceneContext.Instance.PediaDirector.Unlock(entry, packet.Popup));

        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }
}
