# start.bat 双通道识别与固化 — 设计文档

**日期**: 2026-08-31
**状态**: 已审批 → 待实现
**三链原则**: 因果链 ✅ | 执行链 ✅ | 返回链 ✅

---

## 1. 背景与动机

### 现状 gap

MSMC 导入 Minecraft 服务器时，会通过 `StartBatParserService.ParseFromDirectory()` 自动扫描并解析目录下的 start.bat / run.bat / *.cmd / *.bat，提取 JarPath、Heap、JvmArgs 并**回填**到 KnownServer 的手动字段。但：

1. **启动 gap**：`ServerManagerService.StartServer()` 永远自己拼 `java -Xms -Xmx ... -jar` 命令，**从不直接调用** .bat。.bat 里的 `while(true)` 重启循环、PAUSE、echo、环境变量替换全部被跳过。
2. **配置 gap**：`KnownServer`（JSON 持久化 POCO）里没有 `StartupScriptPath` 字段，也没有"用脚本启动 vs 手动"的开关。`ServerInstance.StartupScriptPath` 是运行时 ObservableProperty，不存盘。
3. **Bridge gap**：没有"检测脚本 / 设置启动模式 / 用脚本启动"的 action。
4. **UI gap**：前端完全不知道 .bat 的存在，用户看到的永远是手动组装的 JVM 参数。

### 要解决的问题

用户想用 start.bat 的原始逻辑开服（特别是里面的自动重启循环和环境变量），MSMC 不应该绕过 .bat 只取参数自己拼命令。同时也要保留手动组装 java 命令的能力（比如用户用了 .bat 但想自己改 JVM 参数）。

**核心需求：双通道并存。用户可以选择「用 bat 启动」或「用手动参数启动」，随时切换。**

---

## 2. 决策点（Brainstorming 结果）

| # | 问题 | 决策 |
|---|------|------|
| 1 | 启动策略 | 双通道并存 — 直调 bat 或手动 java 命令，用户可切换 |
| 2 | 识别范围 | 优先级链 start.bat → run.bat → start.cmd → run.cmd → 目录下其他 .bat/.cmd + 用户手动覆盖 |
| 3 | Supervisor 适配 | Job Object 绑 cmd.exe 进程树；HasAutoRestart 时 Supervisor 崩溃自动重启互斥禁用 |
| 4 | 配置模型 | KnownServer 加独立 `StartupConfig` 子对象 |

---

## 3. 数据模型

### 3.1 StartupConfig 子对象

```csharp
// KnownServer 上新增
public class KnownServer
{
    // ... 现有字段不动 ...
    
    /// <summary>启动配置（启动方式、脚本路径、自动检测）</summary>
    public StartupConfig? Startup { get; set; }
}

public class StartupConfig
{
    /// <summary>启动模式：Manual=手动组装java命令, Script=直接调.bat</summary>
    public StartupMode Mode { get; set; } = StartupMode.Manual;
    
    /// <summary>识别到/用户指定的启动脚本绝对路径</summary>
    public string? ScriptPath { get; set; }
    
    /// <summary>脚本文件名（start.bat / run.bat / 自定义）</summary>
    public string? ScriptName { get; set; }
    
    /// <summary>脚本最后一次解析时间</summary>
    public DateTime? LastParseTime { get; set; }
    
    /// <summary>脚本是否包含自动重启循环（Supervisor 据此互斥）</summary>
    public bool HasAutoRestart { get; set; }
    
    /// <summary>上次解析时提取的 JVM 参数快照（用于 diff 对比）</summary>
    public List<string> ScriptJvmArgs { get; set; } = [];
    
    /// <summary>上次解析时提取的 Jar 路径</summary>
    public string? ScriptJarPath { get; set; }
    
    /// <summary>上次解析时提取的最大堆内存（字节）</summary>
    public long ScriptMaxHeapBytes { get; set; }
    
    /// <summary>上次解析时提取的初始堆内存（字节）</summary>
    public long ScriptInitialHeapBytes { get; set; }
}

public enum StartupMode { Manual = 0, Script = 1 }
```

**反序列化兼容**：`Startup` 是可空属性。旧 KnownServer JSON 没有该字段时为 null。启动时降级为 Manual 模式 + 自动检测一次脚本并填充 StartupConfig。

### 3.2 ServerInstance 扩展

`ServerInstance` 已有 `StartupScriptPath` ObservableProperty。新增：
```csharp
/// <summary>启动模式</summary>
[ObservableProperty] private StartupMode _startupMode = StartupMode.Manual;

/// <summary>脚本是否含自动重启循环</summary>
[ObservableProperty] private bool _scriptHasAutoRestart;
```

### 3.3 KnownServer → ServerInstance 映射

在 `ServerDetectionViewModel.StartCurrentServerCommand` 的实例化处补充 StartupConfig 映射：

```csharp
var knownServer = GetActiveServer();
// ... 现有映射 ...

// 新增：StartupConfig 传播
if (knownServer.Startup != null)
{
    instance.StartupScriptPath = knownServer.Startup.ScriptPath;
    instance.StartupMode = knownServer.Startup.Mode;
    instance.ScriptHasAutoRestart = knownServer.Startup.HasAutoRestart;
}
else
{
    // KnownServer 没 StartupConfig → 自动检测一次
    var detected = AutoDetectAndPopulateStartup(knownServer);
    if (detected != null)
    {
        knownServer.Startup = detected;
        _appConfigService.UpdateKnownServer(knownServer);
        instance.StartupScriptPath = detected.ScriptPath;
        instance.StartupMode = detected.Mode;
        instance.ScriptHasAutoRestart = detected.HasAutoRestart;
    }
}
```

---

## 4. 识别与自动固化流程

### 4.1 优先级链扫描

新服务类 `StartupScriptAutoDetector`（静态工具类 + 可注入服务）：

```csharp
public class StartupScriptAutoDetector
{
    private static readonly string[] StandardPatterns = 
        ["start.bat", "run.bat", "start.cmd", "run.cmd"];
    
    public static string? FindScript(string workingDirectory, string? userOverride = null)
    {
        // 1. 用户手动覆盖优先
        if (!string.IsNullOrWhiteSpace(userOverride) && File.Exists(userOverride))
            return userOverride;
        
        // 2. 标准文件名按优先级
        foreach (var pattern in StandardPatterns)
        {
            var candidate = Path.Combine(workingDirectory, pattern);
            if (File.Exists(candidate))
                return candidate;
        }
        
        // 3. 兜底：目录下第一个 .bat / .cmd
        return Directory.GetFiles(workingDirectory, "*.bat", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(workingDirectory, "*.cmd", SearchOption.TopDirectoryOnly))
            .FirstOrDefault();
    }
}
```

### 4.2 Parse → 固化

复用现有 `StartBatParserService.ParseFile()` + `StartupScriptDetector.Analyze()`：

```csharp
public static StartupConfig? AutoDetectAndPopulateStartup(string workingDirectory, string? existingScriptPath = null)
{
    var scriptPath = FindScript(workingDirectory, existingScriptPath);
    if (scriptPath == null) return null;
    
    // 三链原则：因果链 — 因（文件存在）→ 果（解析 + 固化）
    
    // 执行链：两个解析器组合使用
    var parserResult = StartBatParserService.ParseFile(scriptPath, workingDirectory);
    var content = File.ReadAllText(scriptPath);
    var heuristic = StartupScriptDetector.Analyze(content);
    
    if (!parserResult.Success && !heuristic.IsServerStartupScript)
    {
        // 返回空，日志 warning。执行链兜底：解析失败不阻塞启动
        Log.Warning("[SCRIPT] 找到脚本 {Path} 但解析失败", scriptPath);
        return null;
    }
    
    var config = new StartupConfig
    {
        Mode = StartupMode.Manual,  // 默认 Manual，用户可切换
        ScriptPath = scriptPath,
        ScriptName = Path.GetFileName(scriptPath),
        LastParseTime = DateTime.Now,
        HasAutoRestart = heuristic.HasAutoRestart,
        ScriptJvmArgs = parserResult.JvmArguments,
        ScriptJarPath = parserResult.JarPath,
        ScriptMaxHeapBytes = parserResult.MaxHeapBytes ?? 0,
        ScriptInitialHeapBytes = parserResult.InitialHeapBytes ?? 0,
    };
    
    // 返回链：详细日志
    Log.Information("[SCRIPT] 脚本检测成功: {Path}, HasAutoRestart={AR}, HeapMax={Max}, Jar={Jar}",
        scriptPath, config.HasAutoRestart, config.ScriptMaxHeapBytes, config.ScriptJarPath);
    
    return config;
}
```

### 4.3 Parse 触发时机

| 时机 | 触发 | 行为 |
|------|------|------|
| 导入服务器 | `ImportService.Import()` | 扫描+解析，回填 KnownServer.Startup |
| KnownServer.Startup == null 时首次启动 | `StartCurrentServer` | 自动检测一次，写回 KnownServer |
| 用户点「重新解析」 | Bridge action `server:reparseScript` | 重新解析，刷新快照，返回 diff |
| 目录变动（可选） | 文件 watcher + debounce | 自动 re-parse（可后续迭代） |

**关键原则**：parse 结果**只写 StartupConfig 快照**，不自动覆盖 KnownServer 的 `JvmArguments`、`MaxHeapMemoryBytes` 等手动字段。Manual 模式下 KnownServer 自身字段是权威值，脚本解析结果只做参考。

---

## 5. 双通道启动实现

### 5.1 分叉点

在 `ServerManagerService.StartServer(ServerInstance instance)` 和 `StartServerSupervisedAsync` 内部加一条新路径。

```csharp
// StartServer 内部新增

if (instance.StartupMode == StartupMode.Script && !string.IsNullOrEmpty(instance.StartupScriptPath))
{
    // 用户选了 bat 模式 → 直调脚本
    var process = LaunchViaBat(instance);
    // Supervisor 绑定
    if (_supervisor != null && process != null)
    {
        var handle = _supervisor.TrackExistingProcessTree(
            parentPid: process.Id,
            scriptInfo: new ScriptSupervisorInfo
            {
                ScriptPath = instance.StartupScriptPath,
                HasAutoRestart = instance.ScriptHasAutoRestart,
            });
        // ...
    }
    return process;
}

// 否则走现有 Java 路径（不变）
```

### 5.2 LaunchViaBat 实现

```csharp
private Process? LaunchViaBat(ServerInstance instance)
{
    var scriptPath = instance.StartupScriptPath!;
    var batDir = Path.GetDirectoryName(scriptPath) ?? instance.WorkingDirectory;
    
    Log.Information("[BOOT] 走脚本启动路径: {Script}, WorkingDir={Dir}", scriptPath, batDir);
    
    var startInfo = new ProcessStartInfo
    {
        FileName = scriptPath,
        WorkingDirectory = batDir,
        UseShellExecute = false,  // false 让我们能拿 cmd.exe 的 PID 绑定进程树
        CreateNoWindow = false,
    };
    
    var process = Process.Start(startInfo);
    if (process == null)
    {
        Log.Error("[ERR] 启动 bat 进程返回 null");
        return null;
    }
    
    Log.Information("[OK] bat 进程已启动 PID={Pid}", process.Id);
    
    // 短暂等待让 java.exe 子进程 spawn
    _ = Task.Run(async () =>
    {
        await Task.Delay(2000);
        // 检查 java.exe 是否真的跑起来了（防 bat 立即退出）
        if (process.HasExited && process.ExitCode != 0)
        {
            Log.Error("[ERR] bat 进程异常退出 ExitCode={Code}", process.ExitCode);
            // 返回链：读 logs/latest.log 和 crash-reports 做诊断
            ReadServerCrashDetailsLegacy(instance.WorkingDirectory);
        }
    });
    
    return process;
}
```

**关于 UseShellExecute**：设为 `false` 是为了让我们能拿到 `Process.Id` 并传给 Supervisor 绑定进程树。.bat 在 UseShellExecute=false 下 .NET 会自动通过 cmd.exe 执行（FileExtensions 注册）。

### 5.3 Supervisor 互斥重启适配

Supervisor 新增 TrackExistingProcessTree 方法 + 互斥逻辑：

```csharp
// ProcessSupervisorService 新增

public SupervisedProcessHandle? TrackExistingProcessTree(
    int parentPid, ScriptSupervisorInfo scriptInfo)
{
    // 1. Job Object 绑住 parentPid 下的整个进程树
    var handle = CreateJobObjectForProcessTree(parentPid);
    
    // 2. 关键互斥：bat 有 while(true) → Supervisor 禁用崩溃自动重启
    if (scriptInfo.HasAutoRestart)
    {
        handle.EnableCrashRestart = false;
        Log.Information("[SUP] 脚本含自动重启循环，Supervisor 崩溃自动重启已互斥禁用");
    }
    
    // 3. 仍然提供：防睡眠、优先级、内存上限、日志 tail
    return handle;
}
```

**互斥原则**：bat 的 `while(true)` 重启和 Supervisor 的崩溃重启如果同时开，会导致「一个进程被 Supervisor 杀掉重启，同时 bat 也又拉起一个」，最终僵尸进程堆积。**HasAutoRestart=true 时 Supervisor 只做守护和观察，不做重启。**

---

## 6. Bridge API 设计

### 6.1 新增 Actions

| Action | Payload | 返回 |
|--------|---------|------|
| `server:detectStartupScript` | `{ knownServerId: string }` | `{ success, startup: StartupConfig?, errorMessage? }` |
| `server:setStartupMode` | `{ knownServerId: string, mode: "Manual" \| "Script" }` | `{ success }` |
| `server:setScriptPath` | `{ knownServerId: string, scriptPath: string }` | `{ success, startup: StartupConfig?, errorMessage? }` |
| `server:reparseScript` | `{ knownServerId: string }` | `{ success, diff: DiffReport? }` |

### 6.2 修改已有 Action

| Action | 变更 |
|--------|------|
| `server:startServer` | 内部根据 KnownServer.Startup?.Mode 自动分叉。payload 不变。 |

### 6.3 DiffReport 结构

```typescript
interface DiffReport {
  // KnownServer 的手动配置 vs StartupConfig 的脚本快照
  jarPathChanged: boolean;
  heapMaxChanged: { from: string; to: string } | null;
  heapInitChanged: { from: string; to: string } | null;
  jvmArgsAdded: string[];      // 手动有，脚本没有 → 用户加的
  jvmArgsRemoved: string[];    // 脚本有，手动没有 → 用户删的
  unknownArgs: string[];       // 脚本里解析不到的参数
}
```

---

## 7. 前端 UI 设计

### 7.1 服务器卡片新增一行

在 DashboardPage 服务器列表的每张卡片底部加一行：

```
⚙️ 启动：[手动参数] 📋     ⚙️ 启动：[start.bat] 🔄
   解析时间 3 天前            脚本有自动重启循环
   [切换模式] [重新解析]       [切换模式] [重新解析]
```

Mode=Script 时显示 🔄 图标 + 是否有自动重启循环。
Mode=Manual 但检测到 .bat 时显示 📋 + 参数有/无变动提示。

### 7.2 服务器详情 Tab 新增「启动脚本」

| 区域 | 内容 |
|------|------|
| 脚本信息 | ScriptPath, ScriptName, LastParseTime |
| 模式切换 | Radio：手动参数 / 启动脚本 |
| 手动覆盖 | 文件选择器（用户指定自定义脚本路径） |
| 原始脚本预览 | 只读文本框（灰色） |
| 解析快照 | JarPath, HeapMax, HeapInit, JvmArgs 列表 |
| Diff 对比 | 手动配置 vs 脚本快照的差异表 |
| 操作按钮 | 重新解析 / 应用脚本参数到手动 / 清除脚本关联 |

### 7.3 Bridge API 前端调用

```typescript
// bridge.ts 新增
export function detectStartupScript(knownServerId: string): Promise<{
  success: boolean;
  startup?: StartupConfig;
  errorMessage?: string;
}> {
  return bridge.invoke('server:detectStartupScript', { knownServerId });
}

export function setStartupMode(knownServerId: string, mode: 'Manual' | 'Script'): Promise<{
  success: boolean;
}> {
  return bridge.invoke('server:setStartupMode', { knownServerId, mode });
}

// ... setScriptPath, reparseScript
```

---

## 8. 文件级任务分解

### 8.1 后端

| 文件 | 变更 |
|------|------|
| `KnownServer.cs` | 新增 `StartupConfig? Startup` 属性 |
| `ServerInstance.cs` | 新增 `StartupMode` + `ScriptHasAutoRestart` ObservableProperty |
| **新增** `StartupScriptAutoDetector.cs` | 优先级链扫描 + Parse + 固化到 StartupConfig（静态工具类） |
| `ServerManagerService.cs` | `StartServer` 内部加 Mode 分叉 + 新增 `LaunchViaBat()` 方法 |
| **新增** `ScriptSupervisorInfo.cs` | Job Object 绑进程树所需的脚本信息 DTO |
| `ProcessSupervisorService.cs`（或接口） | 新增 `TrackExistingProcessTree(int parentPid, ScriptSupervisorInfo info)` |
| `ServerDetectionViewModel.cs` | `StartCurrentServer` 中 StartupConfig 传播 + 自动检测 |
| `MainWindow.xaml.cs`（Bridge 注册处） | 新增 4 个 Bridge action handler |

### 8.2 前端

| 文件 | 变更 |
|------|------|
| `types/bridge.ts` | 新增 `StartupMode`, `StartupConfig`, `DiffReport` 类型 |
| `utils/bridge.ts` | 新增 4 个 bridge API 函数 |
| `pages/DashboardPage.tsx`（或 ServerManagementPage） | 服务器卡片加 Mode 行 + 详情 Tab 加「启动脚本」面板 |

---

## 9. 三链原则覆盖

| 原则 | 体现 |
|------|------|
| **因果链** | 扫描的因（KnownServer.Startup==null）→ 检测 → 固化；Mode=Script 的因 → 走 bat 启动；HasAutoRestart 的因 → Supervisor 互斥禁用 |
| **执行链** | 每个关键节点有兜底：bat 启动后 2 秒内检查 java.exe 是否真跑起来；Job Object 绑树防止 supervisor 漏掉子进程；Mode=Script 但脚本不存在时 fallback 到 Manual 并弹窗提示 |
| **返回链** | 每个步骤有结构化日志：`[SCRIPT] 检测到 start.bat` / `[BOOT] 走脚本启动路径` / `[SUP] 脚本含自动重启循环，互斥禁用` / bat 启动失败时读 logs/latest.log + crash-reports 做诊断 |

### 关键兜底场景

| 场景 | 处理 |
|------|------|
| 用户切到 Script 但脚本后来被删了 | 启动失败 → fallback Manual + 弹窗 + 日志 Warning |
| bat 里没有 java 命令 | StartupScriptDetector 规则匹配 < 2 → 解析结果丢弃 → StartupConfig 不填 Mode=Script |
| bat 里有 while(true) 重启循环 + Supervisor 也开了重启 | 互斥禁用 Supervisor 崩溃重启，仅 Job Object 守护进程树 |
| bat 启动后 2 秒就退出 | 读 logs/latest.log + crash-reports 尾部 → 返回链诊断 |

---

## 10. 不在本次范围

- 跨平台 .sh 脚本（Linux/macOS）— 后续迭代，当前只做 .bat/.cmd（Windows）
- 文件变动自动 watcher（debounce 自动 re-parse）— MVP 手动「重新解析」按钮够用
- StartBatParserService 的解析精度增强（当前已知够覆盖 90% 场景）
- bat 启动时自定义工作目录（MVP 用 bat 所在目录）
