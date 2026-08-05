using Xunit;
using io.NET.ZTR_OS.Features.StartupDiagnostics.Models;
using io.NET.ZTR_OS.Features.StartupDiagnostics.Services;

namespace io.NET.ZTR_OS.Tests.Services;

public class StartupDiagnosticsTests
{
    [Fact]
    public void LogPatternEngine_Matches_PortConflict()
    {
        var engine = new LogPatternEngine();
        var log = "java.net.BindException: Address already in use: bind\n...\nFailed to bind to port 25565";
        var results = engine.AnalyzeLog(log);
        var match = Assert.Single(results, r => r.OneClickFixCommandId == "kill-port-25565");
        Assert.Equal(DiagnosisSeverity.Critical, match.Severity);
        Assert.Equal("端口25565被占用", match.Description);
    }

    [Fact]
    public void LogPatternEngine_Matches_UnsupportedJavaVersion()
    {
        var engine = new LogPatternEngine();
        var log = "java.lang.UnsupportedClassVersionError: Preview features not enabled for major version 65.0";
        var results = engine.AnalyzeLog(log);
        var match = Assert.Single(results, r => r.OneClickFixCommandId == "switch-java-21");
        Assert.Equal(DiagnosisSeverity.Critical, match.Severity);
        Assert.Equal("需要Java 21", match.Description);
    }

    [Fact]
    public void LogPatternEngine_Matches_Eula()
    {
        var engine = new LogPatternEngine();
        var log = "[Server] ERROR: You need to agree to the EULA in order to run the server";
        var results = engine.AnalyzeLog(log);
        var match = Assert.Single(results, r => r.OneClickFixCommandId == "agree-eula");
        Assert.Equal(DiagnosisSeverity.Critical, match.Severity);
        Assert.Equal("未同意 EULA", match.Description);
    }

    [Fact]
    public void LogPatternEngine_Matches_OOMHeap()
    {
        var engine = new LogPatternEngine();
        var log = "Exception in thread \"main\" java.lang.OutOfMemoryError: Java heap space";
        var results = engine.AnalyzeLog(log);
        var match = Assert.Single(results, r => r.OneClickFixCommandId == "increase-memory-2g");
        Assert.Equal(DiagnosisSeverity.Warning, match.Severity);
    }

    [Fact]
    public void LogPatternEngine_Matches_WorldLocked()
    {
        var engine = new LogPatternEngine();
        var log = "[WARN] World folder lock detected. Is another server running?";
        var results = engine.AnalyzeLog(log);
        var match = Assert.Single(results, r => r.OneClickFixCommandId == "kill-duplicate-process");
        Assert.Equal(DiagnosisSeverity.Warning, match.Severity);
    }

    [Fact]
    public void LogPatternEngine_Matches_AuthlibFailed()
    {
        var engine = new LogPatternEngine();
        var log = "[User Authenticator #1/ERROR]: Failed to verify username";
        var results = engine.AnalyzeLog(log);
        var match = Assert.Single(results, r => r.OneClickFixCommandId == "temp-disable-online-mode");
        Assert.Equal(DiagnosisSeverity.Warning, match.Severity);
    }

    [Fact]
    public void JavaCompatibilityChecker_Paper1_21_RequiresJava21()
    {
        var checker = new JavaCompatibilityChecker();
        var result = checker.Check("paper", "1.21.1", 17);
        Assert.NotNull(result);
        Assert.Equal(DiagnosisSeverity.Warning, result!.Severity);
        Assert.Equal("Paper 1.21.x 建议 Java 21，当前 Java 17", result.Description);
    }

    [Fact]
    public async Task StartupDiagnosticService_AggregateOrders_BySeverity()
    {
        var service = new StartupDiagnosticService(
            new LogPatternEngine(),
            new JavaCompatibilityChecker(),
            new PortConflictDetector());
        var fakeLog = @"
java.net.BindException: Address already in use: bind
[WARN] World folder lock detected
";
        var results = await service.RunAllAsync(fakeLog, "paper", "1.20.4", 17, 25565);
        var ordered = results.ToList();
        var severities = ordered.Select(x => x.Severity).ToList();
        for (int i = 1; i < severities.Count; i++)
        {
            int prev = severities[i - 1] switch
            {
                DiagnosisSeverity.Critical => 3,
                DiagnosisSeverity.Warning => 2,
                DiagnosisSeverity.Info => 1,
                _ => 0
            };
            int curr = severities[i] switch
            {
                DiagnosisSeverity.Critical => 3,
                DiagnosisSeverity.Warning => 2,
                DiagnosisSeverity.Info => 1,
                _ => 0
            };
            Assert.True(prev >= curr, $"排序错误：{severities[i - 1]} 不应在 {severities[i]} 之前");
        }
    }
}
