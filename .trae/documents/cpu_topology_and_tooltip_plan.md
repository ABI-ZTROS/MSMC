# CPU 物理拓扑与趋势图 Tooltip 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在系统监控页面添加 CPU 物理拓扑结构可视化（每核负载展示），并为 **CPU 和内存**两张趋势图添加鼠标悬停提示（显示精确百分比与时间戳）。

**Architecture:** 后端使用 Windows PerformanceCounter 采集每核 CPU 使用率，通过 WMI 获取物理拓扑信息（物理核/逻辑核/超线程）；前端以网格卡片形式展示每核负载，并为折线图组件添加通用的交互式 tooltip（CPU 和内存趋势图复用同一组件）。

**Tech Stack:** C# (.NET WPF) + System.Diagnostics.PerformanceCounter + System.Management (WMI) + React + TypeScript + SVG

---

## 调研结论

### CPU 单核负载采集方案

**主方案：PerformanceCounter**
- 类别: `"Processor"`
- 计数器: `"% Processor Time"`
- 实例名: `"0"`, `"1"`, `"2"`, ... （每个逻辑核心一个实例）
- 用法: 为每个核心创建独立 `PerformanceCounter` 实例，调用 `NextValue()` 获取使用率
- 注意: 首次调用返回 0，需预热

**备用方案：WMI**
- 查询: `SELECT LoadPercentage FROM Win32_Processor`
- 返回每个物理 CPU 包的总负载（非每核精度）
- 仅在 PerformanceCounter 不可用时降级使用

**更精确拓扑信息**（Processor Information 类别）:
- `"Processor Information"` 类别提供 NUMA 节点、核心组等更细粒度信息
- 实例名格式: `"0,0"`（NUMA节点, 核心号），可区分物理核与逻辑核
- 普通场景用 `"Processor"` 类别即可

### 参考项目

- **Open Hardware Monitor**: 开源硬件监控，使用 PerformanceCounter + WMI
- **Libre Hardware Monitor**: Open Hardware Monitor 的活跃分支
- **Windows 任务管理器**: 每核负载展示的标杆 UI（网格布局 + 迷你折线图）

---

## 文件清单

### 后端修改（C#）

| 文件 | 修改类型 | 说明 |
|------|----------|------|
| `src/McServerGuard/Models/SystemMetrics.cs` | 修改 | 添加 `PerCoreCpuUsages` 属性（双精度数组） |
| `src/McServerGuard/Models/Hardware/HardwareInfo.cs` | 修改 | 在 `CpuInfo` 中添加拓扑相关属性 |
| `src/McServerGuard/Services/SystemMonitoring/SystemMonitor.cs` | 修改 | 添加每核 CPU 计数器初始化与采集逻辑 |
| `src/McServerGuard/Services/HardwareInfo/CpuIdentifier.cs` | 修改 | 增强拓扑信息获取（物理核/逻辑核映射） |
| `src/McServerGuard/ViewModels/SystemMonitorViewModel.cs` | 修改 | 传递每核数据到前端 |
| `src/McServerGuard/Views/MainWindow.xaml.cs` | 修改 | 桥接 API 增加每核负载和 CPU 拓扑信息 |

### 前端修改（React/TS）

| 文件 | 修改类型 | 说明 |
|------|----------|------|
| `src/frontend/src/types/bridge.ts` | 修改 | 添加每核负载类型、CPU 拓扑类型 |
| `src/frontend/src/utils/bridge.ts` | 修改 | 添加获取 CPU 拓扑的桥接函数 |
| `src/frontend/src/pages/SystemMonitorPage.tsx` | 修改 | 添加 CPU 拓扑卡片、折线图 tooltip |

---

## 任务分解

### Task 1: 后端 - SystemMetrics 模型扩展

**Files:**
- Modify: `src/McServerGuard/Models/SystemMetrics.cs`

- [ ] **Step 1: 添加每核 CPU 使用率属性**

在 `SystemMetrics` 类中添加：

```csharp
/// <summary>
/// 每个逻辑 CPU 核心的使用率百分比数组。
/// 数组索引对应核心编号（0 开始）。
/// </summary>
[ObservableProperty] private double[] _perCoreCpuUsages = [];
```

- [ ] **Step 2: 验证属性命名一致性**

确认属性名使用 PascalCase，与现有属性风格一致（`CpuUsagePercent` → `PerCoreCpuUsages`）。

---

### Task 2: 后端 - CpuInfo 拓扑信息扩展

**Files:**
- Modify: `src/McServerGuard/Models/Hardware/HardwareInfo.cs`
- Modify: `src/McServerGuard/Services/HardwareInfo/CpuIdentifier.cs`

- [ ] **Step 1: 在 CpuInfo 中添加拓扑属性**

在 `CpuInfo` record 中添加：

```csharp
/// <summary>
/// 物理 CPU 插槽数（即 CPU 芯片个数）。
/// </summary>
public int SocketCount { get; init; }

/// <summary>
/// NUMA 节点数量。
/// </summary>
public int NumaNodeCount { get; init; }

/// <summary>
/// 是否启用超线程（逻辑核心数 > 物理核心数）。
/// </summary>
public bool IsHyperThreadingEnabled { get; init; }

/// <summary>
/// 逻辑核心编号到物理核心编号的映射数组。
/// 数组索引为逻辑核心号，值为物理核心号。
/// 例如 [0, 0, 1, 1] 表示 2 个物理核心，每个物理核心 2 个线程。
/// </summary>
public int[] LogicalToPhysicalCoreMap { get; init; } = [];
```

- [ ] **Step 2: 在 CpuIdentifier 中填充拓扑信息**

修改 `GetCpuInfoInternal` 方法，补充查询：
- `NumberOfCores` → 物理核（已有）
- `NumberOfLogicalProcessors` → 逻辑核（已有）
- `SocketCount` 从 WMI 查询（Win32_Processor 的 DeviceID 计数）
- `IsHyperThreadingEnabled` = LogicalCores > PhysicalCores
- `LogicalToPhysicalCoreMap`: 简化处理，按逻辑核/物理核比率均匀映射（例如 8 逻辑核 4 物理核 → [0,0,1,1,2,2,3,3]）

```csharp
// 计算逻辑核到物理核的映射
var logicalToPhysicalMap = new int[logicalCores];
if (physicalCores > 0 && logicalCores >= physicalCores)
{
    var threadsPerCore = (int)Math.Round((double)logicalCores / physicalCores);
    for (int i = 0; i < logicalCores; i++)
    {
        logicalToPhysicalMap[i] = i / threadsPerCore;
    }
}
```

---

### Task 3: 后端 - SystemMonitor 每核采集

**Files:**
- Modify: `src/McServerGuard/Services/SystemMonitoring/SystemMonitor.cs`

- [ ] **Step 1: 添加每核计数器字段与初始化**

在 `SystemMonitor` 类中添加：

```csharp
/// <summary>
/// 每个 CPU 核心的性能计数器
/// </summary>
private PerformanceCounter[]? _perCoreCpuCounters;

/// <summary>
/// 初始化每核 CPU 计数器
/// </summary>
private void InitPerCoreCounters()
{
    try
    {
        var coreCount = Environment.ProcessorCount;
        _perCoreCpuCounters = new PerformanceCounter[coreCount];
        for (int i = 0; i < coreCount; i++)
        {
            _perCoreCpuCounters[i] = new PerformanceCounter(
                "Processor",
                "% Processor Time",
                i.ToString(),
                true);
            _perCoreCpuCounters[i].NextValue(); // 预热
        }
        Log.Debug("每核 CPU 计数器已初始化，共 {Count} 个核心", coreCount);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "初始化每核 CPU 计数器失败: {Msg}", ex.Message);
        _perCoreCpuCounters = null;
    }
}
```

在构造函数中调用 `InitPerCoreCounters()`（放在现有 CPU 计数器预热之后）。

- [ ] **Step 2: 添加每核采集方法**

```csharp
/// <summary>
/// 获取每个 CPU 核心的使用率
/// </summary>
/// <returns>每核使用率数组，索引对应核心编号</returns>
private double[] GetPerCoreCpuUsage()
{
    if (_perCoreCpuCounters == null || _perCoreCpuCounters.Length == 0)
        return [];

    try
    {
        var result = new double[_perCoreCpuCounters.Length];
        for (int i = 0; i < _perCoreCpuCounters.Length; i++)
        {
            var value = _perCoreCpuCounters[i].NextValue();
            result[i] = Math.Round(Math.Max(0, Math.Min(100, value)), 2);
        }
        return result;
    }
    catch (Exception ex)
    {
        Log.Debug("获取每核 CPU 使用率失败: {Msg}", ex.Message);
        return [];
    }
}
```

- [ ] **Step 3: 在 CollectSnapshot 中调用并填充**

在 `CollectSnapshot` 方法中，在 `var cpuUsage = GetCpuUsage();` 之后添加：

```csharp
var perCoreUsages = GetPerCoreCpuUsage();
```

并在返回的 `SystemMetrics` 对象中添加：

```csharp
PerCoreCpuUsages = perCoreUsages,
```

- [ ] **Step 4: 资源释放**

在 `Dispose` 方法中添加每核计数器的释放：

```csharp
if (_perCoreCpuCounters != null)
{
    foreach (var counter in _perCoreCpuCounters)
        counter.Dispose();
    _perCoreCpuCounters = null;
}
```

---

### Task 4: 后端 - 桥接 API 扩展

**Files:**
- Modify: `src/McServerGuard/Views/MainWindow.xaml.cs`

- [ ] **Step 1: 在 getMetrics 响应中添加每核数据**

修改 `systemMonitor:getMetrics` 处理函数，在返回对象中添加：

```csharp
perCoreCpuUsages = metrics.PerCoreCpuUsages,
```

（两处都要加：metrics == null 的默认返回 和 正常返回）

默认值设为 `new double[0]`。

- [ ] **Step 2: 添加 CPU 拓扑信息 API**

新增 `systemMonitor:getCpuInfo` 处理函数：

```csharp
_bridgeService.RegisterRequestHandler("systemMonitor:getCpuInfo", _ =>
{
    var cpuInfo = _cpuIdentifier?.GetCpuInfo();
    if (cpuInfo == null)
    {
        return Task.FromResult<object?>(new
        {
            modelName = "未知 CPU",
            manufacturer = "未知",
            physicalCores = 0,
            logicalCores = Environment.ProcessorCount,
            socketCount = 1,
            numaNodeCount = 1,
            isHyperThreadingEnabled = false,
            baseClockGHz = 0,
            boostClockGHz = 0,
            architecture = "未知",
            logicalToPhysicalCoreMap = Array.Empty<int>(),
        });
    }

    return Task.FromResult<object?>(new
    {
        modelName = cpuInfo.ModelName,
        manufacturer = cpuInfo.Manufacturer,
        physicalCores = cpuInfo.PhysicalCores,
        logicalCores = cpuInfo.LogicalCores,
        socketCount = cpuInfo.SocketCount,
        numaNodeCount = cpuInfo.NumaNodeCount,
        isHyperThreadingEnabled = cpuInfo.IsHyperThreadingEnabled,
        baseClockGHz = cpuInfo.BaseClockGHz,
        boostClockGHz = cpuInfo.BoostClockGHz,
        architecture = cpuInfo.Architecture,
        logicalToPhysicalCoreMap = cpuInfo.LogicalToPhysicalCoreMap,
    });
});
```

注意：需要确保 `_cpuIdentifier` 在 MainWindow 中有注入。如果没有，通过 `_vm` 的服务定位器获取，或者在构造函数中注入 `CpuIdentifier`。

---

### Task 5: 前端 - 类型定义与桥接函数

**Files:**
- Modify: `src/frontend/src/types/bridge.ts`
- Modify: `src/frontend/src/utils/bridge.ts`

- [ ] **Step 1: 添加 SystemMetrics perCore 字段**

在 `SystemMetrics` 接口中添加：

```typescript
perCoreCpuUsages: number[]
```

- [ ] **Step 2: 添加 CpuInfo 类型**

```typescript
export interface CpuInfo {
  modelName: string
  manufacturer: string
  physicalCores: number
  logicalCores: number
  socketCount: number
  numaNodeCount: number
  isHyperThreadingEnabled: boolean
  baseClockGHz: number
  boostClockGHz: number
  architecture: string
  logicalToPhysicalCoreMap: number[]
}
```

- [ ] **Step 3: 添加桥接函数**

在 `bridge.ts` 中添加：

```typescript
export function getCpuInfo(): Promise<CpuInfo> {
  return bridge.invoke<CpuInfo>('systemMonitor:getCpuInfo')
}
```

---

### Task 6: 前端 - CPU 拓扑可视化组件

**Files:**
- Modify: `src/frontend/src/pages/SystemMonitorPage.tsx`

- [ ] **Step 1: 添加 CPU 信息获取逻辑**

在 `SystemMonitorPage` 组件中添加：
- `cpuInfo` state
- `fetchCpuInfo` 函数
- 在 `useEffect` 中初始调用一次（CPU 信息是静态的，只需获取一次）

```tsx
const [cpuInfo, setCpuInfo] = useState<CpuInfo | null>(null)

const fetchCpuInfo = async () => {
  try {
    const data = await getCpuInfo()
    setCpuInfo(data)
  } catch (e) {
    console.error('获取 CPU 信息失败:', e)
  }
}

// 初始获取 CPU 信息（静态，仅一次）
useEffect(() => {
  fetchCpuInfo()
}, [])
```

- [ ] **Step 2: 创建 CpuTopology 组件（内联定义）**

定义一个 `CpuCoreGrid` 组件，展示每核负载：

```tsx
interface CpuCoreGridProps {
  perCoreUsages: number[]
  cpuInfo: CpuInfo | null
}

function CpuCoreGrid({ perCoreUsages, cpuInfo }: CpuCoreGridProps): JSX.Element {
  const logicalCores = cpuInfo?.logicalCores || perCoreUsages.length || 0
  const physicalCores = cpuInfo?.physicalCores || logicalCores
  const isHT = cpuInfo?.isHyperThreadingEnabled || false

  // 计算网格列数（根据核心数动态调整，最多 8 列）
  const cols = Math.min(Math.max(Math.ceil(Math.sqrt(logicalCores)), 4), 8)

  return (
    <div style={{ width: '100%' }}>
      {/* 标题行 */}
      <div style={{ 
        display: 'flex', 
        justifyContent: 'space-between', 
        alignItems: 'center',
        marginBottom: 12 
      }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--md-body)' }}>
          🧬 CPU 拓扑
        </div>
        <div style={{ fontSize: 11, color: 'var(--md-body-light)', opacity: 0.7 }}>
          {physicalCores} 物理核 / {logicalCores} 逻辑核
          {isHT && ' · 超线程'}
        </div>
      </div>

      {/* 核心网格 */}
      <div 
        style={{ 
          display: 'grid', 
          gridTemplateColumns: `repeat(${cols}, 1fr)`,
          gap: 6 
        }}
      >
        {Array.from({ length: logicalCores }, (_, i) => {
          const usage = perCoreUsages[i] ?? 0
          const physicalCore = cpuInfo?.logicalToPhysicalCoreMap?.[i] ?? i
          const isFirstThreadOfCore = !isHT || cpuInfo?.logicalToPhysicalCoreMap?.[i - 1] !== physicalCore
          
          // 根据负载计算颜色
          const color = usage > 80 ? 'var(--md-gauge-red)' 
            : usage > 50 ? 'var(--md-gauge-yellow)' 
            : 'var(--md-gauge-green)'

          return (
            <div
              key={i}
              title={`核心 ${i} (物理核 ${physicalCore}): ${usage.toFixed(1)}%`}
              style={{
                position: 'relative',
                aspectRatio: '1 / 1',
                borderRadius: 6,
                background: 'var(--md-bg-secondary)',
                border: `1px solid var(--md-subtle-border)`,
                overflow: 'hidden',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                padding: 4,
              }}
            >
              {/* 背景填充 */}
              <div
                style={{
                  position: 'absolute',
                  bottom: 0,
                  left: 0,
                  right: 0,
                  height: `${usage}%`,
                  background: color,
                  opacity: 0.2,
                  transition: 'height 0.3s ease-out',
                }}
              />
              {/* 核心编号 */}
              <div style={{ 
                fontSize: 9, 
                color: 'var(--md-body-lighter)', 
                position: 'relative',
                zIndex: 1 
              }}>
                C{i}
              </div>
              {/* 百分比 */}
              <div style={{ 
                fontSize: 10, 
                fontWeight: 700, 
                color,
                position: 'relative',
                zIndex: 1,
                fontVariantNumeric: 'tabular-nums',
              }}>
                {usage.toFixed(0)}%
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
```

- [ ] **Step 3: 在页面中集成 CPU 拓扑组件**

在仪表盘区域下方（折线图区域上方）添加 CPU 拓扑卡片：

找到折线图区域 `<div className="grid grid-cols-2" ...>` 之前，添加：

```tsx
{/* ═══ CPU 拓扑结构 ═══ */}
<div className="md-card" style={{ padding: 16, marginBottom: 12 }}>
  <CpuCoreGrid 
    perCoreUsages={metrics?.perCoreCpuUsages || []}
    cpuInfo={cpuInfo}
  />
</div>
```

---

### Task 7: 前端 - 折线图 Tooltip 交互

**Files:**
- Modify: `src/frontend/src/pages/SystemMonitorPage.tsx`

- [ ] **Step 1: 扩展 SimpleLineChart Props**

添加 `timestamps` 属性和 `showTooltip` 支持：

```tsx
interface LineChartProps {
  data: number[]
  color: string
  height?: number
  label: string
  timestamps?: string[]  // 新增：每个数据点的时间戳
}
```

- [ ] **Step 2: 添加 tooltip 状态与鼠标交互**

在 `SimpleLineChart` 组件中添加：

```tsx
const [hoverIndex, setHoverIndex] = useState<number | null>(null)
const svgRef = useRef<SVGSVGElement>(null)

const handleMouseMove = (e: React.MouseEvent<SVGSVGElement>) => {
  if (data.length === 0) return
  const svg = svgRef.current
  if (!svg) return
  
  const rect = svg.getBoundingClientRect()
  const mouseX = e.clientX - rect.left
  const scaleX = rect.width / width
  const chartMouseX = mouseX / scaleX - padding.left
  
  if (chartMouseX < 0 || chartMouseX > chartWidth) {
    setHoverIndex(null)
    return
  }
  
  // 计算最近的数据点索引
  const ratio = chartMouseX / chartWidth
  const index = Math.round(ratio * (data.length - 1))
  setHoverIndex(Math.max(0, Math.min(data.length - 1, index)))
}

const handleMouseLeave = () => {
  setHoverIndex(null)
}
```

- [ ] **Step 3: 在 SVG 中添加 tooltip 指示器**

在 `</svg>` 结束前添加（放在折线/面积之后）：

```tsx
{/* Tooltip 指示器 */}
{hoverIndex !== null && points[hoverIndex] && (
  <g>
    {/* 垂直指示线 */}
    <line
      x1={points[hoverIndex].x}
      y1={padding.top}
      x2={points[hoverIndex].x}
      y2={padding.top + chartHeight}
      stroke="var(--md-body)"
      strokeWidth="1"
      strokeDasharray="3,3"
      opacity="0.5"
    />
    {/* 数据点高亮 */}
    <circle
      cx={points[hoverIndex].x}
      cy={points[hoverIndex].y}
      r={5}
      fill={color}
      stroke="white"
      strokeWidth="2"
    />
  </g>
)}
```

- [ ] **Step 4: 添加 tooltip 浮层（在 SVG 外层）**

将 SVG 包裹在一个相对定位的 div 中，添加 tooltip 浮层：

```tsx
<div style={{ position: 'relative', width: '100%', height: chartHeight + padding.top + padding.bottom }}>
  <svg
    ref={svgRef}
    width="100%"
    height={chartHeight + padding.top + padding.bottom}
    viewBox={`0 0 ${width} ${chartHeight + padding.top + padding.bottom}`}
    preserveAspectRatio="none"
    onMouseMove={handleMouseMove}
    onMouseLeave={handleMouseLeave}
    style={{ cursor: data.length > 0 ? 'crosshair' : 'default' }}
  >
    {/* ... 原有 SVG 内容 ... */}
  </svg>
  
  {/* Tooltip 浮层 */}
  {hoverIndex !== null && (
    <div
      style={{
        position: 'absolute',
        top: 8,
        left: `${(points[hoverIndex].x / width) * 100}%`,
        transform: 'translateX(-50%)',
        background: 'var(--md-elevated-bg)',
        border: '1px solid var(--md-subtle-border)',
        borderRadius: 6,
        padding: '6px 10px',
        fontSize: 11,
        color: 'var(--md-body)',
        pointerEvents: 'none',
        whiteSpace: 'nowrap',
        zIndex: 10,
        boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
      }}
    >
      <div style={{ fontWeight: 700, color, marginBottom: 2 }}>
        {data[hoverIndex].toFixed(2)}%
      </div>
      {timestamps && timestamps[hoverIndex] && (
        <div style={{ color: 'var(--md-body-light)', fontSize: 10 }}>
          {formatTime(timestamps[hoverIndex])}
        </div>
      )}
    </div>
  )}
</div>
```

- [ ] **Step 5: 添加时间格式化辅助函数**

```tsx
function formatTime(isoString: string): string {
  try {
    const d = new Date(isoString)
    const hh = d.getHours().toString().padStart(2, '0')
    const mm = d.getMinutes().toString().padStart(2, '0')
    const ss = d.getSeconds().toString().padStart(2, '0')
    return `${hh}:${mm}:${ss}`
  } catch {
    return isoString
  }
}
```

- [ ] **Step 6: 传入 timestamps 数据**

修改页面中两处 `SimpleLineChart` 的调用：

CPU 趋势图：
```tsx
<SimpleLineChart
  data={cpuHistory}
  timestamps={history.map(h => h.timestamp)}
  color="var(--md-gauge-green)"
  height={228}
  label="CPU 使用率趋势"
/>
```

内存趋势图：
```tsx
<SimpleLineChart
  data={memHistory}
  timestamps={history.map(h => h.timestamp)}
  color="var(--md-primary-hue-mid)"
  height={228}
  label="内存使用率趋势"
/>
```

---

## 风险与注意事项

### 性能风险
- **每核计数器开销**：每个核心一个 PerformanceCounter，16 核系统有 16 个计数器。2 秒采样间隔下开销可忽略。
- **缓解**：仅在监控启动时初始化，停止时释放；复用计数器实例而非每次新建。

### 兼容性风险
- **非 Windows 平台**：PerformanceCounter 仅 Windows 可用。
- **缓解**：失败时返回空数组，前端优雅降级（不显示拓扑卡片或显示空状态）。

### 超线程映射精度
- WMI 简化映射可能与实际硬件拓扑不完全一致（尤其是大小核架构）。
- 缓解：当前使用均匀映射算法，足够可视化展示；如需精确拓扑，需解析 CPUID 或使用更底层的 API（如 `GetLogicalProcessorInformation`）。

### 前端 Tooltip 性能
- 鼠标移动频繁触发状态更新，可能导致重渲染。
- 缓解：使用 `useState` 已足够（数据量小，120 点以内）；如遇性能问题，可用 `useRef` + 直接 DOM 操作优化。

---

## 验收标准

1. **CPU 拓扑展示**
   - 系统监控页面显示 CPU 拓扑卡片
   - 展示物理核/逻辑核数量
   - 每个核心显示当前负载百分比和视觉填充
   - 鼠标悬停显示核心编号和精确负载

2. **趋势图 Tooltip（CPU + 内存两张图）**
   - CPU 使用率趋势图：鼠标移到图上显示垂直指示线和数据点高亮
   - 内存使用率趋势图：鼠标移到图上显示垂直指示线和数据点高亮
   - 显示精确的百分比值（保留 2 位小数）
   - 显示对应时间戳（时:分:秒 格式）
   - 鼠标移出后 tooltip 消失

3. **错误处理**
   - 获取 CPU 信息失败时不崩溃，显示空状态或降级展示
   - 每核数据为空时拓扑卡片不显示或显示提示
