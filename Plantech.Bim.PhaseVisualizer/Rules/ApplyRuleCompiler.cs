using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Rules;

internal sealed class ApplyRuleCompiler
{
    public bool TryCompile(
        PhaseAttributeFilter filter,
        out ApplyRuleClauseRuntime? runtimeClause,
        out string? diagnostic)
    {
        runtimeClause = null;
        diagnostic = null;

        if (filter?.ApplyRule == null)
        {
            return false;
        }

        var branch = SelectBranch(filter);
        if (branch == null)
        {
            return false;
        }

        if (!ApplyRuleOperationHelper.TryParse(branch.Op, out var operation))
        {
            diagnostic = $"unsupported applyRule op '{branch.Op}' for '{filter.TargetAttribute}'.";
            return false;
        }

        var field = string.IsNullOrWhiteSpace(branch.Field)
            ? filter.TargetAttribute?.Trim() ?? string.Empty
            : branch.Field.Trim();
        if (string.IsNullOrWhiteSpace(field))
        {
            diagnostic = $"applyRule has no field and targetAttribute is empty for '{filter.TargetAttribute}'.";
            return false;
        }

        var usesInputValue = branch.Value == null;
        var literalValues = usesInputValue
            ? Array.Empty<string>()
            : ApplyRuleOperationHelper.ToLiteralValues(branch.Value);
        if (!usesInputValue && literalValues.Count == 0)
        {
            diagnostic = $"applyRule for '{field}' has empty literal value.";
            return false;
        }

        runtimeClause = new ApplyRuleClauseRuntime
        {
            Field = field,
            Operation = operation,
            UsesInputValue = usesInputValue,
            LiteralValues = literalValues,
        };
        return true;
    }

    private static ApplyRuleClauseConfig? SelectBranch(PhaseAttributeFilter filter)
    {
        var rule = filter.ApplyRule;
        if (rule == null)
        {
            return null;
        }

        if (filter.ValueType == PhaseValueType.Boolean)
        {
            return IsTruthy(filter.Value)
                ? rule.OnTrue
                : rule.OnFalse;
        }

        if (string.IsNullOrWhiteSpace(filter.Value))
        {
            return null;
        }

        return rule.OnValue;
    }

    private static bool IsTruthy(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase);
    }
}
