using SR2MP.Packets.Upgrades;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.PlayerUpgrade)]
public sealed class PlayerUpgradeHandler : BaseClientPacketHandler<PlayerUpgradePacket>
{
    public PlayerUpgradeHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(PlayerUpgradePacket packet)
    {
        var model = SceneContext.Instance.PlayerState._model.upgradeModel;

        var upgrade = model.upgradeDefinitions.items._items.FirstOrDefault(
            x => x._uniqueId == packet.UpgradeID);

        if (upgrade == null)
        {
            SrLogger.LogWarning($"Ignoring player upgrade with unknown id {packet.UpgradeID}.", SrLogTarget.Both);
            return;
        }

        var currentLevel = model.GetUpgradeLevel(upgrade);
        if (packet.TargetLevel < 0)
        {
            var nextLevel = currentLevel + 1;
            if (!upgrade.UpgradeLevelExist(nextLevel))
            {
                SrLogger.LogWarning(
                    $"Ignoring legacy player upgrade '{upgrade.name}' ({packet.UpgradeID}); current level {currentLevel} cannot advance to {nextLevel}.",
                    SrLogTarget.Both);
                return;
            }

            var upgraded = false;
            RunWithHandlingPacket(() => upgraded = model.IncrementUpgradeLevel(upgrade));
            if (!upgraded)
                SrLogger.LogWarning($"Player upgrade '{upgrade.name}' ({packet.UpgradeID}) was rejected by the local upgrade model.", SrLogTarget.Both);

            return;
        }

        if (packet.TargetLevel == currentLevel)
            return;

        if (packet.TargetLevel > 0 && !upgrade.UpgradeLevelExist(packet.TargetLevel))
        {
            SrLogger.LogWarning(
                $"Ignoring player upgrade '{upgrade.name}' ({packet.UpgradeID}); target level {packet.TargetLevel} is invalid.",
                SrLogTarget.Both);
            return;
        }

        RunWithHandlingPacket(() => model.SetUpgradeLevel(upgrade, packet.TargetLevel));
    }
}
