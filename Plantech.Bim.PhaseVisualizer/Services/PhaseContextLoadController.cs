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
    private readonly Dictionary<PhaseSearchScope, PhaseVisualizerContext> _cachedAllPhasesContexts = new();

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
        PhaseSearchScope searchScope,
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

        if (forceReloadFromModel || !_cachedAllPhasesContexts.TryGetValue(searchScope, out var context))
        {
            context = _controller.LoadContext(
                _teklaContext,
                includeAllPhases: true,
                useVisibleViewsForSearch: PhaseSearchScopeMapper.ToUseVisibleViewsFlag(searchScope),
                _log);
            _cachedAllPhasesContexts[searchScope] = context;
        }

        return new PhaseContextLoadResult(
            context ?? new PhaseVisualizerContext(),
            nextStateFilePath,
            hasStateFilePathChanged);
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
