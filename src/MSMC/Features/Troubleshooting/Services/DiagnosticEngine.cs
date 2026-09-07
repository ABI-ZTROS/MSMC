// -----------------------------------------------------------------------------
// DiagnosticEngine.cs — 疑难解答引擎协调器
// 职责: 组装 CheckRunner + ArchiveAnalyzer → 生成 DiagnosticReport
// P4 诚实返回链: 任何步骤异常 → Succeeded=false + ErrorMessage 可见
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public class DiagnosticEngine : IDiagnosticEngine
{
    private readonly ICheckRunner _checkRunner;
    private readonly IDiagnosticArchiveAnalyzer _archiveAnalyzer;
    private readonly ILogger _log;

    public DiagnosticEngine(ICheckRunner checkRunner, IDiagnosticArchiveAnalyzer archiveAnalyzer, ILogger log)
    {
        _checkRunner = checkRunner;
        _archiveAnalyzer = archiveAnalyzer;
        _log = log;
    }

    public async Task<DiagnosticReport> RunDiagnosticAsync(string serverJarPath, string? worldPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var allChecks = new List<CheckResult>();

        try
        {
            // ── Step 1: 系统/配置检查（CheckRunner 10 个）──
            _log.Information("[TROUBLESHOOT] 开始系统/配置检查...");
            var checks = _checkRunner.RunAll(serverJarPath, worldPath, InferServerInfo(serverJarPath));
            allChecks.AddRange(checks);
            _log.Information("[TROUBLESHOOT] 系统/配置检查完成 ({N} 项)", checks.Count);

            ct.ThrowIfCancellationRequested();

            // ── Step 2: 存档扫描（玩家 NBT + Region，Try-Catch 降级）──
            if (!string.IsNullOrEmpty(worldPath) && Directory.Exists(worldPath))
            {
                _log.Information("[TROUBLESHOOT] 开始存档扫描...");

                try
                {
                    var playersDir = Path.Combine(worldPath, "playerdata");
                    if (Directory.Exists(playersDir))
                        allChecks.AddRange(_archiveAnalyzer.AnalyzePlayerDat(playersDir));
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "[TROUBLESHOOT] 玩家存档扫描异常（降级跳过）");
                    allChecks.Add(FailedCheck("archive.player", ex));
                }

                ct.ThrowIfCancellationRequested();

                try
                {
                    var regionDir = Path.Combine(worldPath, "region");
                    if (Directory.Exists(regionDir))
                        allChecks.AddRange(_archiveAnalyzer.AnalyzeRegions(regionDir));
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "[TROUBLESHOOT] Region 扫描异常（降级跳过）");
                    allChecks.Add(FailedCheck("archive.region", ex));
                }

                _log.Information("[TROUBLESHOOT] 存档扫描完成");
            }

            // ── Step 3: 汇总 ──
            sw.Stop();
            var summary = BuildSummary(allChecks, sw.ElapsedMilliseconds);
            var issues = allChecks
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
                    Context: c.RawData is Dictionary<string, object?> d ? d : new Dictionary<string, object?>()))
                .ToList();

            var topPlayers = _archiveAnalyzer.GetTopPlayers(10);

            _log.Information("[TROUBLESHOOT] ✅ 诊断完成: {Total} checks, {Critical} Critical, {Warning} Warning, 耗时 {Ms}ms",
                summary.TotalChecks, summary.CriticalCount, summary.WarningCount, sw.ElapsedMilliseconds);

            return new DiagnosticReport(
                GeneratedAt: DateTime.Now,
                MsmcVersion: GetVersion(),
                ServerJarPath: serverJarPath,
                WorldPath: worldPath ?? string.Empty,
                Server: InferServerInfo(serverJarPath),
                Checks: allChecks,
                Summary: summary,
                Issues: issues,
                TopPlayers: topPlayers,
                AiAnalysis: null,      // P1 实现
                DeepSeekRawResponse: null,
                Succeeded: true,
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _log.Error(ex, "[TROUBLESHOOT] ❌ 诊断引擎顶层异常");
            return new DiagnosticReport(
                DateTime.Now, GetVersion(), serverJarPath, worldPath ?? string.Empty,
                InferServerInfo(serverJarPath), allChecks,
                BuildSummary(allChecks, sw.ElapsedMilliseconds),
                new List<Issue>(), new List<PlayerStat>(), null, null, false, ex.Message);
        }
    }

    // ── P0 未实现 ──
    public Task<DiagnosticReport> RunChecksAsync(string serverJarPath, string? worldPath, IEnumerable<string> checkIds, CancellationToken ct = default)
        => Task.FromResult(new DiagnosticReport(DateTime.Now, GetVersion(), serverJarPath, worldPath ?? "",
            InferServerInfo(serverJarPath), new List<CheckResult>(),
            new DiagnosticSummary(0, 0, 0, 0, 0, 0, 0),
            new List<Issue>(), new List<PlayerStat>(), null, null, false, "P2 未实现"));

    public Task<FixResult> ExecuteFixAsync(string serverJarPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default)
        => Task.FromResult(new FixResult(fix.FixId, false, 0, 0, new List<FixStepResult>(), null, "P1 未实现"));

    // ── 辅助 ──

    private static CheckResult FailedCheck(string checkId, Exception ex)
        => new(checkId, Severity.Warning, "System", "检查异常", ex.Message, false, null, ex.Message);

    private static DiagnosticSummary BuildSummary(List<CheckResult> checks, long ms)
    {
        return new DiagnosticSummary(
            TotalChecks: checks.Count,
            OkCount: checks.Count(c => c.Severity == Severity.Ok),
            WarningCount: checks.Count(c => c.Severity == Severity.Warning),
            ErrorCount: checks.Count(c => c.Severity == Severity.Error),
            CriticalCount: checks.Count(c => c.Severity == Severity.Critical),
            AutoFixableCount: checks.Count(c => c.AutoFixable),
            ScanDurationMs: ms);
    }

    private static ServerInfo InferServerInfo(string jarPath)
    {
        string jarName = Path.GetFileName(jarPath);
        string? core = null;
        // P0 简化: 通过 jar 名猜核心
        if (jarName.Contains("paper", StringComparison.OrdinalIgnoreCase)) core = "Paper";
        else if (jarName.Contains("spigot", StringComparison.OrdinalIgnoreCase)) core = "Spigot";
        else if (jarName.Contains("purpur", StringComparison.OrdinalIgnoreCase)) core = "Purpur";
        else if (jarName.Contains("forge", StringComparison.OrdinalIgnoreCase)) core = "Forge";
        else if (jarName.Contains("fabric", StringComparison.OrdinalIgnoreCase)) core = "Fabric";

        // 检查是否有运行中的 MC 进程
        int? pid = null;
        string? javaVer = null;
        foreach (var p in Process.GetProcessesByName("java"))
        {
            try
            {
                if (p.MainWindowTitle.Contains(jarName) || jarName.Contains(p.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    pid = p.Id;
                    break;
                }
            }
            catch { }
        }

        return new ServerInfo(jarName, core, null, javaVer, null, null, pid, pid != null);
    }

    private static string GetVersion() =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    private static string? InferHint(CheckResult c) => c.CheckId switch
    {
        "java.version" => "Java 版本不匹配是服务器启动失败最常见的原因之一",
        "port.availability" => "端口被其他进程占用，服务器无法监听",
        "port.firewall" => "Windows 防火墙默认阻止非信任网络的入站连接",
        "archive.player" => "非法物品 / NBT 异常可能是插件 bug、作弊或恶意玩家造成的",
        "archive.region.entity.stack" => "实体堆叠通常是 spawner 异常、chunk 修复不当或服务器假死恢复时产生",
        _ => null
    };

    private static string? InferSuggestion(CheckResult c) => c.CheckId switch
    {
        "java.version" => "使用 MSMC 「Java 管理」切换到匹配版本",
        "port.availability" => "关闭占用端口的进程，或修改 server.properties 的 server-port",
        "port.firewall" => "以管理员身份运行 MSMC，或手动添加 netsh 入站规则",
        "archive.player" => "备份存档后清理异常物品 / 修复损坏的 .dat",
        "archive.region.entity.stack" => "用 MSMC 「清理区块」或手动删除异常 .mca 后让服务器重新生成",
        _ => null
    };
}
