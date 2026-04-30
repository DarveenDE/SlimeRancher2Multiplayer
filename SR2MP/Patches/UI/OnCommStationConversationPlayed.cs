using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Dialogue.CommStation;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.UI;

[HarmonyPatch(typeof(FixedConversation), nameof(FixedConversation.RecordPlayed))]
public static class OnFixedConversationPlayed
{
    public static void Postfix(FixedConversation __instance)
    {
        if (__instance)
            CommStationSyncManager.SendPlayed(__instance.GetId(), CommStationSyncManager.TargetConversation);
    }
}

[HarmonyPatch(typeof(ConversationListProvider), nameof(ConversationListProvider.RecordPlayed))]
public static class OnConversationProviderPlayed
{
    public static void Postfix(ConversationListProvider __instance)
    {
        if (__instance)
            CommStationSyncManager.SendPlayed(__instance.GetId(), CommStationSyncManager.TargetProvider);
    }
}

[HarmonyPatch(typeof(RancherDefinition), "RecordPlayed")]
public static class OnRancherConversationPlayed
{
    public static void Postfix(RancherDefinition __instance)
    {
        if (__instance)
            CommStationSyncManager.SendPlayed(__instance.GetId(), CommStationSyncManager.TargetRancher);
    }
}
