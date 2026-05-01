using Il2CppMonomiPark.SlimeRancher.DataModel;

namespace SR2MP.Packets.Actor;

/// <summary>
/// Marks a packet that carries an <see cref="ActorId"/>,
/// allowing authority rules to read it without knowing the concrete struct type.
/// </summary>
public interface IActorPacket
{
    ActorId ActorId { get; }
}
