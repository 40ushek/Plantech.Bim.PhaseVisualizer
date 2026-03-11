using Plantech.Bim.Custom.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Tekla.Structures.Filtering;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;

namespace Plantech.Bim.Custom.Services;

internal sealed class TeklaFilterObjectMatcher
{
    private const string TeklaViewFilterExtension = ".SObjGrp";
    private const int MaxCacheSize = 1024;
    private static readonly TimeSpan HotCacheWindow = TimeSpan.FromSeconds(2);
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, ExpressionCacheEntry> ExpressionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, PathCacheEntry> PathCache = new(StringComparer.OrdinalIgnoreCase);

    public bool IsMatch(string filterName, string? modelPath, ModelObject modelObject)
    {
        return TryMatch(filterName, modelPath, modelObject, out _, out _);
    }

    public bool TryMatch(
        string filterName,
        string? modelPath,
        ModelObject modelObject,
        out string resolvedFilterPath,
        out bool usedExpressionCache)
    {
        resolvedFilterPath = string.Empty;
        usedExpressionCache = false;
        if (!TryResolveTeklaFilterPath(filterName, modelPath, out var fullPath))
        {
            return false;
        }

        resolvedFilterPath = fullPath;
        if (!TryLoadExpression(fullPath, out var expression, out usedExpressionCache))
        {
            return false;
        }

        try
        {
            return Operation.ObjectMatchesToFilter(modelObject, expression);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLoadExpression(
        string fullPath,
        out FilterExpression expression,
        out bool usedExpressionCache)
    {
        expression = null!;
        usedExpressionCache = false;
        var nowUtc = DateTime.UtcNow;

        lock (SyncRoot)
        {
            if (ExpressionCache.TryGetValue(fullPath, out var cached)
                && nowUtc - cached.LastValidationUtc <= HotCacheWindow)
            {
                expression = cached.Expression;
                usedExpressionCache = true;
                return true;
            }
        }

        var writeTimeUtc = File.GetLastWriteTimeUtc(fullPath);
        lock (SyncRoot)
        {
            if (ExpressionCache.TryGetValue(fullPath, out var cached)
                && cached.WriteTimeUtc == writeTimeUtc)
            {
                cached.LastValidationUtc = nowUtc;
                expression = cached.Expression;
                usedExpressionCache = true;
                return true;
            }
        }

        if (!TryBuildExpression(fullPath, out expression))
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (ExpressionCache.Count >= MaxCacheSize)
                ExpressionCache.Clear();
            ExpressionCache[fullPath] = new ExpressionCacheEntry(writeTimeUtc, nowUtc, expression);
            return true;
        }
    }

    private static bool TryBuildExpression(string fullPath, out FilterExpression expression)
    {
        expression = null!;
        try
        {
            var filter = new Filter(fullPath, CultureInfo.InvariantCulture);
            expression = filter.FilterExpression!;
            return expression != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveTeklaFilterPath(string filterName, string? modelPath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(filterName))
        {
            return false;
        }

        var cacheKey = BuildPathCacheKey(filterName, modelPath);
        var nowUtc = DateTime.UtcNow;
        lock (SyncRoot)
        {
            if (PathCache.TryGetValue(cacheKey, out var cached)
                && nowUtc - cached.LastValidationUtc <= HotCacheWindow)
            {
                fullPath = cached.ResolvedPath;
                return cached.Found;
            }
        }

        var candidatePaths = new List<string>();
        if (Path.IsPathRooted(filterName))
        {
            candidatePaths.Add(filterName);
            if (!Path.HasExtension(filterName))
            {
                candidatePaths.Add(filterName + TeklaViewFilterExtension);
            }
        }
        else
        {
            foreach (var directory in TeklaAttributeDirectories.GetSearchDirectories(modelPath))
            {
                candidatePaths.Add(Path.Combine(directory, filterName));
                if (!Path.HasExtension(filterName))
                {
                    candidatePaths.Add(Path.Combine(directory, filterName + TeklaViewFilterExtension));
                }
            }
        }

        foreach (var candidate in candidatePaths
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                fullPath = candidate;
                UpdatePathCache(cacheKey, nowUtc, fullPath, true);
                return true;
            }
        }

        UpdatePathCache(cacheKey, nowUtc, string.Empty, false);
        return false;
    }

    private static string BuildPathCacheKey(string filterName, string? modelPath)
    {
        return FormattableString.Invariant($"{modelPath?.Trim() ?? string.Empty}|{filterName.Trim()}");
    }

    private static void UpdatePathCache(string cacheKey, DateTime nowUtc, string resolvedPath, bool found)
    {
        lock (SyncRoot)
        {
            if (PathCache.Count >= MaxCacheSize)
                PathCache.Clear();
            PathCache[cacheKey] = new PathCacheEntry
            {
                ResolvedPath = resolvedPath,
                Found = found,
                LastValidationUtc = nowUtc,
            };
        }
    }

    private sealed class ExpressionCacheEntry
    {
        public ExpressionCacheEntry(DateTime writeTimeUtc, DateTime lastValidationUtc, FilterExpression expression)
        {
            WriteTimeUtc = writeTimeUtc;
            LastValidationUtc = lastValidationUtc;
            Expression = expression;
        }

        public DateTime WriteTimeUtc { get; }
        public DateTime LastValidationUtc { get; set; }
        public FilterExpression Expression { get; }
    }

    private sealed class PathCacheEntry
    {
        public string ResolvedPath { get; set; } = string.Empty;
        public bool Found { get; set; }
        public DateTime LastValidationUtc { get; set; }
    }
}
