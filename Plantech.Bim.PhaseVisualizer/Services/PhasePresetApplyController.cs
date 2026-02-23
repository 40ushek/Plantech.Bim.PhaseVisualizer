using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhasePresetApplyController
{
    public PhasePresetApplyResult Apply(
        PhaseTablePresetState preset,
        IDictionary<int, PhaseTableRowState> rowCacheByPhase,
        bool currentShowAllPhases,
        bool currentUseVisibleViewsForSearch)
    {
        if (preset == null)
        {
            throw new ArgumentNullException(nameof(preset));
        }

        if (rowCacheByPhase == null)
        {
            throw new ArgumentNullException(nameof(rowCacheByPhase));
        }

        rowCacheByPhase.Clear();
        if (preset.Rows != null)
        {
            foreach (var row in preset.Rows.Where(r => r != null))
            {
                rowCacheByPhase[row.PhaseNumber] = PhaseTableRowStateCloner.Clone(row);
            }
        }

        var showAllPhases = currentShowAllPhases;
        var useVisibleViewsForSearch = currentUseVisibleViewsForSearch;
        var requiresReload = false;

        if (preset.ShowAllPhases.HasValue && preset.ShowAllPhases.Value != currentShowAllPhases)
        {
            showAllPhases = preset.ShowAllPhases.Value;
            requiresReload = true;
        }

        if (preset.UseVisibleViewsForSearch.HasValue
            && preset.UseVisibleViewsForSearch.Value != currentUseVisibleViewsForSearch)
        {
            useVisibleViewsForSearch = preset.UseVisibleViewsForSearch.Value;
            requiresReload = true;
        }

        return new PhasePresetApplyResult(showAllPhases, useVisibleViewsForSearch, requiresReload);
    }
}

internal sealed class PhasePresetApplyResult
{
    public PhasePresetApplyResult(
        bool showAllPhases,
        bool useVisibleViewsForSearch,
        bool requiresReload)
    {
        ShowAllPhases = showAllPhases;
        UseVisibleViewsForSearch = useVisibleViewsForSearch;
        RequiresReload = requiresReload;
    }

    public bool ShowAllPhases { get; }

    public bool UseVisibleViewsForSearch { get; }

    public bool RequiresReload { get; }
}
