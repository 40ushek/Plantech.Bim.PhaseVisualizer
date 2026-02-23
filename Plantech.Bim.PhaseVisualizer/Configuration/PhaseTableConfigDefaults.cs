using Plantech.Bim.PhaseVisualizer.Domain;
using System.Collections.Generic;

namespace Plantech.Bim.PhaseVisualizer.Configuration;

internal static class PhaseTableConfigDefaults
{
    public static PhaseTableConfig Create()
    {
        return new PhaseTableConfig
        {
            Version = 2,
            ObjectScope = PhaseObjectScope.Visible,
            PhaseKey = "number",
            Actions = new List<string> { "select", "show_only", "visualize" },
            Columns = new List<PhaseColumnConfig>
            {
                new()
                {
                    Key = "phase_number",
                    Label = "Phase",
                    Type = PhaseValueType.Integer,
                    ObjectType = PhaseColumnObjectType.Phase,
                    Attribute = "number",
                    Aggregate = PhaseAggregateType.First,
                    VisibleByDefault = true,
                    Order = 10,
                    FilterOps = new List<string> { "equals", "in", "range" },
                },
                new()
                {
                    Key = "phase_name",
                    Label = "Name",
                    Type = PhaseValueType.String,
                    ObjectType = PhaseColumnObjectType.Phase,
                    Attribute = "name",
                    Aggregate = PhaseAggregateType.First,
                    VisibleByDefault = true,
                    Order = 20,
                    FilterOps = new List<string> { "equals", "contains", "in" },
                },
                new()
                {
                    Key = "profile",
                    Label = "Profile",
                    Type = PhaseValueType.String,
                    Editable = true,
                    TargetObjectType = PhaseColumnObjectType.Part,
                    TargetAttribute = "profile",
                    Aggregate = PhaseAggregateType.First,
                    VisibleByDefault = true,
                    Order = 25,
                    FilterOps = new List<string> { "equals", "contains", "in" },
                },
                new()
                {
                    Key = "exclude_gratings",
                    Label = "Exclude Gratings",
                    Type = PhaseValueType.Boolean,
                    Editable = true,
                    TargetAttribute = "exclude_gratings",
                    Aggregate = PhaseAggregateType.First,
                    VisibleByDefault = true,
                    Order = 26,
                    FilterOps = new List<string> { "equals" },
                },
                new()
                {
                    Key = "exclude_existing",
                    Label = "Exclude Existing",
                    Type = PhaseValueType.Boolean,
                    Editable = true,
                    TargetAttribute = "exclude_existing",
                    Aggregate = PhaseAggregateType.First,
                    VisibleByDefault = true,
                    Order = 27,
                    FilterOps = new List<string> { "equals" },
                },
                new()
                {
                    Key = "object_count",
                    Label = "Count",
                    Type = PhaseValueType.Integer,
                    ObjectType = PhaseColumnObjectType.Phase,
                    Attribute = "number",
                    Aggregate = PhaseAggregateType.Count,
                    VisibleByDefault = true,
                    Order = 30,
                    FilterOps = new List<string> { "equals", "range" },
                },
            },
        };
    }
}

