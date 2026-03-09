using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tekla.Structures;

namespace Plantech.Bim.Custom.Common;

internal static class TeklaAttributeDirectories
{
    public static IReadOnlyList<string> GetSearchDirectories(string? modelPath)
    {
        var result = new List<string>();

        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            var attributesPath = Path.Combine(modelPath, "attributes");
            AddIfExisting(result, attributesPath);
            AddIfExisting(result, modelPath);
        }

        AddFromAdvancedOption(result, "XS_PROJECT");
        AddFromAdvancedOption(result, "XS_FIRM");
        AddFromAdvancedOption(result, "XS_SYSTEM");

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddFromAdvancedOption(ICollection<string> target, string optionName)
    {
        try
        {
            var raw = string.Empty;
            TeklaStructuresSettings.GetAdvancedOption(optionName, ref raw);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            foreach (var candidate in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                AddIfExisting(target, candidate);
            }
        }
        catch
        {
        }
    }

    private static void AddIfExisting(ICollection<string> target, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = path!.Trim().Trim('"');
        if (Directory.Exists(normalized))
        {
            target.Add(normalized);
        }
    }
}
