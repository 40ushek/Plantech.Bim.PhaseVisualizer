using System.Collections.Generic;
using System.IO;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal static class PhaseConfigPaths
{
    internal const string ConfigDirectoryName = "PT_PhaseVisualizer";
    internal const string ConfigFileName = "phase-visualizer.json";

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

        yield return Path.Combine(rootDirectory, ConfigDirectoryName, ConfigFileName);
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
}
