using System.Collections.Generic;
using System.IO;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal static class PhaseConfigPaths
{
    internal const string ConfigDirectoryName = "PT_PhaseVisualizer";
    internal const string LegacyConfigFileName = "phase-visualizer.json";
    internal const string ConfigProfileFileSuffix = ".phase-visualizer.json";
    internal const string DefaultProfileKey = "default";

    internal static string BuildConfigFileName(string profileKey)
    {
        return $"{(string.IsNullOrWhiteSpace(profileKey) ? DefaultProfileKey : profileKey.Trim())}{ConfigProfileFileSuffix}";
    }

    internal static string? BuildConfigFilePathFromConfigDirectory(string? configDirectory, string? profileKey = null)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return null;
        }

        var effectiveProfileKey = string.IsNullOrWhiteSpace(profileKey)
            ? string.Empty
            : profileKey!;
        var fileName = string.IsNullOrWhiteSpace(effectiveProfileKey)
            ? LegacyConfigFileName
            : BuildConfigFileName(effectiveProfileKey);
        return Path.Combine(configDirectory, fileName);
    }

    internal static string? BuildConfigDirectoryPathFromRootDirectory(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return null;
        }

        return Path.Combine(rootDirectory, ConfigDirectoryName);
    }

    internal static bool IsConfigDirectoryName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, ConfigDirectoryName, System.StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryExtractProfileKeyFromFileName(string? fileName, out string profileKey)
    {
        profileKey = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (string.Equals(fileName, LegacyConfigFileName, System.StringComparison.OrdinalIgnoreCase))
        {
            profileKey = DefaultProfileKey;
            return true;
        }

        var effectiveFileName = fileName!;
        if (!effectiveFileName.EndsWith(ConfigProfileFileSuffix, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var key = effectiveFileName.Substring(0, effectiveFileName.Length - ConfigProfileFileSuffix.Length).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        profileKey = key;
        return true;
    }
}
