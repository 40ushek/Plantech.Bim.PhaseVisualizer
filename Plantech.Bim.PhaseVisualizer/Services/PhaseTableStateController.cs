using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseTableStateController
{
    private readonly PhaseTableStateStore _stateStore;

    public PhaseTableStateController(PhaseTableStateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public PhaseTableState? Load(string? stateFilePath, ILogger log)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath))
        {
            return null;
        }

        return _stateStore.Load(stateFilePath, log);
    }

    public PhaseTableState? LoadCompatible(string? stateFilePath, string? configFingerprint, ILogger log)
    {
        var state = Load(stateFilePath, log);
        if (state == null)
        {
            return null;
        }

        return HasCompatibleFingerprint(state, configFingerprint) ? state : null;
    }

    public void Save(string? stateFilePath, PhaseTableState state, string? configFingerprint, ILogger log)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath) || state == null)
        {
            return;
        }

        state.Version = 2;
        state.ConfigFingerprint = configFingerprint ?? string.Empty;
        _stateStore.Save(stateFilePath, state, log);
    }

    public static bool HasCompatibleFingerprint(PhaseTableState? state, string? configFingerprint)
    {
        if (state == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(configFingerprint))
        {
            return true;
        }

        return string.Equals(
            state.ConfigFingerprint ?? string.Empty,
            configFingerprint,
            StringComparison.OrdinalIgnoreCase);
    }

    public bool TryGetRestoredShowAllPhases(
        bool restoreFromState,
        bool isRestoring,
        PhaseTableState? state,
        bool currentValue,
        out bool restoredValue)
    {
        restoredValue = currentValue;
        var persisted = state?.ShowAllPhases;
        if (!restoreFromState
            || isRestoring
            || !persisted.HasValue
            || persisted.Value == currentValue)
        {
            return false;
        }

        restoredValue = persisted.Value;
        return true;
    }

    public bool TryGetRestoredUseVisibleViewsForSearch(
        bool restoreFromState,
        bool isRestoring,
        PhaseTableState? state,
        bool currentValue,
        out bool restoredValue)
    {
        restoredValue = currentValue;
        var persisted = state?.UseVisibleViewsForSearch;
        if (!restoreFromState
            || isRestoring
            || !persisted.HasValue
            || persisted.Value == currentValue)
        {
            return false;
        }

        restoredValue = persisted.Value;
        return true;
    }

    public bool TryGetRestoredShowObjectCountInStatus(
        bool restoreFromState,
        bool isRestoring,
        PhaseTableState? state,
        bool currentValue,
        out bool restoredValue)
    {
        restoredValue = currentValue;
        var persisted = state?.ShowObjectCountInStatus;
        if (!restoreFromState
            || isRestoring
            || !persisted.HasValue
            || persisted.Value == currentValue)
        {
            return false;
        }

        restoredValue = persisted.Value;
        return true;
    }
}
