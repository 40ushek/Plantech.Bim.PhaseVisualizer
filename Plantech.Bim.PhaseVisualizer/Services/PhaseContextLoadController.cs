using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Orchestration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseContextLoadController
{
    private readonly PhaseVisualizerController _controller;
    private readonly SynchronizationContext? _teklaContext;
    private readonly ILogger _log;
    private readonly Dictionary<ContextCacheKey, PhaseVisualizerContext> _cachedAllPhasesContexts = new();

    public PhaseContextLoadController(
        PhaseVisualizerController controller,
        SynchronizationContext? teklaContext,
        ILogger log)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _teklaContext = teklaContext;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public string ResolveStateFilePath()
    {
        return _controller.ResolveStateFilePath(_teklaContext, _log) ?? string.Empty;
    }

    public PhaseContextLoadResult Resolve(
        bool forceReloadFromModel,
        bool showAllPhases,
        PhaseSearchScope searchScope,
        bool showObjectCountInStatus,
        string? stateFilePath)
    {
        var nextStateFilePath = stateFilePath ?? string.Empty;
        var currentStateFilePath = _controller.ResolveStateFilePath(_teklaContext, _log) ?? string.Empty;
        var hasStateFilePathChanged = false;

        if (!string.IsNullOrWhiteSpace(currentStateFilePath))
        {
            if (!string.IsNullOrWhiteSpace(nextStateFilePath)
                && !string.Equals(nextStateFilePath, currentStateFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _cachedAllPhasesContexts.Clear();
                hasStateFilePathChanged = true;
            }

            nextStateFilePath = currentStateFilePath;
        }

        var cacheKey = new ContextCacheKey(searchScope, showAllPhases, showObjectCountInStatus);
        if (forceReloadFromModel || !_cachedAllPhasesContexts.TryGetValue(cacheKey, out var context))
        {
            context = _controller.LoadContext(
                teklaContext: _teklaContext,
                includeAllPhases: true,
                searchScope: searchScope,
                showAllPhases: showAllPhases,
                showObjectCountInStatus: showObjectCountInStatus,
                log: _log);
            _cachedAllPhasesContexts[cacheKey] = context;
        }

        return new PhaseContextLoadResult(
            context ?? new PhaseVisualizerContext(),
            nextStateFilePath,
            hasStateFilePathChanged);
    }
}

internal readonly struct ContextCacheKey : IEquatable<ContextCacheKey>
{
    public ContextCacheKey(PhaseSearchScope searchScope, bool showAllPhases, bool showObjectCountInStatus)
    {
        SearchScope = searchScope;
        ShowAllPhases = showAllPhases;
        ShowObjectCountInStatus = showObjectCountInStatus;
    }

    public PhaseSearchScope SearchScope { get; }

    public bool ShowAllPhases { get; }

    public bool ShowObjectCountInStatus { get; }

    public bool Equals(ContextCacheKey other)
    {
        return SearchScope == other.SearchScope
            && ShowAllPhases == other.ShowAllPhases
            && ShowObjectCountInStatus == other.ShowObjectCountInStatus;
    }

    public override bool Equals(object? obj)
    {
        return obj is ContextCacheKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)SearchScope;
            hashCode = (hashCode * 397) ^ ShowAllPhases.GetHashCode();
            hashCode = (hashCode * 397) ^ ShowObjectCountInStatus.GetHashCode();
            return hashCode;
        }
    }
}

internal sealed class PhaseContextLoadResult
{
    public PhaseContextLoadResult(
        PhaseVisualizerContext context,
        string stateFilePath,
        bool hasStateFilePathChanged)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        StateFilePath = stateFilePath ?? string.Empty;
        HasStateFilePathChanged = hasStateFilePathChanged;
    }

    public PhaseVisualizerContext Context { get; }

    public string StateFilePath { get; }

    public bool HasStateFilePathChanged { get; }
}
