using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal sealed class PhaseTableState
{
    public int Version { get; set; } = 2;
    public bool? ShowAllPhases { get; set; }
    public bool? UseVisibleViewsForSearch { get; set; }
    public List<PhaseTableRowState> Rows { get; set; } = new();
    public List<PhaseTablePresetState> Presets { get; set; } = new();
}

internal sealed class PhaseTableRowState
{
    public int PhaseNumber { get; set; }
    public bool Selected { get; set; }
    public Dictionary<string, string?> Inputs { get; set; } = new();
}

internal sealed class PhaseTablePresetState
{
    public string Name { get; set; } = string.Empty;
    public bool? ShowAllPhases { get; set; }
    public bool? UseVisibleViewsForSearch { get; set; }
    public List<PhaseTableRowState> Rows { get; set; } = new();
}

