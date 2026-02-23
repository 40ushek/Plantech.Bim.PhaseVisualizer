using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Rules;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal sealed class PhaseColumnConfig
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public PhaseValueType Type { get; set; } = PhaseValueType.String;
    public bool Editable { get; set; }
    public PhaseColumnObjectType? ObjectType { get; set; }
    public string Attribute { get; set; } = string.Empty;
    public PhaseColumnObjectType? TargetObjectType { get; set; }
    public string TargetAttribute { get; set; } = string.Empty;
    public string BooleanMode { get; set; } = string.Empty;
    public PhaseApplyRuleConfig? ApplyRule { get; set; }
    public PhaseAggregateType Aggregate { get; set; } = PhaseAggregateType.First;
    public bool VisibleByDefault { get; set; } = true;
    public int Order { get; set; }
    public List<string> FilterOps { get; set; } = new();
    public override string ToString()
    {
        return $"Key = {Key}; Attribute = {Attribute}";
    }

}

