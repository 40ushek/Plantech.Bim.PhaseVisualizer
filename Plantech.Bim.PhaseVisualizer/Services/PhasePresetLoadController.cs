using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhasePresetLoadController
{
    private readonly PhasePresetController _presetController;
    private readonly PhasePresetApplyController _presetApplyController;

    public PhasePresetLoadController(
        PhasePresetController presetController,
        PhasePresetApplyController presetApplyController)
    {
        _presetController = presetController ?? throw new ArgumentNullException(nameof(presetController));
        _presetApplyController = presetApplyController ?? throw new ArgumentNullException(nameof(presetApplyController));
    }

    public PhasePresetLoadResult TryLoad(
        PhaseTableState? state,
        string presetName,
        IDictionary<int, PhaseTableRowState> rowCacheByPhase,
        bool currentShowAllPhases,
        bool currentUseVisibleViewsForSearch)
    {
        if (string.IsNullOrWhiteSpace(presetName)
            || rowCacheByPhase == null
            || !_presetController.TryGet(state, presetName, out var preset)
            || preset == null)
        {
            return PhasePresetLoadResult.Failure(currentShowAllPhases, currentUseVisibleViewsForSearch);
        }

        var applyPreset = _presetApplyController.Apply(
            preset,
            rowCacheByPhase,
            currentShowAllPhases,
            currentUseVisibleViewsForSearch);

        return PhasePresetLoadResult.Success(
            preset.Name,
            applyPreset.ShowAllPhases,
            applyPreset.UseVisibleViewsForSearch,
            applyPreset.RequiresReload);
    }
}

internal sealed class PhasePresetLoadResult
{
    private PhasePresetLoadResult(
        bool isSuccess,
        string presetName,
        bool showAllPhases,
        bool useVisibleViewsForSearch,
        bool requiresReload)
    {
        IsSuccess = isSuccess;
        PresetName = presetName ?? string.Empty;
        ShowAllPhases = showAllPhases;
        UseVisibleViewsForSearch = useVisibleViewsForSearch;
        RequiresReload = requiresReload;
    }

    public bool IsSuccess { get; }

    public string PresetName { get; }

    public bool ShowAllPhases { get; }

    public bool UseVisibleViewsForSearch { get; }

    public bool RequiresReload { get; }

    public static PhasePresetLoadResult Success(
        string presetName,
        bool showAllPhases,
        bool useVisibleViewsForSearch,
        bool requiresReload)
    {
        return new PhasePresetLoadResult(
            isSuccess: true,
            presetName: presetName,
            showAllPhases: showAllPhases,
            useVisibleViewsForSearch: useVisibleViewsForSearch,
            requiresReload: requiresReload);
    }

    public static PhasePresetLoadResult Failure(
        bool showAllPhases,
        bool useVisibleViewsForSearch)
    {
        return new PhasePresetLoadResult(
            isSuccess: false,
            presetName: string.Empty,
            showAllPhases: showAllPhases,
            useVisibleViewsForSearch: useVisibleViewsForSearch,
            requiresReload: false);
    }
}
