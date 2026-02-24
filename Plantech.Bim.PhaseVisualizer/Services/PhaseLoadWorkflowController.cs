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
        bool currentShowAllPhases,
        PhaseSearchScope currentSearchScope,
        bool isRestoringShowAllPhases,
        bool isRestoringSearchScope)
    {
        var stateFilePath = currentStateFilePath ?? string.Empty;
        if (restoreFromState && string.IsNullOrWhiteSpace(stateFilePath))
        {
            stateFilePath = _contextLoadController.ResolveStateFilePath();
        }

        var loadedStatePath = stateFilePath;
        var persistedState = _stateController.Load(loadedStatePath, _log);

        var shouldApplyShowAllPhases = _stateController.TryGetRestoredShowAllPhases(
            restoreFromState,
            isRestoringShowAllPhases,
            persistedState,
            currentShowAllPhases,
            out var restoredShowAllPhases);
        var effectiveShowAllPhases = shouldApplyShowAllPhases ? restoredShowAllPhases : currentShowAllPhases;

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
            forceReloadFromModel,
            effectiveSearchScope,
            stateFilePath);
        stateFilePath = resolvedContext.StateFilePath;
        if (!string.Equals(loadedStatePath, stateFilePath, StringComparison.OrdinalIgnoreCase))
        {
            persistedState = _stateController.Load(stateFilePath, _log);
        }

        return new PhaseLoadWorkflowResult(
            resolvedContext.Context ?? new PhaseVisualizerContext(),
            persistedState,
            stateFilePath,
            resolvedContext.HasStateFilePathChanged,
            shouldApplyShowAllPhases,
            effectiveShowAllPhases,
            shouldApplySearchScope,
            effectiveSearchScope);
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
        bool shouldApplySearchScope,
        PhaseSearchScope searchScope)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        PersistedState = persistedState;
        StateFilePath = stateFilePath ?? string.Empty;
        HasStateFilePathChanged = hasStateFilePathChanged;
        ShouldApplyShowAllPhases = shouldApplyShowAllPhases;
        ShowAllPhases = showAllPhases;
        ShouldApplySearchScope = shouldApplySearchScope;
        SearchScope = searchScope;
    }

    public PhaseVisualizerContext Context { get; }

    public PhaseTableState? PersistedState { get; }

    public string StateFilePath { get; }

    public bool HasStateFilePathChanged { get; }

    public bool ShouldApplyShowAllPhases { get; }

    public bool ShowAllPhases { get; }

    public bool ShouldApplySearchScope { get; }

    public PhaseSearchScope SearchScope { get; }
}
