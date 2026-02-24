using System;
using System.Collections.Generic;
using Tekla.Structures;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal sealed class PhaseRow
{
    public PhaseRow(int phaseNumber, string? phaseName, int objectCount)
    {
        PhaseNumber = phaseNumber;
        PhaseName = phaseName ?? string.Empty;
        ObjectCount = objectCount;
        Cells = new Dictionary<string, PhaseCellValue>(StringComparer.OrdinalIgnoreCase);
        ObjectIds = Array.Empty<Identifier>();
    }

    public int PhaseNumber { get; }
    public string PhaseName { get; private set; }
    public int ObjectCount { get; private set; }
    public Dictionary<string, PhaseCellValue> Cells { get; }
    public IReadOnlyList<Identifier> ObjectIds { get; private set; }

    public void UpdatePhaseName(string? phaseName)
    {
        if (string.IsNullOrWhiteSpace(phaseName))
        {
            return;
        }

        PhaseName = phaseName!;
    }

    public void UpdateObjectCount(int objectCount)
    {
        ObjectCount = objectCount;
    }

    public void UpdateObjectIds(IReadOnlyList<Identifier>? objectIds)
    {
        ObjectIds = objectIds ?? Array.Empty<Identifier>();
    }
}

