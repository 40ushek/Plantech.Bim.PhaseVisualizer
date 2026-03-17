using Plantech.Bim.PhaseVisualizer.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        string? requestedStateName,
        bool allowMissingRequestedStateName = false,
        ILogger? log = null)
    {
        var localUserStoragePaths = _localUserStoragePathResolver.ResolveBase(modelPath);
        var rememberedProfileKey = _sessionStore.LoadSelectedProfileKey(localUserStoragePaths.SessionFilePath, log);
        var profileSelection = _configLoader.ResolveSelection(
            modelConfigDirectory,
            requestedProfileKey,
            rememberedProfileKey);

        var stateNames = DiscoverStateNames(modelConfigDirectory, profileSelection);
        var selectedStateName = SelectStateName(requestedStateName, stateNames, allowMissingRequestedStateName);
        if (allowMissingRequestedStateName
            && !stateNames.Any(name => string.Equals(name, selectedStateName, StringComparison.OrdinalIgnoreCase)))
        {
            stateNames = stateNames
                .Append(selectedStateName)
                .OrderBy(name => string.Equals(name, "default", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        var stateFilePath = ResolveColocatedStateFilePath(modelConfigDirectory, profileSelection, selectedStateName);
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
            selectedStateName,
            stateNames,
            configFingerprint,
            profileSelection);
    }

    internal string ResolveColocatedStateFilePath(
        string modelConfigDirectory,
        PhaseConfigProfileSelection profileSelection,
        string? stateName)
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
        var normalizedStateName = NormalizeStateName(stateName);
        var stateFileName = string.Equals(normalizedStateName, "default", StringComparison.OrdinalIgnoreCase)
            ? $"state.{profileKey}.json"
            : $"state.{profileKey}.{normalizedStateName}.json";
        return Path.Combine(configDirectory, stateFileName);
    }

    internal IReadOnlyList<string> DiscoverStateNames(
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

        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "default",
        };

        if (string.IsNullOrWhiteSpace(configDirectory) || !Directory.Exists(configDirectory))
        {
            return stateNames.ToList();
        }

        var profileKey = string.IsNullOrWhiteSpace(selectedProfile.Key)
            ? PhaseConfigPaths.DefaultProfileKey
            : selectedProfile.Key;
        var defaultFileName = $"state.{profileKey}.json";
        var prefix = $"state.{profileKey}.";

        foreach (var filePath in Directory.EnumerateFiles(configDirectory, $"state.{profileKey}*.json"))
        {
            var fileName = Path.GetFileName(filePath);
            if (string.Equals(fileName, defaultFileName, StringComparison.OrdinalIgnoreCase))
            {
                stateNames.Add("default");
                continue;
            }

            if (string.IsNullOrWhiteSpace(fileName)
                || !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stateName = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - ".json".Length);
            stateName = NormalizeStateName(stateName);
            if (!string.IsNullOrWhiteSpace(stateName))
            {
                stateNames.Add(stateName);
            }
        }

        return stateNames
            .OrderBy(name => string.Equals(name, "default", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string NormalizeStateName(string? stateName)
    {
        var normalized = string.IsNullOrWhiteSpace(stateName)
            ? "default"
            : stateName!.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new char[normalized.Length];
        var count = 0;

        foreach (var character in normalized)
        {
            sanitized[count++] = Array.IndexOf(invalidChars, character) >= 0 ? '_' : character;
        }

        var result = new string(sanitized, 0, count).Trim();
        return string.IsNullOrWhiteSpace(result) ? "default" : result;
    }

    private static string SelectStateName(
        string? requestedStateName,
        IReadOnlyList<string> availableStateNames,
        bool allowMissingRequestedStateName)
    {
        var normalizedStateName = NormalizeStateName(requestedStateName);
        if (availableStateNames.Any(name => string.Equals(name, normalizedStateName, StringComparison.OrdinalIgnoreCase)))
        {
            return normalizedStateName;
        }

        if (allowMissingRequestedStateName)
        {
            return normalizedStateName;
        }

        return "default";
    }
}

internal sealed class PhaseRuntimeSelection
{
    public PhaseRuntimeSelection(
        string modelPath,
        string modelConfigDirectory,
        PhaseLocalUserStoragePaths localUserStoragePaths,
        string stateFilePath,
        string selectedStateName,
        IReadOnlyList<string> stateNames,
        string configFingerprint,
        PhaseConfigProfileSelection profileSelection)
    {
        ModelPath = modelPath ?? string.Empty;
        ModelConfigDirectory = modelConfigDirectory ?? string.Empty;
        LocalUserStoragePaths = localUserStoragePaths ?? throw new ArgumentNullException(nameof(localUserStoragePaths));
        StateFilePath = stateFilePath ?? string.Empty;
        SelectedStateName = selectedStateName ?? "default";
        StateNames = stateNames ?? Array.Empty<string>();
        ConfigFingerprint = configFingerprint ?? string.Empty;
        ProfileSelection = profileSelection ?? throw new ArgumentNullException(nameof(profileSelection));
    }

    public string ModelPath { get; }

    public string ModelConfigDirectory { get; }

    public PhaseLocalUserStoragePaths LocalUserStoragePaths { get; }

    public string StateFilePath { get; }

    public string SelectedStateName { get; }

    public IReadOnlyList<string> StateNames { get; }

    public string ConfigFingerprint { get; }

    public PhaseConfigProfileSelection ProfileSelection { get; }
}
