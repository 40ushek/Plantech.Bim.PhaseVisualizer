using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using Tekla.Structures;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal sealed class PhaseTableConfigLoader
{
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
        var foundAnyConfigFile = false;
        foreach (var candidate in EnumerateConfigCandidates(modelConfigDirectory))
        {
            if (File.Exists(candidate.FilePath))
            {
                foundAnyConfigFile = true;
            }

            if (TryLoadConfig(candidate.FilePath, candidate.SourceName, out var config, log))
            {
                return _validator.Validate(config, log);
            }
        }

        var defaults = PhaseTableConfigDefaults.Create();
        if (!foundAnyConfigFile
            && TryCreateModelDefaultConfig(modelConfigDirectory, defaults, out var createdConfigPath, log))
        {
            log?.Information(
                "PhaseVisualizer config not found. Created default config at {Path}.",
                createdConfigPath);
        }
        else
        {
            log?.Warning("PhaseVisualizer config not found. Using embedded defaults.");
        }

        return _validator.Validate(defaults, log);
    }

    public ConfigResolutionInfo ResolveConfigResolution(string modelConfigDirectory)
    {
        var probePaths = new List<string>();
        foreach (var candidate in EnumerateConfigCandidates(modelConfigDirectory))
        {
            probePaths.Add(candidate.FilePath);
            if (File.Exists(candidate.FilePath))
            {
                return new ConfigResolutionInfo(
                    candidate.FilePath,
                    candidate.SourceName,
                    probePaths,
                    PhaseConfigPaths.ConfigFileName);
            }
        }

        return new ConfigResolutionInfo(
            string.Empty,
            "embedded-defaults",
            probePaths,
            PhaseConfigPaths.ConfigFileName);
    }

    public string ResolveEffectiveConfigDirectory(string modelConfigDirectory)
    {
        foreach (var candidate in EnumerateConfigCandidates(modelConfigDirectory))
        {
            if (File.Exists(candidate.FilePath))
            {
                return Path.GetDirectoryName(candidate.FilePath) ?? string.Empty;
            }
        }

        var preferredModelConfigDirectory = ResolvePreferredConfigDirectoryFromRootOrConfigDirectory(modelConfigDirectory);
        if (!string.IsNullOrWhiteSpace(preferredModelConfigDirectory))
        {
            return preferredModelConfigDirectory!;
        }

        var preferredCompanyConfigDirectory = ResolvePreferredConfigDirectoryFromRootOrConfigDirectory(GetCompanyRootDirectory());
        if (!string.IsNullOrWhiteSpace(preferredCompanyConfigDirectory))
        {
            return preferredCompanyConfigDirectory!;
        }

        var preferredApplicationConfigDirectory = ResolvePreferredConfigDirectoryFromRootOrConfigDirectory(GetApplicationRootDirectory());
        return preferredApplicationConfigDirectory ?? string.Empty;
    }

    private static IEnumerable<string> BuildModelConfigPaths(string modelConfigDirectory)
    {
        return BuildConfigFilePathsFromRootOrConfigDirectory(modelConfigDirectory);
    }

    private static IEnumerable<string> GetCompanyConfigPaths()
    {
        return BuildConfigFilePathsFromRootOrConfigDirectory(GetCompanyRootDirectory());
    }

    private static IEnumerable<string> GetApplicationConfigPaths()
    {
        return BuildConfigFilePathsFromRootOrConfigDirectory(GetApplicationRootDirectory());
    }

    private static string? GetCompanyRootDirectory()
    {
        try
        {
            var rawFirmPath = string.Empty;
            TeklaStructuresSettings.GetAdvancedOption("XS_FIRM", ref rawFirmPath);
            if (string.IsNullOrWhiteSpace(rawFirmPath))
            {
                return null;
            }

            foreach (var firmPathToken in rawFirmPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var firmPath = firmPathToken.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(firmPath))
                {
                    continue;
                }

                return firmPath;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetApplicationRootDirectory()
    {
        var applicationBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        if (string.IsNullOrWhiteSpace(applicationBaseDirectory))
        {
            return null;
        }

        return applicationBaseDirectory;
    }

    private static IEnumerable<(string FilePath, string SourceName)> EnumerateConfigCandidates(string modelConfigDirectory)
    {
        var candidates = new (IEnumerable<string> FilePaths, string SourceName)[]
        {
            (BuildModelConfigPaths(modelConfigDirectory), "model"),
            (GetCompanyConfigPaths(), "firm"),
            (GetApplicationConfigPaths(), "application"),
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            foreach (var filePath in candidate.FilePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                var normalizedCandidatePath = NormalizePath(filePath);
                if (!seen.Add(normalizedCandidatePath))
                {
                    continue;
                }

                yield return (filePath, candidate.SourceName);
            }
        }
    }

    private static IEnumerable<string> BuildConfigFilePathsFromRootOrConfigDirectory(string? rootOrConfigDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootOrConfigDirectory))
        {
            yield break;
        }

        var normalizedPath = NormalizePath(rootOrConfigDirectory);
        var rootDirectory = ResolveRootDirectoryFromRootOrConfigDirectory(normalizedPath);
        foreach (var configPath in PhaseConfigPaths.BuildConfigFilePathsFromRootDirectory(rootDirectory))
        {
            yield return configPath;
        }
    }

    private static string? ResolvePreferredConfigDirectoryFromRootOrConfigDirectory(string? rootOrConfigDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootOrConfigDirectory))
        {
            return null;
        }

        var normalizedPath = NormalizePath(rootOrConfigDirectory);
        var rootDirectory = ResolveRootDirectoryFromRootOrConfigDirectory(normalizedPath);
        return PhaseConfigPaths.BuildConfigDirectoryPathFromRootDirectory(rootDirectory);
    }

    private static string ResolveRootDirectoryFromRootOrConfigDirectory(string normalizedPath)
    {
        var leafDirectory = Path.GetFileName(normalizedPath);
        if (PhaseConfigPaths.IsConfigDirectoryName(leafDirectory))
        {
            return Path.GetFullPath(Path.Combine(normalizedPath, ".."));
        }

        return normalizedPath;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path ?? string.Empty;
        }
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

    private static bool TryCreateModelDefaultConfig(
        string modelConfigDirectory,
        PhaseTableConfig defaults,
        out string createdConfigPath,
        ILogger? log)
    {
        createdConfigPath = string.Empty;

        if (defaults == null)
        {
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(modelConfigDirectory))
            {
                return false;
            }

            var normalizedPath = NormalizePath(modelConfigDirectory);
            var modelRootDirectory = ResolveRootDirectoryFromRootOrConfigDirectory(normalizedPath);
            if (string.IsNullOrWhiteSpace(modelRootDirectory) || !Directory.Exists(modelRootDirectory))
            {
                return false;
            }

            var configDirectory = PhaseConfigPaths.BuildConfigDirectoryPathFromRootDirectory(modelRootDirectory);
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                return false;
            }

            Directory.CreateDirectory(configDirectory);

            createdConfigPath = Path.Combine(configDirectory, PhaseConfigPaths.ConfigFileName);
            if (File.Exists(createdConfigPath))
            {
                return true;
            }

            var json = JsonConvert.SerializeObject(defaults, Formatting.Indented, JsonSettings);
            File.WriteAllText(createdConfigPath, json);
            return true;
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "PhaseVisualizer failed to create default model config.");
            createdConfigPath = string.Empty;
            return false;
        }
    }

    public readonly struct ConfigResolutionInfo
    {
        public ConfigResolutionInfo(
            string effectiveConfigPath,
            string sourceName,
            IReadOnlyList<string> probePaths,
            string configFileName)
        {
            EffectiveConfigPath = effectiveConfigPath ?? string.Empty;
            SourceName = sourceName ?? string.Empty;
            ProbePaths = probePaths ?? Array.Empty<string>();
            ConfigFileName = configFileName ?? string.Empty;
        }

        public string EffectiveConfigPath { get; }
        public string SourceName { get; }
        public IReadOnlyList<string> ProbePaths { get; }
        public string ConfigFileName { get; }
    }
}

