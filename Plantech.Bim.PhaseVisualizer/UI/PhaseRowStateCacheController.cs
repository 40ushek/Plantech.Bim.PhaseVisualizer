using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseRowStateCacheController
{
    public void EnsureCache(
        IDictionary<int, PhaseTableRowState> rowCacheByPhase,
        IReadOnlyList<PhaseRow> contextRows,
        IReadOnlyList<PhaseColumnPresentation> columns,
        IReadOnlyList<PhaseTableRowState>? persistedRows)
    {
        if (rowCacheByPhase == null)
        {
            throw new ArgumentNullException(nameof(rowCacheByPhase));
        }

        var rows = contextRows ?? Array.Empty<PhaseRow>();
        var editableColumnKeys = (columns ?? Array.Empty<PhaseColumnPresentation>())
            .Where(c => c != null && c.IsEditable)
            .Select(c => c.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rowCacheByPhase.Count == 0 && persistedRows != null)
        {
            foreach (var row in persistedRows)
            {
                if (row == null)
                {
                    continue;
                }

                rowCacheByPhase[row.PhaseNumber] = PhaseTableRowStateCloner.Clone(row);
            }
        }

        var phaseNumbers = new HashSet<int>(rows.Select(r => r.PhaseNumber));
        var toRemove = rowCacheByPhase.Keys
            .Where(k => !phaseNumbers.Contains(k))
            .ToList();
        foreach (var phaseNumber in toRemove)
        {
            rowCacheByPhase.Remove(phaseNumber);
        }

        foreach (var row in rows)
        {
            if (!rowCacheByPhase.TryGetValue(row.PhaseNumber, out var rowState))
            {
                rowState = new PhaseTableRowState
                {
                    PhaseNumber = row.PhaseNumber,
                    Selected = false,
                    Inputs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                };
                rowCacheByPhase[row.PhaseNumber] = rowState;
            }

            rowState.Inputs ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var columnKey in editableColumnKeys)
            {
                if (!rowState.Inputs.ContainsKey(columnKey))
                {
                    rowState.Inputs[columnKey] = null;
                }
            }
        }
    }

    public void CaptureVisibleRowsToCache(
        IDictionary<int, PhaseTableRowState> rowCacheByPhase,
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        string selectedColumnKey,
        string phaseNumberColumnKey)
    {
        if (rowCacheByPhase == null)
        {
            throw new ArgumentNullException(nameof(rowCacheByPhase));
        }

        var visibleRows = PhaseTableStateAdapter.CaptureRows(
            table,
            columns,
            selectedColumnKey,
            phaseNumberColumnKey);
        foreach (var row in visibleRows)
        {
            if (row == null)
            {
                continue;
            }

            rowCacheByPhase[row.PhaseNumber] = PhaseTableRowStateCloner.Clone(row);
        }
    }

    public IReadOnlyList<PhaseTableRowState> CloneRowsOrdered(
        IEnumerable<PhaseTableRowState>? rows)
    {
        return PhaseTableRowStateCloner.CloneOrdered(rows);
    }
}
