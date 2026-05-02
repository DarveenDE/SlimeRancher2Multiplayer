using HarmonyLib;
using SR2MP.Packets.Geyser;

namespace SR2MP.Patches.Geyser;

[HarmonyPatch(typeof(Il2Cpp.Geyser), nameof(Il2Cpp.Geyser.RunGeyser))]
public static class OnGeyserFire
{
    public static void Prefix(Il2Cpp.Geyser __instance, float duration)
    {
        if (handlingPacket) return;
        if (!Main.Server.IsRunning() && !Main.Client.IsConnected) return;

        Main.SendToAllOrServer(new GeyserTriggerPacket
        {
            ObjectPath = __instance.gameObject.GetGameObjectPath(),
            Duration = duration,
        });
    }
}