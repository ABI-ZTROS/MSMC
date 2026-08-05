namespace io.NET.ZTR_OS.Features.ConfigPreview.Models;

public enum ImpactSeverity
{
    High = 0,
    Medium = 1,
    Info = 2,
}

public class ConfigImpactSummary
{
    public string Key { get; set; } = string.Empty;
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
    public ImpactSeverity ImpactSeverity { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
}
