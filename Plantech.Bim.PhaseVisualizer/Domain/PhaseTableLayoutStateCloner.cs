using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal static class PhaseTableLayoutStateCloner
{
    public static PhaseTableLayoutState? Clone(PhaseTableLayoutState? layout)
    {
        if (layout == null)
        {
            return null;
        }

        var result = new PhaseTableLayoutState
        {
            Columns = (layout.Columns ?? new List<PhaseTableColumnLayoutState>())
                .Where(c => c != null)
                .Select(c => new PhaseTableColumnLayoutState
                {
                    Key = c.Key ?? string.Empty,
                    DisplayIndex = c.DisplayIndex,
                    Width = c.Width,
                })
                .ToList(),
            Sort = layout.Sort == null
                ? null
                : new PhaseTableSortLayoutState
                {
                    ColumnKey = layout.Sort.ColumnKey ?? string.Empty,
                    Descending = layout.Sort.Descending,
                },
        };

        return result;
    }
}
