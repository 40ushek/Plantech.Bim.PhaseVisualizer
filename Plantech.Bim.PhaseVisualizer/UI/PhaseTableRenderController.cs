using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Data;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseTableRenderController
{
    public void BuildTable(
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        IReadOnlyList<PhaseRow> rows,
        string selectedColumnKey,
        string phaseNumberColumnKey)
    {
        if (table == null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        var effectiveColumns = columns ?? Array.Empty<PhaseColumnPresentation>();
        var effectiveRows = rows ?? Array.Empty<PhaseRow>();

        table.BeginLoadData();
        try
        {
            table.Clear();
            table.Columns.Clear();

            table.Columns.Add(selectedColumnKey, typeof(bool));
            table.Columns.Add(phaseNumberColumnKey, typeof(int));
            foreach (var column in effectiveColumns)
            {
                table.Columns.Add(column.Key, ResolveStorageType(column.Type));
            }

            foreach (var row in effectiveRows)
            {
                var dataRow = table.NewRow();
                dataRow[selectedColumnKey] = false;
                dataRow[phaseNumberColumnKey] = row.PhaseNumber;
                foreach (var column in effectiveColumns)
                {
                    dataRow[column.Key] = column.IsEditable
                        ? ResolveEditableDefault(column.Type)
                        : ResolveCellObject(row, column);
                }

                table.Rows.Add(dataRow);
            }
        }
        finally
        {
            table.EndLoadData();
        }
    }

    private static object ResolveCellObject(PhaseRow row, PhaseColumnPresentation column)
    {
        if (row.Cells.TryGetValue(column.Key, out var value))
        {
            return ToStorageValue(value, column.Type);
        }

        var fallback = column.Key switch
        {
            "phase_number" => PhaseCellValue.FromInteger(row.PhaseNumber),
            "phase_name" => PhaseCellValue.FromString(row.PhaseName),
            "object_count" => PhaseCellValue.FromInteger(row.ObjectCount),
            _ => PhaseCellValue.FromString(string.Empty),
        };

        return ToStorageValue(fallback, column.Type);
    }

    private static Type ResolveStorageType(PhaseValueType valueType)
    {
        return valueType switch
        {
            PhaseValueType.Boolean => typeof(bool),
            PhaseValueType.Integer => typeof(int),
            PhaseValueType.Number => typeof(double),
            _ => typeof(string),
        };
    }

    private static object ResolveEditableDefault(PhaseValueType valueType)
    {
        return valueType switch
        {
            PhaseValueType.Boolean => false,
            PhaseValueType.Integer => DBNull.Value,
            PhaseValueType.Number => DBNull.Value,
            _ => string.Empty,
        };
    }

    private static object ToStorageValue(PhaseCellValue value, PhaseValueType valueType)
    {
        var converted = value.ConvertTo(valueType);
        switch (valueType)
        {
            case PhaseValueType.Boolean:
                return converted.Bool.HasValue
                    ? converted.Bool.Value
                    : DBNull.Value;
            case PhaseValueType.Integer:
                return converted.Number.HasValue
                    ? (object)(int)converted.Number.Value
                    : DBNull.Value;
            case PhaseValueType.Number:
                return converted.Number.HasValue
                    ? converted.Number.Value
                    : DBNull.Value;
            default:
                return converted.Text ?? string.Empty;
        }
    }
}
