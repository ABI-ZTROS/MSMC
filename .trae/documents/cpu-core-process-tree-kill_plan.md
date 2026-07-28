# CPU 核心进程负载树与进程管理功能

## 概述

在系统监控页面新增「CPU 核心进程亲和性可视化」功能：以物理核→逻辑核的拓扑树展示 CPU 结构，将 Minecraft 服务器进程通过 CPU 亲和性掩码关联到对应逻辑核并重点标红，同时提供 Java 进程管理（查看详情/杀进程）功能。

## 调研结论（确定 vs 不确定）

| 能力 | 结论 | 依据 |
|------|------|------|
| 系统每核 CPU 负载 | ✅ 已实现 | `SystemMonitor._perCoreCpuCounters`，前端 `CpuTopology` 已渲染 |
| 进程 CPU 亲和性掩码 | ✅ 确定可行 | `Process.ProcessorAffinity`（IntPtr 位掩码，位 N=1 表示允许在核 N 运行） |
| 进程总 CPU% | ✅ 确定可行 | `Process.TotalProcessorTime` 差分，除以采样间隔×核数 |
| 线程理想处理器 | ✅ 确定可行 | `ProcessThread.IdealProcessor`（只读，int） |
| 精确获取进程在各核上的实时负载 | ❌ 不确定/不采用 | 需 ETW 实时追踪（`trace.UseCpuSchedulingData`），管理员权限+高开销+工程复杂，不适合轻量监控面板 |
| 杀进程 | ✅ 已有先例 | `NetworkService.KillProcessByPort`（优雅停止→3s 超时→强杀） |

**采用方案**：亲和性掩码级展示。即展示「Minecraft 进程被允许在哪些逻辑核上运行」，而非「各核上的实时负载比例」。这是轻量、实时、无额外开销的方案。通过亲和性掩码位运算，将 Minecraft 进程关联到对应的逻辑核节点并标红。

**关于 Minecraft 多线程多核心特性**：Paper/Folia 等现代服务端使用多线程并行，进程亲和性掩码默认为全核（`2^n - 1`），意味着 Minecraft 会利用所有可用核心。标红逻辑为：只要进程亲和性掩码的对应位为 1，该逻辑核就标记为「Minecraft 可用」。若用户手动设置了亲和性限制（如只允许跑在 0-3 核），则只有这 4 个核标红。

## 当前状态分析

### 已有基础设施

**后端**：
- [SystemMonitor.cs](file:///workspace/src/McServerGuard/Services/SystemMonitoring/SystemMonitor.cs)：每核 CPU 采集已实现（`_perCoreCpuCounters`，L104-126），Java 进程统计已有（`GetJavaProcessStats()`，L530-626），但 `JavaCpuUsagePercent` 等字段硬编码为 0（L177-183）
- [ThreadAnalyzer.cs](file:///workspace/src/McServerGuard/Services/SystemMonitoring/ThreadAnalyzer.cs)：`AnalyzeJavaThreads(pid)` 已可分析单个 Java 进程线程（L113-153）
- [ProcessScanner.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ProcessScanner.cs)：Java 进程识别已完善，`CollectJavaProcessIds()` 返回 PID 列表
- [NetworkService.cs](file:///workspace/src/McServerGuard/Services/Network/NetworkService.cs)：`KillProcessByPort`（L135-181）是杀进程的现成参考实现
- [CpuIdentifier.cs](file:///workspace/src/McServerGuard/Services/HardwareInfo/CpuIdentifier.cs)：CPU 拓扑识别，含 `LogicalToPhysicalCoreMap`
- [PrivilegeService.cs](file:///workspace/src/McServerGuard/Services/PrivilegeService.cs)：`EnsureAdminPrivileges(reason)` 权限检查
- [App.xaml.cs](file:///workspace/src/McServerGuard/App.xaml.cs)：DI 注册（L144-198）
- [MainWindow.xaml.cs](file:///workspace/src/McServerGuard/Views/MainWindow.xaml.cs)：桥接注册（L532-706 `systemMonitor:*` 区块）

**前端**：
- [SystemMonitorPage.tsx](file:///workspace/src/frontend/src/pages/SystemMonitorPage.tsx)：`CpuTopology` 组件（L21-187）已渲染每核负载网格，使用 `cpuInfo.logicalToPhysicalCoreMap` 映射物理核
- [bridge.ts](file:///workspace/src/frontend/src/utils/bridge.ts)：`killProcess`（L403-405）已有先例
- [types/bridge.ts](file:///workspace/src/frontend/src/types/bridge.ts)：`SystemMetrics`、`CpuInfo` 类型已定义

### 差距分析

1. **进程级 CPU 亲和性数据未采集**：`SystemMetrics` 无进程亲和性字段
2. **无进程管理服务**：缺少独立的进程管理服务（获取亲和性、杀进程）
3. **前端无进程树可视化**：`CpuTopology` 只展示系统级每核负载，未关联进程
4. **无杀进程 UI**：前端无进程列表和杀进程按钮

## 提议改动

### 1. 新增模型：`ProcessAffinityInfo.cs`

**文件**：`src/McServerGuard/Models/ProcessAffinityInfo.cs`（新建）

**内容**：进程亲和性信息 DTO

```csharp
namespace McServerGuard.Models;

/// <summary>
/// Java 进程亲和性信息 —— 描述进程与 CPU 逻辑核的关联关系
/// </summary>
public record ProcessAffinityInfo
{
    /// <summary>进程 PID</summary>
    public int ProcessId { get; init; }

    /// <summary>进程名（如 java、javaw）</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>是否为 Minecraft 服务器进程（由 ProcessScanner 识别）</summary>
    public bool IsMinecraftServer { get; init; }

    /// <summary>服务器显示名（仅 Minecraft 进程有值）</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>CPU 亲和性掩码（位 N=1 表示允许在逻辑核 N 运行）</summary>
    public long AffinityMask { get; init; }

    /// <summary>亲和性掩码对应的逻辑核编号列表</summary>
    public int[] AllowedCoreIndices { get; init; } = [];

    /// <summary>进程总 CPU 使用率百分比（0-100*核数）</summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>工作集内存（字节）</summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>线程数</summary>
    public int ThreadCount { get; init; }

    /// <summary>进程优先级</summary>
    public string PriorityClass { get; init; } = string.Empty;

    /// <summary>命令行参数（截断显示）</summary>
    public string CommandLine { get; init; } = string.Empty;
}
```

### 2. 新增服务：`ProcessManagerService.cs`

**文件**：`src/McServerGuard/Services/SystemMonitoring/ProcessManagerService.cs`（新建）

**职责**：
- 获取所有 Java 进程的亲和性信息
- 获取单个进程详细信息
- 杀进程（复用 `NetworkService.KillProcessByPort` 模式）

**关键方法**：

```csharp
public class ProcessManagerService
{
    private readonly ProcessScanner _processScanner;
    private readonly IPrivilegeService _privilegeService;
    private readonly TimeService _timeService;

    // 上次采样的进程 CPU 时间，用于差分计算
    private readonly Dictionary<int, (DateTime Time, TimeSpan TotalProcessorTime)> _lastCpuSample = new();

    /// <summary>
    /// 获取所有 Java 进程的亲和性信息
    /// </summary>
    public List<ProcessAffinityInfo> GetJavaProcessAffinities();

    /// <summary>
    /// 获取指定进程的详细信息
    /// </summary>
    public ProcessAffinityInfo? GetProcessInfo(int pid);

    /// <summary>
    /// 杀进程（优雅停止 → 3s 超时 → 强杀）
    /// </summary>
    public (bool Success, string? Error) KillProcess(int pid, bool graceful = true);

    /// <summary>
    /// 设置进程 CPU 亲和性
    /// </summary>
    public (bool Success, string? Error) SetProcessAffinity(int pid, long affinityMask);
}
```

**实现要点**：
- `GetJavaProcessAffinities()`：调用 `ProcessScanner.CollectJavaProcessIds()` 获取 PID 列表，对每个 PID 用 `Process.GetProcessById(pid)` 获取 `ProcessorAffinity`、`TotalProcessorTime`、`WorkingSet64`、`Threads.Count`、`PriorityClass`
- CPU% 差分计算：`(当前TotalProcessorTime - 上次TotalProcessorTime) / (采样间隔 × 核数) × 100`，采样间隔通过 `TimeService` 获取
- 亲和性掩码转核心索引：遍历 `long` 的每一位，位为 1 的位号加入 `AllowedCoreIndices`
- `KillProcess`：复用 `NetworkService.KillProcessByPort` 模式（`CloseMainWindow` → 3s 超时 → `Kill(entireProcessTree: true)`），捕获 `Win32Exception(5)` 调用 `EnsureAdminPrivileges`
- 进程退出竞态：所有 `Process` 属性访问包 `try-catch`（`InvalidOperationException` / `Win32Exception`），进程已退出时跳过

**接口**：`src/McServerGuard/Services/SystemMonitoring/IProcessManagerService.cs`（新建）

### 3. DI 注册

**文件**：[App.xaml.cs](file:///workspace/src/McServerGuard/App.xaml.cs)（编辑）

在 L175 附近追加：

```csharp
services.AddSingleton<IProcessManagerService, ProcessManagerService>();
```

### 4. 桥接处理程序注册

**文件**：[MainWindow.xaml.cs](file:///workspace/src/McServerGuard/Views/MainWindow.xaml.cs)（编辑）

在 L706 附近（`systemMonitor:getCpuInfo` 之后）追加：

```csharp
_bridgeService.RegisterRequestHandler("processManager:getAffinities", _ =>
{
    return _processManagerService.GetJavaProcessAffinities();
});

_bridgeService.RegisterRequestHandler("processManager:getInfo", payload =>
{
    var pid = (int)(payload as dynamic)?.pid ?? 0;
    return _processManagerService.GetProcessInfo(pid);
});

_bridgeService.RegisterRequestHandler("processManager:kill", payload =>
{
    var pid = (int)(payload as dynamic)?.pid ?? 0;
    var graceful = (bool?)((payload as dynamic)?.graceful) ?? true;
    return _processManagerService.KillProcess(pid, graceful);
});

_bridgeService.RegisterRequestHandler("processManager:setAffinity", payload =>
{
    var dyn = payload as dynamic;
    var pid = (int?)dyn?.pid ?? 0;
    var mask = (long?)dyn?.affinityMask ?? 0;
    return _processManagerService.SetProcessAffinity(pid, mask);
});
```

需在 `MainWindow` 构造函数注入 `IProcessManagerService`。

### 5. 前端类型定义

**文件**：[types/bridge.ts](file:///workspace/src/frontend/src/types/bridge.ts)（编辑）

新增类型：

```typescript
export interface ProcessAffinityInfo {
  processId: number
  processName: string
  isMinecraftServer: boolean
  displayName: string
  affinityMask: number  // long 作为 number 传输（64位以下足够）
  allowedCoreIndices: number[]
  cpuUsagePercent: number
  workingSetBytes: number
  threadCount: number
  priorityClass: string
  commandLine: string
}

export interface KillProcessByIdRequest {
  pid: number
  graceful?: boolean
}

export interface SetAffinityRequest {
  pid: number
  affinityMask: number
}
```

### 6. 前端桥接 API

**文件**：[bridge.ts](file:///workspace/src/frontend/src/utils/bridge.ts)（编辑）

在 L405 附近追加：

```typescript
// ═════════════════════════════════════════════════════════════════════
// 进程管理 API
// ═════════════════════════════════════════════════════════════════════

export function getProcessAffinities(): Promise<ProcessAffinityInfo[]> {
  return bridge.invoke<ProcessAffinityInfo[]>('processManager:getAffinities')
}

export function getProcessInfo(pid: number): Promise<ProcessAffinityInfo | null> {
  return bridge.invoke<ProcessAffinityInfo | null>('processManager:getInfo', { pid })
}

export function killProcessById(pid: number, graceful: boolean = true): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('processManager:kill', { pid, graceful })
}

export function setProcessAffinity(pid: number, affinityMask: number): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('processManager:setAffinity', { pid, affinityMask })
}
```

### 7. 前端可视化组件：`CpuProcessTree.tsx`

**文件**：`src/frontend/src/components/ui/CpuProcessTree.tsx`（新建）

**职责**：渲染物理核→逻辑核拓扑树，将 Minecraft 进程关联的核心标红

**数据源**：
- `cpuInfo`（`CpuInfo`，含 `logicalToPhysicalCoreMap`）
- `perCoreUsages`（`number[]`，系统每核负载）
- `processAffinities`（`ProcessAffinityInfo[]`，进程亲和性列表）

**树结构**：
```
CPU（根）
├── 物理核 0
│   ├── 逻辑核 0  [负载 45%] [Minecraft] ← 标红
│   └── 逻辑核 1  [负载 12%] [Minecraft] ← 标红
├── 物理核 1
│   ├── 逻辑核 2  [负载 78%]
│   └── 逻辑核 3  [负载 23%] [Minecraft] ← 标红
└── ...
```

**标红逻辑**：
1. 遍历 `processAffinities`，筛选 `isMinecraftServer === true` 的进程
2. 对每个 Minecraft 进程，遍历 `allowedCoreIndices`，将对应逻辑核标记为「Minecraft 占用」
3. 被标记的逻辑核节点：左边框改为红色（`var(--md-gauge-red)`），显示 Minecraft 图标/标签
4. 非 Minecraft 但负载高的核：保持现有黄/红色分级

**交互**：
- 每个逻辑核节点 hover 显示 tooltip：核心编号、物理核映射、系统负载、关联的 Minecraft 进程名+PID
- 点击 Minecraft 标签展开进程详情面板（PID、CPU%、内存、线程数、优先级、命令行）
- 进程详情面板含「终止进程」按钮，点击后调用 `killProcessById(pid)`，成功后刷新列表

**样式**：复用现有 `md-card` 类和 CSS 变量，树形缩进用 `marginLeft` 层级递增。超线程对（同一物理核的两个逻辑核）横向并排。

### 8. 集成到 SystemMonitorPage

**文件**：[SystemMonitorPage.tsx](file:///workspace/src/frontend/src/pages/SystemMonitorPage.tsx)（编辑）

**改动**：
1. 导入 `CpuProcessTree` 组件和 `getProcessAffinities` API
2. 新增 state：`const [processAffinities, setProcessAffinities] = useState<ProcessAffinityInfo[]>([])`
3. 在 `fetchMetrics` 中同时拉取进程亲和性：`const affinities = await getProcessAffinities(); setProcessAffinities(affinities)`
4. 在 `CpuTopology` 组件下方新增 `CpuProcessTree`：

```tsx
{/* ═══ CPU 核心进程亲和性树 ═══ */}
<div style={{ marginBottom: 12 }}>
  <CpuProcessTree
    cpuInfo={cpuInfo}
    perCoreUsages={metrics?.perCoreCpuUsages ?? []}
    processAffinities={processAffinities}
    onKillProcess={handleKillProcess}
  />
</div>
```

5. 新增 `handleKillProcess` 回调：

```tsx
const handleKillProcess = async (pid: number) => {
  try {
    const result = await killProcessById(pid);
    if (result.success) {
      // 刷新列表
      await fetchMetrics();
    } else {
      console.error('杀进程失败:', result.error);
    }
  } catch (e) {
    console.error('杀进程失败:', e);
  }
};
```

## 假设与决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 负载精度 | 亲和性掩码级 | ETW 精确追踪复杂度过高，不适合轻量监控面板；掩码级已能满足「哪些核跑着 Minecraft」的需求 |
| 树结构 | 物理核→逻辑核拓扑 | 复用现有 `LogicalToPhysicalCoreMap`，天然树形，信息密度适中 |
| 杀进程范围 | 仅 Java/Minecraft 进程 | 安全可控，避免误杀系统进程；`ProcessScanner` 已有 Java 进程识别能力 |
| CPU% 计算 | TotalProcessorTime 差分 | 比 PerformanceCounter("Process","% Processor Time") 更稳定，不受计数器实例名漂移影响 |
| 亲和性掩码数据类型 | `long`（C#）/ `number`（TS） | 64 位以下 CPU 足够；JSON 序列化 `IntPtr` 需转 `long` |
| Minecraft 多核处理 | 掩码所有位=1 时全核标红 | 默认全核亲和性，Minecraft 多线程会利用所有可用核，符合预期 |
| 杀进程模式 | 优雅停止→3s 超时→强杀 | 复用 `NetworkService.KillProcessByPort` 成熟模式 |
| 进程列表刷新频率 | 与系统指标同步（2秒） | 复用现有轮询机制，无需额外定时器 |
| 新建文件数 | 4 个（模型+接口+服务+前端组件） | 遵循现有架构分层，不过度拆分 |

## 验证步骤

1. **后端编译验证**：
   ```bash
   cd /workspace/src/McServerGuard
   dotnet build
   ```
   确认无编译错误

2. **前端编译验证**：
   ```bash
   cd /workspace/src/frontend
   npm run build
   ```
   确认 TypeScript 类型检查通过

3. **功能验证清单**：
   - [ ] 启动应用，进入系统监控页面
   - [ ] CPU 拓扑树正确展示物理核→逻辑核层级
   - [ ] 启动一个 Minecraft 服务器，观察其逻辑核节点标红
   - [ ] 点击 Minecraft 标签，展开进程详情面板
   - [ ] 面板显示 PID、CPU%、内存、线程数、优先级、命令行
   - [ ] 点击「终止进程」按钮，进程被终止，列表刷新
   - [ ] 多个 Minecraft 服务器同时运行时，各进程的核标记互不冲突
   - [ ] 非 Minecraft 的 Java 进程不标红（仅显示在进程列表中）
   - [ ] 非管理员模式下杀进程失败时，提示权限不足

4. **边界情况验证**：
   - [ ] 进程在拉取过程中退出：不崩溃，日志记录跳过
   - [ ] 无 Java 进程运行时：树正常展示，无标红节点
   - [ ] CPU 核心数超过 64：亲和性掩码仍正确（`long` 足够）
   - [ ] Minecraft 进程手动设置亲和性限制后：仅受限核标红
