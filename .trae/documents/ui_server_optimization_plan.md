# UI/服务器管理/GitHub Actions 全面优化计划

> **For agentic workers:** 按任务顺序执行，每个任务独立提交，确保每一步都能编译通过。

**Goal:** 优化动画和页面质感、合并服务器检测与导入功能、实现已知服务器快捷启动、根治 GitHub Actions 稳定性问题

**Architecture:** 
- 动画层：统一通过资源字典和附加属性实现，避免每个页面重复写 Storyboard
- 服务器管理：检测/导入/已知服务器 三合一，左列列表 + 右列详情 + 底部操作栏
- CI 层：增加预检查步骤，测试与构建解耦，失败时自动上传诊断日志

**Tech Stack:** WPF (.NET 10), CommunityToolkit.Mvvm, MaterialDesignInXamlToolkit, MahApps.Metro.IconPacks, GitHub Actions

---

## 现状分析

### 动画现状
- ✅ 导航项有悬停缩放 + 选中过渡
- ✅ 颜色选择器按钮有悬停/按下动画
- ✅ 服务器检测页卡片有入场动画（Loaded 触发）
- ✅ 主窗口启动淡入
- ❌ **页面切换只有淡入，没有滑入/滑出过渡**
- ❌ **大部分页面（配置编辑、系统监控、AI守护、设置）缺少入场动画**
- ❌ **按钮没有统一的点击缩放反馈**
- ❌ **缺少数据加载骨架屏/脉冲动画**
- ❌ **卡片悬停效果不统一（有的用 MaterialDesignCard，有的是自定义 Border）**

### 服务器检测与导入 —— 功能重叠严重

| 功能 | 检测页 (ServerDetection) | 导入页 (ServerImport) |
|------|------------------------|---------------------|
| 扫描运行中进程 | ✅ | ❌ |
| 选择JAR文件导入 | ❌ | ✅ |
| 显示服务器详情 | ✅（部分） | ✅（完整） |
| JVM参数编辑 | ❌ | ✅ |
| 启动/停止服务器 | ❌ | ✅ |
| 已知服务器列表 | ✅ | ❌ |
| 保存为已知服务器 | ✅ | ❌ |

**问题：** 两个页面各做一半，用户要在两个页面之间来回跳。保存了服务器却不能直接启动，必须去导入页重新选JAR。

### GitHub Actions 问题
- ✅ 编译本身能通过（已验证）
- ✅ ServerTypeClassifier 测试失败已修复
- ⚠️ **测试项目引用了已删除的 NuGet 包**（OnnxRuntime 传递依赖警告）
- ⚠️ **audit 任务有 Node.js 20 废弃警告**
- ⚠️ **缺少代码覆盖率报告**
- ⚠️ **CI 失败时没有自动上传诊断产物**

---

## 任务清单

### Task 1: 统一动画系统 —— 页面切换 + 入场 + 交互反馈

**目标：** 让所有页面切换和交互有流畅、统一的动画效果

**Files:**
- Modify: `src/McServerGuard/Themes/AppResources.xaml`
- Modify: `src/McServerGuard/Views/MainWindow.xaml.cs`
- Modify: `src/McServerGuard/Views/MainWindow.xaml`
- Modify: `src/McServerGuard/Views/ConfigEditorPage.xaml`
- Modify: `src/McServerGuard/Views/SystemMonitorPage.xaml`
- Modify: `src/McServerGuard/Views/AIGuardPage.xaml`
- Modify: `src/McServerGuard/Views/SettingsPage.xaml`
- Modify: `src/McServerGuard/Views/ServerImportPage.xaml`

**Steps:**

- [ ] **Step 1.1: 在 AppResources.xaml 中添加统一动画资源**

添加以下资源（放在 `<ResourceDictionary>` 顶部）：

```xml
<!-- ═══════════════════════════════════════════════════════════════ -->
<!-- ✨ 统一动画资源 —— 全项目共用，改一处全局生效 -->
<!-- ═══════════════════════════════════════════════════════════════ -->

<!-- 🎢 标准缓动函数 -->
<CubicEase x:Key="StandardEase" EasingMode="EaseOut" />
<QuarticEase x:Key="EmphasizedEase" EasingMode="EaseOut" />

<!-- 📄 页面入场故事板：淡入 + 从右滑入 -->
<Storyboard x:Key="PageEnterStoryboard">
    <DoubleAnimation Storyboard.TargetProperty="Opacity"
                     From="0" To="1"
                     Duration="0:0:0.3"
                     EasingFunction="{StaticResource StandardEase}" />
    <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)"
                     From="30" To="0"
                     Duration="0:0:0.35"
                     EasingFunction="{StaticResource EmphasizedEase}" />
</Storyboard>

<!-- 🃏 卡片悬停故事板：微微上浮 + 阴影加深 -->
<Style x:Key="AnimatedCardStyle" TargetType="materialDesign:Card">
    <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}" />
    <Setter Property="Margin" Value="4" />
    <Setter Property="UniformCornerRadius" Value="12" />
    <Setter Property="RenderTransform">
        <Setter.Value>
            <TranslateTransform Y="0" />
        </Setter.Value>
    </Setter>
    <Setter Property="Effect">
        <Setter.Value>
            <DropShadowEffect Color="#000000" Opacity="0" BlurRadius="0" ShadowDepth="0" />
        </Setter.Value>
    </Setter>
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Trigger.EnterActions>
                <BeginStoryboard>
                    <Storyboard>
                        <DoubleAnimation Storyboard.TargetProperty="RenderTransform.Y"
                                         Duration="0:0:0.2"
                                         To="-3"
                                         EasingFunction="{StaticResource StandardEase}" />
                        <DoubleAnimation Storyboard.TargetProperty="Effect.Opacity"
                                         Duration="0:0:0.25"
                                         To="0.25"
                                         EasingFunction="{StaticResource StandardEase}" />
                        <DoubleAnimation Storyboard.TargetProperty="Effect.BlurRadius"
                                         Duration="0:0:0.25"
                                         To="12"
                                         EasingFunction="{StaticResource StandardEase}" />
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
                <BeginStoryboard>
                    <Storyboard>
                        <DoubleAnimation Storyboard.TargetProperty="RenderTransform.Y"
                                         Duration="0:0:0.2"
                                         To="0"
                                         EasingFunction="{StaticResource StandardEase}" />
                        <DoubleAnimation Storyboard.TargetProperty="Effect.Opacity"
                                         Duration="0:0:0.2"
                                         To="0"
                                         EasingFunction="{StaticResource StandardEase}" />
                        <DoubleAnimation Storyboard.TargetProperty="Effect.BlurRadius"
                                         Duration="0:0:0.2"
                                         To="0"
                                         EasingFunction="{StaticResource StandardEase}" />
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.ExitActions>
        </Trigger>
    </Style.Triggers>
</Style>

<!-- 🔘 按钮点击缩放动画附加样式 -->
<Style x:Key="AnimatedButtonStyle" TargetType="Button"
       BasedOn="{StaticResource MaterialDesignRaisedButton}">
    <Setter Property="RenderTransformOrigin" Value="0.5,0.5" />
    <Setter Property="RenderTransform">
        <Setter.Value>
            <ScaleTransform ScaleX="1" ScaleY="1" />
        </Setter.Value>
    </Setter>
    <Style.Triggers>
        <Trigger Property="IsPressed" Value="True">
            <Trigger.EnterActions>
                <BeginStoryboard>
                    <Storyboard>
                        <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX"
                                         Duration="0:0:0.08"
                                         To="0.96"
                                         EasingFunction="{StaticResource StandardEase}" />
                        <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY"
                                         Duration="0:0:0.08"
                                         To="0.96"
                                         EasingFunction="{StaticResource StandardEase}" />
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
                <BeginStoryboard>
                    <Storyboard>
                        <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleX"
                                         Duration="0:0:0.15"
                                         To="1"
                                         EasingFunction="{StaticResource EmphasizedEase}" />
                        <DoubleAnimation Storyboard.TargetProperty="RenderTransform.ScaleY"
                                         Duration="0:0:0.15"
                                         To="1"
                                         EasingFunction="{StaticResource EmphasizedEase}" />
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.ExitActions>
        </Trigger>
    </Style.Triggers>
</Style>

<!-- 💫 脉冲动画（用于加载/等待状态） -->
<Storyboard x:Key="PulseStoryboard" RepeatBehavior="Forever" AutoReverse="True">
    <DoubleAnimation Storyboard.TargetProperty="Opacity"
                     From="0.4" To="1"
                     Duration="0:0:1"
                     EasingFunction="{StaticResource StandardEase}" />
</Storyboard>
```

- [ ] **Step 1.2: 给主窗口页面切换加上滑入动画**

修改 MainWindow.xaml.cs 中的 `MainWindow_Loaded` 方法，在切换页面时触发动画。同时需要在 MainWindow.xaml 中给 ContentControl 加上 `RenderTransform`：

MainWindow.xaml 中的 MainContent 改为：
```xml
<ContentControl Grid.Row="1" Grid.Column="1" Margin="8"
                Content="{Binding CurrentPage}"
                Opacity="0"
                x:Name="MainContent"
                RenderTransformOrigin="0.5,0.5">
    <ContentControl.RenderTransform>
        <TranslateTransform X="30" />
    </ContentControl.RenderTransform>
```

MainWindow.xaml.cs 中新增 `OnNavigating` 方法（订阅 SelectedTabIndex 变化，或在代码后台加事件）。

**简化方案：** 在 MainWindow.xaml.cs 里注册 `CurrentPage` 变化时触发动画。给 MainViewModel 的 `OnSelectedTabIndexChanged` 加一个事件通知，或者直接在 MainWindow.xaml.cs 里用 `DataContextChanged` + 属性监听。

最直接的方式：在 MainWindow.xaml.cs 中加一个 `OnContentChanged` 的处理：

```csharp
// 在构造函数中加
MainContent.LayoutUpdated += (s, e) => { /* 不好，太频繁 */ };

// 更好的方式：监听 DataContext 中的 SelectedTabIndex
private MainViewModel? _vm;

protected override void OnContentChanged(object oldContent, object newContent)
{
    base.OnContentChanged(oldContent, newContent);
    if (DataContext is MainViewModel vm && _vm != vm)
    {
        _vm = vm;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            {
                AnimatePageTransition();
            }
        };
    }
}

private void AnimatePageTransition()
{
    if (!_themeService.EnableAnimations)
    {
        MainContent.Opacity = 1;
        return;
    }

    var duration = _themeService.AnimationDuration;

    // 先快速淡出（可选，或者直接淡入新页面）
    var fadeIn = new DoubleAnimation
    {
        From = 0,
        To = 1,
        Duration = TimeSpan.FromMilliseconds(duration),
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };
    var slideIn = new DoubleAnimation
    {
        From = 20,
        To = 0,
        Duration = TimeSpan.FromMilliseconds(duration + 50),
        EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
    };

    MainContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    if (MainContent.RenderTransform is TranslateTransform tt)
    {
        tt.BeginAnimation(TranslateTransform.XProperty, slideIn);
    }
}
```

- [ ] **Step 1.3: 给其余5个页面加上入场动画**

给 ConfigEditorPage.xaml、SystemMonitorPage.xaml、AIGuardPage.xaml、SettingsPage.xaml、ServerImportPage.xaml 的根元素（第一个子元素，通常是 Grid 或 StackPanel）加上 `Loaded` 事件触发的入场动画。

**统一模式：** 在 UserControl 的第一个子元素（最外层布局 Grid）上加上：
```xml
<Grid.RenderTransform>
    <TranslateTransform X="30" />
</Grid.RenderTransform>
<Grid.Opacity>0</Grid.Opacity>
<Grid.Triggers>
    <EventTrigger RoutedEvent="Loaded">
        <BeginStoryboard Storyboard="{StaticResource PageEnterStoryboard}" />
    </EventTrigger>
</Grid.Triggers>
```

注意：ServerDetectionPage 已经有了，不用改。但要把它的样式也统一用 `PageEnterStoryboard`。

- [ ] **Step 1.4: 验证编译通过**

```bash
dotnet build src/McServerGuard/McServerGuard.csproj -c Release
```
Expected: Build succeeded, 0 errors

- [ ] **Step 1.5: 提交**

```bash
git add src/McServerGuard/Themes/AppResources.xaml src/McServerGuard/Views/MainWindow.xaml src/McServerGuard/Views/MainWindow.xaml.cs src/McServerGuard/Views/ConfigEditorPage.xaml src/McServerGuard/Views/SystemMonitorPage.xaml src/McServerGuard/Views/AIGuardPage.xaml src/McServerGuard/Views/SettingsPage.xaml src/McServerGuard/Views/ServerImportPage.xaml
git commit -m "feat: 统一动画系统 - 页面切换+入场+卡片悬停+按钮点击"
```

---

### Task 2: 合并服务器检测与导入 —— 统一为"服务器管理"页面

**目标：** 一个页面搞定所有服务器相关操作，左侧列表 + 右侧详情 + 启动/停止

**Files:**
- Modify: `src/McServerGuard/ViewModels/MainViewModel.cs`
- Modify: `src/McServerGuard/ViewModels/ServerDetectionViewModel.cs` — 大幅扩充，吸收 Import 的功能
- Modify: `src/McServerGuard/Views/ServerDetectionPage.xaml` — 重新设计布局
- Create: 不需要新建文件，用现有文件改造
- Modify: `src/McServerGuard/Views/MainWindow.xaml` — 导航项改名
- Delete: `src/McServerGuard/Views/ServerImportPage.xaml`
- Delete: `src/McServerGuard/Views/ServerImportPage.xaml.cs`
- Delete: `src/McServerGuard/ViewModels/ServerImportViewModel.cs` — 功能合并到 DetectionViewModel

**设计方案：**

```
┌─────────────────────────────────────────────────┐
│ 🔍 服务器管理                                  │
├───────────────┬─────────────────────────────────┤
│ 📋 服务器列表  │  📝 服务器详情 / 参数配置      │
│               │                                 │
│ [🖥️ 生存服] ◄─ │  类型: Paper 1.21.4            │
│ [🖥️ 测试服]   │  路径: D:\MC\survival          │
│ [📥 导入JAR]  │  Java: Zulu 21                 │
│ [🔍 重新扫描] │  内存: 8GB / 16GB              │
│               │                                 │
│ 已知服务器:   │  🎛️ JVM 参数编辑器             │
│ • 生存服 ✅运行│  [预设: Aikar | G1GC | ZGC]   │
│ • 测试服      │  -Xms4G -Xmx8G ...             │
│ • 模组服      │                                 │
├───────────────┴─────────────────────────────────┤
│ [▶️ 启动] [⏹️ 停止] [💾 保存] [⚙️ 编辑配置]      │
└─────────────────────────────────────────────────┘
```

**Steps:**

- [ ] **Step 2.1: 扩充 ServerDetectionViewModel，吸收导入页的功能**

在 ServerDetectionViewModel 中添加以下属性和命令（从 ServerImportViewModel 迁移）：
- JVM 参数编辑（SelectedArguments、InitialMemory、MaxMemory、SelectedCategory 等）
- 启动/停止命令（StartServerCommand、StopServerCommand）
- JAR 文件选择 + 导入（BrowseJarFile、ImportServer）
- 预设应用（ApplyAikarPreset、ApplyG1GCPreset、ApplyZgcPreset）
- 参数增删改（AddArgument、RemoveArgument、StartEditArgument 等）

**核心新增命令：**
- `StartKnownServerCommand(KnownServer server)` —— 从已知服务器直接启动
- `StopServerCommand` —— 停止当前选中的服务器

**关键改动：**
```csharp
// 新增字段
private readonly IServerManagerService _serverManager;
private readonly IAiSelfLearningService _aiLearning;

// 新增属性（从 ImportViewModel 迁移）
[ObservableProperty] private string _initialMemory = "2G";
[ObservableProperty] private string _maxMemory = "4G";
[ObservableProperty] private ArgumentCategory _selectedCategory = ArgumentCategory.Memory;
[ObservableProperty] private bool _isStarting;
[ObservableProperty] private bool _isStopping;
// ... 其他 JVM 参数相关属性

public ObservableCollection<string> SelectedArguments { get; } = [];
public ObservableCollection<ArgumentCategory> AllArgumentCategories { get; } =
    new(Enum.GetValues<ArgumentCategory>());

// 新增命令
[RelayCommand(CanExecute = nameof(CanStartKnownServer))]
private async Task StartKnownServerAsync(KnownServer? server) { ... }

[RelayCommand(CanExecute = nameof(CanStopServer))]
private async Task StopServerAsync() { ... }

[RelayCommand]
private void BrowseJarFile() { ... }

// 从 ImportViewModel 迁移的所有命令方法...
```

- [ ] **Step 2.2: 重新设计 ServerDetectionPage.xaml 布局**

改为两列布局：
- 左列（35%宽度）：服务器列表（运行中 + 已知 + 导入按钮）
- 右列（65%宽度）：服务器详情 + JVM参数编辑器 + 启动/停止按钮

底部操作栏：启动、停止、保存、编辑配置

- [ ] **Step 2.3: 删除 ServerImportPage 和 ServerImportViewModel**

确认所有功能都已迁移到 Detection 后，删除：
- `src/McServerGuard/Views/ServerImportPage.xaml`
- `src/McServerGuard/Views/ServerImportPage.xaml.cs`
- `src/McServerGuard/ViewModels/ServerImportViewModel.cs`

- [ ] **Step 2.4: 更新 MainViewModel 和 MainWindow**

- MainViewModel 中移除 ImportPage，导航项从6个变5个
- MainWindow.xaml 中导航项"服务器导入"移除，"服务器检测"改名为"服务器管理"
- CurrentPage 的 switch 调整索引

- [ ] **Step 2.5: 验证编译通过**

```bash
dotnet build src/McServerGuard/McServerGuard.csproj -c Release
```
Expected: Build succeeded, 0 errors

- [ ] **Step 2.6: 提交**

```bash
git add -A
git commit -m "refactor: 合并服务器检测与导入为统一服务器管理页面"
```

---

### Task 3: 已知服务器快捷启动 —— 一点即启

**目标：** 在已知服务器列表中直接点启动按钮就能跑起来，不用再选JAR配参数

**Files:**
- Modify: `src/McServerGuard/ViewModels/ServerDetectionViewModel.cs` — 添加 StartKnownServerCommand
- Modify: `src/McServerGuard/Views/ServerDetectionPage.xaml` — 已知服务器卡片加启动按钮
- Modify: `src/McServerGuard/Models/KnownServer.cs` — 确认字段齐全

**Steps:**

- [ ] **Step 3.1: 确认 KnownServer 模型字段齐全**

检查 KnownServer 是否有启动需要的所有字段：JavaPath、MaxHeapMemoryBytes、JvmArguments（如果保存了的话）。如果没有，补充以下字段：
- `public List<string> JvmArguments { get; set; } = [];`
- `public long InitialHeapMemoryBytes { get; set; }`
- `public string JavaPath { get; set; } = "java";`

- [ ] **Step 3.2: 添加 StartKnownServerCommand**

在 ServerDetectionViewModel 中：
```csharp
[RelayCommand(CanExecute = nameof(CanStartKnownServer))]
private async Task StartKnownServerAsync(KnownServer? server)
{
    if (server is null) return;

    IsStarting = true;
    Log.Information("🚀 快捷启动已知服务器: {Name}", server.Name);

    try
    {
        var jvmArgs = server.JvmArguments.Count > 0
            ? server.JvmArguments.ToList()
            : BuildDefaultArguments(server);

        var instance = new ServerInstance
        {
            ServerType = ServerType.Unknown, // 可以从 JAR 名推断
            WorkingDirectory = server.WorkingDirectory,
            JavaPath = server.JavaPath,
            ServerJarPath = server.ServerJarPath,
            ServerJarName = Path.GetFileName(server.ServerJarPath),
            JvmArguments = jvmArgs,
            InitialHeapMemoryBytes = server.InitialHeapMemoryBytes,
            MaxHeapMemoryBytes = server.MaxHeapMemoryBytes,
            ServerPort = server.Port
        };

        var process = await Task.Run(() => _serverManager.StartServer(instance));
        if (process != null)
        {
            Log.Information("✅ 快捷启动成功: {Name}, PID={Pid}", server.Name, process.Id);
            // 刷新检测结果
            await DetectAsync();
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "💥 快捷启动失败: {Message}", ex.Message);
    }
    finally
    {
        IsStarting = false;
    }
}

private bool CanStartKnownServer(KnownServer? server)
    => server != null && !IsStarting && !IsDetecting;
```

- [ ] **Step 3.3: 更新 UI，已知服务器卡片加启动按钮**

在已知服务器的 DataTemplate 中，把删除按钮换成一个操作组：
- 运行按钮（▶️）— 快捷启动
- 配置按钮（⚙️）— 加载到右侧编辑器
- 删除按钮（🗑️）— 移除

- [ ] **Step 3.4: 保存时记录JVM参数**

修改 SaveAsKnownServer 命令，保存时把当前选中服务器的 JVM 参数、Java 路径等也存下来，这样下次启动才能直接用。

- [ ] **Step 3.5: 验证编译通过**

```bash
dotnet build src/McServerGuard/McServerGuard.csproj -c Release
```
Expected: Build succeeded, 0 errors

- [ ] **Step 3.6: 提交**

```bash
git add -A
git commit -m "feat: 已知服务器快捷启动 - 一点即启，自动保存JVM参数"
```

---

### Task 4: GitHub Actions 稳定性根治

**目标：** CI 稳定可靠，失败时有诊断信息，不会因为非关键问题红叉

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `src/McServerGuard.Tests/McServerGuard.Tests.csproj` — 清理废弃警告
- Create: `.github/workflows/pr-title-check.yml`（可选）

**Steps:**

- [ ] **Step 4.1: 清理测试项目的废弃警告**

McServerGuard.Tests.csproj 中的 NoWarn 有 `NU1904`（OnnxRuntime 漏洞），但主项目已经移除了 ML/ONNX 包，测试项目可能也不再有这个传递依赖了。验证一下，如果没有就从 NoWarn 里移除。

- [ ] **Step 4.2: 重写 CI 工作流，增加稳定性**

ci.yml 的改进点：

1. **增加 pre-build 检查**：先做语法检查 + 引用检查
2. **测试失败不阻断发布**：测试失败也上传产物，方便诊断
3. **失败时自动上传构建日志**：方便排查
4. **升级 action 版本以消除 Node.js 20 警告**：用 v5 版本的 action（如果有的话），或者加一个警告忽略
5. **增加 .NET 版本矩阵？** 不，保持单版本就好
6. **缓存优化**：不仅缓存 NuGet，也缓存 build 输出（增量编译）

具体改动：

```yaml
name: MSMC CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build:
    name: 🏗️ 编译 + 测试 + 发布
    runs-on: windows-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore dependencies
        run: dotnet restore

      # 先宽松编译（警告不视为错误），确认代码能编过
      - name: Build (relaxed)
        run: dotnet build src/McServerGuard/McServerGuard.csproj --configuration Release --no-restore

      # 再严格编译（警告=错误），有问题单独报
      - name: Build (strict - warnings as errors)
        continue-on-error: true
        id: strict_build
        run: dotnet build src/McServerGuard/McServerGuard.csproj --configuration Release --no-restore -p:TreatWarningsAsErrors=true -p:CodeAnalysisTreatWarningsAsErrors=true -p:AnalysisMode=All

      - name: Build test project
        run: dotnet build src/McServerGuard.Tests/McServerGuard.Tests.csproj --configuration Release --no-restore

      - name: Test
        id: test
        continue-on-error: true
        run: |
          dotnet test src/McServerGuard.Tests/McServerGuard.Tests.csproj --configuration Release --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: src/McServerGuard.Tests/TestResults/

      - name: Publish (single-file)
        if: success() || failure()
        run: |
          dotnet publish src/McServerGuard/McServerGuard.csproj `
            --configuration Release `
            -p:RuntimeIdentifier=win-x64 `
            -p:SelfContained=true `
            -p:PublishSingleFile=true `
            -p:EnableCompressionInSingleFile=true `
            -o publish/win-x64
        shell: pwsh

      - name: Verify Artifact
        if: success() || failure()
        shell: pwsh
        run: |
          $exe = "publish/win-x64/McServerGuard.exe"
          if (-not (Test-Path $exe)) {
            echo "❌ 发布产物中找不到 McServerGuard.exe！"
            exit 1
          }
          $size = (Get-Item $exe).Length
          echo "📦 McServerGuard.exe 大小: $([math]::Round($size/1MB, 2)) MB"
          if ($size -lt 1MB) {
            echo "❌ EXE 文件太小，可能发布失败！"
            exit 1
          }
          echo "✅ 发布产物检查通过！"

      - name: Upload Artifact
        if: success() || failure()
        uses: actions/upload-artifact@v4
        with:
          name: MSMC-win-x64-singlefile
          path: publish/win-x64/
          retention-days: 30

      # 测试失败时最终标记失败
      - name: Final status check
        if: steps.test.outcome != 'success'
        run: |
          echo "❌ 测试未通过，请查看 test-results 产物"
          exit 1
        shell: pwsh

  audit:
    name: 🔍 代码质量检查
    runs-on: windows-latest
    needs: build
    continue-on-error: true
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Code Format Check
        continue-on-error: true
        run: dotnet format --verify-no-changes --verbosity diagnostic
        shell: pwsh

      - name: Code Quality Scan
        continue-on-error: true
        shell: pwsh
        run: |
          # ...（保留原有扫描逻辑）

      - name: Security Vulnerability Scan (direct)
        continue-on-error: true
        run: dotnet list package --vulnerable
        shell: pwsh
```

关键改进：
1. 宽松编译先跑，确保代码能编过
2. 严格编译 `continue-on-error: true`，不阻断
3. 测试 `continue-on-error: true`，但最后有 Final status check 确保失败时 CI 还是红的
4. Publish 和 Verify Artifact 都加了 `if: success() || failure()`，即使测试失败也有产物
5. 安全扫描也 `continue-on-error: true` 了

- [ ] **Step 4.3: 验证语法正确**

用 yamllint 或直接肉眼检查（GitHub Actions 会在运行时报错）。

- [ ] **Step 4.4: 提交**

```bash
git add .github/workflows/ci.yml src/McServerGuard.Tests/McServerGuard.Tests.csproj
git commit -m "ci: 根治CI稳定性 - 宽松+严格双编译,测试失败仍上传产物"
```

---

## 风险与注意事项

1. **Task 2（合并页面）风险最高** —— 改动量大，容易引入回归
   - 建议：先把 ImportViewModel 的功能一点点迁移到 DetectionViewModel，每迁一部分编译验证一次
   - 最后再删文件

2. **动画性能** —— WPF 中 DropShadowEffect 用多了会卡
   - 建议：只在卡片悬停时启用，默认关闭（Opacity=0）
   - 如果低配机器卡顿，可以在设置里加开关

3. **已知服务器启动路径有效性** —— 用户可能移动了JAR文件
   - 建议：启动前检查文件是否存在，不存在时提示用户重新选择

4. **CI Node.js 20 警告** —— 这是 GitHub 的事，action 升级到 v5 才会解决
   - 建议：保持 v4，警告不影响功能，等官方更新

---

## 执行顺序建议

1. **Task 4 (CI)** — 先改 CI，让它稳定，这样后面的改动都有可靠的绿灯指示
2. **Task 1 (动画)** — 改动相对独立，不会影响功能
3. **Task 3 (快捷启动)** — 在现有结构上加功能，改动较小
4. **Task 2 (合并页面)** — 最大的改动，最后做，确保前面都稳了再动大手术
