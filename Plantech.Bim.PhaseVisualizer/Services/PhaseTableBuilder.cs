using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseTableBuilder
{
    public IReadOnlyList<PhaseRow> BuildRows(PhaseSnapshot snapshot, PhaseTableConfig config, ILogger? log = null)
    {
        if (snapshot == null)
        {
            return Array.Empty<PhaseRow>();
        }

        var snapshotObjects = snapshot.Objects ?? Array.Empty<PhaseObjectRecord>();
        var snapshotAllPhases = snapshot.AllPhases ?? Array.Empty<PhaseCatalogEntry>();
        var snapshotPhaseObjectCounts = snapshot.PhaseObjectCounts ?? new Dictionary<int, int>();
        if (snapshotObjects.Count == 0 && snapshotAllPhases.Count == 0 && snapshotPhaseObjectCounts.Count == 0)
        {
            return Array.Empty<PhaseRow>();
        }

        if (config?.Columns == null || config.Columns.Count == 0)
        {
            log?.Warning("PhaseVisualizer BuildRows skipped: config has no columns.");
            return Array.Empty<PhaseRow>();
        }

        var sortedColumns = config.Columns
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var loggedAssemblyFallbackSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rows = snapshotObjects
            .GroupBy(o => o.PhaseNumber)
            .Select(group => BuildRow(group.Key, group.ToList(), sortedColumns, log, loggedAssemblyFallbackSources))
            .OrderBy(r => r.PhaseNumber)
            .ThenBy(r => r.PhaseName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (snapshotAllPhases.Count > 0)
        {
            MergeMissingPhases(rows, snapshotAllPhases, sortedColumns, log, loggedAssemblyFallbackSources);
        }

        if (snapshotPhaseObjectCounts.Count > 0)
        {
            ApplyObjectCounts(rows, snapshotPhaseObjectCounts, sortedColumns);
        }

        return rows;
    }

    private static void ApplyObjectCounts(
        IReadOnlyList<PhaseRow> rows,
        IReadOnlyDictionary<int, int> phaseObjectCounts,
        IReadOnlyList<PhaseColumnConfig> columns)
    {
        foreach (var row in rows)
        {
            if (!phaseObjectCounts.TryGetValue(row.PhaseNumber, out var count))
            {
                continue;
            }

            row.UpdateObjectCount(count);
            foreach (var column in columns)
            {
                if (column.Aggregate != PhaseAggregateType.Count)
                {
                    continue;
                }

                row.Cells[column.Key] = PhaseCellValue.FromInteger(count).ConvertTo(column.Type);
            }
        }
    }

    private static void MergeMissingPhases(
        IList<PhaseRow> rows,
        IReadOnlyList<PhaseCatalogEntry> allPhases,
        IReadOnlyList<PhaseColumnConfig> columns,
        ILogger? log,
        ISet<string> loggedAssemblyFallbackSources)
    {
        var byPhase = rows.ToDictionary(r => r.PhaseNumber);
        foreach (var phase in allPhases)
        {
            if (byPhase.TryGetValue(phase.PhaseNumber, out var existing))
            {
                ApplyPhaseIdentity(existing, phase.PhaseNumber, phase.PhaseName, columns);
                continue;
            }

            var emptyObjects = Array.Empty<PhaseObjectRecord>();
            var row = BuildRow(phase.PhaseNumber, emptyObjects, columns, log, loggedAssemblyFallbackSources);
            ApplyPhaseIdentity(row, phase.PhaseNumber, phase.PhaseName, columns);
            rows.Add(row);
            byPhase[phase.PhaseNumber] = row;
        }

        var ordered = rows
            .OrderBy(r => r.PhaseNumber)
            .ThenBy(r => r.PhaseName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        rows.Clear();
        foreach (var row in ordered)
        {
            rows.Add(row);
        }
    }

    private static void ApplyPhaseIdentity(
        PhaseRow row,
        int phaseNumber,
        string phaseName,
        IReadOnlyList<PhaseColumnConfig> columns)
    {
        foreach (var column in columns)
        {
            if (column.Aggregate == PhaseAggregateType.Count)
            {
                row.Cells[column.Key] = PhaseCellValue.FromInteger(row.ObjectCount).ConvertTo(column.Type);
                continue;
            }

            if (column.ObjectType == PhaseColumnObjectType.Phase
                && string.Equals(column.Attribute, "number", StringComparison.OrdinalIgnoreCase))
            {
                row.Cells[column.Key] = PhaseCellValue.FromInteger(phaseNumber).ConvertTo(column.Type);
                continue;
            }

            if (column.ObjectType != PhaseColumnObjectType.Phase
                || !string.Equals(column.Attribute, "name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(phaseName))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.PhaseName))
            {
                row.UpdatePhaseName(phaseName);
            }

            row.Cells[column.Key] = PhaseCellValue.FromString(phaseName).ConvertTo(column.Type);
        }
    }

    private static PhaseRow BuildRow(
        int phaseNumber,
        IReadOnlyList<PhaseObjectRecord> objects,
        IReadOnlyList<PhaseColumnConfig> columns,
        ILogger? log,
        ISet<string> loggedAssemblyFallbackSources)
    {
        var phaseName = objects
            .Select(o => o.PhaseName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? string.Empty;

        var row = new PhaseRow(phaseNumber, phaseName, objects.Count);

        foreach (var column in columns)
        {
            var value = AggregateColumn(column, objects, log, loggedAssemblyFallbackSources);
            row.Cells[column.Key] = value;
        }

        return row;
    }

    private static PhaseCellValue AggregateColumn(
        PhaseColumnConfig column,
        IReadOnlyList<PhaseObjectRecord> objects,
        ILogger? log,
        ISet<string> loggedAssemblyFallbackSources)
    {
        if (column.Aggregate == PhaseAggregateType.Count)
        {
            return PhaseCellValue.FromInteger(objects.Count).ConvertTo(column.Type);
        }

        var values = objects
            .Select(o => ResolveValue(o, column.ObjectType, column.Attribute, log, loggedAssemblyFallbackSources))
            .Where(v => v.HasValue)
            .ToList();

        if (values.Count == 0)
        {
            return EmptyFor(column.Type);
        }

        var aggregateResult = column.Aggregate switch
        {
            PhaseAggregateType.First => values[0],
            PhaseAggregateType.Distinct => AggregateDistinct(values),
            PhaseAggregateType.Min => AggregateMin(values),
            PhaseAggregateType.Max => AggregateMax(values),
            _ => values[0],
        };

        return aggregateResult.ConvertTo(column.Type);
    }

    private static PhaseCellValue ResolveValue(
        PhaseObjectRecord record,
        PhaseColumnObjectType? objectType,
        string attribute,
        ILogger? log,
        ISet<string> loggedAssemblyFallbackSources)
    {
        if (!objectType.HasValue || string.IsNullOrWhiteSpace(attribute))
        {
            return PhaseCellValue.Empty;
        }

        if (objectType == PhaseColumnObjectType.Phase)
        {
            if (string.Equals(attribute, "number", StringComparison.OrdinalIgnoreCase))
            {
                return PhaseCellValue.FromInteger(record.PhaseNumber);
            }

            if (string.Equals(attribute, "name", StringComparison.OrdinalIgnoreCase))
            {
                return PhaseCellValue.FromString(record.PhaseName);
            }

            return PhaseCellValue.Empty;
        }

        if (!PhaseSourceResolver.TryBuildModelSource(
                objectType.Value,
                attribute,
                out var normalizedAttribute,
                out var source,
                out _))
        {
            return PhaseCellValue.Empty;
        }

        if (TryGetAttributeValue(record.Attributes, source, out var directValue))
        {
            return directValue;
        }

        if (objectType == PhaseColumnObjectType.Assembly)
        {
            var fallbackPartSource = $"part.{normalizedAttribute}";
            if (TryGetAttributeValue(record.Attributes, fallbackPartSource, out var fallbackPartValue))
            {
                if (!string.IsNullOrWhiteSpace(source) && loggedAssemblyFallbackSources.Add(source))
                {
                    log?.Warning(
                        "PhaseVisualizer source {Source} fell back to {FallbackSource} because assembly main-part value was unavailable.",
                        source,
                        fallbackPartSource);
                }

                return fallbackPartValue;
            }
        }

        return PhaseCellValue.Empty;
    }

    private static bool TryGetAttributeValue(
        IReadOnlyDictionary<string, PhaseCellValue> attributes,
        string key,
        out PhaseCellValue value)
    {
        if (attributes.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var pair in attributes)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = PhaseCellValue.Empty;
        return false;
    }

    private static PhaseCellValue AggregateDistinct(IReadOnlyCollection<PhaseCellValue> values)
    {
        var distinct = values
            .Select(v => v.AsComparableString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
        {
            return PhaseCellValue.Empty;
        }

        return PhaseCellValue.FromString(string.Join("; ", distinct));
    }

    private static PhaseCellValue AggregateMin(IReadOnlyCollection<PhaseCellValue> values)
    {
        var numericValues = ExtractNumericValues(values);

        if (numericValues.Count == 0)
        {
            return PhaseCellValue.Empty;
        }

        return PhaseCellValue.FromNumber(numericValues.Min());
    }

    private static PhaseCellValue AggregateMax(IReadOnlyCollection<PhaseCellValue> values)
    {
        var numericValues = ExtractNumericValues(values);

        if (numericValues.Count == 0)
        {
            return PhaseCellValue.Empty;
        }

        return PhaseCellValue.FromNumber(numericValues.Max());
    }

    private static double? TryToNumber(PhaseCellValue value)
    {
        if (value.Number.HasValue)
        {
            return value.Number.Value;
        }

        if (value.Bool.HasValue)
        {
            return value.Bool.Value ? 1d : 0d;
        }

        if (double.TryParse(value.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static List<double> ExtractNumericValues(IReadOnlyCollection<PhaseCellValue> values)
    {
        var result = new List<double>();
        foreach (var value in values)
        {
            if (TryToNumber(value) is double number)
            {
                result.Add(number);
            }
        }

        return result;
    }

    private static PhaseCellValue EmptyFor(PhaseValueType valueType)
    {
        return valueType switch
        {
            PhaseValueType.Number => PhaseCellValue.FromNumber(null),
            PhaseValueType.Integer => PhaseCellValue.FromInteger(null),
            PhaseValueType.Boolean => PhaseCellValue.FromBool(null),
            _ => PhaseCellValue.FromString(null),
        };
    }
}

