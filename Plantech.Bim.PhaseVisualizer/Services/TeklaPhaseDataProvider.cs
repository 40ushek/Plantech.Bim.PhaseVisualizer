using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Common;
using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Tekla.Structures.Filtering;
using Tekla.Structures.Filtering.Categories;
using Tekla.Structures;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class TeklaPhaseDataProvider
{
    private const string UdaPrefix = "part.ua.";

    public PhaseSnapshot LoadPhaseSnapshot(
        SynchronizationContext? teklaContext,
        IReadOnlyCollection<PhaseColumnConfig>? requiredColumns,
        bool includeAllPhases,
        bool useVisibleViewsForSearch,
        bool includePhaseObjectCounts = true,
        ILogger? log = null)
    {
        var searchScope = PhaseSearchScopeMapper.FromUseVisibleViewsFlag(useVisibleViewsForSearch);
        return LoadPhaseSnapshot(
            teklaContext,
            requiredColumns,
            includeAllPhases,
            searchScope,
            includePhaseObjectCounts,
            log);
    }

    public PhaseSnapshot LoadPhaseSnapshot(
        SynchronizationContext? teklaContext,
        IReadOnlyCollection<PhaseColumnConfig>? requiredColumns,
        bool includeAllPhases,
        PhaseSearchScope searchScope,
        bool includePhaseObjectCounts = true,
        ILogger? log = null)
    {
        var requested = RequiredSourceSet.FromColumns(requiredColumns);
        return TeklaContextDispatcher.Run(
                   teklaContext,
                   () => CollectSnapshot(log, requested, includeAllPhases, searchScope, includePhaseObjectCounts),
                   log,
                   noContextWarning: "PhaseVisualizer data load runs without Tekla synchronization context.",
                   sendFailedWarning: "PhaseVisualizer data load on Tekla context failed. Falling back to direct call.")
               ?? new PhaseSnapshot();
    }

    private static PhaseSnapshot CollectSnapshot(
        ILogger? log,
        RequiredSourceSet requested,
        bool includeAllPhases,
        PhaseSearchScope searchScope,
        bool includePhaseObjectCounts)
    {
        var model = LazyModelConnector.ModelInstance;
        if (model == null)
        {
            return new PhaseSnapshot();
        }

        var phases = CollectAllPhases(model);
        var phaseObjectCounts = includePhaseObjectCounts
            ? BuildPhaseCountMapByPhaseFilter(model, phases, log)
            : new Dictionary<int, int>();

        var attributeScanScope = searchScope;
        if (requested.RequiresAttributeScan)
        {
            var records = CollectAttributeScanRecords(model, log, requested, attributeScanScope);
            var allPhases = includeAllPhases
                ? phases
                : Array.Empty<PhaseCatalogEntry>();

            LogSnapshotSummary(
                log,
                requested.RequiresAttributeScan,
                attributeScanScope,
                allPhases.Count,
                records.Count,
                phaseObjectCounts);

            return new PhaseSnapshot
            {
                CreatedAtUtc = DateTime.UtcNow,
                Objects = records,
                AllPhases = allPhases,
                PhaseObjectCounts = phaseObjectCounts,
            };
        }

        var filteredPhases = includeAllPhases
            ? phases
            : includePhaseObjectCounts
                ? phases
                    .Where(p => phaseObjectCounts.TryGetValue(p.PhaseNumber, out var count) && count > 0)
                    .ToList()
                : phases.ToList();

        LogSnapshotSummary(
            log,
            requested.RequiresAttributeScan,
            attributeScanScope,
            filteredPhases.Count,
            0,
            phaseObjectCounts);

        return new PhaseSnapshot
        {
            CreatedAtUtc = DateTime.UtcNow,
            Objects = Array.Empty<PhaseObjectRecord>(),
            AllPhases = filteredPhases,
            PhaseObjectCounts = phaseObjectCounts,
        };
    }

    private static List<PhaseObjectRecord> CollectAttributeScanRecords(
        Model model,
        ILogger? log,
        RequiredSourceSet requested,
        PhaseSearchScope scope)
    {
        return scope == PhaseSearchScope.VisibleViews
            ? CollectVisibleViewParts(model, log, requested)
            : CollectTeklaModelParts(model, log, requested);
    }

    private static void LogSnapshotSummary(
        ILogger? log,
        bool requiresAttributeScan,
        PhaseSearchScope attributeScanScope,
        int phaseRowCount,
        int recordsCount,
        IReadOnlyDictionary<int, int> phaseObjectCounts)
    {
        if (log == null)
        {
            return;
        }

        var nonEmptyPhaseCount = phaseObjectCounts?.Count ?? 0;
        log.Information(
            "PhaseVisualizer snapshot built. AttributeScanRequired={RequiresAttributeScan}, AttributeScanScope={AttributeScanScope}, RowCount={RowCount}, Records={Records}, NonEmptyPhaseCount={NonEmptyPhaseCount}, CountScope=ModelWide",
            requiresAttributeScan,
            attributeScanScope,
            phaseRowCount,
            recordsCount,
            nonEmptyPhaseCount);
    }

    private static IReadOnlyDictionary<int, int> BuildPhaseCountMapByPhaseFilter(
        Model model,
        IReadOnlyCollection<PhaseCatalogEntry> phases,
        ILogger? log)
    {
        if (model == null || phases == null || phases.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var selector = model.GetModelObjectSelector();
        if (selector == null)
        {
            return new Dictionary<int, int>();
        }

        var previousAutoFetch = ModelObjectEnumerator.AutoFetch;
        ModelObjectEnumerator.AutoFetch = true;
        var result = new Dictionary<int, int>();
        try
        {
            foreach (var phase in phases)
            {
                try
                {
                    var filter = BuildPhaseFilter(phase.PhaseNumber);
                    var objects = selector.GetObjectsByFilter(filter);

                    //if (phase.PhaseNumber == 127)
                    //    while (objects.MoveNext())
                    //    {
                    //        var o = objects.Current;
                    //    }

                    var count = objects?.GetSize() ?? 0;
                    if (count > 0)
                    {
                        result[phase.PhaseNumber] = count;
                    }
                }
                catch (Exception ex)
                {
                    log?.Warning(
                        ex,
                        "PhaseVisualizer phase count failed for phase {PhaseNumber}.",
                        phase.PhaseNumber);
                }
            }
        }
        finally
        {
            ModelObjectEnumerator.AutoFetch = previousAutoFetch;
        }

        return result;
    }

    private static BinaryFilterExpression BuildPhaseFilter(int phaseNumber)
    {
        return new BinaryFilterExpression(
            new ObjectFilterExpressions.Phase(),
            NumericOperatorType.IS_EQUAL,
            new NumericConstantFilterExpression(phaseNumber));
    }

    private static List<PhaseObjectRecord> CollectTeklaModelParts(Model model, ILogger? log, RequiredSourceSet requested)
    {
        var selector = model.GetModelObjectSelector();
        if (selector == null)
        {
            return new List<PhaseObjectRecord>();
        }

        var result = new List<PhaseObjectRecord>();
        var uniqueObjectKeys = new HashSet<string>(StringComparer.Ordinal);
        var assemblyByKey = new Dictionary<string, Assembly?>(StringComparer.Ordinal);
        var previousAutoFetch = ModelObjectEnumerator.AutoFetch;
        ModelObjectEnumerator.AutoFetch = true;
        try
        {
            var allObjects = selector.GetAllObjects();
            AppendPartRecords(allObjects, result, uniqueObjectKeys, requested, assemblyByKey);

            log?.Information(
                "PhaseVisualizer Tekla model part snapshot collected. Parts={PartCount}",
                result.Count);

            return result;
        }
        finally
        {
            ModelObjectEnumerator.AutoFetch = previousAutoFetch;
        }
    }

    private static List<PhaseObjectRecord> CollectVisibleViewParts(Model model, ILogger? log, RequiredSourceSet requested)
    {
        var selector = model.GetModelObjectSelector();
        if (selector == null)
        {
            return new List<PhaseObjectRecord>();
        }

        var result = new List<PhaseObjectRecord>();
        var uniqueObjectKeys = new HashSet<string>(StringComparer.Ordinal);
        var assemblyByKey = new Dictionary<string, Assembly?>(StringComparer.Ordinal);
        var previousAutoFetch = ModelObjectEnumerator.AutoFetch;
        ModelObjectEnumerator.AutoFetch = true;
        try
        {
            var visibleViews = ViewHandler.GetVisibleViews();
            var viewCount = 0;
            if (visibleViews != null)
            {
                while (visibleViews.MoveNext())
                {
                    if (visibleViews.Current is not View view || view.WorkArea == null)
                    {
                        continue;
                    }

                    viewCount++;
                    var objectsInView = selector.GetObjectsByBoundingBox(
                        view.WorkArea.MinPoint,
                        view.WorkArea.MaxPoint);
                    AppendPartRecords(objectsInView, result, uniqueObjectKeys, requested, assemblyByKey);
                }
            }

            if (viewCount == 0)
            {
                var activeView = TeklaViewHelper.GetActiveView();
                if (activeView?.WorkArea != null)
                {
                    var objectsInActiveView = selector.GetObjectsByBoundingBox(
                        activeView.WorkArea.MinPoint,
                        activeView.WorkArea.MaxPoint);
                    AppendPartRecords(objectsInActiveView, result, uniqueObjectKeys, requested, assemblyByKey);
                    viewCount = 1;
                }
            }

            log?.Information(
                "PhaseVisualizer visible-view part snapshot collected. Views={ViewCount}, Parts={PartCount}",
                viewCount,
                result.Count);

            return result;
        }
        finally
        {
            ModelObjectEnumerator.AutoFetch = previousAutoFetch;
        }
    }

    private static IReadOnlyList<PhaseCatalogEntry> CollectAllPhases(Model model)
    {
        var result = new List<PhaseCatalogEntry>();
        var seen = new HashSet<int>();

        var phases = model.GetPhases();
        var enumerator = phases?.GetEnumerator();
        if (enumerator == null)
        {
            return result;
        }

        while (enumerator.MoveNext())
        {
            if (enumerator.Current is not Phase phase)
            {
                continue;
            }

            if (!seen.Add(phase.PhaseNumber))
            {
                continue;
            }

            result.Add(new PhaseCatalogEntry
            {
                PhaseNumber = phase.PhaseNumber,
                PhaseName = phase.PhaseName ?? string.Empty,
            });
        }

        return result
            .OrderBy(x => x.PhaseNumber)
            .ToList();
    }

    private static void AppendPartRecords(
        ModelObjectEnumerator? objects,
        ICollection<PhaseObjectRecord> target,
        ISet<string> uniqueObjectKeys,
        RequiredSourceSet requested,
        IDictionary<string, Assembly?> assemblyByKey)
    {
        if (objects == null)
        {
            return;
        }

        var size = objects.GetSize();
        if (size <= 0)
        {
            return;
        }

        if (target is List<PhaseObjectRecord> list && list.Capacity < list.Count + size)
        {
            list.Capacity = list.Count + size;
        }

        while (objects.MoveNext())
        {
            if (objects.Current is Part part)
            {
                var key = BuildObjectKey(part.Identifier);
                if (!uniqueObjectKeys.Add(key))
                {
                    continue;
                }

                target.Add(BuildRecord(part, requested, assemblyByKey));
                continue;
            }

            if (requested.BoltAttributes.Count > 0 && objects.Current is BoltGroup boltGroup)
            {
                var key = BuildObjectKey(boltGroup.Identifier);
                if (!uniqueObjectKeys.Add(key))
                {
                    continue;
                }

                target.Add(BuildBoltRecord(boltGroup, requested));
            }
        }
    }

    private static PhaseObjectRecord BuildRecord(
        Part part,
        RequiredSourceSet requested,
        IDictionary<string, Assembly?> assemblyByKey)
    {
        part.GetPhase(out var phase);

        var phaseNumber = phase?.PhaseNumber ?? 0;
        var phaseName = phase?.PhaseName ?? string.Empty;

        var attributes = new Dictionary<string, PhaseCellValue>(StringComparer.Ordinal)
        {
            ["phase.number"] = PhaseCellValue.FromInteger(phaseNumber),
            ["phase.name"] = PhaseCellValue.FromString(phaseName),
        };

        foreach (var partAttribute in requested.PartAttributes)
        {
            if (TryGetPartAttributeValue(part, partAttribute, out var partValue))
            {
                attributes[$"part.{partAttribute}"] = PhaseCellValue.FromString(partValue);
            }
        }

        if (requested.AssemblyAttributes.Count > 0)
        {
            var assembly = GetAssemblyCached(part, assemblyByKey);
            if (assembly != null)
            {
                foreach (var assemblyAttribute in requested.AssemblyAttributes)
                {
                    var value = string.Empty;
                    assembly.GetReportProperty(assemblyAttribute, ref value);
                    attributes[$"assembly.{assemblyAttribute}"] = PhaseCellValue.FromString(value);
                }
            }
        }

        AppendUserAttributes(part, attributes, requested.UdaNames);

        return new PhaseObjectRecord
        {
            ObjectId = part.Identifier,
            PhaseNumber = phaseNumber,
            PhaseName = phaseName,
            Attributes = attributes,
        };
    }

    private static PhaseObjectRecord BuildBoltRecord(BoltGroup bolt, RequiredSourceSet requested)
    {
        bolt.GetPhase(out var phase);

        var phaseNumber = phase?.PhaseNumber ?? 0;
        var phaseName = phase?.PhaseName ?? string.Empty;

        var attributes = new Dictionary<string, PhaseCellValue>(StringComparer.Ordinal)
        {
            ["phase.number"] = PhaseCellValue.FromInteger(phaseNumber),
            ["phase.name"] = PhaseCellValue.FromString(phaseName),
        };

        foreach (var boltAttribute in requested.BoltAttributes)
        {
            var value = string.Empty;
            bolt.GetReportProperty(boltAttribute, ref value);
            attributes[$"bolt.{boltAttribute}"] = PhaseCellValue.FromString(value);
        }

        return new PhaseObjectRecord
        {
            ObjectId = bolt.Identifier,
            PhaseNumber = phaseNumber,
            PhaseName = phaseName,
            Attributes = attributes,
        };
    }

    private static bool TryGetPartAttributeValue(
        Part part,
        string attribute,
        out string? value)
    {
        value = null;
        switch (attribute)
        {
            case "profile":
                value = part.Profile?.ProfileString;
                return true;
            case "material":
                value = part.Material?.MaterialString;
                return true;
            case "class":
                value = part.Class;
                return true;
            case "name":
                value = part.Name;
                return true;
            case "finish":
                value = part.Finish;
                return true;
            default:
                return false;
        }
    }

    private static Assembly? GetAssemblyCached(Part part, IDictionary<string, Assembly?> assemblyByKey)
    {
        if (part == null)
        {
            return null;
        }

        try
        {
            var assembly = part.GetAssembly();
            if (assembly == null)
            {
                return null;
            }

            var key = BuildObjectKey(assembly.Identifier);
            if (!assemblyByKey.TryGetValue(key, out var cached))
            {
                assemblyByKey[key] = assembly;
                cached = assembly;
            }

            return cached;
        }
        catch
        {
            return null;
        }
    }

    private static void AppendUserAttributes(
        Part part,
        IDictionary<string, PhaseCellValue> target,
        IReadOnlyCollection<string> udaNames)
    {
        if (udaNames == null || udaNames.Count == 0)
        {
            return;
        }

        foreach (var udaName in udaNames)
        {
            if (string.IsNullOrWhiteSpace(udaName))
            {
                continue;
            }

            if (TryGetUserPropertyValue(part, udaName, out var value))
            {
                target[$"{UdaPrefix}{udaName}"] = PhaseCellValue.FromObject(value);
            }
        }
    }

    private static bool TryGetUserPropertyValue(Part part, string udaName, out object? value)
    {
        value = null;

        string text = string.Empty;
        if (part.GetUserProperty(udaName, ref text))
        {
            value = text;
            return true;
        }

        int integer = 0;
        if (part.GetUserProperty(udaName, ref integer))
        {
            value = integer;
            return true;
        }

        double number = 0d;
        if (part.GetUserProperty(udaName, ref number))
        {
            value = number;
            return true;
        }

        return false;
    }

    private static string BuildObjectKey(Identifier identifier)
    {
        if (identifier.GUID != Guid.Empty)
        {
            return identifier.GUID.ToString("D", CultureInfo.InvariantCulture);
        }

        return FormattableString.Invariant($"{identifier.ID}:{identifier.ID2}");
    }



    private sealed class RequiredSourceSet
    {
        public bool RequiresAttributeScan { get; private set; }
        public IReadOnlyCollection<string> PartAttributes => _partAttributes;
        public IReadOnlyCollection<string> AssemblyAttributes => _assemblyAttributes;
        public IReadOnlyCollection<string> BoltAttributes => _boltAttributes;
        public IReadOnlyCollection<string> UdaNames => _udaNames;

        private readonly HashSet<string> _partAttributes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _assemblyAttributes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _boltAttributes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _udaNames = new(StringComparer.OrdinalIgnoreCase);

        public static RequiredSourceSet FromColumns(IReadOnlyCollection<PhaseColumnConfig>? columns)
        {
            var result = new RequiredSourceSet();
            if (columns == null || columns.Count == 0)
            {
                return result;
            }

            foreach (var column in columns.Where(c => c != null))
            {
                if (column.Editable
                    || !column.ObjectType.HasValue
                    || string.IsNullOrWhiteSpace(column.Attribute))
                {
                    continue;
                }

                var attribute = column.Attribute.Trim();
                switch (column.ObjectType.Value)
                {
                    case PhaseColumnObjectType.Phase:
                        continue;
                    case PhaseColumnObjectType.Part:
                        result.RequiresAttributeScan = true;
                        if (attribute.StartsWith("ua.", StringComparison.OrdinalIgnoreCase))
                        {
                            var udaName = attribute.Substring("ua.".Length).Trim();
                            if (!string.IsNullOrWhiteSpace(udaName))
                            {
                                result._udaNames.Add(udaName);
                            }

                            continue;
                        }

                        result._partAttributes.Add(attribute);
                        continue;
                    case PhaseColumnObjectType.Assembly:
                        result.RequiresAttributeScan = true;
                        result._assemblyAttributes.Add(attribute);
                        continue;
                    case PhaseColumnObjectType.Bolt:
                        result.RequiresAttributeScan = true;
                        result._boltAttributes.Add(attribute);
                        continue;
                    default:
                        continue;
                }
            }

            return result;
        }
    }
}
