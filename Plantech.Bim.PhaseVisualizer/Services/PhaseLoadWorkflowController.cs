using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Orchestration;
using Serilog;
using System;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseLoadWorkflowController
{
    private readonly PhaseTableStateController _stateController;
    private readonly PhaseContextLoadController _contextLoadController;
    private readonly ILogger _log;

    public PhaseLoadWorkflowController(
        PhaseTableStateController stateController,
        PhaseContextLoadController contextLoadController,
        ILogger log)
    {
        _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
        _contextLoadController = contextLoadController ?? throw new ArgumentNullException(nameof(contextLoadController));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public PhaseLoadWorkflowResult Execute(
        bool restoreFromState,
        bool forceReloadFromModel,
        string? currentStateFilePath,
        string? currentSelectedProfileKey,
        bool currentShowAllPhases,
        PhaseSearchScope currentSearchScope,
        bool currentShowObjectCountInStatus,
        bool isRestoringShowAllPhases,
        bool isRestoringShowObjectCountInStatus,
        bool isRestoringSearchScope)
    {
        var runtimeSelection = _contextLoadController.ResolveRuntimeSelection(currentSelectedProfileKey);
        var stateFilePath = runtimeSelection.StateFilePath;
        var loadedStatePath = stateFilePath;
        var persistedState = LoadPersistedState(runtimeSelection, loadedStatePath);

        var shouldApplyShowAllPhases = _stateController.TryGetRestoredShowAllPhases(
            restoreFromState,
            isRestoringShowAllPhases,
            persistedState,
            currentShowAllPhases,
            out var restoredShowAllPhases);
        var effectiveShowAllPhases = shouldApplyShowAllPhases ? restoredShowAllPhases : currentShowAllPhases;

        var shouldApplyShowObjectCountInStatus = _stateController.TryGetRestoredShowObjectCountInStatus(
            restoreFromState,
            isRestoringShowObjectCountInStatus,
            persistedState,
            currentShowObjectCountInStatus,
            out var restoredShowObjectCountInStatus);
        var effectiveShowObjectCountInStatus = shouldApplyShowObjectCountInStatus
            ? restoredShowObjectCountInStatus
            : currentShowObjectCountInStatus;

        if (!effectiveShowObjectCountInStatus)
        {
            effectiveShowAllPhases = true;
            shouldApplyShowAllPhases = shouldApplyShowAllPhases || !currentShowAllPhases;
        }

        var shouldApplySearchScope = _stateController.TryGetRestoredUseVisibleViewsForSearch(
            restoreFromState,
            isRestoringSearchScope,
            persistedState,
            PhaseSearchScopeMapper.ToUseVisibleViewsFlag(currentSearchScope),
            out var restoredUseVisibleViewsForSearch);
        var effectiveSearchScope = shouldApplySearchScope
            ? PhaseSearchScopeMapper.FromUseVisibleViewsFlag(restoredUseVisibleViewsForSearch)
            : currentSearchScope;

        var resolvedContext = _contextLoadController.Resolve(
            runtimeSelection,
            forceReloadFromModel,
            effectiveShowAllPhases,
            effectiveSearchScope,
            effectiveShowObjectCountInStatus,
            stateFilePath);
        stateFilePath = resolvedContext.StateFilePath;
        if (!string.Equals(loadedStatePath, stateFilePath, StringComparison.OrdinalIgnoreCase))
        {
            persistedState = LoadPersistedState(runtimeSelection, stateFilePath);
        }

        return new PhaseLoadWorkflowResult(
            resolvedContext.Context ?? new PhaseVisualizerContext(),
            persistedState,
            stateFilePath,
            resolvedContext.HasStateFilePathChanged,
            shouldApplyShowAllPhases,
            effectiveShowAllPhases,
            shouldApplyShowObjectCountInStatus,
            effectiveShowObjectCountInStatus,
            shouldApplySearchScope,
            effectiveSearchScope);
    }

    private PhaseTableState? LoadPersistedState(PhaseRuntimeSelection runtimeSelection, string stateFilePath)
    {
        var persistedState = _stateController.Load(stateFilePath, _log);
        if (persistedState != null)
        {
            return persistedState;
        }

        var selectedProfileKey = runtimeSelection.ProfileSelection.SelectedProfile.Key;
        if (!string.Equals(selectedProfileKey, Configuration.PhaseConfigPaths.DefaultProfileKey, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return _stateController.Load(runtimeSelection.LocalUserStoragePaths.LegacyStateFilePath, _log);
    }
}

internal sealed class PhaseLoadWorkflowResult
{
    public PhaseLoadWorkflowResult(
        PhaseVisualizerContext context,
        PhaseTableState? persistedState,
        string stateFilePath,
        bool hasStateFilePathChanged,
        bool shouldApplyShowAllPhases,
        bool showAllPhases,
        bool shouldApplyShowObjectCountInStatus,
        bool showObjectCountInStatus,
        bool shouldApplySearchScope,
        PhaseSearchScope searchScope)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        PersistedState = persistedState;
        StateFilePath = stateFilePath ?? string.Empty;
        HasStateFilePathChanged = hasStateFilePathChanged;
        ShouldApplyShowAllPhases = shouldApplyShowAllPhases;
        ShowAllPhases = showAllPhases;
        ShouldApplyShowObjectCountInStatus = shouldApplyShowObjectCountInStatus;
        ShowObjectCountInStatus = showObjectCountInStatus;
        ShouldApplySearchScope = shouldApplySearchScope;
        SearchScope = searchScope;
    }

    public PhaseVisualizerContext Context { get; }

    public PhaseTableState? PersistedState { get; }

    public string StateFilePath { get; }

    public bool HasStateFilePathChanged { get; }

    public bool ShouldApplyShowAllPhases { get; }

    public bool ShowAllPhases { get; }

    public bool ShouldApplyShowObjectCountInStatus { get; }

    public bool ShowObjectCountInStatus { get; }

    public bool ShouldApplySearchScope { get; }

    public PhaseSearchScope SearchScope { get; }
}
