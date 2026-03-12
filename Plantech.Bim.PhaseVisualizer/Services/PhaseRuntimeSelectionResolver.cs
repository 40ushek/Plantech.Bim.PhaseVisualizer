using Plantech.Bim.PhaseVisualizer.Configuration;
using Serilog;
using System;

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

        var stateFilePath = _localUserStoragePathResolver.ResolveStateFilePath(
            localUserStoragePaths,
            profileSelection.SelectedProfile.Key);
        _sessionStore.SaveSelectedProfileKey(
            localUserStoragePaths.SessionFilePath,
            profileSelection.SelectedProfile.Key,
            log);

        return new PhaseRuntimeSelection(
            modelPath,
            modelConfigDirectory,
            localUserStoragePaths,
            stateFilePath,
            profileSelection);
    }
}

internal sealed class PhaseRuntimeSelection
{
    public PhaseRuntimeSelection(
        string modelPath,
        string modelConfigDirectory,
        PhaseLocalUserStoragePaths localUserStoragePaths,
        string stateFilePath,
        PhaseConfigProfileSelection profileSelection)
    {
        ModelPath = modelPath ?? string.Empty;
        ModelConfigDirectory = modelConfigDirectory ?? string.Empty;
        LocalUserStoragePaths = localUserStoragePaths ?? throw new ArgumentNullException(nameof(localUserStoragePaths));
        StateFilePath = stateFilePath ?? string.Empty;
        ProfileSelection = profileSelection ?? throw new ArgumentNullException(nameof(profileSelection));
    }

    public string ModelPath { get; }

    public string ModelConfigDirectory { get; }

    public PhaseLocalUserStoragePaths LocalUserStoragePaths { get; }

    public string StateFilePath { get; }

    public PhaseConfigProfileSelection ProfileSelection { get; }
}
