using System.Net;
using SR2MP.Packets.Upgrades;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.PlayerUpgrade)]
public sealed class PlayerUpgradeHandler : BasePacketHandler<PlayerUpgradePacket>
{
    public PlayerUpgradeHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(PlayerUpgradePacket packet, IPEndPoint senderEndPoint)
    {
        var model = SceneContext.Instance.PlayerState._model.upgradeModel;

        var upgrade = model.upgradeDefinitions.items._items.FirstOrDefault(
            x => x._uniqueId == packet.UpgradeID);

        if (upgrade == null)
        {
            SrLogger.LogWarning($"Ignoring player upgrade with unknown id {packet.UpgradeID} from {DescribeClient(senderEndPoint)}.", SrLogTarget.Both);
            return;
        }

        var currentLevel = model.GetUpgradeLevel(upgrade);
        var nextLevel = currentLevel + 1;
        if (!upgrade.UpgradeLevelExist(nextLevel))
        {
            SrLogger.LogWarning(
                $"Ignoring player upgrade '{upgrade.name}' ({packet.UpgradeID}) from {DescribeClient(senderEndPoint)}; current level {currentLevel} cannot advance to {nextLevel}.",
                SrLogTarget.Both);
            return;
        }

        var upgraded = false;
        RunWithHandlingPacket(() => upgraded = model.IncrementUpgradeLevel(upgrade));
        if (!upgraded)
        {
            SrLogger.LogWarning(
                $"Player upgrade '{upgrade.name}' ({packet.UpgradeID}) from {DescribeClient(senderEndPoint)} was rejected by the host upgrade model.",
                SrLogTarget.Both);
            return;
        }

        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }

    private string DescribeClient(IPEndPoint senderEndPoint)
        => clientManager.TryGetClient(senderEndPoint, out var client) && client != null
            ? $"{client.PlayerId} ({senderEndPoint})"
            : senderEndPoint.ToString();
}
