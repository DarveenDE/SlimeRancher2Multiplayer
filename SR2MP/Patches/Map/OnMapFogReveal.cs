using System.Reflection;
using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Map;
using SR2MP.Packets.World;

namespace SR2MP.Patches.Map;

// RevealFog was removed from MapDirector in SR2 Patch 1.2; zone-based reveals
// now go through MapUnlock. TargetMethod returns null so Harmony always skips
// this patch cleanly, while the Postfix body remains for if the method returns.
[HarmonyPatch]
public static class OnMapFogReveal
{
    public static MethodBase? TargetMethod()
        => typeof(MapDirector).GetMethod("RevealFog",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(Vector3), typeof(float) }, null);

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
