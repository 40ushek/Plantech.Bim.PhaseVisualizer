using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseConfigProfileSessionStore
{
    public string LoadSelectedProfileKey(string? sessionFilePath, ILogger? log = null)
    {
        if (string.IsNullOrWhiteSpace(sessionFilePath) || !File.Exists(sessionFilePath))
        {
            return string.Empty;
        }

        try
        {
            var json = File.ReadAllText(sessionFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            var session = JsonConvert.DeserializeObject<PhaseConfigProfileSession>(json);
            return session?.SelectedProfileKey?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "PhaseVisualizer session load failed at {Path}.", sessionFilePath);
            return string.Empty;
        }
    }

    public void SaveSelectedProfileKey(string? sessionFilePath, string? profileKey, ILogger? log = null)
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
}
