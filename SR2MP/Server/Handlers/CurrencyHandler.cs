using System.Net;
using Il2CppMonomiPark.SlimeRancher.Economy;
using SR2MP.Packets.Economy;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.CurrencyAdjust)]
public sealed class CurrencyHandler : BasePacketHandler<CurrencyPacket>
{
    public CurrencyHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(CurrencyPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        // Validate: currency type is known.
        if (!TryGetCurrency(packet.CurrencyType, out var currencyDefinition))
        {
            SrLogger.LogWarning(
                $"Ignoring currency update with invalid currency type {packet.CurrencyType} from {DescribeClient(clientEp)}.",
                SrLogTarget.Both);
            return;
        }

        // Authority: baseline match (HasBaseline + PreviousAmount == hostAmount).
        // Pipeline logs the rejection; we send a correction so the client converges.
        if (!CheckAuthority(packet, client.PlayerId, clientEp).IsAllowed)
        {
            var correctionAmount = SceneContext.Instance.PlayerState.GetCurrency(currencyDefinition);
            SendHostAmount(packet.CurrencyType, correctionAmount, packet.ShowUINotification, clientEp);
            return;
        }

        if (packet.PreviousAmount + packet.DeltaAmount != packet.NewAmount)
        {
            SrLogger.LogWarning(
                $"Ignoring malformed currency update for type {packet.CurrencyType} from {DescribeClient(clientEp)}; previous {packet.PreviousAmount} + delta {packet.DeltaAmount} != requested {packet.NewAmount}.",
                SrLogTarget.Both);
            var correctionAmount = SceneContext.Instance.PlayerState.GetCurrency(currencyDefinition);
            SendHostAmount(packet.CurrencyType, correctionAmount, packet.ShowUINotification, clientEp);
            return;
        }

        var requestedNewAmount = packet.NewAmount;
        var hostAmount = SceneContext.Instance.PlayerState.GetCurrency(currencyDefinition);
        var expectedNewAmount = hostAmount + packet.DeltaAmount;

        if (expectedNewAmount < 0)
        {
            SrLogger.LogWarning(
                $"Ignoring currency spend for type {packet.CurrencyType} from {DescribeClient(clientEp)}; requested amount would go negative ({expectedNewAmount}).",
                SrLogTarget.Both);
            SendHostAmount(packet.CurrencyType, hostAmount, packet.ShowUINotification, clientEp);
            return;
        }

        RunWithHandlingPacket(() =>
        {
            if (packet.DeltaAmount < 0)
                SceneContext.Instance.PlayerState.SpendCurrency(currencyDefinition, -packet.DeltaAmount);
            else
                SceneContext.Instance.PlayerState.AddCurrency(currencyDefinition, packet.DeltaAmount, packet.ShowUINotification);
        });

        var acceptedAmount = SceneContext.Instance.PlayerState.GetCurrency(currencyDefinition);
        packet.PreviousAmount = hostAmount;
        packet.DeltaAmount = acceptedAmount - hostAmount;
        packet.NewAmount = acceptedAmount;

        if (acceptedAmount == requestedNewAmount)
            Main.Server.SendToAllExcept(packet, clientEp);
        else
            Main.Server.SendToAll(packet);
    }

    private static bool TryGetCurrency(byte currencyType, out ICurrency currencyDefinition)
    {
        currencyDefinition = null!;

        var currencies = GameContext.Instance.LookupDirector._currencyList._currencies;
        var currencyIndex = currencyType - 1;
        if (currencyIndex < 0 || currencyIndex >= currencies.Count)
            return false;

        var currency = currencies[currencyIndex];
        if (!currency)
            return false;

        currencyDefinition = currency.Cast<ICurrency>();
        return currencyDefinition != null;
    }

    private static void SendHostAmount(byte currencyType, int hostAmount, bool showUiNotification, IPEndPoint clientEp)
    {
        Main.Server.SendToClient(new CurrencyPacket
        {
            CurrencyType = currencyType,
            NewAmount = hostAmount,
            PreviousAmount = hostAmount,
            DeltaAmount = 0,
            ShowUINotification = showUiNotification,
        }, clientEp);
    }

    private string DescribeClient(IPEndPoint clientEp)
        => clientManager.TryGetClient(clientEp, out var client) && client != null
            ? $"{client.PlayerId} ({clientEp})"
            : clientEp.ToString();
}
