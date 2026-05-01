using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.Upgrades;

namespace SR2MP.Patches.Player;

[HarmonyPatch(typeof(UpgradeModel), nameof(UpgradeModel.IncrementUpgradeLevel))]
public static class OnPlayerUpgraded
{
    public static void Postfix(UpgradeDefinition definition, bool __result)
    {
        if (handlingPacket) return;
        if (!__result || definition == null) return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;

        var packet = new PlayerUpgradePacket
        {
            UpgradeID = (byte)definition._uniqueId,
            TargetLevel = (sbyte)SceneContext.Instance.PlayerState._model.upgradeModel.GetUpgradeLevel(definition)
        };

        Main.SendToAllOrServer(packet);
    }
}
