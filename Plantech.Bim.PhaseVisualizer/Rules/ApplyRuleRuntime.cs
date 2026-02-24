using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Plantech.Bim.PhaseVisualizer.Domain;

namespace Plantech.Bim.PhaseVisualizer.Rules;

internal enum ApplyRuleOperation
{
    Eq,
    Neq,
    In,
    Contains,
    NotContains,
    StartsWith,
    NotStartsWith,
    EndsWith,
    NotEndsWith,
    Gt,
    Gte,
    Lt,
    Lte,
}

internal sealed class ApplyRuleClauseRuntime
{
    public string Field { get; set; } = string.Empty;
    public ApplyRuleOperation Operation { get; set; } = ApplyRuleOperation.Eq;
    public bool UsesInputValue { get; set; }
    public IReadOnlyList<string> LiteralValues { get; set; } = Array.Empty<string>();
    public bool UseProfileScope { get; set; }
    public PhaseColumnObjectType? ProfileScopeObjectType { get; set; }
}

internal static class ApplyRuleOperationHelper
{
    public static bool TryParse(string? operation, out ApplyRuleOperation parsed)
    {
        parsed = default;
        var normalized = Normalize(operation);
        return normalized switch
        {
            "eq" => TrySet(ApplyRuleOperation.Eq, out parsed),
            "neq" => TrySet(ApplyRuleOperation.Neq, out parsed),
            "in" => TrySet(ApplyRuleOperation.In, out parsed),
            "contains" => TrySet(ApplyRuleOperation.Contains, out parsed),
            "notcontains" => TrySet(ApplyRuleOperation.NotContains, out parsed),
            "startswith" => TrySet(ApplyRuleOperation.StartsWith, out parsed),
            "notstartswith" => TrySet(ApplyRuleOperation.NotStartsWith, out parsed),
            "endswith" => TrySet(ApplyRuleOperation.EndsWith, out parsed),
            "notendswith" => TrySet(ApplyRuleOperation.NotEndsWith, out parsed),
            "gt" => TrySet(ApplyRuleOperation.Gt, out parsed),
            "gte" => TrySet(ApplyRuleOperation.Gte, out parsed),
            "lt" => TrySet(ApplyRuleOperation.Lt, out parsed),
            "lte" => TrySet(ApplyRuleOperation.Lte, out parsed),
            _ => false,
        };
    }

    public static string Normalize(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return string.Empty;
        }

        var value = operation!.Trim().Replace("_", string.Empty).Replace("-", string.Empty);
        return value.ToLowerInvariant() switch
        {
            "equals" => "eq",
            "eq" => "eq",
            "notequals" => "neq",
            "neq" => "neq",
            "contains" => "contains",
            "notcontains" => "notcontains",
            "startswith" => "startswith",
            "notstartswith" => "notstartswith",
            "endswith" => "endswith",
            "notendswith" => "notendswith",
            "in" => "in",
            "gt" => "gt",
            "gte" => "gte",
            "lt" => "lt",
            "lte" => "lte",
            _ => string.Empty,
        };
    }

    public static IReadOnlyList<string> ToLiteralValues(object? rawValue)
    {
        if (rawValue == null)
        {
            return Array.Empty<string>();
        }

        if (rawValue is string s)
        {
            return new[] { s.Trim() };
        }

        if (rawValue is IEnumerable sequence)
        {
            var values = new List<string>();
            foreach (var item in sequence)
            {
                var text = ToInvariantString(item);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text.Trim());
                }
            }

            return values;
        }

        var single = ToInvariantString(rawValue);
        return string.IsNullOrWhiteSpace(single)
            ? Array.Empty<string>()
            : new[] { single.Trim() };
    }

    public static string ToInvariantString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static bool TrySet(ApplyRuleOperation value, out ApplyRuleOperation parsed)
    {
        parsed = value;
        return true;
    }
}
