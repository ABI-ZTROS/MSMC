# 代码审查修复计划

## 概述

对项目进行全面审查后发现的 14 个问题进行系统性修复，涵盖 Bug 修复、性能优化、UI/XAML 改进、代码质量提升和架构优化。

## 进度状态

### ✅ 已完成（3 项）
- **#1 MainViewModel 线程封套问题** — 简化为 Dispatcher.BeginInvoke + try-catch
- **#2 空 catch 块修复（5 处）** — 全部改为捕获 Exception 并记录日志
- **#3 async void 异常防护** — OnRefreshTick 已包裹 try-catch

### 🔄 进行中（1 项）
- **#4 SystemMonitor 实现 IDisposable** — ISystemMonitor 接口已继承 IDisposable，待实现 SystemMonitor.Dispose()

### ⏳ 待办（10 项）
- #5 ~ #14，详见下方修复项清单

---

## 用户确认的决策（本轮 /plan 询问结果）

| 编号 | 决策内容 |
|------|----------|
| #4 | Dispose 在 **App.OnExit** 中调用（生命周期最清晰，与应用退出同步） |
| #5 | **全部 Page 全量审查** Loaded→Unloaded 配对，确保所有 += 都有对应 -= |
| #6 | NavItem 模型字段：**IconKind + Title + PageIndex**（最小集合，XAML 用 DataTemplate 渲染） |
| #7 | **圆角 + 颜色全检查**（除 Gglaoguan 缅怀卡片保留硬编码外，其他硬编码色值统一到主题资源） |
| #8 | Troll 窗口保持原样，不修改 |
| #9 | 刷新频率 **确认合理不改**，仅在文档记录结论（1秒间隔，端口5秒/流量1秒） |
| #10 | **加注释说明**性能考量，不改变现有逻辑（条目数量在可控范围） |
| #11 | 注释精简范围：**重点 5 文件**（MainViewModel/ServerDetectionViewModel/UserAgreementWindow.xaml.cs/ThemeService/SystemMonitor） |
| #12 | **迁移 FindFirstVisualChild** 到 AnimationHelper，AnimateItemsControl 保留在 Page 中 |
| #13 | NoWarn **逐项全量审查**，判断每项是否仍需抑制 |
| #14 | version.json 初始版本 **0.2**（本次重构升级），引入 Nerdbank.GitVersioning |

---

## 修复项清单

### 🔴 高优先级（1-4）

#### #1 MainViewModel 线程封套问题 ✅ 已完成
- **文件**：`src/McServerGuard/ViewModels/MainViewModel.cs`
- **修法**：简化为直接在 UI 线程用 `Dispatcher.BeginInvoke` 延迟执行，内层加 try-catch 兜底

#### #2 空 catch 块（5 处）✅ 已完成
- **修复位置**：ServerImporterService.cs、JavaFinder.cs、ConfigEditorViewModel.cs、SettingsPage.xaml.cs、MemoryOptimizerService.cs
- **修法**：全部改为 `catch (Exception ex) { Log.Debug/Warning(ex, "..."); }`

#### #3 async void 无异常防护 ✅ 已完成
- **文件**：`ViewModels/NetworkMonitorViewModel.cs:352` OnRefreshTick
- **修法**：包裹 try-catch，捕获异常记录日志，设置 IsRefreshing = false 恢复状态

#### #4 SystemMonitor 未实现 IDisposable 🔄 进行中
- **文件**：`Services/SystemMonitoring/SystemMonitor.cs`、`App.xaml.cs`
- **进度**：ISystemMonitor 接口已继承 IDisposable ✅
- **待办**：
  1. SystemMonitor 实现 `Dispose()` 方法：
     - 调用 `StopMonitoring()` 停止监控
     - 释放 `_monitoringTimer`（Dispose 并置 null）
     - 释放 `_monitoringCts`（Cancel、Dispose 并置 null）
     - 释放 `_cpuCounter`（Dispose）
     - 添加 `_disposed` 标志防止重复释放
  2. App.xaml.cs 的 `OnExit` 中通过 DI 容器获取 ISystemMonitor 并调用 Dispose
  3. 注册方式不变（Singleton）

---

### 🟡 中优先级（5-10）

#### #5 事件订阅泄漏（全部 Page 全量审查）
- **范围**：所有 Page 的 Loaded→Unloaded 配对审查
  - `Views/ServerDetectionPage.xaml.cs`
  - `Views/SystemMonitorPage.xaml.cs`
  - `Views/ConfigEditorPage.xaml.cs`
  - `Views/SettingsPage.xaml.cs`
  - `Views/NetworkMonitorPage.xaml.cs`（已确认正确清理，作为参考）
- **修法**：
  1. 逐一检查每个 Page 的 Loaded 中的 `+=` 订阅
  2. 确认是否有对应 Unloaded 中的 `-=` 取消订阅
  3. 对缺失清理的订阅，补充 Unloaded 处理
  4. 同时检查 code-behind 中对 ViewModel 事件、外部服务事件的订阅
- **预期**：大部分 Page 的 Loaded 只做动画初始化，风险低；仅对有订阅的补充清理

#### #6 MainWindow 导航重构为数据驱动
- **文件**：`ViewModels/MainViewModel.cs`、`Views/MainWindow.xaml`、新增 `Models/NavItem.cs`
- **NavItem 模型字段**（确认）：
  - `PackIconKind IconKind` — MaterialDesign 图标枚举
  - `string Title` — 显示标题
  - `int PageIndex` — 对应页面索引
- **修法**：
  1. 新增 `Models/NavItem.cs`，包含上述 3 个字段（普通类或 record）
  2. MainViewModel 新增 `ObservableCollection<NavItem> NavItems`，构造函数中填充 5 项
  3. MainWindow.xaml 的 NavListBox 改为 `ItemsSource="{Binding NavItems}"`
  4. 用 DataTemplate 渲染图标（PackIcon Kind="{Binding IconKind}"）+ 文字（TextBlock Text="{Binding Title}"）
  5. 删除 NavItemText1~5 的 x:Name 硬编码
  6. 保留 `SelectedTabIndex` 双向绑定（ListBox.SelectedIndex）
- **验证**：5 个页面切换正常，动画/状态保持正常

#### #7 UserAgreementWindow 硬编码圆角 + 颜色全检查
- **文件**：`Views/UserAgreementWindow.xaml`
- **修法**：
  1. 扫描整个 XAML，所有硬编码 `CornerRadius`/`RadiusX`/`RadiusY` → 替换为 `{DynamicResource AppCornerRadius}` / `AppCornerRadiusSmall` / `AppCornerRadiusLarge`
  2. 所有硬编码颜色（Background/Foreground/BorderBrush 等）→ 替换为对应的主题 DynamicResource
  3. **例外**：Gglaoguan 缅怀卡片的硬编码颜色保留不动（用户确认）
  4. 按钮的 `CornerRadius="4"` → `CornerRadius="{DynamicResource AppCornerRadiusSmall}"`

#### #8 Troll 窗口 — 不修改
- 用户确认保持原样，跳过

#### #9 网络流量监控刷新频率 — 确认合理不改
- **文件**：`ViewModels/NetworkMonitorViewModel.cs`
- **结论**：当前 `_refreshTimer.Interval = 1秒`，端口每 5 tick（5秒）刷新、流量每 tick（1秒）采样，频率合理
- **动作**：不修改代码，仅在文档记录此结论

#### #10 ConfigEditorViewModel 批量事件订阅 — 加注释说明
- **文件**：`ViewModels/ConfigEditorViewModel.cs:880` 附近
- **修法**：在 `entry.PropertyChanged += OnConfigEntryChanged` 处添加注释，说明：
  - 配置条目数量通常在几十个范围内
  - 订阅开销可忽略
  - 配置文件切换时旧条目会被 GC 回收（短期订阅无需手动取消）
- **不改逻辑**

---

### 🟢 低优先级（11-14）

#### #11 注释精选精简（重点 5 文件）
- **范围**：仅处理以下 5 个文件
  1. `ViewModels/MainViewModel.cs`
  2. `ViewModels/ServerDetectionViewModel.cs`
  3. `Views/UserAgreementWindow.xaml.cs`
  4. `Services/ThemeService.cs`
  5. `Services/SystemMonitoring/SystemMonitor.cs`
- **修法**：
  - 删除"字段名=注释内容"的不言自明注释
  - 删除方法签名已完全表达的 XML `<param>` 和 `<returns>` 注释
  - 保留：复杂逻辑解释、设计意图、边界条件、为什么这样做
- **不动**：Models 目录、其他文件

#### #12 ServerDetectionPage 可视化树辅助方法迁移
- **文件**：`Views/ServerDetectionPage.xaml.cs`、`Views/Helpers/AnimationHelper.cs`
- **修法**：
  1. 将 `FindFirstVisualChild<T>` 移到 `AnimationHelper` 作为公共静态方法
  2. ServerDetectionPage 改为调用 `AnimationHelper.FindFirstVisualChild<T>`
  3. `AnimateItemsControl` 保留在 Page 中（动画逻辑属 View 层）
  4. 删除 Page 中的私有 `FindFirstVisualChild` 定义

#### #13 NoWarn 清单逐项全量审查
- **文件**：`McServerGuard.csproj`（第 29-44 行）
- **修法**：逐项审查每个 NoWarn，判断是否仍需抑制：
  - **CS8602/CS8604/CS8625**（可空警告）— 移除抑制，修复暴露的 null 警告
  - **CA1305**（IFormatProvider）— WPF 项目可保留
  - **CA1416**（平台兼容性）— 评估是否仍需
  - **IDE 规则**（IDE0063/IDE0090/IDE0260 等）— 代码风格，保留抑制合理
  - 其他每项逐一判断
- **验证**：移除抑制后修复所有新暴露的警告，确保编译无 Warning

#### #14 版本号自动化（GitVersioning，初始 0.2）
- **文件**：`McServerGuard.csproj`、新增 `version.json`
- **修法**：
  1. 添加 `Nerdbank.GitVersioning` NuGet 包
  2. 创建 `version.json`：
     ```json
     {
       "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/main/src/NerdBank.GitVersioning/consts.schema.json",
       "version": "0.2",
       "publicReleaseRefSpec": ["^refs/heads/main$"],
       "release": { "tagName": "v{version}" }
     }
     ```
  3. 从 csproj 移除 `<Version>0.1.0</Version>`
  4. 保留版本号规则（大重构+1大版本、正常提交+小数位、fix标签），通过 Git tag/commit 控制
- **验证**：CI 中确认版本号正确生成（如 0.2.x）

---

## 实施顺序

1. **第一批：高优先级 Bug 修复**（#4）— 完成 IDisposable 实现
2. **第二批：中优先级**（#5→#7→#6）— #6 导航重构改动最大放最后
   - #5 事件泄漏审查（低风险）
   - #7 圆角+颜色修复（中风险）
   - #6 导航重构（高风险，需充分测试）
   - #9/#10 仅文档/注释，穿插完成
3. **第三批：低优先级**（#11→#12→#13→#14）
   - #11 注释精简（低风险但需仔细）
   - #12 方法迁移（低风险）
   - #13 NoWarn 审查（可能暴露大量警告，需谨慎）
   - #14 版本号自动化（需测试 CI）
4. **版本号更新**：所有修复完成后，版本号由 GitVersioning 自动管理

## 验证方式

- 每个 Batch 完成后推送代码，触发 GitHub Actions CI 验证编译
- CI 通过后下载 Artifact 确认 EXE 正常生成
- #6 导航重构后需确认所有 5 个页面切换正常
- #7 主题修改后需确认圆角/颜色在运行时动态切换正常
- #13 NoWarn 移除后确认编译无新增 Warning
- #14 GitVersioning 配置后确认 CI 中版本号正确生成
