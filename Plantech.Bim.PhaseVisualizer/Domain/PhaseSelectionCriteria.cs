using System.Collections.Generic;
using Plantech.Bim.PhaseVisualizer.Rules;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal sealed class PhaseAttributeFilter
{
    public PhaseColumnObjectType? TargetObjectType { get; set; }
    public string TargetAttribute { get; set; } = string.Empty;
    public string BooleanMode { get; set; } = string.Empty;
    public PhaseApplyRuleConfig? ApplyRule { get; set; }
    public PhaseValueType ValueType { get; set; } = PhaseValueType.String;
    public string Value { get; set; } = string.Empty;
}

internal sealed class PhaseSelectionCriteria
{
    public int PhaseNumber { get; set; }
    public List<PhaseAttributeFilter> AttributeFilters { get; set; } = new();
}

