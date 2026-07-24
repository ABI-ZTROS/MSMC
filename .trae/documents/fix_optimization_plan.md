# 修复与优化项目统计计划

> 基于 4 路并行审查（代码质量 / 错误处理与资源 / UI与XAML / 架构与性能），共发现 64 项问题。
> **所有决策已与用户确认，全部修复，采用最彻底方案。**

---

## ✅ 用户确认决策汇总

| 决策项 | 选择 |
|--------|------|
| 整体范围 | **全部 6 类全修**（64 项） |
| 架构深度 | **完整 DI 重构**（子 VM 走 DI，ServerImporterService 拆分） |
| 性能重构 | **完整异步重构**（P1 WMI 批量查询+异步，P5 环形缓冲数组） |
| 资源清理 | **全部 4 个 VM 实现 IDisposable + 缓存定期清理** |
| B2 开关 | 绑定实现（新增 EnableWindowsNotifications 属性） |
| B4 虚拟化 | 独立 ScrollViewer（CanContentScroll=True） |
| U4 嵌套虚拟化 | 改 TreeView（原生层级虚拟化） |
| A6 动画统一 | Helper 静态方法（AnimationHelper.PlayPageEntrance） |
| P5 历史集合 | 环形缓冲数组（T[] + head/tail 指针） |
| U5 硬编码颜色 | 含色板按钮（全部抽取为资源键） |
| U9 无障碍 | 补充 AutomationProperties |
| C6 魔法数字 | 全部提取常量 |
| U6 固定尺寸 | 全部改（MinWidth + * 比例） |
| U8 动画问题 | 全部修（FillBehavior + Expander 反向 + GaugeRing） |
| C1 路径服务 | 抽取 IAppPaths |
| C4 JVM 验证 | 加正则验证 |
| C5 端口验证 | 加 1-65535 范围验证 |
| A8 大方法拆分 | 拆分（3 个 90+ 行方法） |

---

## 📊 总体统计

| 类别 | 高 | 中 | 低 | 小计 |
|------|---|---|---|-----|
| 🔴 功能性 Bug | 5 | 2 | 0 | 7 |
| 🟠 性能问题 | 4 | 5 | 2 | 11 |
| 🟡 资源/内存泄漏 | 1 | 8 | 4 | 13 |
| 🔵 架构/代码质量 | 2 | 7 | 3 | 12 |
| 🟢 UI/XAML | 2 | 6 | 4 | 12 |
| ⚪ 配置/健壮性 | 1 | 4 | 4 | 9 |
| **合计** | **15** | **32** | **17** | **64** |

---

## 🔴 高优先级 — 功能性 Bug（必修）

### B1. NetworkMonitorPage 错误 Dispose 共享 ViewModel 导致功能失效
- **文件**：`Views/NetworkMonitorPage.xaml.cs:33-41`
- **问题**：`OnUnloaded` 中调用 `(_viewModel as IDisposable)?.Dispose()`，但 `_viewModel` 是 `MainViewModel.NetworkPage` 的共享引用。Tab 切换触发 Unloaded 后 Dispose 计时器，重新加载时计时器不会重启，**网络监控功能在切换 Tab 后永久失效**。
- **修法**：移除 OnUnloaded 中的 Dispose 调用，ViewModel 生命周期由 DI 容器管理。
- **决策**：✅ 必修（功能性 bug）

### B2. SettingsPage 装饰性 ToggleButton 无绑定，用户操作无效
- **文件**：`Views/SettingsPage.xaml:535`
- **问题**："Windows 通知中心"开关 `IsChecked="True"` 硬编码，未绑定任何属性。用户切换无效果，状态不持久化。
- **修法**：绑定到 ViewModel 的通知开关属性，或暂时移除该控件以免误导。
- **决策**：需确认 — 绑定实现 vs 暂时移除？

### B3. IndependentLoadingIcon Storyboard 泄漏
- **文件**：`Views/Controls/IndependentLoadingIcon.xaml.cs:32-49`
- **问题**：`OnLoaded` 中 `_storyboard = new Storyboard()` 直接覆盖字段，未先停止前一个。Loaded 可能多次触发（Tab 切换），导致前一个 Storyboard 未停止即被覆盖，动画持续后台运行。
- **修法**：OnLoaded 开头先 `_storyboard?.Stop(); _storyboard = null;` 再创建新的。
- **决策**：✅ 必修（资源泄漏）

### B4. ServerDetectionPage 嵌套虚拟化失效，列表多时卡顿
- **文件**：`Views/ServerDetectionPage.xaml:270-478`
- **问题**：外层 ScrollViewer + StackPanel 给子元素无限高度，内层 ItemsControl 的虚拟化彻底失效。50+ 服务器时启动卡顿明显。
- **修法**：给内层 ItemsControl 套独立 ScrollViewer（CanContentScroll=True），或改用 VirtualizingStackPanel 作为根面板。
- **决策**：需确认 — 修法选择？

### B5. 日志文件路径使用相对路径，可能丢失日志
- **文件**：`App.xaml.cs:58`
- **问题**：`$"logs/mcserverguard-{...}.log"` 是相对路径，依赖 cwd。从快捷方式启动可能写到错误位置或失败。
- **修法**：改为 `Path.Combine(AppContext.BaseDirectory, "logs", ...)`，并 `Directory.CreateDirectory`。
- **决策**：✅ 必修

### B6. AppConfigService 单例非线程安全
- **文件**：`Services/AppConfigService.cs:28,45,134-202`
- **问题**：Singleton 服务，`Config.KnownServers` 列表读写无锁，并发访问会竞态损坏。
- **修法**：用 `lock` 保护所有读写，或改用不可变记录 + `Interlocked.Exchange`。
- **决策**：✅ 必修

### B7. ServerDetector._detectionCache 字典无锁并发访问
- **文件**：`Services/ServerDetection/ServerDetector.cs:536,148,163,176`
- **问题**：`DetectAllAsync` 被后台自动检测和 UI 命令两路并发调用，`Dictionary` 读写竞态。
- **修法**：改用 `ConcurrentDictionary` 或加 `lock`。
- **决策**：✅ 必修

---

## 🟠 高优先级 — 性能问题

### P1. ProcessScanner 同步 WMI 在 3 秒循环中（自动检测卡顿主因）
- **文件**：`Services/ServerDetection/ProcessScanner.cs:56-58,215-216,270-271`
- **问题**：每 3 秒同步执行 `Process.GetProcessesByName` + 逐进程 WMI 查询 + 递归父进程链（每层 1 次 WMI）。N 个 Java 进程最坏 N×5 次 WMI 查询，阻塞线程池。
- **修法**：用一次 WMI 批量查询替代逐进程查询；父进程链改为内存构建进程树；提供异步版本。
- **决策**：需确认 — 重构幅度？

### P2. ServerImporterService.ImportServer 在 UI 线程同步执行 WMI + 文件扫描
- **文件**：`Services/ServerDetection/ServerImporterService.cs:91-179,241-265`
- **问题**：用户点"导入"后同步执行 WMI 查询 + 配置扫描，UI 明显卡顿。
- **修法**：改为 `ImportServerAsync`，`Task.Run` 包裹 WMI 和扫描。
- **决策**：✅ 必修

### P3. AppConfigService.Load/Save 同步文件 I/O 在 UI 线程
- **文件**：`Services/AppConfigService.cs:62,117`
- **问题**：`File.ReadAllText`/`File.WriteAllText` 同步调用，启动时和每次添加/删除已知服务器都阻塞 UI。
- **修法**：改为 `ReadAllTextAsync`/`WriteAllTextAsync`，Save 改为 `async Task`。
- **决策**：✅ 必修

### P4. NetworkService 一次刷新 3 次枚举端口（网络监控卡顿主因）
- **文件**：`Services/Network/NetworkService.cs:191-207` + `ViewModels/NetworkMonitorViewModel.cs:381-388`
- **问题**：`RefreshPorts` 调用 `GetAllListeningPorts` + `GetUsedPercentage`（内部再调）+ `GetPortDistribution`（内部再调），同一次刷新枚举 3 次。每 5 秒一次。
- **修法**：增加一次性返回 `(ports, usedPct, distribution)` 的方法，或复用第一次的 ports 列表。
- **决策**：✅ 必修

### P5. SystemMonitorViewModel 每 2 秒重建整个历史 List（O(n) 复制 + 移除）
- **文件**：`ViewModels/SystemMonitorViewModel.cs:267-291`
- **问题**：每次采样 `new List(history) { metrics }` + `RemoveAt(0)`（O(n)），触发 5 个 OnPropertyChanged。
- **修法**：用 `Queue<SystemMetrics>` 或环形缓冲替代 List。
- **决策**：需确认 — 修法选择？

### P6. NetworkTrafficService.Save 在锁内同步写文件
- **文件**：`Services/Network/NetworkTrafficService.cs:127-187,320-345`
- **问题**：`Sample()` 在 `lock` 内调用 `Save()`，`Save` 内 `File.WriteAllText` 同步 I/O。每 60 秒持锁阻塞其他线程。
- **修法**：锁内只更新内存，快照拷贝出锁外异步写盘。
- **决策**：✅ 必修

---

## 🟡 资源/内存泄漏（中优先级）

### R1. 多个 ViewModel 未实现 IDisposable
- **文件**：
  - `MainViewModel.cs:91-116`（Lambda 事件订阅 + clockTimer 无法停止）
  - `ServerDetectionViewModel.cs:68,89`（Lambda 订阅 + DetectionCompleted 订阅）
  - `ConfigEditorViewModel.cs:90,96,116-118`（_groupUpdateTimer + _loadCts + Lambda 订阅）
  - `SystemMonitorViewModel.cs:46,296-302`（_monitoringCts）
- **修法**：4 个 ViewModel 实现 IDisposable，Lambda 改命名方法，计时器保存为字段。
- **决策**：需确认 — 范围（全部 4 个 vs 仅 MainViewModel）？

### R2. Process 对象未 Dispose（句柄泄漏）
- **文件**：`Services/ServerDetection/ServerManagerService.cs:237-345,571-631`
- **问题**：`Process.Start` 返回值和 `Process.GetProcessById` 返回值多处未 Dispose，泄漏非托管句柄。
- **修法**：使用 `using` 语句包裹。
- **决策**：✅ 必修

### R3. WMI ManagementObject 遍历时未释放（COM 对象泄漏）
- **文件**：`ServerManagerService.cs:643-705`、`SystemMonitor.cs:296-344`
- **问题**：`foreach (var obj in collection)` 中 `obj` 是 `ManagementObject`（IDisposable），未 Dispose。
- **修法**：foreach 内 `using var obj = ...` 或显式 Dispose。
- **决策**：✅ 必修

### R4. TcpForwarderService 未实现 IDisposable
- **文件**：`Services/Network/TcpForwarderService.cs:26-277`
- **问题**：持有 `_sessions`（含 TcpListener、CTS、ActiveClients），退出时不停止监听器、不断开连接。
- **修法**：实现 IDisposable，Dispose 中遍历 _sessions 取消 CTS、停止 Listener、关闭 Clients。
- **决策**：✅ 必修

### R5. UserAgreementWindow 计时器与事件订阅未在 Closed 清理
- **文件**：`Views/UserAgreementWindow.xaml.cs:73-80`
- **问题**：`_countdownTimer`、`_shakeTimer` 无 Closed 事件处理；Loaded/Activated/Deactivated 订阅未取消。
- **修法**：添加 Closed 事件处理，停止计时器并取消订阅。
- **决策**：✅ 必修

### R6. MemoryOptimizerService GC 通知后台线程无法退出
- **文件**：`Services/MemoryOptimizerService.cs:91-92,270-295,311-315`
- **问题**：`Dispose` 不调用 `GC.CancelFullGCNotification`，`MonitorFullGCNotification` 的 `while(true)` 无法退出。
- **修法**：Dispose 和 OnApplicationExit 中调用 `GC.CancelFullGCNotification()`。
- **决策**：✅ 必修

### R7. SystemMonitor Timer Dispose 竞态
- **文件**：`Services/SystemMonitoring/SystemMonitor.cs:204-253,263-284`
- **问题**：Timer 回调中 Dispose 自身，`Timer.Dispose()` 不等回调完成，可能访问已释放资源。
- **修法**：使用 `Timer.DisposeAsync()` 或 `Timer.Dispose(WaitHandle)`。
- **决策**：✅ 必修

### R8. ServerDetector 缓存无主动清理（内存持续增长）
- **文件**：`Services/ServerDetection/ServerDetector.cs:536,547`
- **问题**：`_detectionCache` 和 `_portScanCache` 只在命中时检查 TTL，过期条目不主动移除。
- **修法**：添加定期清理，或用 `MemoryCache`。
- **决策**：需确认 — 修法选择？

---

## 🔵 架构 / 代码质量（中优先级）

### A1. MainViewModel God Object — 注入 12 个服务但自身只用 3 个
- **文件**：`ViewModels/MainViewModel.cs:36-89`
- **问题**：8 个服务字段从未在 MainViewModel 自身使用，仅转发给子 ViewModel。且直接 `new` 实例化子 ViewModel，绕过 DI。
- **修法**：子 ViewModel 通过 DI 容器直接注入；MainViewModel 构造函数只注入 5 个子 ViewModel。
- **决策**：需确认 — 重构幅度（完整 DI vs 仅删冗余字段）？

### A2. View 代码隐藏普遍使用服务定位器反模式（7 处）
- **文件**：`MainWindow.xaml.cs:46-47` 等 7 处 `App.Services.GetRequiredService<T>()`
- **问题**：隐藏依赖、阻碍测试、View 与全局容器耦合。
- **修法**：通过 ViewModel 属性透传主题配置；或接受现状（WPF 常见模式）。
- **决策**：需确认 — 是否重构？

### A3. ServerImporterService 绕 DI new ConfigFileScanner + 单类 6+ 职责
- **文件**：`Services/ServerDetection/ServerImporterService.cs:73-332,156-157`
- **问题**：手工 new ConfigFileScanner（DI 已注册），调同步 ScanAll；单类承担 JAR 检测/类型识别/工作目录/进程查找/WMI/内存解析/配置扫描。
- **修法**：注入 ConfigFileScanner 用 ScanAllAsync；拆分为小服务。
- **决策**：需确认 — 重构幅度？

### A4. ActiveOperation 保存/恢复模式重复 6 次
- **文件**：`ViewModels/ServerDetectionViewModel.cs:796,885,1015,1195,1290,1336`
- **修法**：提取 `IDisposable BeginOperation(ServerOperation op)`，`using var scope = BeginOperation(...)`。
- **决策**：✅ 建议修（消除重复）

### A5. NetshPortBridgeService Process.Start 模式重复 5 次
- **文件**：`Services/Network/NetshPortBridgeService.cs:64-382`
- **修法**：提取 `RunNetsh(string args, int timeout, out string output, out int exitCode)`。
- **决策**：✅ 建议修

### A6. 页面 OnLoaded 入场动画模式重复 4 次
- **文件**：4 个 Page 的 OnLoaded
- **修法**：提取基类 `AnimatedPageBase` 或 `AnimationHelper.PlayPageEntrance()`。
- **决策**：需确认 — 修法选择？

### A7. 死代码：SystemMonitorViewModel 4 个未使用属性
- **文件**：`ViewModels/SystemMonitorViewModel.cs:153-163,285-288`
- **问题**：`CpuHistoryText`/`MemoryHistoryText`/`CpuDataPoints`/`MemoryDataPoints` 在 XAML 中零引用，是迁移遗留物，每次刷新还触发 Select/ToList 重建。
- **修法**：删除属性定义和对应 OnPropertyChanged。
- **决策**：✅ 必修（含性能收益）

### A8. 大型方法 ServerDetector.DetectAllAsync (100行) / BuildServerInstanceAsync (92行) / ProcessScanner.ScanServerProcesses (114行)
- **修法**：拆分为子方法。
- **决策**：需确认 — 是否拆分？

---

## 🟢 UI / XAML 问题

### U1. ColorSwatchStyle 在 AppResources 与 SettingsPage 冲突定义
- **文件**：`Themes/AppResources.xaml:510-616` vs `Views/SettingsPage.xaml:41-98`
- **问题**：同名样式两处定义，尺寸/模板差异显著，本地遮蔽全局。
- **修法**：统一为一份（放 AppResources），SettingsPage 用 BasedOn 覆盖尺寸。
- **决策**：✅ 必修

### U2. SectionTitleStyle 四处重复定义
- **文件**：AppResources + SettingsPage + UserAgreementWindow + ServerDetectionPage
- **修法**：AppResources 保留基础，各页面 BasedOn 派生不同 key。
- **决策**：✅ 建议修

### U3. NetworkMonitorPage 顶部仪表盘行窄屏溢出无滚动
- **文件**：`Views/NetworkMonitorPage.xaml:19-82`
- **问题**：5 张固定宽度卡片水平排列，窄窗口裁剪无滚动。
- **修法**：改用 WrapPanel 或加 ScrollViewer。
- **决策**：✅ 必修

### U4. ConfigEditorPage 三层嵌套虚拟化
- **文件**：`Views/ConfigEditorPage.xaml:465-533`
- **修法**：内层去掉虚拟化配置，或改用 TreeView。
- **决策**：需确认 — 修法选择？

### U5. 硬编码颜色集群（SettingsPage 色板 + 图例 + DataTrigger 等）
- **文件**：多处（SettingsPage:161-453, NetworkMonitorPage:288-298, ConfigEditorPage:449-451）
- **问题**：色板按钮硬编码有一定合理性；但 DataTrigger 错误/成功色、图例色、阴影渐变应改主题资源。
- **决策**：需确认 — 范围（仅 DataTrigger/图例 vs 含色板）？

### U6. 硬编码尺寸（固定宽度窄屏溢出）
- **文件**：NetworkMonitorPage:89(400px), SettingsPage:466(400px Slider), ConfigEditorPage:139(280px)
- **修法**：改用 MinWidth + * 比例分配。
- **决策**：需确认 — 范围？

### U7. MainWindow.xaml 重复合并资源字典与转换器
- **文件**：`Views/MainWindow.xaml:35-49`
- **问题**：Window.Resources 重复声明已在 App.xaml 全局注册的内容。
- **修法**：删除 Window.Resources 整段。
- **决策**：✅ 建议修

### U8. 动画问题集群
- **MainWindow.xaml.cs:108-126**：FillBehavior 默认 HoldEnd 锁住属性
- **ConfigEditorPage.xaml:487-497**：Expander.Expanded 动画无 Collapsed 反向
- **GaugeRingControl.cs:197-204**：动画中断 Completed 不保证触发
- **决策**：需确认 — 范围？

### U9. 无障碍：全项目零 AutomationProperties
- **问题**：图标按钮、仪表盘、状态点等无 AutomationProperties.Name，屏幕阅读器不可用。
- **决策**：需确认 — 是否补充（工作量大）？

### U10. ConfigEditorPage 三个按钮内联重复样式
- **文件**：`Views/ConfigEditorPage.xaml:364-424`
- **修法**：抽取 `DisabledOpacityButtonStyle` 共享样式。
- **决策**：✅ 建议修

---

## ⚪ 配置 / 健壮性

### C1. 配置目录路径在两处重复硬编码
- **文件**：`AppConfigService.cs:33-35` + `NetworkTrafficService.cs:101-104`
- **修法**：抽取 `IAppPaths` 服务集中管理路径。
- **决策**：需确认 — 是否抽取？

### C2. AppConfigService.Save 非原子写入
- **文件**：`Services/AppConfigService.cs:108-124`
- **问题**：`File.WriteAllText` 直接写目标文件，崩溃会损坏配置。
- **修法**：采用临时文件 + File.Replace（参考 NetworkTrafficService.Save）。
- **决策**：✅ 必修

### C3. AppConfigService 反序列化未校验集合 null
- **文件**：`Services/AppConfigService.cs:63-80`
- **修法**：反序列化后 `Config.KnownServers ??= new List<KnownServer>();`。
- **决策**：✅ 必修

### C4. JVM 内存参数无验证
- **文件**：`ViewModels/ServerDetectionViewModel.cs:446-447`
- **问题**：`InitialMemory="2G"` 字符串，非法输入静默返回 0，启动 JVM 失败。
- **修法**：OnInitialMemoryChanged 中正则验证，非法时提示并禁用启动。
- **决策**：需确认 — 是否加验证？

### C5. 桥接端口无范围验证
- **文件**：`ViewModels/NetworkMonitorViewModel.cs:195-220`
- **修法**：setter 中校验 1-65535，CanExecute 检查。
- **决策**：需确认 — 是否加验证？

### C6. 魔法数字（1048576 字节转换重复 4 处 / Task.Delay 值散落 / netsh 超时值散落）
- **修法**：提取常量。
- **决策**：需确认 — 范围？

---

## ❓ 决策点（全部已确认）

| # | 决策项 | 确认结果 |
|---|--------|----------|
| 1 | 整体范围 | ✅ 全部 6 类全修 |
| 2 | B2 ToggleButton | ✅ 绑定实现 |
| 3 | B4 虚拟化 | ✅ 独立 ScrollViewer |
| 4 | P1 ProcessScanner | ✅ 完整异步重构（WMI 批量+内存进程树+异步版） |
| 5 | P5 历史集合 | ✅ 环形缓冲数组 |
| 6 | R1 ViewModel IDisposable | ✅ 全部 4 个 |
| 7 | R8 缓存清理 | ✅ 定期清理 |
| 8 | A1 MainViewModel | ✅ 完整 DI 重构 |
| 9 | A2 服务定位器 | ✅ 通过 ViewModel 属性透传 |
| 10 | A3 ServerImporterService | ✅ 完整拆分 |
| 11 | A6 页面动画 | ✅ Helper 静态方法 |
| 12 | A8 大方法拆分 | ✅ 拆分 |
| 13 | U4 嵌套虚拟化 | ✅ 改 TreeView |
| 14 | U5 硬编码颜色 | ✅ 含色板按钮全抽资源键 |
| 15 | U6 硬编码尺寸 | ✅ 全部改 MinWidth+* |
| 16 | U8 动画问题 | ✅ 全部修 |
| 17 | U9 无障碍 | ✅ 补充 AutomationProperties |
| 18 | C1 路径服务 | ✅ 抽取 IAppPaths |
| 19 | C4/C5 输入验证 | ✅ 加验证 |
| 20 | C6 魔法数字 | ✅ 全部提取常量 |

---

## 📋 最终实施顺序（6 批）

### 第一批：功能性 Bug（B1-B7）— 修复用户可感知问题
1. **B1** NetworkMonitorPage 移除 OnLoaded 中错误 Dispose
2. **B2** SettingsPage ToggleButton 绑定 EnableWindowsNotifications
3. **B3** IndependentLoadingIcon OnLoaded 先 Stop 旧 Storyboard
4. **B4** ServerDetectionPage 内层 ItemsControl 套独立 ScrollViewer
5. **B5** 日志路径改 AppContext.BaseDirectory
6. **B6** AppConfigService 加 lock 保护读写
7. **B7** ServerDetector._detectionCache 改 ConcurrentDictionary

### 第二批：资源泄漏（R1-R8）— 防止内存增长
1. **R1** 4 个 ViewModel 实现 IDisposable（MainVM/ServerDetectionVM/ConfigEditorVM/SystemMonitorVM）
2. **R2** Process 对象 using 包裹（ServerManagerService）
3. **R3** WMI ManagementObject foreach 内 using（ServerManagerService/SystemMonitor）
4. **R4** TcpForwarderService 实现 IDisposable
5. **R5** UserAgreementWindow 添加 Closed 事件清理计时器
6. **R6** MemoryOptimizerService Dispose 调用 GC.CancelFullGCNotification
7. **R7** SystemMonitor Timer 改 DisposeAsync
8. **R8** ServerDetector 缓存定期清理过期条目

### 第三批：性能（P1-P6）— 消除卡顿
1. **P1** ProcessScanner WMI 批量查询 + 内存进程树 + 异步版本
2. **P2** ServerImporterService 改 ImportServerAsync + Task.Run
3. **P3** AppConfigService 改 ReadAllTextAsync/WriteAllTextAsync
4. **P4** NetworkService 增加一次性返回 (ports, usedPct, distribution) 方法
5. **P5** SystemMonitorViewModel 改环形缓冲数组（T[] + head/tail）
6. **P6** NetworkTrafficService.Save 锁外异步写盘

### 第四批：架构/代码质量（A1-A8）— 提升可维护性
1. **A1** MainViewModel 完整 DI 重构（子 VM 走 DI，删 8 个冗余字段）
2. **A2** View 服务定位器改为 ViewModel 属性透传
3. **A3** ServerImporterService 注入 ConfigFileScanner + 拆分小服务
4. **A4** 提取 BeginOperation(ServerOperation) IDisposable 消除 6 处重复
5. **A5** NetshPortBridgeService 提取 RunNetsh 消除 5 处重复
6. **A6** AnimationHelper.PlayPageEntrance 静态方法统一 4 页面动画
7. **A7** 删除 SystemMonitorViewModel 4 个死属性
8. **A8** 拆分 3 个大型方法（DetectAllAsync/BuildServerInstanceAsync/ScanServerProcesses）

### 第五批：UI/XAML（U1-U10）— 统一视觉一致性
1. **U1** ColorSwatchStyle 统一到 AppResources
2. **U2** SectionTitleStyle AppResources 保留基础，各页 BasedOn 派生
3. **U3** NetworkMonitorPage 顶部改 WrapPanel
4. **U4** ConfigEditorPage 改 TreeView
5. **U5** 所有硬编码颜色（含色板）抽取为资源键
6. **U6** 所有固定宽度改 MinWidth + * 比例
7. **U7** MainWindow.xaml 删除重复 Window.Resources
8. **U8** 动画全部修（FillBehavior.Stop + Expander 反向 + GaugeRing 中断处理）
9. **U9** 补充 AutomationProperties.Name（图标按钮/仪表盘/状态点/滑块）
10. **U10** ConfigEditorPage 抽取 DisabledOpacityButtonStyle

### 第六批：配置/健壮性（C1-C6）— 增强鲁棒性
1. **C1** 抽取 IAppPaths 服务集中管理路径
2. **C2** AppConfigService.Save 改临时文件 + File.Replace 原子写
3. **C3** 反序列化后校验集合 null
4. **C4** JVM 内存参数正则验证
5. **C5** 桥接端口 1-65535 范围验证
6. **C6** 全部魔法数字提取常量（字节转换/Task.Delay/netsh 超时/动画延迟）

---

## ⚠️ 风险提示

- **A1 完整 DI 重构**：改动最大，需重新注册所有子 VM 到 DI 容器，可能影响启动流程
- **P1 WMI 批量重构**：涉及 ProcessScanner 核心检测逻辑，需充分测试
- **U4 改 TreeView**：ConfigEditorPage 数据模板需重写，改动较大
- **U9 无障碍**：工作量大，需逐个控件补充
- 建议每批完成后触发 CI 验证编译，确保不引入回归
