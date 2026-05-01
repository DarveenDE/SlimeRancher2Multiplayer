using System.Net;
using SR2MP.Server.Managers;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Utils;
// ReSharper disable InconsistentNaming

namespace SR2MP.Server.Handlers;

public abstract class BasePacketHandler<T> : IPacketHandler where T : IPacket, new()
{
    protected readonly NetworkManager networkManager;
    protected readonly ClientManager clientManager;

    protected BasePacketHandler(NetworkManager networkManager, ClientManager clientManager)
    {
        this.networkManager = networkManager;
        this.clientManager = clientManager;
    }

    public void Handle(byte[] data, IPEndPoint clientEp)
    {
        using var reader = new PacketReader(data);
        var packet = reader.ReadPacket<T>();

        Handle(packet, clientEp);
    }

    protected abstract void Handle(T packet, IPEndPoint clientEp);

    /// <summary>
    /// Evaluates the registered <see cref="AuthorityRule"/> for this packet type.
    /// Logging on rejection is handled by the pipeline — the handler only needs to
    /// check the return value and return early if <see cref="AuthorityResult.IsAllowed"/> is false.
    /// </summary>
    protected AuthorityResult CheckAuthority(T packet, string playerId, IPEndPoint senderEp)
        => AuthorityPipeline.Instance.Check(
            new PacketEnvelope(playerId, senderEp, packet.Type, packet));
}