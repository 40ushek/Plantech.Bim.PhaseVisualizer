using Plantech.Bim.PhaseVisualizer.Configuration;
using Serilog;
using System;
using System.IO;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseRuntimeSelectionResolver
{
    private readonly PhaseTableConfigLoader _configLoader;
    private readonly PhaseLocalUserStoragePathResolver _localUserStoragePathResolver;
    private readonly PhaseConfigProfileSessionStore _sessionStore;

    public PhaseRuntimeSelectionResolver(
        PhaseTableConfigLoader configLoader,
        PhaseLocalUserStoragePathResolver localUserStoragePathResolver,
        PhaseConfigProfileSessionStore sessionStore)
    {
        _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
        _localUserStoragePathResolver = localUserStoragePathResolver ?? throw new ArgumentNullException(nameof(localUserStoragePathResolver));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    }

    public PhaseRuntimeSelection Resolve(
        string modelPath,
        string modelConfigDirectory,
        string? requestedProfileKey,
        ILogger? log = null)
    {
        var localUserStoragePaths = _localUserStoragePathResolver.ResolveBase(modelPath);
        var rememberedProfileKey = _sessionStore.LoadSelectedProfileKey(localUserStoragePaths.SessionFilePath, log);
        var profileSelection = _configLoader.ResolveSelection(
            modelConfigDirectory,
            requestedProfileKey,
            rememberedProfileKey);

        var stateFilePath = ResolveColocatedStateFilePath(modelConfigDirectory, profileSelection);
        var configFingerprint = PhaseConfigFingerprint.ComputeFromFile(profileSelection.SelectedProfile.FilePath);
        _sessionStore.SaveSelectedProfileKey(
            localUserStoragePaths.SessionFilePath,
            profileSelection.SelectedProfile.Key,
            log);

        return new PhaseRuntimeSelection(
            modelPath,
            modelConfigDirectory,
            localUserStoragePaths,
            stateFilePath,
            configFingerprint,
            profileSelection);
    }

    internal string ResolveColocatedStateFilePath(
        string modelConfigDirectory,
        PhaseConfigProfileSelection profileSelection)
    {
        if (profileSelection == null)
        {
            throw new ArgumentNullException(nameof(profileSelection));
        }

        var selectedProfile = profileSelection.SelectedProfile;
        var configDirectory = selectedProfile.HasPhysicalFile
            ? Path.GetDirectoryName(selectedProfile.FilePath)
            : _configLoader.ResolveEffectiveConfigDirectory(
                modelConfigDirectory,
                selectedProfile.Key,
                rememberedProfileKey: null);

        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return string.Empty;
        }

        var profileKey = string.IsNullOrWhiteSpace(selectedProfile.Key)
            ? PhaseConfigPaths.DefaultProfileKey
            : selectedProfile.Key;
        return Path.Combine(configDirectory, $"state.{profileKey}.json");
    }
}

internal sealed class PhaseRuntimeSelection
{
    public PhaseRuntimeSelection(
        string modelPath,
        string modelConfigDirectory,
        PhaseLocalUserStoragePaths localUserStoragePaths,
        string stateFilePath,
        string configFingerprint,
        PhaseConfigProfileSelection profileSelection)
    {
        ModelPath = modelPath ?? string.Empty;
        ModelConfigDirectory = modelConfigDirectory ?? string.Empty;
        LocalUserStoragePaths = localUserStoragePaths ?? throw new ArgumentNullException(nameof(localUserStoragePaths));
        StateFilePath = stateFilePath ?? string.Empty;
        ConfigFingerprint = configFingerprint ?? string.Empty;
        ProfileSelection = profileSelection ?? throw new ArgumentNullException(nameof(profileSelection));
    }

    public string ModelPath { get; }

    public string ModelConfigDirectory { get; }

    public PhaseLocalUserStoragePaths LocalUserStoragePaths { get; }

    public string StateFilePath { get; }

    public string ConfigFingerprint { get; }

    public PhaseConfigProfileSelection ProfileSelection { get; }
}
