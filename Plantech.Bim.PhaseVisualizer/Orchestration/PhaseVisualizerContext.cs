using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Orchestration;

internal sealed class PhaseSnapshotMeta
{
    public DateTime CreatedAtUtc { get; set; }
    public int ObjectCount { get; set; }
    public int RowCount { get; set; }
}

internal sealed class PhaseVisualizerContext
{
    public PhaseTableConfig Config { get; set; } = PhaseTableConfigDefaults.Create();
    public PhaseSnapshotMeta SnapshotMeta { get; set; } = new();
    public IReadOnlyList<PhaseRow> Rows { get; set; } = Array.Empty<PhaseRow>();
    public string StateFilePath { get; set; } = string.Empty;
}

