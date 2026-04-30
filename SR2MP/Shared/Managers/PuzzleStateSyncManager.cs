using Il2CppMonomiPark.SlimeRancher.DataModel;

namespace SR2MP.Shared.Managers;

public static class PuzzleStateSyncManager
{
    public static bool TryGetSlotId(PuzzleSlotModel model, out string id)
    {
        id = string.Empty;

        foreach (var slot in SceneContext.Instance.GameModel.slots)
        {
            if (slot.Value == model)
            {
                id = slot.Key;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetDepositorId(PlortDepositorModel model, out string id)
    {
        id = string.Empty;

        foreach (var depositor in SceneContext.Instance.GameModel.depositors)
        {
            if (depositor.Value == model)
            {
                id = depositor.Key;
                return true;
            }
        }

        return false;
    }

    public static bool ApplySlotState(string id, bool filled, string source)
    {
        var gameModel = SceneContext.Instance.GameModel;

        if (!gameModel.slots.TryGetValue(id, out var model))
        {
            gameModel.slots.Add(id, new PuzzleSlotModel
            {
                filled = filled,
                gameObj = null,
            });
            return true;
        }

        model.filled = filled;

        if (!model.gameObj)
            return true;

        var slot = model.gameObj.GetComponent<PuzzleSlot>();
        if (!slot)
        {
            SrLogger.LogWarning($"Puzzle slot '{id}' has no PuzzleSlot component while applying {source}", SrLogTarget.Both);
            return false;
        }

        handlingPacket = true;
        try
        {
            slot.OnFilledChangedFromModel();
        }
        finally
        {
            handlingPacket = false;
        }

        return true;
    }

    public static bool ApplyDepositorState(string id, int amountDeposited, string source)
    {
        var gameModel = SceneContext.Instance.GameModel;

        if (!gameModel.depositors.TryGetValue(id, out var model))
        {
            gameModel.depositors.Add(id, new PlortDepositorModel
            {
                AmountDeposited = amountDeposited,
                _gameObject = null,
            });
            return true;
        }

        model.AmountDeposited = amountDeposited;

        if (!model._gameObject)
            return true;

        var depositor = model._gameObject.GetComponent<PlortDepositor>();
        if (!depositor)
        {
            SrLogger.LogWarning($"Plort depositor '{id}' has no PlortDepositor component while applying {source}", SrLogTarget.Both);
            return false;
        }

        handlingPacket = true;
        try
        {
            depositor.OnFilledChangedFromModel();
        }
        finally
        {
            handlingPacket = false;
        }

        return true;
    }
}
