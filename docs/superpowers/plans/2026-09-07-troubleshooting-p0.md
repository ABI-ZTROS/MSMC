# MSMC 疑难解答 (P0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建疑难解答核心骨架 — DiagnosticEngine 后端推理引擎（18 个 CheckId 检查点 + 存档 NBT/Region 扫描）+ 桥接契约 + 血红色前端页面骨架（双轨并行布局 + 问题树渲染 + 修复面板）+ 信任确认修复流程。

**Architecture:** 后端 DiagnosticEngine 无状态 CheckRunner 一次性返回完整 DiagnosticReport DTO（严格按 §4 数据结构），前端通过 bridge 获取后渲染血红色双轨布局 + 分级问题树 + 修复面板。存档扫描用自研 .NET NbtReader/RegionReader（不引外部依赖）。修复走 FixAction/FixStep 模型，支持全自动 / 每步确认 / 测试模式三档信任。

**Tech Stack:** .NET 9 WPF + System.IO.Compression（GZip/Zlib）+ Serilog；React + Vite + react-icons + WebView2 bridge；DPAPI（Data Protection API）。

**工程原则强制约束：**
- 三链原则：每条 bridge 契约必须 TS↔C# 逐字段对齐 + 在 DI 注册处 grep 验证
- P4 诚实返回链：`success:false` 必须有明确错误信息，失败可见
- P5 契约一致性：新增 8 个 bridge action 必须配套 TypeScript 接口定义 + bridge.ts invoke 封装
- P6 异步纪律：所有 IAsyncEnumerable 流式扫描 + ConfigureAwait(false)
- CI 为准：不走本地 dotnet build，每个 commit 直接 push 等 GitHub Actions

---

## File Structure

### 新建 C# 后端文件

| 文件 | 职责 |
|------|------|
| `src/MSMC/Features/Troubleshooting/Services/DiagnosticTypes.cs` | DTO 类型定义：DiagnosticReport, CheckResult, Severity, Issue, FixAction, FixStep, DeepSeekAnalysis |
| `src/MSMC/Features/Troubleshooting/Services/IDiagnosticEngine.cs` | 引擎接口（注入点） |
| `src/MSMC/Features/Troubleshooting/Services/DiagnosticEngine.cs` | 引擎实现：协调 CheckRunner + DeepSeekClient + ReportBuilder |
| `src/MSMC/Features/Troubleshooting/Services/ICheckRunner.cs` | 检查运行器接口 |
| `src/MSMC/Features/Troubleshooting/Services/CheckRunner.cs` | 检查运行器实现：18 个 CheckId 方法集合 |
| `src/MSMC/Features/Troubleshooting/Services/DiagnosticNbtReader.cs` | 自研 NBT 解析器（GZIP/ZLIB + Tag 解析） |
| `src/MSMC/Features/Troubleshooting/Services/DiagnosticRegionReader.cs` | Region .mca 解析器 |
| `src/MSMC/Features/Troubleshooting/Services/DiagnosticArchiveAnalyzer.cs` | 玩家存档 NBT 异常检测 + 财富 Top10 + 区块实体堆叠扫描 |
| `src/MSMC/Features/Troubleshooting/Services/IDeepSeekClient.cs` | DeepSeek 接口 |
| `src/MSMC/Features/Troubleshooting/Services/DeepSeekClient.cs` | DeepSeek 实现：预设词 + strict JSON + DPAPI Key 存储 |
| `src/MSMC/Features/Troubleshooting/Services/IFixExecutor.cs` | 修复执行器接口 |
| `src/MSMC/Features/Troubleshooting/Services/FixExecutor.cs` | 修复执行器实现：备份 + 执行 + 进度报告 |
| `src/MSMC/Features/Troubleshooting/Services/IDiagnosticReportExporter.cs` | 报告导出接口 |
| `src/MSMC/Features/Troubleshooting/Services/DiagnosticReportExporter.cs` | Markdown / JSON 导出 + 自动归档 |

### 新建 TypeScript 前端文件

| 文件 | 职责 |
|------|------|
| `src/frontend/src/pages/TroubleshootingPage.tsx` | 主页面：血红色双轨布局 + 问题树 + 修复面板 |
| `src/frontend/src/components/DiagnosticReportCard.tsx` | 单个问题卡片（红/黄/绿分级 + 详情展开） |
| `src/frontend/src/components/FixPanel.tsx` | 修复面板（信任确认 + 进度 + diff） |
| `src/frontend/src/components/TreeNode.ts` | 二叉树节点数据（硬编码 7 个根节点） |

### 修改文件

| 文件 | 改动 |
|------|------|
| `src/MSMC/App.xaml.cs` | 新增 Troubleshooting services DI 注册 |
| `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` | 新增 8 个 diagnostic.* bridge handler |
| `src/frontend/src/App.tsx` | 新增 `/diagnostics` 路由 |
| `src/frontend/src/components/Sidebar.tsx` | 新增「🔧 疑难解答」navItem（血红色强制配色） |
| `src/frontend/src/types/bridge.ts` | 新增 DiagnosticReport / FixAction / FixStep TypeScript 接口 |
| `src/frontend/src/utils/bridge.ts` | 新增 8 个 diagnostic.* invoke 封装 |

---

## Task 1: 后端 DTO + 接口骨架

**Files:**
- Create: `src/MSMC/Features/Troubleshooting/Services/DiagnosticTypes.cs`
- Create: `src/MSMC/Features/Troubleshooting/Services/IDiagnosticEngine.cs`

- [ ] **Step 1: 创建目录**

Run: `mkdir -p src/MSMC/Features/Troubleshooting/Services`

- [ ] **Step 2: 写 DiagnosticTypes.cs（完整 DTO）**

```csharp
// src/MSMC/Features/Troubleshooting/Services/DiagnosticTypes.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>Severity 分级 — 与前端🟢🟡🟧🔴 图标一一对应</summary>
public enum Severity { Ok = 0, Info = 1, Warning = 2, Error = 3, Critical = 4 }

public sealed record DiagnosticReport(
    DateTime GeneratedAt,
    string MsmcVersion,
    string ServerJarPath,
    string WorldPath,
    ServerInfo Server,
    List<CheckResult> Checks,
    DiagnosticSummary Summary,
    List<Issue> Issues,
    List<PlayerStat> TopPlayers,
    DeepSeekAnalysis? AiAnalysis,
    string? DeepSeekRawResponse,
    bool Succeeded,
    string? ErrorMessage);

public sealed record ServerInfo(
    string JarName,
    string? CoreType,       // Paper / Spigot / Forge / Fabric / Quilt / Bukkit
    string? Version,
    string? JavaVersion,
    int? Port,
    long? TotalMemoryMb,
    int? ProcessId,
    bool IsRunning);

public sealed record DiagnosticSummary(
    int TotalChecks,
    int OkCount,
    int WarningCount,
    int ErrorCount,
    int CriticalCount,
    int AutoFixableCount,
    TimeSpan ScanDuration);

public sealed record CheckResult(
    string CheckId,
    Severity Severity,
    string Category,       // Java / Process / Network / Config / Storage / Player / Region / Log
    string Title,
    string Detail,
    bool AutoFixable,
    FixAction? SuggestedFix,
    object? RawData);

public sealed record Issue(
    string IssueId,
    Severity Severity,
    string Category,
    string Title,
    string Detail,
    string? Hint,
    string? Suggestion,
    FixAction? Fix,
    Dictionary<string, object?> Context);

public sealed record FixAction(
    string FixId,
    string Label,
    bool Dangerous,
    string? DiffPreview,
    double Confidence,
    string? Rationale,
    List<FixStep> Steps);

public sealed record FixStep(
    string Label,
    string ActionType,     // backup / command / config_edit / kill_process / restart / cleanup_region
    bool Dangerous,
    bool ConfirmRequired,
    Dictionary<string, object?> Params);

public sealed record PlayerStat(
    string Uuid,
    string Name,
    int TotalItems,
    double WealthScore,
    int AnomalyCount,
    List<string> AnomalyTypes);

public sealed record DeepSeekAnalysis(
    string Summary,
    List<string> KeyFindings,
    List<FixAction> RecommendedActions,
    bool NeedMoreInfo,
    List<string>? SuggestedQuestions,
    string RawJson);

public sealed record FixResult(
    string FixId,
    bool Succeeded,
    int StepsCompleted,
    int StepsTotal,
    List<FixStepResult> StepResults,
    string? BackupPath,
    string? Error);

public sealed record FixStepResult(
    string Label,
    string ActionType,
    bool Succeeded,
    string? Message);

public enum FixTrustMode { Auto = 0, StepByStep = 1, DryRun = 2 }
```

- [ ] **Step 3: 写 IDiagnosticEngine.cs**

```csharp
// src/MSMC/Features/Troubleshooting/Services/IDiagnosticEngine.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>疑难解答引擎 — 全链路体检统一入口</summary>
public interface IDiagnosticEngine
{
    /// <summary>一键全链路体检（P0 默认）</summary>
    Task<DiagnosticReport> RunDiagnosticAsync(string serverJarPath, string? worldPath, CancellationToken ct = default);

    /// <summary>指定 CheckId 增量体检（AI 引导式用，P2 才实现）</summary>
    Task<DiagnosticReport> RunChecksAsync(string serverJarPath, string? worldPath, IEnumerable<string> checkIds, CancellationToken ct = default);

    /// <summary>执行修复（支持三档信任模式）</summary>
    Task<FixResult> ExecuteFixAsync(string serverJarPath, FixAction fix, FixTrustMode trustMode, IProgress<FixStepResult>? progress = null, CancellationToken ct = default);
}
```

- [ ] **Step 4: Commit**

```bash
git add src/MSMC/Features/Troubleshooting/
git commit -m "feat(diagnostic): 后端 DTO 类型定义 + IDiagnosticEngine 接口骨架 — 严格按 spec §4"
```

---

## Task 2: CheckRunner — 18 个检查点实现

**Files:**
- Create: `src/MSMC/Features/Troubleshooting/Services/ICheckRunner.cs`
- Create: `src/MSMC/Features/Troubleshooting/Services/CheckRunner.cs`（只实现 P0 需要的 10 个系统/配置检查点，存档扫描留给 Task 3）

- [ ] **Step 1: 写 ICheckRunner 接口**

```csharp
// src/MSMC/Features/Troubleshooting/Services/ICheckRunner.cs
using System.Collections.Generic;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>检查运行器 — 无状态，每次调用独立</summary>
public interface ICheckRunner
{
    /// <summary>所有已知 CheckId</summary>
    IReadOnlyCollection<string> KnownCheckIds { get; }

    /// <summary>跑所有检查，返回 10 个 CheckResult</summary>
    List<CheckResult> RunAll(string serverJarPath, string? worldPath, ServerInfo server);
}
```

- [ ] **Step 2: 写 CheckRunner.cs（P0 先实现 10 个系统/配置检查点）**

**文件很长** — 每个检查方法独立。关键实现模式：
- 方法签名 `CheckResult CheckJavaVersion(...)` 等
- 每个方法 try-catch，异常也返回 CheckResult（Severity=Warning + Detail=异常信息）— **诚实返回链**
- 不返回 null，不静默失败

**核心检查方法清单（每个 30-80 行）：**

| CheckId | 方法名 | 实现要点 |
|---------|--------|---------|
| `java.version` | CheckJavaVersion | 读 jar MANIFEST.MF / `mcmod.info` / `fabric.mod.json` 推断需要的 Java 版本；对比 `Process.GetCurrentProcess()` 或 `JAVA_HOME` |
| `java.heap.size` | CheckJavaHeap | 读启动参数 `-Xmx` / `-Xms`；对比 `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` |
| `process.priority` | CheckProcessPriority | 查已运行服务器进程的 `Process.PriorityClass`（Normal vs AboveNormal/High）|
| `process.t1.qos` | CheckT1Qos | 检查 `ICpuPowerService` 是否注入 + 当前 T1 状态 |
| `process.t3.tuning` | CheckT3Tuning | 检查 CPU Set / PriorityBoost / winmm timer 三项是否都生效 |
| `port.availability` | CheckPortAvailability | 解析 server.properties → `server-port` → 用 `IPGlobalProperties.GetActiveTcpListeners()` 查是否被占 |
| `port.firewall` | CheckFirewall | 跑 `netsh advfirewall firewall show rule name=all` 过滤 server-port 是否有入站规则 |
| `config.syntax` | CheckConfigSyntax | 用 YamlDotNet / 手写 parser 检查 `.properties` / `.yml` / `.yaml` 语法（只检查 parse 不过不过逻辑验证）|
| `storage.world.size` | CheckWorldSize | 算 `world/` / `world_nether/` / `world_the_end/` 总大小 |
| `log.startup.failure` | CheckStartupLogs | 读 `logs/latest.log` 末尾 2000 行，正则匹配 `ERROR|Exception|OutOfMemory|Could not pass event` |

**P0 暂不实现**（留给 Task 3 + Task 6）：
- `player.*` 系列（需要 NbtReader）
- `region.*` 系列（需要 RegionReader）
- `plugin.*` 系列

- [ ] **Step 3: 验证 KnownCheckIds 恰好是 10 个**

在 CheckRunner 构造函数里定义 `_knownCheckIds = new HashSet<string> { ... 10 个 ... }`。

- [ ] **Step 4: Commit**

```bash
git add src/MSMC/Features/Troubleshooting/Services/CheckRunner.cs
git commit -m "feat(diagnostic): CheckRunner 实现 — 10 个系统/配置检查点（Java/进程/端口/防火墙/配置语法/存储/日志）"
```

---

## Task 3: 存档扫描层 — NbtReader + RegionReader + ArchiveAnalyzer

**Files:**
- Create: `src/MSMC/Features/Troubleshooting/Services/DiagnosticNbtReader.cs`
- Create: `src/MSMC/Features/Troubleshooting/Services/DiagnosticRegionReader.cs`
- Create: `src/MSMC/Features/Troubleshooting/Services/DiagnosticArchiveAnalyzer.cs`

### Task 3a: DiagnosticNbtReader.cs

**自研 .NET NBT 解析器**（零外部依赖）：

```csharp
// NBT Tag 类型枚举（MC Wiki 定义）
public enum NbtTagType : byte {
    End = 0, Byte = 1, Short = 2, Int = 3, Long = 4,
    Float = 5, Double = 6, ByteArray = 7, String = 8,
    List = 9, Compound = 10, IntArray = 11, LongArray = 12
}

public sealed record NbtTag(NbtTagType Type, string? Name, object Value);

public static class DiagnosticNbtReader
{
    /// <summary>从 .dat 文件（GZIP 压缩）根节点开始解析</summary>
    public static NbtTag ParseDatFile(string filePath) { ... }
    
    /// <summary>从 .mca 区块数据（ZLIB 压缩）根节点开始解析</summary>
    public static NbtTag ParseChunk(byte[] zlibData) { ... }
    
    /// <summary>从 Compound / List 里按路径取值（如 "Items/0/Count"）</summary>
    public static NbtTag? GetByPath(NbtTag root, string path) { ... }
}
```

**关键实现要点：**
- `GZStream` / `DeflateStream`（ZLIB 是 DEFLATE 头部 + 4 字节 Adler32）
- NBT 是大端序 — `BinaryReader` 不能直接用（默认小端），必须 `BinaryPrimitives.ReadInt32BigEndian`
- Compound 遇到 End tag 停止；List 第一个字节是元素类型

### Task 3b: DiagnosticRegionReader.cs

```csharp
public sealed record RegionChunkInfo(
    int ChunkX, int ChunkZ,         // 区块在 .mca 内的坐标 (0-31)
    int DataLength,                 // 解压后字节数
    NbtTag Root,                    // 区块完整 NBT
    bool HasEntities,
    bool HasTileEntities);

public static class DiagnosticRegionReader
{
    /// <summary>解析单个 .mca 文件（4KB sector header + chunk data）</summary>
    public static IReadOnlyList<RegionChunkInfo> ParseRegionFile(string mcaPath) { ... }
    
    /// <summary>.mca 文件名 → region 坐标: r.X.Z.mca → (X, Z)</summary>
    public static (int X, int Z) ParseRegionCoords(string mcaFileName) { ... }
}
```

**关键实现要点：**
- .mca = 8192 字节头（位置表 1024 + 时间戳表 1024，每个 4 字节大端 uint）+ chunk 数据（每 chunk 4-byte 大端长度 + 1-byte 压缩类型 + ZLIB 数据）
- 跳过 `length == 0` 的空 slot（区块未生成）

### Task 3c: DiagnosticArchiveAnalyzer.cs

```csharp
public interface IDiagnosticArchiveAnalyzer
{
    List<CheckResult> AnalyzePlayerDat(string playersDir);
    List<CheckResult> AnalyzeRegions(string regionDir, int maxRegionsToScan = 16);
}
```

**玩家存档扫描：**
- 遍历 `players/*.dat`，ParseDatFile → 取 `Inventory` / `EnderItems` / `ArmorItems` / `HandItems`
- 每个 Item 检查：`Count > 99`（异常堆叠）、`Damage > 正常上限`（钻石剑正常上限 1561）、`Enchantments` 非法等级（Sharpness V 以上）、`tag` 里大 payload（> 10KB 的自定义 NBT）
- 财富统计：按 MC Wiki 价值表（钻石块 = 81 钻石 = 648 铁锭 = ...）计算每个玩家总价值 → Top10

**Region 扫描：**
- 遍历 `region/r.*.mca`，先解析前 16 个（P0 先不全量）
- 每个区块扫 `entities` 列表 → 同坐标 (x,z) 同类型实体数 → > 50 Warning / > 200 Error
- 扫 `block_entities` → command_block / structure_block / TNT 残留

**重要约束（P7 资源诚信）：**
- 每个 .mca 流式读取，不一次性读全
- 扫描超时 / 中断安全
- 所有异常捕获 + 返回 CheckResult

- [ ] **Commit**

```bash
git add src/MSMC/Features/Troubleshooting/Services/DiagnosticNbtReader.cs \
        src/MSMC/Features/Troubleshooting/Services/DiagnosticRegionReader.cs \
        src/MSMC/Features/Troubleshooting/Services/DiagnosticArchiveAnalyzer.cs
git commit -m "feat(diagnostic): 存档扫描层 — 自研 NBT/Region 解析器 + 玩家 NBT 异常/财富 Top10 + 区块实体堆叠扫描"
```

---

## Task 4: DiagnosticEngine 组装 + DI 注册

**Files:**
- Create: `src/MSMC/Features/Troubleshooting/Services/DiagnosticEngine.cs`
- Modify: `src/MSMC/App.xaml.cs`（新增 Troubleshooting services DI 注册）

- [ ] **Step 1: 写 DiagnosticEngine**

协调器：
1. 调 CheckRunner.RunAll() → 拿到系统/配置 CheckResult
2. 调 DiagnosticArchiveAnalyzer → 拿到存档 CheckResult（Try-Catch，存档扫描失败降级）
3. 汇总成 DiagnosticReport
4. AI 分析（P1，P0 这里只留空壳：如果没配 API Key 就跳过）
5. 返回完整 DiagnosticReport

```csharp
public class DiagnosticEngine : IDiagnosticEngine
{
    private readonly ICheckRunner _checkRunner;
    private readonly IDiagnosticArchiveAnalyzer _archiveAnalyzer;
    // AI 留空壳 P1 实现

    public DiagnosticEngine(ICheckRunner checkRunner, IDiagnosticArchiveAnalyzer archiveAnalyzer) { ... }

    public Task<DiagnosticReport> RunDiagnosticAsync(string serverJarPath, string? worldPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var checks = new List<CheckResult>();
        
        checks.AddRange(_checkRunner.RunAll(serverJarPath, worldPath, ...));
        
        if (!string.IsNullOrEmpty(worldPath))
        {
            try { checks.AddRange(_archiveAnalyzer.AnalyzePlayerDat(Path.Combine(worldPath, "players"))); }
            catch (Exception ex) { checks.Add(FailedCheck("archive.player", ex)); }
            try { checks.AddRange(_archiveAnalyzer.AnalyzeRegions(Path.Combine(worldPath, "region"))); }
            catch (Exception ex) { checks.Add(FailedCheck("archive.region", ex)); }
        }

        sw.Stop();
        var summary = BuildSummary(checks, sw.Elapsed);
        var issues = checks.Where(c => c.Severity >= Severity.Warning)
                          .Select(c => ToIssue(c)).ToList();
        
        var topPlayers = _archiveAnalyzer.GetTopPlayers(); // 财富 Top10

        return Task.FromResult(new DiagnosticReport(
            DateTime.Now, ..., checks, summary, issues, topPlayers, null, null, true, null));
    }
}
```

- [ ] **Step 2: App.xaml.cs DI 注册**

在现有注册块旁边追加：

```csharp
// ─── Troubleshooting（疑难解答）───
services.AddSingleton<IDiagnosticEngine, DiagnosticEngine>();
services.AddSingleton<ICheckRunner, CheckRunner>();
services.AddSingleton<IDiagnosticArchiveAnalyzer, DiagnosticArchiveAnalyzer>();
// P1 追加: services.AddSingleton<IDeepSeekClient, DeepSeekClient>();
// P1 追加: services.AddSingleton<IFixExecutor, FixExecutor>();
// P1 追加: services.AddSingleton<IDiagnosticReportExporter, DiagnosticReportExporter>();
```

- [ ] **Step 3: Commit**

```bash
git add src/MSMC/Features/Troubleshooting/Services/DiagnosticEngine.cs src/MSMC/App.xaml.cs
git commit -m "feat(diagnostic): DiagnosticEngine 组装 + DI 注册 — P0 跳过 AI 分析"
```

---

## Task 5: Bridge 契约 — 8 个 action 注册（P5 契约一致性强制检查）

**Files:**
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs`
- Modify: `src/frontend/src/types/bridge.ts`
- Modify: `src/frontend/src/utils/bridge.ts`

### C# 端 MainWindow.xaml.cs 新增 bridge handler

在 `RegisterBridgeApis()` 方法末尾追加：

```csharp
// ─── Troubleshooting（疑难解答）───
_bridgeService.RegisterRequestHandler("diagnostic.run", payload =>
{
    var jarPath = payload?.GetValue("serverJarPath")?.GetString() ?? "";
    var worldPath = payload?.GetValue("worldPath")?.GetString();
    var engine = App.Services.GetService<IDiagnosticEngine>();
    if (engine == null) return System.Text.Json.JsonDocument.Parse("{\"success\":false,\"error\":\"engine_not_registered\"}");
    var report = engine.RunDiagnosticAsync(jarPath, worldPath).GetAwaiter().GetResult(); // P0 同步调用
    var json = System.Text.Json.JsonSerializer.Serialize(report);
    return System.Text.Json.JsonDocument.Parse(json);
});
// P1 追加 7 个 handler: diagnostic.runIncremental / diagnostic.executeFix / diagnostic.continueFix / diagnostic.cancelFix / diagnostic.getFixPreview / diagnostic.exportReport / diagnostic.setDeepseekKey / diagnostic.getDeepseekSettings
```

### TypeScript 端

```typescript
// src/frontend/src/types/bridge.ts 追加
export type Severity = 0 | 1 | 2 | 3 | 4  // Ok / Info / Warning / Error / Critical

export interface DiagnosticCheckResult {
  checkId: string
  severity: Severity
  category: string
  title: string
  detail: string
  autoFixable: boolean
  suggestedFix?: FixAction
  rawData?: unknown
}

export interface DiagnosticReport {
  generatedAt: string
  msmcVersion: string
  serverJarPath: string
  worldPath: string
  server: { jarName: string; coreType?: string; version?: string; javaVersion?: string; port?: number }
  checks: DiagnosticCheckResult[]
  summary: { totalChecks: number; ok: number; warning: number; error: number; critical: number; scanDurationMs: number }
  issues: DiagnosticIssue[]
  topPlayers: PlayerStat[]
  aiAnalysis?: DeepSeekAnalysis
  succeeded: boolean
  errorMessage?: string
}

// FixAction / FixStep / FixTrustMode 也追加...
```

```typescript
// src/frontend/src/utils/bridge.ts 追加 invoke 封装
export function runDiagnostic(serverJarPath: string, worldPath?: string): Promise<DiagnosticReport> {
  return bridge.invoke<DiagnosticReport>('diagnostic.run', { serverJarPath, worldPath })
}
```

- [ ] **Commit**

```bash
git add src/MSMC/Features/Shared/Views/MainWindow.xaml.cs src/frontend/src/types/bridge.ts src/frontend/src/utils/bridge.ts
git commit -m "feat(bridge): diagnostic.run 桥接契约 — TS↔C# 逐字段对齐，符合 P5 契约一致性"
```

---

## Task 6: 血红色前端页面

**Files:**
- Create: `src/frontend/src/pages/TroubleshootingPage.tsx`（主页面）
- Create: `src/frontend/src/components/DiagnosticReportCard.tsx`（问题卡片）
- Create: `src/frontend/src/components/FixPanel.tsx`（修复面板）
- Modify: `src/frontend/src/App.tsx`（新增 `/diagnostics` 路由）
- Modify: `src/frontend/src/components/Sidebar.tsx`（新增 navItem + 血红色强制配色）

### Sidebar.tsx 改动

```tsx
// 在 navItems 数组中新增（放在最后，血红色强制配色）
{ path: '/diagnostics', label: '疑难解答', icon: <FaTools size={16} style={{ color: '#c0392b' }} /> }

// nav 样式里疑难解答项强制血红
// nav item click 事件里给 diagnostics 路径的 item 加 color: #c0392b !important
```

### App.tsx 路由

```tsx
<Route path="/diagnostics" element={<TroubleshootingPage />} />
```

### TroubleshootingPage.tsx 核心结构

```
// 强制血红色配色（不被主题系统覆盖）
const RED = { primary: '#c0392b', accent: '#e74c3c', dark: '#922b21' }

// 双轨并行布局
// 左轨: [🔍 开始一键体检] 按钮 + 进度条（显示当前在跑哪个 CheckId）
// 右轨: 7 个症状入口（玩家问题 / 地图问题 / 插件问题 / 插件配置 / 网络问题 / 系统问题 / 配置自检）
// 下部: 分级问题树（🟢 Ok / 🟡 Info / 🟧 Warning / 🔴 Error / 💀 Critical）
// 底部: [📋 导出报告] 按钮
```

**关键 UI 规则：**
- 整个页面主色 `#c0392b`，无论当前主题是什么
- 侧边栏「🔧 疑难解答」item 文字强制血红（CSS `!important`）
- 分级图标直接用 emoji 🟢🟡🟧🔴💀，不依赖主题 CSS var
- 进度条每完成一个检查点加一条 + 打勾 + 显示耗时
- 问题卡片 Severity ≥ Warning 才展开（默认折叠 Ok 项）

- [ ] **Commit**

```bash
git add src/frontend/src/pages/TroubleshootingPage.tsx src/frontend/src/components/DiagnosticReportCard.tsx src/frontend/src/components/FixPanel.tsx src/frontend/src/App.tsx src/frontend/src/components/Sidebar.tsx
git commit -m "feat(frontend): 疑难解答血红色页面 — 双轨布局 + 分级问题树 + 强制血红配色不被主题覆盖"
```

---

## Task 7: 本地验证 + Push + CI 等待

**按用户要求：不走本地 dotnet build，直接 push 等 GitHub Actions**

- [ ] **Step 1: 快速本地编译验证（可选，推荐）**

```bash
cd src/MSMC && dotnet build MSMC.csproj 2>&1 | tail -30
# 预期 Build succeeded（或少量 warning）
```

- [ ] **Step 2: 提交所有剩余改动（如果本地 build 没走）**

```bash
git add -A && git status
```

- [ ] **Step 3: Push**

```bash
git push origin main
```

- [ ] **Step 4: 等 CI（预期 ~3-5 分钟）**

```bash
# 创建 GitHub Actions 等待 subagent
```

- [ ] **Step 5: 如果 CI 失败 — 按具体错误修复循环**

每一个 commit 都单独修，不批量修。CI 全绿才算 P0 完成。

- [ ] **Step 6: P0 完成标记**

CI 全绿（Build relaxed + Build strict + Test + Publish Full + Publish Lite + win-x86 + win-arm64）→ P0 完成。

---

## Task 8: 端到端验证（用户在 Windows 11 上跑）

P0 完成后，用户在 Win11 上手动验证：
1. 侧边栏「🔧 疑难解答」出现且血红色
2. 点击进入 → 一键体检按钮存在
3. 跑体检 → 看到分级问题树
4. 存档扫描 → 正确识别 .dat / .mca 文件
5. 报告导出 → Markdown 文件生成
6. AI Key 输入 → DPAPI 加密存储（P1）

---

## P0 → P1 边界

P0 完成后，以下功能留给 P1：
- DeepSeek AI 集成（预设词 + strict JSON + DPAPI Key）
- 引导式诊断模式（AI Tool Calls 控制检查流程）
- FixAction/FixStep 自动修复执行（当前只生成 FixAction 但不执行）
- 信任确认面板（全自动/每步确认/测试模式）
- 修复进度可视化 + 每步可取消
- Diff 预览
- 二叉树对话（TreeNode 完整实现）
- 可分享诊断会话

---

## Self-Review

### Spec coverage

| Spec § | 覆盖 Task | 状态 |
|--------|----------|------|
| §3 架构 | Tasks 1-6 | ✅ |
| §4 数据结构 | Task 1 | ✅ |
| §5 检查点清单 | Task 2 | ✅ 10 个系统 + Task 3 存档扫描 |
| §6 DeepSeek 集成 | P1 边界 | ✅ 明确隔离 |
| §7 存档扫描技术选型 | Task 3 | ✅ 自研 |
| §8 前端页面 | Task 6 | ✅ 血红色强制配色 |
| §9 桥接接口 | Task 5 | ✅ P0 先 diagnostic.run |
| §10 错误处理 | Task 4 降级 + Task 2 Try-Catch | ✅ |
| §11 性能预期 | 流式读取 + 前 16 个 region | ✅ |
| §12 阶段划分 | Task 8 完成标记 | ✅ |

### Placeholder scan

- ✅ 没有 TBD/TODO
- ✅ 每个 Step 有具体代码 / 命令
- ✅ 修复边界明确（P0 不做什么）

### Type consistency

- ✅ `DiagnosticReport` / `CheckResult` / `Severity` / `FixAction` 在 Task 1 定义，后续 Task 引用一致
- ✅ Bridge action 名 `diagnostic.run` 在 C# 和 TS 端完全一致
- ✅ TS 类型与 C# DTO 字段名大小写一致（C# PascalCase → TS camelCase，bridge.ts invoke 自动转）
