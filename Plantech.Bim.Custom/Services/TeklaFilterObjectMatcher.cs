using Plantech.Bim.Custom.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Tekla.Structures.Filtering;
using Tekla.Structures.Model;

namespace Plantech.Bim.Custom.Services;

internal sealed class TeklaFilterObjectMatcher
{
    private const string TeklaViewFilterExtension = ".SObjGrp";
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);

    public bool IsMatch(string filterName, string? modelPath, int objectId)
    {
        return TryMatch(filterName, modelPath, objectId, out _);
    }

    public bool TryMatch(string filterName, string? modelPath, int objectId, out string resolvedFilterPath)
    {
        resolvedFilterPath = string.Empty;
        if (!TryResolveTeklaFilterPath(filterName, modelPath, out var fullPath))
        {
            return false;
        }

        resolvedFilterPath = fullPath;
        var objectIds = LoadObjectIds(fullPath);
        return objectIds.Contains(objectId);
    }

    private static HashSet<int> LoadObjectIds(string fullPath)
    {
        var writeTimeUtc = File.GetLastWriteTimeUtc(fullPath);

        lock (SyncRoot)
        {
            if (Cache.TryGetValue(fullPath, out var cached) && cached.WriteTimeUtc == writeTimeUtc)
            {
                return cached.ObjectIds;
            }
        }

        var loaded = BuildObjectIdSet(fullPath);

        lock (SyncRoot)
        {
            Cache[fullPath] = new CacheEntry(writeTimeUtc, loaded);
            return loaded;
        }
    }

    private static HashSet<int> BuildObjectIdSet(string fullPath)
    {
        var result = new HashSet<int>();
        var model = new Model();
        var selector = model.GetModelObjectSelector();
        if (selector == null)
        {
            return result;
        }

        var previousAutoFetch = ModelObjectEnumerator.AutoFetch;
        ModelObjectEnumerator.AutoFetch = false;
        try
        {
            var filter = new Filter(fullPath, CultureInfo.InvariantCulture);
            var expression = filter.FilterExpression;
            if (expression == null)
            {
                return result;
            }

            var objects = selector.GetObjectsByFilter(expression);
            if (objects == null)
            {
                return result;
            }

            while (objects.MoveNext())
            {
                if (objects.Current is ModelObject modelObject)
                {
                    result.Add(modelObject.Identifier.ID);
                }
            }

            return result;
        }
        catch
        {
            return new HashSet<int>();
        }
        finally
        {
            ModelObjectEnumerator.AutoFetch = previousAutoFetch;
        }
    }

    private static bool TryResolveTeklaFilterPath(string filterName, string? modelPath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(filterName))
        {
            return false;
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
                return true;
            }
        }

        return false;
    }

    private sealed class CacheEntry
    {
        public CacheEntry(DateTime writeTimeUtc, HashSet<int> objectIds)
        {
            WriteTimeUtc = writeTimeUtc;
            ObjectIds = objectIds;
        }

        public DateTime WriteTimeUtc { get; }
        public HashSet<int> ObjectIds { get; }
    }
}
