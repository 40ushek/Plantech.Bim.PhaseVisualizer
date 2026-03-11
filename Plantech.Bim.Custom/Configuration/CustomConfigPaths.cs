using System;
using System.Collections.Generic;
using System.IO;
using Tekla.Structures;

namespace Plantech.Bim.Custom.Configuration;

internal static class CustomConfigPaths
{
    internal const string ConfigDirectoryName = "PT_PhaseVisualizer";

    internal static IEnumerable<string> EnumerateCandidatePaths(string configFileName, string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(configFileName))
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            yield return Path.Combine(modelPath, "attributes", ConfigDirectoryName, configFileName);
            yield return Path.Combine(modelPath, ConfigDirectoryName, configFileName);
        }

        foreach (var firmRoot in GetAdvancedOptionDirectories("XS_FIRM"))
        {
            yield return Path.Combine(firmRoot, ConfigDirectoryName, configFileName);
        }

        var appBase = AppDomain.CurrentDomain.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(appBase))
        {
            yield return Path.Combine(appBase, ConfigDirectoryName, configFileName);
        }
    }

    private static IEnumerable<string> GetAdvancedOptionDirectories(string optionName)
    {
        string raw;
        try
        {
            raw = string.Empty;
            TeklaStructuresSettings.GetAdvancedOption(optionName, ref raw);
        }
        catch
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        foreach (var token in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = token.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(candidate))
                yield return candidate;
        }
    }
}
