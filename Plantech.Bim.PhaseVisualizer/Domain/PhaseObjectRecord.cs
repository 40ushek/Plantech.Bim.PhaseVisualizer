using System;
using System.Collections.Generic;
using Tekla.Structures;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal sealed class PhaseObjectRecord
{
    public Identifier ObjectId { get; set; } = new Identifier();
    public int PhaseNumber { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public Dictionary<string, PhaseCellValue> Attributes { get; set; } =
        new(StringComparer.Ordinal);
}

