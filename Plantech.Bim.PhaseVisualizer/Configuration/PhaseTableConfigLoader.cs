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
        foreach (var candidate in EnumerateConfigCandidates(modelConfigDirectory))
        {
            if (TryLoadConfig(candidate.FilePath, candidate.SourceName, out var config, log))
            {
                return _validator.Validate(config, log);
            }
        }

        log?.Warning("PhaseVisualizer config not found. Using embedded defaults.");
        return _validator.Validate(PhaseTableConfigDefaults.Create(), log);
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
        return PhaseConfigPaths.BuildPreferredConfigDirectoryPathFromRootDirectory(rootDirectory);
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
}

