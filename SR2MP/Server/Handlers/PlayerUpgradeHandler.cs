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
        var requestedLevel = packet.TargetLevel >= 0 ? packet.TargetLevel : (sbyte)(currentLevel + 1);

        if (requestedLevel <= currentLevel)
        {
            SrLogger.LogDebug(
                $"Ignoring stale player upgrade '{upgrade.name}' ({packet.UpgradeID}) from {DescribeClient(senderEndPoint)}; host level is already {currentLevel}, requested {requestedLevel}.",
                SrLogTarget.Main);
            SendHostLevel(packet.UpgradeID, (sbyte)currentLevel, senderEndPoint);
            return;
        }

        if (requestedLevel != currentLevel + 1 || !upgrade.UpgradeLevelExist(requestedLevel))
        {
            SrLogger.LogWarning(
                $"Ignoring player upgrade '{upgrade.name}' ({packet.UpgradeID}) from {DescribeClient(senderEndPoint)}; host level {currentLevel} cannot advance to requested level {requestedLevel}.",
                SrLogTarget.Both);
            SendHostLevel(packet.UpgradeID, (sbyte)currentLevel, senderEndPoint);
            return;
        }

        var upgraded = false;
        RunWithHandlingPacket(() => upgraded = model.IncrementUpgradeLevel(upgrade));
        if (!upgraded)
        {
            SrLogger.LogWarning(
                $"Player upgrade '{upgrade.name}' ({packet.UpgradeID}) from {DescribeClient(senderEndPoint)} was rejected by the host upgrade model.",
                SrLogTarget.Both);
            SendHostLevel(packet.UpgradeID, (sbyte)currentLevel, senderEndPoint);
            return;
        }

        packet.TargetLevel = (sbyte)model.GetUpgradeLevel(upgrade);
        Main.Server.SendToAllExcept(packet, senderEndPoint);
    }

    private static void SendHostLevel(byte upgradeId, sbyte level, IPEndPoint senderEndPoint)
    {
        Main.Server.SendToClient(new PlayerUpgradePacket
        {
            UpgradeID = upgradeId,
            TargetLevel = level,
        }, senderEndPoint);
    }

    private string DescribeClient(IPEndPoint senderEndPoint)
        => clientManager.TryGetClient(senderEndPoint, out var client) && client != null
            ? $"{client.PlayerId} ({senderEndPoint})"
            : senderEndPoint.ToString();
}
