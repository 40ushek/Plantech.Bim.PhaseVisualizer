using System;
using System.IO;

namespace Plantech.Bim.Custom.Services;

public sealed class FilteredEvaluationDiagnosticsService
{
    public FilteredEvaluationResult Enrich(FilteredEvaluationResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        result.ConfigFileContent = ReadFileText(result.ConfigFilePath);
        result.ResolvedTeklaFilterContent = ReadFileText(result.ResolvedTeklaFilterPath);
        return result;
    }

    private static string ReadFileText(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return string.Empty;
        }
    }
}
