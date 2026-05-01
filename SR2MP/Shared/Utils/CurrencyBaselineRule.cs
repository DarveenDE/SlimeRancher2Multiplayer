using Il2CppMonomiPark.SlimeRancher.Economy;
using SR2MP.Packets.Economy;

namespace SR2MP.Shared.Utils;

/// <summary>
/// A currency update is valid only if the client is working from an up-to-date
/// baseline: <see cref="CurrencyPacket.PreviousAmount"/> must equal the host's
/// current amount for that currency type.
///
/// Rejects legacy packets that omit the baseline fields (<see cref="CurrencyPacket.HasBaseline"/> = false).
///
/// Used for: CurrencyAdjust.
/// </summary>
public sealed class CurrencyBaselineRule : AuthorityRule
{
    public override AuthorityResult Check(PacketEnvelope env)
    {
        if (env.Packet is not CurrencyPacket packet)
            return AuthorityResult.Allowed;

        if (!packet.HasBaseline)
            return AuthorityResult.Reject("packet has no baseline (legacy client)");

        if (!TryGetHostAmount(packet.CurrencyType, out var hostAmount))
            return AuthorityResult.Allowed; // game state unavailable — let handler handle

        if (packet.PreviousAmount == hostAmount)
            return AuthorityResult.Allowed;

        // Positive currency deltas are additive: if the host has already moved past
        // the client's baseline, apply the delta on top of the host amount instead
        // of dropping a real sale. Negative spends still require an exact baseline.
        if (packet.DeltaAmount > 0 && hostAmount >= packet.PreviousAmount)
            return AuthorityResult.Allowed;

        if (packet.PreviousAmount != hostAmount)
            return AuthorityResult.Reject(
                $"stale baseline for currency {packet.CurrencyType}: " +
                $"host={hostAmount}, clientExpected={packet.PreviousAmount}, requestedNew={packet.NewAmount}");

        return AuthorityResult.Allowed;
    }

    private static bool TryGetHostAmount(byte currencyType, out int hostAmount)
    {
        hostAmount = 0;

        if (SceneContext.Instance == null || SceneContext.Instance.PlayerState == null)
            return false;

        var currencies = GameContext.Instance?.LookupDirector?._currencyList?._currencies;
        if (currencies == null)
            return false;

        var idx = currencyType - 1;
        if (idx < 0 || idx >= currencies.Count)
            return false;

        var currency = currencies[idx];
        if (!currency)
            return false;

        var iCurrency = currency.Cast<ICurrency>();
        if (iCurrency == null)
            return false;

        hostAmount = SceneContext.Instance.PlayerState.GetCurrency(iCurrency);
        return true;
    }
}
