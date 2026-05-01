using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Economy;
using SR2MP.Packets.Economy;

namespace SR2MP.Patches.Economy;

[HarmonyPatch(typeof(PlayerState))]
public static class CurrencyPatch
{
    [HarmonyPostfix, HarmonyPatch(nameof(PlayerState.AddCurrency))]
    public static void AddCurrency(
        PlayerState __instance,
        ICurrency currencyDefinition,
        int adjust,
        bool showUiNotification)
    {
        if (handlingPacket) return;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (currencyDefinition == null)
            return;

        var currency = currencyDefinition.PersistenceId;

        var packet = new CurrencyPacket
        {
            NewAmount = __instance._model.GetCurrencyAmount(currencyDefinition),
            PreviousAmount = __instance._model.GetCurrencyAmount(currencyDefinition) - adjust,
            DeltaAmount = adjust,
            CurrencyType = (byte)currency,
            ShowUINotification = showUiNotification,
        };

        Main.SendToAllOrServer(packet);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(PlayerState.SpendCurrency))]
    public static void SpendCurrency(
        PlayerState __instance,
        ICurrency currency,
        int adjust)
    {
        if (handlingPacket) return;

        var currencyId = currency.PersistenceId;
        var newAmount = __instance._model.GetCurrencyAmount(currency);

        var packet = new CurrencyPacket
        {
            NewAmount = newAmount,
            PreviousAmount = newAmount + adjust,
            DeltaAmount = -adjust,
            CurrencyType = (byte)currencyId,
            ShowUINotification = true,
        };

        Main.SendToAllOrServer(packet);
    }
}
