using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Rules;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal sealed class PhaseTableConfigValidator
{
    private const int SupportedConfigVersion = 2;
    // Legacy macro-era criteria flags. Prefer explicit editable columns with targetObjectType + targetAttribute.
    private const string CriteriaExcludeGratings = "exclude_gratings";
    private const string CriteriaExcludeExisting = "exclude_existing";
    private const string OpEquals = "equals";
    private const string OpNotEquals = "notEquals";
    private const string OpContains = "contains";
    private const string OpNotContains = "notContains";
    private const string OpStartsWith = "startsWith";
    private const string OpNotStartsWith = "notStartsWith";
    private const string OpEndsWith = "endsWith";
    private const string OpNotEndsWith = "notEndsWith";
    private const string OpIn = "in";
    private const string OpRange = "range";
    private const string OpGt = "gt";
    private const string OpGte = "gte";
    private const string OpLt = "lt";
    private const string OpLte = "lte";
    private const string OpIsTrue = "isTrue";
    private const string OpIsFalse = "isFalse";
    private const string BooleanModePositiveNumber = "positiveNumber";

    private static readonly HashSet<string> StringOps = new(StringComparer.OrdinalIgnoreCase)
    {
        OpEquals, OpNotEquals, OpContains, OpNotContains, OpStartsWith, OpNotStartsWith, OpEndsWith, OpNotEndsWith, OpIn,
    };

    private static readonly HashSet<string> NumberOps = new(StringComparer.OrdinalIgnoreCase)
    {
        OpEquals, OpNotEquals, OpIn, OpRange, OpGt, OpGte, OpLt, OpLte,
    };

    private static readonly HashSet<string> BooleanOps = new(StringComparer.OrdinalIgnoreCase)
    {
        OpEquals, OpNotEquals, OpIsTrue, OpIsFalse,
    };

    private static readonly HashSet<string> SupportedEditableCriteriaAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        CriteriaExcludeGratings,
        CriteriaExcludeExisting,
    };

    private readonly ApplyRuleValidator _applyRuleValidator = new();

    public PhaseTableConfig Validate(PhaseTableConfig? config, ILogger? log = null)
    {
        if (config == null)
        {
            log?.Warning("PhaseVisualizer config is null. Using defaults.");
            return PhaseTableConfigDefaults.Create();
        }

        if (config.Version != SupportedConfigVersion)
        {
            log?.Warning(
                "PhaseVisualizer config version {Version} is not supported (expected {Expected}). Using defaults.",
                config.Version,
                SupportedConfigVersion);
            return PhaseTableConfigDefaults.Create();
        }

        var normalized = new PhaseTableConfig
        {
            Version = SupportedConfigVersion,
            ObjectScope = PhaseObjectScope.Visible,
            PhaseKey = string.IsNullOrWhiteSpace(config.PhaseKey)
                ? "number"
                : config.PhaseKey.Trim(),
            Actions = NormalizeActions(config.Actions),
            Columns = new List<PhaseColumnConfig>(),
        };

        var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawColumn in config.Columns ?? Enumerable.Empty<PhaseColumnConfig>())
        {
            if (!TryNormalizeColumn(rawColumn, uniqueKeys, out var normalizedColumn, log))
            {
                continue;
            }

            normalized.Columns.Add(normalizedColumn);
        }

        if (normalized.Columns.Count == 0)
        {
            log?.Warning("PhaseVisualizer config has no valid columns. Using defaults.");
            return PhaseTableConfigDefaults.Create();
        }

        normalized.Columns = normalized.Columns
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Actions.Count == 0)
        {
            normalized.Actions = PhaseTableConfigDefaults.Create().Actions;
        }

        return normalized;
    }

    private bool TryNormalizeColumn(
        PhaseColumnConfig raw,
        HashSet<string> uniqueKeys,
        out PhaseColumnConfig normalized,
        ILogger? log)
    {
        normalized = new PhaseColumnConfig();

        if (raw == null)
        {
            return false;
        }

        var key = raw.Key?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            log?.Warning("PhaseVisualizer config skipped column with empty key.");
            return false;
        }

        if (!uniqueKeys.Add(key))
        {
            log?.Warning("PhaseVisualizer config skipped duplicate column key: {Key}", key);
            return false;
        }

        var rawAttribute = raw.Attribute?.Trim() ?? string.Empty;
        var rawTargetAttribute = raw.TargetAttribute?.Trim() ?? string.Empty;
        var rawBooleanMode = raw.BooleanMode?.Trim() ?? string.Empty;
        var rawApplyRule = raw.ApplyRule;

        PhaseColumnObjectType? normalizedObjectType = null;
        var normalizedAttribute = string.Empty;
        PhaseColumnObjectType? normalizedTargetObjectType = null;
        var normalizedTargetAttribute = string.Empty;
        var normalizedBooleanMode = string.Empty;
        PhaseApplyRuleConfig? normalizedApplyRule = null;

        if (raw.Editable)
        {
            if (raw.ObjectType.HasValue || !string.IsNullOrWhiteSpace(rawAttribute))
            {
                log?.Warning(
                    "PhaseVisualizer config skipped editable column {Key}: editable columns cannot define objectType/attribute.",
                    key);
                return false;
            }

            var hasTargetObjectType = raw.TargetObjectType.HasValue;
            var hasTargetAttribute = !string.IsNullOrWhiteSpace(rawTargetAttribute);
            if (hasTargetObjectType || hasTargetAttribute)
            {
                if (!hasTargetObjectType && hasTargetAttribute)
                {
                    if (!SupportedEditableCriteriaAttributes.Contains(rawTargetAttribute))
                    {
                        log?.Warning(
                            "PhaseVisualizer config skipped editable column {Key}: unknown targetAttribute '{TargetAttribute}' without targetObjectType.",
                            key,
                            rawTargetAttribute);
                        return false;
                    }

                    log?.Warning(
                        "PhaseVisualizer config column {Key} uses legacy criteria targetAttribute '{TargetAttribute}'. " +
                        "Prefer explicit editable columns with targetObjectType + targetAttribute.",
                        key,
                        rawTargetAttribute);
                    normalizedTargetAttribute = rawTargetAttribute;
                }
                else
                {
                    if (!hasTargetObjectType || !hasTargetAttribute)
                    {
                        log?.Warning(
                            "PhaseVisualizer config skipped editable column {Key}: targetObjectType and targetAttribute must both be defined.",
                            key);
                        return false;
                    }

                    if (raw.TargetObjectType != PhaseColumnObjectType.Part
                        && raw.TargetObjectType != PhaseColumnObjectType.Assembly)
                    {
                        log?.Warning(
                            "PhaseVisualizer config skipped editable column {Key}: targetObjectType must be Part or Assembly.",
                            key);
                        return false;
                    }

                    normalizedTargetObjectType = raw.TargetObjectType.Value;
                    normalizedTargetAttribute = rawTargetAttribute;
                }
            }

            normalizedBooleanMode = NormalizeBooleanMode(rawBooleanMode, key, raw.Type, log);

            if (rawApplyRule != null)
            {
                if (string.IsNullOrWhiteSpace(normalizedTargetAttribute))
                {
                    log?.Warning(
                        "PhaseVisualizer config column {Key} ignored applyRule because targetAttribute is required.",
                        key);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(normalizedBooleanMode))
                    {
                        log?.Warning(
                            "PhaseVisualizer config column {Key} uses both booleanMode and applyRule. applyRule has priority.",
                            key);
                    }

                    if (_applyRuleValidator.TryNormalize(
                            rawApplyRule,
                            key,
                            raw.Type,
                            out normalizedApplyRule,
                            out var applyRuleDiagnostics))
                    {
                        foreach (var diagnostic in applyRuleDiagnostics)
                        {
                            log?.Warning(diagnostic);
                        }
                    }
                    else
                    {
                        foreach (var diagnostic in applyRuleDiagnostics)
                        {
                            log?.Warning(diagnostic);
                        }
                    }
                }
            }
        }
        else
        {
            if (!raw.ObjectType.HasValue || string.IsNullOrWhiteSpace(rawAttribute))
            {
                log?.Warning(
                    "PhaseVisualizer config skipped column {Key}: objectType and attribute are required for model columns.",
                    key);
                return false;
            }

            if (raw.TargetObjectType.HasValue || !string.IsNullOrWhiteSpace(rawTargetAttribute))
            {
                log?.Warning(
                    "PhaseVisualizer config skipped column {Key}: model columns cannot define targetObjectType/targetAttribute.",
                    key);
                return false;
            }

            if (!PhaseSourceResolver.TryBuildModelSource(
                    raw.ObjectType.Value,
                    rawAttribute,
                    out normalizedAttribute,
                    out _,
                    out var failureReason))
            {
                log?.Warning(
                    "PhaseVisualizer config skipped column {Key}: invalid objectType/attribute ({Reason}).",
                    key,
                    failureReason);
                return false;
            }

            normalizedObjectType = raw.ObjectType.Value;

            if (!string.IsNullOrWhiteSpace(rawBooleanMode))
            {
                log?.Warning(
                    "PhaseVisualizer config column {Key} ignored booleanMode because only editable columns support it.",
                    key);
            }

            if (rawApplyRule != null)
            {
                log?.Warning(
                    "PhaseVisualizer config column {Key} ignored applyRule because only editable columns support it.",
                    key);
            }
        }

        normalized = new PhaseColumnConfig
        {
            Key = key,
            Label = string.IsNullOrWhiteSpace(raw.Label) ? key : raw.Label.Trim(),
            Type = raw.Type,
            Editable = raw.Editable,
            ObjectType = normalizedObjectType,
            Attribute = normalizedAttribute,
            TargetObjectType = normalizedTargetObjectType,
            TargetAttribute = normalizedTargetAttribute,
            BooleanMode = normalizedBooleanMode,
            ApplyRule = normalizedApplyRule,
            Aggregate = raw.Aggregate,
            VisibleByDefault = raw.VisibleByDefault,
            Order = raw.Order,
            FilterOps = NormalizeFilterOps(raw.FilterOps, raw.Type, key, log),
        };

        return true;
    }

    private static List<string> NormalizeActions(IReadOnlyCollection<string>? actions)
    {
        if (actions == null || actions.Count == 0)
        {
            return new List<string>();
        }

        var result = new List<string>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in actions)
        {
            var value = action?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (unique.Add(value!))
            {
                result.Add(value!);
            }
        }

        return result;
    }

    private static List<string> NormalizeFilterOps(
        IReadOnlyCollection<string>? filterOps,
        PhaseValueType valueType,
        string columnKey,
        ILogger? log)
    {
        if (filterOps == null || filterOps.Count == 0)
        {
            return new List<string>();
        }

        var result = new List<string>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var op in filterOps)
        {
            var value = op?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalizedOp = NormalizeFilterOpName(value!);
            if (string.IsNullOrWhiteSpace(normalizedOp))
            {
                continue;
            }

            if (!IsSupportedFilterOpForType(normalizedOp, valueType))
            {
                log?.Warning(
                    "PhaseVisualizer config skipped filter op {Op} for column {Key} (type={Type}).",
                    value,
                    columnKey,
                    valueType);
                continue;
            }

            if (unique.Add(normalizedOp))
            {
                result.Add(normalizedOp);
            }
        }

        return result;
    }

    private static string NormalizeFilterOpName(string op)
    {
        var value = op?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("_", string.Empty).Replace("-", string.Empty);
        return normalized.ToLowerInvariant() switch
        {
            "equals" => OpEquals,
            "eq" => OpEquals,
            "notequals" => OpNotEquals,
            "neq" => OpNotEquals,
            "contains" => OpContains,
            "notcontains" => OpNotContains,
            "startswith" => OpStartsWith,
            "notstartswith" => OpNotStartsWith,
            "endswith" => OpEndsWith,
            "notendswith" => OpNotEndsWith,
            "in" => OpIn,
            "range" => OpRange,
            "gt" => OpGt,
            "gte" => OpGte,
            "lt" => OpLt,
            "lte" => OpLte,
            "istrue" => OpIsTrue,
            "isfalse" => OpIsFalse,
            _ => string.Empty,
        };
    }

    private static bool IsSupportedFilterOpForType(string op, PhaseValueType valueType)
    {
        return valueType switch
        {
            PhaseValueType.String => StringOps.Contains(op),
            PhaseValueType.Number => NumberOps.Contains(op),
            PhaseValueType.Integer => NumberOps.Contains(op),
            PhaseValueType.Boolean => BooleanOps.Contains(op),
            _ => false,
        };
    }

    private static string NormalizeBooleanMode(
        string rawBooleanMode,
        string columnKey,
        PhaseValueType valueType,
        ILogger? log)
    {
        if (string.IsNullOrWhiteSpace(rawBooleanMode))
        {
            return string.Empty;
        }

        if (valueType != PhaseValueType.Boolean)
        {
            log?.Warning(
                "PhaseVisualizer config column {Key} ignored booleanMode '{BooleanMode}' because column type is {Type}, expected Boolean.",
                columnKey,
                rawBooleanMode,
                valueType);
            return string.Empty;
        }

        if (string.Equals(rawBooleanMode, BooleanModePositiveNumber, StringComparison.OrdinalIgnoreCase))
        {
            return BooleanModePositiveNumber;
        }

        log?.Warning(
            "PhaseVisualizer config column {Key} ignored unknown booleanMode '{BooleanMode}'. Supported values: {Supported}.",
            columnKey,
            rawBooleanMode,
            BooleanModePositiveNumber);
        return string.Empty;
    }
}

