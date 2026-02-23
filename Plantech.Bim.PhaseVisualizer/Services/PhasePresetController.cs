using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhasePresetController
{
    public bool SaveOrUpdate(
        PhaseTableState state,
        string presetName,
        bool showAllPhases,
        bool useVisibleViewsForSearch,
        IReadOnlyCollection<PhaseTableRowState> rows)
    {
        if (state == null || string.IsNullOrWhiteSpace(presetName))
        {
            return false;
        }

        state.Presets ??= new();
        var existing = state.Presets
            .FirstOrDefault(x => string.Equals(x.Name, presetName, StringComparison.OrdinalIgnoreCase));
        var presetRows = PhaseTableRowStateCloner.CloneOrdered(rows);

        if (existing == null)
        {
            state.Presets.Add(new PhaseTablePresetState
            {
                Name = presetName,
                ShowAllPhases = showAllPhases,
                UseVisibleViewsForSearch = useVisibleViewsForSearch,
                Rows = presetRows,
            });
            return true;
        }

        existing.Name = presetName;
        existing.ShowAllPhases = showAllPhases;
        existing.UseVisibleViewsForSearch = useVisibleViewsForSearch;
        existing.Rows = presetRows;
        return true;
    }

    public bool TryGet(
        PhaseTableState? state,
        string presetName,
        out PhaseTablePresetState? preset)
    {
        preset = null;
        if (state?.Presets == null || string.IsNullOrWhiteSpace(presetName))
        {
            return false;
        }

        var existing = state.Presets
            .FirstOrDefault(x => string.Equals(x.Name, presetName, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            return false;
        }

        preset = new PhaseTablePresetState
        {
            Name = existing.Name,
            ShowAllPhases = existing.ShowAllPhases,
            UseVisibleViewsForSearch = existing.UseVisibleViewsForSearch,
            Rows = PhaseTableRowStateCloner.CloneOrdered(existing.Rows),
        };
        return true;
    }

    public bool Delete(PhaseTableState? state, string presetName)
    {
        if (state?.Presets == null || state.Presets.Count == 0 || string.IsNullOrWhiteSpace(presetName))
        {
            return false;
        }

        return state.Presets.RemoveAll(
            x => string.Equals(x.Name, presetName, StringComparison.OrdinalIgnoreCase)) > 0;
    }
}
