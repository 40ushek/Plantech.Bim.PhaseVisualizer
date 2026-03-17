using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Plantech.Bim.PhaseVisualizer.Tests;

public sealed class StateStorageBehaviorTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    [Fact]
    public void PhaseRuntimeSelectionResolver_ResolvesStateNextToModelConfig()
    {
        using var tempScope = new TempDirectoryScope();
        var localAppDataRoot = tempScope.CreateSubdirectory("local-app-data");
        var modelRoot = tempScope.CreateSubdirectory("model");
        WriteConfigFile(modelRoot, "seva.phase-visualizer.json");

        var resolver = CreateResolver(localAppDataRoot);
        var runtimeSelection = resolver.Resolve(
            modelRoot,
            Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName),
            requestedProfileKey: "seva");

        Assert.Equal(
            Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName, "state.seva.json"),
            runtimeSelection.StateFilePath);
        Assert.StartsWith(localAppDataRoot, runtimeSelection.LocalUserStoragePaths.SessionFilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PhaseRuntimeSelectionResolver_ResolvesStateNextToFirmConfig()
    {
        using var tempScope = new TempDirectoryScope();
        var localAppDataRoot = tempScope.CreateSubdirectory("local-app-data");
        var modelRoot = tempScope.CreateSubdirectory("model");
        var firmRoot = tempScope.CreateSubdirectory("firm");
        WriteConfigFile(firmRoot, "seva.phase-visualizer.json");

        var resolver = CreateResolver(localAppDataRoot, firmRoot);
        var runtimeSelection = resolver.Resolve(
            modelRoot,
            Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName),
            requestedProfileKey: "seva");

        Assert.Equal(
            Path.Combine(firmRoot, PhaseConfigPaths.ConfigDirectoryName, "state.seva.json"),
            runtimeSelection.StateFilePath);
        Assert.StartsWith(localAppDataRoot, runtimeSelection.LocalUserStoragePaths.SessionFilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PhaseRuntimeSelectionResolver_MapsLegacyConfigToStateDefaultJson()
    {
        using var tempScope = new TempDirectoryScope();
        var localAppDataRoot = tempScope.CreateSubdirectory("local-app-data");
        var modelRoot = tempScope.CreateSubdirectory("model");
        WriteConfigFile(modelRoot, PhaseConfigPaths.LegacyConfigFileName);

        var resolver = CreateResolver(localAppDataRoot);
        var runtimeSelection = resolver.Resolve(
            modelRoot,
            Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName),
            requestedProfileKey: null);

        Assert.Equal(
            Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName, "state.default.json"),
            runtimeSelection.StateFilePath);
    }

    [Fact]
    public void PhaseLoadWorkflowController_LoadPersistedState_IgnoresFingerprintMismatch()
    {
        using var tempScope = new TempDirectoryScope();
        var runtimeSelection = CreateRuntimeSelection(tempScope, out _, out _, out _);
        var stateController = new PhaseTableStateController(new PhaseTableStateStore());

        stateController.Save(
            runtimeSelection.StateFilePath,
            new PhaseTableState
            {
                Rows = new List<PhaseTableRowState>
                {
                    new() { PhaseNumber = 10, Selected = true },
                },
            },
            configFingerprint: "WRONG",
            Log);

        var persistedState = PhaseLoadWorkflowController.LoadPersistedState(
            runtimeSelection,
            runtimeSelection.StateFilePath,
            stateController,
            Log);

        Assert.Null(persistedState);
    }

    [Fact]
    public void PhaseLoadWorkflowController_LoadPersistedState_ReturnsNullWhenColocatedStateIsMissing()
    {
        using var tempScope = new TempDirectoryScope();
        var runtimeSelection = CreateRuntimeSelection(tempScope, out _, out _, out _);
        var stateController = new PhaseTableStateController(new PhaseTableStateStore());

        var persistedState = PhaseLoadWorkflowController.LoadPersistedState(
            runtimeSelection,
            runtimeSelection.StateFilePath,
            stateController,
            Log);

        Assert.Null(persistedState);
    }

    [Fact]
    public void PhaseLoadWorkflowController_LoadPersistedState_UsesOnlyColocatedState()
    {
        using var tempScope = new TempDirectoryScope();
        var runtimeSelection = CreateRuntimeSelection(tempScope, out _, out _, out _);
        var stateController = new PhaseTableStateController(new PhaseTableStateStore());

        stateController.Save(
            runtimeSelection.StateFilePath,
            new PhaseTableState
            {
                Rows = new List<PhaseTableRowState>
                {
                    new() { PhaseNumber = 30, Selected = true },
                },
            },
            runtimeSelection.ConfigFingerprint,
            Log);

        var persistedState = PhaseLoadWorkflowController.LoadPersistedState(
            runtimeSelection,
            runtimeSelection.StateFilePath,
            stateController,
            Log);

        Assert.NotNull(persistedState);
        Assert.Single(persistedState!.Rows);
        Assert.Equal(30, persistedState.Rows[0].PhaseNumber);
    }

    [Fact]
    public void PhaseContextLoadController_HasStateFilePathChanged_ReturnsTrueForDifferentProfiles()
    {
        Assert.True(PhaseContextLoadController.HasStateFilePathChanged(
            @"D:\model\PT_PhaseVisualizer\state.default.json",
            @"D:\model\PT_PhaseVisualizer\state.seva.json"));
        Assert.False(PhaseContextLoadController.HasStateFilePathChanged(
            @"D:\model\PT_PhaseVisualizer\state.default.json",
            @"D:\model\PT_PhaseVisualizer\state.default.json"));
    }

    private static PhaseRuntimeSelection CreateRuntimeSelection(
        TempDirectoryScope tempScope,
        out string localAppDataRoot,
        out string modelRoot,
        out PhaseRuntimeSelectionResolver resolver)
    {
        localAppDataRoot = tempScope.CreateSubdirectory("local-app-data");
        modelRoot = tempScope.CreateSubdirectory("model");
        WriteConfigFile(modelRoot, "default.phase-visualizer.json", "{ \"columns\": [] }");

        resolver = CreateResolver(localAppDataRoot);
        return resolver.Resolve(
            modelRoot,
            Path.Combine(modelRoot, PhaseConfigPaths.ConfigDirectoryName),
            requestedProfileKey: null);
    }

    private static PhaseRuntimeSelectionResolver CreateResolver(string localAppDataRoot, params string[] firmRoots)
    {
        return new PhaseRuntimeSelectionResolver(
            new PhaseTableConfigLoader(
                new PhaseTableConfigValidator(),
                new FakeConfigRootProvider(firmRoots)),
            new PhaseLocalUserStoragePathResolver(localAppDataRoot),
            new PhaseConfigProfileSessionStore());
    }

    private static void WriteConfigFile(string rootDirectory, string fileName, string? contents = null)
    {
        var configDirectory = Path.Combine(rootDirectory, PhaseConfigPaths.ConfigDirectoryName);
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(Path.Combine(configDirectory, fileName), contents ?? "{ \"columns\": [] }");
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
            Path.Combine(Path.GetTempPath(), "PhaseVisualizerStateStorageTests", Guid.NewGuid().ToString("N"));

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
