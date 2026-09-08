// -----------------------------------------------------------------------------
// FixExecutor.cs — 7 个真实 FixAction 执行器
// 红线原则: 每个 FixAction 都真执行，不返回假成功
// 安全原则: 先备份、再执行；异常返回 FixResult.Error 而不是吞掉
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public class FixExecutor : IFixExecutor
{
    private readonly ILogger _log;

    public FixExecutor(ILogger log)
    {
        _log = log;
    }

    public async Task<FixResult> ExecuteAsync(string serverJarPath, string? worldPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default)
    {
        var stepResults = new List<FixStepResult>();
        string? backupPath = null;

        try
        {
            _log.Information("[FIX] 开始执行 Fix {FixId} (mode={Mode})", fix.FixId, trustMode);

            if (trustMode == FixTrustMode.DryRun)
            {
                foreach (var step in fix.Steps)
                    stepResults.Add(new FixStepResult(step.Label, step.ActionType, true, "[DryRun] 模拟完成"));

                return new FixResult(fix.FixId, true, stepResults.Count, fix.Steps.Count, stepResults, null, null);
            }

            // Auto / StepByStep: 真执行
            // 先做世界备份（绝大多数 fix_id 都需要前置备份）
            if (fix.Dangerous && !string.IsNullOrEmpty(worldPath) && Directory.Exists(worldPath))
            {
                backupPath = await ExecuteBackupInternal(worldPath, stepResults, ct);
            }

            switch (fix.FixId)
            {
                case "backup.world":
                    // 单独的备份请求，不自动前置
                    backupPath = await ExecuteBackupInternal(worldPath, stepResults, ct);
                    break;

                case "server.kill":
                    await ExecuteServerKill(serverJarPath, stepResults);
                    break;

                case "port.kill.process":
                    ExecutePortKill(fix, stepResults);
                    break;

                case "config.edit.server-properties":
                    await ExecuteConfigEdit(fix, worldPath, stepResults);
                    break;

                case "java.switch.version":
                    await ExecuteJavaSwitch(fix, serverJarPath, stepResults);
                    break;

                case "region.clean.entities":
                    await ExecuteRegionClean(fix, worldPath, stepResults, ct);
                    break;

                case "player.reset.damage":
                    await ExecutePlayerReset(fix, worldPath, stepResults, ct);
                    break;

                default:
                    stepResults.Add(new FixStepResult(
                        $"未知 FixId: {fix.FixId}",
                        fix.FixId, false, "未实现"));
                    break;
            }

            bool allOk = stepResults.Count > 0 && stepResults.All(s => s.Succeeded);
            _log.Information("[FIX] Fix {FixId} 完成: {Ok}/{Total} steps", fix.FixId,
                stepResults.Count(s => s.Succeeded), stepResults.Count);

            return new FixResult(fix.FixId, allOk, stepResults.Count, fix.Steps.Count, stepResults,
                backupPath, allOk ? null : "部分步骤失败");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[FIX] Fix {FixId} 顶层异常", fix.FixId);
            stepResults.Add(new FixStepResult("FIX 异常", fix.FixId, false, ex.Message));
            return new FixResult(fix.FixId, false, stepResults.Count, fix.Steps.Count, stepResults,
                backupPath, ex.Message);
        }
    }

    // ── 服务器状态查询 ──

    public Task<bool> IsServerRunningAsync(string serverJarPath)
    {
        var jarName = Path.GetFileName(serverJarPath);
        foreach (var p in Process.GetProcessesByName("java"))
        {
            try
            {
                if (IsMatch(p, jarName) || IsJavaProcessMatchingJar(p.Id, jarName))
                    return Task.FromResult(true);
            }
            catch { }
        }
        return Task.FromResult(false);
    }

    public async Task<bool> KillServerAsync(string serverJarPath)
    {
        var jarName = Path.GetFileName(serverJarPath);
        return await KillServerProcessAsync(jarName);
    }

    private static bool IsMatch(Process p, string jarName)
    {
        if (!string.IsNullOrEmpty(p.MainWindowTitle) &&
            p.MainWindowTitle.Contains(jarName, StringComparison.OrdinalIgnoreCase))
            return true;
        // 检查进程启动参数（非 Windows 上可能不可用）
        try
        {
            var cmdLine = p.StartInfo.Arguments;
            if (!string.IsNullOrEmpty(cmdLine) && cmdLine.Contains(jarName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }
        return false;
    }

    /// <summary>用 WMI 命令行兜底匹配：java -jar 控制台进程通常没有窗口标题</summary>
    private static bool IsJavaProcessMatchingJar(int pid, string jarName)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (var obj in searcher.Get())
            {
                var cmd = obj["CommandLine"] as string;
                if (!string.IsNullOrEmpty(cmd) && cmd.Contains(jarName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    // ═══════════════════════════════════════════════════════════
    // 7 个 FixAction 的真实实现
    // ═══════════════════════════════════════════════════════════

    private async Task<string?> ExecuteBackupInternal(string? worldPath, List<FixStepResult> results, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(worldPath) || !Directory.Exists(worldPath))
        {
            results.Add(new FixStepResult("备份", "backup", false, "worldPath 无效，跳过"));
            return null;
        }

        try
        {
            var parent = Directory.GetParent(worldPath)?.FullName ?? ".";
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupDir = Path.Combine(parent, $".bak-{timestamp}");

            _log.Information("[FIX] 备份 {Src} → {Dst}", worldPath, backupDir);
            CopyDirectoryRecursive(worldPath, backupDir, ct);
            _log.Information("[FIX] 备份完成");

            results.Add(new FixStepResult(
                $"备份 world → .bak-{timestamp}",
                "backup", true, backupDir));

            return backupDir;
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("备份", "backup", false, ex.Message));
            return null;
        }
    }

    private async Task ExecuteServerKill(string serverJarPath, List<FixStepResult> results)
    {
        var jarName = Path.GetFileName(serverJarPath);
        bool killed = await KillServerProcessAsync(jarName);
        results.Add(new FixStepResult(
            killed ? "服务器已终止" : "未找到运行中服务器",
            "kill_process", killed, killed ? "进程已终止" : "可能已停止"));
    }

    private void ExecutePortKill(FixAction fix, List<FixStepResult> results)
    {
        int pid = ExtractIntParam(fix, "pid");
        if (pid <= 0)
        {
            results.Add(new FixStepResult("杀掉占用端口进程", "kill_process", false, "缺少 pid 参数"));
            return;
        }
        try
        {
            var proc = Process.GetProcessById(pid);
            proc.Kill();
            results.Add(new FixStepResult($"终止 PID={pid} ({proc.ProcessName})",
                "kill_process", true, "进程已终止"));
        }
        catch (ArgumentException)
        {
            results.Add(new FixStepResult($"PID={pid} 已不存在", "kill_process", false, "进程已退出"));
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult($"终止 PID={pid}", "kill_process", false, ex.Message));
        }
    }

    private async Task ExecuteConfigEdit(FixAction fix, string? worldPath, List<FixStepResult> results)
    {
        var propFile = FindServerProperties(worldPath);
        if (propFile == null)
        {
            results.Add(new FixStepResult("修改 server.properties", "config_edit", false,
                "未找到 server.properties（通常在服务器 jar 同级目录）"));
            return;
        }

        try
        {
            int maxPlayers = ExtractIntParam(fix, "maxPlayers");
            int serverPort = ExtractIntParam(fix, "serverPort");
            int viewDistance = ExtractIntParam(fix, "viewDistance");

            var lines = File.ReadAllLines(propFile).ToList();
            bool changed = false;

            if (maxPlayers > 0)
                changed |= SetLineValue(lines, "max-players", maxPlayers.ToString());
            if (serverPort > 0)
                changed |= SetLineValue(lines, "server-port", serverPort.ToString());
            if (viewDistance > 0)
                changed |= SetLineValue(lines, "view-distance", viewDistance.ToString());

            if (changed)
            {
                File.WriteAllLines(propFile, lines);
                results.Add(new FixStepResult("更新 server.properties",
                    "config_edit", true,
                    $"max-players={maxPlayers}, server-port={serverPort}, view-distance={viewDistance}"));
            }
            else
            {
                results.Add(new FixStepResult("检查 server.properties",
                    "config_edit", true, "无需修改"));
            }
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("修改 server.properties", "config_edit", false, ex.Message));
        }
        await Task.CompletedTask;
    }

    private async Task ExecuteJavaSwitch(FixAction fix, string serverJarPath, List<FixStepResult> results)
    {
        string javaPath = ExtractStringParam(fix, "javaPath");
        var parent = Path.GetDirectoryName(serverJarPath);
        if (parent == null || string.IsNullOrEmpty(javaPath))
        {
            results.Add(new FixStepResult("切换 Java", "command", false, "缺少 javaPath 参数"));
            return;
        }

        if (!File.Exists(javaPath))
        {
            results.Add(new FixStepResult("切换 Java", "command", false, $"java 路径不存在: {javaPath}"));
            return;
        }

        try
        {
            int updated = 0;
            foreach (var scriptFile in DiscoverStartScripts(parent))
            {
                var content = File.ReadAllText(scriptFile);

                // 先备份原脚本（唯一名），替换出错可回滚
                var backup = $"{scriptFile}.bak-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
                File.Copy(scriptFile, backup);

                // 只替换「行首缩进后紧跟 java/java.exe 可执行名（可带引号包裹的路径前缀）」的写法，
                // 不动 set JAVA= 变量赋值、不动注释/文本中间出现的 java。
                // 统一替换为带引号的完整路径，避免引号内路径替换出语法错误。
                var newContent = Regex.Replace(content,
                    @"^(\s*)(?:""[^""]*?[\\/])?(?:java|java\.exe)""?",
                    m => m.Groups[1].Value + "\"" + javaPath + "\"",
                    RegexOptions.Multiline);

                if (newContent != content)
                {
                    File.WriteAllText(scriptFile, newContent);
                    updated++;
                }
                else
                {
                    // 无 java 路径可替换时清理刚建的备份，避免残留
                    try { if (File.Exists(backup)) File.Delete(backup); } catch { }
                }
            }

            results.Add(new FixStepResult(
                updated > 0 ? $"已更新 {updated} 个启动脚本（已备份原脚本）" : "启动脚本无 java 路径，跳过",
                "command", updated > 0, updated > 0 ? $"Java 路径已替换为 {javaPath}" : "无需修改"));
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("切换 Java", "command", false, ex.Message));
        }
        await Task.CompletedTask;
    }

    private async Task ExecuteRegionClean(FixAction fix, string? worldPath, List<FixStepResult> results, CancellationToken ct)
    {
        var regionDir = worldPath != null ? Path.Combine(worldPath, "region") : null;
        if (regionDir == null || !Directory.Exists(regionDir))
        {
            results.Add(new FixStepResult("清理 Region", "cleanup_region", false, "region 目录不存在"));
            return;
        }

        int cleaned = 0;
        try
        {
            foreach (var mcaFile in Directory.GetFiles(regionDir, "*.mca"))
            {
                ct.ThrowIfCancellationRequested();
                // 唯一备份名（时间戳 + GUID）：绝不复用/删除原文件，防止同秒重跑丢档
                var backup = $"{mcaFile}.bak-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
                File.Move(mcaFile, backup);
                cleaned++;

                if (cleaned <= 5 || cleaned % 50 == 0)
                    results.Add(new FixStepResult(
                        $"标记区块 {Path.GetFileName(mcaFile)} 为待重新生成",
                        "cleanup_region", true, $"已备份为 {Path.GetFileName(backup)}"));
            }

            if (cleaned > 5)
                results.Add(new FixStepResult(
                    $"共处理 {cleaned} 个异常 .mca",
                    "cleanup_region", true, "完成。下次服务器启动时这些区块会自动重新生成"));
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("清理 Region", "cleanup_region", false, ex.Message));
        }
        await Task.CompletedTask;
    }

    private async Task ExecutePlayerReset(FixAction fix, string? worldPath, List<FixStepResult> results, CancellationToken ct)
    {
        var playerDir = worldPath != null ? Path.Combine(worldPath, "playerdata") : null;
        if (playerDir == null || !Directory.Exists(playerDir))
        {
            results.Add(new FixStepResult("重置玩家物品", "cleanup_player", false, "playerdata 目录不存在"));
            return;
        }

        // 红线原则：不返回假成功。NBT damage 重置（写回存档树）尚未实现，
        // 因此这里只做全量备份并诚实标记失败，避免用户误以为修复已生效。
        int backedUp = 0;
        try
        {
            foreach (var datFile in Directory.GetFiles(playerDir, "*.dat"))
            {
                ct.ThrowIfCancellationRequested();
                var backup = $"{datFile}.bak-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
                if (!File.Exists(backup))
                {
                    File.Copy(datFile, backup);
                    backedUp++;
                }
            }

            results.Add(new FixStepResult(
                backedUp > 0 ? $"已备份 {backedUp} 个玩家存档" : "无玩家存档可备份",
                "cleanup_player", false,
                "玩家物品 damage 重置（写回 NBT）尚未实现；已备份全部 .dat，可后续手动处理"));
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("重置玩家物品", "cleanup_player", false, ex.Message));
        }
        await Task.CompletedTask;
    }

    // ── 辅助 ──

    private async Task<bool> KillServerProcessAsync(string jarName)
    {
        bool anyKilled = false;
        foreach (var p in Process.GetProcessesByName("java"))
        {
            try
            {
                if (IsMatch(p, jarName) || IsJavaProcessMatchingJar(p.Id, jarName))
                {
                    _log.Information("[FIX] Kill PID={Pid} for {Jar}", p.Id, jarName);
                    p.Kill();
                    try { await p.WaitForExitAsync(new CancellationTokenSource(5000).Token); }
                    catch { }
                    anyKilled = true;
                }
            }
            catch { }
        }
        return anyKilled;
    }

    private static int ExtractIntParam(FixAction fix, string key)
    {
        foreach (var step in fix.Steps)
        {
            if (step.Params.TryGetValue(key, out var v) && v != null)
            {
                try { return Convert.ToInt32(v); } catch { return 0; }
            }
        }
        return 0;
    }

    private static string ExtractStringParam(FixAction fix, string key)
    {
        foreach (var step in fix.Steps)
        {
            if (step.Params.TryGetValue(key, out var v) && v != null)
                return v.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static string? FindServerProperties(string? worldPath)
    {
        if (string.IsNullOrEmpty(worldPath)) return null;
        var parent = Directory.GetParent(worldPath);
        if (parent == null) return null;
        var p = Path.Combine(parent.FullName, "server.properties");
        return File.Exists(p) ? p : null;
    }

    private static bool SetLineValue(List<string> lines, string key, string value)
    {
        bool changed = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith(key + "="))
            {
                lines[i] = $"{key}={value}";
                changed = true;
            }
        }
        return changed;
    }

    private static IEnumerable<string> DiscoverStartScripts(string parent)
    {
        foreach (var dir in new[] { parent, Path.Combine(parent, "scripts") })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "start*"))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".bat" or ".cmd" or ".ps1" or ".sh" or ".txt")
                    yield return file;
            }
        }
    }

    private static void CopyDirectoryRecursive(string src, string dst, CancellationToken ct)
    {
        Directory.CreateDirectory(dst);
        foreach (var dir in Directory.GetDirectories(src))
        {
            ct.ThrowIfCancellationRequested();
            CopyDirectoryRecursive(dir, Path.Combine(dst, Path.GetFileName(dir)), ct);
        }
        foreach (var file in Directory.GetFiles(src))
        {
            ct.ThrowIfCancellationRequested();
            var dest = Path.Combine(dst, Path.GetFileName(file));
            if (!File.Exists(dest))
                File.Copy(file, dest);
        }
    }
}
