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
        if (string.IsNullOrWhiteSpace(packet.ID))
        {
            SrLogger.LogWarning($"Ignoring pedia unlock with empty id from {senderEndPoint}.", SrLogTarget.Both);
            return;
        }

        var lookup = GameContext.Instance.AutoSaveDirector._saveReferenceTranslation._pediaEntryLookup;
        if (!lookup.TryGetValue(packet.ID, out var entry) || !entry)
        {
            SrLogger.LogWarning($"Ignoring pedia unlock with unknown id {packet.ID} from {DescribeClient(senderEndPoint)}.", SrLogTarget.Both);
            return;
        }

        if (SceneContext.Instance.PediaDirector.IsUnlocked(entry))
        {
            SrLogger.LogDebug($"Ignoring duplicate pedia unlock '{packet.ID}' from {DescribeClient(senderEndPoint)}.", SrLogTarget.Main);
            return;
        }

        var unlocked = false;
        RunWithHandlingPacket(() => unlocked = SceneContext.Instance.PediaDirector.Unlock(entry, packet.Popup));
        if (!unlocked)
        {
            SrLogger.LogWarning($"Pedia unlock '{packet.ID}' from {DescribeClient(senderEndPoint)} was rejected by the host pedia director.", SrLogTarget.Both);
            return;
        }

        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }

    private string DescribeClient(IPEndPoint senderEndPoint)
        => clientManager.TryGetClient(senderEndPoint, out var client) && client != null
            ? $"{client.PlayerId} ({senderEndPoint})"
            : senderEndPoint.ToString();
}
