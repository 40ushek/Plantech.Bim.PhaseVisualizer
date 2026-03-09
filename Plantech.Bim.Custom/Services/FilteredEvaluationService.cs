using Plantech.Bim.Custom.Configuration;
using System;
using System.IO;
using Tekla.Structures;
using Tekla.Structures.Model;

namespace Plantech.Bim.Custom.Services;

public sealed class FilteredEvaluationService
{
    private const string ConfigFileName = "filtered01.json";

    private readonly Model _model = new();
    private readonly CustomAttributeConfigLoader _configLoader = new(ConfigFileName);
    private readonly TeklaFilterObjectMatcher _filterMatcher = new();

    public static bool IsTeklaConnected()
    {
        try
        {
            return new Model().GetConnectionStatus();
        }
        catch
        {
            return false;
        }
    }

    public FilteredEvaluationResult Evaluate(int objectId)
    {
        var identifier = new Identifier(objectId);
        var modelObject = _model.SelectModelObject(identifier);
        if (modelObject == null)
        {
            return new FilteredEvaluationResult
            {
                ObjectId = objectId,
                ConfigFileName = ConfigFileName,
                FailureReason = "Model object was not found.",
            };
        }

        var modelPath = _model.GetInfo()?.ModelPath;
        var config = _configLoader.Load(modelPath);
        var configFilePath = ResolveConfigFilePath(modelPath);
        if (config == null)
        {
            return new FilteredEvaluationResult
            {
                ObjectId = objectId,
                ObjectType = modelObject.GetType().Name,
                HasModelObject = true,
                ConfigFileName = ConfigFileName,
                ConfigFilePath = configFilePath,
                FailureReason = "Config file was not found or could not be parsed.",
            };
        }

        if (!string.IsNullOrWhiteSpace(config.TeklaFilterName))
        {
            var isMatch = _filterMatcher.TryMatch(
                config.TeklaFilterName,
                modelPath,
                modelObject.Identifier.ID,
                out var resolvedFilterPath);

            return new FilteredEvaluationResult
            {
                ObjectId = modelObject.Identifier.ID,
                ObjectType = modelObject.GetType().Name,
                HasModelObject = true,
                HasConfig = true,
                IsMatch = isMatch,
                IntegerValue = isMatch ? config.TrueValue : config.FalseValue,
                ConfigFileName = ConfigFileName,
                ConfigFilePath = configFilePath,
                TeklaFilterName = config.TeklaFilterName,
                ResolvedTeklaFilterPath = resolvedFilterPath,
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
            ConfigFilePath = configFilePath,
            ReportProperty = config.ReportProperty,
            ExpectedValue = config.ExpectedValue,
            ActualValue = actualValue,
            FailureReason = isValueMatch || !string.IsNullOrWhiteSpace(actualValue) || !string.IsNullOrWhiteSpace(config.ReportProperty)
                ? string.Empty
                : "Configured report property was not available on the selected object.",
        };
    }

    private string ResolveConfigFilePath(string? modelPath)
    {
        foreach (var candidate in CustomConfigPaths.EnumerateCandidatePaths(ConfigFileName, modelPath))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
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

    private static bool MatchConfiguredValue(string actualValue, CustomAttributeConfig config)
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
