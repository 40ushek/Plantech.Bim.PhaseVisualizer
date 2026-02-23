using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using System;
using System.IO;
using System.Reflection;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal sealed class PhaseTableConfigLoader
{
    internal const string ConfigDirectoryName = ".plantech";
    internal const string ConfigFileName = "phase-visualizer.json";

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Include,
        Converters = { new StringEnumConverter() },
    };

    private readonly PhaseTableConfigValidator _validator;

    public PhaseTableConfigLoader()
        : this(new PhaseTableConfigValidator())
    {
    }

    internal PhaseTableConfigLoader(PhaseTableConfigValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public PhaseTableConfig Load(string modelConfigDirectory, ILogger? log = null)
    {
        if (TryLoadConfig(BuildModelConfigPath(modelConfigDirectory), "model", out var config, log))
        {
            return _validator.Validate(config, log);
        }

        if (TryLoadConfig(GetExtensionConfigPath(), "extension", out config, log))
        {
            return _validator.Validate(config, log);
        }

        log?.Warning("PhaseVisualizer config not found. Using embedded defaults.");
        return _validator.Validate(PhaseTableConfigDefaults.Create(), log);
    }

    public string ResolveEffectiveConfigDirectory(string modelConfigDirectory)
    {
        var modelConfigPath = BuildModelConfigPath(modelConfigDirectory);
        if (!string.IsNullOrWhiteSpace(modelConfigPath) && File.Exists(modelConfigPath))
        {
            return Path.GetDirectoryName(modelConfigPath) ?? string.Empty;
        }

        var extensionConfigPath = GetExtensionConfigPath();
        if (!string.IsNullOrWhiteSpace(extensionConfigPath) && File.Exists(extensionConfigPath))
        {
            return Path.GetDirectoryName(extensionConfigPath) ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(modelConfigDirectory))
        {
            return modelConfigDirectory;
        }

        return !string.IsNullOrWhiteSpace(extensionConfigPath)
            ? Path.GetDirectoryName(extensionConfigPath) ?? string.Empty
            : string.Empty;
    }

    private static string? BuildModelConfigPath(string modelConfigDirectory)
    {
        if (string.IsNullOrWhiteSpace(modelConfigDirectory))
        {
            return null;
        }

        return Path.Combine(modelConfigDirectory, ConfigFileName);
    }

    private static string? GetExtensionConfigPath()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrWhiteSpace(assemblyLocation))
        {
            return null;
        }

        var extensionRoot = Path.GetDirectoryName(assemblyLocation);
        if (string.IsNullOrWhiteSpace(extensionRoot))
        {
            return null;
        }

        return Path.Combine(extensionRoot, ConfigDirectoryName, ConfigFileName);
    }

    private static bool TryLoadConfig(
        string? filePath,
        string sourceName,
        out PhaseTableConfig? config,
        ILogger? log)
    {
        config = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                log?.Warning(
                    "PhaseVisualizer {Source} config is empty at {Path}.",
                    sourceName,
                    filePath);
                return false;
            }

            var parsed = JsonConvert.DeserializeObject<PhaseTableConfig>(json, JsonSettings);
            if (parsed == null)
            {
                log?.Warning(
                    "PhaseVisualizer {Source} config cannot be parsed at {Path}.",
                    sourceName,
                    filePath);
                return false;
            }

            config = parsed;
            return true;
        }
        catch (Exception ex)
        {
            log?.Warning(
                ex,
                "PhaseVisualizer {Source} config load failed at {Path}.",
                sourceName,
                filePath);
            return false;
        }
    }
}

