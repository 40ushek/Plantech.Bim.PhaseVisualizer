using System;
using System.Collections.Generic;
using Tekla.Structures;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal sealed class PhaseRow
{
    public int PhaseNumber { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public int ObjectCount { get; set; }
    public Dictionary<string, PhaseCellValue> Cells { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<Identifier> ObjectIds { get; set; } = Array.Empty<Identifier>();
}

