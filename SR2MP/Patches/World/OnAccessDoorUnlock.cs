using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher;
using Il2CppMonomiPark.World;
using SR2MP.Packets.World;

namespace SR2MP.Patches.World;

[HarmonyPatch(typeof(AccessDoor), "set_CurrState")]
public static class OnAccessDoorUnlock
{
    public static void Postfix(AccessDoor __instance, AccessDoor.State value)
    {
        if (handlingPacket || value != AccessDoor.State.OPEN)
            return;

        var id = GetDoorId(__instance);
        if (id == null)
            return;

        var packet = new AccessDoorPacket
        {
            ID = id,
            State = AccessDoor.State.OPEN
        };
        Main.SendToAllOrServer(packet);
    }

    private static string? GetDoorId(AccessDoor door)
    {
        foreach (var doorEntry in SceneContext.Instance.GameModel.doors)
        {
            var model = doorEntry.Value;
            if (model == door._model || model.gameObj == door.gameObject)
                return doorEntry.Key;
        }

        return null;
    }
}
