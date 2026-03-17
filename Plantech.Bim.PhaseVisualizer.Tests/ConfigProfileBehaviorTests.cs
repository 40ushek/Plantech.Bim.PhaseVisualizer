using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Plantech.Bim.PhaseVisualizer.Tests;

public sealed class ConfigProfileBehaviorTests
{
    [Fact]
    public void PhaseTableConfigLoader_DiscoverProfiles_MergesModelAndFirm_WithModelPrecedence()
    {
        using var tempScope = new TempDirectoryScope();
        var modelRoot = tempScope.CreateSubdirectory("model");
        var firmRoot = tempScope.CreateSubdirectory("firm");

        WriteConfigFile(modelRoot, "default.phase-visualizer.json");
        WriteConfigFile(modelRoot, "production.phase-visualizer.json");
        WriteConfigFile(modelRoot, PhaseConfigPaths.LegacyConfigFileName);
        WriteConfigFile(firmRoot, "default.phase-visualizer.json");
        WriteConfigFile(firmRoot, "shop.phase-visualizer.json");

        var loader = CreateLoader(firmRoot);
        var modelConfigDirectory = Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName);

        var catalog = loader.DiscoverProfiles(modelConfigDirectory);

        Assert.Collection(
            catalog.Profiles,
            profile =>
            {
                Assert.Equal("default", profile.Key);
                Assert.Equal(PhaseConfigProfileSourceKind.Model, profile.SourceKind);
                Assert.EndsWith("default.phase-visualizer.json", profile.FilePath, StringComparison.OrdinalIgnoreCase);
            },
            profile =>
            {
                Assert.Equal("production", profile.Key);
                Assert.Equal(PhaseConfigProfileSourceKind.Model, profile.SourceKind);
            },
            profile =>
            {
                Assert.Equal("shop", profile.Key);
                Assert.Equal(PhaseConfigProfileSourceKind.Firm, profile.SourceKind);
            });
    }

    [Fact]
    public void PhaseTableConfigLoader_ResolveSelection_FallsBackFromMissingRememberedProfileToDefault()
    {
        using var tempScope = new TempDirectoryScope();
        var modelRoot = tempScope.CreateSubdirectory("model");

        WriteConfigFile(modelRoot, "default.phase-visualizer.json");
        WriteConfigFile(modelRoot, "production.phase-visualizer.json");

        var loader = CreateLoader();
        var modelConfigDirectory = Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName);

        var selection = loader.ResolveSelection(
            modelConfigDirectory,
            selectedProfileKey: null,
            rememberedProfileKey: "missing-profile");

        Assert.Equal("default", selection.SelectedProfile.Key);
        Assert.Equal("default", selection.SelectedProfile.DisplayName);
    }

    [Fact]
    public void PhaseLocalUserStoragePathResolver_UsesLocalSessionStorage()
    {
        using var tempScope = new TempDirectoryScope();
        var localAppDataRoot = tempScope.CreateSubdirectory("local-app-data");
        var modelRoot = tempScope.CreateSubdirectory("model");

        var resolver = new PhaseLocalUserStoragePathResolver(localAppDataRoot);
        var basePaths = resolver.ResolveBase(modelRoot);

        Assert.StartsWith(localAppDataRoot, basePaths.BaseDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("session.json", basePaths.SessionFilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PhaseRuntimeSelectionResolver_RemembersLastSelectedProfilePerUser()
    {
        using var tempScope = new TempDirectoryScope();
        var localAppDataRoot = tempScope.CreateSubdirectory("local-app-data");
        var modelRoot = tempScope.CreateSubdirectory("model");

        WriteConfigFile(modelRoot, "default.phase-visualizer.json");
        WriteConfigFile(modelRoot, "design.phase-visualizer.json");

        var loader = CreateLoader();
        var sessionStore = new PhaseConfigProfileSessionStore();
        var resolver = new PhaseRuntimeSelectionResolver(
            loader,
            new PhaseLocalUserStoragePathResolver(localAppDataRoot),
            sessionStore);
        var modelConfigDirectory = Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName);

        var selectedDesign = resolver.Resolve(
            modelRoot,
            modelConfigDirectory,
            requestedProfileKey: "design",
            requestedStateName: null);
        var rememberedSelection = resolver.Resolve(
            modelRoot,
            modelConfigDirectory,
            requestedProfileKey: null,
            requestedStateName: null);

        Assert.Equal("design", selectedDesign.ProfileSelection.SelectedProfile.Key);
        Assert.Equal("design", rememberedSelection.ProfileSelection.SelectedProfile.Key);
        Assert.EndsWith(
            Path.Combine(PhaseConfigPaths.ConfigDirectoryName, "state.design.json"),
            rememberedSelection.StateFilePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "design",
            sessionStore.LoadSelectedProfileKey(rememberedSelection.LocalUserStoragePaths.SessionFilePath));
    }

    private static PhaseTableConfigLoader CreateLoader(params string[] firmRoots)
    {
        return new PhaseTableConfigLoader(
            new PhaseTableConfigValidator(),
            new FakeConfigRootProvider(firmRoots));
    }

    private static void WriteConfigFile(string rootDirectory, string fileName)
    {
        var configDirectory = Path.Combine(rootDirectory, PhaseConfigPaths.ConfigDirectoryName);
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(Path.Combine(configDirectory, fileName), "{}");
    }

    private sealed class FakeConfigRootProvider : IPhaseConfigRootProvider
    {
        private readonly IReadOnlyList<string> _firmRoots;

        public FakeConfigRootProvider(params string[] firmRoots)
        {
            _firmRoots = firmRoots ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> GetFirmRootDirectories()
        {
            return _firmRoots;
        }
    }

    private sealed class TempDirectoryScope : IDisposable
    {
        private readonly string _rootDirectory =
            Path.Combine(Path.GetTempPath(), "PhaseVisualizerConfigProfileTests", Guid.NewGuid().ToString("N"));

        public TempDirectoryScope()
        {
            Directory.CreateDirectory(_rootDirectory);
        }

        public string CreateSubdirectory(string name)
        {
            var path = Path.Combine(_rootDirectory, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
    }
}
