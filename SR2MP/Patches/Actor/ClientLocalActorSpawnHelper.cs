using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Components.Actor;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.Actor;

internal static class ClientLocalActorSpawnHelper
{
    public static bool TryPrepareForLocalNetworkSpawn(
        GameObject actor,
        Identifiable identifiable,
        string source,
        out ActorId actorId,
        out bool shouldSendSpawn)
    {
        shouldSendSpawn = true;
        actorId = identifiable.GetActorId();

        if (actorId.Value == 0)
            return false;

        if (!Main.Client.IsConnected)
            return true;

        if (actorManager.Actors.ContainsKey(actorId.Value))
        {
            shouldSendSpawn = false;
            return true;
        }

        if (!NetworkSessionState.TryGetAssignedActorIdRange(out var minActorId, out var maxActorId))
        {
            SrLogger.LogWarning(
                $"Not sending client-local actor spawn from {source} for actor {actorId.Value}; no client actor-id range has been assigned yet.",
                SrLogTarget.Both);
            return false;
        }

        if (actorId.Value >= minActorId && actorId.Value < maxActorId)
            return true;

        return TryRemapActorId(actor, identifiable, actorId, minActorId, maxActorId, source, out actorId);
    }

    public static bool TrySendSpawnForUntrackedClientActor(
        GameObject actor,
        Identifiable identifiable,
        int actorType,
        int sceneGroup,
        string source,
        out ActorId actorId)
    {
        if (!TryPrepareForLocalNetworkSpawn(actor, identifiable, source, out actorId, out var shouldSendSpawn))
            return false;

        if (!shouldSendSpawn)
            return true;

        var packet = CreateSpawnPacket(actor, actorId, actorType, sceneGroup);
        Main.SendToAllOrServer(packet);

        if (TryGetModel(actorId, out var model))
            actorManager.Actors[actorId.Value] = model;
        actorManager.SetActorOwner(actorId.Value, LocalID);

        SrLogger.LogMessage(
            $"Sent client-local actor spawn actor={actorId.Value}, type={actorType}, scene={sceneGroup}, source={source}.",
            SrLogTarget.Main);
        return true;
    }

    public static ActorSpawnPacket CreateSpawnPacket(
        GameObject actor,
        ActorId actorId,
        int actorType,
        int sceneGroup)
        => new()
        {
            ActorType = actorType,
            SceneGroup = sceneGroup,
            ActorId = actorId,
            Position = actor.transform.position,
            Rotation = actor.transform.rotation,
        };

    private static bool TryRemapActorId(
        GameObject actor,
        Identifiable identifiable,
        ActorId oldActorId,
        long minActorId,
        long maxActorId,
        string source,
        out ActorId newActorId)
    {
        newActorId = default;

        if (!TryGetModel(oldActorId, out var model))
        {
            SrLogger.LogWarning(
                $"Not sending client-local actor spawn from {source} for actor {oldActorId.Value}; identifiable has no model.",
                SrLogTarget.Both);
            return false;
        }

        var nextActorId = NetworkActorManager.GetNextActorIdInRange(minActorId, maxActorId);
        newActorId = new ActorId(nextActorId);

        if (nextActorId < minActorId || nextActorId >= maxActorId
            || SceneContext.Instance.GameModel.identifiables.ContainsKey(newActorId))
        {
            SrLogger.LogWarning(
                $"Not sending client-local actor spawn from {source} for actor {oldActorId.Value}; no free id in assigned range [{minActorId}, {maxActorId}).",
                SrLogTarget.Both);
            return false;
        }

        SceneContext.Instance.GameModel.identifiables.Remove(oldActorId);
        model.actorId = newActorId;
        SceneContext.Instance.GameModel.identifiables[newActorId] = model;

        var networkActor = actor.GetComponent<NetworkActor>();
        if (networkActor)
            networkActor.OverrideActorIdCache(newActorId);

        SrLogger.LogMessage(
            $"Remapped client-local actor from {oldActorId.Value} to {newActorId.Value} for {source}.",
            SrLogTarget.Main);
        return true;
    }

    private static bool TryGetModel(ActorId actorId, out IdentifiableModel model)
    {
        model = null!;
        return SceneContext.Instance.GameModel.identifiables.TryGetValue(actorId, out model)
               && model != null;
    }
}
