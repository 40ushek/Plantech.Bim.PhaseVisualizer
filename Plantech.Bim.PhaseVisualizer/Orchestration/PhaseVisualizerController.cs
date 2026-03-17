using Plantech.Bim.PhaseVisualizer.Common;
using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Contracts;
using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Services;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Tekla.Structures;

namespace Plantech.Bim.PhaseVisualizer.Orchestration;

internal sealed class PhaseVisualizerController
{
    private readonly PhaseTableConfigLoader _configProvider;
    private readonly TeklaPhaseDataProvider _dataProvider;
    private readonly PhaseTableBuilder _tableBuilder;
    private readonly IPhaseActionExecutor _actionExecutor;
    private readonly PhaseRuntimeSelectionResolver _runtimeSelectionResolver;
    private readonly object _pathDiagnosticsSync = new();
    private bool _pathDiagnosticsLogged;

    public PhaseVisualizerController()
        : this(
            new PhaseTableConfigLoader(),
            new TeklaPhaseDataProvider(),
            new PhaseTableBuilder(),
            new PhaseActionExecutor(),
            null)
    {
    }

    public PhaseVisualizerController(
        PhaseTableConfigLoader configProvider,
        TeklaPhaseDataProvider dataProvider,
        PhaseTableBuilder tableBuilder,
        IPhaseActionExecutor actionExecutor)
        : this(
            configProvider,
            dataProvider,
            tableBuilder,
            actionExecutor,
            null)
    {
    }

    internal PhaseVisualizerController(
        PhaseTableConfigLoader configProvider,
        TeklaPhaseDataProvider dataProvider,
        PhaseTableBuilder tableBuilder,
        IPhaseActionExecutor actionExecutor,
        PhaseRuntimeSelectionResolver? runtimeSelectionResolver)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _tableBuilder = tableBuilder ?? throw new ArgumentNullException(nameof(tableBuilder));
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _runtimeSelectionResolver = runtimeSelectionResolver
            ?? new PhaseRuntimeSelectionResolver(
                _configProvider,
                new PhaseLocalUserStoragePathResolver(),
                new PhaseConfigProfileSessionStore());
    }

    public IPhaseActionExecutor ActionExecutor => _actionExecutor;

    public string ResolveStateFilePath(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        return ResolveRuntimeSelection(teklaContext, selectedProfileKey: null, selectedStateName: null, log).StateFilePath;
    }

    public string ResolveEffectiveConfigDirectory(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        var runtimeSelection = ResolveRuntimeSelection(teklaContext, selectedProfileKey: null, selectedStateName: null, log);
        return _configProvider.ResolveEffectiveConfigDirectory(
            runtimeSelection.ModelConfigDirectory,
            runtimeSelection.ProfileSelection.SelectedProfile.Key,
            rememberedProfileKey: null);
    }

    public void LogStartupDiagnostics(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        var contextPaths = ResolveContextPathsFromTekla(teklaContext, log);
        LogPathDiagnosticsOnce(contextPaths, selectedProfileKey: null, log);
    }

    internal PhaseRuntimeSelection ResolveRuntimeSelection(
        SynchronizationContext? teklaContext,
        string? selectedProfileKey,
        string? selectedStateName = null,
        ILogger? log = null)
    {
        var contextPaths = ResolveContextPathsFromTekla(teklaContext, log);
        return ResolveRuntimeSelection(contextPaths, selectedProfileKey, selectedStateName, log);
    }

    internal PhaseRuntimeSelection ResolveRuntimeSelection(
        string? modelConfigDirectory,
        string? selectedProfileKey,
        string? selectedStateName = null,
        ILogger? log = null)
    {
        var contextPaths = ResolveContextPathsFromConfigDirectory(modelConfigDirectory);
        return ResolveRuntimeSelection(contextPaths, selectedProfileKey, selectedStateName, log);
    }

    public PhaseVisualizerContext LoadContext(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        return LoadContext(
            teklaContext,
            includeAllPhases: false,
            searchScope: PhaseSearchScope.TeklaModel,
            showAllPhases: false,
            showObjectCountInStatus: true,
            selectedProfileKey: null,
            log: log);
    }

    public PhaseVisualizerContext LoadContext(
        SynchronizationContext? teklaContext,
        bool includeAllPhases,
        bool useVisibleViewsForSearch = false,
        ILogger? log = null)
    {
        var searchScope = PhaseSearchScopeMapper.FromUseVisibleViewsFlag(useVisibleViewsForSearch);
        return LoadContext(
            teklaContext,
            includeAllPhases,
            searchScope,
            showAllPhases: includeAllPhases,
            showObjectCountInStatus: true,
            selectedProfileKey: null,
            log: log);
    }

    public PhaseVisualizerContext LoadContext(
        SynchronizationContext? teklaContext,
        bool includeAllPhases,
        PhaseSearchScope searchScope,
        bool showAllPhases,
        bool showObjectCountInStatus,
        string? selectedProfileKey = null,
        string? selectedStateName = null,
        ILogger? log = null)
    {
        var contextPaths = ResolveContextPathsFromTekla(teklaContext, log);
        return LoadContextCore(
            contextPaths,
            teklaContext,
            includeAllPhases,
            searchScope,
            showAllPhases,
            showObjectCountInStatus,
            selectedProfileKey,
            selectedStateName,
            log);
    }

    public PhaseVisualizerContext LoadContext(
        string? modelConfigDirectory,
        SynchronizationContext? teklaContext,
        bool includeAllPhases,
        bool useVisibleViewsForSearch = false,
        ILogger? log = null)
    {
        var searchScope = PhaseSearchScopeMapper.FromUseVisibleViewsFlag(useVisibleViewsForSearch);
        return LoadContext(
            modelConfigDirectory,
            teklaContext,
            includeAllPhases,
            searchScope,
            showAllPhases: includeAllPhases,
            showObjectCountInStatus: true,
            selectedProfileKey: null,
            log: log);
    }

    public PhaseVisualizerContext LoadContext(
        string? modelConfigDirectory,
        SynchronizationContext? teklaContext,
        bool includeAllPhases,
        PhaseSearchScope searchScope,
        bool showAllPhases,
        bool showObjectCountInStatus,
        string? selectedProfileKey = null,
        string? selectedStateName = null,
        ILogger? log = null)
    {
        var contextPaths = ResolveContextPathsFromConfigDirectory(modelConfigDirectory);
        return LoadContextCore(
            contextPaths,
            teklaContext,
            includeAllPhases,
            searchScope,
            showAllPhases,
            showObjectCountInStatus,
            selectedProfileKey,
            selectedStateName,
            log);
    }

    private PhaseVisualizerContext LoadContextCore(
        ContextPaths contextPaths,
        SynchronizationContext? teklaContext,
        bool includeAllPhases,
        PhaseSearchScope searchScope,
        bool showAllPhases,
        bool showObjectCountInStatus,
        string? selectedProfileKey,
        string? selectedStateName,
        ILogger? log = null)
    {
        var runtimeSelection = ResolveRuntimeSelection(contextPaths, selectedProfileKey, selectedStateName, log);
        LogPathDiagnosticsOnce(contextPaths, runtimeSelection.ProfileSelection.SelectedProfile.Key, log);

        var configLoad = _configProvider.LoadResolved(
            runtimeSelection.ModelConfigDirectory,
            runtimeSelection.ProfileSelection,
            log);
        var effectiveConfigDirectory = configLoad.EffectiveConfigDirectory;
        var config = configLoad.Config;
        var includePhaseObjectCounts = ShouldIncludePhaseObjectCounts(
            config,
            includeAllPhases,
            showAllPhases,
            showObjectCountInStatus);
        var snapshot = _dataProvider.LoadPhaseSnapshot(
            teklaContext,
            config.Columns,
            includeAllPhases,
            searchScope,
            includePhaseObjectCounts,
            log);
        var rows = _tableBuilder.BuildRows(snapshot, config, log);
        var objectCount = includePhaseObjectCounts && snapshot.PhaseObjectCounts.Count > 0
            ? snapshot.PhaseObjectCounts.Values.Sum()
            : 0;

        return new PhaseVisualizerContext
        {
            Config = config,
            Rows = rows,
            StateFilePath = runtimeSelection.StateFilePath,
            ActiveStateName = runtimeSelection.SelectedStateName,
            ConfigPath = configLoad.EffectiveConfigPath,
            ConfigFingerprint = PhaseConfigFingerprint.ComputeFromFile(configLoad.EffectiveConfigPath),
            ConfigSource = configLoad.SourceName,
            LogPath = PhaseVisualizerLogConfigurator.ResolveLogPath(effectiveConfigDirectory),
            ConfigProfiles = configLoad.Catalog.Profiles,
            StateNames = runtimeSelection.StateNames,
            ActiveProfile = configLoad.SelectedProfile,
            SnapshotMeta = new PhaseSnapshotMeta
            {
                CreatedAtUtc = snapshot.CreatedAtUtc,
                ObjectCount = objectCount,
                RowCount = rows.Count,
            },
        };
    }

    private static bool ShouldIncludePhaseObjectCounts(
        PhaseTableConfig config,
        bool includeAllPhases,
        bool showAllPhases,
        bool showObjectCountInStatus)
    {
        return showObjectCountInStatus;
    }

    private void LogPathDiagnosticsOnce(ContextPaths contextPaths, string? selectedProfileKey, ILogger? log)
    {
        if (log == null)
        {
            return;
        }

        lock (_pathDiagnosticsSync)
        {
            if (_pathDiagnosticsLogged)
            {
                return;
            }

            _pathDiagnosticsLogged = true;
        }

        var modelPath = contextPaths.ModelPath;
        var firmPath = ResolveFirmPath();
        var environmentPath = ResolveEnvironmentPath();
        var runtimeSelection = ResolveRuntimeSelection(contextPaths, selectedProfileKey, selectedStateName: null, log);
        var configResolution = _configProvider.ResolveConfigResolution(
            contextPaths.ModelConfigDirectory,
            runtimeSelection.ProfileSelection.SelectedProfile.Key,
            rememberedProfileKey: null);
        var effectiveConfigPath = string.IsNullOrWhiteSpace(configResolution.EffectiveConfigPath)
            ? "<embedded-defaults>"
            : configResolution.EffectiveConfigPath;

        log.Information("PhaseVisualizer startup diagnostics:");
        log.Information("  Model={ModelPath}", FormatPathForLog(modelPath));
        log.Information("  Firm={FirmPath}", FormatPathForLog(firmPath));
        log.Information("  Environment={EnvironmentPath}", FormatPathForLog(environmentPath));
        log.Information("  ConfigProfile={ConfigProfile}", configResolution.ProfileDisplayName);
        log.Information("  ConfigFile={ConfigFileName}", configResolution.ConfigFileName);
        log.Information("  ConfigPath={ConfigPath}", effectiveConfigPath);
        log.Information("  ConfigSource={ConfigSource}", configResolution.SourceName);

        if (configResolution.ProbePaths.Count == 0)
        {
            log.Information("  ConfigProbePath=<none>");
            return;
        }

        for (var i = 0; i < configResolution.ProbePaths.Count; i++)
        {
            log.Information(
                "  ConfigProbePath[{Index}]={ProbePath}",
                i + 1,
                configResolution.ProbePaths[i]);
        }
    }

    private static string ResolveFirmPath()
    {
        try
        {
            var raw = string.Empty;
            TeklaStructuresSettings.GetAdvancedOption("XS_FIRM", ref raw);
            return ResolveFirstPathToken(raw);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveEnvironmentPath()
    {
        try
        {
            var raw = string.Empty;
            TeklaStructuresSettings.GetAdvancedOption("XS_SYSTEM", ref raw);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = Regex.Replace(raw, @"[\\/]+", "\\");
            var match = Regex.Match(
                normalized,
                @"(.*?\\environments\\[^\\;]+)",
                RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim().TrimEnd('\\');
            }

            return ResolveFirstPathToken(raw);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveFirstPathToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        foreach (var token in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var path = token.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static string FormatPathForLog(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? "<empty>" : path!;
    }

    private static ContextPaths ResolveContextPathsFromTekla(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        var modelPath = ResolveModelPath(teklaContext, log);
        return ContextPaths.FromModelPath(modelPath);
    }

    private static ContextPaths ResolveContextPathsFromConfigDirectory(string? modelConfigDirectory)
    {
        var safeModelConfigDirectory = modelConfigDirectory ?? string.Empty;
        var modelPath = ResolveModelPathFromConfigDirectory(safeModelConfigDirectory);
        return new ContextPaths(modelPath, safeModelConfigDirectory);
    }

    private static string ResolveModelPath(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        var modelPath = TeklaContextDispatcher.Run(teklaContext, () =>
        {
            var model = LazyModelConnector.ModelInstance;
            var info = model?.GetInfo();
            return info?.ModelPath;
        },
        log,
        noContextWarning: "PhaseVisualizer model path resolve runs without Tekla synchronization context.",
        sendFailedWarning: "PhaseVisualizer model path resolve on Tekla context failed. Falling back to direct call.");

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return string.Empty;
        }

        return modelPath ?? string.Empty;
    }

    private static string BuildModelConfigDirectory(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return string.Empty;
        }

        return Path.Combine(modelPath, PhaseConfigPaths.ConfigDirectoryName);
    }

    private static string ResolveModelPathFromConfigDirectory(string? modelConfigDirectory)
    {
        if (string.IsNullOrWhiteSpace(modelConfigDirectory))
        {
            return string.Empty;
        }

        try
        {
            var normalized = Path.GetFullPath(modelConfigDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (TryResolveFromPlantechConfigDirectory(normalized, out var modelPathFromPlantech))
            {
                return EnsureExistingDirectory(modelPathFromPlantech);
            }

            // Legacy fallback for old external callers still passing <model>\.mpd\menu.
            if (TryResolveFromLegacyMenuDirectory(normalized, out var modelPathFromLegacyMenu))
            {
                return EnsureExistingDirectory(modelPathFromLegacyMenu);
            }

            return EnsureExistingDirectory(normalized);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool TryResolveFromPlantechConfigDirectory(string normalizedConfigDirectory, out string modelPath)
    {
        modelPath = string.Empty;
        var leaf = Path.GetFileName(normalizedConfigDirectory);
        if (!PhaseConfigPaths.IsConfigDirectoryName(leaf))
        {
            return false;
        }

        modelPath = Path.GetFullPath(Path.Combine(normalizedConfigDirectory, ".."));
        return true;
    }

    private static bool TryResolveFromLegacyMenuDirectory(string normalizedConfigDirectory, out string modelPath)
    {
        modelPath = string.Empty;
        var leaf = Path.GetFileName(normalizedConfigDirectory);
        if (!leaf.Equals("menu", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var maybeMpdPath = Path.GetFullPath(Path.Combine(normalizedConfigDirectory, ".."));
        if (!Path.GetFileName(maybeMpdPath).Equals(".mpd", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        modelPath = Path.GetFullPath(Path.Combine(maybeMpdPath, ".."));
        return true;
    }

    private static string EnsureExistingDirectory(string path)
    {
        return Directory.Exists(path) ? path : string.Empty;
    }

    private PhaseRuntimeSelection ResolveRuntimeSelection(
        ContextPaths contextPaths,
        string? selectedProfileKey,
        string? selectedStateName,
        ILogger? log)
    {
        return _runtimeSelectionResolver.Resolve(
            contextPaths.ModelPath,
            contextPaths.ModelConfigDirectory,
            selectedProfileKey,
            selectedStateName,
            log);
    }

    private readonly struct ContextPaths
    {
        public ContextPaths(string modelPath, string modelConfigDirectory)
        {
            ModelPath = modelPath;
            ModelConfigDirectory = modelConfigDirectory;
        }

        public string ModelPath { get; }
        public string ModelConfigDirectory { get; }

        public static ContextPaths FromModelPath(string modelPath)
        {
            return new ContextPaths(modelPath, BuildModelConfigDirectory(modelPath));
        }
    }
}
