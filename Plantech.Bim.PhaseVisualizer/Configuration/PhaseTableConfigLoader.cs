using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    private readonly IPhaseConfigRootProvider _rootProvider;

    public PhaseTableConfigLoader()
        : this(new PhaseTableConfigValidator(), new TeklaPhaseConfigRootProvider())
    {
    }

    internal PhaseTableConfigLoader(
        PhaseTableConfigValidator validator,
        IPhaseConfigRootProvider rootProvider)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
    }

    public PhaseTableConfig Load(string modelConfigDirectory, ILogger? log = null)
    {
        return LoadResolved(modelConfigDirectory, selectedProfileKey: null, rememberedProfileKey: null, log).Config;
    }

    public PhaseTableConfig Load(
        string modelConfigDirectory,
        string? selectedProfileKey,
        string? rememberedProfileKey,
        ILogger? log = null)
    {
        return LoadResolved(modelConfigDirectory, selectedProfileKey, rememberedProfileKey, log).Config;
    }

    public PhaseConfigProfileCatalog DiscoverProfiles(string modelConfigDirectory)
    {
        return BuildCatalog(modelConfigDirectory);
    }

    public PhaseConfigProfileSelection ResolveSelection(
        string modelConfigDirectory,
        string? selectedProfileKey,
        string? rememberedProfileKey)
    {
        var discoveredCatalog = BuildCatalog(modelConfigDirectory);
        var selectedProfile = SelectProfile(discoveredCatalog, selectedProfileKey, rememberedProfileKey);
        var catalog = discoveredCatalog.Profiles.Any()
            ? discoveredCatalog
            : new PhaseConfigProfileCatalog(new[] { selectedProfile });
        var probePaths = BuildProbePaths(modelConfigDirectory);
        return new PhaseConfigProfileSelection(catalog, selectedProfile, probePaths);
    }

    public PhaseConfigLoadResult LoadResolved(
        string modelConfigDirectory,
        string? selectedProfileKey,
        string? rememberedProfileKey,
        ILogger? log = null)
    {
        var selection = ResolveSelection(modelConfigDirectory, selectedProfileKey, rememberedProfileKey);
        return LoadResolved(modelConfigDirectory, selection, log);
    }

    public PhaseConfigLoadResult LoadResolved(
        string modelConfigDirectory,
        PhaseConfigProfileSelection selection,
        ILogger? log = null)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        var selectedProfile = selection.SelectedProfile;
        if (selectedProfile.HasPhysicalFile
            && TryLoadConfig(selectedProfile.FilePath, selectedProfile.SourceName, out var config, log))
        {
            return new PhaseConfigLoadResult(
                _validator.Validate(config, log),
                selection.Catalog,
                selectedProfile,
                selection.ProbePaths,
                ResolveEffectiveConfigDirectory(modelConfigDirectory, selection));
        }

        var defaults = PhaseTableConfigDefaults.Create();
        var effectiveCatalog = selection.Catalog;
        var effectiveProfile = selectedProfile;
        var hasPhysicalProfiles = selection.Catalog.Profiles.Any(profile => profile.HasPhysicalFile);

        if (!hasPhysicalProfiles
            && TryCreateModelDefaultConfig(modelConfigDirectory, defaults, out var createdConfigPath, log))
        {
            effectiveProfile = new PhaseConfigProfileDescriptor(
                PhaseConfigPaths.DefaultProfileKey,
                PhaseConfigPaths.DefaultProfileKey,
                Path.GetFileName(createdConfigPath) ?? PhaseConfigPaths.BuildConfigFileName(PhaseConfigPaths.DefaultProfileKey),
                createdConfigPath,
                PhaseConfigProfileSourceKind.Model);
            effectiveCatalog = new PhaseConfigProfileCatalog(new[] { effectiveProfile });

            log?.Information(
                "PhaseVisualizer config not found. Created default config profile at {Path}.",
                createdConfigPath);
        }
        else if (!hasPhysicalProfiles)
        {
            log?.Warning("PhaseVisualizer config profile not found. Using embedded defaults.");
        }
        else if (selectedProfile.HasPhysicalFile)
        {
            log?.Warning(
                "PhaseVisualizer selected config profile {Profile} failed to load from {Path}. Using embedded defaults.",
                selectedProfile.Key,
                selectedProfile.FilePath);
        }

        return new PhaseConfigLoadResult(
            _validator.Validate(defaults, log),
            effectiveCatalog,
            effectiveProfile,
            selection.ProbePaths,
            ResolveEffectiveConfigDirectory(modelConfigDirectory, selection));
    }

    public ConfigResolutionInfo ResolveConfigResolution(string modelConfigDirectory)
    {
        return ResolveConfigResolution(modelConfigDirectory, selectedProfileKey: null, rememberedProfileKey: null);
    }

    public ConfigResolutionInfo ResolveConfigResolution(
        string modelConfigDirectory,
        string? selectedProfileKey,
        string? rememberedProfileKey)
    {
        var selection = ResolveSelection(modelConfigDirectory, selectedProfileKey, rememberedProfileKey);
        return new ConfigResolutionInfo(
            selection.SelectedProfile.FilePath,
            selection.SelectedProfile.SourceName,
            selection.ProbePaths,
            selection.SelectedProfile.FileName,
            selection.SelectedProfile.Key,
            selection.SelectedProfile.DisplayName);
    }

    public string ResolveEffectiveConfigDirectory(string modelConfigDirectory)
    {
        return ResolveEffectiveConfigDirectory(
            modelConfigDirectory,
            selectedProfileKey: null,
            rememberedProfileKey: null);
    }

    public string ResolveEffectiveConfigDirectory(
        string modelConfigDirectory,
        string? selectedProfileKey,
        string? rememberedProfileKey)
    {
        var selection = ResolveSelection(modelConfigDirectory, selectedProfileKey, rememberedProfileKey);
        return ResolveEffectiveConfigDirectory(modelConfigDirectory, selection);
    }

    private string ResolveEffectiveConfigDirectory(
        string modelConfigDirectory,
        PhaseConfigProfileSelection selection)
    {
        var selectedProfile = selection.SelectedProfile;
        if (selectedProfile.HasPhysicalFile)
        {
            return Path.GetDirectoryName(selectedProfile.FilePath) ?? string.Empty;
        }

        var preferredModelConfigDirectory = ResolvePreferredConfigDirectoryFromRootOrConfigDirectory(modelConfigDirectory);
        return preferredModelConfigDirectory ?? string.Empty;
    }

    private PhaseConfigProfileCatalog BuildCatalog(string modelConfigDirectory)
    {
        var profilesByKey = new Dictionary<string, PhaseConfigProfileDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateSourceDirectories(modelConfigDirectory))
        {
            foreach (var descriptor in DiscoverProfilesInDirectory(candidate.ConfigDirectory, candidate.SourceKind))
            {
                if (!profilesByKey.ContainsKey(descriptor.Key))
                {
                    profilesByKey[descriptor.Key] = descriptor;
                }
            }
        }

        return new PhaseConfigProfileCatalog(
            profilesByKey.Values
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static IEnumerable<PhaseConfigProfileDescriptor> DiscoverProfilesInDirectory(
        string? configDirectory,
        PhaseConfigProfileSourceKind sourceKind)
    {
        if (string.IsNullOrWhiteSpace(configDirectory) || !Directory.Exists(configDirectory))
        {
            yield break;
        }

        var explicitProfiles = new Dictionary<string, PhaseConfigProfileDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in Directory.EnumerateFiles(configDirectory, "*" + PhaseConfigPaths.ConfigProfileFileSuffix))
        {
            var fileName = Path.GetFileName(filePath);
            if (!PhaseConfigPaths.TryExtractProfileKeyFromFileName(fileName, out var profileKey))
            {
                continue;
            }

            explicitProfiles[profileKey] = new PhaseConfigProfileDescriptor(
                profileKey,
                profileKey,
                fileName ?? PhaseConfigPaths.BuildConfigFileName(profileKey),
                filePath,
                sourceKind);
        }

        foreach (var descriptor in explicitProfiles.Values
            .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            yield return descriptor;
        }

        var legacyDefaultPath = Path.Combine(configDirectory, PhaseConfigPaths.LegacyConfigFileName);
        if (File.Exists(legacyDefaultPath)
            && !explicitProfiles.ContainsKey(PhaseConfigPaths.DefaultProfileKey))
        {
            yield return new PhaseConfigProfileDescriptor(
                PhaseConfigPaths.DefaultProfileKey,
                PhaseConfigPaths.DefaultProfileKey,
                PhaseConfigPaths.LegacyConfigFileName,
                legacyDefaultPath,
                sourceKind);
        }
    }

    private IEnumerable<(string ConfigDirectory, PhaseConfigProfileSourceKind SourceKind)> EnumerateSourceDirectories(
        string modelConfigDirectory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configDirectory in BuildConfigDirectoriesFromRootOrConfigDirectory(modelConfigDirectory))
        {
            var normalized = NormalizePath(configDirectory);
            if (!string.IsNullOrWhiteSpace(normalized) && seen.Add(normalized))
            {
                yield return (configDirectory, PhaseConfigProfileSourceKind.Model);
            }
        }

        foreach (var firmRootDirectory in _rootProvider.GetFirmRootDirectories())
        {
            foreach (var configDirectory in BuildConfigDirectoriesFromRootOrConfigDirectory(firmRootDirectory))
            {
                var normalized = NormalizePath(configDirectory);
                if (!string.IsNullOrWhiteSpace(normalized) && seen.Add(normalized))
                {
                    yield return (configDirectory, PhaseConfigProfileSourceKind.Firm);
                }
            }
        }
    }

    private IReadOnlyList<string> BuildProbePaths(string modelConfigDirectory)
    {
        var probePaths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateSourceDirectories(modelConfigDirectory))
        {
            foreach (var filePath in Directory.Exists(candidate.ConfigDirectory)
                ? Directory.EnumerateFiles(candidate.ConfigDirectory, "*.json")
                : Array.Empty<string>())
            {
                var normalized = NormalizePath(filePath);
                if (seen.Add(normalized))
                {
                    probePaths.Add(filePath);
                }
            }

            var legacyDefaultPath = Path.Combine(candidate.ConfigDirectory, PhaseConfigPaths.LegacyConfigFileName);
            var normalizedLegacyDefaultPath = NormalizePath(legacyDefaultPath);
            if (seen.Add(normalizedLegacyDefaultPath))
            {
                probePaths.Add(legacyDefaultPath);
            }
        }

        return probePaths;
    }

    private static PhaseConfigProfileDescriptor SelectProfile(
        PhaseConfigProfileCatalog catalog,
        string? selectedProfileKey,
        string? rememberedProfileKey)
    {
        var selectedProfile = catalog.FindByKey(selectedProfileKey)
            ?? catalog.FindByKey(rememberedProfileKey)
            ?? catalog.FindByKey(PhaseConfigPaths.DefaultProfileKey)
            ?? catalog.Profiles.FirstOrDefault();

        return selectedProfile ?? new PhaseConfigProfileDescriptor(
            PhaseConfigPaths.DefaultProfileKey,
            PhaseConfigPaths.DefaultProfileKey,
            PhaseConfigPaths.BuildConfigFileName(PhaseConfigPaths.DefaultProfileKey),
            string.Empty,
            PhaseConfigProfileSourceKind.EmbeddedDefaults);
    }

    private static IEnumerable<string> BuildConfigDirectoriesFromRootOrConfigDirectory(string? rootOrConfigDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootOrConfigDirectory))
        {
            yield break;
        }

        var normalizedPath = NormalizePath(rootOrConfigDirectory);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            yield break;
        }

        var rootDirectory = ResolveRootDirectoryFromRootOrConfigDirectory(normalizedPath);
        var configDirectory = PhaseConfigPaths.BuildConfigDirectoryPathFromRootDirectory(rootDirectory);
        if (!string.IsNullOrWhiteSpace(configDirectory))
        {
            yield return configDirectory!;
        }
    }

    private static string? ResolvePreferredConfigDirectoryFromRootOrConfigDirectory(string? rootOrConfigDirectory)
    {
        return BuildConfigDirectoriesFromRootOrConfigDirectory(rootOrConfigDirectory).FirstOrDefault();
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

            createdConfigPath = Path.Combine(
                configDirectory,
                PhaseConfigPaths.BuildConfigFileName(PhaseConfigPaths.DefaultProfileKey));
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
            log?.Warning(ex, "PhaseVisualizer failed to create default model config profile.");
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
            string configFileName,
            string profileKey,
            string profileDisplayName)
        {
            EffectiveConfigPath = effectiveConfigPath ?? string.Empty;
            SourceName = sourceName ?? string.Empty;
            ProbePaths = probePaths ?? Array.Empty<string>();
            ConfigFileName = configFileName ?? string.Empty;
            ProfileKey = profileKey ?? string.Empty;
            ProfileDisplayName = profileDisplayName ?? string.Empty;
        }

        public string EffectiveConfigPath { get; }
        public string SourceName { get; }
        public IReadOnlyList<string> ProbePaths { get; }
        public string ConfigFileName { get; }
        public string ProfileKey { get; }
        public string ProfileDisplayName { get; }
    }
}
