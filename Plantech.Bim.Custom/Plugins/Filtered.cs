using Plantech.Bim.Custom.Services;
using System.ComponentModel.Composition;
using Tekla.Structures;
using Tekla.Structures.CustomPropertyPlugin;

namespace Plantech.Bim.Custom.Plugins;

[Export(typeof(ICustomPropertyPlugin)), ExportMetadata("CustomProperty", "CUSTOM.PT.Filtered01")]
internal class Filtered : CustomBase, ICustomPropertyPlugin
{
    private static readonly FilteredEvaluationService EvaluationService = new();
    private static readonly object SyncRoot = new();
    private static int _cachedObjectId = -1;
    private static int _cachedValue = -1;

    public double GetDoubleProperty(int objectId) => 0;
    public int GetIntegerProperty(int objectId)
    {
        //var identifier = new Identifier(objectId);
        //var modelObject = Common.LazyModelConnector.ModelInstance.SelectModelObject(identifier);
        //string _class = "";
        //modelObject.GetReportProperty("CLASS_ATTR", ref _class);
        //return _class == "2" ? 1 : 0;

        lock (SyncRoot)
        {
            if (_cachedObjectId == objectId)
            {
                return _cachedValue;
            }
        }

        var value = EvaluationService.Evaluate(objectId).IntegerValue;

        lock (SyncRoot)
        {
            _cachedValue = value;
            _cachedObjectId = objectId;
            return _cachedValue;
        }
    }
    public string GetStringProperty(int objectId) => GetIntegerProperty(objectId).ToString();
}
