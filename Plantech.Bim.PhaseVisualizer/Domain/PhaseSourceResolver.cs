using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal static class PhaseSourceResolver
{
    private const string PhasePrefix = "phase.";
    private const string PartPrefix = "part.";
    private const string PartUdaPrefix = "part.ua.";
    private const string AssemblyMainPartPrefix = "assembly.mainpart.";
    private const string UdaPrefix = "ua.";

    private static readonly HashSet<string> SupportedPhaseAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "number",
        "name",
    };

    private static readonly HashSet<string> SupportedPartAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "profile",
        "material",
        "class",
        "name",
        "finish",
    };

    public static bool TryBuildModelSource(
        PhaseColumnObjectType objectType,
        string? attribute,
        out string normalizedAttribute,
        out string source,
        out string failureReason)
    {
        normalizedAttribute = string.Empty;
        source = string.Empty;
        failureReason = string.Empty;

        switch (objectType)
        {
            case PhaseColumnObjectType.Phase:
            {
                var phaseAttribute = NormalizeSimpleAttribute(attribute);
                if (!SupportedPhaseAttributes.Contains(phaseAttribute))
                {
                    failureReason = "phase attribute must be 'number' or 'name'";
                    return false;
                }

                normalizedAttribute = phaseAttribute;
                source = $"{PhasePrefix}{phaseAttribute}";
                return true;
            }
            case PhaseColumnObjectType.Part:
                return TryBuildPartLikeSource(
                    PartPrefix,
                    attribute,
                    allowUda: true,
                    out normalizedAttribute,
                    out source,
                    out failureReason);
            case PhaseColumnObjectType.AssemblyMainPart:
                return TryBuildPartLikeSource(
                    AssemblyMainPartPrefix,
                    attribute,
                    allowUda: false,
                    out normalizedAttribute,
                    out source,
                    out failureReason);
            default:
                failureReason = $"unsupported objectType '{objectType}'";
                return false;
        }
    }

    public static bool TryGetTemplateStringField(
        PhaseColumnObjectType objectType,
        string attribute,
        out string templateField)
    {
        templateField = string.Empty;

        if (!TryBuildModelSource(objectType, attribute, out var normalizedAttribute, out _, out _))
        {
            return false;
        }

        if (normalizedAttribute.StartsWith(UdaPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        switch (objectType)
        {
            case PhaseColumnObjectType.Part:
                templateField = normalizedAttribute switch
                {
                    "profile" => "PROFILE",
                    "material" => "MATERIAL",
                    "class" => "CLASS",
                    "name" => "NAME",
                    "finish" => "FINISH",
                    _ => string.Empty,
                };
                return templateField.Length > 0;
            case PhaseColumnObjectType.AssemblyMainPart:
                templateField = normalizedAttribute switch
                {
                    "profile" => "ASSEMBLY.MAINPART.PROFILE",
                    "material" => "ASSEMBLY.MAINPART.MATERIAL",
                    "class" => "ASSEMBLY.MAINPART.CLASS",
                    "name" => "ASSEMBLY.MAINPART.NAME",
                    "finish" => "ASSEMBLY.MAINPART.FINISH",
                    _ => string.Empty,
                };
                return templateField.Length > 0;
            default:
                return false;
        }
    }

    private static bool TryBuildPartLikeSource(
        string sourcePrefix,
        string? rawAttribute,
        bool allowUda,
        out string normalizedAttribute,
        out string source,
        out string failureReason)
    {
        normalizedAttribute = string.Empty;
        source = string.Empty;
        failureReason = string.Empty;

        var attribute = rawAttribute?.Trim() ?? string.Empty;
        if (attribute.Length == 0)
        {
            failureReason = "attribute cannot be empty";
            return false;
        }

        if (allowUda && attribute.StartsWith(UdaPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var udaName = attribute.Substring(UdaPrefix.Length).Trim();
            if (udaName.Length == 0)
            {
                failureReason = "ua.<name> requires a non-empty user attribute name";
                return false;
            }

            normalizedAttribute = $"ua.{udaName}";
            source = $"{sourcePrefix}ua.{udaName}";
            return true;
        }

        var simpleAttribute = NormalizeSimpleAttribute(attribute);
        if (!SupportedPartAttributes.Contains(simpleAttribute))
        {
            failureReason = allowUda
                ? "attribute must be one of profile/material/class/name/finish or ua.<name>"
                : "attribute must be one of profile/material/class/name/finish";
            return false;
        }

        normalizedAttribute = simpleAttribute;
        source = $"{sourcePrefix}{simpleAttribute}";
        return true;
    }

    private static string NormalizeSimpleAttribute(string? attribute)
    {
        return attribute?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}

