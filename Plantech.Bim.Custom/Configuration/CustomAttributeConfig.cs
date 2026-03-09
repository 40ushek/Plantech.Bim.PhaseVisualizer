namespace Plantech.Bim.Custom.Configuration;

internal sealed class CustomAttributeConfig
{
    public string TeklaFilterName { get; set; } = string.Empty;
    public string ReportProperty { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public int TrueValue { get; set; } = 1;
    public int FalseValue { get; set; } = 0;
    public bool IgnoreCase { get; set; } = true;
}
