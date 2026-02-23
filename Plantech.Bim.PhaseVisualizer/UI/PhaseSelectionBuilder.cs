using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseSelectionBuildOptions
{
    public string SelectedColumnKey { get; set; } = string.Empty;
    public string PhaseNumberColumnKey { get; set; } = string.Empty;
}

internal static class PhaseSelectionBuilder
{
    public static List<PhaseSelectionCriteria> Collect(
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        string selectedColumnKey = "__selected",
        string phaseNumberColumnKey = "__phase_number")
    {
        return Collect(
            table,
            columns,
            new PhaseSelectionBuildOptions
            {
                SelectedColumnKey = selectedColumnKey,
                PhaseNumberColumnKey = phaseNumberColumnKey,
            });
    }

    public static List<PhaseSelectionCriteria> Collect(
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        PhaseSelectionBuildOptions options)
    {
        var result = new List<PhaseSelectionCriteria>();
        var seen = new HashSet<int>();

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var selected = row[options.SelectedColumnKey] is bool flag && flag;
            if (!selected)
            {
                continue;
            }

            if (row[options.PhaseNumberColumnKey] is not int phaseNumber)
            {
                continue;
            }

            if (!seen.Add(phaseNumber))
            {
                continue;
            }

            var attributeFilters = CollectAttributeFilters(table, columns, rowIndex);
            result.Add(new PhaseSelectionCriteria
            {
                PhaseNumber = phaseNumber,
                AttributeFilters = attributeFilters,
            });
        }

        return result;
    }

    private static List<PhaseAttributeFilter> CollectAttributeFilters(
        DataTable table,
        IReadOnlyList<PhaseColumnPresentation> columns,
        int rowIndex)
    {
        var result = new List<PhaseAttributeFilter>();
        foreach (var column in columns.Where(c => c.IsEditable))
        {
            if (string.IsNullOrWhiteSpace(column.TargetAttribute)
                || !table.Columns.Contains(column.Key))
            {
                continue;
            }

            if (!TryGetEditableFilterValue(table, rowIndex, column, out var value))
            {
                continue;
            }

            result.Add(new PhaseAttributeFilter
            {
                TargetObjectType = column.TargetObjectType,
                TargetAttribute = column.TargetAttribute,
                BooleanMode = column.BooleanMode,
                ApplyRule = column.ApplyRule,
                ValueType = column.Type,
                Value = value,
            });
        }

        return result;
    }

    private static bool TryGetEditableFilterValue(
        DataTable table,
        int rowIndex,
        PhaseColumnPresentation column,
        out string value)
    {
        value = string.Empty;
        var raw = table.Rows[rowIndex][column.Key];
        if (raw == null || raw == DBNull.Value)
        {
            return false;
        }

        switch (column.Type)
        {
            case PhaseValueType.Boolean:
            {
                if (raw is bool booleanValue)
                {
                    if (booleanValue)
                    {
                        value = "true";
                        return true;
                    }

                    if (string.Equals(
                        column.BooleanMode?.Trim(),
                        "positiveNumber",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        value = "false";
                        return true;
                    }

                    if (column.ApplyRule?.OnFalse != null)
                    {
                        value = "false";
                        return true;
                    }

                    return false;
                }

                if (raw is string booleanText)
                {
                    var normalized = booleanText.Trim();
                    if (normalized.Length == 0)
                    {
                        return false;
                    }

                    if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase))
                    {
                        value = "true";
                        return true;
                    }

                    if (string.Equals(
                            column.BooleanMode?.Trim(),
                            "positiveNumber",
                            StringComparison.OrdinalIgnoreCase)
                        && (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase)))
                    {
                        value = "false";
                        return true;
                    }

                    if (column.ApplyRule?.OnFalse != null
                        && string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        value = "false";
                        return true;
                    }

                    return false;
                }

                return false;
            }
            case PhaseValueType.Integer:
            {
                if (raw is int integerValue)
                {
                    value = integerValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                return false;
            }
            case PhaseValueType.Number:
            {
                if (raw is double numberValue)
                {
                    value = numberValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                return false;
            }
            default:
            {
                var text = (raw as string ?? string.Empty).Trim();
                if (text.Length == 0)
                {
                    return false;
                }

                value = text;
                return true;
            }
        }
    }
}
