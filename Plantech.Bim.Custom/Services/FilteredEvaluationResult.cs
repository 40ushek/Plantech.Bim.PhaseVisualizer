namespace Plantech.Bim.Custom.Services;

public sealed class FilteredEvaluationResult
{
    public int ObjectId { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public bool HasModelObject { get; set; }
    public bool HasConfig { get; set; }
    public bool IsMatch { get; set; }
    public int IntegerValue { get; set; }
    public string ConfigFileName { get; set; } = string.Empty;
    public string ConfigFilePath { get; set; } = string.Empty;
    public string ConfigFileContent { get; set; } = string.Empty;
    public string TeklaFilterName { get; set; } = string.Empty;
    public string ResolvedTeklaFilterPath { get; set; } = string.Empty;
    public string ResolvedTeklaFilterContent { get; set; } = string.Empty;
    public bool UsedTeklaFilterPathCache { get; set; }
    public string ReportProperty { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
}
