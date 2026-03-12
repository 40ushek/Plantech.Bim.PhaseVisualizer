using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal enum PhaseConfigProfileSourceKind
{
    Model,
    Firm,
    EmbeddedDefaults,
}

internal sealed class PhaseConfigProfileDescriptor
{
    public PhaseConfigProfileDescriptor(
        string key,
        string displayName,
        string fileName,
        string filePath,
        PhaseConfigProfileSourceKind sourceKind)
    {
        Key = key ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        FileName = fileName ?? string.Empty;
        FilePath = filePath ?? string.Empty;
        SourceKind = sourceKind;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string FileName { get; }

    public string FilePath { get; }

    public PhaseConfigProfileSourceKind SourceKind { get; }

    public bool HasPhysicalFile => !string.IsNullOrWhiteSpace(FilePath);

    public string SourceName => SourceKind switch
    {
        PhaseConfigProfileSourceKind.Model => "model",
        PhaseConfigProfileSourceKind.Firm => "firm",
        _ => "embedded-defaults",
    };
}

internal sealed class PhaseConfigProfileCatalog
{
    public PhaseConfigProfileCatalog(IReadOnlyList<PhaseConfigProfileDescriptor>? profiles)
    {
        Profiles = profiles ?? Array.Empty<PhaseConfigProfileDescriptor>();
    }

    public IReadOnlyList<PhaseConfigProfileDescriptor> Profiles { get; }

    public PhaseConfigProfileDescriptor? FindByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class PhaseConfigProfileSelection
{
    public PhaseConfigProfileSelection(
        PhaseConfigProfileCatalog catalog,
        PhaseConfigProfileDescriptor selectedProfile,
        IReadOnlyList<string>? probePaths)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        SelectedProfile = selectedProfile ?? throw new ArgumentNullException(nameof(selectedProfile));
        ProbePaths = probePaths ?? Array.Empty<string>();
    }

    public PhaseConfigProfileCatalog Catalog { get; }

    public PhaseConfigProfileDescriptor SelectedProfile { get; }

    public IReadOnlyList<string> ProbePaths { get; }
}

internal sealed class PhaseConfigLoadResult
{
    public PhaseConfigLoadResult(
        PhaseTableConfig config,
        PhaseConfigProfileCatalog catalog,
        PhaseConfigProfileDescriptor selectedProfile,
        IReadOnlyList<string>? probePaths,
        string effectiveConfigDirectory)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        SelectedProfile = selectedProfile ?? throw new ArgumentNullException(nameof(selectedProfile));
        ProbePaths = probePaths ?? Array.Empty<string>();
        EffectiveConfigDirectory = effectiveConfigDirectory ?? string.Empty;
    }

    public PhaseTableConfig Config { get; }

    public PhaseConfigProfileCatalog Catalog { get; }

    public PhaseConfigProfileDescriptor SelectedProfile { get; }

    public IReadOnlyList<string> ProbePaths { get; }

    public string EffectiveConfigPath => SelectedProfile.FilePath;

    public string EffectiveConfigDirectory { get; }

    public string SourceName => SelectedProfile.SourceName;

    public string ConfigFileName => SelectedProfile.FileName;
}
