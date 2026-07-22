# 调研：用现成 NuGet 包替代自研轮子

## 摘要

项目当前共有 **~1264 行 WPF 自绘图表控件** + **~1073 行 Windows 系统交互服务** + **~469 行业务领域常量**。本次调研结论：

- **图表控件**：用 **LiveCharts2** (`LiveChartsCore.SkiaSharpView.WPF`) 替换 **3 个自绘控件**（PieChartControl/BarChartControl/TrendChartControl），GaugeRingControl 因定制动画 + 三档语义色映射保留自研。可削减约 **634 行** 自绘代码。
- **网络桥接/扫描/端口映射**：**无合适的现成包**可替代。`froxy`/`Portless.NET` 是 dotnet tool（命令行）不是嵌入库；`SSH.NET` 是 SSH 隧道，机制与 `netsh portproxy` 不同；Vanara.PInvoke 已用于端口-进程映射。这部分**保持现状**。
- **流量采样**：`Oakrey.Network.Tools` 提供 `TrafficMonitor`（IObservable<Traffic>），但要求 **.NET 10**，当前项目是 `net9.0-windows`。当前 `NetworkTrafficService` 已用原生 `GetIPv4Statistics()` 零依赖实现，**保持现状**。

最终净收益：减少 1 个 NuGet 依赖（LiveCharts2，约 3MB），删 3 个自绘控件文件 + 替换 XAML 绑定，**不增加** .NET 版本要求。

---

## 现状分析（基于 Phase 1 探索）

### 自研轮子清单与可替代性

| 模块 | 文件 | 行数 | 可替代性 | 现成包候选 |
|---|---|---|---|---|
| PieChartControl | [Views/Controls/PieChartControl.cs](file:///workspace/src/McServerGuard/Views/Controls/PieChartControl.cs) | 233 | **高** | LiveCharts2 `PieSeries` |
| BarChartControl | [Views/Controls/BarChartControl.cs](file:///workspace/src/McServerGuard/Views/Controls/BarChartControl.cs) | 273 | **高** | LiveCharts2 `ColumnSeries` |
| TrendChartControl | [Views/Controls/TrendChartControl.cs](file:///workspace/src/McServerGuard/Views/Controls/TrendChartControl.cs) | 361 | **高** | LiveCharts2 `LineSeries` + `Fill` |
| GaugeRingControl | [Views/Controls/GaugeRingControl.cs](file:///workspace/src/McServerGuard/Views/Controls/GaugeRingControl.cs) | 397 | **中** | LiveCharts2 `Gauge`（缺三档色映射，不推荐） |
| PortBridgeService | [Services/Network/PortBridgeService.cs](file:///workspace/src/McServerGuard/Services/Network/PortBridgeService.cs) | 362 | **低** | 无（netsh portproxy 无封装库） |
| NetworkTrafficService | [Services/Network/NetworkTrafficService.cs](file:///workspace/src/McServerGuard/Services/Network/NetworkTrafficService.cs) | 275 | **中** | Oakrey.Network.Tools（需 .NET 10） |
| PortScanner | [Services/ServerDetection/PortScanner.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/PortScanner.cs) | 140 | **中-高** | 收益小（当前 11 端口本地扫描已极简） |
| NetworkService | [Services/Network/NetworkService.cs](file:///workspace/src/McServerGuard/Services/Network/NetworkService.cs) | 158 | **低** | 业务聚合层无对应库 |
| PortToProcessMapper | [Services/ServerDetection/PortToProcessMapper.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/PortToProcessMapper.cs) | 138 | **已用 Vanara** | `Vanara.PInvoke.IpHlpApi` 已引入 |
| ServerConstants | [Constants/ServerConstants.cs](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs) | 354 | **低** | 业务领域知识（31 种 MC 服务器识别指纹） |

### 候选包对比（图表库）

| 库 | 许可证 | Gauge | Pie | Column | Line | 跨平台 | 备注 |
|---|---|---|---|---|---|---|---|
| **LiveCharts2** | MIT | ✓ | ✓ | ✓ | ✓ | ✓ | SkiaSharp 渲染，MVVM 友好，2.x 稳定 |
| ScottPlot 5 | MIT | ✗ | ✓ | ✓ | ✓ | ✓ | 性能最高，但无 Gauge 控件 |
| OxyPlot | MIT | ✗ | ✓ | ✓ | ✓ | ✓ | 软件渲染，无 Gauge，维护偏慢 |
| 自绘 DrawingVisual | — | ✓ | ✓ | ✓ | ✓ | 仅 Windows | 零依赖、零 GC，但开发成本高 |

**结论**：只有 LiveCharts2 同时覆盖 Gauge/Pie/Column/Line 四类控件，且 MIT 许可证无商业限制。但 Gauge 的三档语义色（绿/黄/红）需自定义 `PaintTasks`，迁移成本高于另三个图表控件，因此 **GaugeRingControl 保留自研**。

### 候选包对比（端口桥接）

| 包 | 类型 | 替代 netsh portproxy？ | 替代 advfirewall？ | 结论 |
|---|---|---|---|---|
| `froxy` | dotnet tool（CLI） | ✗ 非嵌入库 | ✗ | 不可用 |
| `Portless.NET.Tool` | dotnet tool（YARP 反代） | ✗ 机制不同 | ✗ | 不可用 |
| `SSH.NET` | 库 | ✗ SSH 隧道 ≠ portproxy | ✗ | 不可用 |
| `Chilkat.SshTunnel` | 库（商业） | ✗ 付费 + SSH | ✗ | 不可用 |
| `Vanara.PInvoke.NetFw` | 库 | ✗ 仅防火墙 API | ✓ 可替代 | 但防火墙代码仅 30 行，netsh 已够简洁 |

**结论**：没有任何现成包封装 `netsh portproxy` 文本接口。`Vanara.PInvoke.NetFw` 可编程操作防火墙规则（`INetFwPolicy2`），但当前 [EnableFirewallRule](file:///workspace/src/McServerGuard/Services/Network/PortBridgeService.cs) 仅 30 行 netsh 调用已足够，迁移到 COM 互操作反而增加复杂度。**保持现状**。

---

## 提议变更

### 变更 1：引入 LiveCharts2 NuGet 包

**文件**：[src/McServerGuard/McServerGuard.csproj](file:///workspace/src/McServerGuard/McServerGuard.csproj)

**改动**：在 `<ItemGroup>` 中追加一行 PackageReference。

```xml
<PackageReference Include="LiveChartsCore.SkiaSharpView.WPF" Version="2.*" />
```

**为什么**：MIT 许可证、SkiaSharp 硬件加速渲染、原生 WPF 控件、2.x 稳定版、覆盖 Pie/Column/Line 全套。版本用 `2.*` 与项目其他包（CommunityToolkit/MaterialDesign 均用 `*`）保持一致风格。

### 变更 2：删除 PieChartControl 自绘控件

**删除文件**：[src/McServerGuard/Views/Controls/PieChartControl.cs](file:///workspace/src/McServerGuard/Views/Controls/PieChartControl.cs)（233 行）

**为什么**：LiveCharts2 的 `PieChart` 控件原生支持扇形按值分配、百分比标签、图例，完全等价当前自绘实现。

**XAML 替换**（在 [Views/NetworkMonitorPage.xaml](file:///workspace/src/McServerGuard/Views/NetworkMonitorPage.xaml) 中）：

将原 `<controls:PieChartControl .../>` 替换为：

```xml
<lcw:PieChart Height="220"
              Series="{Binding PortDistributionSeries}"
              LegendPosition="Right"
              LegendTextPaint="{StaticResource ChartLabelPaint}"
              TooltipPosition="Center" />
```

**ViewModel 改动**（[ViewModels/NetworkMonitorViewModel.cs](file:///workspace/src/McServerGuard/ViewModels/NetworkMonitorViewModel.cs)）：

- 删除 `ObservableCollection<PieSlice> PortDistributionSlices` 属性 + `UpdatePieSlices()` 方法
- 新增 `ISeries[] PortDistributionSeries` 属性，由 `UpdatePieSlices()` 改名为 `UpdatePortDistributionSeries()` 填充，使用 `PieSeries<double>` + `SKColor`（替换原 `Color.FromRgb`）

### 变更 3：删除 BarChartControl 自绘控件

**删除文件**：[src/McServerGuard/Views/Controls/BarChartControl.cs](file:///workspace/src/McServerGuard/Views/Controls/BarChartControl.cs)（273 行）

**为什么**：LiveCharts2 的 `ColumnSeries` 双系列 + `Highlight` 完全等价当前自绘双系列柱状图 + 当前小时高亮。

**XAML 替换**：

```xml
<lcw:CartesianChart Height="240"
                    Series="{Binding HourlyThroughputSeries}"
                    XAxes="{Binding HourlyThroughputXAxis}"
                    TooltipPosition="Top" />
```

**ViewModel 改动**：
- 删除 `ObservableCollection<double> HourlyUploadData` + `HourlyDownloadData`
- 新增 `ISeries[] HourlyThroughputSeries`（两个 `ColumnSeries<double>`，分别绑定上传/下载 24 小时数据）
- 新增 `ICartesianAxis[] HourlyThroughputXAxis`（X 轴标签 0–23 时）
- 当前小时高亮：用 `ColumnSeries<T>.Mapping` 设置 `CustomHoverFill`/`CustomHoverStroke`

### 变更 4：删除 TrendChartControl 自绘控件

**删除文件**：[src/McServerGuard/Views/Controls/TrendChartControl.cs](file:///workspace/src/McServerGuard/Views/Controls/TrendChartControl.cs)（361 行）

**为什么**：LiveCharts2 的 `LineSeries` + `Fill` 面积填充 + `GeometrySize` 最新点光晕完全等价当前自绘折线趋势图。

**需先确认使用位置**：搜索结果显示该控件已存在，但需确认是否在当前页面实际使用。若未使用则直接删除文件即可，无需 XAML 替换。

**XAML 替换**（若使用）：

```xml
<lcw:CartesianChart Series="{Binding TrendSeries}"
                    DrawMarginFrame="{Binding TrendFrame}" />
```

### 变更 5：GaugeRingControl 保留自研

**文件**：[src/McServerGuard/Views/Controls/GaugeRingControl.cs](file:///workspace/src/McServerGuard/Views/Controls/GaugeRingControl.cs)（397 行，不动）

**为什么**：
1. 当前实现有**三档语义色映射**（0–60% 绿 / 60–85% 黄 / 85–100% 红），LiveCharts2 的 `Gauge` 控件需用 `PaintTasks` + `NeedleConstraint` 自定义实现同等效果，迁移后代码未必更少
2. 当前实现有 **`DisplayValue` 中间动画属性** + `QuarticEase` 缓动 + `Freeze()` 零 GC 缓存，性能优化专业
3. 网速仪表盘需要 **非百分比 `Maximum`** 支持（已实现），LiveCharts2 `Gauge` 的 `Max` 属性行为需额外验证

**结论**：保留自研，避免迁移风险。

### 不变更：网络服务层

以下模块**保持现状**，不引入新包：

- [PortBridgeService.cs](file:///workspace/src/McServerGuard/Services/Network/PortBridgeService.cs)：netsh portproxy + advfirewall 无现成封装库
- [NetworkTrafficService.cs](file:///workspace/src/McServerGuard/Services/Network/NetworkTrafficService.cs)：Oakrey.Network.Tools 需 .NET 10（项目当前 net9.0-windows），原生 `GetIPv4Statistics()` 已零依赖
- [PortScanner.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/PortScanner.cs)：仅扫 11 个本地端口，140 行已极简
- [NetworkService.cs](file:///workspace/src/McServerGuard/Services/Network/NetworkService.cs) + [PortToProcessMapper.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/PortToProcessMapper.cs)：已用 `Vanara.PInvoke.IpHlpApi`
- [ServerConstants.cs](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs)：31 种 MC 服务器识别指纹是项目核心领域知识

---

## 假设与决策

1. **LiveCharts2 版本**：用 `2.*` 通配符，与项目其他包风格一致。当前稳定版 2.0.0-rc5+ 已可用，正式版随时跟进。
2. **.NET 版本**：**不升级**。项目保持 `net9.0-windows10.0.22000.0`。LiveCharts2 支持 .NET 6+，无需升级。
3. **包体积**：接受 LiveCharts2 + SkiaSharp 增加 ~3MB 单文件发布体积（当前已 `SelfContained=true` 本身体积就大，3MB 增量可忽略）。
4. **性能权衡**：LiveCharts2 用 SkiaSharp 渲染，每帧有少量 GC（远小于自绘的零 GC），但 WPF 仪表盘每秒刷新场景下性能足够。
5. **GaugeRingControl 不迁移**：三档色 + DisplayValue 动画是定制需求，迁移成本 > 收益。
6. **TrendChartControl 使用情况**：执行时需先用 Grep 确认该控件是否在 XAML 中实际引用；若未引用则直接删文件不做 XAML 替换。
7. **PieSlice/BarChart 数据结构**：[Models/PieSlice.cs](file:///workspace/src/McServerGuard/Models/PieSlice.cs) 若仅被 PieChartControl 使用，删除控件后一并删除该模型。

---

## 验证步骤

1. **编译验证**：`dotnet build src/McServerGuard/McServerGuard.csproj` 通过（`TreatWarningsAsErrors=true` 下零警告）
2. **运行验证**：启动应用，进入网络监控页面
   - 饼图正确显示系统/注册/动态端口三色扇形 + 百分比标签
   - 柱状图正确显示 24 小时上传/下载双系列 + 当前小时高亮
   - 上传/下载仪表盘（GaugeRingControl 保留）动画正常
   - 每秒刷新无闪烁、无跨线程异常
3. **包体积验证**：发布后 `bin/Release/net9.0-windows/win-x64/publish/` 体积增量 ≤ 5MB
4. **回归验证**：桥接/端口扫描/流量统计功能不受影响（未改动相关服务层）

---

## 执行顺序建议

1. 追加 LiveCharts2 PackageReference + 还原 NuGet
2. 改造 NetworkMonitorViewModel（新增 Series 属性，保留旧 Slices 属性过渡）
3. 替换 NetworkMonitorPage.xaml 中 PieChart + BarChart 的 XAML
4. 删除 PieChartControl.cs / BarChartControl.cs / PieSlice.cs（确认无引用）
5. Grep 确认 TrendChartControl 使用情况，按需删除或替换
6. 编译 + 运行验证
