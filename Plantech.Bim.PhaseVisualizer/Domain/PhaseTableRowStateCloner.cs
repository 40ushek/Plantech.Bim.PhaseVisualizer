using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal static class PhaseTableRowStateCloner
{
    public static PhaseTableRowState Clone(PhaseTableRowState row)
    {
        if (row == null)
        {
            throw new ArgumentNullException(nameof(row));
        }

        return new PhaseTableRowState
        {
            PhaseNumber = row.PhaseNumber,
            Selected = row.Selected,
            Inputs = row.Inputs == null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?>(row.Inputs, StringComparer.OrdinalIgnoreCase),
        };
    }

    public static List<PhaseTableRowState> CloneOrdered(IEnumerable<PhaseTableRowState>? rows)
    {
        return (rows ?? Array.Empty<PhaseTableRowState>())
            .Where(r => r != null)
            .OrderBy(r => r.PhaseNumber)
            .Select(Clone)
            .ToList();
    }
}
