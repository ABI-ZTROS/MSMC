# McServerGuard 启动阶段 CPU 竞争排查报告

> 排查日期: 2026-07-28
> 排查范围: 启动阶段并行运行且消耗 CPU 的后台任务
> 目标: 识别启动阶段不必要的 CPU 竞争、UI 线程阻塞、fire-and-forget 异常吞没等问题

---

## 一、启动时序全景（按代码执行顺序）

以下为应用启动后各后台任务的启动时序：

| 序号 | 时间点（相对启动） | 任务 | 触发位置 | 线程 | CPU 开销 |
|------|-------------------|------|----------|------|---------|
| 1 | T+0 | DI 容器构建 + SystemMonitor 构造（含 CPU 计数器预热） | App.xaml.cs L193 / SystemMonitor.cs L77-91 | UI | 中 |
| 2 | T+0 | ServerDetector 构造（含缓存清理 Timer 启动） | ServerDetector.cs L99 | UI | 低 |
| 3 | T+0 | SettingsViewModel 构造 → `_ = LoadJavaInstallationsAsync()` | SettingsViewModel.cs L258 | UI→TP | **高** |
| 4 | T+0 | ServerDetectionViewModel 构造 → `StartAutoDetect()` | ServerDetectionViewModel.cs L92 | UI→TP | **高** |
| 5 | T+0 | NetworkMonitorViewModel 构造 → DispatcherTimer(1s) 启动 | NetworkMonitorViewModel.cs L352-357 | UI | 中 |
| 6 | T+0 | MemoryOptimizerService 构造 → `GC.RegisterForFullGCNotification` + fire-and-forget `MonitorFullGCNotification` | MemoryOptimizerService.cs L91-92 | TP | 低 |
| 7 | T+0 | MainViewModel 构造 → DispatcherTimer(1s) + fire-and-forget 自动检测 | MainViewModel.cs L112-124 | UI | 中 |
| 8 | T+500ms | SystemMonitorViewModel 构造中延迟启动监控 | SystemMonitorViewModel.cs L140-148 | TP | **高** |
| 9 | T+500ms | MainViewModel fire-and-forget → `DetectServersAsync()` | MainViewModel.cs L117-118 | UI→TP | **高** |
| 10 | T+>0 | MemoryOptimizerService.Start() | App.xaml.cs L266 | UI | 低 |

> **核心问题**: T+0 到 T+500ms 区间内，至少有 4 个重度 CPU 消耗任务同时启动（Java 查找、自动检测循环、系统监控、首次服务器检测），且 Java 查找和自动检测之间缺乏协调。

---

## 二、逐文件排查结果

### 2.1 ProcessScanner.cs

**文件**: `/workspace/src/McServerGuard/Services/ServerDetection/ProcessScanner.cs`

**结论**: WMI 批量查询实现合理，无 P0/P1 级别问题。

**详细分析**:

- **L48-51**: `ScanServerProcessesAsync()` 使用 `Task.Run(() => ScanServerProcesses()).ConfigureAwait(false)` 正确地将同步 WMI 调用封送到线程池，不阻塞调用线程。
- **L223-259**: `LoadAllProcessInfoBatch()` 采用一次性 WMI 查询获取所有进程信息（PID/父PID/命令行/名称），将 WMI 调用次数从 O(N) 降为 O(1)。注释中也明确说明了这一优化。
- **L194-213**: `CollectJavaProcessIds()` 通过 `Process.GetProcessesByName` 获取 Java 进程 PID 列表，使用 `using` 及时释放 Process 对象避免句柄泄漏。
- **L316-343**: `IsProcessLaunchedByShellInMemory()` 父进程链追溯完全基于内存字典遍历，无额外 WMI 调用。

**可优化点**（P2 级别）:

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P2-1 | L197-198 | `Process.GetProcessesByName` 调用两次（"java" + "javaw"） | P2 | 两次调用会分别产生两次进程快照枚举。可改用单次 `Process.GetProcesses()` + LINQ 筛选，减少一半的系统调用开销。在高频（5 秒）自动检测循环中此开销会累积。 |

---

### 2.2 ServerDetectionViewModel.cs

**文件**: `/workspace/src/McServerGuard/ViewModels/ServerDetectionViewModel.cs`

**发现 1 个 P1 问题 + 1 个 P2 问题**:

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P1-1 | L92 | **自动检测在 ViewModel 构造阶段立即启动，与前端加载并行** | **P1** | `StartAutoDetect()` 在构造函数末尾直接调用，而该 ViewModel 通过 DI 在 App.xaml.cs L182 注册为 Singleton。实际构造发生在 `_serviceProvider.BuildServiceProvider()` (App.xaml.cs L193) 时，即 DI 容器构建阶段——此时主窗口尚未 Show()。自动检测循环立即开始后台线程执行 WMI 扫描（5 秒间隔），与前端 UI 初始化、MainWindow.Show() 并行，造成 CPU 竞争。 |
| P2-2 | L178 | `StartAutoDetect()` 中检查 `_serverDetector.IsAutoDetectRunning` 前已调用 `_serverDetector.StartAutoDetect()` | P2 | 无实际功能影响，但如果第一次调用时检测器已经在运行，第二次调用会直接标记 `IsAutoDetectEnabled = true` 返回，不会重复启动。逻辑正确但略显冗余。 |

**P1-1 详细分析**:

构造函数调用链:
```
App.OnStartup() → BuildServiceProvider() → new ServerDetectionViewModel() → StartAutoDetect()
                                                                    → _serverDetector.StartAutoDetect()
                                                                        → Task.Run(无限循环: DetectAllAsync + Delay 5s)
```

在 `BuildServiceProvider()` 期间，所有 Singleton 服务按依赖顺序依次构造。`ServerDetectionViewModel` 依赖 `IServerDetector`，所以 `ServerDetector` 先构造（其构造函数启动 30 秒缓存清理 Timer），然后 `ServerDetectionViewModel` 构造并立即启动自动检测循环。

**建议**: 将 `StartAutoDetect()` 延迟到主窗口 Loaded 事件后执行，或改为由 MainViewModel 统一协调启动时机。

---

### 2.3 SystemMonitor.cs

**文件**: `/workspace/src/McServerGuard/Services/SystemMonitoring/SystemMonitor.cs`

**发现 1 个 P1 问题 + 1 个 P2 问题**:

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P1-2 | L221 | **首次采集为同步调用，在 UI 线程执行** | **P1** | `StartMonitoring()` 在 L221 直接调用 `callback(CollectSnapshot())`。`CollectSnapshot()` 内部调用 `GetCpuUsage()`（PerformanceCounter 或 WMI）、`_memoryMonitor.GetUsedMemory()`、`_diskMonitor.GetDiskInfo()`、`GetJavaProcessStats()`（枚举所有 Java 进程），这些都是耗时的系统调用。虽然 `StartMonitoring` 本身是在 `SystemMonitorViewModel` 的 `Dispatcher.InvokeAsync` 中调用（L143），所以 callback 也在 UI 线程执行——首次采集会阻塞 UI 线程。 |
| P2-3 | L230-271 | **Timer 回调中使用 `CollectSnapshotAsync().ContinueWith(TaskScheduler.Default)`** | P2 | 周期性采集正确使用了异步方式，但 `ContinueWith` 使用 `TaskScheduler.Default`，导致后续的 `callback(t.Result)` 在线程池线程执行。如果 callback 需要更新 UI 绑定属性（确实如此，见 SystemMonitorViewModel.cs L223-226 的 Dispatcher.InvokeAsync），则 callback 内部会通过 Dispatcher 回到 UI 线程。这个设计本身可行，但引入了不必要的线程切换。可改为 `await` + `Dispatcher.InvokeAsync`，更清晰。 |

**P1-2 详细分析**:

`SystemMonitorViewModel.StartMonitoring()` (L140-148):
```csharp
_ = Task.Run(async () => {
    await Task.Delay(500);
    _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => {
        try { StartMonitoring(); }
        ...
    });
});
```

在 `StartMonitoring()` 中:
```csharp
public void StartMonitoring(TimeSpan interval, Action<SystemMetrics> callback, CancellationToken cancellationToken) {
    // ...
    // 首次采集（立即执行）—— 在 UI 线程！
    callback(CollectSnapshot());  // CollectSnapshot 是同步的，包含 WMI/PerformanceCounter 调用
    // ...
}
```

`callback` 是 `SystemMonitorViewModel.StartMonitoring()` L221-226 传入的 lambda，内部使用 `Dispatcher.InvokeAsync`，但因为已经在 UI 线程上，所以 `InvokeAsync` 会直接同步执行（`InvokeAsync` 在已处于 Dispatcher 线程时的行为取决于优先级，但 `Dispatcher.InvokeAsync` 通常会排队到 Dispatcher 队列）。

**关键**: `CollectSnapshot()` 本身在 UI 线程上同步执行 WMI/PerformanceCounter 调用，这是 CPU 密集型操作。

**建议**: 将首次采集改为 `callback(await CollectSnapshotAsync())`，或在 `SystemMonitorViewModel.StartMonitoring()` 中先异步采集再 callback。

---

### 2.4 JavaFinder.cs

**文件**: `/workspace/src/McServerGuard/Services/ServerDetection/JavaFinder.cs`

**发现 1 个 P1 问题 + 1 个 P2 问题**:

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P1-3 | L64-128 / L321-366 | **Java 查找为同步且包含进程启动（`java -version`），在后台线程但与自动检测并行竞争** | **P1** | `FindAllJavaInstallations()` 依次执行: 注册表查询(L164-215) + PATH 扫描 + `where.exe` 进程启动(L221-256) + 常见目录扫描 + **对每个候选路径启动 `java -version` 进程**（L321-366 `VerifyJava`）。如果系统中有 N 个候选 Java 安装，将串行启动 N 个 `java -version` 进程，每个等待最多 5 秒（L340 `WaitForExit(5000)`）。日志显示总耗时 1.3 秒（11.775→13.184），说明有 2-3 个候选路径。 |
| P2-4 | L321-366 | **`VerifyJava` 串行执行 `java -version` 进程** | P2 | 多个候选 Java 路径的验证是串行执行的（foreach 循环中逐个调用 `VerifyJava`），可改为并行验证以减少总等待时间。 |

**P1-3 详细分析 — 调用链与启动阶段并行竞争**:

```
SettingsViewModel 构造函数 (L258):
    _ = LoadJavaInstallationsAsync();   // fire-and-forget
        → await Task.Run(() => _javaFinderService.FindAll())
            → FindAllJavaInstallations()
                → VerifyJava(path1) → Process.Start("java", "-version") → WaitForExit(5000)
                → VerifyJava(path2) → Process.Start("java", "-version") → WaitForExit(5000)
                → ...
```

**线程安全性**: `LoadJavaInstallationsAsync` 使用了 `await Task.Run()`，所以 `FindAll` 中的进程启动和 `where.exe` 调用在线程池执行，**不在 UI 线程**。但它在启动阶段与以下任务并行竞争 CPU:

1. `ServerDetectionViewModel.StartAutoDetect()` → 自动检测循环 → WMI 扫描
2. `MainViewModel` → 500ms 延迟后 `DetectServersAsync()` → 又一轮 WMI 扫描
3. `SystemMonitorViewModel` → 500ms 延迟后启动监控 → PerformanceCounter/WMI 采样

**与用户日志吻合**: 用户日志中 11.775→13.184 的 1.3 秒正是 `FindAll` → `VerifyJava` × N 个候选路径的串行进程启动时间。

**建议**:
1. 将 Java 查找延迟到用户切换到设置页时执行（懒加载）
2. 或至少与自动检测错开启动时间（如延迟 2-3 秒）
3. 并行化 `VerifyJava` 调用

---

### 2.5 MainViewModel.cs

**文件**: `/workspace/src/McServerGuard/ViewModels/MainViewModel.cs`

**发现 1 个 P1 问题 + 1 个 P2 问题**:

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P1-4 | L112-124 | **fire-and-forget `BeginInvoke` 启动自动检测，与 ServerDetectionViewModel 的自动检测循环并行** | **P1** | MainViewModel 构造函数中通过 `Dispatcher.BeginInvoke(new Action(async () => { await Task.Delay(500); await DetectServersAsync(); }))` 触发首次检测。此时 `ServerDetectionViewModel` 的自动检测循环已在构造阶段启动（P1-1）。两次检测完全独立执行，会导致同一时刻有两个 `DetectAllAsync()` 在线程池中并行运行，产生双倍 WMI/进程枚举开销。 |
| P2-5 | L112 | **fire-and-forget 异常处理不完善** | P2 | `Dispatcher.BeginInvoke` 返回的 `DispatcherOperation` 未被持有或跟踪。虽然内部有 try-catch，但如果 `Dispatcher` 在 500ms 延迟期间关闭（极端情况），异常会被静默吞没。 |

**P1-4 详细分析 — 双重检测并行竞争**:

```
时间线:
T+0ms:   ServerDetectionViewModel 构造 → StartAutoDetect()
         → Task.Run → DetectAllAsync() #1 启动

T+500ms: MainViewModel fire-and-forget → DetectServersAsync()
         → DetectionPage.DetectCommand.ExecuteAsync()
         → _serverDetector.DetectAllAsync() #2 启动

T+5000ms: 自动检测循环 → DetectAllAsync() #3 启动（如果 #1 已完成）
```

**问题**: T+500ms 到 T+5000ms 之间，自动检测循环可能还在执行第一轮（如果 WMI 较慢），此时 MainViewModel 触发的第二轮检测会并行执行，产生:
- 双倍 WMI 查询（`SELECT * FROM Win32_Process`）
- 双倍 `Process.GetProcessesByName("java")` 调用
- 双倍端口扫描（25565-25590 区间）
- 双倍配置文件扫描

**建议**: MainViewModel 的首次检测应复用自动检测循环的结果（通过 `DetectionCompleted` 事件），而非独立触发第二轮检测。或至少在自动检测正在运行时跳过手动触发。

---

### 2.6 App.xaml.cs

**文件**: `/workspace/src/McServerGuard/App.xaml.cs`

**发现 1 个 P2 问题**:

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P2-6 | L266 | **MemoryOptimizerService.Start() 在主窗口 Show() 后立即调用，但两个 DispatcherTimer 立即开始 Tick** | P2 | `MemoryOptimizerService.Start()` 启动两个 DispatcherTimer: 5 分钟优化 + 5 秒内存监控。虽然初始 Tick 不会立即触发（按 Interval 延迟），但 `OnMemoryMonitorTimerTick` 每 5 秒检查 `GC.GetTotalMemory(false)` 调用一次。此开销很小（微秒级），P2 级别。 |

**无 fire-and-forget `_ = Task.Run(...)` 模式**: App.xaml.cs 中没有使用 `Task.Run` 的 fire-and-forget 启动模式。所有异步启动任务均由各 ViewModel/Service 的构造函数触发。

---

### 2.7 额外发现: SystemMonitorViewModel.cs

**文件**: `/workspace/src/McServerGuard/ViewModels/SystemMonitorViewModel.cs`

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P1-5 | L140-148 | **fire-and-forget `Task.Run` 启动监控，嵌套 Dispatcher.InvokeAsync** | **P1** | 构造函数中使用 `_ = Task.Run(async () => { await Task.Delay(500); _ = Dispatcher.InvokeAsync(() => StartMonitoring()); })` 模式。这是一个三层嵌套的异步启动: 线程池 → 延迟 → UI 线程 → StartMonitoring。问题在于 `Task.Run` 本身是 fire-and-forget（丢弃了返回的 Task），且内部异常处理仅在 `InvokeAsync` 回调中有 try-catch，如果 `Task.Run` 本身失败（如 `Application.Current` 为 null），异常会被静默吞没，因为没有注册 `TaskScheduler.UnobservedTaskException` 的覆盖。虽然 App.xaml.cs L344 注册了全局 `UnobservedTaskException` 处理，但这只在 Task 被 GC 回收时触发，时机不确定。 |

---

### 2.8 额外发现: NetworkMonitorViewModel.cs

**文件**: `/workspace/src/McServerGuard/ViewModels/NetworkMonitorViewModel.cs`

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P2-7 | L352-357 | **DispatcherTimer(1s) 在构造阶段立即启动** | P2 | 网络监控的 1 秒刷新定时器在构造函数中启动。首秒 Tick 会触发 `RefreshPorts()`（含 `netsh` 命令）和 `RefreshTraffic()`。虽然端口刷新在 `Task.Run` 中执行，但 `netsh` 命令在启动阶段与 WMI 扫描并行运行，增加 I/O 竞争。`LoadHourlyData()` 也在构造函数中同步调用（L347），读取流量数据文件。 |

---

### 2.9 额外发现: MemoryOptimizerService.cs

**文件**: `/workspace/src/McServerGuard/Services/MemoryOptimizerService.cs`

| # | 行号 | 问题 | 严重程度 | 说明 |
|---|------|------|----------|------|
| P2-8 | L91-92 | **fire-and-forget `Task.Run(MonitorFullGCNotification)` 在构造函数中启动** | P2 | `_ = Task.Run(MonitorFullGCNotification)` 启动一个无限循环后台任务。该任务使用 `GC.WaitForFullGCApproach(5000)` 阻塞等待，CPU 开销极低。但 fire-and-forget 模式意味着任务引用未被跟踪。虽然 `MonitorFullGCNotification` 内部有 try-catch，且 `GC.CancelFullGCNotification()` 在 Dispose/Exit 时调用可确保退出，但 fire-and-forget 本身是不良实践。 |
| P2-9 | L270-295 | **`MonitorFullGCNotification` 使用 `async void`** | P2 | `private async void MonitorFullGCNotification()` 是 async void 方法。异常无法被调用方捕获，只能依赖 `TaskScheduler.UnobservedTaskException`（不适用于 async void）或顶层 `AppDomain.UnhandledException`。虽然内部有 try-catch，但 async void 模式本身是代码异味。 |

---

## 三、启动阶段 CPU 竞争时序图

```
T+0ms
├── [UI线程] DI 容器构建
│   ├── SystemMonitor 构造 → CPU 计数器预热 (PerformanceCounter.NextValue × N)
│   ├── ServerDetector 构造 → 缓存清理 Timer 启动
│   ├── SettingsViewModel 构造 → _ = LoadJavaInstallationsAsync() [fire-and-forget → TP]
│   │   └── [TP] Java 查找: 注册表 + PATH + where.exe + 目录扫描 + java -version ×N ≈ 1.3s
│   ├── ServerDetectionViewModel 构造 → StartAutoDetect() [立即启动]
│   │   └── [TP] 自动检测循环 → DetectAllAsync() #1 (WMI + 进程枚举 + 端口扫描)
│   ├── SystemMonitorViewModel 构造 → _ = Task.Run(async => { await 500ms; StartMonitoring() })
│   ├── NetworkMonitorViewModel 构造 → DispatcherTimer(1s) 启动
│   ├── MainViewModel 构造 → _ = BeginInvoke(async => { await 500ms; DetectServersAsync() })
│   │   └── [UI → TP] 500ms 后 → DetectServersAsync() → DetectAllAsync() #2
│   └── [UI线程] MainWindow.Show()
│
│   ★ T+0~1300ms: TP 线程上 Java 查找 + 自动检测 #1 并行执行
│   ★ T+0~500ms: UI 线程上 CPU 计数器预热 + DI 构建完成
│
T+500ms
├── [TP] SystemMonitorViewModel → StartMonitoring() → 首次同步采集 (UI 线程!)
│   └── CollectSnapshot() → PerformanceCounter + WMI + 进程枚举
├── [TP] MainViewModel → DetectServersAsync() #2 → WMI + 进程枚举 + 端口扫描
│
│   ★ T+500ms~1500ms: 最多 3 个 CPU 密集任务并行
│       1. Java 查找 (可能仍在进行)
│       2. 自动检测 #1 (可能仍在进行)
│       3. 首次系统监控采集 + 首次手动检测 #2
│
T+1000ms
├── [UI] NetworkMonitor 首次端口刷新 (netsh 命令在 TP 执行)
│
T+5000ms
├── [TP] 自动检测循环第二轮 → DetectAllAsync() #3
├── [UI] SystemMonitor 周期采集 → CollectSnapshotAsync()
```

---

## 四、问题汇总表

| 编号 | 文件 | 行号 | 问题描述 | 严重程度 |
|------|------|------|----------|----------|
| P1-1 | `ViewModels/ServerDetectionViewModel.cs` | L92 | 自动检测在 DI 构造阶段立即启动，与前端加载并行 | **P1** |
| P1-2 | `Services/SystemMonitoring/SystemMonitor.cs` | L221 | 首次采集为同步调用 `CollectSnapshot()`，在 UI 线程执行 WMI/PerformanceCounter | **P1** |
| P1-3 | `Services/ServerDetection/JavaFinder.cs` | L64-128, L321-366 | Java 查找串行启动 N 个 `java -version` 进程（各 5s 超时），在启动阶段与自动检测并行竞争 CPU | **P1** |
| P1-4 | `ViewModels/MainViewModel.cs` | L112-124 | fire-and-forget 首次检测与自动检测循环并行，产生双倍 WMI/进程枚举/端口扫描开销 | **P1** |
| P1-5 | `ViewModels/SystemMonitorViewModel.cs` | L140-148 | fire-and-forget `Task.Run` 嵌套 `Dispatcher.InvokeAsync`，异常路径不完整 | **P1** |
| P2-1 | `Services/ServerDetection/ProcessScanner.cs` | L197-198 | `Process.GetProcessesByName` 调用两次，可合并为单次枚举 | P2 |
| P2-2 | `ViewModels/ServerDetectionViewModel.cs` | L178 | `StartAutoDetect` 中判断逻辑略显冗余 | P2 |
| P2-3 | `Services/SystemMonitoring/SystemMonitor.cs` | L230-271 | `ContinueWith(TaskScheduler.Default)` 引入不必要的线程切换 | P2 |
| P2-4 | `Services/ServerDetection/JavaFinder.cs` | L321-366 | `VerifyJava` 串行执行 `java -version`，可并行化 | P2 |
| P2-5 | `ViewModels/MainViewModel.cs` | L112 | `Dispatcher.BeginInvoke` 返回值未跟踪 | P2 |
| P2-6 | `App.xaml.cs` | L266 | MemoryOptimizerService.Start() 在 Show() 后立即调用 | P2 |
| P2-7 | `ViewModels/NetworkMonitorViewModel.cs` | L352-357 | 1 秒端口刷新 Timer 在构造阶段立即启动 | P2 |
| P2-8 | `Services/MemoryOptimizerService.cs` | L91-92 | fire-and-forget `Task.Run` 启动 GC 监控 | P2 |
| P2-9 | `Services/MemoryOptimizerService.cs` | L270-295 | `MonitorFullGCNotification` 使用 `async void` | P2 |

---

## 五、修复建议优先级排序

### P1 修复建议（应立即修复）

1. **P1-1 + P1-4 联合修复**: 统一启动协调
   - 将 `ServerDetectionViewModel` 构造函数中的 `StartAutoDetect()` 改为不自动启动
   - 由 `MainViewModel` 在主窗口 `Loaded` 事件后统一协调: 先执行首次检测（`DetectServersAsync`），完成后再启动自动检测循环
   - 这样消除双重并行检测和启动阶段的 CPU 竞争

2. **P1-2 修复**: 首次采集异步化
   - `SystemMonitor.StartMonitoring()` 中将 `callback(CollectSnapshot())` 改为 `callback(await CollectSnapshotAsync())`
   - 或在 `SystemMonitorViewModel.StartMonitoring()` 中先异步采集再回调

3. **P1-3 修复**: Java 查找延迟 + 并行化
   - `SettingsViewModel.LoadJavaInstallationsAsync()` 改为懒加载，仅在用户导航到设置页时触发
   - 或至少延迟 2-3 秒启动，避开自动检测的高峰期
   - `VerifyJava` 改为并行执行（`Parallel.ForEach` 或 `Task.WhenAll`）

4. **P1-5 修复**: 消除 fire-and-forget 嵌套
   - `SystemMonitorViewModel` 构造函数中的 `Task.Run(async () => { await Delay; Dispatcher.InvokeAsync(...) })` 改为持有 Task 引用，或使用更清晰的启动模式（如 `Loaded` 事件 + `async void`）

### P2 修复建议（建议改进）

1. **P2-1**: 合并 `Process.GetProcessesByName` 为单次枚举
2. **P2-3**: 用 `async/await` 替代 `ContinueWith`
3. **P2-4**: Java 验证并行化
4. **P2-7**: 网络监控 Timer 延迟到首屏渲染后启动
5. **P2-8/P2-9**: `MonitorFullGCNotification` 改为 `async Task` + 持有引用
