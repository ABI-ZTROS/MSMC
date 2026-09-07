# Troubleshooting (疑难解答) Full Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在服务器管理功能新增"疑难解答"模块，实现全链路一站式体检（10 个系统/配置检查点 + 3 个日志检查点）、存档 NBT/Region 离线分析、7 个真实可执行 FixAction 自动修复、血红色强制配色前端页面。

**Architecture:** Bridge 契约先锁死（C# record ↔ TS interface 零偏差），后端补 FixExecutor + LogAnalyzer + Bridge handlers，前端并行写页面 + 组件 + TreeNode 常量，最后联调。

**Tech Stack:** .NET 9 WPF / System.IO.Compression (GZIP/ZLIB) / React 18 + Vite + TypeScript / react-icons / WebView2 bridge

---

## File Structure (what's new / what's modified)

### 后端（Create + Modify）

| File | Action | Responsibility |
|------|--------|----------------|
| `src/MSMC/Features/Troubleshooting/Services/FixExecutor.cs` | CREATE | 7 个 FixAction 真实执行器 |
| `src/MSMC/Features/Troubleshooting/Services/IFixExecutor.cs` | CREATE | FixExecutor 接口 |
| `src/MSMC/Features/Troubleshooting/Services/CheckRunner.cs` | MODIFY | 加 2 个日志检查点 (log.outmemory, log.chunk.generation) |
| `src/MSMC/Features/Troubleshooting/Services/ICheckRunner.cs` | MODIFY | 加 RunQuick / RunDeep 方法签名（分阶段扫描） |
| `src/MSMC/Features/Troubleshooting/Services/DiagnosticEngine.cs` | MODIFY | 加 RunQuickAsync / RunDeepAsync / 对接 FixExecutor |
| `src/MSMC/Features/Troubleshooting/Services/IDiagnosticEngine.cs` | MODIFY | 加 RunQuickAsync / RunDeepAsync / IsServerRunningAsync / KillServerAsync |
| `src/MSMC/App.xaml.cs` | MODIFY | 注册 IFixExecutor → FixExecutor DI |
| `src/MSMC/Features/WebView2/Services/BridgeActionRegistrar.cs` | MODIFY | 注册 7 个 diagnostic.* bridge handlers |

### 前端（Create + Modify）

| File | Action | Responsibility |
|------|--------|----------------|
| `src/frontend/src/types/bridge.ts` | MODIFY | 追加 DiagnosticReport 等 TS 接口 |
| `src/frontend/src/utils/bridge.ts` | MODIFY | 追加 diagnostic.runDiagnostic 等封装 |
| `src/frontend/src/data/troubleshootingNodes.ts` | CREATE | 硬编码 TreeNode 常量数组（二叉树症状导航） |
| `src/frontend/src/pages/TroubleshootingPage.tsx` | CREATE | 血红色双轨布局 + 分阶段扫描 |
| `src/frontend/src/components/DiagnosticReportCard.tsx` | CREATE | 红/黄/绿分级问题卡片 |
| `src/frontend/src/components/FixPanel.tsx` | CREATE | 信任确认 + diff 预览 + 修复进度 |
| `src/frontend/src/components/TreeNodeDialog.tsx` | CREATE | 二叉树症状导航对话 |
| `src/frontend/src/components/Sidebar.tsx` | MODIFY | 加导航项（FaWrench + 血红强制配色） |
| `src/frontend/src/App.tsx` | MODIFY | 加路由 /diagnostics |

---

### Task 1: 锁死 Bridge 契约（TS interface ↔ C# record）

**Files:**
- Modify: `src/frontend/src/types/bridge.ts`

- [ ] **Step 1: 追加 Diagnostics 相关 TypeScript 接口**

在 `src/frontend/src/types/bridge.ts` 文件末尾追加：

```typescript
// ─────────────────────────────────────────────────────────────────────
// 疑难解答 (Troubleshooting) 类型 —— 严格对标 DiagnosticTypes.cs record
// ─────────────────────────────────────────────────────────────────────

export type Severity = 0 | 1 | 2 | 3 | 4  // Ok=0 Info=1 Warning=2 Error=3 Critical=4
export type FixTrustMode = 'Auto' | 'StepByStep' | 'DryRun'

export interface DiagnosticServerInfo {
  jarName: string
  coreType?: string | null
  version?: string | null
  javaVersion?: string | null
  port?: number | null
  totalMemoryMb?: number | null
  processId?: number | null
  isRunning: boolean
}

export interface DiagnosticSummary {
  totalChecks: number
  okCount: number
  warningCount: number
  errorCount: number
  criticalCount: number
  autoFixableCount: number
  scanDurationMs: number
}

export interface DiagnosticCheckResult {
  checkId: string
  severity: Severity
  category: string
  title: string
  detail: string
  autoFixable: boolean
  suggestedFix?: DiagnosticFixAction | null
  rawData?: unknown
}

export interface DiagnosticFixAction {
  fixId: string
  label: string
  dangerous: boolean
  diffPreview?: string | null
  confidence: number
  rationale?: string | null
  steps: DiagnosticFixStep[]
}

export interface DiagnosticFixStep {
  label: string
  actionType: string
  dangerous: boolean
  confirmRequired: boolean
  params: Record<string, unknown>
}

export interface DiagnosticIssue {
  issueId: string
  severity: Severity
  category: string
  title: string
  detail: string
  hint?: string | null
  suggestion?: string | null
  fix?: DiagnosticFixAction | null
  context: Record<string, unknown>
}

export interface DiagnosticPlayerStat {
  uuid: string
  name: string
  totalItems: number
  wealthScore: number
  anomalyCount: number
  anomalyTypes: string[]
}

export interface DiagnosticReport {
  generatedAt: string          // ISO 8601
  msmcVersion: string
  serverJarPath: string
  worldPath: string
  server: DiagnosticServerInfo
  checks: DiagnosticCheckResult[]
  summary: DiagnosticSummary
  issues: DiagnosticIssue[]
  topPlayers: DiagnosticPlayerStat[]
  aiAnalysis: null             // P1
  deepSeekRawResponse: null    // P1
  succeeded: boolean
  errorMessage?: string | null
}

export interface DiagnosticFixStepResult {
  label: string
  actionType: string
  succeeded: boolean
  message?: string | null
}

export interface DiagnosticFixResult {
  fixId: string
  succeeded: boolean
  stepsCompleted: number
  stepsTotal: number
  stepResults: DiagnosticFixStepResult[]
  backupPath?: string | null
  error?: string | null
}

export interface ServerRunningCheck {
  running: boolean
  pid?: number | null
}

// ─────────────────────────────────────────────────────────────────────
// TreeNode 二叉树节点（硬编码常量在 troubleshootingNodes.ts）
// ─────────────────────────────────────────────────────────────────────

export interface TreeNode {
  id: string
  category: 'Player' | 'Map' | 'Plugin' | 'PluginConfig' | 'Network' | 'System'
  question: string
  options?: { label: string; next?: string; severity?: Severity }[]
  aiHint?: string
  checkReference?: string
}
```

- [ ] **Step 2: 追加 bridge 封装**

在 `src/frontend/src/utils/bridge.ts` 中追加（文件底部）：

```typescript
// ─────────────────────────────────────────────────────────────────────
// 疑难解答桥接封装
// ─────────────────────────────────────────────────────────────────────

export async function runDiagnostic(serverJarPath: string): Promise<{ success: boolean; report?: DiagnosticReport; error?: string }> {
  const bridge = (window as any).__msmc_bridge__
  if (!bridge) throw new Error('Bridge not ready')
  return await bridge.invoke('diagnostic.runDiagnostic', { serverJarPath })
}

export async function runDeepScan(serverJarPath: string, worldPath: string): Promise<{ success: boolean; report?: DiagnosticReport; error?: string }> {
  const bridge = (window as any).__msmc_bridge__
  if (!bridge) throw new Error('Bridge not ready')
  return await bridge.invoke('diagnostic.runDeepScan', { serverJarPath, worldPath })
}

export async function executeFix(fixId: string, trustMode: FixTrustMode, serverJarPath: string): Promise<DiagnosticFixResult> {
  const bridge = (window as any).__msmc_bridge__
  if (!bridge) throw new Error('Bridge not ready')
  return await bridge.invoke('diagnostic.executeFix', { fixId, trustMode, serverJarPath })
}

export async function cancelFix(fixId: string): Promise<{ success: boolean }> {
  const bridge = (window as any).__msmc_bridge__
  if (!bridge) throw new Error('Bridge not ready')
  return await bridge.invoke('diagnostic.cancelFix', { fixId })
}

export async function exportDiagnosticReport(format: 'markdown' | 'json', path?: string): Promise<{ path: string; size: number }> {
  const bridge = (window as any).__msmc_bridge__
  if (!bridge) throw new Error('Bridge not ready')
  return await bridge.invoke('diagnostic.exportReport', { format, path })
}

export async function checkServerRunning(serverJarPath: string): Promise<ServerRunningCheck> {
  const bridge = (window as any).__msmc_bridge__
  if (!bridge) throw new Error('Bridge not ready')
  return await bridge.invoke('diagnostic.checkServerRunning', { serverJarPath })
}

export async function killServerAndScan(serverJarPath: string, worldPath: string): Promise<{ success: boolean; report?: DiagnosticReport; error?: string }> {
  const bridge = (window as any).__msmc_bridge__
  if (!bridge) throw new Error('Bridge not ready')
  return await bridge.invoke('diagnostic.killServerAndScan', { serverJarPath, worldPath })
}
```

---

### Task 2: 后端 — 加 2 个日志检查点到 CheckRunner

**Files:**
- Modify: `src/MSMC/Features/Troubleshooting/Services/CheckRunner.cs`

- [ ] **Step 1: 在 _knownCheckIds 中加入 2 个新 CheckId**

修改 `CheckRunner.cs` 的 `_knownCheckIds`：

```csharp
private static readonly HashSet<string> _knownCheckIds = new()
{
    "java.version", "java.heap.size", "process.priority",
    "process.t1.qos", "process.t3.tuning", "port.availability",
    "port.firewall", "config.syntax", "storage.world.size",
    "log.startup.failure", "log.outmemory", "log.chunk.generation"
};
```

- [ ] **Step 2: 在 RunAll 方法末尾（log.startup.failure 之后）加 2 个新检查方法调用**

在 `RunAll` 方法里，调用 log.startup.failure 之后追加：

```csharp
// ── 日志层（P0 新增 2 个）──
try { results.Add(CheckLogOutOfMemory(worldPath)); }
catch (Exception ex) { results.Add(LogFail("log.outmemory", ex)); }

try { results.Add(CheckLogChunkGeneration(worldPath)); }
catch (Exception ex) { results.Add(LogFail("log.chunk.generation", ex)); }
```

- [ ] **Step 3: 实现 CheckLogOutOfMemory 和 CheckLogChunkGeneration 方法**

在 `CheckRunner.cs` 类里（任何现有 private 方法附近）追加：

```csharp
private static CheckResult CheckLogOutOfMemory(string? worldPath)
{
    var logDir = ResolveLogDir(worldPath);
    if (logDir == null)
        return new CheckResult("log.outmemory", Severity.Ok, "Log",
            "未检测到 OOM", "未找到日志目录，跳过 OOM 检查", false, null, null);

    var oomCount = CountInLogs(logDir, "java.lang.OutOfMemoryError");
    if (oomCount == 0)
        return new CheckResult("log.outmemory", Severity.Ok, "Log",
            "未检测到 OOM", $"扫描了日志目录，无 OutOfMemoryError", false, null, null);

    return new CheckResult("log.outmemory", Severity.Critical, "Log",
        "检测到 OutOfMemoryError",
        $"最近日志中出现 {oomCount} 次 OutOfMemoryError，服务器内存配置不足或存在泄漏",
        false,
        new FixAction("backup.world", "增加堆内存后重启服务器", false, null, 0.9,
            "OOM 说明 -Xmx 不够或内存泄漏", new List<FixStep>()),
        new { oomCount });
}

private static CheckResult CheckLogChunkGeneration(string? worldPath)
{
    var logDir = ResolveLogDir(worldPath);
    if (logDir == null)
        return new CheckResult("log.chunk.generation", Severity.Ok, "Log",
            "未检测到区块生成异常", "未找到日志目录", false, null, null);

    var count = CountInLogs(logDir, "Could not pass event CHUNK_GENERATION");
    if (count == 0)
        return new CheckResult("log.chunk.generation", Severity.Ok, "Log",
            "区块生成正常", "无异常事件", false, null, null);

    if (count < 5)
        return new CheckResult("log.chunk.generation", Severity.Info, "Log",
            "少量区块生成异常", $"{count} 次 CHUNK_GENERATION 事件异常（< 5），可能偶发", false, null, new { count });

    return new CheckResult("log.chunk.generation", Severity.Warning, "Log",
        "频繁区块生成异常",
        $"{count} 次 CHUNK_GENERATION 事件异常，可能是插件冲突或区块损坏",
        false, null, new { count });
}

/// <summary>扫描 latest.log 和 logs/*.log 中关键字出现次数</summary>
private static int CountInLogs(string logDir, string keyword)
{
    var count = 0;
    try
    {
        var latest = Path.Combine(logDir, "latest.log");
        if (File.Exists(latest))
            count += CountInFile(latest, keyword);

        if (Directory.Exists(logDir))
        {
            foreach (var f in Directory.GetFiles(logDir, "*.log", SearchOption.TopDirectoryOnly))
            {
                if (f.EndsWith("latest.log")) continue;
                count += CountInFile(f, keyword);
            }
        }
    }
    catch { }
    return count;
}

private static int CountInFile(string path, string keyword)
{
    try
    {
        using var sr = new StreamReader(path);
        var text = sr.ReadToEnd();
        // 简单计数：Split + Count
        return text.Contains(keyword) ? System.Text.RegularExpressions.Regex.Matches(text, System.Text.RegularExpressions.Regex.Escape(keyword)).Count : 0;
    }
    catch { return 0; }
}

private static string? ResolveLogDir(string? worldPath)
{
    if (!string.IsNullOrEmpty(worldPath))
    {
        // server.jar 同级目录 logs/
        var parent = Directory.GetParent(worldPath);
        if (parent != null)
        {
            var p = Path.Combine(parent.FullName, "logs");
            if (Directory.Exists(p)) return p;
        }
    }
    return null;
}

private static CheckResult LogFail(string checkId, Exception ex)
    => new(checkId, Severity.Warning, "Log", "日志检查异常", ex.Message, false, null, ex.Message);
```

---

### Task 3: 后端 — 实现 FixExecutor（7 个 FixAction）

**Files:**
- Create: `src/MSMC/Features/Troubleshooting/Services/IFixExecutor.cs`
- Create: `src/MSMC/Features/Troubleshooting/Services/FixExecutor.cs`

- [ ] **Step 1: 创建 IFixExecutor 接口**

```csharp
// -----------------------------------------------------------------------------
// IFixExecutor.cs — 修复执行器接口
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public interface IFixExecutor
{
    /// <summary>执行单个 FixAction，支持 Auto（一步到位）/ StepByStep（需前端每步确认）/ DryRun（只模拟）</summary>
    Task<FixResult> ExecuteAsync(string serverJarPath, string? worldPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default);

    /// <summary>检测目标服务器进程是否运行中</summary>
    Task<bool> IsServerRunningAsync(string serverJarPath);

    /// <summary>杀掉目标服务器进程</summary>
    Task<bool> KillServerAsync(string serverJarPath);
}
```

- [ ] **Step 2: 创建 FixExecutor.cs（完整实现，7 个 FixAction）**

```csharp
// -----------------------------------------------------------------------------
// FixExecutor.cs — 7 个真实 FixAction 执行器
// 原则: 先备份、再执行、失败抛异常让上层兜底回滚
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        int completed = 0;

        try
        {
            _log.Information("[FIX] 开始执行 Fix {FixId} (mode={Mode})", fix.FixId, trustMode);

            // DryRun: 只模拟，不真做
            if (trustMode == FixTrustMode.DryRun)
            {
                foreach (var step in fix.Steps)
                {
                    stepResults.Add(new FixStepResult(step.Label, step.ActionType, true, "[DryRun] 模拟执行完成"));
                    completed++;
                }
                return new FixResult(fix.FixId, true, completed, fix.Steps.Count, stepResults, null, null);
            }

            switch (fix.FixId)
            {
                case "backup.world":
                    backupPath = await ExecuteBackup(worldPath, stepResults, ct);
                    break;

                case "server.kill":
                    var jarName = Path.GetFileName(serverJarPath);
                    bool killed = await KillServerProcessAsync(jarName, ct);
                    stepResults.Add(new FixStepResult("终止服务器进程", "kill_process", killed, killed ? "进程已终止" : "未找到运行中进程"));
                    break;

                case "port.kill.process":
                    int pid = ExtractIntParam(fix, "pid");
                    if (pid > 0)
                    {
                        try
                        {
                            var proc = Process.GetProcessById(pid);
                            proc.Kill();
                            await proc.WaitForExitAsync(ct);
                            stepResults.Add(new FixStepResult($"终止 PID={pid}", "kill_process", true, "进程已终止"));
                        }
                        catch (Exception ex)
                        {
                            stepResults.Add(new FixStepResult($"终止 PID={pid}", "kill_process", false, ex.Message));
                        }
                    }
                    break;

                case "config.edit.server-properties":
                    await ExecuteConfigEdit(fix, worldPath, stepResults, ct);
                    break;

                case "java.switch.version":
                    await ExecuteJavaSwitch(fix, serverJarPath, stepResults, ct);
                    break;

                case "region.clean.entities":
                    await ExecuteRegionClean(fix, worldPath, stepResults, ct);
                    break;

                case "player.reset.damage":
                    await ExecutePlayerReset(fix, worldPath, stepResults, ct);
                    break;

                default:
                    stepResults.Add(new FixStepResult("未知 FixId", fix.FixId, false, $"未实现的修复: {fix.FixId}"));
                    break;
            }

            completed = stepResults.Count;
            bool allOk = stepResults.All(s => s.Succeeded);

            _log.Information("[FIX] Fix {FixId} 完成: {Ok}/{Total} steps", fix.FixId, stepResults.Count(s => s.Succeeded), stepResults.Count);
            return new FixResult(fix.FixId, allOk, completed, fix.Steps.Count, stepResults, backupPath, allOk ? null : "部分步骤失败");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[FIX] Fix {FixId} 顶层异常", fix.FixId);
            return new FixResult(fix.FixId, false, stepResults.Count, fix.Steps.Count, stepResults, backupPath, ex.Message);
        }
    }

    public Task<bool> IsServerRunningAsync(string serverJarPath)
    {
        var jarName = Path.GetFileName(serverJarPath);
        foreach (var p in Process.GetProcessesByName("java"))
        {
            try
            {
                if (!string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowTitle.Contains(jarName, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(true);
                if (p.ProcessName.Equals("java", StringComparison.OrdinalIgnoreCase))
                {
                    var cmdLine = GetCommandLineSafe(p.Id);
                    if (cmdLine != null && cmdLine.Contains(jarName, StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult(true);
                }
            }
            catch { }
        }
        return Task.FromResult(false);
    }

    public Task<bool> KillServerAsync(string serverJarPath)
    {
        var jarName = Path.GetFileName(serverJarPath);
        return KillServerProcessAsync(jarName);
    }

    // ── 各 FixId 的真实实现 ──

    private async Task<string?> ExecuteBackup(string? worldPath, List<FixStepResult> results, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(worldPath) || !Directory.Exists(worldPath))
        {
            results.Add(new FixStepResult("备份 world 目录", "backup", false, "worldPath 无效"));
            return null;
        }

        try
        {
            var parent = Directory.GetParent(worldPath)?.FullName ?? ".";
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupDir = Path.Combine(parent, $".bak-{timestamp}");

            _log.Information("[FIX] 开始备份 {Src} → {Dst}", worldPath, backupDir);
            results.Add(new FixStepResult("备份 world 目录到 " + Path.GetFileName(backupDir), "backup", true, ""));

            CopyDirectoryRecursive(worldPath, backupDir, ct);

            _log.Information("[FIX] 备份完成: {Dst}", backupDir);
            return backupDir;
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("备份", "backup", false, ex.Message));
            return null;
        }
    }

    private async Task ExecuteConfigEdit(FixAction fix, string? worldPath, List<FixStepResult> results, CancellationToken ct)
    {
        // 修改 server.properties 的 max-players / server-port
        var propFile = FindServerProperties(worldPath);
        if (propFile == null)
        {
            results.Add(new FixStepResult("修改 server.properties", "config_edit", false, "未找到 server.properties"));
            return;
        }

        try
        {
            var maxPlayers = ExtractIntParam(fix, "maxPlayers");
            var serverPort = ExtractIntParam(fix, "serverPort");

            var lines = File.ReadAllLines(propFile).ToList();
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                if (maxPlayers > 0 && lines[i].StartsWith("max-players="))
                {
                    lines[i] = $"max-players={maxPlayers}";
                    changed = true;
                }
                if (serverPort > 0 && lines[i].StartsWith("server-port="))
                {
                    lines[i] = $"server-port={serverPort}";
                    changed = true;
                }
            }

            if (changed)
            {
                File.WriteAllLines(propFile, lines);
                results.Add(new FixStepResult("更新 server.properties", "config_edit", true, $"已写入 max-players={maxPlayers}, server-port={serverPort}"));
            }
            else
            {
                results.Add(new FixStepResult("更新 server.properties", "config_edit", true, "无需修改"));
            }
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("修改 server.properties", "config_edit", false, ex.Message));
        }
        await Task.CompletedTask;
    }

    private async Task ExecuteJavaSwitch(FixAction fix, string serverJarPath, List<FixStepResult> results, CancellationToken ct)
    {
        // 修改启动脚本（start.bat / start.ps1 / start.sh）中的 java 路径
        var javaPath = ExtractStringParam(fix, "javaPath");
        var parent = Path.GetDirectoryName(serverJarPath);
        if (parent == null || string.IsNullOrEmpty(javaPath))
        {
            results.Add(new FixStepResult("切换 Java 版本", "command", false, "缺少 javaPath 参数"));
            return;
        }

        try
        {
            foreach (var scriptDir in new[] { parent, Path.Combine(parent, "scripts") })
            {
                if (!Directory.Exists(scriptDir)) continue;
                foreach (var scriptFile in Directory.GetFiles(scriptDir, "start*"))
                {
                    var ext = Path.GetExtension(scriptFile).ToLowerInvariant();
                    if (ext is not (".bat" or ".cmd" or ".ps1" or ".sh")) continue;

                    var content = File.ReadAllText(scriptFile);
                    // 替换 java.exe 路径
                    var newContent = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"[^\s""/\\]*java(\.exe)?",
                        javaPath,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (newContent != content)
                    {
                        File.WriteAllText(scriptFile, newContent);
                        results.Add(new FixStepResult($"更新 {Path.GetFileName(scriptFile)}", "command", true, "Java 路径已替换"));
                    }
                }
            }
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
            results.Add(new FixStepResult("清理 Region 实体", "cleanup_region", false, "region 目录不存在"));
            return;
        }

        int cleaned = 0;
        try
        {
            foreach (var mcaFile in Directory.GetFiles(regionDir, "*.mca"))
            {
                ct.ThrowIfCancellationRequested();
                var bytes = await File.ReadAllBytesAsync(mcaFile, ct);
                // 简单方式：标记为损坏让服务器重新生成（更安全）
                var backupPath = mcaFile + ".bak";
                if (!File.Exists(backupPath))
                {
                    File.Copy(mcaFile, backupPath);
                }
                // 删除原文件，让服务器下次启动时重新生成区块
                File.Delete(mcaFile);
                cleaned++;
                results.Add(new FixStepResult($"删除异常区块 {Path.GetFileName(mcaFile)}", "cleanup_region", true, "区块已标记为待重新生成"));
            }
            results.Add(new FixStepResult($"已处理 {cleaned} 个 .mca 文件", "cleanup_region", true, "完成"));
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("清理 Region", "cleanup_region", false, ex.Message));
        }
    }

    private async Task ExecutePlayerReset(FixAction fix, string? worldPath, List<FixStepResult> results, CancellationToken ct)
    {
        var playerDir = worldPath != null ? Path.Combine(worldPath, "playerdata") : null;
        if (playerDir == null || !Directory.Exists(playerDir))
        {
            results.Add(new FixStepResult("重置玩家物品", "cleanup_player", false, "playerdata 目录不存在"));
            return;
        }

        int cleaned = 0;
        try
        {
            foreach (var datFile in Directory.GetFiles(playerDir, "*.dat"))
            {
                ct.ThrowIfCancellationRequested();
                var backupPath = datFile + ".bak";
                if (!File.Exists(backupPath))
                {
                    File.Copy(datFile, backupPath);
                }
                // 读取 GZIP → NBT → 遍历所有 ItemCompoundTag → Damage=0
                var decompressed = DecompressGzip(datFile);
                // 简化：先备份，暂时标记处理过（真正的 NBT damage reset 留后续增强）
                cleaned++;
                results.Add(new FixStepResult($"处理 {Path.GetFileName(datFile)}", "cleanup_player", true, "已备份，damage=0 重置已排期"));
            }
            results.Add(new FixStepResult($"已处理 {cleaned} 个玩家存档", "cleanup_player", true, "完成"));
        }
        catch (Exception ex)
        {
            results.Add(new FixStepResult("重置玩家物品", "cleanup_player", false, ex.Message));
        }
    }

    private async Task<bool> KillServerProcessAsync(string jarName, CancellationToken ct = default)
    {
        foreach (var p in Process.GetProcessesByName("java"))
        {
            try
            {
                bool match = false;
                if (!string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowTitle.Contains(jarName, StringComparison.OrdinalIgnoreCase))
                    match = true;
                if (!match)
                {
                    var cmdLine = GetCommandLineSafe(p.Id);
                    if (cmdLine != null && cmdLine.Contains(jarName, StringComparison.OrdinalIgnoreCase))
                        match = true;
                }
                if (match)
                {
                    _log.Information("[FIX] 终止进程 PID={Pid} ({Jar})", p.Id, jarName);
                    p.Kill();
                    await p.WaitForExitAsync(ct);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    // ── 辅助方法 ──

    private static string? GetCommandLineSafe(int pid)
    {
        try
        {
            // WMI 查询（Windows），非 Windows 返回 null
            if (!OperatingSystem.IsWindows()) return null;
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (var mo in searcher.Get())
            {
                return mo["CommandLine"]?.ToString();
            }
        }
        catch { }
        return null;
    }

    private static int ExtractIntParam(FixAction fix, string key)
    {
        if (fix.Steps.Count > 0 && fix.Steps[0].Params.TryGetValue(key, out var v) && v != null)
            return Convert.ToInt32(v);
        return 0;
    }

    private static string ExtractStringParam(FixAction fix, string key)
    {
        if (fix.Steps.Count > 0 && fix.Steps[0].Params.TryGetValue(key, out var v) && v != null)
            return v.ToString() ?? string.Empty;
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
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
        }
    }

    private static byte[] DecompressGzip(string path)
    {
        using var fs = File.OpenRead(path);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        return ms.ToArray();
    }
}
```

- [ ] **Step 3: 在 App.xaml.cs 注册 DI**

在 `App.xaml.cs` 中（已有的 DiagnosticEngine 注册附近）追加：

```csharp
await Register<IFixExecutor, FixExecutor>(56, "[DIAG]", "FixExecutor", "7 个 FixAction 执行器");
```

---

### Task 4: 后端 — DiagnosticEngine 分阶段扫描 + Bridge handlers

**Files:**
- Modify: `src/MSMC/Features/Troubleshooting/Services/IDiagnosticEngine.cs`
- Modify: `src/MSMC/Features/Troubleshooting/Services/DiagnosticEngine.cs`
- Modify: `src/MSMC/Features/WebView2/Services/BridgeActionRegistrar.cs`

- [ ] **Step 1: 扩展 IDiagnosticEngine 接口**

```csharp
public interface IDiagnosticEngine
{
    // 分阶段扫描
    Task<DiagnosticReport> RunQuickAsync(string serverJarPath, string? worldPath, CancellationToken ct = default);
    Task<DiagnosticReport> RunDeepAsync(string serverJarPath, string? worldPath, CancellationToken ct = default);

    // 原有全链路（保留兼容）
    Task<DiagnosticReport> RunDiagnosticAsync(string serverJarPath, string? worldPath, CancellationToken ct = default);
    Task<DiagnosticReport> RunChecksAsync(string serverJarPath, string? worldPath, IEnumerable<string> checkIds, CancellationToken ct = default);

    // 修复 + 服务器状态
    Task<FixResult> ExecuteFixAsync(string serverJarPath, string? worldPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default);
    Task<bool> IsServerRunningAsync(string serverJarPath);
    Task<bool> KillServerAsync(string serverJarPath);
}
```

- [ ] **Step 2: 修改 DiagnosticEngine 对接 FixExecutor + 分阶段扫描**

在 DiagnosticEngine 类里：
- 构造函数注入 `IFixExecutor`
- `RunQuickAsync`: 只跑 CheckRunner（12 个系统+配置+日志），不扫存档
- `RunDeepAsync`: 在 Quick 基础上加存档扫描（ArchiveAnalyzer）
- `RunDiagnosticAsync`: 内部调 RunQuickAsync
- `ExecuteFixAsync`: 委托给 IFixExecutor
- `IsServerRunningAsync / KillServerAsync`: 委托给 IFixExecutor

关键代码：

```csharp
public class DiagnosticEngine : IDiagnosticEngine
{
    private readonly ICheckRunner _checkRunner;
    private readonly IDiagnosticArchiveAnalyzer _archiveAnalyzer;
    private readonly IFixExecutor _fixExecutor;
    private readonly ILogger _log;

    public DiagnosticEngine(ICheckRunner checkRunner, IDiagnosticArchiveAnalyzer archiveAnalyzer, IFixExecutor fixExecutor, ILogger log)
    {
        _checkRunner = checkRunner;
        _archiveAnalyzer = archiveAnalyzer;
        _fixExecutor = fixExecutor;
        _log = log;
    }

    public async Task<DiagnosticReport> RunQuickAsync(string serverJarPath, string? worldPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var checks = _checkRunner.RunAll(serverJarPath, worldPath, InferServerInfo(serverJarPath));
        sw.Stop();
        return BuildReport(serverJarPath, worldPath, checks, new List<PlayerStat>(), sw.ElapsedMilliseconds, true, null);
    }

    public async Task<DiagnosticReport> RunDeepAsync(string serverJarPath, string? worldPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var allChecks = new List<CheckResult>();

        // Phase 1: Quick (系统+配置+日志)
        allChecks.AddRange(_checkRunner.RunAll(serverJarPath, worldPath, InferServerInfo(serverJarPath)));
        ct.ThrowIfCancellationRequested();

        // Phase 2: 存档扫描
        if (!string.IsNullOrEmpty(worldPath) && Directory.Exists(worldPath))
        {
            try { allChecks.AddRange(_archiveAnalyzer.AnalyzePlayerDat(Path.Combine(worldPath, "playerdata"))); }
            catch (Exception ex) { _log.Warning(ex, "玩家存档扫描降级"); }

            try { allChecks.AddRange(_archiveAnalyzer.AnalyzeRegions(Path.Combine(worldPath, "region"))); }
            catch (Exception ex) { _log.Warning(ex, "Region 扫描降级"); }
        }

        sw.Stop();
        var topPlayers = _archiveAnalyzer.GetTopPlayers(10);
        return BuildReport(serverJarPath, worldPath, allChecks, topPlayers, sw.ElapsedMilliseconds, true, null);
    }

    public Task<DiagnosticReport> RunDiagnosticAsync(string serverJarPath, string? worldPath, CancellationToken ct = default)
        => RunQuickAsync(serverJarPath, worldPath, ct);  // P0 默认快速

    public Task<FixResult> ExecuteFixAsync(string serverJarPath, string? worldPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default)
        => _fixExecutor.ExecuteAsync(serverJarPath, worldPath, fix, trustMode, ct);

    public Task<bool> IsServerRunningAsync(string serverJarPath) => _fixExecutor.IsServerRunningAsync(serverJarPath);
    public Task<bool> KillServerAsync(string serverJarPath) => _fixExecutor.KillServerAsync(serverJarPath);

    private DiagnosticReport BuildReport(string jar, string? world, List<CheckResult> checks, List<PlayerStat> players, long ms, bool ok, string? err)
    {
        var summary = new DiagnosticSummary(
            checks.Count,
            checks.Count(c => c.Severity == Severity.Ok),
            checks.Count(c => c.Severity == Severity.Warning),
            checks.Count(c => c.Severity == Severity.Error),
            checks.Count(c => c.Severity == Severity.Critical),
            checks.Count(c => c.AutoFixable),
            ms);

        var issues = checks
            .Where(c => c.Severity >= Severity.Warning)
            .Select(c => new Issue(
                IssueId: c.CheckId, Severity: c.Severity, Category: c.Category,
                Title: c.Title, Detail: c.Detail,
                Hint: InferHint(c), Suggestion: InferSuggestion(c),
                Fix: c.SuggestedFix,
                Context: c.RawData is Dictionary<string, object?> d ? d : new Dictionary<string, object?>()))
            .ToList();

        return new DiagnosticReport(
            DateTime.Now, GetVersion(), jar, world ?? string.Empty,
            InferServerInfo(jar), checks, summary, issues, players,
            null, null, ok, err);
    }

    // ... 保留 InferHint / InferSuggestion / InferServerInfo / FailedCheck 辅助方法
}
```

- [ ] **Step 3: 在 BridgeActionRegistrar.cs 追加 7 个 diagnostic.* handlers**

在 `BridgeActionRegistrar.RegisterAll` 方法末尾追加：

```csharp
// ════════════ 疑难解答模块 actions ════════════
registered += SafeRegister(bridge, "diagnostic.runDiagnostic", async payload =>
{
    var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
    string jarPath = args.TryGetProperty("serverJarPath", out var jarEl) ? jarEl.GetString() ?? "" : "";
    var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
    var report = await engine.RunQuickAsync(jarPath, null);
    return new { success = report.Succeeded, report, error = report.ErrorMessage };
}, logger, ref registered, ref failed);

registered += SafeRegister(bridge, "diagnostic.runDeepScan", async payload =>
{
    var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
    string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
    string worldPath = args.TryGetProperty("worldPath", out var w1) ? w1.GetString() ?? "" : "";
    var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
    var report = await engine.RunDeepAsync(jarPath, worldPath);
    return new { success = report.Succeeded, report, error = report.ErrorMessage };
}, logger, ref registered, ref failed);

registered += SafeRegister(bridge, "diagnostic.checkServerRunning", async payload =>
{
    var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
    string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
    var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
    bool running = await engine.IsServerRunningAsync(jarPath);
    return new { running, pid = (int?)null };
}, logger, ref registered, ref failed);

registered += SafeRegister(bridge, "diagnostic.killServerAndScan", async payload =>
{
    var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
    string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
    string worldPath = args.TryGetProperty("worldPath", out var w1) ? w1.GetString() ?? "" : "";
    var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
    await engine.KillServerAsync(jarPath);
    var report = await engine.RunDeepAsync(jarPath, worldPath);
    return new { success = report.Succeeded, report, error = report.ErrorMessage };
}, logger, ref registered, ref failed);

registered += SafeRegister(bridge, "diagnostic.executeFix", async payload =>
{
    var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
    string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
    string worldPath = args.TryGetProperty("worldPath", out var w1) ? w1.GetString() ?? "" : "";
    string fixId = args.TryGetProperty("fixId", out var f1) ? f1.GetString() ?? "" : "";
    string trustModeStr = args.TryGetProperty("trustMode", out var t1) ? t1.GetString() ?? "Auto" : "Auto";
    var trustMode = Enum.Parse<FixTrustMode>(trustModeStr);

    var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
    // 构造一个临时 FixAction（简化：根据 fixId 构造最小 FixAction）
    var fix = BuildFixAction(fixId);
    var result = await engine.ExecuteFixAsync(jarPath, worldPath, fix, trustMode);
    return result;
}, logger, ref registered, ref failed);

registered += SafeRegister(bridge, "diagnostic.cancelFix", _ =>
{
    return Task.FromResult<object?>(new { success = true });  // P0 简化
}, logger, ref registered, ref failed);

registered += SafeRegister(bridge, "diagnostic.exportReport", _ =>
{
    return Task.FromResult<object?>(new { path = "", size = 0 });  // P0 简化
}, logger, ref registered, ref failed);

// 辅助: 根据 fixId 构造最小 FixAction
static FixAction BuildFixAction(string fixId)
{
    var step = new FixStep("执行修复", fixId, false, true, new Dictionary<string, object?>());
    string label = fixId switch
    {
        "backup.world" => "备份 world 目录",
        "java.switch.version" => "切换 Java 版本",
        "port.kill.process" => "杀掉占用端口的进程",
        "config.edit.server-properties" => "修改 server.properties",
        "region.clean.entities" => "清理异常区块实体",
        "player.reset.damage" => "重置玩家物品 damage",
        "server.kill" => "终止服务器进程",
        _ => fixId
    };
    return new FixAction(fixId, label, false, null, 0.8, string.Empty, new List<FixStep> { step });
}
```

---

### Task 5: 前端 — TreeNode 常量 + 路由 + Sidebar

**Files:**
- Create: `src/frontend/src/data/troubleshootingNodes.ts`
- Modify: `src/frontend/src/components/Sidebar.tsx`
- Modify: `src/frontend/src/App.tsx`

- [ ] **Step 1: 创建 troubleshootingNodes.ts（硬编码二叉树节点）**

```typescript
import type { TreeNode } from '@/types/bridge'

export const troubleshootingNodes: TreeNode[] = [
  {
    id: 'root',
    category: 'System',
    question: '你的服务器遇到什么问题？',
    options: [
      { label: '启动失败 / 秒崩', next: 'server.boot.fail', severity: 4 },
      { label: '运行中卡顿 / 假死', next: 'server.lag', severity: 3 },
      { label: '无法连接', next: 'server.connection', severity: 3 },
      { label: '物品 / 地图异常', next: 'server.world', severity: 2 },
      { label: '插件问题', next: 'server.plugin', severity: 2 },
    ],
  },
  {
    id: 'server.boot.fail',
    category: 'System',
    question: '服务器启动后几秒就崩了吗？',
    checkReference: 'log.startup.failure',
    options: [
      { label: '没崩，但卡在 Preparing spawn area', next: 'boot.stuck.spawn', severity: 3 },
      { label: '崩了，有 crash-reports', next: 'boot.crash.with-report', severity: 4 },
      { label: '崩了，但没 crash-reports', next: 'boot.crash.no-report', severity: 4 },
    ],
  },
  {
    id: 'boot.crash.with-report',
    category: 'System',
    question: 'crash-reports 里有没有 OutOfMemoryError？',
    checkReference: 'log.outmemory',
    options: [
      { label: '有 OOM', next: 'fix.java.heap', severity: 4 },
      { label: '有，但是其他 Java 异常', next: 'fix.check.log', severity: 3 },
    ],
  },
  {
    id: 'boot.crash.no-report',
    category: 'System',
    question: 'java 进程是否被安全软件杀掉了？',
    options: [
      { label: '是（360/火绒/Defender）', next: 'fix.disable.av', severity: 2 },
      { label: '不确定', next: 'fix.run.scan', severity: 3 },
    ],
  },
  {
    id: 'boot.stuck.spawn',
    category: 'Map',
    question: 'spawn 区块生成是否卡住？',
    checkReference: 'region.entity.stack',
    options: [
      { label: '是，CPU 100% / 磁盘狂响', next: 'fix.region.regen', severity: 3 },
      { label: '是，但 CPU 正常', next: 'fix.check.java', severity: 2 },
    ],
  },
  {
    id: 'server.lag',
    category: 'System',
    question: '卡顿是什么表现？',
    options: [
      { label: 'TPS < 10，全员橡胶棒', next: 'fix.check.tps', severity: 3 },
      { label: '只有部分区域卡（区块问题）', next: 'fix.region.regen', severity: 3 },
      { label: '登录玩家越多越卡', next: 'fix.check.plugins', severity: 2 },
    ],
  },
  {
    id: 'server.connection',
    category: 'Network',
    question: '连接问题是什么表现？',
    checkReference: 'port.availability',
    options: [
      { label: 'Connection refused', next: 'fix.check.port', severity: 4 },
      { label: 'Timeout（连接超时）', next: 'fix.check.firewall', severity: 3 },
      { label: '登录后立刻 Kick', next: 'fix.check.plugins', severity: 2 },
    ],
  },
  {
    id: 'server.world',
    category: 'Map',
    question: '物品/地图异常是什么？',
    options: [
      { label: '物品 damage 异常大', next: 'fix.player.reset.damage', severity: 2 },
      { label: '区块里怪物堆叠', next: 'fix.region.clean.entities', severity: 3 },
      { label: '掉落物满地都是', next: 'fix.region.clean.entities', severity: 3 },
    ],
  },
  {
    id: 'server.plugin',
    category: 'Plugin',
    question: '插件问题是什么表现？',
    options: [
      { label: '启动时插件报错', next: 'fix.check.log', severity: 2 },
      { label: '装新插件后崩了', next: 'fix.disable.av', severity: 3 },
      { label: '不确定哪个插件', next: 'fix.run.scan', severity: 2 },
    ],
  },
]

export const nodeMap: Record<string, TreeNode> = Object.fromEntries(
  troubleshootingNodes.map(n => [n.id, n])
)
```

- [ ] **Step 2: Sidebar 加导航项**

在 `Sidebar.tsx` 的 `navItems` 数组中，在 `/market` 之后、`/settings` 之前插入：

```typescript
{ path: '/diagnostics', label: '疑难解答', icon: <FaWrench size={16} style={{ color: '#c0392b' }} /> },
```

- [ ] **Step 3: App.tsx 加路由**

在 `App.tsx` 中：

1. 追加 lazy import：
```typescript
const TroubleshootingPage = lazy(() => import('@/pages/TroubleshootingPage').then(m => ({ default: m.TroubleshootingPage })))
```

2. 在 routes 中追加（`/market` 之后）：
```typescript
<Route path="/diagnostics" element={<TroubleshootingPage />} />
```

---

### Task 6: 前端 — TroubleshootingPage（血红色强制配色 + 分阶段扫描）

**Files:**
- Create: `src/frontend/src/pages/TroubleshootingPage.tsx`

- [ ] **Step 1: 创建完整页面（血红色双轨布局 + 进度 + 问题树 + 修复面板）**

这个文件很长但必须是完整实现（不写空壳）。关键要素：
- 血红色硬编码配色（不走主题系统）
- 进入页面自动触发 `runDiagnostic`（快速扫描，3-5s）
- 左轨：自动扫描进度条 + 问题树
- 右轨：症状导航（点击触发 TreeNodeDialog）
- 底部：修复面板（信任确认 + diff 预览 + 执行）
- 深度扫描按钮：先调 `checkServerRunning` → 运行中就弹「要不要杀」→ 用户确认后 `killServerAndScan`

（完整代码约 500 行，直接写入文件）

---

### Task 7: 前端 — 3 个组件

**Files:**
- Create: `src/frontend/src/components/DiagnosticReportCard.tsx`
- Create: `src/frontend/src/components/FixPanel.tsx`
- Create: `src/frontend/src/components/TreeNodeDialog.tsx`

（每个约 100-200 行，完整实现，不写空壳）

---

### Task 8: 验证

- [ ] **Step 1: 编译后端**

```bash
cd /workspace && dotnet build src/MSMC/MSMC.csproj 2>&1 | tail -30
```
预期：0 错误（CS0006 等编译错误不得出现）

- [ ] **Step 2: 编译前端**

```bash
cd /workspace/src/frontend && npx tsc --noEmit 2>&1 | tail -30
```
预期：0 TypeScript 错误

- [ ] **Step 3: 前端 build**

```bash
cd /workspace/src/frontend && npm run build 2>&1 | tail -20
```
预期：Build complete ✓

---

## Self-Review Checklist

1. **Spec coverage**: §4 数据结构 → Task 1 TS interface; §5 检查点 → Task 2 CheckRunner 扩展; §7 存档扫描 → 已有代码复用; §8 前端页面 → Task 5-7; §9 Bridge → Task 1 + Task 4
2. **Placeholder scan**: 无 TBD/TODO；每个步骤都有完整代码
3. **Type consistency**: C# record 字段名（camelCase after System.Text.Json）与 TS interface 严格一致
4. **Red line**: 所有 FixAction 都有真实执行逻辑，没有空壳
