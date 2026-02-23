using Newtonsoft.Json;
using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System;
using System.IO;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseTableStateStore
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Include,
        Formatting = Formatting.Indented,
    };

    public PhaseTableState? Load(string? filePath, ILogger? log = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var state = JsonConvert.DeserializeObject<PhaseTableState>(json, JsonSettings);
            if (state == null)
            {
                return null;
            }

            state.Rows ??= new();
            state.Presets ??= new();
            foreach (var preset in state.Presets)
            {
                preset.Rows ??= new();
            }

            return state;
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "PhaseVisualizer state load failed at {Path}.", filePath);
            return null;
        }
    }

    public void Save(string? filePath, PhaseTableState? state, ILogger? log = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || state == null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(state, JsonSettings);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "PhaseVisualizer state save failed at {Path}.", filePath);
        }
    }
}

