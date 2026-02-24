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
        PhaseSearchScope currentSearchScope)
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
        var searchScope = currentSearchScope;
        var requiresReload = false;

        if (preset.ShowAllPhases.HasValue && preset.ShowAllPhases.Value != currentShowAllPhases)
        {
            showAllPhases = preset.ShowAllPhases.Value;
            requiresReload = true;
        }

        if (preset.UseVisibleViewsForSearch.HasValue
            && preset.UseVisibleViewsForSearch.Value != PhaseSearchScopeMapper.ToUseVisibleViewsFlag(currentSearchScope))
        {
            searchScope = PhaseSearchScopeMapper.FromUseVisibleViewsFlag(preset.UseVisibleViewsForSearch.Value);
            requiresReload = true;
        }

        return new PhasePresetApplyResult(showAllPhases, searchScope, requiresReload);
    }
}

internal sealed class PhasePresetApplyResult
{
    public PhasePresetApplyResult(
        bool showAllPhases,
        PhaseSearchScope searchScope,
        bool requiresReload)
    {
        ShowAllPhases = showAllPhases;
        SearchScope = searchScope;
        RequiresReload = requiresReload;
    }

    public bool ShowAllPhases { get; }

    public PhaseSearchScope SearchScope { get; }

    public bool RequiresReload { get; }
}
