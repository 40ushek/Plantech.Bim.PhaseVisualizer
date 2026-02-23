using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal sealed class PhaseSnapshot
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public IReadOnlyList<PhaseObjectRecord> Objects { get; set; } = Array.Empty<PhaseObjectRecord>();
    public IReadOnlyList<PhaseCatalogEntry> AllPhases { get; set; } = Array.Empty<PhaseCatalogEntry>();
    public IReadOnlyDictionary<int, int> PhaseObjectCounts { get; set; } = new Dictionary<int, int>();
}

