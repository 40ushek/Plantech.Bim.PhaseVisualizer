using System;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Domain;

internal static class PhaseSourceResolver
{
    private const string PhasePrefix = "phase.";
    private const string PartPrefix = "part.";
    private const string AssemblyPrefix = "assembly.";
    private const string BoltPrefix = "bolt.";
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
            case PhaseColumnObjectType.Assembly:
            {
                var assemblyAttribute = attribute?.Trim() ?? string.Empty;
                if (assemblyAttribute.Length == 0)
                {
                    failureReason = "attribute cannot be empty";
                    return false;
                }

                normalizedAttribute = assemblyAttribute;
                source = $"{AssemblyPrefix}{assemblyAttribute}";
                return true;
            }
            case PhaseColumnObjectType.Bolt:
            {
                var boltAttribute = attribute?.Trim() ?? string.Empty;
                if (boltAttribute.Length == 0)
                {
                    failureReason = "attribute cannot be empty";
                    return false;
                }

                normalizedAttribute = boltAttribute;
                source = $"{BoltPrefix}{boltAttribute}";
                return true;
            }
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
            case PhaseColumnObjectType.Assembly:
                // The attribute IS the Tekla template field name (e.g. ASSEMBLY.MAINPART.PROFILE).
                templateField = normalizedAttribute;
                return templateField.Length > 0;
            case PhaseColumnObjectType.Bolt:
                // The attribute IS the Tekla report property name (e.g. BOLT_STANDARD).
                templateField = normalizedAttribute;
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

