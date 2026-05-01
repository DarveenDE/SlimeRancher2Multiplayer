using Il2CppMonomiPark.SlimeRancher.Economy;
using SR2MP.Shared.Managers;
using SR2MP.Components.Player;
using SR2MP.Packets.Loading;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Utils;

namespace SR2MP.Client.Handlers;

[PacketHandler((byte)PacketType.ConnectAck)]
public sealed class ConnectAckHandler : BaseClientPacketHandler<ConnectAckPacket>
{
    public ConnectAckHandler(Client client, RemotePlayerManager playerManager)
        : base(client, playerManager) { }

    protected override void Handle(ConnectAckPacket packet)
    {
        if (!NetworkProtocol.TryValidatePeer("your client", "host", packet.ProtocolVersion, packet.RequiredGameVersion, out var rejectMessage))
        {
            SrLogger.LogWarning(
                $"Rejected hosted world during handshake: {rejectMessage} HostMod={packet.ModVersion}",
                SrLogTarget.Both);
            Client.RejectConnection(rejectMessage);
            return;
        }

        if (!string.Equals(packet.ModVersion, NetworkProtocol.ModVersion, StringComparison.OrdinalIgnoreCase))
        {
            SrLogger.LogWarning(
                $"Host is using SR2MP {packet.ModVersion}; client is using {NetworkProtocol.ModVersion}. Protocol is compatible, continuing.",
                SrLogTarget.Both);
        }

        SrLogger.LogMessage($"Connection acknowledged by server; waiting for initial sync (PlayerId: {packet.PlayerId})",
            SrLogTarget.Both);

        NetworkSessionState.PhaseGate.TryTransition(
            SessionPhase.InitialSync,
            $"ConnectAck received (PlayerId: {packet.PlayerId})");

        SceneContext.Instance.PlayerState._model.SetCurrency(GameContext.Instance.LookupDirector._currencyList[0].Cast<ICurrency>(), packet.Money);
        SceneContext.Instance.PlayerState._model.SetCurrency(GameContext.Instance.LookupDirector._currencyList[1].Cast<ICurrency>(), packet.RainbowMoney);

        cheatsEnabled = packet.AllowCheats;

        foreach (var (id, username) in packet.OtherPlayers)
        {
            SpawnPlayer(id, username);
        }
    }

    private void SpawnPlayer(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id) || id == Client.OwnPlayerId)
            return;

        if (playerManager.GetPlayer(id) != null || playerObjects.ContainsKey(id))
            return;

        var playerObject = Object.Instantiate(playerPrefab).GetComponent<NetworkPlayer>();
        playerObject.gameObject.SetActive(true);
        playerObject.ID = id;
        playerObject.gameObject.name = id;
        playerObjects.Add(id, playerObject.gameObject);
        playerManager.AddPlayer(id).Username = name;
        Object.DontDestroyOnLoad(playerObject);
    }
}
