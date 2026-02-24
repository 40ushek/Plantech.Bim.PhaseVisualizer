using Plantech.Bim.PhaseVisualizer.Common;
using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Orchestration;
using Plantech.Bim.PhaseVisualizer.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseVisualizerViewModel : INotifyPropertyChanged
{
    private const string SelectedColumnKey = "__selected";
    private const string PhaseNumberColumnKey = "__phase_number";

    private readonly PhaseVisualizerController _controller;
    private readonly SynchronizationContext? _teklaContext;
    private readonly ILogger _log;
    private readonly PhaseTableStateController _stateController;
    private readonly PhaseLoadWorkflowController _loadWorkflowController;
    private readonly PhaseLogFileController _logFileController;
    private readonly PhasePresetLoadController _presetLoadController;
    private readonly PhasePresetMutationController _presetMutationController;
    private readonly PhasePresetOperationContextController _presetOperationContextController;
    private readonly PhaseStatePersistenceController _statePersistenceController;
    private readonly PhaseRowStateCacheController _rowStateCacheController;
    private readonly PhaseTableSortController _tableSortController;
    private readonly PhaseLoadedContextController _loadedContextController;
    private readonly PhaseTableStateUiController _tableStateUiController;
    private readonly DataTable _table = new("PhaseVisualizer");
    private List<PhaseColumnPresentation> _columns = new();
    private readonly Dictionary<int, PhaseTableRowState> _cachedRowStatesByPhase = new();
    private string _stateFilePath = string.Empty;
    private bool _isRestoringShowAllPhases;
    private bool _isRestoringUseVisibleViewsForSearch;

    private string _statusText = "Ready";
    private bool _showAllPhases;
    private bool _useVisibleViewsForSearch;
    private string _presetName = string.Empty;
    private List<string> _presetNames = new();

    public PhaseVisualizerViewModel(
        PhaseVisualizerController controller,
        SynchronizationContext? teklaContext,
        ILogger? log = null)
        : this(controller, teklaContext, null, log)
    {
    }

    public PhaseVisualizerViewModel(
        PhaseVisualizerController controller,
        SynchronizationContext? teklaContext,
        PhaseTableStateStore? stateStore,
        ILogger? log = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _teklaContext = teklaContext;
        var effectiveStateStore = stateStore ?? new PhaseTableStateStore();
        _stateController = new PhaseTableStateController(effectiveStateStore);
        _log = log ?? Serilog.Log.Logger.ForContext<PhaseVisualizerViewModel>();
        var contextLoadController = new PhaseContextLoadController(_controller, _teklaContext, _log);
        _loadWorkflowController = new PhaseLoadWorkflowController(_stateController, contextLoadController, _log);
        _logFileController = new PhaseLogFileController(_controller, _teklaContext, _log);
        var presetController = new PhasePresetController();
        _presetLoadController = new PhasePresetLoadController(presetController, new PhasePresetApplyController());
        _presetMutationController = new PhasePresetMutationController(_stateController, presetController);
        _presetOperationContextController = new PhasePresetOperationContextController();
        _statePersistenceController = new PhaseStatePersistenceController(_stateController, new PhaseStateSnapshotController());
        _rowStateCacheController = new PhaseRowStateCacheController();
        var columnsController = new PhaseColumnsController();
        var tableRenderController = new PhaseTableRenderController();
        _tableSortController = new PhaseTableSortController();
        _tableStateUiController = new PhaseTableStateUiController();
        _loadedContextController = new PhaseLoadedContextController(
            columnsController,
            _rowStateCacheController,
            tableRenderController);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<PhaseColumnPresentation> Columns => _columns;

    public DataView RowsView => _table.DefaultView;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool ShowAllPhases
    {
        get => _showAllPhases;
        set
        {
            if (_showAllPhases == value)
            {
                return;
            }

            _showAllPhases = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> PresetNames => _presetNames;

    public bool UseVisibleViewsForSearch
    {
        get => _useVisibleViewsForSearch;
        set
        {
            if (_useVisibleViewsForSearch == value)
            {
                return;
            }

            _useVisibleViewsForSearch = value;
            OnPropertyChanged();
        }
    }

    public string PresetName
    {
        get => _presetName;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_presetName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _presetName = normalized;
            OnPropertyChanged();
        }
    }

    public void Load()
    {
        Load(restoreShowAllPhasesFromState: true, forceReloadFromModel: false);
    }

    public void Load(bool restoreShowAllPhasesFromState, bool forceReloadFromModel = false)
    {
        var loadResult = _loadWorkflowController.Execute(
            restoreShowAllPhasesFromState,
            forceReloadFromModel,
            _stateFilePath,
            ShowAllPhases,
            UseVisibleViewsForSearch,
            _isRestoringShowAllPhases,
            _isRestoringUseVisibleViewsForSearch);

        ApplyLoadResult(loadResult);
        if (loadResult.HasStateFilePathChanged)
        {
            _cachedRowStatesByPhase.Clear();
        }

        _stateFilePath = loadResult.StateFilePath;
        ApplyLoadedContext(loadResult.Context, loadResult.PersistedState);
    }

    public void Apply()
    {
        CaptureVisibleRowsToCache();
        var selection = PhaseSelectionBuilder.Collect(_table, _columns);
        var applyResult = _controller.ActionExecutor.ShowOnly(selection, _teklaContext);
        SaveState();
        StatusText = PhaseApplyStatusFormatter.Build(
            applyResult,
            selection);
    }

    public bool OpenLogFile()
    {
        var result = _logFileController.Open();
        StatusText = result.StatusText;
        return result.IsSuccess;
    }

    private void ApplyLoadResult(PhaseLoadWorkflowResult loadResult)
    {
        if (loadResult == null)
        {
            return;
        }

        if (loadResult.ShouldApplyShowAllPhases)
        {
            try
            {
                _isRestoringShowAllPhases = true;
                ShowAllPhases = loadResult.ShowAllPhases;
            }
            finally
            {
                _isRestoringShowAllPhases = false;
            }
        }

        if (loadResult.ShouldApplyUseVisibleViewsForSearch)
        {
            try
            {
                _isRestoringUseVisibleViewsForSearch = true;
                UseVisibleViewsForSearch = loadResult.UseVisibleViewsForSearch;
            }
            finally
            {
                _isRestoringUseVisibleViewsForSearch = false;
            }
        }
    }

    private void ApplyLoadedContext(PhaseVisualizerContext context, PhaseTableState? persistedState)
    {
        ApplyPresetNamesState(persistedState);

        var result = _loadedContextController.Apply(
            _table,
            _cachedRowStatesByPhase,
            context,
            persistedState,
            ShowAllPhases,
            UseVisibleViewsForSearch,
            SelectedColumnKey,
            PhaseNumberColumnKey);

        _columns = result.Columns.ToList();
        StatusText = result.StatusText;
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(RowsView));
    }

    public bool SavePreset()
    {
        if (!TryCreatePresetOperationContext(out var context))
        {
            return false;
        }

        CaptureVisibleRowsToCache();
        var presetRows = _rowStateCacheController.CloneRowsOrdered(_cachedRowStatesByPhase.Values);
        var saveResult = _presetMutationController.TrySave(
            context.StateFilePath,
            context.PresetName,
            ShowAllPhases,
            UseVisibleViewsForSearch,
            presetRows,
            _log);
        if (!saveResult.IsSuccess || saveResult.State == null)
        {
            return false;
        }

        CompletePresetMutation(
            saveResult.State,
            saveResult.PresetName,
            $"Preset '{saveResult.PresetName}' saved.");
        return true;
    }

    public bool LoadPreset()
    {
        if (!TryCreatePresetOperationContext(out var context))
        {
            return false;
        }

        var state = _stateController.Load(context.StateFilePath, _log);
        ApplyPresetNamesState(state);
        var loadedPreset = _presetLoadController.TryLoad(
            state,
            context.PresetName,
            _cachedRowStatesByPhase,
            ShowAllPhases,
            UseVisibleViewsForSearch);
        if (!loadedPreset.IsSuccess)
        {
            return false;
        }

        ShowAllPhases = loadedPreset.ShowAllPhases;
        UseVisibleViewsForSearch = loadedPreset.UseVisibleViewsForSearch;

        if (loadedPreset.RequiresReload)
        {
            Load(restoreShowAllPhasesFromState: false, forceReloadFromModel: false);
        }

        _tableStateUiController.ApplyCachedRows(
            _table,
            _columns,
            _cachedRowStatesByPhase,
            SelectedColumnKey,
            PhaseNumberColumnKey);
        OnPropertyChanged(nameof(RowsView));
        PresetName = loadedPreset.PresetName;
        StatusText = $"Preset '{loadedPreset.PresetName}' loaded.";
        return true;
    }

    public bool DeletePreset()
    {
        if (!TryCreatePresetOperationContext(out var context))
        {
            return false;
        }

        var deleteResult = _presetMutationController.TryDelete(
            context.StateFilePath,
            context.PresetName,
            _log);
        if (!deleteResult.IsSuccess || deleteResult.State == null)
        {
            return false;
        }

        CompletePresetMutation(
            deleteResult.State,
            string.Empty,
            $"Preset '{context.PresetName}' deleted.");
        return true;
    }

    public void SaveState()
    {
        if (string.IsNullOrWhiteSpace(_stateFilePath)
            || _table.Columns.Count == 0)
        {
            return;
        }

        CaptureVisibleRowsToCache();
        var state = _statePersistenceController.SaveSnapshot(
            _stateFilePath,
            ShowAllPhases,
            UseVisibleViewsForSearch,
            _cachedRowStatesByPhase.Values.ToList(),
            _log);
        ApplyPresetNamesState(state);
    }

    public bool TrySortRows(string columnKey, ListSortDirection direction)
    {
        return _tableSortController.TrySortRows(_table, columnKey, direction);
    }

    private void ApplyPresetNamesState(PhaseTableState? state)
    {
        var result = _tableStateUiController.BuildPresetNamesState(state, PresetName);
        _presetNames = result.Names.ToList();
        OnPropertyChanged(nameof(PresetNames));
        if (result.ShouldClearCurrent)
        {
            PresetName = string.Empty;
        }
    }

    private void CaptureVisibleRowsToCache()
    {
        _rowStateCacheController.CaptureVisibleRowsToCache(
            _cachedRowStatesByPhase,
            _table,
            _columns,
            SelectedColumnKey,
            PhaseNumberColumnKey);
    }

    private bool TryCreatePresetOperationContext(out PhasePresetOperationContext context)
    {
        return _presetOperationContextController.TryCreate(PresetName, _stateFilePath, out context);
    }

    private void CompletePresetMutation(
        PhaseTableState state,
        string presetName,
        string statusText)
    {
        ApplyPresetNamesState(state);
        PresetName = presetName;
        StatusText = statusText;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

