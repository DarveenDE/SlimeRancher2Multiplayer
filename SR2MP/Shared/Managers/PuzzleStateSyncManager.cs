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

    public static bool TryGetSlotState(string id, out bool filled)
    {
        filled = false;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel || string.IsNullOrWhiteSpace(id))
            return false;

        if (!SceneContext.Instance.GameModel.slots.TryGetValue(id, out var model) || model == null)
            return false;

        filled = model.filled;
        return true;
    }

    public static bool TryGetDepositorAmount(string id, out int amountDeposited)
    {
        amountDeposited = 0;

        if (!SceneContext.Instance || !SceneContext.Instance.GameModel || string.IsNullOrWhiteSpace(id))
            return false;

        if (!SceneContext.Instance.GameModel.depositors.TryGetValue(id, out var model) || model == null)
            return false;

        amountDeposited = model.AmountDeposited;
        return true;
    }

    public static bool ApplySlotState(string id, bool filled, string source)
    {
        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var gameModel = SceneContext.Instance.GameModel;
        var isRepairSnapshot = IsRepairSource(source);

        if (!gameModel.slots.TryGetValue(id, out var model))
        {
            gameModel.slots.Add(id, new PuzzleSlotModel
            {
                filled = filled,
                gameObj = null,
            });
            LogSlotRepairCorrection(isRepairSnapshot, id, false, false, true, filled);
            return true;
        }

        var beforeFilled = model.filled;
        model.filled = filled;
        LogSlotRepairCorrection(isRepairSnapshot, id, true, beforeFilled, true, filled);

        if (!model.gameObj)
        {
            LogMissingVisibleTarget("Puzzle slot", id, source);
            return true;
        }

        var slot = model.gameObj.GetComponent<PuzzleSlot>();
        if (!slot)
        {
            SrLogger.LogWarning($"Puzzle slot '{id}' has no PuzzleSlot component while applying {source}", SrLogTarget.Both);
            return false;
        }

        RunWithHandlingPacket(() => slot.OnFilledChangedFromModel());

        return true;
    }

    public static bool ApplyDepositorState(string id, int amountDeposited, string source)
    {
        if (!SceneContext.Instance || !SceneContext.Instance.GameModel)
            return false;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var gameModel = SceneContext.Instance.GameModel;
        var isRepairSnapshot = IsRepairSource(source);

        if (!gameModel.depositors.TryGetValue(id, out var model))
        {
            gameModel.depositors.Add(id, new PlortDepositorModel
            {
                AmountDeposited = amountDeposited,
                _gameObject = null,
            });
            LogDepositorRepairCorrection(isRepairSnapshot, id, false, 0, true, amountDeposited);
            return true;
        }

        var beforeAmount = model.AmountDeposited;
        model.AmountDeposited = amountDeposited;
        LogDepositorRepairCorrection(isRepairSnapshot, id, true, beforeAmount, true, amountDeposited);

        if (!model._gameObject)
        {
            LogMissingVisibleTarget("Plort depositor", id, source);
            return true;
        }

        var depositor = model._gameObject.GetComponent<PlortDepositor>();
        if (!depositor)
        {
            SrLogger.LogWarning($"Plort depositor '{id}' has no PlortDepositor component while applying {source}", SrLogTarget.Both);
            return false;
        }

        RunWithHandlingPacket(() => depositor.OnFilledChangedFromModel());

        return true;
    }

    private static void LogMissingVisibleTarget(string area, string id, string source)
    {
        if (source.Contains("initial", StringComparison.OrdinalIgnoreCase)
            || source.Contains("repair", StringComparison.OrdinalIgnoreCase))
            return;

        SrLogger.LogWarning(
            $"{area} '{id}' has no visible GameObject while applying {source}; model state was updated only.",
            SrLogTarget.Both);
    }

    private static void LogSlotRepairCorrection(
        bool isRepairSnapshot,
        string id,
        bool beforeExists,
        bool beforeFilled,
        bool targetExists,
        bool targetFilled)
    {
        if (!isRepairSnapshot)
            return;

        if (beforeExists == targetExists && beforeFilled == targetFilled)
            return;

        var beforeHash = HashBoolState(beforeExists, beforeFilled);
        var targetHash = HashBoolState(targetExists, targetFilled);
        SrLogger.LogMessage(
            $"Repair corrected puzzle slot '{id}' ({FormatHash(beforeHash)} -> {FormatHash(targetHash)}).",
            SrLogTarget.Main);
    }

    private static void LogDepositorRepairCorrection(
        bool isRepairSnapshot,
        string id,
        bool beforeExists,
        int beforeAmount,
        bool targetExists,
        int targetAmount)
    {
        if (!isRepairSnapshot)
            return;

        if (beforeExists == targetExists && beforeAmount == targetAmount)
            return;

        var beforeHash = HashAmountState(beforeExists, beforeAmount);
        var targetHash = HashAmountState(targetExists, targetAmount);
        SrLogger.LogMessage(
            $"Repair corrected plort depositor '{id}' ({FormatHash(beforeHash)} -> {FormatHash(targetHash)}).",
            SrLogTarget.Main);
    }

    private static bool IsRepairSource(string source)
        => source.Contains("repair", StringComparison.OrdinalIgnoreCase);

    private static int HashBoolState(bool exists, bool value)
    {
        var hash = 0;
        hash = AddHash(hash, exists ? 1 : 0);
        return AddHash(hash, value ? 1 : 0);
    }

    private static int HashAmountState(bool exists, int value)
    {
        var hash = 0;
        hash = AddHash(hash, exists ? 1 : 0);
        return AddHash(hash, value);
    }

    private static int AddHash(int hash, int value)
    {
        unchecked
        {
            return (hash * 397) ^ value;
        }
    }

    private static string FormatHash(int hash)
        => $"0x{hash:X8}";
}
