namespace io.NET.ZTR_OS.Features.StartupDiagnostics.Models;

public enum DiagnosisSeverity
{
    Info,
    Warning,
    Critical
}

public record StartupDiagnosis(
    DiagnosisSeverity Severity,
    string Description,
    string SuggestedAction,
    string? OneClickFixCommandId = null);
