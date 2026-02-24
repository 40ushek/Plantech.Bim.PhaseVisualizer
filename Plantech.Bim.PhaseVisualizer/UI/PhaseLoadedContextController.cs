using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Orchestration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseLoadedContextController
{
    private readonly PhaseColumnsController _columnsController;
    private readonly PhaseRowStateCacheController _rowStateCacheController;
    private readonly PhaseTableRenderController _tableRenderController;

    public PhaseLoadedContextController(
        PhaseColumnsController columnsController,
        PhaseRowStateCacheController rowStateCacheController,
        PhaseTableRenderController tableRenderController)
    {
        _columnsController = columnsController ?? throw new ArgumentNullException(nameof(columnsController));
        _rowStateCacheController = rowStateCacheController ?? throw new ArgumentNullException(nameof(rowStateCacheController));
        _tableRenderController = tableRenderController ?? throw new ArgumentNullException(nameof(tableRenderController));
    }

    public PhaseLoadedContextResult Apply(
        DataTable table,
        IDictionary<int, PhaseTableRowState> rowCacheByPhase,
        PhaseVisualizerContext context,
        PhaseTableState? persistedState,
        bool showAllPhases,
        PhaseSearchScope searchScope,
        string selectedColumnKey,
        string phaseNumberColumnKey)
    {
        if (table == null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        if (rowCacheByPhase == null)
        {
            throw new ArgumentNullException(nameof(rowCacheByPhase));
        }

        var effectiveContext = context ?? new PhaseVisualizerContext();
        var columns = _columnsController.Build(effectiveContext.Config.Columns);

        _rowStateCacheController.EnsureCache(
            rowCacheByPhase,
            effectiveContext.Rows,
            columns,
            persistedState?.Rows);

        var rowsForMode = FilterRowsForMode(effectiveContext.Rows, showAllPhases);
        _tableRenderController.BuildTable(
            table,
            columns,
            rowsForMode,
            selectedColumnKey,
            phaseNumberColumnKey);

        PhaseTableStateAdapter.ApplyRows(
            table,
            columns,
            new PhaseTableState
            {
                Rows = rowCacheByPhase.Values
                    .OrderBy(r => r.PhaseNumber)
                    .ToList(),
            },
            selectedColumnKey,
            phaseNumberColumnKey);

        var statusText = PhaseLoadStatusFormatter.Build(
            table.Rows.Count,
            effectiveContext.SnapshotMeta.ObjectCount,
            showAllPhases,
            searchScope);

        return new PhaseLoadedContextResult(columns, statusText);
    }

    private static IReadOnlyList<PhaseRow> FilterRowsForMode(IReadOnlyList<PhaseRow> rows, bool showAllPhases)
    {
        if (showAllPhases)
        {
            return rows ?? Array.Empty<PhaseRow>();
        }

        return (rows ?? Array.Empty<PhaseRow>())
            .Where(r => r.ObjectCount > 0)
            .ToList();
    }
}

internal sealed class PhaseLoadedContextResult
{
    public PhaseLoadedContextResult(IReadOnlyList<PhaseColumnPresentation> columns, string statusText)
    {
        Columns = columns ?? Array.Empty<PhaseColumnPresentation>();
        StatusText = statusText ?? string.Empty;
    }

    public IReadOnlyList<PhaseColumnPresentation> Columns { get; }

    public string StatusText { get; }
}
