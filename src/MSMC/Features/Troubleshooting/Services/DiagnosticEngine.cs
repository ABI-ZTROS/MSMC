// -----------------------------------------------------------------------------
// DiagnosticEngine.cs — 疑难解答引擎协调器（扩展后）
// 职责: RunQuickAsync（只跑 CheckRunner） / RunDeepAsync（加存档扫描） / 委托 FixExecutor
// P4 诚实返回链: 任何步骤异常 → Succeeded=false + ErrorMessage 可见
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public class DiagnosticEngine : IDiagnosticEngine
{
    private readonly ICheckRunner _checkRunner;
    private readonly IDiagnosticArchiveAnalyzer _archiveAnalyzer;
    private readonly IFixExecutor _fixExecutor;
    private readonly IDeepSeekService _deepSeek;
    private readonly ILogger _log;

    public DiagnosticEngine(
        ICheckRunner checkRunner,
        IDiagnosticArchiveAnalyzer archiveAnalyzer,
        IFixExecutor fixExecutor,
        IDeepSeekService deepSeek,
        ILogger log)
    {
        _checkRunner = checkRunner;
        _archiveAnalyzer = archiveAnalyzer;
        _fixExecutor = fixExecutor;
        _deepSeek = deepSeek;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════
    // 分阶段扫描入口
    // ═══════════════════════════════════════════════════════════

    public Task<DiagnosticReport> RunDiagnosticAsync(string serverJarPath, string? worldPath, CancellationToken ct = default)
        => RunQuickAsync(serverJarPath, worldPath, ct);

    public async Task<DiagnosticReport> RunQuickAsync(string serverJarPath, string? worldPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _log.Information("[TROUBLESHOOT] [Quick] 开始系统/配置/日志检查...");
            var checks = _checkRunner.RunAll(serverJarPath, worldPath, InferServerInfo(serverJarPath));
            sw.Stop();

            _log.Information("[TROUBLESHOOT] [Quick] 完成: {N} checks, {Ms}ms", checks.Count, sw.ElapsedMilliseconds);
            var report = BuildReport(serverJarPath, worldPath, checks, new List<PlayerStat>(), sw.ElapsedMilliseconds, true, null);
            return await AttachAiAnalysisAsync(report, null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _log.Error(ex, "[TROUBLESHOOT] [Quick] 顶层异常");
            return BuildReport(serverJarPath, worldPath, new List<CheckResult>(), new List<PlayerStat>(), sw.ElapsedMilliseconds, false, ex.Message);
        }
    }

    public async Task<DiagnosticReport> RunDeepAsync(string serverJarPath, string? worldPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var allChecks = new List<CheckResult>();

        try
        {
            // Phase 1: Quick
            _log.Information("[TROUBLESHOOT] [Deep] Phase 1/2: 系统/配置/日志检查...");
            allChecks.AddRange(_checkRunner.RunAll(serverJarPath, worldPath, InferServerInfo(serverJarPath)));
            ct.ThrowIfCancellationRequested();

            // Phase 2: 存档 NBT/Region 扫描
            if (!string.IsNullOrEmpty(worldPath) && Directory.Exists(worldPath))
            {
                _log.Information("[TROUBLESHOOT] [Deep] Phase 2/2: 存档扫描...");

                try
                {
                    var playersDir = Path.Combine(worldPath, "playerdata");
                    if (Directory.Exists(playersDir))
                        allChecks.AddRange(_archiveAnalyzer.AnalyzePlayerDat(playersDir));
                }
                catch (Exception ex) { _log.Warning(ex, "[TROUBLESHOOT] 玩家存档扫描降级"); }

                ct.ThrowIfCancellationRequested();

                try
                {
                    var regionDir = Path.Combine(worldPath, "region");
                    if (Directory.Exists(regionDir))
                        allChecks.AddRange(_archiveAnalyzer.AnalyzeRegions(regionDir));
                }
                catch (Exception ex) { _log.Warning(ex, "[TROUBLESHOOT] Region 扫描降级"); }

                _log.Information("[TROUBLESHOOT] [Deep] 存档扫描完成");
            }

            sw.Stop();
            var topPlayers = _archiveAnalyzer.GetTopPlayers(10);

            _log.Information("[TROUBLESHOOT] [Deep] ✅ 完成: {Total} checks, {Ms}ms", allChecks.Count, sw.ElapsedMilliseconds);
            var report = BuildReport(serverJarPath, worldPath, allChecks, topPlayers, sw.ElapsedMilliseconds, true, null);
            return await AttachAiAnalysisAsync(report, null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _log.Error(ex, "[TROUBLESHOOT] [Deep] 顶层异常");
            return BuildReport(serverJarPath, worldPath, allChecks, new List<PlayerStat>(), sw.ElapsedMilliseconds, false, ex.Message);
        }
    }

    // ── 增量体检：按 checkIds 过滤运行；空集合 = 全部 ──
    public async Task<DiagnosticReport> RunChecksAsync(string serverJarPath, string? worldPath, IEnumerable<string> checkIds, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var server = InferServerInfo(serverJarPath);
            var allowed = new HashSet<string>(checkIds, StringComparer.OrdinalIgnoreCase);
            var checks = _checkRunner.RunAll(serverJarPath, worldPath, server)
                .Where(c => allowed.Count == 0 || allowed.Contains(c.CheckId))
                .ToList();
            sw.Stop();
            return BuildReport(serverJarPath, worldPath, checks, new List<PlayerStat>(), sw.ElapsedMilliseconds, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _log.Error(ex, "[TROUBLESHOOT] [Checks] 顶层异常");
            return BuildReport(serverJarPath, worldPath, new List<CheckResult>(), new List<PlayerStat>(), sw.ElapsedMilliseconds, false, ex.Message);
        }
    }

    // ── 修复执行（委托 FixExecutor）──

    public Task<FixResult> ExecuteFixAsync(string serverJarPath, string? worldPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default)
        => _fixExecutor.ExecuteAsync(serverJarPath, worldPath, fix, trustMode, ct);

    public Task<bool> IsServerRunningAsync(string serverJarPath)
        => _fixExecutor.IsServerRunningAsync(serverJarPath);

    public Task<bool> KillServerAsync(string serverJarPath)
        => _fixExecutor.KillServerAsync(serverJarPath);

    // ── AI 后处理：已配置 Key 时附加分析；失败不阻断报告 ──

    private async Task<DiagnosticReport> AttachAiAnalysisAsync(DiagnosticReport report, string? question, CancellationToken ct)
    {
        if (!_deepSeek.IsConfigured) return report;
        try
        {
            var analysis = await _deepSeek.AnalyzeReportAsync(report, question, ct);
            if (analysis == null) return report;
            return report with
            {
                AiAnalysis = analysis,
                DeepSeekRawResponse = analysis.RawJson,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[TROUBLESHOOT] AI 后处理失败（不影响报告）");
            return report;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 报告构建 + 辅助方法（原代码保留）
    // ═══════════════════════════════════════════════════════════

    private DiagnosticReport BuildReport(
        string jarPath, string? worldPath,
        List<CheckResult> checks, List<PlayerStat> players,
        long scanMs, bool succeeded, string? error)
    {
        var summary = new DiagnosticSummary(
            TotalChecks: checks.Count,
            OkCount: checks.Count(c => c.Severity == Severity.Ok),
            WarningCount: checks.Count(c => c.Severity == Severity.Warning),
            ErrorCount: checks.Count(c => c.Severity == Severity.Error),
            CriticalCount: checks.Count(c => c.Severity == Severity.Critical),
            AutoFixableCount: checks.Count(c => c.AutoFixable),
            ScanDurationMs: scanMs);

        var issues = checks
            .Where(c => c.Severity >= Severity.Warning)
            .Select(c => new Issue(
                IssueId: c.CheckId,
                Severity: c.Severity,
                Category: c.Category,
                Title: c.Title,
                Detail: c.Detail,
                Hint: InferHint(c),
                Suggestion: InferSuggestion(c),
                Fix: c.SuggestedFix,
                Context: ToContextDict(c.RawData)))
            .ToList();

        return new DiagnosticReport(
            GeneratedAt: DateTime.Now,
            MsmcVersion: GetVersion(),
            ServerJarPath: jarPath,
            WorldPath: worldPath ?? string.Empty,
            Server: InferServerInfo(jarPath),
            Checks: checks,
            Summary: summary,
            Issues: issues,
            TopPlayers: players,
            AiAnalysis: null,
            DeepSeekRawResponse: null,
            Succeeded: succeeded,
            ErrorMessage: error);
    }

    /// <summary>把 CheckResult.RawData（任意对象）规整成前端可用的字典：已是字典直接用，否则序列化转换</summary>
    private static Dictionary<string, object?> ToContextDict(object? raw)
    {
        if (raw is Dictionary<string, object?> d) return d;
        if (raw == null) return new Dictionary<string, object?>();
        try
        {
            var json = JsonSerializer.Serialize(raw);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static CheckResult FailedCheck(string checkId, Exception ex)
        => new(checkId, Severity.Warning, "System", "检查异常", ex.Message, false, null, ex.Message);

    private static ServerInfo InferServerInfo(string jarPath)
    {
        string jarName = Path.GetFileName(jarPath);
        string? core = jarName.Contains("paper", StringComparison.OrdinalIgnoreCase) ? "Paper"
            : jarName.Contains("spigot", StringComparison.OrdinalIgnoreCase) ? "Spigot"
            : jarName.Contains("purpur", StringComparison.OrdinalIgnoreCase) ? "Purpur"
            : jarName.Contains("forge", StringComparison.OrdinalIgnoreCase) ? "Forge"
            : jarName.Contains("fabric", StringComparison.OrdinalIgnoreCase) ? "Fabric"
            : jarName.Contains("glowstone", StringComparison.OrdinalIgnoreCase) ? "Glowstone"
            : null;

        int? pid = null;
        foreach (var p in Process.GetProcessesByName("java"))
        {
            try
            {
                if (!string.IsNullOrEmpty(p.MainWindowTitle) &&
                    p.MainWindowTitle.Contains(jarName, StringComparison.OrdinalIgnoreCase))
                {
                    pid = p.Id;
                    break;
                }
            }
            catch { }
        }

        // WMI 命令行兜底：java -jar 控制台进程通常没有窗口标题
        if (pid == null)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'java.exe' OR Name = 'javaw.exe'");
                foreach (var obj in searcher.Get())
                {
                    try
                    {
                        var cmd = obj["CommandLine"] as string;
                        if (!string.IsNullOrEmpty(cmd) && cmd.Contains(jarName, StringComparison.OrdinalIgnoreCase))
                        {
                            pid = Convert.ToInt32(obj["ProcessId"]);
                            break;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        return new ServerInfo(jarName, core, null, null, null, null, pid, pid != null);
    }

    private static string GetVersion() =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    private static string? InferHint(CheckResult c) => c.CheckId switch
    {
        "java.version" => "Java 版本不匹配是服务器启动失败最常见的原因",
        "port.availability" => "端口被其他进程占用，服务器无法监听",
        "port.firewall" => "Windows 防火墙默认阻止非信任网络入站",
        "log.outmemory" => "堆内存不足或存在内存泄漏，服务器最终会崩溃",
        "log.chunk.generation" => "区块生成事件异常通常是插件冲突或区块损坏",
        _ => null
    };

    private static string? InferSuggestion(CheckResult c) => c.CheckId switch
    {
        "java.version" => "用 MSMC「Java 管理」切换到匹配版本",
        "port.availability" => "关闭占用端口的进程或修改 server.properties 的 server-port",
        "log.outmemory" => "增加 -Xmx 堆内存配置后重启服务器",
        _ => null
    };
}
