# 启动流程重构 + 主题修复 计划

## 一、问题根因分析

### 问题 1：控件圆角失效

**根因**：[SettingsPage.tsx](file:///workspace/src/frontend/src/pages/SettingsPage.tsx) 中 ColorPicker 只用了 `onChangeEnd`（颜色选择结束时才通知后端），但圆角/动画等设置没有对应的实时同步机制。更关键的是：

- 后端 [ThemeService.AnimationDuration](file:///workspace/src/McServerGuard/Services/ThemeService.cs#L281-L285) 和 [EnableAnimations](file:///workspace/src/McServerGuard/Services/ThemeService.cs#L288-L292) 的 setter 只改字段值，**不调用 `ApplyTheme()`**。
- 后端 `ApplyTheme()` 的 `UpdateResources()` 方法**没有把圆角和动画时长写入前端 CSS 变量的通道**——前端 CSS 变量只在 `applySettingsToCss()` 时设置，而这只在页面加载 `loadSettings()` 时调用一次。
- 前端 `ColorPicker` 只有 `onChangeEnd`，没有用 `onChange` 做实时预览，所以调色盘拖动时界面颜色不变。

### 问题 2：调色盘不是实时修改颜色

**根因**：[SettingsPage.tsx:309-320](file:///workspace/src/frontend/src/pages/SettingsPage.tsx#L309-L320) 中 ColorPicker 绑定的是 `onChangeEnd={handleSetPrimary}`，即**鼠标松开时才通知后端**。拖动过程中只改变了 ColorPicker 内部状态，没有同步到全局 CSS 变量。

### 问题 3：动画速度失效

**根因**：
1. 后端 `AnimationDuration`/`EnableAnimations` setter 不触发 `ApplyTheme()`
2. 即使触发了 `ApplyTheme()`，后端也只更新 WPF 资源字典，**不向前端推送 CSS 变量更新**
3. 前端 `loadSettings()` 是唯一应用动画设置到 CSS 的路径，而设置变化时没有重新调用

### 问题 4：启动流程需要重构

**现状**：[App.xaml.cs:55-279](file:///workspace/src/McServerGuard/App.xaml.cs#L55-L279) 的启动流程全部在 `OnStartup` 中同步执行，主窗口创建出来之前用户只能看到一个空窗或系统默认的等待光标。启动失败时弹一个丑陋的 `MessageBox`。

**目标**：
1. 立即显示一个 WPF 原生的等待窗口（.NET 框架窗口）
2. 等待窗口用类似日志输出的方式显示启动进度
3. 等待窗口有丝滑的动画和 UI
4. 等待窗口的主题色跟随用户的自定义颜色设置
5. 启动过程中的错误实时显示在等待窗口上

## 二、修改范围

### 后端（C#）
1. **ThemeService.cs**：修复 AnimationDuration/EnableAnimations setter 触发 ApplyTheme；新增向前端推送主题变量的事件/方法
2. **App.xaml.cs**：重构启动流程，先显示启动窗口，再异步执行初始化
3. **新增 StartupWindow.xaml + .cs**：启动等待窗口，带日志输出区域、进度动画、主题色跟随
4. **WebView2BridgeService.cs**：新增主题变更事件推送（前端 ← 后端）

### 前端（TS/React）
1. **SettingsPage.tsx**：ColorPicker 增加 `onChange` 实时预览；圆角/动画滑块实时同步
2. **theme.ts**：增加单属性更新函数（不用每次全量 applySettingsToCss）
3. **bridge.ts**：监听后端主题变更事件，实时更新 CSS 变量
4. **ColorPicker.tsx**：确认 onChange/onChangeEnd 正常工作（已具备）

## 三、详细修改步骤

### 阶段一：修复调色盘实时预览 + 圆角/动画失效

**步骤 1.1**：修改 `SettingsPage.tsx`
- ColorPicker 增加 `onChange` 回调，调用前端 `applyPrimaryColor()` / `applyAccentColor()` 做实时预览
- 圆角滑块：增加 `onChange` 实时更新 `--md-radius` 系列变量
- 动画滑块/开关：实时更新 `--md-duration-*` 和 `--md-enable-animations`
- `onChangeEnd` 时才通知后端保存（保持原有逻辑）

**步骤 1.2**：在 `theme.ts` 中增加单属性更新函数
- `applyCornerRadius(radius: number)`：单独更新圆角变量
- `applyAnimationSettings(duration: number, enabled: boolean)`：单独更新动画变量
- 导出已有 `applyPrimaryColor`

**步骤 1.3**：修复后端 ThemeService
- `AnimationDuration` setter：`value` 变更后调用 `ApplyTheme()` + `SaveSettings()`
- `EnableAnimations` setter：同上
- `CornerRadius` setter：确认已有 `ApplyTheme()` 调用（已有，无需改）

**步骤 1.4**：后端桥接新增主题变更通知
- 在 `WebView2BridgeService` 中新增 `SendThemeChanged(settings)` 方法
- `ThemeService` 增加 `ThemeChanged` 事件
- `ApplyTheme()` 触发事件 → 桥接发送 `theme:changed` 事件到前端

**步骤 1.5**：前端监听主题变更
- 在 appStore 或 bridge 初始化时监听 `theme:changed` 事件
- 收到后调用 `applySettingsToCss()` 更新全局样式

### 阶段二：重构启动流程 + 新增启动窗口

**步骤 2.1**：创建 `StartupWindow`（WPF 窗口）
- 无边框、圆角窗口
- 顶部：Logo + 应用名 + 版本号
- 中部：滚动的日志输出区域（类似终端，逐行追加启动日志）
- 底部：进度条/加载动画
- 主题色从 ThemeService 读取，跟随用户设置
- 使用 MaterialDesign 主题色资源保持一致

**步骤 2.2**：重构 `App.xaml.cs.OnStartup`
- 第一步：初始化日志 + 主题服务（轻量，立即加载主题设置）
- 第二步：创建并显示 `StartupWindow`（非模态，立即返回）
- 第三步：在后台线程（`Task.Run`）中执行重的初始化：
  - DI 容器构建
  - 服务注册
  - 配置加载
  - 管理员权限检查（需要交互的话回到 UI 线程）
  - 用户协议检查（首次使用时显示协议窗口）
- 第四步：每完成一步通过 `Dispatcher` 回 UI 线程更新 StartupWindow 的日志
- 第五步：全部完成后，创建 MainWindow 并显示，关闭 StartupWindow
- 错误处理：任何步骤异常时，在 StartupWindow 中显示错误详情 + 退出按钮，不再弹丑陋的 MessageBox

**步骤 2.3**：StartupWindow 的主题跟随
- 构造时从 ThemeService 读取颜色并应用到窗口背景/文字/进度条
- 监听 ThemeService.ThemeChanged 事件，动态更新颜色
- 因为是 WPF 原生窗口，直接用 DynamicResource 绑定即可

### 阶段三：WPF 原生控件的主题跟随

**步骤 3.1**：WPF 侧确认
- 确认 `ApplyTheme()` 已经把主色、背景色、文字色写入 `Application.Current.Resources`
- 确认 StartupWindow 使用这些 DynamicResource 键名
- 确保所有 WPF 窗口（MainWindow、UserAgreementWindow、StartupWindow）都用同一套资源键

## 四、风险与注意事项

1. **启动线程安全**：启动过程中从后台线程更新 UI 必须用 `Dispatcher.InvokeAsync`，避免跨线程异常
2. **用户协议弹窗**：用户协议需要在 UI 线程上 ShowDialog，注意不要阻塞后台初始化流程
3. **管理员权限提升**：`RequestElevation()` 会重启进程，需要确保 StartupWindow 能正确处理这种情况（直接 Close 即可，进程会被新实例替换）
4. **主题加载顺序**：主题服务必须在 StartupWindow 创建之前加载完成，否则窗口初始颜色不对
5. **WebView2 初始化时机**：MainWindow 里 WebView2 的初始化仍然在 MainWindow_Loaded 里做，不影响启动窗口
6. **圆角失效的真正原因**：需要确认是后端没更新、还是前端没收到、还是 CSS 变量名不匹配。先加日志再调。

## 五、验证方式

1. **调色盘实时性**：拖动调色盘时，界面主色实时变化，松开后保存到后端
2. **圆角实时性**：拖动圆角滑块，所有卡片/按钮的圆角实时变化
3. **动画速度**：调整动画时长，页面过渡动画速度随之变化；关闭动画开关，所有动画立即停止
4. **启动窗口**：
   - 启动程序后立即看到启动窗口，不是白屏
   - 日志逐行输出，能看到每个步骤在做什么
   - 启动完成后窗口平滑过渡到主窗口
   - 主题色和用户设置的一致
5. **错误展示**：故意制造一个启动错误（比如删掉某个依赖），错误信息显示在启动窗口里，不是 MessageBox
