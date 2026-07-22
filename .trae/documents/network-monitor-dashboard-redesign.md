# 网络监控页面仪表盘化重构计划（收尾）

## 概述

移除 3D 圆柱可视化，将网络监控页面重构为仪表盘风格：端口分布饼图、实时上传/下载网速仪表盘、每日网络吞吐量柱状图与分析。本计划承接上一会话，步骤 1–6 已完成，仅剩步骤 7（精简 code-behind）与步骤 8（服务注册与注入）。

## 当前状态分析（已验证）

### 已完成（步骤 1–6）
- `Views/Controls/GaugeRingControl.cs` — 新增 `Maximum` 依赖属性（默认 100），`DrawGauge()` 改用 `displayValue / max * 270`，颜色档位按百分比判断。向后兼容 SystemMonitorPage。
- `Views/Controls/PieChartControl.cs` — 自绘饼图控件，含 `PieSlice` 模型 + `Slices`/`CenterText`/`CenterSubText` 依赖属性，`INotifyCollectionChanged` 监听。
- `Views/Controls/BarChartControl.cs` — 自绘双系列柱状图，含 `UploadData`/`DownloadData`/`HighlightIndex` 依赖属性，Y 轴自适应、当前小时高亮、图例。
- `Services/Network/NetworkTrafficService.cs` — 网卡字节采样、实时速度、按小时累积、JSON 持久化到 `%APPDATA%/McServerGuard/traffic.json`（保留 30 天，每 60 秒自动保存）。
- `ViewModels/NetworkMonitorViewModel.cs` — 构造函数已注入 `NetworkTrafficService`；新增 `UploadSpeedMB`/`DownloadSpeedMB`/`SpeedMaximumMB`/`UploadSpeedText`/`DownloadSpeedText`/`TodayUploadText`/`TodayDownloadText`/`DailyAnalysisText`/`CurrentHour` 属性、`PortDistributionSlices`/`HourlyUploadData`/`HourlyDownloadData` 集合；`StartAutoRefresh` 每秒同时调用 `RefreshPorts()` 与 `RefreshTraffic()`；`Dispose()` 调用 `_trafficService.Save()`。
- `Views/NetworkMonitorPage.xaml` — 已移除全部 `Viewport3D`；顶部 3 张统计卡 + 上传/下载 `GaugeRingControl` + 自动刷新指示器；右侧上 `PieChartControl` + 下 `BarChartControl` + 分析文本。

### 待修复（步骤 7–8，当前为编译阻断状态）
- `Views/NetworkMonitorPage.xaml.cs` — 仍保留 `UpdateCylinders()`、`ApplyCylinderMesh()`、`OnViewModelPropertyChanged()`，并引用 `SystemCylinderMesh`/`RegisteredCylinderMesh`/`DynamicCylinderMesh` 字段。这些字段由 XAML 的 `x:Name` 生成，但 XAML 已移除 3D，字段不存在 → **编译错误 CS0103**。
- `App.xaml.cs` — 未注册 `NetworkTrafficService`。
- `ViewModels/MainViewModel.cs` — 第 94 行 `new NetworkMonitorViewModel(networkService, portBridgeService)` 只传 2 个参数，而构造函数需 3 个 → **编译错误 CS7036**。

## 实施方案（剩余两步）

### 第七步：精简 NetworkMonitorPage.xaml.cs

**文件**: `src/McServerGuard/Views/NetworkMonitorPage.xaml.cs`

**原因**: XAML 已移除 3D，code-behind 中对已删除 mesh 字段的引用导致编译失败；且饼图/柱状图通过数据绑定自动更新，不再需要 `PropertyChanged` 驱动的圆柱重建。

**改动**:
1. 移除 `using System;`（仅 `UpdateCylinders`/`ApplyCylinderMesh` 用 `Math`，删除后无引用）
2. 移除 `using System.ComponentModel;`（仅 `OnViewModelPropertyChanged` 用 `PropertyChangedEventArgs`）
3. 移除 `using System.Windows.Media;`（仅 `ApplyCylinderMesh` 用 `PointCollection`）
4. 移除 `using System.Windows.Media.Media3D;`（3D 专用）
5. 删除 `OnViewModelPropertyChanged()` 方法
6. 删除 `UpdateCylinders()` 方法
7. 删除 `ApplyCylinderMesh()` 方法
8. `OnLoaded` 简化为仅缓存 `_viewModel`（移除 `PropertyChanged` 订阅与 `UpdateCylinders()` 调用）
9. `OnUnloaded` 简化为仅 `Dispose` + 置空（移除 `PropertyChanged` 取消订阅）

**目标文件内容**:
```csharp
using System.Windows;
using System.Windows.Controls;
using McServerGuard.ViewModels;

namespace McServerGuard.Views;

public partial class NetworkMonitorPage : UserControl
{
    private NetworkMonitorViewModel? _viewModel;

    public NetworkMonitorPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as NetworkMonitorViewModel;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        (_viewModel as IDisposable)?.Dispose();
        _viewModel = null;
    }
}
```

### 第八步：注册服务与注入

#### 8a. App.xaml.cs 注册 NetworkTrafficService

**文件**: `src/McServerGuard/App.xaml.cs`

**原因**: `NetworkTrafficService` 需作为单例注入 `MainViewModel`，当前未注册 → DI 解析失败。

**改动**: 在 `services.AddSingleton<IPortBridgeService, PortBridgeService>();`（第 91 行）之后新增一行：
```csharp
services.AddSingleton<NetworkTrafficService>();
```
（命名空间 `McServerGuard.Services.Network` 已在第 17 行导入，无需新增 using。）

#### 8b. MainViewModel.cs 注入 trafficService

**文件**: `src/McServerGuard/ViewModels/MainViewModel.cs`

**原因**: `NetworkMonitorViewModel` 构造函数已改为三参数，`MainViewModel` 需补传 `trafficService`，否则编译错误。

**改动**:
1. 构造函数参数列表末尾新增 `NetworkTrafficService trafficService`（紧跟 `IPortBridgeService portBridgeService` 之后）
2. 第 77 行服务计数由 `11` 改为 `12`：`Log.Information("🧠 MainViewModel 初始化，注入 {ServiceCount} 个服务", 12);`
3. 第 94 行改为：
   ```csharp
   NetworkPage = new NetworkMonitorViewModel(networkService, portBridgeService, trafficService);
   ```
4. **不新增字段**：`trafficService` 仅透传给 `NetworkPage`，无需存储（`NetworkMonitorViewModel.Dispose()` 自行调用 `_trafficService.Save()`，单例服务生命周期由 DI 容器管理）。命名空间 `McServerGuard.Services.Network` 已在第 20 行导入。

## 假设与决策

1. **trafficService 不存字段**：与 `_networkService`/`_portBridgeService` 的现有写法不同，此处采用最小改动——仅透传参数，避免新增未使用字段（项目 `TreatWarningsAsErrors=true`，CS0414 风险）。现有 `_networkService`/`_portBridgeService` 字段为历史遗留，不在本次改动范围。
2. **服务注册位置**：放在网络服务组（`IPortBridgeService` 之后），语义聚合；`MainViewModel` 在第 138 行注册，注册顺序满足依赖关系。
3. **生命周期**：`NetworkTrafficService` 为单例，`MainViewModel` 为单例；`NetworkMonitorViewModel` 由 `MainViewModel` 构造时 new 出（非 DI 管理），其 `Dispose()` 仅调用 `Save()` 不调用服务 `Dispose()`，单例服务不会被提前释放。

## 验证步骤

1. **编译验证**: `dotnet build src/McServerGuard/McServerGuard.csproj` 无错误无警告。
2. **阻断修复确认**: `NetworkMonitorPage.xaml.cs` 不再引用任何 `*CylinderMesh` 字段；`MainViewModel.cs` 调用三参数构造函数。
3. **DI 解析**: 应用启动时 `NetworkTrafficService` 被成功解析并注入 `MainViewModel` → `NetworkMonitorViewModel`。
4. **运行时**: 网络监控页加载后，上传/下载仪表盘每秒更新，饼图显示端口分布，柱状图显示 24 小时吞吐量并高亮当前小时，分析文本显示总量与峰值时段。
5. **向后兼容**: SystemMonitorPage 三个 `GaugeRingControl`（CPU/内存/磁盘）显示无回归。
6. **持久化**: 退出应用后 `%APPDATA%/McServerGuard/traffic.json` 已写入；重启后柱状图加载历史数据。
