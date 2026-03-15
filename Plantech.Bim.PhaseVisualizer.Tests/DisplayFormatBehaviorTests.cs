using Plantech.Bim.PhaseVisualizer.Configuration;
using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Services;
using System.Collections.Generic;
using Xunit;

namespace Plantech.Bim.PhaseVisualizer.Tests;

public sealed class DisplayFormatBehaviorTests
{
    [Fact]
    public void PhaseTableBuilder_FormatsUnixSecondsDate_ForDistinctStringColumn()
    {
        var builder = new PhaseTableBuilder();
        var snapshot = new PhaseSnapshot
        {
            Objects = new[]
            {
                new PhaseObjectRecord
                {
                    PhaseNumber = 1,
                    PhaseName = "Phase 1",
                    Attributes = new Dictionary<string, PhaseCellValue>
                    {
                        ["part.ua.PT_FREIGABE"] = PhaseCellValue.FromString("1730764800"),
                    },
                },
                new PhaseObjectRecord
                {
                    PhaseNumber = 1,
                    PhaseName = "Phase 1",
                    Attributes = new Dictionary<string, PhaseCellValue>
                    {
                        ["part.ua.PT_FREIGABE"] = PhaseCellValue.FromString("1752969600"),
                    },
                },
            },
        };

        var config = new PhaseTableConfig
        {
            Version = 2,
            Columns = new List<PhaseColumnConfig>
            {
                new()
                {
                    Key = "freigabe_values",
                    Label = "FreigabeDaten",
                    Type = PhaseValueType.String,
                    ObjectType = PhaseColumnObjectType.Part,
                    Attribute = "ua.PT_FREIGABE",
                    Aggregate = PhaseAggregateType.Distinct,
                    VisibleByDefault = true,
                    Order = 37,
                    DisplayFormat = "unixSecondsDate",
                    DateFormat = "yyyy-MM-dd",
                },
            },
        };

        var rows = builder.BuildRows(snapshot, config);

        var row = Assert.Single(rows);
        Assert.True(row.Cells.TryGetValue("freigabe_values", out var value));
        Assert.Equal("2024-11-05; 2025-07-20", value.Text);
    }
}
