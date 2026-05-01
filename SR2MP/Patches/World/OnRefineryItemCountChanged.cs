using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.World;

[HarmonyPatch(typeof(GadgetsModel), nameof(GadgetsModel.SetCount))]
public static class OnRefineryItemCountChanged
{
    // Last count we sent for each persistent ID — suppresses the frequent identical-value
    // SetCount calls that the game engine makes every frame even when nothing changed.
    private static readonly Dictionary<int, int> _lastSentCount = new();

    public static void Postfix(IdentifiableType __0, int __1)
    {
        if (handlingPacket)
            return;

        if (!Main.Server.IsRunning() && !Main.Client.IsConnected)
            return;

        if (!__0)
            return;

        if (!RefinerySyncManager.IsRefineryItem(__0))
            return;

        var persistentId = NetworkActorManager.GetPersistentID(__0);
        var newCount = Math.Max(0, __1);

        if (_lastSentCount.TryGetValue(persistentId, out var prev) && prev == newCount)
            return;

        _lastSentCount[persistentId] = newCount;

        Main.SendToAllOrServer(new RefineryItemCountsPacket
        {
            Items = new Dictionary<int, int>
            {
                [persistentId] = newCount
            }
        });
    }
}
