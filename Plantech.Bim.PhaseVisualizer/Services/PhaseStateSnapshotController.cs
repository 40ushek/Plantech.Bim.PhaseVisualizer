using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseStateSnapshotController
{
    public PhaseTableState Build(
        PhaseTableState? persistedState,
        bool showAllPhases,
        bool useVisibleViewsForSearch,
        bool showObjectCountInStatus,
        IReadOnlyCollection<PhaseTableRowState> rows,
        PhaseTableLayoutState? layout)
    {
        var state = persistedState ?? new PhaseTableState();
        state.ShowAllPhases = showAllPhases;
        state.UseVisibleViewsForSearch = useVisibleViewsForSearch;
        state.ShowObjectCountInStatus = showObjectCountInStatus;
        state.Rows = PhaseTableRowStateCloner.CloneOrdered(rows);
        state.Layout = PhaseTableLayoutStateCloner.Clone(layout);
        state.Presets ??= new();

        return state;
    }
}
