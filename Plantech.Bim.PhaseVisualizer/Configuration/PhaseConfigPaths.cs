using System.Collections.Generic;
using System.IO;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal static class PhaseConfigPaths
{
    internal const string PreferredConfigDirectoryName = "PT_PhaseVisualizer";
    internal const string LegacyConfigDirectoryName = ".plantech";
    internal const string ConfigDirectoryName = PreferredConfigDirectoryName;
    internal const string ConfigFileName = "phase-visualizer.json";

    internal static IReadOnlyList<string> ConfigDirectorySearchOrder { get; } = new[]
    {
        PreferredConfigDirectoryName,
        LegacyConfigDirectoryName,
    };

    internal static string? BuildConfigFilePathFromConfigDirectory(string? configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return null;
        }

        return Path.Combine(configDirectory, ConfigFileName);
    }

    internal static IEnumerable<string> BuildConfigFilePathsFromRootDirectory(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            yield break;
        }

        foreach (var configDirectoryName in ConfigDirectorySearchOrder)
        {
            yield return Path.Combine(rootDirectory, configDirectoryName, ConfigFileName);
        }
    }

    internal static string? BuildPreferredConfigDirectoryPathFromRootDirectory(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return null;
        }

        return Path.Combine(rootDirectory, PreferredConfigDirectoryName);
    }

    internal static bool IsConfigDirectoryName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var configDirectoryName in ConfigDirectorySearchOrder)
        {
            if (string.Equals(value, configDirectoryName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
