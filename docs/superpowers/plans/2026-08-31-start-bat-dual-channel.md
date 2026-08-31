# start.bat 双通道识别与固化 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MSMC 自动识别服务器目录下的 start.bat / run.bat / *.cmd / *.bat，解析参数快照固化到 KnownServer.StartupConfig，启动时按 Mode 分叉（直调 bat vs 手动 java），Supervisor 互斥适配，Bridge API + 前端 UI 完整闭环。

**Architecture:** KnownServer 加独立 StartupConfig 子对象 → StartupScriptAutoDetector 优先级链扫描 → ServerManagerService.StartServer 内部 Mode 分叉 → Supervisor TrackExistingProcessTree 互斥重启 → 4 个 Bridge action handler → 前端服务器卡片 + 详情面板。

**Tech Stack:** C# (WPF + CommunityToolkit.Mvvm + Serilog), TypeScript (React + react-icons/fa6)

---

## 文件清单

| 操作 | 文件 | 职责 |
|------|------|------|
| Modify | `src/MSMC/Features/ServerDetection/Models/KnownServer.cs` | 加 `StartupConfig? Startup` + `StartupMode` 枚举 |
| Modify | `src/MSMC/Features/ServerDetection/Models/ServerInstance.cs` | 加 `StartupMode` + `ScriptHasAutoRestart` ObservableProperty |
| **New** | `src/MSMC/Features/ServerDetection/Services/StartupScriptAutoDetector.cs` | 优先级链扫描 + Parse + 固化 |
| **New** | `src/MSMC/Features/ServerDetection/Services/ScriptSupervisorInfo.cs` | Supervisor DTO |
| Modify | `src/MSMC/Features/ServerDetection/Services/ServerManagerService.cs` | 分叉启动 + LaunchViaBat |
| Modify | `src/MSMC/Features/Shared/Services/ProcessSupervisorService.cs` | TrackExistingProcessTree |
| Modify | `src/MSMC/Features/ServerDetection/ViewModels/ServerDetectionViewModel.cs` | StartupConfig 传播 |
| Modify | `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` | 4 个 Bridge action |
| Modify | `src/frontend/src/types/bridge.ts` | StartupMode / StartupConfig / DiffReport 类型 |
| Modify | `src/frontend/src/utils/bridge.ts` | 4 个 bridge API 函数 |
| Modify | `src/frontend/src/pages/DashboardPage.tsx` | 服务器卡片 Mode 行 |

---

## Task 1: 数据模型（KnownServer + ServerInstance）

### KnownServer.cs 加 StartupConfig + StartupMode 枚举

- [ ] **Step 1: KnownServer.cs 尾部追加**

在文件末尾（namespace 闭括号之前）加 StartupMode 枚举和 StartupConfig 类，并在 KnownServer 类里加属性。

KnownServer.cs 末尾（当前 line 143 `}` 之后）：
```csharp
} // end of KnownServer

/// <summary>启动模式枚举</summary>
public enum StartupMode
{
    /// <summary>手动组装 java 命令（默认）</summary>
    Manual = 0,
    /// <summary>直接调用 .bat 启动脚本</summary>
    Script = 1,
}

/// <summary>启动配置子对象（脚本检测 + 固化快照）</summary>
public class StartupConfig
{
    public StartupMode Mode { get; set; } = StartupMode.Manual;
    public string? ScriptPath { get; set; }
    public string? ScriptName { get; set; }
    public DateTime? LastParseTime { get; set; }
    public bool HasAutoRestart { get; set; }
    public List<string> ScriptJvmArgs { get; set; } = [];
    public string? ScriptJarPath { get; set; }
    public long ScriptMaxHeapBytes { get; set; }
    public long ScriptInitialHeapBytes { get; set; }
}
```

KnownServer 类内（在 `PerServerSupervisorPolicy? Supervisor` 属性后面追加）：
```csharp
/// <summary>启动配置（启动方式、脚本路径、自动检测快照）</summary>
public StartupConfig? Startup { get; set; }
```

### ServerInstance.cs 加字段

ServerInstance.cs 已有的 `_startupScriptPath` 后面追加：
```csharp
/// <summary>启动模式</summary>
[ObservableProperty] private StartupMode _startupMode = StartupMode.Manual;

/// <summary>脚本是否含自动重启循环（Supervisor 互斥用）</summary>
[ObservableProperty] private bool _scriptHasAutoRestart;
```

- [ ] **Step 2: 编译验证**
Run: `dotnet build src/MSMC/MSMC.csproj 2>&1 | tail -20`
Expected: 成功（0 errors）

---

## Task 2: StartupScriptAutoDetector 服务

### 新建 StartupScriptAutoDetector.cs

- [ ] **Step 1: 创建文件**

路径：`src/MSMC/Features/ServerDetection/Services/StartupScriptAutoDetector.cs`

完整内容：
```csharp
// -----------------------------------------------------------------------------
// 文件名: StartupScriptAutoDetector.cs
// 功能描述: 优先级链扫描服务器目录下的启动脚本，并组合 StartBatParserService
//          + StartupScriptDetector 进行解析，生成 StartupConfig 快照固化。
// 三链原则:
//   因果链: KnownServer.Startup==null / 导入新服 → 扫描 + 解析
//   执行链: 标准文件名链 → 目录兜底 → 用户覆盖优先级最高
//   返回链: 每步结构化日志 + 解析失败兜底返回 null 不阻塞启动
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace io.NET.ZTR_OS.Features.ServerDetection.Services;

public static class StartupScriptAutoDetector
{
    private static readonly ILogger Log = Serilog.Log.ForContext<StartupScriptAutoDetector>();

    /// <summary>标准脚本文件名（按优先级）</summary>
    private static readonly string[] StandardPatterns = ["start.bat", "run.bat", "start.cmd", "run.cmd"];

    /// <summary>
    /// 优先级链查找脚本（用户覆盖优先 → 标准名 → 目录兜底）
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录</param>
    /// <param name="userOverridePath">用户手动指定的脚本路径（可空）</param>
    /// <returns>脚本绝对路径；未找到返回 null</returns>
    public static string? FindScript(string workingDirectory, string? userOverridePath = null)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            return null;

        // 1. 用户手动覆盖（最高优先级）
        if (!string.IsNullOrWhiteSpace(userOverridePath) && File.Exists(userOverridePath))
        {
            Log.Debug("[SCRIPT] 使用用户手动指定脚本: {Path}", userOverridePath);
            return userOverridePath;
        }

        // 2. 标准文件名按优先级
        foreach (var pattern in StandardPatterns)
        {
            var candidate = Path.Combine(workingDirectory, pattern);
            if (File.Exists(candidate))
            {
                Log.Debug("[SCRIPT] 标准命名匹配: {Pattern} → {Path}", pattern, candidate);
                return candidate;
            }
        }

        // 3. 兜底：目录下第一个 .bat / .cmd
        var fallback = Directory.GetFiles(workingDirectory, "*.bat", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(workingDirectory, "*.cmd", SearchOption.TopDirectoryOnly))
            .FirstOrDefault();

        if (fallback != null)
        {
            Log.Debug("[SCRIPT] 兜底匹配目录下脚本: {Path}", fallback);
        }

        return fallback;
    }

    /// <summary>
    /// 组合 StartBatParserService（参数解析）+ StartupScriptDetector（启发式分析）
    /// 生成 StartupConfig 快照。
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录</param>
    /// <param name="existingScriptPath">KnownServer 上已有的脚本路径（可空）</param>
    /// <returns>StartupConfig 快照；未找到或解析失败返回 null</returns>
    public static StartupConfig? AutoDetectAndPopulateStartup(string workingDirectory, string? existingScriptPath = null)
    {
        var scriptPath = FindScript(workingDirectory, existingScriptPath);
        if (scriptPath == null)
        {
            Log.Debug("[SCRIPT] 未在目录找到任何启动脚本: {Dir}", workingDirectory);
            return null;
        }

        // 执行链：两个解析器组合使用
        var parserResult = StartBatParserService.ParseFile(scriptPath, workingDirectory);

        var content = string.Empty;
        try { content = File.ReadAllText(scriptPath); }
        catch (Exception ex) { Log.Warning(ex, "[SCRIPT] 读取脚本失败: {Path}", scriptPath); }

        var heuristic = string.IsNullOrEmpty(content)
            ? new StartupScriptInfo { RawContent = content }
            : StartupScriptDetector.Analyze(content);

        // 执行链兜底：两个解析器都不行则返回 null，不阻塞启动
        if (!parserResult.Success && !heuristic.IsServerStartupScript)
        {
            Log.Warning("[SCRIPT] 找到脚本但解析失败: {Path} | ParserErr={ParserErr}", scriptPath, parserResult.ErrorMessage);
            return null;
        }

        var config = new StartupConfig
        {
            Mode = StartupMode.Manual,
            ScriptPath = scriptPath,
            ScriptName = Path.GetFileName(scriptPath),
            LastParseTime = DateTime.Now,
            HasAutoRestart = heuristic.HasAutoRestart,
            ScriptJvmArgs = parserResult.JvmArguments,
            ScriptJarPath = parserResult.JarPath,
            ScriptMaxHeapBytes = parserResult.MaxHeapBytes ?? 0,
            ScriptInitialHeapBytes = parserResult.InitialHeapBytes ?? 0,
        };

        // 返回链：详细结构化日志
        Log.Information(
            "[SCRIPT] ✅ 脚本检测成功: {Path} | HasAutoRestart={AR} | HeapMax={Max} | HeapInit={Init} | Jar={Jar} | JvmArgsCount={Count}",
            scriptPath, config.HasAutoRestart, config.ScriptMaxHeapBytes, config.ScriptInitialHeapBytes,
            config.ScriptJarPath, config.ScriptJvmArgs.Count);

        if (parserResult.UnknownArgs.Count > 0)
        {
            Log.Debug("[SCRIPT] 未识别参数: {Args}", string.Join(", ", parserResult.UnknownArgs));
        }

        return config;
    }

    /// <summary>
    /// 对比 KnownServer 手动配置 vs StartupConfig 脚本快照，生成 DiffReport。
    /// 用于「手动配置是否偏离脚本原意」的提示。
    /// </summary>
    public static DiffReport? ComputeDiff(KnownServer server, StartupConfig script)
    {
        if (server == null || script == null) return null;

        var manualArgs = server.JvmArguments ?? new List<string>();
        var scriptArgs = script.ScriptJvmArgs ?? new List<string>();

        var manualSet = new HashSet<string>(manualArgs, StringComparer.OrdinalIgnoreCase);
        var scriptSet = new HashSet<string>(scriptArgs, StringComparer.OrdinalIgnoreCase);

        var added = manualArgs.Where(a => !scriptSet.Contains(a)).ToList();
        var removed = scriptArgs.Where(a => !manualSet.Contains(a)).ToList();

        long heapMaxDiff = script.ScriptMaxHeapBytes != 0 && script.ScriptMaxHeapBytes != server.MaxHeapMemoryBytes;
        long heapInitDiff = script.ScriptInitialHeapBytes != 0 && script.ScriptInitialHeapBytes != server.InitialHeapMemoryBytes;

        return new DiffReport
        {
            JarPathChanged = script.ScriptJarPath != null && !string.Equals(script.ScriptJarPath, server.ServerJarPath, StringComparison.OrdinalIgnoreCase),
            HeapMaxFrom = heapMaxDiff ? FormatBytes(script.ScriptMaxHeapBytes) : null,
            HeapMaxTo = heapMaxDiff ? FormatBytes(server.MaxHeapMemoryBytes) : null,
            HeapInitFrom = heapInitDiff ? FormatBytes(script.ScriptInitialHeapBytes) : null,
            HeapInitTo = heapInitDiff ? FormatBytes(server.InitialHeapMemoryBytes) : null,
            JvmArgsAdded = added,
            JvmArgsRemoved = removed,
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0";
        string[] units = ["B", "KB", "MB", "GB"];
        int i = 0;
        double d = bytes;
        while (d >= 1024 && i < units.Length - 1) { d /= 1024; i++; }
        return $"{d:0.##}{units[i]}";
    }
}

/// <summary>手动配置 vs 脚本快照的 Diff 报告（用于 Bridge 返回）</summary>
public class DiffReport
{
    public bool JarPathChanged { get; set; }
    public string? HeapMaxFrom { get; set; }
    public string? HeapMaxTo { get; set; }
    public string? HeapInitFrom { get; set; }
    public string? HeapInitTo { get; set; }
    public List<string> JvmArgsAdded { get; set; } = [];
    public List<string> JvmArgsRemoved { get; set; } = [];
}
```

- [ ] **Step 2: 编译验证**
Run: `dotnet build src/MSMC/MSMC.csproj 2>&1 | tail -20`
Expected: 成功（0 errors）

---

## Task 3: ServerManagerService 分叉启动

### 3a. 新增 ScriptSupervisorInfo.cs

- [ ] **Step 1: 创建文件**

路径：`src/MSMC/Features/ServerDetection/Services/ScriptSupervisorInfo.cs`

```csharp
namespace io.NET.ZTR_OS.Features.ServerDetection.Services;

/// <summary>Supervisor 绑进程树所需的脚本启动信息 DTO</summary>
public class ScriptSupervisorInfo
{
    public string ScriptPath { get; set; } = string.Empty;
    public bool HasAutoRestart { get; set; }
}
```

### 3b. ServerManagerService.StartServer 内部加分叉

- [ ] **Step 2: 找到 StartServer 方法（当前 line 581），在 Case A 之前插入 Mode 分叉**

在 `public Process? StartServer(ServerInstance server)` 方法内，最开头加：

```csharp
// ===== 新增：Mode 分叉 =====
if (server.StartupMode == StartupMode.Script && !string.IsNullOrEmpty(server.StartupScriptPath))
{
    // 用户选了 bat 模式 → 直调脚本
    Log.Information("[BOOT] 模式=Script，走 bat 启动路径: {Script}", server.StartupScriptPath);
    var batProcess = LaunchViaBat(server);

    if (batProcess != null && _supervisor != null)
    {
        try
        {
            var scriptInfo = new ScriptSupervisorInfo
            {
                ScriptPath = server.StartupScriptPath,
                HasAutoRestart = server.ScriptHasAutoRestart,
            };
            var handle = _supervisor.TrackExistingProcessTree(batProcess.Id, scriptInfo);
            if (handle != null)
            {
                Log.Information("[SUP] 进程树已绑定 PID={Pid}, HasAutoRestart={AR}", batProcess.Id, server.ScriptHasAutoRestart);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SUP] Supervisor 绑定进程树失败，降级为裸 bat 启动");
        }
    }

    return batProcess;
}
// ===== end 新增 =====
```

### 3c. 新增 LaunchViaBat 方法

- [ ] **Step 3: 在 ServerManagerService 类内（StartServer 方法之后）追加**

```csharp
/// <summary>
/// 通过 .bat / .cmd 脚本启动服务器进程。
/// </summary>
/// <remarks>
/// 使用 UseShellExecute=false 让 .NET 通过 cmd.exe 执行脚本，
/// 这样可以拿到 PID 传给 Supervisor 绑进程树。
/// bat 启动后 2 秒内检查进程是否立即退出（返回链兜底）。
/// </remarks>
private Process? LaunchViaBat(ServerInstance server)
{
    var scriptPath = server.StartupScriptPath!;
    var batDir = Path.GetDirectoryName(scriptPath) ?? server.WorkingDirectory;

    if (!File.Exists(scriptPath))
    {
        Log.Error("[BOOT] 脚本不存在: {Path}，回退到手动 Java 启动", scriptPath);
        return null;
    }

    Log.Information("[BOOT] 走 bat 启动路径: Script={Script}, WorkingDir={Dir}", scriptPath, batDir);

    var startInfo = new ProcessStartInfo
    {
        FileName = scriptPath,
        WorkingDirectory = batDir,
        UseShellExecute = false,
        CreateNoWindow = false,
    };

    Process process;
    try
    {
        process = Process.Start(startInfo);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "[BOOT] 启动 bat 异常");
        return null;
    }

    if (process == null)
    {
        Log.Error("[BOOT] Process.Start 返回 null");
        return null;
    }

    Log.Information("[OK] bat 进程已启动 PID={Pid}", process.Id);

    // 返回链：短暂等待后检查进程是否存活，异常退出时读日志做诊断
    _ = Task.Run(async () =>
    {
        await Task.Delay(2000);
        try
        {
            if (process.HasExited)
            {
                Log.Warning("[BOOT] bat 进程 2 秒内退出 ExitCode={Code}", process.ExitCode);
                ServerManagerService.ReadServerCrashDetailsLegacy(server.WorkingDirectory);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[BOOT] 检查 bat 存活时异常（可能进程已释放）");
        }
    });

    return process;
}
```

- [ ] **Step 4: 编译验证**
Run: `dotnet build src/MSMC/MSMC.csproj 2>&1 | tail -20`
Expected: 成功

---

## Task 4: Supervisor TrackExistingProcessTree 互斥重启

### 找到 ProcessSupervisorService.cs

- [ ] **Step 1: 先找到文件**
Run: `find src/MSMC -name "ProcessSupervisorService.cs" -type f`

- [ ] **Step 2: 在接口 IProcessSupervisorService 里加方法签名**

```csharp
/// <summary>
/// 跟踪已存在的进程 PID 并绑定其子进程树到 Job Object。
/// 用于 bat 脚本启动场景（bat 会 spawn cmd.exe → java.exe 子进程树）。
/// </summary>
/// <param name="parentPid">根进程 PID（bat 对应的 cmd.exe 或等价 PID）</param>
/// <param name="scriptInfo">脚本信息（用于互斥重启决策）</param>
/// <returns>监管句柄；失败返回 null</returns>
SupervisedProcessHandle? TrackExistingProcessTree(int parentPid, ScriptSupervisorInfo scriptInfo);
```

- [ ] **Step 3: 在 ProcessSupervisorService 实现类里加方法**

核心逻辑：
```csharp
public SupervisedProcessHandle? TrackExistingProcessTree(int parentPid, ScriptSupervisorInfo scriptInfo)
{
    // 1. 验证父进程存在
    Process parentProcess;
    try { parentProcess = Process.GetProcessById(parentPid); }
    catch (ArgumentException)
    {
        Log.Warning("[SUP] 父进程 PID={Pid} 已不存在，跳过绑定", parentPid);
        return null;
    }

    // 2. 给 Supervisor 内部已有的方法传父进程来创建 Job Object 并绑进程树
    //    （伪代码 — 复用现有 CreateJobObjectForProcessTree 逻辑）
    var handle = CreateHandleFromProcessTree(parentProcess);
    if (handle == null) return null;

    // 3. 关键互斥：bat 有 while(true) → 禁用 Supervisor 崩溃自动重启
    if (scriptInfo.HasAutoRestart)
    {
        handle.EnableCrashRestart = false;
        Log.Information("[SUP] ⚠️ 脚本含 while(true) 重启循环，Supervisor 崩溃自动重启已互斥禁用");
        Log.Information("[SUP] Supervisor 仍提供防睡眠 / 优先级 / 内存上限 / 日志 tail 能力");
    }

    return handle;
}
```

- [ ] **Step 4: 编译验证**
Run: `dotnet build src/MSMC/MSMC.csproj 2>&1 | tail -20`
Expected: 成功

---

## Task 5: ServerDetectionViewModel 启动时自动检测 StartupConfig

### 找到 StartCurrentServer 相关代码

- [ ] **Step 1: 找到当前 StartCurrentServer 方法里 KnownServer → ServerInstance 映射处**

在 `new ServerInstance { ... }` 赋值块之后（约当前 line 1700 之后）追加：

```csharp
// ===== 新增：StartupConfig 传播 =====
if (server.Startup != null)
{
    instance.StartupScriptPath = server.Startup.ScriptPath;
    instance.StartupMode = server.Startup.Mode;
    instance.ScriptHasAutoRestart = server.Startup.HasAutoRestart;
}
else
{
    // KnownServer 没 StartupConfig → 自动检测一次，写回 KnownServer
    var detected = StartupScriptAutoDetector.AutoDetectAndPopulateStartup(server.WorkingDirectory);
    if (detected != null)
    {
        server.Startup = detected;
        _appConfigService.UpdateKnownServer(server);
        instance.StartupScriptPath = detected.ScriptPath;
        instance.StartupMode = detected.Mode;
        instance.ScriptHasAutoRestart = detected.HasAutoRestart;
        Log.Information("[SCRIPT] 首次启动时自动检测成功: {Script}, 默认 Mode={Mode}", detected.ScriptName, detected.Mode);
    }
}
// ===== end 新增 =====
```

- [ ] **Step 2: 同时在 KnownServer 初次导入时（MainWindow.xaml.cs Import handler）也加检测**

找到当前已有的 `StartBatParserService.ParseFromDirectory` 调用块（line ~1635），替换为调用我们新的 `AutoDetectAndPopulateStartup`：

```csharp
// 原来是: var parseResult = StartBatParserService.ParseFromDirectory(server.WorkingDirectory);
// 改为:
var detectedConfig = StartupScriptAutoDetector.AutoDetectAndPopulateStartup(server.WorkingDirectory);
if (detectedConfig != null)
{
    server.Startup = detectedConfig;
    _appConfigService.UpdateKnownServer(server);
}
```

- [ ] **Step 3: 编译验证**
Run: `dotnet build src/MSMC/MSMC.csproj 2>&1 | tail -20`
Expected: 成功

---

## Task 6: Bridge Action Handlers（MainWindow.xaml.cs）

### 新增 4 个 Bridge action

找到当前 Bridge action 注册处（`_bridgeService.RegisterRequestHandler` 集中区域），在已有 `server:parseStartBat` 附近追加：

- [ ] **Step 1: `server:detectStartupScript`**

```csharp
_bridgeService.RegisterRequestHandler("server:detectStartupScript", payload =>
{
    try
    {
        var knownServerId = ExtractIdFromPayload(payload);
        var known = _vm?.DetectionPage?.KnownServers
            .FirstOrDefault(s => s.Id == knownServerId);
        if (known == null)
            return Task.FromResult<object?>(new { success = false, error = "服务器不存在" });

        var config = StartupScriptAutoDetector.AutoDetectAndPopulateStartup(known.WorkingDirectory, known.Startup?.ScriptPath);
        return Task.FromResult<object?>(new { success = true, startup = SerializeStartupConfig(config) });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "detectStartupScript 异常");
        return Task.FromResult<object?>(new { success = false, error = ex.Message });
    }
});
```

- [ ] **Step 2: `server:setStartupMode`**

```csharp
_bridgeService.RegisterRequestHandler("server:setStartupMode", payload =>
{
    try
    {
        var (knownServerId, modeStr) = ExtractIdAndMode(payload);
        var known = _vm?.DetectionPage?.KnownServers
            .FirstOrDefault(s => s.Id == knownServerId);
        if (known == null)
            return Task.FromResult<object?>(new { success = false, error = "服务器不存在" });

        var mode = modeStr == "Script" ? StartupMode.Script : StartupMode.Manual;

        // 确保 KnownServer 有 StartupConfig
        known.Startup ??= StartupScriptAutoDetector.AutoDetectAndPopulateStartup(known.WorkingDirectory);
        known.Startup ??= new StartupConfig();

        known.Startup.Mode = mode;
        _appConfigService.UpdateKnownServer(known);

        Log.Information("[SCRIPT] 用户切换启动模式: Server={Server}, Mode={Mode}", known.Name, mode);
        return Task.FromResult<object?>(new { success = true });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "setStartupMode 异常");
        return Task.FromResult<object?>(new { success = false, error = ex.Message });
    }
});
```

- [ ] **Step 3: `server:setScriptPath`**

```csharp
_bridgeService.RegisterRequestHandler("server:setScriptPath", payload =>
{
    try
    {
        var (knownServerId, scriptPath) = ExtractIdAndString(payload, "scriptPath");
        var known = _vm?.DetectionPage?.KnownServers
            .FirstOrDefault(s => s.Id == knownServerId);
        if (known == null)
            return Task.FromResult<object?>(new { success = false, error = "服务器不存在" });

        if (!File.Exists(scriptPath))
            return Task.FromResult<object?>(new { success = false, error = "脚本文件不存在" });

        var config = StartupScriptAutoDetector.AutoDetectAndPopulateStartup(known.WorkingDirectory, scriptPath);
        known.Startup = config ?? new StartupConfig { ScriptPath = scriptPath, ScriptName = Path.GetFileName(scriptPath) };
        known.Startup.Mode = StartupMode.Script;
        _appConfigService.UpdateKnownServer(known);

        return Task.FromResult<object?>(new { success = true, startup = SerializeStartupConfig(known.Startup) });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "setScriptPath 异常");
        return Task.FromResult<object?>(new { success = false, error = ex.Message });
    }
});
```

- [ ] **Step 4: `server:reparseScript`**

```csharp
_bridgeService.RegisterRequestHandler("server:reparseScript", payload =>
{
    try
    {
        var knownServerId = ExtractIdFromPayload(payload);
        var known = _vm?.DetectionPage?.KnownServers
            .FirstOrDefault(s => s.Id == knownServerId);
        if (known == null)
            return Task.FromResult<object?>(new { success = false, error = "服务器不存在" });

        var config = StartupScriptAutoDetector.AutoDetectAndPopulateStartup(known.WorkingDirectory, known.Startup?.ScriptPath);
        if (config == null)
            return Task.FromResult<object?>(new { success = false, error = "解析失败或未找到脚本" });

        config.Mode = known.Startup?.Mode ?? StartupMode.Manual;
        known.Startup = config;

        var diff = StartupScriptAutoDetector.ComputeDiff(known, config);

        return Task.FromResult<object?>(new { success = true, startup = SerializeStartupConfig(config), diff });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "reparseScript 异常");
        return Task.FromResult<object?>(new { success = false, error = ex.Message });
    }
});
```

- [ ] **Step 5: 辅助方法（ExtractIdFromPayload, ExtractIdAndMode, ExtractIdAndString, SerializeStartupConfig）**

放在 Bridge 注册块附近的私有区域：

```csharp
private static string? ExtractIdFromPayload(object? payload)
{
    if (payload is System.Text.Json.JsonElement el && el.ValueKind == System.Text.Json.JsonValueKind.Object)
    {
        return el.TryGetProperty("knownServerId", out var idProp) ? idProp.GetString() : null;
    }
    return payload?.ToString();
}

private static (string? id, string mode) ExtractIdAndMode(object? payload)
{
    if (payload is System.Text.Json.JsonElement el && el.ValueKind == System.Text.Json.JsonValueKind.Object)
    {
        var id = el.TryGetProperty("knownServerId", out var idP) ? idP.GetString() : null;
        var mode = el.TryGetProperty("mode", out var modeP) ? modeP.GetString() ?? "Manual" : "Manual";
        return (id, mode);
    }
    return (payload?.ToString(), "Manual");
}

private static (string? id, string str) ExtractIdAndString(object? payload, string fieldName)
{
    if (payload is System.Text.Json.JsonElement el && el.ValueKind == System.Text.Json.JsonValueKind.Object)
    {
        var id = el.TryGetProperty("knownServerId", out var idP) ? idP.GetString() : null;
        var str = el.TryGetProperty(fieldName, out var sp) ? sp.GetString() ?? "" : "";
        return (id, str);
    }
    return (null, "");
}

private static object SerializeStartupConfig(StartupConfig? c)
{
    if (c == null) return null;
    return new
    {
        mode = c.Mode.ToString(),
        scriptPath = c.ScriptPath,
        scriptName = c.ScriptName,
        lastParseTime = c.LastParseTime?.ToString("o"),
        hasAutoRestart = c.HasAutoRestart,
        jvmArgs = c.ScriptJvmArgs,
        jarPath = c.ScriptJarPath,
        maxHeapBytes = c.ScriptMaxHeapBytes,
        initialHeapBytes = c.ScriptInitialHeapBytes,
    };
}
```

- [ ] **Step 6: 编译验证**
Run: `dotnet build src/MSMC/MSMC.csproj 2>&1 | tail -30`
Expected: 成功

---

## Task 7: 前端类型 + Bridge API

### 7a. types/bridge.ts 新增

- [ ] **Step 1: 在文件末尾追加**

```typescript
// ─── 启动脚本类型 ───

export type StartupMode = 'Manual' | 'Script'

export interface StartupConfig {
  mode: StartupMode
  scriptPath?: string
  scriptName?: string
  lastParseTime?: string
  hasAutoRestart: boolean
  jvmArgs: string[]
  jarPath?: string
  maxHeapBytes: number
  initialHeapBytes: number
}

export interface DiffReport {
  jarPathChanged: boolean
  heapMaxFrom?: string
  heapMaxTo?: string
  heapInitFrom?: string
  heapInitTo?: string
  jvmArgsAdded: string[]
  jvmArgsRemoved: string[]
}
```

### 7b. utils/bridge.ts 新增

- [ ] **Step 2: 追加 import + 4 个 API 函数**

import 块加：
```typescript
StartupMode, StartupConfig, DiffReport,
```

API 函数：
```typescript
// ─── 启动脚本 API ───

export function detectStartupScript(knownServerId: string): Promise<{
  success: boolean;
  startup?: StartupConfig;
  error?: string;
}> {
  return bridge.invoke('server:detectStartupScript', { knownServerId });
}

export function setStartupMode(knownServerId: string, mode: StartupMode): Promise<{
  success: boolean;
}> {
  return bridge.invoke('server:setStartupMode', { knownServerId, mode });
}

export function setScriptPath(knownServerId: string, scriptPath: string): Promise<{
  success: boolean;
  startup?: StartupConfig;
  error?: string;
}> {
  return bridge.invoke('server:setScriptPath', { knownServerId, scriptPath });
}

export function reparseScript(knownServerId: string): Promise<{
  success: boolean;
  startup?: StartupConfig;
  diff?: DiffReport;
  error?: string;
}> {
  return bridge.invoke('server:reparseScript', { knownServerId });
}
```

- [ ] **Step 3: TypeScript 编译验证**
Run: `cd src/frontend && npx tsc --noEmit 2>&1 | head -20`
Expected: 0 errors

---

## Task 8: 前端 UI（DashboardPage 服务器卡片）

### 在 DashboardPage 服务器卡片上加启动模式行

- [ ] **Step 1: 找到 DashboardPage.tsx 里渲染服务器列表的区域**

每个 KnownServer 卡片底部（端口 / 状态那一行下面）加一行启动模式指示器：

```tsx
{server.startup && (
  <div style={{
    marginTop: 8,
    padding: '6px 10px',
    borderRadius: 6,
    backgroundColor: 'var(--md-card-hover)',
    fontSize: 11,
    display: 'flex',
    alignItems: 'center',
    gap: 8,
  }}>
    <span style={{ color: 'var(--md-body-light)' }}>⚙️ 启动：</span>
    {server.startup.mode === 'Script' ? (
      <span style={{ color: 'var(--md-accent-text)', fontWeight: 500 }}>
        🔄 {server.startup.scriptName}
        {server.startup.hasAutoRestart && (
          <span style={{ color: 'var(--md-warning-text)', marginLeft: 6 }}>
            自动重启循环
          </span>
        )}
      </span>
    ) : (
      <span style={{ color: 'var(--md-body)' }}>
        📋 手动参数
        {server.startup.lastParseTime && (
          <span style={{ color: 'var(--md-body-light)', marginLeft: 6 }}>
            · 脚本已识别
          </span>
        )}
      </span>
    )}
    <button
      className="md-btn md-btn-outlined"
      style={{ fontSize: 10, marginLeft: 'auto', padding: '2px 8px' }}
      onClick={() => handleReparseStartup(server.id)}
    >
      重新解析
    </button>
    <button
      className="md-btn md-btn-outlined"
      style={{ fontSize: 10, padding: '2px 8px' }}
      onClick={() => handleToggleStartupMode(server.id, server.startup!.mode)}
    >
      切换
    </button>
  </div>
)}
```

### 加事件处理器

```typescript
const handleToggleStartupMode = async (serverId: string, currentMode: string) => {
  const newMode = currentMode === 'Script' ? 'Manual' : 'Script';
  await setStartupMode(serverId, newMode as StartupMode);
  // 刷新服务器列表
  refreshServers();
};

const handleReparseStartup = async (serverId: string) => {
  const result = await reparseScript(serverId);
  if (result.success) {
    // 显示 diff toast
    refreshServers();
  }
};
```

- [ ] **Step 2: TypeScript 编译验证**
Run: `cd src/frontend && npx tsc --noEmit 2>&1 | head -20`
Expected: 0 errors

---

## Task 9: 最终全量编译验证 + Commit

- [ ] **Step 1: .NET 全量构建**
Run: `cd src/MSMC && dotnet build MSMC.csproj 2>&1 | tail -30`
Expected: 0 errors

- [ ] **Step 2: 前端 TypeScript 检查**
Run: `cd src/frontend && npx tsc --noEmit 2>&1 | head -30`
Expected: 0 errors

- [ ] **Step 3: 写一条 commit**
Run: `git add -A && git commit -m "feat(server): start.bat 双通道识别与固化

- KnownServer 新增 StartupConfig 子对象 + StartupMode 枚举
- 新增 StartupScriptAutoDetector 优先级链扫描 + 解析固化
- ServerManagerService.StartServer 支持 Mode 分叉：直调 bat vs 手动 java
- Supervisor TrackExistingProcessTree 进程树绑定 + while(true) 互斥重启
- 4 个 Bridge action: detectStartupScript / setStartupMode / setScriptPath / reparseScript
- 前端 DashboardPage 服务器卡片新增启动模式指示器
- 三链原则：因果（检测→固化→分叉）、执行（兜底→回退→互斥）、返回链（结构化日志）"`

---

## 自检清单

| 需求 | 实现位置 |
|------|----------|
| 优先级链识别 | StartupScriptAutoDetector.FindScript |
| Parse + 固化 | StartupScriptAutoDetector.AutoDetectAndPopulateStartup |
| KnownServer 持久化 | KnownServer.Startup 属性 + StartupConfig 类 |
| KnownServer → ServerInstance 传播 | ServerDetectionViewModel.StartCurrentServer |
| 双通道启动分叉 | ServerManagerService.StartServer |
| bat 直调 + 安全检查 | ServerManagerService.LaunchViaBat |
| Supervisor 互斥重启 | ProcessSupervisorService.TrackExistingProcessTree |
| Bridge API | MainWindow.xaml.cs 4 个 handler |
| 前端 Bridge 函数 | utils/bridge.ts |
| 前端 UI | DashboardPage.tsx 服务器卡片 |
| 三链原则 | 每个 Task 都标注了因果链/执行链/返回链 |
