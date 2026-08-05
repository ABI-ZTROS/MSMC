using io.NET.ZTR_OS.Features.StartupDiagnostics.Models;

namespace io.NET.ZTR_OS.Features.StartupDiagnostics.Services;

public class StartupDiagnosticService
{
    private readonly LogPatternEngine _logPatternEngine;
    private readonly JavaCompatibilityChecker _javaChecker;
    private readonly PortConflictDetector _portDetector;

    public StartupDiagnosticService(
        LogPatternEngine logPatternEngine,
        JavaCompatibilityChecker javaChecker,
        PortConflictDetector portDetector)
    {
        _logPatternEngine = logPatternEngine;
        _javaChecker = javaChecker;
        _portDetector = portDetector;
    }

    public async Task<List<StartupDiagnosis>> RunAllAsync(
        string logTail,
        string coreType,
        string mcVersion,
        int javaMajor,
        int? port = 25565)
    {
        var all = new List<StartupDiagnosis>();

        var logMatches = _logPatternEngine.AnalyzeLog(logTail);
        all.AddRange(logMatches);

        var javaDiag = _javaChecker.Check(coreType, mcVersion, javaMajor);
        if (javaDiag != null)
            all.Add(javaDiag);

        if (port.HasValue)
        {
            var portDiag = await _portDetector.CheckPortAsync(port.Value);
            if (portDiag != null)
                all.Add(portDiag);
        }

        return all
            .OrderByDescending(d => d.Severity switch
            {
                DiagnosisSeverity.Critical => 3,
                DiagnosisSeverity.Warning => 2,
                DiagnosisSeverity.Info => 1,
                _ => 0
            })
            .ToList();
    }
}
