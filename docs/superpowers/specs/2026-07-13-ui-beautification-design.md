# MSMC UI 美化设计文档

> 日期: 2026-07-13
> 状态: 已批准

## 1. 概述

对 MSMC (Minecraft Server Management Client) 的 WPF UI 进行深度美化改造，从"能用的骨架"升级为"精美的成品"。

三大改造方向：
- Material Design 深度定制（颜色系统/动画/过渡效果）
- 自绘 WPF 图表（圆环仪表盘 + 折线趋势图）
- 侧边抽屉导航（DrawerHost + 汉堡菜单）

同时修复当前 XAML 中大量缺失的 Converter 和绑定错误。

## 2. Material Design 深度定制

### 2.1 自定义颜色与样式

- 主色: DeepPurple，辅色: Lime（保持不变）
- 新增 `AppResources.xaml` 合并字典，定义自定义 Brush：
  - `AccentGradientBrush`：DeepPurple → Purple 渐变，用于标题栏和重要按钮
  - `CardBackgroundBrush`：半透明深灰 (#1E1E2E)，统一卡片背景
  - `GaugeGreenBrush` / `GaugeYellowBrush` / `GaugeRedBrush`：仪表盘渐变色
- 卡片统一圆角 12px，阴影加深
- 导航项选中态：背景半透明紫色高亮 + 左侧 3px 竖线指示器

### 2.2 动画与过渡

- 页面切换：`TransitioningContentControl` 淡入滑动过渡
- 卡片加载：`MaterialDesign:TransitionAssist` 入场动画
- Snackbar 通知：替代底部状态栏文字，检测完成/保存成功等操作通过 Snackbar 弹出

### 2.3 窗口增强

- 标题栏集成 DrawerHost 汉堡菜单按钮
- 底部状态栏绑定 `StatusMessage` + 实时时钟

## 3. 自绘 WPF 图表

### 3.1 GaugeRingControl（圆环仪表盘）

- 位置: `src/McServerGuard/Views/Controls/GaugeRingControl.cs`
- 技术: WPF `DrawingVisual` + 自定义 `FrameworkElement`
- 外观: 圆弧进度环，渐变色弧线（绿→黄→红），中心大号数字
- 依赖属性: `Value` (0-100), `Label` (string), `Unit` (string)
- 动画: 值变化时弧线平滑过渡

### 3.2 TrendChartControl（折线趋势图）

- 位置: `src/McServerGuard/Views/Controls/TrendChartControl.cs`
- 技术: WPF `DrawingVisual` + 自定义 `FrameworkElement`
- 外观: 折线 + 面积填充，浅色网格线，X轴时间标签
- 依赖属性: `DataPoints` (List<double>), `LineColor` (Brush), `FillColor` (Brush)
- 最多显示 120 个数据点，超出自动滚动

## 4. 侧边抽屉导航

### 4.1 DrawerHost

- 替换当前固定 70px `Border + StackPanel` 导航栏
- 使用 `materialDesign:DrawerHost` 包裹整个内容区
- `LeftDrawer` 包含导航项列表

### 4.2 汉堡菜单

- 标题栏左侧放置 `ToggleButton` 绑定 `DrawerHost.OpenDrawerCommand`
- 图标: `PackIcon Kind="Menu"` / `Kind="ArrowLeft"`（展开/收起切换）
- 点击抽屉外部区域自动关闭

### 4.3 导航项

- 4 个导航项：检测 / 配置 / 监控 / AI
- 每项: PackIcon + TextBlock
- 选中态: 背景高亮 + 左侧指示条
- 悬停: 微光效果

## 5. Converter 修复清单

当前 XAML 引用了大量不存在的 Converter，需全部新建或修复：

| XAML 中引用的 Key | 实际 Converter 类 | 说明 |
|---|---|---|
| `IndexToBool0/1/2/3` | `IndexToBoolConverter` | 带 ConverterParameter 的通用索引→bool 转换 |
| `BoolStringConverter` | `BoolStringConverter` | bool ↔ "true"/"false" 互转 |
| `TypeToVis` | `ValueTypeToVisibilityConverter` | 根据 ValueType 字符串控制 Visibility |
| `BoolToVis` | `BoolToVisibilityConverter` | bool → Visibility（已存在，Key 已匹配） |
| `NullToVis` | `NullToVisibilityConverter` | null → Collapsed（已存在，Key 匹配需检查） |
| `InvertBool` | `InvertBoolConverter` | bool 取反（已存在，需确认 XAML Key） |
| `BoolToIcon` | `BoolToPackIconConverter` | true→CheckCircle, false→AlertCircle |
| `BoolToText` | `BoolToTextConverter` | true→"是", false→"否" |

## 6. 绑定修复清单

| 位置 | 问题 | 修复 |
|---|---|---|
| MainWindow 状态栏 | 绑定 `StatusText` 但 VM 属性是 `StatusMessage` | XAML 改为 `StatusMessage` |
| MainWindow 顶部按钮 | 绑定 `StartDetectionCommand` 但 VM 命令是 `DetectServersCommand` | XAML 改为 `DetectServersCommand` |
| AIGuardPage 输出框 | 绑定 `AiOutput` 但 VM 属性是 `AnalysisOutput` | XAML 改为 `AnalysisOutput` |
| AIGuardPage 分析按钮 | 缺少 Command 绑定（日志分析没有按钮） | 添加分析按钮绑定 `AnalyzeLogCommand` |
| AIGuardPage 优化按钮 | 绑定 `GenerateOptimizationCommand` 但 VM 命令是 `RequestConfigOptimizationCommand` | XAML 修正命令名 |
| AIGuardPage 崩溃预测 | 绑定 `PredictCrashCommand` 但 VM 没有此命令 | 需要在 VM 添加或移除按钮 |
| AIGuardPage 建议 | 绑定 `OptimizationSuggestions` 但 VM 属性是 `ConfigSuggestions` | XAML 修正属性名 |
| AIGuardPage HasSuggestions | 绑定 `HasSuggestions` 但 VM 没有此属性 | 需要在 VM 添加计算属性 |
| AIGuardPage ModelStatusText | VM 没有此属性 | 需要在 VM 添加 |
| AIGuardPage ModelInfoText | VM 没有此属性 | 需要在 VM 添加 |
| ServerDetectionPage NullToVis | 引用 Key 不匹配（用了 NullToVis 而非 NullToVisibilityConverter） | XAML 修正 Key |
| ServerDetectionPage InvertBool | 引用 Key 不匹配（用了 InvertBool 而非 InvertBoolConverter） | XAML 修正 Key |
| ServerDetectionPage DetectionResult.IsDetected | 需确认 DetectionResult 模型有此属性 | 检查并添加 |
| SystemMonitorPage MemoryInfoText | VM 没有此属性 | 需要在 VM 添加 |
| SystemMonitorPage DiskInfoText | VM 没有此属性 | 需要在 VM 添加 |
| 状态栏右侧时间 | 没有绑定 | 添加实时时钟绑定 |

## 7. ViewModel 补充属性

### MainViewModel
- `SnackbarMessageQueue` 属性（Snackbar 消息队列）

### AIGuardViewModel
- `ModelStatusText`: 根据 IsModelLoaded 返回状态描述
- `ModelInfoText`: 模型详细信息
- `HasSuggestions`: ConfigSuggestions.Count > 0
- `GenerateHealthReportCommand` → 已存在（属性名匹配）
- 添加 `PredictCrashCommand`（如果需要）

### SystemMonitorViewModel
- `MemoryInfoText`: 格式化的内存详情文本
- `DiskInfoText`: 格式化的磁盘详情文本

### ServerDetectionViewModel
- 确认 `DetectionResult` 有 `IsDetected` 属性

## 8. 实施任务分解

1. **Task 1: 修复 Converter** — 新建所有缺失的 Converter，修复 Key 引用
2. **Task 2: 修复 ViewModel 绑定** — 补充缺失属性，修正属性名/命令名
3. **Task 3: 自绘图表控件** — GaugeRingControl + TrendChartControl
4. **Task 4: 侧边抽屉导航** — DrawerHost + 汉堡菜单 + 导航项重构
5. **Task 5: Material Design 深度定制** — 自定义资源字典 + 过渡动画 + Snackbar
6. **Task 6: 页面细节打磨** — 各页面布局优化、动画、空状态美化
7. **Task 7: 编译验证 + CI** — 确保编译通过，推送并等待 Action 结果
