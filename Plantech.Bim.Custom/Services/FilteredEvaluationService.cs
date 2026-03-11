using Plantech.Bim.Custom.Common;
using Plantech.Bim.Custom.Configuration;
using System;
using Tekla.Structures;
using Tekla.Structures.Model;

namespace Plantech.Bim.Custom.Services;

public sealed class FilteredEvaluationService
{
    private const string ConfigFileName = "filtered01.json";

    private readonly CustomPropertyConfigLoader _configLoader = new(ConfigFileName);
    private readonly TeklaFilterObjectMatcher _filterMatcher = new();
    private static Model ModelInstance => LazyModelConnector.ModelInstance;

    private static readonly TimeSpan ModelPathCacheWindow = TimeSpan.FromSeconds(2);
    private string? _cachedModelPath;
    private DateTime _cachedModelPathUtc = DateTime.MinValue;

    private string? GetModelPath()
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc - _cachedModelPathUtc > ModelPathCacheWindow)
        {
            _cachedModelPath = ModelInstance.GetInfo()?.ModelPath;
            _cachedModelPathUtc = nowUtc;
        }
        return _cachedModelPath;
    }

    public static bool IsTeklaConnected()
    {
        try
        {
            return ModelInstance.GetConnectionStatus();
        }
        catch
        {
            return false;
        }
    }

    public FilteredEvaluationResult Evaluate(int objectId)
    {
        var identifier = new Identifier(objectId);
        var modelObject = ModelInstance.SelectModelObject(identifier);
        if (modelObject == null)
        {
            return new FilteredEvaluationResult
            {
                ObjectId = objectId,
                ConfigFileName = ConfigFileName,
                FailureReason = "Model object was not found.",
            };
        }

        var modelPath = GetModelPath();
        var configSnapshot = _configLoader.LoadSnapshot(modelPath);
        var config = configSnapshot.Config;
        if (config == null)
        {
            return new FilteredEvaluationResult
            {
                ObjectId = objectId,
                ObjectType = modelObject.GetType().Name,
                HasModelObject = true,
                ConfigFileName = ConfigFileName,
                ConfigFilePath = configSnapshot.ConfigPath,
                FailureReason = "Config file was not found or could not be parsed.",
            };
        }

        if (!string.IsNullOrWhiteSpace(config.TeklaFilterName))
        {
            var isMatch = _filterMatcher.TryMatch(
                config.TeklaFilterName,
                modelPath,
                modelObject,
                out var resolvedFilterPath,
                out var usedExpressionCache);

            return new FilteredEvaluationResult
            {
                ObjectId = modelObject.Identifier.ID,
                ObjectType = modelObject.GetType().Name,
                HasModelObject = true,
                HasConfig = true,
                IsMatch = isMatch,
                IntegerValue = isMatch ? config.TrueValue : config.FalseValue,
                ConfigFileName = ConfigFileName,
                ConfigFilePath = configSnapshot.ConfigPath,
                TeklaFilterName = config.TeklaFilterName,
                ResolvedTeklaFilterPath = resolvedFilterPath,
                UsedTeklaFilterExpressionCache = usedExpressionCache,
                FailureReason = isMatch || !string.IsNullOrWhiteSpace(resolvedFilterPath)
                    ? string.Empty
                    : "Tekla filter file was not found or did not include the object.",
            };
        }

        var actualValue = ReadReportProperty(modelObject, config.ReportProperty);
        var isValueMatch = MatchConfiguredValue(actualValue, config);
        return new FilteredEvaluationResult
        {
            ObjectId = modelObject.Identifier.ID,
            ObjectType = modelObject.GetType().Name,
            HasModelObject = true,
            HasConfig = true,
            IsMatch = isValueMatch,
            IntegerValue = isValueMatch ? config.TrueValue : config.FalseValue,
            ConfigFileName = ConfigFileName,
            ConfigFilePath = configSnapshot.ConfigPath,
            ReportProperty = config.ReportProperty,
            ExpectedValue = config.ExpectedValue,
            ActualValue = actualValue,
            FailureReason = isValueMatch || !string.IsNullOrWhiteSpace(actualValue) || !string.IsNullOrWhiteSpace(config.ReportProperty)
                ? string.Empty
                : "Configured report property was not available on the selected object.",
        };
    }

    private static string ReadReportProperty(ModelObject modelObject, string reportProperty)
    {
        if (string.IsNullOrWhiteSpace(reportProperty))
        {
            return string.Empty;
        }

        var reportValue = string.Empty;
        return modelObject.GetReportProperty(reportProperty, ref reportValue)
            ? reportValue ?? string.Empty
            : string.Empty;
    }

    private static bool MatchConfiguredValue(string actualValue, CustomPropertyConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ReportProperty))
        {
            return false;
        }

        var comparison = config.IgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            actualValue?.Trim(),
            config.ExpectedValue?.Trim(),
            comparison);
    }
}
