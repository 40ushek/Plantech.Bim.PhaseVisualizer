using Plantech.Bim.PhaseVisualizer.Domain;
using Plantech.Bim.PhaseVisualizer.Rules;
using Plantech.Bim.PhaseVisualizer.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Tekla.Structures.Filtering;
using Tekla.Structures.Filtering.Categories;

namespace Plantech.Bim.PhaseVisualizer.Services;

internal sealed class PhaseFilterExpressionBuilder
{
    private const string TeklaViewFilterExtension = ".SObjGrp";
    private static readonly ApplyRuleCompiler ApplyRuleCompiler = new();

    public BinaryFilterExpressionCollection Build(IReadOnlyCollection<PhaseSelectionCriteria>? selection)
    {
        return Build(selection, out _);
    }

    public BinaryFilterExpressionCollection Build(
        IReadOnlyCollection<PhaseSelectionCriteria>? selection,
        out IReadOnlyList<string> diagnostics)
    {
        var diagnosticList = new List<string>();
        var normalizedSelection = NormalizeSelection(selection, diagnosticList);
        var phaseSelectionExpression = BuildPhaseSelectionExpression(normalizedSelection, diagnosticList);
        diagnostics = diagnosticList;
        return phaseSelectionExpression;
    }

    private static BinaryFilterExpressionCollection BuildPhaseSelectionExpression(
        IReadOnlyList<PhaseSelectionCriteria> normalizedSelection,
        IList<string> diagnostics)
    {
        if (normalizedSelection.Count == 1)
        {
            return BuildPhaseGroup(normalizedSelection[0], diagnostics);
        }

        var phaseSelectionExpression = new BinaryFilterExpressionCollection();
        for (var i = 0; i < normalizedSelection.Count; i++)
        {
            var criteria = normalizedSelection[i];
            var phaseGroup = BuildPhaseGroup(criteria, diagnostics);
            if (phaseGroup.Count == 0)
            {
                continue;
            }

            var operatorType = BinaryFilterOperatorType.BOOLEAN_OR;

            phaseSelectionExpression.Add(new BinaryFilterExpressionItem(
                phaseGroup,
                operatorType));
        }

        return phaseSelectionExpression;
    }

    private static BinaryFilterExpressionCollection BuildPhaseGroup(
        PhaseSelectionCriteria criteria,
        IList<string> diagnostics)
    {
        var phaseGroup = new BinaryFilterExpressionCollection();

        AddAndFilter(
            phaseGroup,
            BuildIntegerExpression(
                new ObjectFilterExpressions.Phase(),
                "equals",
                criteria.PhaseNumber));

        foreach (var attributeFilter in criteria.AttributeFilters)
        {
            AddAndFilter(
                phaseGroup,
                BuildTeklaFilterReferenceExpression(
                    attributeFilter,
                    criteria.PhaseNumber,
                    diagnostics));

            AddAndFilter(
                phaseGroup,
                BuildAttributeExpression(
                    attributeFilter,
                    criteria.PhaseNumber,
                    diagnostics));
        }

        return phaseGroup;
    }

    private static void AddAndFilter(BinaryFilterExpressionCollection target, FilterExpression? expression)
    {
        if (expression == null)
        {
            return;
        }

        target.Add(new BinaryFilterExpressionItem(
            expression,
            BinaryFilterOperatorType.BOOLEAN_AND));
    }

    private static FilterExpression? BuildAttributeExpression(
        PhaseAttributeFilter? attributeFilter,
        int phaseNumber,
        IList<string> diagnostics)
    {
        if (attributeFilter == null || string.IsNullOrWhiteSpace(attributeFilter.TargetAttribute))
        {
            return null;
        }

        var targetAttribute = attributeFilter.TargetAttribute.Trim();

        if (TryBuildConfiguredRuleExpression(attributeFilter, phaseNumber, diagnostics, out var configuredRuleExpression))
        {
            return configuredRuleExpression;
        }

        if (TryBuildLegacyExpression(
                attributeFilter,
                phaseNumber,
                diagnostics,
                out var legacyExpression))
        {
            return legacyExpression;
        }

        return BuildGenericTargetAttributeExpression(attributeFilter, targetAttribute, phaseNumber, diagnostics);
    }

    private static FilterExpression? BuildTeklaFilterReferenceExpression(
        PhaseAttributeFilter attributeFilter,
        int phaseNumber,
        IList<string> diagnostics)
    {
        var filterName = attributeFilter.TeklaFilterName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filterName))
        {
            return null;
        }

        if (!IsTruthy(attributeFilter.Value))
        {
            diagnostics.Add($"INFO: Phase {phaseNumber}: teklaFilter '{filterName}' skipped because value is false.");
            return null;
        }

        if (!TryResolveTeklaFilterPath(
                filterName,
                out var fullPath,
                out var sourceKind,
                out var sourceFolder,
                out var resolveError,
                out var candidates,
                out var probeFolders))
        {
            var probeFoldersText = probeFolders.Count == 0 ? "<none>" : string.Join(" | ", probeFolders);
            diagnostics.Add(
                $"WARN: Phase {phaseNumber}: teklaFilter '{filterName}' not found (sourceKind={sourceKind}, reason={resolveError}), probed folders: {probeFoldersText}, candidates: {string.Join(" | ", candidates)}, filter ignored.");
            return null;
        }

        diagnostics.Add(
            $"INFO: Phase {phaseNumber}: teklaFilter '{filterName}' found (sourceKind={sourceKind}, folder='{sourceFolder}', path='{fullPath}').");

        try
        {
            var filter = new Filter(fullPath, CultureInfo.InvariantCulture);
            var expression = filter.FilterExpression;
            if (expression == null)
            {
                diagnostics.Add($"WARN: Phase {phaseNumber}: teklaFilter '{filterName}' has empty expression, filter ignored.");
                return null;
            }

            if (attributeFilter.TeklaFilterNegate)
            {
                if (!TryNegateFilterExpression(expression, out var negatedExpression, out var negateReason))
                {
                    diagnostics.Add(
                        $"WARN: Phase {phaseNumber}: teklaFilter '{filterName}' negate failed ({negateReason}), filter ignored.");
                    return null;
                }

                diagnostics.Add($"INFO: Phase {phaseNumber}: teklaFilter '{filterName}' applied with negate=true.");
                expression = negatedExpression;
            }

            return expression;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"WARN: Phase {phaseNumber}: teklaFilter '{filterName}' load failed ({ex.Message}), filter ignored.");
            return null;
        }
    }

    private static bool TryNegateFilterExpression(
        FilterExpression expression,
        out FilterExpression negatedExpression,
        out string reason)
    {
        reason = string.Empty;
        negatedExpression = expression;

        if (expression is BinaryFilterExpressionCollection collection)
        {
            var negatedCollection = new BinaryFilterExpressionCollection();
            for (var i = 0; i < collection.Count; i++)
            {
                var item = collection[i];
                if (!TryGetBinaryItemExpression(item, out var childExpression))
                {
                    reason = "cannot read binary item expression";
                    negatedExpression = expression;
                    return false;
                }

                if (!TryNegateFilterExpression(childExpression, out var negatedChild, out reason))
                {
                    negatedExpression = expression;
                    return false;
                }

                var itemOperator = TryGetBinaryItemOperator(item, out var existingOperator)
                    ? existingOperator
                    : BinaryFilterOperatorType.EMPTY;
                var negatedOperator = itemOperator switch
                {
                    BinaryFilterOperatorType.BOOLEAN_AND => BinaryFilterOperatorType.BOOLEAN_OR,
                    BinaryFilterOperatorType.BOOLEAN_OR => BinaryFilterOperatorType.BOOLEAN_AND,
                    _ => itemOperator,
                };

                negatedCollection.Add(new BinaryFilterExpressionItem(negatedChild, negatedOperator));
            }

            negatedExpression = negatedCollection;
            return true;
        }

        if (expression is not BinaryFilterExpression binaryExpression)
        {
            reason = $"unsupported expression type: {expression.GetType().Name}";
            return false;
        }

        if (!TryGetBinaryOperatorName(binaryExpression, out var operatorName))
        {
            reason = "cannot read binary operator";
            return false;
        }

        var invertedOperatorName = InvertOperatorName(operatorName);
        if (string.IsNullOrWhiteSpace(invertedOperatorName))
        {
            reason = $"unsupported operator: {operatorName}";
            return false;
        }

        if (!TryGetBinaryOperands(binaryExpression, out var left, out var right))
        {
            reason = "cannot read binary operands";
            return false;
        }

        if (left is StringFilterExpression stringLeft
            && right is StringConstantFilterExpression stringRight)
        {
            if (!TryParseEnumValue<StringOperatorType>(invertedOperatorName, out var parsedStringOperator))
            {
                reason = $"cannot parse string operator: {invertedOperatorName}";
                return false;
            }

            negatedExpression = new BinaryFilterExpression(
                stringLeft,
                parsedStringOperator,
                stringRight);
            return true;
        }

        if (left is NumericFilterExpression numericLeft
            && right is NumericConstantFilterExpression numericRight)
        {
            if (!TryParseEnumValue<NumericOperatorType>(invertedOperatorName, out var parsedNumericOperator))
            {
                reason = $"cannot parse numeric operator: {invertedOperatorName}";
                return false;
            }

            negatedExpression = new BinaryFilterExpression(
                numericLeft,
                parsedNumericOperator,
                numericRight);
            return true;
        }

        if (left is BooleanFilterExpression booleanLeft
            && right is BooleanConstantFilterExpression booleanRight)
        {
            if (!TryParseEnumValue<BooleanOperatorType>(invertedOperatorName, out var parsedBooleanOperator))
            {
                reason = $"cannot parse boolean operator: {invertedOperatorName}";
                return false;
            }

            negatedExpression = new BinaryFilterExpression(
                booleanLeft,
                parsedBooleanOperator,
                booleanRight);
            return true;
        }

        if (left is DateTimeFilterExpression dateTimeLeft
            && right is DateTimeConstantFilterExpression dateTimeRight)
        {
            if (!TryParseEnumValue<DateTimeOperatorType>(invertedOperatorName, out var parsedDateTimeOperator))
            {
                reason = $"cannot parse datetime operator: {invertedOperatorName}";
                return false;
            }

            negatedExpression = new BinaryFilterExpression(
                dateTimeLeft,
                parsedDateTimeOperator,
                dateTimeRight);
            return true;
        }

        reason = $"unsupported binary operand types: {left.GetType().Name}/{right.GetType().Name}";
        return false;
    }

    private static string InvertOperatorName(string input)
    {
        var normalized = (input ?? string.Empty).Trim();
        return normalized switch
        {
            "IS_EQUAL" => "IS_NOT_EQUAL",
            "IS_NOT_EQUAL" => "IS_EQUAL",
            "CONTAINS" => "NOT_CONTAINS",
            "NOT_CONTAINS" => "CONTAINS",
            "STARTS_WITH" => "NOT_STARTS_WITH",
            "NOT_STARTS_WITH" => "STARTS_WITH",
            "ENDS_WITH" => "NOT_ENDS_WITH",
            "NOT_ENDS_WITH" => "ENDS_WITH",
            "SMALLER_THAN" => "GREATER_OR_EQUAL",
            "SMALLER_OR_EQUAL" => "GREATER_THAN",
            "GREATER_THAN" => "SMALLER_OR_EQUAL",
            "GREATER_OR_EQUAL" => "SMALLER_THAN",
            "EARLIER_THAN" => "LATER_OR_EQUAL",
            "EARLIER_OR_EQUAL" => "LATER_THAN",
            "LATER_THAN" => "EARLIER_OR_EQUAL",
            "LATER_OR_EQUAL" => "EARLIER_THAN",
            "BOOLEAN_AND" => "BOOLEAN_OR",
            "BOOLEAN_OR" => "BOOLEAN_AND",
            _ => string.Empty,
        };
    }

    private static bool TryGetBinaryItemExpression(BinaryFilterExpressionItem item, out FilterExpression expression)
    {
        expression = default!;
        if (item == null)
        {
            return false;
        }

        var value = GetMemberValue(item, "FilterExpression", "<FilterExpression>k__BackingField");
        if (value is FilterExpression filterExpression)
        {
            expression = filterExpression;
            return true;
        }

        return false;
    }

    private static bool TryGetBinaryItemOperator(BinaryFilterExpressionItem item, out BinaryFilterOperatorType operatorType)
    {
        operatorType = BinaryFilterOperatorType.EMPTY;
        if (item == null)
        {
            return false;
        }

        var value = GetMemberValue(item, "BinaryFilterItemOperatorType", "<BinaryFilterItemOperatorType>k__BackingField");
        if (value is BinaryFilterOperatorType binaryOperatorType)
        {
            operatorType = binaryOperatorType;
            return true;
        }

        return false;
    }

    private static bool TryGetBinaryOperands(BinaryFilterExpression expression, out object left, out object right)
    {
        left = default!;
        right = default!;

        if (expression == null)
        {
            return false;
        }

        var leftValue = GetMemberValue(expression, "Left", "<Left>k__BackingField");
        var rightValue = GetMemberValue(expression, "Right", "<Right>k__BackingField");
        if (leftValue == null || rightValue == null)
        {
            return false;
        }

        left = leftValue;
        right = rightValue;
        return true;
    }

    private static bool TryGetBinaryOperatorName(BinaryFilterExpression expression, out string operatorName)
    {
        operatorName = string.Empty;
        if (expression == null)
        {
            return false;
        }

        var value = GetMemberValue(expression, "Operator", "<Operator>k__BackingField");
        if (value == null)
        {
            return false;
        }

        operatorName = value.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(operatorName);
    }

    private static object? GetMemberValue(object source, string propertyName, string fieldName)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var property = source.GetType().GetProperty(propertyName, Flags);
        if (property != null)
        {
            return property.GetValue(source, null);
        }

        var field = source.GetType().GetField(fieldName, Flags);
        if (field != null)
        {
            return field.GetValue(source);
        }

        return null;
    }

    private static bool TryParseEnumValue<TEnum>(string name, out TEnum value)
        where TEnum : struct
    {
        if (Enum.TryParse(name, ignoreCase: true, out TEnum parsed))
        {
            value = parsed;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryResolveTeklaFilterPath(
        string filterName,
        out string fullPath,
        out string sourceKind,
        out string sourceFolder,
        out string error,
        out IReadOnlyList<string> candidates,
        out IReadOnlyList<string> probeFolders)
    {
        fullPath = string.Empty;
        sourceKind = Path.IsPathRooted(filterName) ? "absolute-path" : "tekla-attribute-directories";
        sourceFolder = string.Empty;
        error = string.Empty;

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
            var modelPath = LazyModelConnector.ModelInstance?.GetInfo()?.ModelPath;
            var searchDirectories = TeklaAttributeDirectories.GetSearchDirectories(modelPath);
            if (searchDirectories.Count == 0)
            {
                error = "attribute search directories unavailable";
                candidates = Array.Empty<string>();
                probeFolders = Array.Empty<string>();
                return false;
            }

            foreach (var directory in searchDirectories)
            {
                candidatePaths.Add(Path.Combine(directory, filterName));
                if (!Path.HasExtension(filterName))
                {
                    candidatePaths.Add(Path.Combine(directory, filterName + TeklaViewFilterExtension));
                }
            }
        }

        var distinctCandidates = candidatePaths
                     .Where(c => !string.IsNullOrWhiteSpace(c))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .ToList();
        candidates = distinctCandidates;
        probeFolders = distinctCandidates
            .Select(Path.GetDirectoryName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var candidate in distinctCandidates)
        {
            if (File.Exists(candidate))
            {
                fullPath = candidate;
                sourceFolder = Path.GetDirectoryName(candidate) ?? string.Empty;
                return true;
            }
        }

        error = "file not found";
        return false;
    }

    private static bool TryBuildLegacyExpression(
        PhaseAttributeFilter attributeFilter,
        int phaseNumber,
        IList<string> diagnostics,
        out FilterExpression? expression)
    {
        expression = null;

        if (TryBuildLegacyMappedExpression(attributeFilter, phaseNumber, diagnostics, out var mappedExpression))
        {
            expression = mappedExpression;
            return true;
        }

        return false;
    }

    private static bool TryBuildConfiguredRuleExpression(
        PhaseAttributeFilter attributeFilter,
        int phaseNumber,
        IList<string> diagnostics,
        out FilterExpression? expression)
    {
        expression = null;
        if (attributeFilter.ApplyRule == null)
        {
            return false;
        }

        if (!ApplyRuleCompiler.TryCompile(attributeFilter, out var compiledClause, out var compileDiagnostic))
        {
            if (!string.IsNullOrWhiteSpace(compileDiagnostic))
            {
                diagnostics.Add($"Phase {phaseNumber}: {compileDiagnostic}");
            }

            return true;
        }

        if (compiledClause == null)
        {
            return true;
        }

        expression = BuildRuntimeClauseExpression(compiledClause, attributeFilter.ValueType, attributeFilter.Value, phaseNumber, diagnostics);
        return true;
    }

    private static bool TryBuildLegacyMappedExpression(
        PhaseAttributeFilter attributeFilter,
        int phaseNumber,
        IList<string> diagnostics,
        out FilterExpression? expression)
    {
        expression = null;
        if (!LegacyApplyRuleMapper.TryMap(attributeFilter, out var mappedLegacyClause))
        {
            return false;
        }

        if (mappedLegacyClause == null)
        {
            return true;
        }

        expression = BuildRuntimeClauseExpression(
            mappedLegacyClause,
            attributeFilter.ValueType,
            attributeFilter.Value,
            phaseNumber,
            diagnostics);
        return true;
    }

    private static FilterExpression? BuildGenericTargetAttributeExpression(
        PhaseAttributeFilter attributeFilter,
        string targetAttribute,
        int phaseNumber,
        IList<string> diagnostics)
    {
        if (!TryResolveGenericTemplateField(
                attributeFilter,
                targetAttribute,
                phaseNumber,
                diagnostics,
                out var templateField))
        {
            return null;
        }

        var values = ParseStringValues(attributeFilter.Value ?? string.Empty);
        if (values.Count == 0)
        {
            return null;
        }

        var operation = values.Count > 1 ? "in" : "equals";
        return BuildStringExpression(
            new TemplateFilterExpressions.CustomString(templateField),
            operation,
            values);
    }

    private static bool TryResolveGenericTemplateField(
        PhaseAttributeFilter attributeFilter,
        string targetAttribute,
        int phaseNumber,
        IList<string> diagnostics,
        out string templateField)
    {
        templateField = string.Empty;
        if (!attributeFilter.TargetObjectType.HasValue)
        {
            diagnostics.Add(
                $"Phase {phaseNumber}: targetObjectType is required for '{attributeFilter.TargetAttribute}', filter ignored.");
            return false;
        }

        if (TryResolveTemplateStringField(
                attributeFilter.TargetObjectType.Value,
                targetAttribute,
                out templateField))
        {
            return true;
        }

        diagnostics.Add(
            $"Phase {phaseNumber}: invalid target '{attributeFilter.TargetObjectType}.{attributeFilter.TargetAttribute}', filter ignored.");
        return false;
    }

    private static bool TryResolveTemplateStringField(
        PhaseColumnObjectType targetObjectType,
        string targetAttribute,
        out string templateField)
    {
        templateField = string.Empty;

        if (PhaseSourceResolver.TryGetTemplateStringField(
                targetObjectType,
                targetAttribute.Trim(),
                out templateField))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(targetAttribute))
        {
            return false;
        }

        // Fallback for dynamic/custom Tekla fields configured directly in JSON.
        templateField = targetAttribute.Trim();
        return true;
    }

    private static FilterExpression? BuildRuntimeClauseExpression(
        ApplyRuleClauseRuntime clause,
        PhaseValueType valueType,
        string? inputValue,
        int phaseNumber,
        IList<string> diagnostics)
    {
        var values = ResolveRuleValues(clause, inputValue);
        if (values.Count == 0)
        {
            diagnostics.Add($"Phase {phaseNumber}: applyRule for field '{clause.Field}' has no usable value.");
            return null;
        }

        if (clause.UseProfileScope)
        {
            return BuildScopedProfileStringExpression(
                clause.ProfileScopeObjectType,
                ConvertOperationToString(clause.Operation),
                values);
        }

        if (TryBuildStrictNumericRuleExpression(clause, values, phaseNumber, diagnostics, out var strictNumericExpression))
        {
            return strictNumericExpression;
        }

        if (TryBuildPreferredNumericRuleExpression(clause, valueType, values, out var preferredNumericExpression))
        {
            return preferredNumericExpression;
        }

        return BuildStringExpression(
            new TemplateFilterExpressions.CustomString(clause.Field),
            ConvertOperationToString(clause.Operation),
            values);
    }

    private static bool TryBuildStrictNumericRuleExpression(
        ApplyRuleClauseRuntime clause,
        IReadOnlyList<string> values,
        int phaseNumber,
        IList<string> diagnostics,
        out FilterExpression? expression)
    {
        expression = null;
        if (!IsNumericOperation(clause.Operation))
        {
            return false;
        }

        if (!TryParseNumericValues(values, out var numericValues))
        {
            diagnostics.Add(
                $"Phase {phaseNumber}: applyRule for field '{clause.Field}' expects numeric value(s), got '{string.Join(", ", values)}'.");
            return true;
        }

        expression = BuildNumberExpression(
            new TemplateFilterExpressions.CustomNumber(clause.Field),
            clause.Operation,
            numericValues);
        return true;
    }

    private static bool TryBuildPreferredNumericRuleExpression(
        ApplyRuleClauseRuntime clause,
        PhaseValueType valueType,
        IReadOnlyList<string> values,
        out FilterExpression? expression)
    {
        expression = null;
        if (clause.Operation is not (ApplyRuleOperation.Eq or ApplyRuleOperation.Neq or ApplyRuleOperation.In))
        {
            return false;
        }

        var preferNumeric = valueType is PhaseValueType.Integer or PhaseValueType.Number or PhaseValueType.Boolean;
        if (!preferNumeric || !TryParseNumericValues(values, out var numericValues))
        {
            return false;
        }

        expression = BuildNumberExpression(
            new TemplateFilterExpressions.CustomNumber(clause.Field),
            clause.Operation,
            numericValues);
        return true;
    }

    private static IReadOnlyList<string> ResolveRuleValues(ApplyRuleClauseRuntime clause, string? inputValue)
    {
        if (!clause.UsesInputValue)
        {
            if (clause.Operation == ApplyRuleOperation.In && clause.LiteralValues.Count == 1)
            {
                return ParseStringValues(clause.LiteralValues[0]);
            }

            return clause.LiteralValues
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        var normalizedInput = inputValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return Array.Empty<string>();
        }

        if (clause.Operation == ApplyRuleOperation.In)
        {
            return ParseStringValues(normalizedInput);
        }

        return new[] { normalizedInput };
    }

    private static bool TryParseNumericValues(IReadOnlyList<string> values, out IReadOnlyList<double> parsed)
    {
        var result = new List<double>();
        foreach (var value in values)
        {
            if (double.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var numericValue))
            {
                result.Add(numericValue);
                continue;
            }

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(1d);
                continue;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(0d);
                continue;
            }

            parsed = Array.Empty<double>();
            return false;
        }

        parsed = result;
        return result.Count > 0;
    }

    private static FilterExpression BuildNumberExpression(
        NumericFilterExpression expression,
        ApplyRuleOperation operation,
        IReadOnlyList<double> values)
    {
        if (operation == ApplyRuleOperation.In)
        {
            return BuildNumberInExpression(expression, values);
        }

        var op = operation switch
        {
            ApplyRuleOperation.Eq => NumericOperatorType.IS_EQUAL,
            ApplyRuleOperation.Neq => NumericOperatorType.IS_NOT_EQUAL,
            ApplyRuleOperation.Gt => NumericOperatorType.GREATER_THAN,
            ApplyRuleOperation.Gte => NumericOperatorType.GREATER_OR_EQUAL,
            ApplyRuleOperation.Lt => NumericOperatorType.SMALLER_THAN,
            ApplyRuleOperation.Lte => NumericOperatorType.SMALLER_OR_EQUAL,
            _ => throw new InvalidOperationException($"Unsupported numeric operation: {operation}"),
        };

        return new BinaryFilterExpression(
            expression,
            op,
            new NumericConstantFilterExpression(values[0]));
    }

    private static FilterExpression BuildNumberInExpression(
        NumericFilterExpression expression,
        IReadOnlyList<double> values)
    {
        if (values.Count == 1)
        {
            return new BinaryFilterExpression(
                expression,
                NumericOperatorType.IS_EQUAL,
                new NumericConstantFilterExpression(values[0]));
        }

        var inGroup = new BinaryFilterExpressionCollection();
        foreach (var value in values)
        {
            inGroup.Add(new BinaryFilterExpressionItem(
                new BinaryFilterExpression(
                    expression,
                    NumericOperatorType.IS_EQUAL,
                    new NumericConstantFilterExpression(value)),
                BinaryFilterOperatorType.BOOLEAN_OR));
        }

        return inGroup;
    }

    private static bool IsNumericOperation(ApplyRuleOperation operation)
    {
        return operation is ApplyRuleOperation.Gt or ApplyRuleOperation.Gte or ApplyRuleOperation.Lt or ApplyRuleOperation.Lte;
    }

    private static string ConvertOperationToString(ApplyRuleOperation operation)
    {
        return operation switch
        {
            ApplyRuleOperation.Eq => "equals",
            ApplyRuleOperation.Neq => "notEquals",
            ApplyRuleOperation.In => "in",
            ApplyRuleOperation.Contains => "contains",
            ApplyRuleOperation.NotContains => "notContains",
            ApplyRuleOperation.StartsWith => "startsWith",
            ApplyRuleOperation.NotStartsWith => "notStartsWith",
            ApplyRuleOperation.EndsWith => "endsWith",
            ApplyRuleOperation.NotEndsWith => "notEndsWith",
            _ => throw new InvalidOperationException($"Unsupported string operation: {operation}"),
        };
    }

    private static FilterExpression? BuildScopedProfileStringExpression(
        PhaseColumnObjectType? targetObjectType,
        string operation,
        IReadOnlyCollection<string> values)
    {
        var profileExpressions = GetProfileExpressions(targetObjectType);
        var builtExpressions = new List<FilterExpression>();
        foreach (var expression in profileExpressions)
        {
            var built = BuildStringExpression(expression, operation, values);
            if (built != null)
            {
                builtExpressions.Add(built);
            }
        }

        if (builtExpressions.Count == 0)
        {
            return null;
        }

        if (builtExpressions.Count == 1)
        {
            return builtExpressions[0];
        }

        var useAnd = NormalizeOperation(operation).StartsWith("not", StringComparison.Ordinal);
        var aggregate = new BinaryFilterExpressionCollection();
        foreach (var built in builtExpressions)
        {
            aggregate.Add(new BinaryFilterExpressionItem(
                built,
                useAnd
                    ? BinaryFilterOperatorType.BOOLEAN_AND
                    : BinaryFilterOperatorType.BOOLEAN_OR));
        }

        return aggregate;
    }

    private static IReadOnlyList<StringFilterExpression> GetProfileExpressions(PhaseColumnObjectType? targetObjectType)
    {
        var partExpression = CreateProfileExpression(PhaseColumnObjectType.Part);
        var assemblyExpression = CreateProfileExpression(PhaseColumnObjectType.Assembly);

        return targetObjectType switch
        {
            PhaseColumnObjectType.Part => partExpression == null
                ? Array.Empty<StringFilterExpression>()
                : new[] { partExpression },
            PhaseColumnObjectType.Assembly => assemblyExpression == null
                ? Array.Empty<StringFilterExpression>()
                : new[] { assemblyExpression },
            _ => new StringFilterExpression?[] { partExpression, assemblyExpression }
                .Where(x => x != null)
                .Select(x => x!)
                .ToArray(),
        };
    }

    private static StringFilterExpression? CreateProfileExpression(PhaseColumnObjectType objectType)
    {
        var attribute = objectType == PhaseColumnObjectType.Assembly
            ? "ASSEMBLY.MAINPART.PROFILE"
            : "profile";

        if (!PhaseSourceResolver.TryGetTemplateStringField(objectType, attribute, out var templateField))
        {
            return null;
        }

        return new TemplateFilterExpressions.CustomString(templateField);
    }

    private static FilterExpression? BuildStringExpression(
        StringFilterExpression expression,
        string operation,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return BuildStringExpression(
            expression,
            operation,
            new[] { value });
    }

    private static FilterExpression? BuildStringExpression(
        StringFilterExpression expression,
        string operation,
        IReadOnlyCollection<string> values)
    {
        var normalizedOperation = NormalizeOperation(operation);
        var normalizedValues = values?
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? new List<string>();
        if (normalizedValues.Count == 0)
        {
            return null;
        }

        if (normalizedOperation == "in")
        {
            return BuildStringInExpression(expression, normalizedValues);
        }

        var op = normalizedOperation switch
        {
            "equals" => StringOperatorType.IS_EQUAL,
            "notequals" => StringOperatorType.IS_NOT_EQUAL,
            "contains" => StringOperatorType.CONTAINS,
            "notcontains" => StringOperatorType.NOT_CONTAINS,
            "startswith" => StringOperatorType.STARTS_WITH,
            "notstartswith" => StringOperatorType.NOT_STARTS_WITH,
            "endswith" => StringOperatorType.ENDS_WITH,
            "notendswith" => StringOperatorType.NOT_ENDS_WITH,
            _ => throw new InvalidOperationException($"Unsupported string operation: {operation}"),
        };

        return new BinaryFilterExpression(
            expression,
            op,
            new StringConstantFilterExpression(normalizedValues[0]));
    }

    private static FilterExpression BuildStringInExpression(
        StringFilterExpression expression,
        IReadOnlyList<string> values)
    {
        if (values.Count == 1)
        {
            return new BinaryFilterExpression(
                expression,
                StringOperatorType.IS_EQUAL,
                new StringConstantFilterExpression(values[0]));
        }

        var inGroup = new BinaryFilterExpressionCollection();
        foreach (var value in values)
        {
            inGroup.Add(new BinaryFilterExpressionItem(
                new BinaryFilterExpression(
                    expression,
                    StringOperatorType.IS_EQUAL,
                    new StringConstantFilterExpression(value)),
                BinaryFilterOperatorType.BOOLEAN_OR));
        }

        return inGroup;
    }

    private static FilterExpression BuildIntegerExpression(
        NumericFilterExpression expression,
        string operation,
        int value)
    {
        return BuildIntegerExpression(
            expression,
            operation,
            new[] { value });
    }

    private static FilterExpression BuildIntegerExpression(
        NumericFilterExpression expression,
        string operation,
        IReadOnlyCollection<int> values)
    {
        var normalizedOperation = NormalizeOperation(operation);
        var normalizedValues = values
            .Distinct()
            .ToList();
        if (normalizedValues.Count == 0)
        {
            throw new InvalidOperationException("Integer expression requires at least one value.");
        }

        if (normalizedOperation == "in")
        {
            return BuildIntegerInExpression(expression, normalizedValues);
        }

        var op = normalizedOperation switch
        {
            "equals" => NumericOperatorType.IS_EQUAL,
            "notequals" => NumericOperatorType.IS_NOT_EQUAL,
            "gt" => NumericOperatorType.GREATER_THAN,
            "gte" => NumericOperatorType.GREATER_OR_EQUAL,
            "lt" => NumericOperatorType.SMALLER_THAN,
            "lte" => NumericOperatorType.SMALLER_OR_EQUAL,
            _ => throw new InvalidOperationException($"Unsupported integer operation: {operation}"),
        };

        return new BinaryFilterExpression(
            expression,
            op,
            new NumericConstantFilterExpression(normalizedValues[0]));
    }

    private static FilterExpression BuildIntegerInExpression(
        NumericFilterExpression expression,
        IReadOnlyList<int> values)
    {
        if (values.Count == 1)
        {
            return new BinaryFilterExpression(
                expression,
                NumericOperatorType.IS_EQUAL,
                new NumericConstantFilterExpression(values[0]));
        }

        var inGroup = new BinaryFilterExpressionCollection();
        foreach (var value in values)
        {
            inGroup.Add(new BinaryFilterExpressionItem(
                new BinaryFilterExpression(
                    expression,
                    NumericOperatorType.IS_EQUAL,
                    new NumericConstantFilterExpression(value)),
                BinaryFilterOperatorType.BOOLEAN_OR));
        }

        return inGroup;
    }

    private static string NormalizeOperation(string operation)
    {
        return operation?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static List<string> ParseStringValues(string input)
    {
        return input
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsTruthy(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<PhaseSelectionCriteria> NormalizeSelection(
        IReadOnlyCollection<PhaseSelectionCriteria>? selection,
        IList<string> diagnostics)
    {
        if (selection == null || selection.Count == 0)
        {
            return Array.Empty<PhaseSelectionCriteria>();
        }

        return selection
            .Where(x => x != null)
            .GroupBy(x => x.PhaseNumber)
            .Select(g =>
            {
                if (g.Key <= 0)
                {
                    diagnostics.Add($"Phase {g.Key}: invalid phase number, criteria ignored.");
                    return null;
                }

                var attributeFilters = g
                    .SelectMany(x => x.AttributeFilters ?? Enumerable.Empty<PhaseAttributeFilter>())
                    .Where(x => x != null
                        && (!string.IsNullOrWhiteSpace(x.TargetAttribute)
                            || !string.IsNullOrWhiteSpace(x.TeklaFilterName))
                        && !string.IsNullOrWhiteSpace(x.Value))
                        .GroupBy(
                            x => FormattableString.Invariant(
                            $"{x.TargetObjectType}:{x.TargetAttribute}:{x.TeklaFilterName}:{x.TeklaFilterNegate}:{x.BooleanMode}:{x.ValueType}:{x.Value}:{BuildApplyRuleSignature(x.ApplyRule)}"),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToList();

                return new PhaseSelectionCriteria
                {
                    PhaseNumber = g.Key,
                    AttributeFilters = attributeFilters,
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .OrderBy(x => x.PhaseNumber)
            .ToList();
    }

    private static string BuildApplyRuleSignature(PhaseApplyRuleConfig? applyRule)
    {
        if (applyRule == null)
        {
            return string.Empty;
        }

        return FormattableString.Invariant(
            $"{BuildApplyRuleClauseSignature(applyRule.OnTrue)}|{BuildApplyRuleClauseSignature(applyRule.OnFalse)}|{BuildApplyRuleClauseSignature(applyRule.OnValue)}");
    }

    private static string BuildApplyRuleClauseSignature(ApplyRuleClauseConfig? clause)
    {
        if (clause == null)
        {
            return string.Empty;
        }

        var value = clause.Value == null
            ? string.Empty
            : string.Join(",", ApplyRuleOperationHelper.ToLiteralValues(clause.Value));
        return FormattableString.Invariant($"{clause.Field}:{clause.Op}:{value}");
    }

}
