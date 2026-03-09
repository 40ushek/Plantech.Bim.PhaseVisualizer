using Plantech.Bim.Custom.Configuration;
using Plantech.Bim.Custom.Services;
using System;
using System.ComponentModel.Composition;
using Tekla.Structures;
using Tekla.Structures.CustomPropertyPlugin;
using Tekla.Structures.Model;

namespace Plantech.Bim.Custom.Plugins;

[Export(typeof(ICustomPropertyPlugin)), ExportMetadata("CustomProperty", "CUSTOM.PT.Filtered01")]
internal class Filtered : CustomBase, ICustomPropertyPlugin
{
    private static readonly CustomAttributeConfigLoader ConfigLoader = new("filtered01.json");
    private static readonly TeklaFilterObjectMatcher FilterMatcher = new();

    public double GetDoubleProperty(int objectId) => GetIntegerProperty(objectId);

    public int GetIntegerProperty(int objectId)
    {
        var id = new Identifier(objectId);
        var modelObject = _modelInstance.SelectModelObject(id);
        if (modelObject == null)
        {
            return 0;
        }

        var modelPath = _modelInstance.GetInfo()?.ModelPath;
        var config = ConfigLoader.Load(modelPath);
        if (config == null)
        {
            return 0;
        }

        return IsMatch(modelObject, config, modelPath)
            ? config.TrueValue
            : config.FalseValue;
    }

    public string GetStringProperty(int objectId) => GetIntegerProperty(objectId).ToString();

    private static bool IsMatch(ModelObject modelObject, CustomAttributeConfig config, string? modelPath)
    {
        if (!string.IsNullOrWhiteSpace(config.TeklaFilterName))
        {
            return FilterMatcher.IsMatch(config.TeklaFilterName, modelPath, modelObject.Identifier.ID);
        }

        return MatchConfiguredValue(modelObject, config);
    }

    private static bool MatchConfiguredValue(ModelObject modelObject, CustomAttributeConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ReportProperty))
        {
            return false;
        }

        var reportValue = string.Empty;
        if (!modelObject.GetReportProperty(config.ReportProperty, ref reportValue))
        {
            return false;
        }

        var comparison = config.IgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            reportValue?.Trim(),
            config.ExpectedValue?.Trim(),
            comparison);
    }
}
