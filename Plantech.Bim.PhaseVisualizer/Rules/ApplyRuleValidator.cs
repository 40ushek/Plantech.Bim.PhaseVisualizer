using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Rules;

internal sealed class ApplyRuleValidator
{
    public bool TryNormalize(
        PhaseApplyRuleConfig? rawRule,
        string columnKey,
        PhaseValueType valueType,
        out PhaseApplyRuleConfig? normalizedRule,
        out List<string> diagnostics)
    {
        diagnostics = new List<string>();
        normalizedRule = null;

        if (rawRule == null)
        {
            return true;
        }

        var normalized = new PhaseApplyRuleConfig
        {
            OnTrue = TryNormalizeClause(rawRule.OnTrue, columnKey, "onTrue", valueType, diagnostics),
            OnFalse = TryNormalizeClause(rawRule.OnFalse, columnKey, "onFalse", valueType, diagnostics),
            OnValue = TryNormalizeClause(rawRule.OnValue, columnKey, "onValue", valueType, diagnostics),
        };

        if (normalized.OnTrue == null && normalized.OnFalse == null && normalized.OnValue == null)
        {
            diagnostics.Add(
                $"PhaseVisualizer config column {columnKey} ignored applyRule because no valid rule clauses remained.");
            return false;
        }

        normalizedRule = normalized;
        return true;
    }

    private static ApplyRuleClauseConfig? TryNormalizeClause(
        ApplyRuleClauseConfig? rawClause,
        string columnKey,
        string branchName,
        PhaseValueType valueType,
        List<string> diagnostics)
    {
        if (rawClause == null)
        {
            return null;
        }

        var normalizedOp = ApplyRuleOperationHelper.Normalize(rawClause.Op);
        if (string.IsNullOrWhiteSpace(normalizedOp))
        {
            diagnostics.Add(
                $"PhaseVisualizer config column {columnKey} ignored applyRule.{branchName}: unsupported op '{rawClause.Op}'.");
            return null;
        }

        if (branchName is "onTrue" or "onFalse")
        {
            if (valueType != PhaseValueType.Boolean)
            {
                diagnostics.Add(
                    $"PhaseVisualizer config column {columnKey} ignored applyRule.{branchName}: branch is valid only for Boolean columns.");
                return null;
            }
        }
        else if (branchName == "onValue")
        {
            if (valueType == PhaseValueType.Boolean)
            {
                diagnostics.Add(
                    $"PhaseVisualizer config column {columnKey} ignored applyRule.onValue: branch is not used for Boolean columns.");
                return null;
            }
        }

        return new ApplyRuleClauseConfig
        {
            Field = rawClause.Field?.Trim() ?? string.Empty,
            Op = normalizedOp,
            Value = rawClause.Value,
        };
    }
}
