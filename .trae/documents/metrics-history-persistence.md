# 系统监控趋势数据持久化方案

## 概述

当前 CPU/内存使用率趋势数据仅保存在内存环形缓冲区中（最多 120 点，约 4 分钟），应用关闭即丢失。本方案将趋势数据持久化到磁盘，支持跨天历史查询，每天 23:59:59 自动切割新文件，无数据时段不绘制。

## 现状分析

### 数据流
1. `SystemMonitor.StartMonitoring` 每 2 秒采集一次 → 回调 `SystemMonitorViewModel.OnMetricsUpdate`
2. `OnMetricsUpdate` 写入内存环形缓冲区（120 点）+ 维护 LiveCharts2 `_cpuValues`/`_memoryValues`
3. 前端 `SystemMonitorPage` 通过 `systemMonitor:getMetrics` 每 2 秒拉取，本地 `history` 数组追加到 120 点
4. 前端 `systemMonitor:getHistory` 拉取后端 `MetricsHistory` 快照

### 关键约束
- 采样间隔 2 秒 → 每天最多 43,200 个数据点
- 每个数据点仅需 `{ timestamp, cpuUsagePercent, memoryUsagePercent }` → 约 28 字节
- 每天最大数据量 ≈ 1.2 MB（纯数据），JSON 约翻倍 ~2.4 MB
- 数据目录已确定：`%AppData%/McServerGuard/`

### 需求
1. 长期保存 CPU 和内存使用率趋势
2. 使用体积小、容量大的文件格式
3. 无数据的时段不绘制（前端图表跳过间隙）
4. 每日 23:59:59 切割新一天的数据文件

## 设计决策

### 文件格式选择：自定义二进制格式（.msmcd）

**为什么不用 SQLite/LiteDB/JSON？**
- SQLite：引入 ~1MB 依赖，对仅追加写入场景过重
- LiteDB：同样引入额外 NuGet，且对简单时间序列数据过重
- JSON：每天 43200 个点，每次追加需重写整个文件；读取也需全量反序列化

**自定义二进制格式优势：**
- 零额外依赖
- 仅追加写入（append-only），O(1) 写入
- 固定记录大小：每点 16 字节（8 字节 Unix 毫秒时间戳 + 4 字节 float CPU + 4 字节 float 内存）
- 读取时按偏移量直接定位，无需全量解析
- 每天最大 ~691 KB（43200 × 16），一个月 ~20 MB
- 文件头 32 字节：魔数(4B) + 版本(2B) + 采样间隔秒(2B) + 记录数(4B) + 保留(20B)

### 文件命名与切割

- 路径：`%AppData%/McServerGuard/metrics/yyyyMMdd.msmcd`
- 切割时机：采集时检测 `DateTime.Now` 是否跨天，跨天则关闭当前文件、创建新文件
- 不需要定时器在 23:59:59 精确触发——采样循环本身 2 秒一次，在首次发现日期变更时切换即可

### 旧文件清理

- 保留最近 30 天的数据文件
- 启动时扫描 `metrics/` 目录，删除超过 30 天的 `.msmcd` 文件

## 实现计划

### Step 1：新建 `MetricsPersistenceService` 服务

**文件**: `src/McServerGuard/Services/SystemMonitoring/MetricsPersistenceService.cs`

职责：
- 管理当前日期对应的 `.msmcd` 文件句柄
- `Append(long timestampMs, float cpu, float mem)` — 追加一个数据点，自动处理跨天切割
- `LoadDay(DateTime date)` — 读取指定日期的所有数据点
- `LoadRecentDays(int days)` — 读取最近 N 天的数据点
- `CleanupOldFiles()` — 删除超过 30 天的旧文件

二进制格式：
```
文件头 (32 bytes):
  [0..3]   魔数: 0x4D534D43 ("MSMC")
  [4..5]   版本: 0x0001
  [6..7]   采样间隔（秒）: 0x0002
  [8..11]  记录数 (uint32)
  [12..31] 保留（全零）

每条记录 (16 bytes):
  [0..7]   Unix 毫秒时间戳 (int64, little-endian)
  [8..11]  CPU 使用率 (float32, little-endian)
  [12..15] 内存使用率 (float32, little-endian)
```

### Step 2：创建接口 `IMetricsPersistenceService`

**文件**: `src/McServerGuard/Services/SystemMonitoring/IMetricsPersistenceService.cs`

```csharp
public interface IMetricsPersistenceService : IDisposable
{
    void Append(DateTime timestamp, double cpuUsagePercent, double memoryUsagePercent);
    List<MetricsHistoryPoint> LoadDay(DateTime date);
    List<MetricsHistoryPoint> LoadRecentDays(int days);
    void CleanupOldFiles(int retainDays = 30);
}

public record MetricsHistoryPoint(DateTime Timestamp, double CpuUsagePercent, double MemoryUsagePercent);
```

### Step 3：修改 `SystemMonitorViewModel`

**文件**: `src/McServerGuard/ViewModels/SystemMonitorViewModel.cs`

变更：
- 注入 `IMetricsPersistenceService`
- 在 `OnMetricsUpdate` 中调用 `_persistence.Append(metrics.Timestamp, metrics.CpuUsagePercent, metrics.MemoryUsagePercent)`
- 在 `StartMonitoring` 中调用 `_persistence.CleanupOldFiles()`

### Step 4：注册 DI 服务

**文件**: `src/McServerGuard/App.xaml.cs`

变更：
- `services.AddSingleton<IMetricsPersistenceService, MetricsPersistenceService>();`

### Step 5：修改桥接 API — 新增历史数据查询

**文件**: `src/McServerGuard/Views/MainWindow.xaml.cs`

变更：
- 新增 `systemMonitor:getHistoryRange` 桥接 API，接受 `{ days: number }` 参数，返回多天数据
- 修改现有 `systemMonitor:getHistory` 使其返回当天持久化数据（而非内存缓冲区）
- 每条历史记录包含 `timestamp` 字段，前端据此判断数据间隙

### Step 6：修改前端图表 — 支持多天数据与间隙跳过

**文件**: `src/frontend/src/types/bridge.ts`

变更：
- `HistoryPoint` 添加可选 `date` 字段用于区分日期

**文件**: `src/frontend/src/utils/bridge.ts`

变更：
- 新增 `getSystemHistoryRange(days: number)` API
- 修改 `getSystemHistory()` 返回值适配

**文件**: `src/frontend/src/pages/SystemMonitorPage.tsx`

变更：
- 新增日期选择器（最近 7 天/30 天），切换时拉取对应范围的历史数据
- 图表数据点携带真实时间戳，`SimpleLineChart` 组件在连续点时间差 > 阈值（如 30 秒）时断开连线（gap skip）
- 当前实时数据仍然从 `systemMonitor:getMetrics` 拉取并追加到当日图表

### Step 7：图表间隙处理

**文件**: `src/frontend/src/pages/SystemMonitorPage.tsx`

`SimpleLineChart` 组件改造：
- 输入数据改为 `{ timestamp: string, value: number }[]`
- 绘制时，如果相邻两点的 timestamp 间隔 > 30 秒，则在 path 中断开（MoveTo 下一个点而非 LineTo）
- 这样无数据时段不会连线，只显示有数据的片段

## 文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Services/SystemMonitoring/IMetricsPersistenceService.cs` | 新建 | 持久化接口 + `MetricsHistoryPoint` 记录类型 |
| `Services/SystemMonitoring/MetricsPersistenceService.cs` | 新建 | 二进制追加写入实现 |
| `ViewModels/SystemMonitorViewModel.cs` | 修改 | 注入持久化服务，OnMetricsUpdate 追加写入 |
| `App.xaml.cs` | 修改 | DI 注册 |
| `Views/MainWindow.xaml.cs` | 修改 | 新增/修改桥接 API |
| `frontend/src/types/bridge.ts` | 修改 | 扩展 HistoryPoint 类型 |
| `frontend/src/utils/bridge.ts` | 修改 | 新增 API 函数 |
| `frontend/src/pages/SystemMonitorPage.tsx` | 修改 | 日期选择 + 图表间隙处理 |

## 验证步骤

1. 启动应用，监控系统监控页面趋势图正常绘制
2. 关闭应用后重新打开，确认历史趋势数据能恢复显示
3. 检查 `%AppData%/McServerGuard/metrics/` 目录下生成 `.msmcd` 文件
4. 手动修改系统时间到次日，确认新一天数据写入新文件
5. 确认无数据时段（关闭应用一段时间后重新打开）图表不连线
6. 确认超过 30 天的旧文件在启动时被清理
