using Plantech.Bim.PhaseVisualizer.Common;
using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Contracts;
using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Services;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer.Orchestration;

internal sealed class PhaseVisualizerController
{
    private const string ConfigDirectoryName = ".plantech";
    private const string StateFileName = "phase-visualizer.state.json";

    private readonly PhaseTableConfigLoader _configProvider;
    private readonly TeklaPhaseDataProvider _dataProvider;
    private readonly PhaseTableBuilder _tableBuilder;
    private readonly IPhaseActionExecutor _actionExecutor;

    public PhaseVisualizerController()
        : this(
            new PhaseTableConfigLoader(),
            new TeklaPhaseDataProvider(),
            new PhaseTableBuilder(),
            new PhaseActionExecutor())
    {
    }

    public PhaseVisualizerController(
        PhaseTableConfigLoader configProvider,
        TeklaPhaseDataProvider dataProvider,
        PhaseTableBuilder tableBuilder,
        IPhaseActionExecutor actionExecutor)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _tableBuilder = tableBuilder ?? throw new ArgumentNullException(nameof(tableBuilder));
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
    }

    public IPhaseActionExecutor ActionExecutor => _actionExecutor;

    public string ResolveStateFilePath(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        return ResolveContextPathsFromTekla(teklaContext, log).StateFilePath;
    }

    public string ResolveEffectiveConfigDirectory(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        var contextPaths = ResolveContextPathsFromTekla(teklaContext, log);
        return _configProvider.ResolveEffectiveConfigDirectory(contextPaths.ModelConfigDirectory);
    }

    public PhaseVisualizerContext LoadContext(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        return LoadContext(
            teklaContext,
            includeAllPhases: false,
            searchScope: PhaseSearchScope.TeklaModel,
            showAllPhases: false,
            showObjectCountInStatus: true,
            log);
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
            log);
    }

    public PhaseVisualizerContext LoadContext(
        SynchronizationContext? teklaContext,
        bool includeAllPhases,
        PhaseSearchScope searchScope,
        bool showAllPhases,
        bool showObjectCountInStatus,
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
            log);
    }

    public PhaseVisualizerContext LoadContext(
        string? modelConfigDirectory,
        SynchronizationContext? teklaContext,
        bool includeAllPhases,
        PhaseSearchScope searchScope,
        bool showAllPhases,
        bool showObjectCountInStatus,
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
            log);
    }

    private PhaseVisualizerContext LoadContextCore(
        ContextPaths contextPaths,
        SynchronizationContext? teklaContext,
        bool includeAllPhases,
        PhaseSearchScope searchScope,
        bool showAllPhases,
        bool showObjectCountInStatus,
        ILogger? log = null)
    {
        var config = _configProvider.Load(contextPaths.ModelConfigDirectory, log);
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
            StateFilePath = contextPaths.StateFilePath,
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

    private static ContextPaths ResolveContextPathsFromTekla(SynchronizationContext? teklaContext, ILogger? log = null)
    {
        var modelPath = ResolveModelPath(teklaContext, log);
        return ContextPaths.FromModelPath(modelPath);
    }

    private static ContextPaths ResolveContextPathsFromConfigDirectory(string? modelConfigDirectory)
    {
        var safeModelConfigDirectory = modelConfigDirectory ?? string.Empty;
        var modelPath = ResolveModelPathFromConfigDirectory(safeModelConfigDirectory);
        return new ContextPaths(
            safeModelConfigDirectory,
            BuildStateFilePath(modelPath));
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

        return Path.Combine(modelPath, ConfigDirectoryName);
    }

    private static string BuildStateFilePath(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return string.Empty;
        }

        return Path.Combine(modelPath, "attributes", StateFileName);
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
        if (!leaf.Equals(ConfigDirectoryName, StringComparison.OrdinalIgnoreCase))
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

    private readonly struct ContextPaths
    {
        public ContextPaths(string modelConfigDirectory, string stateFilePath)
        {
            ModelConfigDirectory = modelConfigDirectory;
            StateFilePath = stateFilePath;
        }

        public string ModelConfigDirectory { get; }
        public string StateFilePath { get; }

        public static ContextPaths FromModelPath(string modelPath)
        {
            return new ContextPaths(
                BuildModelConfigDirectory(modelPath),
                BuildStateFilePath(modelPath));
        }
    }
}
