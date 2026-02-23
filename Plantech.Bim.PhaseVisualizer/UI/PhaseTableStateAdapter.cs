using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal static class PhaseTableStateAdapter
{
    public static List<string> ExtractPresetNames(PhaseTableState? state)
    {
        return state?.Presets?
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }

    public static List<PhaseTableRowState> CaptureRows(
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        string selectedColumnKey,
        string phaseNumberColumnKey)
    {
        var editableColumns = columns
            .Where(c => c.IsEditable && table.Columns.Contains(c.Key))
            .ToDictionary(c => c.Key, c => c, StringComparer.OrdinalIgnoreCase);

        var rowsByPhase = new Dictionary<int, PhaseTableRowState>();
        for (var index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            if (row[phaseNumberColumnKey] is not int phaseNumber)
            {
                continue;
            }

            var rowState = new PhaseTableRowState
            {
                PhaseNumber = phaseNumber,
                Selected = row[selectedColumnKey] is bool selectedFlag && selectedFlag,
            };

            foreach (var editableColumn in editableColumns.Values)
            {
                rowState.Inputs[editableColumn.Key] = ToPersistedValue(row[editableColumn.Key]);
            }

            rowsByPhase[phaseNumber] = rowState;
        }

        return rowsByPhase.Values
            .OrderBy(r => r.PhaseNumber)
            .ToList();
    }

    public static void ApplyRows(
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        PhaseTableState? state,
        string selectedColumnKey,
        string phaseNumberColumnKey)
    {
        if (state?.Rows == null || state.Rows.Count == 0 || table.Rows.Count == 0)
        {
            return;
        }

        var rowsByPhase = state.Rows
            .Where(r => r != null)
            .GroupBy(r => r.PhaseNumber)
            .Select(g => g.First())
            .ToDictionary(r => r.PhaseNumber, r => r);
        if (rowsByPhase.Count == 0)
        {
            return;
        }

        var editableByKey = columns
            .Where(c => c.IsEditable)
            .ToDictionary(c => c.Key, c => c, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            if (row[phaseNumberColumnKey] is not int phaseNumber
                || !rowsByPhase.TryGetValue(phaseNumber, out var rowState))
            {
                continue;
            }

            row[selectedColumnKey] = rowState.Selected;
            if (rowState.Inputs == null || rowState.Inputs.Count == 0)
            {
                continue;
            }

            foreach (var input in rowState.Inputs)
            {
                if (!editableByKey.TryGetValue(input.Key, out var column)
                    || !table.Columns.Contains(input.Key))
                {
                    continue;
                }

                row[input.Key] = FromPersistedValue(input.Value, column.Type);
            }
        }
    }

    private static string? ToPersistedValue(object raw)
    {
        if (raw == null || raw == DBNull.Value)
        {
            return null;
        }

        if (raw is bool booleanValue)
        {
            return booleanValue ? "true" : "false";
        }

        return Convert.ToString(raw, CultureInfo.InvariantCulture);
    }

    private static object FromPersistedValue(string? value, PhaseValueType valueType)
    {
        switch (valueType)
        {
            case PhaseValueType.Boolean:
                return bool.TryParse(value, out var boolValue) && boolValue;
            case PhaseValueType.Integer:
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue)
                    ? (object)integerValue
                    : DBNull.Value;
            case PhaseValueType.Number:
                return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var numberValue)
                    ? numberValue
                    : DBNull.Value;
            default:
                return value ?? string.Empty;
        }
    }
}
