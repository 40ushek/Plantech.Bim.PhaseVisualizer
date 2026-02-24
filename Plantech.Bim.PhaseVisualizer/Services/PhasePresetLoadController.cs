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
        PhaseSearchScope currentSearchScope)
    {
        if (string.IsNullOrWhiteSpace(presetName)
            || rowCacheByPhase == null
            || !_presetController.TryGet(state, presetName, out var preset)
            || preset == null)
        {
            return PhasePresetLoadResult.Failure(currentShowAllPhases, currentSearchScope);
        }

        var applyPreset = _presetApplyController.Apply(
            preset,
            rowCacheByPhase,
            currentShowAllPhases,
            currentSearchScope);

        return PhasePresetLoadResult.Success(
            preset.Name,
            applyPreset.ShowAllPhases,
            applyPreset.SearchScope,
            applyPreset.RequiresReload);
    }
}

internal sealed class PhasePresetLoadResult
{
    private PhasePresetLoadResult(
        bool isSuccess,
        string presetName,
        bool showAllPhases,
        PhaseSearchScope searchScope,
        bool requiresReload)
    {
        IsSuccess = isSuccess;
        PresetName = presetName ?? string.Empty;
        ShowAllPhases = showAllPhases;
        SearchScope = searchScope;
        RequiresReload = requiresReload;
    }

    public bool IsSuccess { get; }

    public string PresetName { get; }

    public bool ShowAllPhases { get; }

    public PhaseSearchScope SearchScope { get; }

    public bool RequiresReload { get; }

    public static PhasePresetLoadResult Success(
        string presetName,
        bool showAllPhases,
        PhaseSearchScope searchScope,
        bool requiresReload)
    {
        return new PhasePresetLoadResult(
            isSuccess: true,
            presetName: presetName,
            showAllPhases: showAllPhases,
            searchScope: searchScope,
            requiresReload: requiresReload);
    }

    public static PhasePresetLoadResult Failure(
        bool showAllPhases,
        PhaseSearchScope searchScope)
    {
        return new PhasePresetLoadResult(
            isSuccess: false,
            presetName: string.Empty,
            showAllPhases: showAllPhases,
            searchScope: searchScope,
            requiresReload: false);
    }
}
