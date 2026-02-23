using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Rules;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseColumnPresentation
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public PhaseColumnObjectType? TargetObjectType { get; set; }
    public string TargetAttribute { get; set; } = string.Empty;
    public string BooleanMode { get; set; } = string.Empty;
    public PhaseApplyRuleConfig? ApplyRule { get; set; }
    public PhaseValueType Type { get; set; } = PhaseValueType.String;
    public bool IsEditable { get; set; }
}

