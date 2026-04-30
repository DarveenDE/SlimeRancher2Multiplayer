using Il2CppMonomiPark.SlimeRancher.Economy;
using SR2MP.Packets.Economy;
using SR2MP.Shared.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.CurrencyAdjust)]
public sealed class CurrencyHandler : BaseClientPacketHandler<CurrencyPacket>
{
    public CurrencyHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(CurrencyPacket packet)
    {
        var currencies = GameContext.Instance.LookupDirector._currencyList._currencies;
        var currencyIndex = packet.CurrencyType - 1;
        if (currencyIndex < 0 || currencyIndex >= currencies.Count)
        {
            SrLogger.LogWarning($"Ignoring currency update with invalid currency type {packet.CurrencyType}.", SrLogTarget.Both);
            return;
        }

        var currency = currencies[currencyIndex];
        if (!currency)
            return;

        var currencyDefinition = currency.Cast<ICurrency>();

        var difference = packet.NewAmount - SceneContext.Instance.PlayerState.GetCurrency(currencyDefinition);

        RunWithHandlingPacket(() =>
        {
            if (difference < 0)
                SceneContext.Instance.PlayerState.SpendCurrency(currencyDefinition, -difference);
            else
                SceneContext.Instance.PlayerState.AddCurrency(currencyDefinition, difference, packet.ShowUINotification);
        });
    }
}
