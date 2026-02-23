using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseTableSortController
{
    public bool TrySortRows(DataTable table, string columnKey, ListSortDirection direction)
    {
        if (table == null
            || string.IsNullOrWhiteSpace(columnKey)
            || !table.Columns.Contains(columnKey))
        {
            return false;
        }

        var rows = table.Rows.Cast<DataRow>().ToList();
        if (rows.Count <= 1)
        {
            return true;
        }

        var ordered = direction == ListSortDirection.Ascending
            ? rows.OrderBy(r => GetSortToken(r[columnKey]), SortTokenComparer.Instance)
                .Select(r => r.ItemArray)
                .ToList()
            : rows.OrderByDescending(r => GetSortToken(r[columnKey]), SortTokenComparer.Instance)
                .Select(r => r.ItemArray)
                .ToList();

        table.BeginLoadData();
        try
        {
            table.Clear();
            foreach (var rowValues in ordered)
            {
                table.Rows.Add(rowValues);
            }
        }
        finally
        {
            table.EndLoadData();
        }

        return true;
    }

    private static SortToken GetSortToken(object raw)
    {
        if (raw is null || raw == DBNull.Value)
        {
            return new SortToken(false, 0.0, string.Empty);
        }

        if (raw is bool b)
        {
            return new SortToken(true, b ? 1d : 0d, b ? "true" : "false");
        }

        if (raw is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            var numericValue = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            return new SortToken(true, numericValue, numericValue.ToString(CultureInfo.InvariantCulture));
        }

        var text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number))
        {
            return new SortToken(true, number, text);
        }

        return new SortToken(false, 0.0, text);
    }

    private readonly struct SortToken
    {
        public SortToken(bool isNumber, double number, string text)
        {
            IsNumber = isNumber;
            Number = number;
            Text = text;
        }

        public bool IsNumber { get; }
        public double Number { get; }
        public string Text { get; }
    }

    private sealed class SortTokenComparer : IComparer<SortToken>
    {
        public static readonly SortTokenComparer Instance = new();

        public int Compare(SortToken x, SortToken y)
        {
            if (x.IsNumber && y.IsNumber)
            {
                return x.Number.CompareTo(y.Number);
            }

            if (x.IsNumber != y.IsNumber)
            {
                return x.IsNumber ? -1 : 1;
            }

            return StringComparer.CurrentCultureIgnoreCase.Compare(x.Text, y.Text);
        }
    }
}
