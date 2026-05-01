using System.Net;
using Il2CppMonomiPark.SlimeRancher.Player.PlayerItems;
using SR2MP.Packets.Actor;
using SR2MP.Packets.Utils;
using SR2MP.Server.Managers;

namespace SR2MP.Server.Handlers;

[PacketHandler((byte)PacketType.ActorTransfer)]
public sealed class ActorTransferHandler : BasePacketHandler<ActorTransferPacket>
{
    public ActorTransferHandler(NetworkManager networkManager, ClientManager clientManager)
        : base(networkManager, clientManager) { }

    protected override void Handle(ActorTransferPacket packet, IPEndPoint clientEp)
    {
        if (!clientManager.TryGetClient(clientEp, out var client) || client == null)
            return;

        if (!actorManager.Actors.TryGetValue(packet.ActorId.Value, out var actor))
        {
            SrLogger.LogWarning($"Rejected actor transfer from {client.PlayerId} ({clientEp}); actor {packet.ActorId.Value} does not exist.", SrLogTarget.Both);
            return;
        }

        // Authority: current owner must be the host or the requesting client.
        if (!CheckAuthority(packet, client.PlayerId, clientEp).IsAllowed)
            return;

        packet.OwnerPlayer = client.PlayerId;

        if (actor.TryGetNetworkComponent(out var component))
        {
            var vac = SceneContext.Instance.Player.GetComponent<PlayerItemController>()._vacuumItem;
            var gameObject = actor.GetGameObject();
            if (gameObject && vac._held == gameObject)
            {
                vac.LockJoint.connectedBody = null;
                vac._held = null;
                vac.SetHeldRad(0f);
                vac._vacMode = VacuumItem.VacMode.NONE;
                gameObject.GetComponent<Vacuumable>().Release();
            }

            component.LocallyOwned = false;
        }

        actorManager.SetActorOwner(packet.ActorId.Value, client.PlayerId);

        Main.Server.SendToAll(packet);
    }
}
