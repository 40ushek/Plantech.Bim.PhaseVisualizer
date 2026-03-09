using Plantech.Bim.Custom.Configuration;
using Plantech.Bim.Custom.Services;
using System.ComponentModel.Composition;
using Tekla.Structures;
using Tekla.Structures.CustomPropertyPlugin;

namespace Plantech.Bim.Custom.Plugins;

[Export(typeof(ICustomPropertyPlugin)), ExportMetadata("CustomProperty", "CUSTOM.PT.Filtered01")]
internal class Filtered : CustomBase, ICustomPropertyPlugin
{
    private static readonly FilteredEvaluationService EvaluationService = new();

    public double GetDoubleProperty(int objectId) => GetIntegerProperty(objectId);
    public int GetIntegerProperty(int objectId) => EvaluationService.Evaluate(objectId).IntegerValue;
    public string GetStringProperty(int objectId) => GetIntegerProperty(objectId).ToString();
}
