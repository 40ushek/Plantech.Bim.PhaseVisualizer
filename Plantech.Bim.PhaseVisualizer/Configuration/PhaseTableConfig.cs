using Plantech.Bim.PhaseVisualizer.Domain;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal sealed class PhaseTableConfig
{
    public int Version { get; set; } = 2;
    public PhaseObjectScope ObjectScope { get; set; } = PhaseObjectScope.Visible;
    public string PhaseKey { get; set; } = "number";
    public List<PhaseColumnConfig> Columns { get; set; } = new();
    public List<string> Actions { get; set; } = new();
}

