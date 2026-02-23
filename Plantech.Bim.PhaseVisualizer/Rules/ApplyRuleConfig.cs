namespace Plantech.Bim.PhaseVisualizer.Rules;

internal sealed class PhaseApplyRuleConfig
{
    public ApplyRuleClauseConfig? OnTrue { get; set; }
    public ApplyRuleClauseConfig? OnFalse { get; set; }
    public ApplyRuleClauseConfig? OnValue { get; set; }
}

internal sealed class ApplyRuleClauseConfig
{
    public string Field { get; set; } = string.Empty;
    public string Op { get; set; } = string.Empty;
    public object? Value { get; set; }
}
