using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plantech.Bim.PhaseVisualizer.UI;

internal sealed class PhaseColumnsController
{
    public List<PhaseColumnPresentation> Build(IReadOnlyList<PhaseColumnConfig>? configColumns)
    {
        var columns = (configColumns ?? Array.Empty<PhaseColumnConfig>())
            .Where(c => c != null && c.VisibleByDefault)
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(c => new PhaseColumnPresentation
            {
                Key = c.Key,
                Label = string.IsNullOrWhiteSpace(c.Label) ? c.Key : c.Label,
                TargetObjectType = c.TargetObjectType,
                TargetAttribute = c.TargetAttribute ?? string.Empty,
                BooleanMode = c.BooleanMode ?? string.Empty,
                ApplyRule = c.ApplyRule,
                Aggregate = c.Aggregate,
                Type = c.Type,
                IsEditable = c.Editable,
            })
            .ToList();

        if (columns.Count > 0)
        {
            return columns;
        }

        return CreateFallbackColumns();
    }

    private static List<PhaseColumnPresentation> CreateFallbackColumns()
    {
        return new List<PhaseColumnPresentation>
        {
            new()
            {
                Key = "phase_number",
                Label = "Phase",
                Aggregate = PhaseAggregateType.First,
                Type = PhaseValueType.Integer,
            },
            new()
            {
                Key = "phase_name",
                Label = "Name",
                Aggregate = PhaseAggregateType.First,
                Type = PhaseValueType.String,
            },
            new()
            {
                Key = "object_count",
                Label = "Count",
                Aggregate = PhaseAggregateType.Count,
                Type = PhaseValueType.Integer,
            },
        };
    }
}
