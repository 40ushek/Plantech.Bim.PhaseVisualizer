using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal sealed class PhaseTableState
{
    public int Version { get; set; } = 2;
    public bool? ShowAllPhases { get; set; }
    public bool? UseVisibleViewsForSearch { get; set; }
    public bool? ShowObjectCountInStatus { get; set; }
    public PhaseTableLayoutState? Layout { get; set; }
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

internal sealed class PhaseTableLayoutState
{
    public List<PhaseTableColumnLayoutState> Columns { get; set; } = new();
    public PhaseTableSortLayoutState? Sort { get; set; }
}

internal sealed class PhaseTableColumnLayoutState
{
    public string Key { get; set; } = string.Empty;
    public int DisplayIndex { get; set; }
    public double Width { get; set; }
    public string WidthUnit { get; set; } = string.Empty;
}

internal sealed class PhaseTableSortLayoutState
{
    public string ColumnKey { get; set; } = string.Empty;
    public bool Descending { get; set; }
}

