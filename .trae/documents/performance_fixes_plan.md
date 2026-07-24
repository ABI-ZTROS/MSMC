# 三大问题紧急修复计划

> 日期：2026-07-25
> 范围：动画性能优化 + 配置编辑器保存修复 + 端口桥接重写

---

## 📋 问题总览

| 编号 | 问题 | 严重程度 | 修复策略 |
|------|------|----------|----------|
| P1 | 动画严重卡顿（<30fps） | 🔴 严重 | 性能优化 + 缓存 + 硬件加速 |
| P2 | 配置编辑器保存按钮失效/文件占用无弹窗 | 🟡 中等 | 增强文件占用检测 + 三重提示 |
| P3 | 端口桥接无法正常工作 | 🔴 严重 | 彻底重写，netsh 内核态为主 |

---

## 🔴 P1：动画严重卡顿修复

### 问题根因分析
基于代码审查，卡顿原因按贡献度排序：

1. **GaugeRingControl 每帧全量重绘** — `DoubleAnimation` 驱动 `DisplayValue`，每帧触发 `OnRender`，在 `OnRender` 中重建 `StreamGeometry`，这是最大的性能杀手
2. **大量透明图层合成开销** — 卡片阴影、渐变背景、模糊效果导致 GPU 合成压力大
3. **LiveCharts2 全量刷新** — 每次数据更新都触发全图重绘
4. **页面入场动画重叠** — 4个页面每次切换都播放错落入场动画
5. **网络监控1秒全量刷新** — 端口列表每秒全量替换，引发布局重算

### 修复方案（不砍效果，只优化性能）

#### 1.1 GaugeRingControl 性能重构
**文件**：`src/McServerGuard/Views/Controls/GaugeRingControl.cs`

- 改用 `CompositionTarget.Rendering` 手动帧动画，替代 `DoubleAnimation`
- 预计算并缓存进度弧几何（`StreamGeometry`），值变化时只重建弧段而非整圆
- 背景环、刻度、文本全部使用 `BitmapCache` 缓存为静态纹理
- 动画期间降低 `BitmapScalingMode` 为 `LowQuality`，动画结束恢复
- 动画帧率限制在 60fps（`CompositionTarget` 可能跑到显示器刷新率）

#### 1.2 全局硬件加速与渲染优化
**文件**：`src/McServerGuard/App.xaml.cs`

- 启动时设置 `RenderOptions.ProcessRenderMode = RenderMode.Default`（强制硬件加速）
- 全局设置 `RenderOptions.BitmapScalingMode = BitmapScalingMode.LowQuality`（动画期间）
- 全局设置 `RenderOptions.EdgeMode = EdgeMode.Unspecified`（保留抗锯齿但优化路径）
- 禁用 `BitmapEffect`（已废弃，用 `Effect` 替代，如有）

#### 1.3 静态内容 BitmapCache 缓存
**文件**：`src/McServerGuard/Views/` 各页面 XAML

- 所有卡片容器（Border 包裹的 CardBorderStyle）添加 `CacheMode="BitmapCache"`
- 图标、Logo 等静态内容添加缓存
- 侧边栏导航项静态部分缓存
- 注意：动态内容（文本变化、数据绑定）不缓存，只缓存父容器的静态背景/边框

#### 1.4 LiveCharts2 性能调优
**文件**：
- `src/McServerGuard/ViewModels/SystemMonitorViewModel.cs`
- `src/McServerGuard/Views/SystemMonitorPage.xaml`

- 历史数据点从全部保留改为最多保留 300 个点（5分钟 × 每秒1个 = 300，当前2秒刷新 = 150个）
- 启用 `LineSeries` 的 `GeometrySize` 设为 0（去掉数据点标记，只画线）
- 设置 `ChartUpdateThrottling` 或等效机制，限制图表刷新频率
- 折线使用 `Fill = null`，只画线不填充，减少绘制量

#### 1.5 网络监控刷新优化
**文件**：`src/McServerGuard/ViewModels/NetworkMonitorViewModel.cs`

- 端口列表刷新间隔从 1 秒改为 2 秒
- 增量更新：只更新变化的项，不全量替换 `ObservableCollection`
- 使用 `ObservableCollection` 的批量更新机制，避免每次 Add 都触发 `CollectionChanged`

#### 1.6 页面入场动画优化
**文件**：`src/McServerGuard/Views/Helpers/AnimationHelper.cs`

- 错落入场动画的元素数量限制：只对可见区域内的元素播放动画
- 动画时长从 500ms 缩短到 300ms
- 使用 `Linear` 缓动函数替代 `CubicEase`（计算量更小）
- 添加页面切换时的动画取消机制：切换页面时停止上一页未完成的动画

---

## 🟡 P2：配置编辑器保存修复

### 问题根因分析
1. 保存按钮 `CanSaveConfig` 可能因为 `HasUnsavedChanges` 计算逻辑问题导致一直为 false
2. 文件占用检测使用 `FileShare.Read` 方式，检测的是"能否读取"而非"能否写入"
3. 只有状态文字提示，没有模态弹窗，用户可能注意不到

### 修复方案

#### 2.1 修复保存按钮 CanExecute 逻辑
**文件**：`src/McServerGuard/ViewModels/ConfigEditorViewModel.cs`

- 审查 `CanSaveConfig` 的计算逻辑，确保 `HasUnsavedChanges` 正确更新
- 确保每次内容变更时都触发 `SaveCommand.NotifyCanExecuteChanged()`
- 可能是因为 `_currentFilePath` 为空导致——需要检查初始化流程

#### 2.2 增强文件占用检测
**文件**：`src/McServerGuard/ViewModels/ConfigEditorViewModel.cs`

- 将检测方式从 `FileShare.Read` 改为 `FileShare.None` 独占打开测试
- 测试方式：尝试以 `FileAccess.Write` + `FileShare.None` 打开文件
- 使用 `HResult` 判断具体错误类型：
  - `0x80070020` (ERROR_SHARING_VIOLATION)：文件被其他进程占用
  - `0x80070021` (ERROR_LOCK_VIOLATION)：文件被锁定
- 先检查文件是否存在，不存在直接通过

#### 2.3 三重提示机制
**文件**：`src/McServerGuard/ViewModels/ConfigEditorViewModel.cs`

保存失败时同时触发：
1. **MessageBox 模态弹窗** — 明确告诉用户文件被占用，无法保存
2. **Toast 通知** — 右下角弹出错误通知
3. **状态栏文字变红** — 页面底部状态文字显示红色错误信息

弹窗内容示例：
> 标题：保存失败
> 内容：配置文件正被其他程序占用（可能是服务器正在运行），无法保存。
> 请关闭服务器后再试，或另存为其他文件。
> 按钮：确定

---

## 🔴 P3：端口桥接彻底重写

### 问题根因分析
当前实现的问题：
1. **策略顺序错误**：先用 TcpForwarder（用户态），失败才用 netsh。但 TcpForwarder 有天然缺陷：软件关闭就失效、性能差、外部访问可能受限
2. **监听地址可能不对**：如果监听 127.0.0.1，外部网络访问不到
3. **缺少防火墙规则**：netsh portproxy 只管转发，不管防火墙，外部连接被防火墙拦截
4. **缺少权限检测**：netsh 需要管理员权限，没有 UAC 提权机制
5. **缺少 IP Helper 服务检测**：portproxy 依赖 iphlpsvc 服务
6. **缺少连通性验证**：添加后不知道到底能不能用

### 修复方案：netsh 内核态 + 完整诊断

#### 3.1 策略反转：netsh 为主，TcpForwarder 降级
**文件**：`src/McServerGuard/Services/Network/CompositePortBridgeService.cs`

- 主策略：`netsh interface portproxy`（内核态、高性能、持久化、重启有效）
- 降级策略：TcpForwarder（用户态，仅在没有管理员权限时使用）
- 默认监听地址：`0.0.0.0`（所有网卡，支持外部访问）

#### 3.2 管理员权限检测与 UAC 提权
**文件**：
- 新建 `src/McServerGuard/Services/Network/ElevationHelper.cs`
- 修改 `src/McServerGuard/Services/Network/NetshPortBridgeService.cs`

- 添加权限检测：`WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`
- 需要提权时，启动新的进程以管理员身份执行 netsh 命令
- 方式：`Process.Start(new ProcessStartInfo { Verb = "runas", ... })`
- 提权后通过 IPC 或文件共享返回结果（简化版：直接启动隐藏进程执行命令，等待退出）

#### 3.3 自动防火墙规则管理
**文件**：`src/McServerGuard/Services/Network/NetshPortBridgeService.cs`

- 添加桥接时自动添加入站防火墙规则
- 使用 `netsh advfirewall firewall add rule` 命令
- 规则命名：`MSMC_PortBridge_{ListenPort}`
- 删除桥接时同步删除防火墙规则
- 提供防火墙状态检测：检测是否被第三方防火墙拦截

#### 3.4 IP Helper 服务检测
**文件**：`src/McServerGuard/Services/Network/NetshPortBridgeService.cs`

- 添加规则前检测 `iphlpsvc` 服务状态
- 如果服务未启动，尝试启动（需要管理员权限）
- 无法启动时给出明确错误提示

#### 3.5 连通性验证功能
**文件**：`src/McServerGuard/Services/Network/NetshPortBridgeService.cs`

- 添加桥接后，自动进行本地回环连通性测试
- 方式：尝试 `TcpClient.Connect("127.0.0.1", listenPort)`，看能不能连上
- 连接成功说明端口在监听（不验证转发是否通，因为目标可能没开）
- 测试失败时返回详细的诊断信息

#### 3.6 完善的错误诊断体系
**文件**：`src/McServerGuard/Services/Network/PortBridgeResult.cs`（新建）

- 定义 `PortBridgeResult` 结果类，包含：
  - `Success`：是否成功
  - `ErrorMessage`：错误消息
  - `ErrorCode`：错误码（枚举）
  - `Suggestion`：修复建议
- 错误码枚举：
  - `Success` = 0
  - `PermissionDenied` = 1（需要管理员权限）
  - `PortAlreadyInUse` = 2（监听端口被占用）
  - `IPHelperServiceStopped` = 3（IP Helper服务未启动）
  - `FirewallBlocked` = 4（防火墙拦截）
  - `TargetUnreachable` = 5（目标地址不可达）
  - `UnknownError` = 99

#### 3.7 UI 层改进
**文件**：
- `src/McServerGuard/ViewModels/NetworkMonitorViewModel.cs`
- `src/McServerGuard/Views/NetworkMonitorPage.xaml`

- 添加桥接结果的详细展示（成功/失败原因）
- 失败时显示修复建议
- 添加「以管理员身份重试」按钮
- 桥接列表显示更详细的信息：监听地址、目标地址、状态（运行中/已停止）
- 添加「测试连通性」按钮

---

## 📁 文件变更清单

### P1 动画性能（8个文件）
| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `GaugeRingControl.cs` | 修改 | 重构渲染逻辑，添加缓存和手动帧动画 |
| `App.xaml.cs` | 修改 | 添加全局渲染优化配置 |
| `MainWindow.xaml` | 修改 | 卡片/静态内容添加 BitmapCache |
| `SettingsPage.xaml` | 修改 | 卡片添加缓存 |
| `SystemMonitorPage.xaml` | 修改 | 图表性能调优 |
| `SystemMonitorViewModel.cs` | 修改 | 数据点数量限制 |
| `NetworkMonitorViewModel.cs` | 修改 | 刷新间隔改为2秒，增量更新 |
| `AnimationHelper.cs` | 修改 | 动画优化 |

### P2 配置保存（2个文件）
| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `ConfigEditorViewModel.cs` | 修改 | 修复 CanSave、增强占用检测、添加弹窗 |
| `ConfigEditorPage.xaml` | 修改 | （如有需要）状态文字样式调整 |

### P3 端口桥接（7个文件）
| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `CompositePortBridgeService.cs` | 修改 | 策略反转：netsh 为主 |
| `NetshPortBridgeService.cs` | 修改 | 增强：防火墙、服务检测、连通性验证 |
| `TcpForwarderService.cs` | 修改 | 降级为备选方案 |
| `IPortBridgeService.cs` | 修改 | 接口调整 |
| `ElevationHelper.cs` | 新增 | UAC 提权辅助类 |
| `PortBridgeResult.cs` | 新增 | 结果模型 + 错误码 |
| `NetworkMonitorViewModel.cs` | 修改 | UI 层改进 |
| `NetworkMonitorPage.xaml` | 修改 | UI 改进 |

---

## ⚠️ 风险与注意事项

1. **BitmapCache 副作用**：缓存的元素在 DPI 变化时可能模糊，需处理 `DpiChanged` 事件
2. **管理员提权**：UAC 弹窗可能打断用户体验，但这是 netsh 的必要条件
3. **防火墙规则**：添加防火墙规则可能触发安全软件告警
4. **netsh 规则持久化**：卸载软件时需要清理，否则规则会残留
5. **性能优化的回归风险**：修改渲染逻辑可能引入视觉瑕疵，需要充分测试

---

## ✅ 验证方式

### P1 动画性能
- 使用 WPF Performance Suite 或类似工具监控 FPS
- 切换页面、观察仪表盘动画、查看图表刷新
- 主观体验：拖动窗口、切换页面是否流畅

### P2 配置保存
- 用记事本打开配置文件并保持打开状态，尝试在 MSMC 中保存 → 应弹窗提示
- 关闭记事本后再保存 → 应保存成功
- 保存按钮在未修改时禁用，修改后启用

### P3 端口桥接
- 非管理员身份添加桥接 → 应提示需要管理员权限
- 管理员身份添加桥接 → 应成功，并自动添加防火墙规则
- `netsh interface portproxy show all` 验证规则存在
- 从另一台机器测试连接 → 应能连通
- 删除桥接 → 规则和防火墙规则都应被清理
