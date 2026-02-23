using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseTableStateUiController
{
    public PhasePresetNamesState BuildPresetNamesState(PhaseTableState? state, string? currentPresetName)
    {
        var names = PhaseTableStateAdapter.ExtractPresetNames(state);
        var shouldClearCurrent = !string.IsNullOrWhiteSpace(currentPresetName)
            && !names.Any(x => string.Equals(x, currentPresetName, StringComparison.OrdinalIgnoreCase));
        return new PhasePresetNamesState(names, shouldClearCurrent);
    }

    public void ApplyRows(
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        PhaseTableState? state,
        string selectedColumnKey,
        string phaseNumberColumnKey)
    {
        PhaseTableStateAdapter.ApplyRows(
            table,
            columns,
            state,
            selectedColumnKey,
            phaseNumberColumnKey);
    }

    public void ApplyCachedRows(
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        IDictionary<int, PhaseTableRowState> rowCacheByPhase,
        string selectedColumnKey,
        string phaseNumberColumnKey)
    {
        var state = new PhaseTableState
        {
            Rows = (rowCacheByPhase ?? new Dictionary<int, PhaseTableRowState>())
                .Values
                .OrderBy(r => r.PhaseNumber)
                .ToList(),
        };

        ApplyRows(
            table,
            columns,
            state,
            selectedColumnKey,
            phaseNumberColumnKey);
    }
}

internal sealed class PhasePresetNamesState
{
    public PhasePresetNamesState(IReadOnlyList<string> names, bool shouldClearCurrent)
    {
        Names = names ?? Array.Empty<string>();
        ShouldClearCurrent = shouldClearCurrent;
    }

    public IReadOnlyList<string> Names { get; }

    public bool ShouldClearCurrent { get; }
}
