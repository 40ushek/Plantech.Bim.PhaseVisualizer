using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhasePresetMutationController
{
    private readonly PhaseTableStateController _stateController;
    private readonly PhasePresetController _presetController;

    public PhasePresetMutationController(
        PhaseTableStateController stateController,
        PhasePresetController presetController)
    {
        _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
        _presetController = presetController ?? throw new ArgumentNullException(nameof(presetController));
    }

    public PhasePresetMutationResult TrySave(
        string? stateFilePath,
        string? presetName,
        bool showAllPhases,
        bool useVisibleViewsForSearch,
        IReadOnlyCollection<PhaseTableRowState> presetRows,
        ILogger log)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath)
            || presetName == null)
        {
            return PhasePresetMutationResult.Failure();
        }

        var normalizedPresetName = presetName.Trim();
        if (normalizedPresetName.Length == 0)
        {
            return PhasePresetMutationResult.Failure();
        }

        var state = _stateController.Load(stateFilePath, log) ?? new PhaseTableState();
        if (!_presetController.SaveOrUpdate(
                state,
                normalizedPresetName,
                showAllPhases,
                useVisibleViewsForSearch,
                presetRows))
        {
            return PhasePresetMutationResult.Failure();
        }

        _stateController.Save(stateFilePath, state, log);
        return PhasePresetMutationResult.Success(state, normalizedPresetName);
    }

    public PhasePresetMutationResult TryDelete(
        string? stateFilePath,
        string? presetName,
        ILogger log)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath)
            || presetName == null)
        {
            return PhasePresetMutationResult.Failure();
        }

        var normalizedPresetName = presetName.Trim();
        if (normalizedPresetName.Length == 0)
        {
            return PhasePresetMutationResult.Failure();
        }

        var state = _stateController.Load(stateFilePath, log);
        if (state == null || !_presetController.Delete(state, normalizedPresetName))
        {
            return PhasePresetMutationResult.Failure();
        }

        _stateController.Save(stateFilePath, state, log);
        return PhasePresetMutationResult.Success(state, normalizedPresetName);
    }
}

internal sealed class PhasePresetMutationResult
{
    private PhasePresetMutationResult(bool isSuccess, PhaseTableState? state, string presetName)
    {
        IsSuccess = isSuccess;
        State = state;
        PresetName = presetName ?? string.Empty;
    }

    public bool IsSuccess { get; }

    public PhaseTableState? State { get; }

    public string PresetName { get; }

    public static PhasePresetMutationResult Success(PhaseTableState state, string presetName)
    {
        return new PhasePresetMutationResult(true, state, presetName);
    }

    public static PhasePresetMutationResult Failure()
    {
        return new PhasePresetMutationResult(false, null, string.Empty);
    }
}
