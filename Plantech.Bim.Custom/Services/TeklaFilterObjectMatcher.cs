using Plantech.Bim.Custom.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;

namespace Plantech.Bim.Custom.Services;

internal sealed class TeklaFilterObjectMatcher
{
    private const string TeklaViewFilterExtension = ".SObjGrp";
    private const int MaxCacheSize = 1024;
    private static readonly TimeSpan HotCacheWindow = TimeSpan.FromSeconds(2);
    private static readonly object SyncRoot = new();
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
        out bool usedPathCache)
    {
        resolvedFilterPath = string.Empty;
        if (!TryResolveTeklaFilterPath(filterName, modelPath, out var fullPath, out usedPathCache))
        {
            return false;
        }

        resolvedFilterPath = fullPath;
        try
        {
            return Operation.ObjectMatchesToFilter(modelObject, BuildRuntimeFilterName(filterName, fullPath));
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveTeklaFilterPath(
        string filterName,
        string? modelPath,
        out string fullPath,
        out bool usedPathCache)
    {
        fullPath = string.Empty;
        usedPathCache = false;
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
                usedPathCache = true;
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

    private static string BuildRuntimeFilterName(string filterName, string resolvedPath)
    {
        if (Path.IsPathRooted(filterName))
        {
            return Path.GetFileName(resolvedPath);
        }

        return Path.HasExtension(filterName)
            ? filterName
            : filterName + TeklaViewFilterExtension;
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

    private sealed class PathCacheEntry
    {
        public string ResolvedPath { get; set; } = string.Empty;
        public bool Found { get; set; }
        public DateTime LastValidationUtc { get; set; }
    }
}
