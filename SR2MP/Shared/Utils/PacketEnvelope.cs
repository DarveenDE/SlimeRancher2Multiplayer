using System.Net;
using SR2MP.Packets.Utils;

namespace SR2MP.Shared.Utils;

/// <summary>
/// Carries all context needed by an <see cref="AuthorityRule"/> at check time:
/// who sent the packet, the decoded packet body, and the resolved PlayerId.
/// </summary>
public sealed class PacketEnvelope
{
    public string PlayerId { get; }
    public System.Net.IPEndPoint SenderEndPoint { get; }
    public PacketType PacketType { get; }
    public IPacket Packet { get; }

    public PacketEnvelope(string playerId, System.Net.IPEndPoint senderEndPoint, PacketType packetType, IPacket packet)
    {
        PlayerId = playerId;
        SenderEndPoint = senderEndPoint;
        PacketType = packetType;
        Packet = packet;
    }
}
