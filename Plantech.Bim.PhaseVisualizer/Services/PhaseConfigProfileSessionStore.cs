using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseConfigProfileSessionStore
{
    public PhaseConfigProfileSession LoadSession(string? sessionFilePath, ILogger? log = null)
    {
        if (string.IsNullOrWhiteSpace(sessionFilePath) || !File.Exists(sessionFilePath))
        {
            return new PhaseConfigProfileSession();
        }

        try
        {
            var json = File.ReadAllText(sessionFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PhaseConfigProfileSession();
            }

            return JsonConvert.DeserializeObject<PhaseConfigProfileSession>(json)
                ?? new PhaseConfigProfileSession();
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "PhaseVisualizer session load failed at {Path}.", sessionFilePath);
            return new PhaseConfigProfileSession();
        }
    }

    public string LoadSelectedProfileKey(string? sessionFilePath, ILogger? log = null)
    {
        return LoadSession(sessionFilePath, log).SelectedProfileKey?.Trim() ?? string.Empty;
    }

    public string LoadSelectedStateName(string? sessionFilePath, ILogger? log = null)
    {
        return PhaseRuntimeSelectionResolver.NormalizeStateName(
            LoadSession(sessionFilePath, log).SelectedStateName);
    }

    public void SaveSelection(
        string? sessionFilePath,
        string? profileKey,
        string? stateName,
        ILogger? log = null)
    {
        if (string.IsNullOrWhiteSpace(sessionFilePath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(sessionFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var session = new PhaseConfigProfileSession
            {
                SelectedProfileKey = profileKey?.Trim() ?? string.Empty,
                SelectedStateName = PhaseRuntimeSelectionResolver.NormalizeStateName(stateName),
            };
            File.WriteAllText(sessionFilePath, JsonConvert.SerializeObject(session, Formatting.Indented));
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "PhaseVisualizer session save failed at {Path}.", sessionFilePath);
        }
    }
}

internal sealed class PhaseConfigProfileSession
{
    public string SelectedProfileKey { get; set; } = string.Empty;

    public string SelectedStateName { get; set; } = "default";
}
