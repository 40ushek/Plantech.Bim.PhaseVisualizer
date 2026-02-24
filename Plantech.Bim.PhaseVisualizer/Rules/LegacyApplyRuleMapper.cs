using Plantech.Bim.PhaseVisualizer.Domain;
using System;

namespace Plantech.Bim.PhaseVisualizer.Rules;

internal static class LegacyApplyRuleMapper
{
    private const string BooleanModePositiveNumber = "positivenumber";
    private const string ExcludeExistingAttribute = "exclude_existing";
    private const string ExcludeGratingsAttribute = "exclude_gratings";

    public static bool TryMap(PhaseAttributeFilter filter, out ApplyRuleClauseRuntime? runtimeClause)
    {
        runtimeClause = null;
        if (filter == null || string.IsNullOrWhiteSpace(filter.TargetAttribute))
        {
            return false;
        }

        if (TryMapBooleanMode(filter, out runtimeClause))
        {
            return true;
        }

        if (TryMapExcludeExisting(filter, out runtimeClause))
        {
            return true;
        }

        if (TryMapExcludeGratings(filter, out runtimeClause))
        {
            return true;
        }

        return false;
    }

    private static bool TryMapBooleanMode(PhaseAttributeFilter filter, out ApplyRuleClauseRuntime? runtimeClause)
    {
        runtimeClause = null;
        if (filter.ValueType != PhaseValueType.Boolean
            || !string.Equals(filter.BooleanMode?.Trim(), BooleanModePositiveNumber, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        runtimeClause = new ApplyRuleClauseRuntime
        {
            Field = filter.TargetAttribute.Trim(),
            Operation = IsTruthy(filter.Value) ? ApplyRuleOperation.Gt : ApplyRuleOperation.Eq,
            UsesInputValue = false,
            LiteralValues = new[] { "0" },
        };

        return true;
    }

    private static bool TryMapExcludeExisting(PhaseAttributeFilter filter, out ApplyRuleClauseRuntime? runtimeClause)
    {
        runtimeClause = null;
        if (!string.Equals(filter.TargetAttribute.Trim(), ExcludeExistingAttribute, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsTruthy(filter.Value))
        {
            return true;
        }

        runtimeClause = new ApplyRuleClauseRuntime
        {
            Field = "PT_INFO_BESTAND",
            Operation = ApplyRuleOperation.Neq,
            UsesInputValue = false,
            LiteralValues = new[] { "1" },
        };

        return true;
    }

    private static bool TryMapExcludeGratings(PhaseAttributeFilter filter, out ApplyRuleClauseRuntime? runtimeClause)
    {
        runtimeClause = null;
        if (!string.Equals(filter.TargetAttribute.Trim(), ExcludeGratingsAttribute, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsTruthy(filter.Value))
        {
            return true;
        }

        runtimeClause = new ApplyRuleClauseRuntime
        {
            Field = "PROFILE",
            Operation = ApplyRuleOperation.NotStartsWith,
            UsesInputValue = false,
            LiteralValues = new[] { "GIRO" },
            UseProfileScope = true,
            ProfileScopeObjectType = filter.TargetObjectType,
        };

        return true;
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
