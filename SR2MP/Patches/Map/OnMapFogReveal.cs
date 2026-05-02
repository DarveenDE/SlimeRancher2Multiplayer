using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Map;
using SR2MP.Packets.World;

namespace SR2MP.Patches.Map;

// Intercepts the per-position fog-of-war reveal and broadcasts it to all connected clients.
// NOTE: The exact method name depends on the SR2 assembly. If "RevealFog" is not found at
// runtime, Harmony will skip this patch without crashing; verify against the current SR2 DLLs.
[HarmonyPatch(typeof(MapDirector), "RevealFog")]
public static class OnMapFogReveal
{
    public static void Postfix(Vector3 position, float radius)
    {
        if (handlingPacket || (!Main.Server.IsRunning() && !Main.Client.IsConnected))
            return;

        Main.SendToAllOrServer(new MapFogPacket
        {
            Position = position,
            Radius = radius,
        });
    }
}
