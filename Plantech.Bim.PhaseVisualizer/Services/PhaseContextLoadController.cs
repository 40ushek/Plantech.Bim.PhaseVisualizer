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

    public PhaseRuntimeSelection ResolveRuntimeSelection(string? selectedProfileKey)
    {
        return _controller.ResolveRuntimeSelection(_teklaContext, selectedProfileKey, _log);
    }

    public PhaseContextLoadResult Resolve(
        PhaseRuntimeSelection runtimeSelection,
        bool forceReloadFromModel,
        bool showAllPhases,
        PhaseSearchScope searchScope,
        bool showObjectCountInStatus,
        string? stateFilePath)
    {
        if (runtimeSelection == null)
        {
            throw new ArgumentNullException(nameof(runtimeSelection));
        }

        var nextStateFilePath = stateFilePath ?? string.Empty;
        var currentStateFilePath = runtimeSelection.StateFilePath;
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

        var cacheKey = new ContextCacheKey(
            runtimeSelection.ProfileSelection.SelectedProfile.Key,
            searchScope,
            showAllPhases,
            showObjectCountInStatus);
        if (forceReloadFromModel || !_cachedAllPhasesContexts.TryGetValue(cacheKey, out var context))
        {
            context = _controller.LoadContext(
                teklaContext: _teklaContext,
                includeAllPhases: true,
                searchScope: searchScope,
                showAllPhases: showAllPhases,
                showObjectCountInStatus: showObjectCountInStatus,
                selectedProfileKey: runtimeSelection.ProfileSelection.SelectedProfile.Key,
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
    public ContextCacheKey(
        string profileKey,
        PhaseSearchScope searchScope,
        bool showAllPhases,
        bool showObjectCountInStatus)
    {
        ProfileKey = profileKey ?? string.Empty;
        SearchScope = searchScope;
        ShowAllPhases = showAllPhases;
        ShowObjectCountInStatus = showObjectCountInStatus;
    }

    public string ProfileKey { get; }

    public PhaseSearchScope SearchScope { get; }

    public bool ShowAllPhases { get; }

    public bool ShowObjectCountInStatus { get; }

    public bool Equals(ContextCacheKey other)
    {
        return string.Equals(ProfileKey, other.ProfileKey, StringComparison.OrdinalIgnoreCase)
            && SearchScope == other.SearchScope
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
            var hashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(ProfileKey);
            hashCode = (hashCode * 397) ^ (int)SearchScope;
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
