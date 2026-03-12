using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Rules;
using Plantech.Bim.PhaseVisualizer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Tekla.Structures.Filtering;
using Xunit;

namespace Plantech.Bim.PhaseVisualizer.Tests;

public sealed class ApplyRuleBehaviorTests
{
    [Fact]
    public void ApplyRuleCompiler_BooleanTrue_UsesOnTrueClause()
    {
        var compiler = new ApplyRuleCompiler();
        var filter = new PhaseAttributeFilter
        {
            TargetAttribute = "CUSTOM.HasBooleans",
            ValueType = PhaseValueType.Boolean,
            Value = "true",
            ApplyRule = new PhaseApplyRuleConfig
            {
                OnTrue = new ApplyRuleClauseConfig { Op = "gt", Value = 0 },
                OnFalse = new ApplyRuleClauseConfig { Op = "eq", Value = 0 },
            },
        };

        var success = compiler.TryCompile(filter, out var clause, out var diagnostic);

        Assert.True(success);
        Assert.Null(diagnostic);
        Assert.NotNull(clause);
        Assert.Equal("CUSTOM.HasBooleans", clause!.Field);
        Assert.Equal(ApplyRuleOperation.Gt, clause.Operation);
        Assert.False(clause.UsesInputValue);
        Assert.Single(clause.LiteralValues);
        Assert.Equal("0", clause.LiteralValues[0]);
    }

    [Fact]
    public void ApplyRuleCompiler_BooleanFalse_UsesOnFalseClause()
    {
        var compiler = new ApplyRuleCompiler();
        var filter = new PhaseAttributeFilter
        {
            TargetAttribute = "CUSTOM.HasBooleans",
            ValueType = PhaseValueType.Boolean,
            Value = "false",
            ApplyRule = new PhaseApplyRuleConfig
            {
                OnTrue = new ApplyRuleClauseConfig { Op = "gt", Value = 0 },
                OnFalse = new ApplyRuleClauseConfig { Op = "eq", Value = 0 },
            },
        };

        var success = compiler.TryCompile(filter, out var clause, out var diagnostic);

        Assert.True(success);
        Assert.Null(diagnostic);
        Assert.NotNull(clause);
        Assert.Equal(ApplyRuleOperation.Eq, clause!.Operation);
    }

    [Fact]
    public void LegacyApplyRuleMapper_MapsExcludeExisting_WhenTrue()
    {
        var filter = new PhaseAttributeFilter
        {
            TargetAttribute = "exclude_existing",
            ValueType = PhaseValueType.Boolean,
            Value = "true",
        };

        var mapped = LegacyApplyRuleMapper.TryMap(filter, out var clause);

        Assert.True(mapped);
        Assert.NotNull(clause);
        Assert.Equal("PT_INFO_BESTAND", clause!.Field);
        Assert.Equal(ApplyRuleOperation.Neq, clause.Operation);
        Assert.False(clause.UsesInputValue);
        Assert.Single(clause.LiteralValues);
        Assert.Equal("1", clause.LiteralValues[0]);
    }

    [Fact]
    public void LegacyApplyRuleMapper_RecognizesExcludeExisting_WhenFalse_AsNoOp()
    {
        var filter = new PhaseAttributeFilter
        {
            TargetAttribute = "exclude_existing",
            ValueType = PhaseValueType.Boolean,
            Value = "false",
        };

        var mapped = LegacyApplyRuleMapper.TryMap(filter, out var clause);

        Assert.True(mapped);
        Assert.Null(clause);
    }

    [Fact]
    public void LegacyApplyRuleMapper_MapsExcludeGratings_WhenTrue()
    {
        var filter = new PhaseAttributeFilter
        {
            TargetAttribute = "exclude_gratings",
            ValueType = PhaseValueType.Boolean,
            Value = "true",
            TargetObjectType = PhaseColumnObjectType.Assembly,
        };

        var mapped = LegacyApplyRuleMapper.TryMap(filter, out var clause);

        Assert.True(mapped);
        Assert.NotNull(clause);
        Assert.True(clause!.UseProfileScope);
        Assert.Equal(PhaseColumnObjectType.Assembly, clause.ProfileScopeObjectType);
        Assert.Equal(ApplyRuleOperation.NotStartsWith, clause.Operation);
        Assert.Single(clause.LiteralValues);
        Assert.Equal("GIRO", clause.LiteralValues[0]);
    }

    [Fact]
    public void LegacyApplyRuleMapper_RecognizesExcludeGratings_WhenFalse_AsNoOp()
    {
        var filter = new PhaseAttributeFilter
        {
            TargetAttribute = "exclude_gratings",
            ValueType = PhaseValueType.Boolean,
            Value = "false",
        };

        var mapped = LegacyApplyRuleMapper.TryMap(filter, out var clause);

        Assert.True(mapped);
        Assert.Null(clause);
    }

    [Fact]
    public void ApplyRuleValidator_RejectsOnValue_ForBooleanColumn()
    {
        var validator = new ApplyRuleValidator();
        var rule = new PhaseApplyRuleConfig
        {
            OnValue = new ApplyRuleClauseConfig
            {
                Field = "PROFILE",
                Op = "startsWith",
                Value = "HEB",
            },
        };

        var success = validator.TryNormalize(
            rule,
            columnKey: "exclude_gratings",
            valueType: PhaseValueType.Boolean,
            out var normalized,
            out var diagnostics);

        Assert.False(success);
        Assert.Null(normalized);
        Assert.Contains(
            diagnostics,
            d => d.IndexOf("applyRule.onValue", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void PhaseFilterExpressionBuilder_AddsDiagnostic_ForUnsupportedApplyRuleOp()
    {
        var builder = new PhaseFilterExpressionBuilder();
        var selection = new[]
        {
            new PhaseSelectionCriteria
            {
                PhaseNumber = 122,
                AttributeFilters = new List<PhaseAttributeFilter>
                {
                    new()
                    {
                        TargetAttribute = "exclude_existing",
                        ValueType = PhaseValueType.Boolean,
                        Value = "true",
                        ApplyRule = new PhaseApplyRuleConfig
                        {
                            OnTrue = new ApplyRuleClauseConfig
                            {
                                Field = "PT_INFO_BESTAND",
                                Op = "unsupported_op",
                                Value = 1,
                            },
                        },
                    },
                },
            },
        };

        var result = builder.Build(selection, out var diagnostics);

        Assert.NotNull(result);
        Assert.NotEmpty(diagnostics);
        Assert.Contains(
            diagnostics,
            d => d.IndexOf("unsupported applyRule op", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void PhaseFilterExpressionBuilder_AddsDiagnostic_WhenTargetObjectTypeMissing_ForGenericAttribute()
    {
        var builder = new PhaseFilterExpressionBuilder();
        var selection = new[]
        {
            new PhaseSelectionCriteria
            {
                PhaseNumber = 122,
                AttributeFilters = new List<PhaseAttributeFilter>
                {
                    new()
                    {
                        TargetAttribute = "CUSTOM.HasBooleans",
                        ValueType = PhaseValueType.String,
                        Value = "1",
                    },
                },
            },
        };

        var result = builder.Build(selection, out var diagnostics);

        Assert.NotNull(result);
        Assert.NotEmpty(diagnostics);
        Assert.Contains(
            diagnostics,
            d => d.IndexOf("targetObjectType is required", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void PhaseFilterExpressionBuilder_DoesNotReportDiagnostic_ForDynamicCustomFieldFallback()
    {
        var builder = new PhaseFilterExpressionBuilder();
        var selection = new[]
        {
            new PhaseSelectionCriteria
            {
                PhaseNumber = 122,
                AttributeFilters = new List<PhaseAttributeFilter>
                {
                    new()
                    {
                        TargetObjectType = PhaseColumnObjectType.Part,
                        TargetAttribute = "CUSTOM.HasBooleans",
                        ValueType = PhaseValueType.String,
                        Value = "1",
                    },
                },
            },
        };

        var result = builder.Build(selection, out var diagnostics);

        Assert.NotNull(result);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void PhaseFilterExpressionBuilder_DoesNotReportDiagnostic_ForLegacyExcludeExistingFalse()
    {
        var builder = new PhaseFilterExpressionBuilder();
        var selection = new[]
        {
            new PhaseSelectionCriteria
            {
                PhaseNumber = 122,
                AttributeFilters = new List<PhaseAttributeFilter>
                {
                    new()
                    {
                        TargetAttribute = "exclude_existing",
                        ValueType = PhaseValueType.Boolean,
                        Value = "false",
                    },
                },
            },
        };

        var result = builder.Build(selection, out var diagnostics);

        Assert.NotNull(result);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void PhaseFilterExpressionBuilder_KeepsTeklaFilterScopedToTheEnabledPhase()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "PhaseVisualizerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var filterPath = Path.Combine(tempDirectory, "standard.SObjGrp");
        File.WriteAllText(
            filterPath,
            """
            TITLE_OBJECT_GROUP
            {
                Version= 1.05
                Count= 2
                SECTION_OBJECT_GROUP
                {
                    0
                    1
                    co_part
                    proNAME
                    albl_Name
                    ==
                    albl_Equals
                    BEAM
                    0
                    &&
                }
                SECTION_OBJECT_GROUP
                {
                    0
                    1
                    co_part
                    proCLASS
                    albl_Class
                    ==
                    albl_Equals
                    2
                    0
                    &&
                }
            }
            """);

        try
        {
            var builder = new PhaseFilterExpressionBuilder();
            var selection = new[]
            {
                new PhaseSelectionCriteria
                {
                    PhaseNumber = 1,
                    AttributeFilters = new List<PhaseAttributeFilter>
                    {
                        new()
                        {
                            ValueType = PhaseValueType.Boolean,
                            Value = "true",
                            TeklaFilterName = filterPath,
                        },
                    },
                },
                new PhaseSelectionCriteria
                {
                    PhaseNumber = 2,
                    AttributeFilters = new List<PhaseAttributeFilter>(),
                },
            };

            var result = builder.Build(selection, out var diagnostics);

            Assert.DoesNotContain(
                diagnostics,
                d => d.IndexOf("WARN:", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Equal(2, result.Count);

            var firstPhaseGroup = Assert.IsType<BinaryFilterExpressionCollection>(GetBinaryItemExpression(result[0]));
            var secondPhaseGroup = Assert.IsType<BinaryFilterExpressionCollection>(GetBinaryItemExpression(result[1]));

            Assert.Equal(2, firstPhaseGroup.Count);
            Assert.Single(secondPhaseGroup);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static FilterExpression GetBinaryItemExpression(BinaryFilterExpressionItem item)
    {
        var member = typeof(BinaryFilterExpressionItem).GetProperty("FilterExpression", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? (MemberInfo?)typeof(BinaryFilterExpressionItem).GetField("<FilterExpression>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        return member switch
        {
            PropertyInfo property => Assert.IsAssignableFrom<FilterExpression>(property.GetValue(item)),
            FieldInfo field => Assert.IsAssignableFrom<FilterExpression>(field.GetValue(item)),
            _ => throw new InvalidOperationException("Could not read FilterExpression from BinaryFilterExpressionItem."),
        };
    }
}
