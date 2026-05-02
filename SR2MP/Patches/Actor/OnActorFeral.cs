using HarmonyLib;
using SR2MP.Components.Actor;
using SR2MP.Packets.Actor;
using SR2MP.Shared.Utils;

namespace SR2MP.Patches.Actor;

// Intercepts SlimeFeral.SetFeral() (public void, no params) to broadcast feral mutation.
// Uses string-based patching so Harmony silently skips if the method name differs in this
// SR2 build — verify against current DLLs when updating the game.
[HarmonyPatch(typeof(SlimeFeral), "SetFeral")]
public static class OnActorFeral
{
    public static void Postfix(SlimeFeral __instance)
    {
        if (handlingPacket) return;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;
        if (NetworkSessionState.InitialActorLoadInProgress) return;

        var networkActor = __instance.GetComponent<NetworkActor>();
        if (networkActor == null || !networkActor.LocallyOwned) return;

        var actorId = networkActor.ActorId;
        if (actorId.Value == 0) return;

        Main.SendToAllOrServer(new ActorFeralPacket { ActorId = actorId });
    }
}
