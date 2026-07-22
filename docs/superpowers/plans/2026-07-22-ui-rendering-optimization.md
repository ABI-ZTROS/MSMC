# UI 渲染与动画性能极致优化方案

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 McServerGuard WPF 应用的 UI 渲染帧率、内存占用、动画流畅度推向极致，消除掉帧、卡顿与 GC 抖动，使所有动画与交互在普通硬件上稳定 60 FPS。

**Architecture:** 以 "Freeze 一切可 Freeze 对象、全面启用 UI 虚拟化、将依赖动画转换为独立动画、全局统一动画开关" 为核心策略；通过静态资源缓存、Bitmap Caching 策略、视觉树精简、数据更新节流四层优化叠加，实现从资源层到控件层的全链路性能提升。

**Tech Stack:** WPF (.NET), MaterialDesignInXamlToolkit, MahApps.Metro.IconPacks, CommunityToolkit.Mvvm, Serilog.

---

## 调研结论（当前已发现的性能瓶颈）

1. **Freezable 资源未 Freeze**：`AppResources.xaml` 中的 `CubicEase`、`QuarticEase`、`Storyboard`、`LinearGradientBrush` 及大量 `SolidColorBrush` 未 Freeze，导致运行时复制对象并增加 GC。
2. **依赖动画大量使用**：`ServerDetectionPage.xaml` 与 `ConfigEditorPage.xaml` 的 Loading 图标使用 `RotateTransform.Angle` 依赖动画，由 CPU 在 UI 线程逐帧插值，阻塞渲染。
3. **UI 虚拟化缺失**：
   - `ServerDetectionPage` 的 `RunningServersList`、`KnownServersList`、`SelectedArguments` 等使用普通 `ItemsControl` + `StackPanel`，大数据量下一次性实例化所有子项。
   - `ConfigEditorPage` 外层虽然启用虚拟化，但分类下的内层 `ItemsControl` 仍未虚拟化。
   - `NetworkMonitorPage` 的 `DataGrid` 未显式启用行/列虚拟化。
4. **视觉树冗余**：
   - `ServerDetectionPage` 每个服务器项叠加 3 个 `Ellipse` 做状态切换。
   - `ModernNavListBoxItemStyle` 模板内嵌套 5 个 `Border` 与发光效果。
   - `MainWindow.xaml` 设置了无意义的 `Window.RenderTransform`。
5. **Viewport3D 高开销**：`NetworkMonitorPage` 使用 `Viewport3D` 做端口可视化，`MeshGeometry3D` 每帧重绘且 `IsHitTestVisible="False"` 仍参与布局。
6. **Bitmap Caching 滥用/误用**：`MainContent` `ContentControl` 在内容切换时缓存会失效重建；`NavSidebar` 仅显示 56px 宽但缓存 240px 区域。
7. **动画设置未全局生效**：`SettingsViewModel.EnableAnimations` 与 `AnimationDuration` 仅影响 `MainWindow` 页面切换和 `GaugeRingControl`，大量 XAML 内联动画与 `EventTrigger` 不受控制。
8. **集合更新无节流**：`TrendChartControl` 每次集合变更都 `InvalidateVisual`；`ColorPickerControl` 滑块拖动时高频更新 `SelectedColor`。
9. **主题服务资源抖动**：`SettingsViewModel` 每次颜色变化都调用 `ThemeService.ApplyTheme()`，全量重建并替换所有 Brush，导致全窗口重绘。

---

## 文件变更总览

| 文件 | 操作 | 说明 |
|------|------|------|
| `src/McServerGuard/Themes/AppResources.xaml` | 修改 | Freeze 所有资源、简化导航模板、优化动画 |
| `src/McServerGuard/App.xaml` | 修改 | 添加全局渲染选项、StaticResource 优化 |
| `src/McServerGuard/Views/MainWindow.xaml` | 修改 | 移除无用 RenderTransform、优化缓存、全局 UseLayoutRounding |
| `src/McServerGuard/Views/MainWindow.xaml.cs` | 修改 | 接入全局动画开关、动画帧率控制 |
| `src/McServerGuard/Views/ServerDetectionPage.xaml` | 修改 | 列表虚拟化、状态指示器简化、Loading 独立动画 |
| `src/McServerGuard/Views/ConfigEditorPage.xaml` | 修改 | 内层虚拟化、移除卡片 Loaded 动画、Loading 独立动画 |
| `src/McServerGuard/Views/NetworkMonitorPage.xaml` | 修改 | 替换/禁用 Viewport3D、DataGrid 虚拟化、Tab 延迟加载 |
| `src/McServerGuard/Views/SystemMonitorPage.xaml` | 修改 | 监控数据节流入口 |
| `src/McServerGuard/Views/SettingsPage.xaml` | 修改 | 颜色预设 BitmapCache、图片占位 |
| `src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml` | 创建 | 基于独立动画的旋转 Loading 控件 |
| `src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml.cs` | 创建 | 动画控制与全局开关适配 |
| `src/McServerGuard/Views/Controls/TrendChartControl.cs` | 修改 | 数据更新节流、批量渲染 |
| `src/McServerGuard/Views/Controls/ColorPickerControl.xaml.cs` | 修改 | 滑块值节流 |
| `src/McServerGuard/Views/Helpers/AnimationHelper.cs` | 修改 | 使用全局缓存缓动函数、接入动画开关 |
| `src/McServerGuard/Services/ThemeService.cs` | 修改 | 批量更新、Brush 复用、减少全量重绘 |
| `src/McServerGuard/ViewModels/SettingsViewModel.cs` | 修改 | 批量应用主题、防抖 |
| `src/McServerGuard/Services/AnimationSettings.cs` | 创建 | 全局动画配置静态访问 |

---

## Task 1: Freeze 全局资源并统一渲染选项

**Files:**
- Modify: `src/McServerGuard/Themes/AppResources.xaml`
- Modify: `src/McServerGuard/App.xaml`
- Modify: `src/McServerGuard/Views/MainWindow.xaml`

**目标：** 所有 `Freezable` 资源在 XAML 中标记 `Freeze="True"`，全局开启 `UseLayoutRounding` 与 `SnapsToDevicePixels`，减少布局抖动与资源复制。

- [ ] **Step 1.1: Freeze AppResources.xaml 中的缓动函数与故事板**

  将：
  ```xml
  <CubicEase x:Key="StandardEase" EasingMode="EaseOut" />
  <QuarticEase x:Key="EmphasizedEase" EasingMode="EaseOut" />
  ```
  改为：
  ```xml
  <CubicEase x:Key="StandardEase" EasingMode="EaseOut" Freeze="True" />
  <QuarticEase x:Key="EmphasizedEase" EasingMode="EaseOut" Freeze="True" />
  ```

  将 `PageEnterStoryboard` 标记为可 Freeze：
  ```xml
  <Storyboard x:Key="PageEnterStoryboard" Freeze="True">
      <DoubleAnimation ... EasingFunction="{StaticResource StandardEase}" />
      <DoubleAnimation ... EasingFunction="{StaticResource EmphasizedEase}" />
  </Storyboard>
  ```

- [ ] **Step 1.2: Freeze 所有 Brush 与 GradientBrush**

  将所有 `SolidColorBrush` 与 `LinearGradientBrush` 添加 `Freeze="True"`：
  ```xml
  <SolidColorBrush x:Key="CardBackgroundBrush" Color="#0F172A" Freeze="True" />
  <LinearGradientBrush x:Key="AccentGradientBrush" StartPoint="0,0" EndPoint="1,0" Freeze="True">
      <GradientStop Color="#60A5FA" Offset="0" />
      <GradientStop Color="#3B82F6" Offset="1" />
  </LinearGradientBrush>
  ```

  > 注意：被 `DynamicResource` 引用的 Brush 仍可 Freeze；Freeze 后不可再修改，但主题服务会在切换时替换为新 Brush，符合当前设计。

- [ ] **Step 1.3: 在 App.xaml 添加全局渲染选项**

  修改 `Application` 根节点，添加：
  ```xml
  <Application ...
               TextOptions.TextFormattingMode="Display"
               TextOptions.TextRenderingMode="ClearType"
               UseLayoutRounding="True">
  ```

  将 `MainWindow.xaml` 中重复且为默认值的 `TextOptions` 与 `Window.RenderTransform` 移除：
  ```xml
  <!-- 删除 -->
  <Window.RenderTransform>
      <ScaleTransform ScaleX="1" ScaleY="1" CenterX="0.5" CenterY="0.5" />
  </Window.RenderTransform>
  <!-- 删除 TextOptions.TextFormattingMode / TextOptions.TextRenderingMode -->
  ```

- [ ] **Step 1.4: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded with no warnings about frozen resources.

- [ ] **Step 1.5: Commit**

  ```bash
  git add src/McServerGuard/Themes/AppResources.xaml src/McServerGuard/App.xaml src/McServerGuard/Views/MainWindow.xaml
  git commit -m "perf: freeze global resources and unify render options"
  ```

---

## Task 2: 创建全局动画配置与缓动函数缓存

**Files:**
- Create: `src/McServerGuard/Services/AnimationSettings.cs`
- Modify: `src/McServerGuard/Views/Helpers/AnimationHelper.cs`
- Modify: `src/McServerGuard/Services/ThemeService.cs`

**目标：** 让所有代码动画共享同一组 Freeze 的缓动函数，并从主题服务读取全局开关与时长。

- [ ] **Step 2.1: 创建 AnimationSettings 静态访问类**

  Create `src/McServerGuard/Services/AnimationSettings.cs`:
  ```csharp
  namespace McServerGuard.Services;

  using System.Windows.Media;
  using System.Windows.Media.Animation;

  public static class AnimationSettings
  {
      private static readonly CubicEase StandardEase = new() { EasingMode = EasingMode.EaseOut };
      private static readonly QuarticEase EmphasizedEase = new() { EasingMode = EasingMode.EaseOut };
      private static readonly QuarticEase EmphasizedEaseIn = new() { EasingMode = EasingMode.EaseIn };

      static AnimationSettings()
      {
          StandardEase.Freeze();
          EmphasizedEase.Freeze();
          EmphasizedEaseIn.Freeze();
      }

      public static IEasingFunction Standard => StandardEase;
      public static IEasingFunction Emphasized => EmphasizedEase;
      public static IEasingFunction EmphasizedIn => EmphasizedEaseIn;

      public static IThemeService? ThemeService { get; set; }

      public static bool AnimationsEnabled => ThemeService?.EnableAnimations ?? true;

      public static int DurationMs(int baseMs)
      {
          var configured = ThemeService?.AnimationDuration ?? baseMs;
          return AnimationsEnabled ? configured : 0;
      }
  }
  ```

- [ ] **Step 2.2: 在 App 初始化时注入 ThemeService**

  修改 `src/McServerGuard/App.xaml.cs`（假设已有服务定位逻辑），在 `OnStartup` 或容器构建后设置：
  ```csharp
  AnimationSettings.ThemeService = Services.GetRequiredService<IThemeService>();
  ```

- [ ] **Step 2.3: 重构 AnimationHelper 使用共享缓动函数**

  修改 `src/McServerGuard/Views/Helpers/AnimationHelper.cs`，移除每次创建 `new CubicEase()` 的代码，替换为 `AnimationSettings.Standard` / `AnimationSettings.Emphasized`，并在方法入口处根据 `AnimationSettings.AnimationsEnabled` 提前返回。

  示例：
  ```csharp
  public static void FadeAndSlideIn(UIElement element, int durationMs, double slideDistance = 20)
  {
      if (!AnimationSettings.AnimationsEnabled || durationMs <= 0)
      {
          element.Opacity = 1;
          if (element.RenderTransform is TranslateTransform t) t.Y = 0;
          return;
      }
      // ... 使用 AnimationSettings.Standard
  }
  ```

- [ ] **Step 2.4: ThemeService 属性变更时通知 AnimationSettings**

  在 `ThemeService.cs` 的 `EnableAnimations` 与 `AnimationDuration` setter 中，无需额外代码（因为 `AnimationSettings` 直接读取 `ThemeService` 实例）。

- [ ] **Step 2.5: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 2.6: Commit**

  ```bash
  git add src/McServerGuard/Services/AnimationSettings.cs src/McServerGuard/Views/Helpers/AnimationHelper.cs src/McServerGuard/App.xaml.cs
  git commit -m "perf: add global animation settings and shared easing cache"
  ```

---

## Task 3: 创建基于独立动画的 Loading 控件

**Files:**
- Create: `src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml`
- Create: `src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml.cs`

**目标：** 将 `PackIcon` 的 `RotateTransform.Angle` 依赖动画改为 `RenderOptions` 可硬件加速的独立动画，避免 UI 线程插值。

- [ ] **Step 3.1: 创建 IndependentLoadingIcon XAML**

  Create `src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml`:
  ```xml
  <UserControl x:Class="McServerGuard.Views.Controls.IndependentLoadingIcon"
               xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
               x:Name="Root">
      <materialDesign:PackIcon Kind="Loading"
                               Width="{Binding ElementName=Root, Path=IconSize}"
                               Height="{Binding ElementName=Root, Path=IconSize}"
                               Foreground="{Binding ElementName=Root, Path=Foreground}"
                               RenderTransformOrigin="0.5,0.5"
                               RenderOptions.EdgeMode="Aliased">
          <materialDesign:PackIcon.RenderTransform>
              <RotateTransform x:Name="SpinnerRotate" Angle="0" />
          </materialDesign:PackIcon.RenderTransform>
      </materialDesign:PackIcon>
  </UserControl>
  ```

- [ ] **Step 3.2: 创建代码隐藏控制动画**

  Create `src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml.cs`:
  ```csharp
  namespace McServerGuard.Views.Controls;

  using System.Windows;
  using System.Windows.Controls;
  using System.Windows.Media;
  using System.Windows.Media.Animation;
  using McServerGuard.Services;

  public partial class IndependentLoadingIcon : UserControl
  {
      private readonly RotateTransform _rotateTransform;
      private Storyboard? _storyboard;

      public IndependentLoadingIcon()
      {
          InitializeComponent();
          _rotateTransform = (RotateTransform)SpinnerRotate;
          Loaded += OnLoaded;
          Unloaded += OnUnloaded;
      }

      public static readonly DependencyProperty IconSizeProperty =
          DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(IndependentLoadingIcon),
              new PropertyMetadata(48.0));

      public double IconSize
      {
          get => (double)GetValue(IconSizeProperty);
          set => SetValue(IconSizeProperty, value);
      }

      private void OnLoaded(object sender, RoutedEventArgs e)
      {
          if (!AnimationSettings.AnimationsEnabled)
              return;

          _storyboard = new Storyboard();
          var anim = new DoubleAnimation
          {
              From = 0,
              To = 360,
              Duration = TimeSpan.FromSeconds(1),
              RepeatBehavior = RepeatBehavior.Forever
          };
          Storyboard.SetTarget(anim, _rotateTransform);
          Storyboard.SetTargetProperty(anim, new PropertyPath(RotateTransform.AngleProperty));
          _storyboard.Children.Add(anim);
          _storyboard.Begin(this, true);
      }

      private void OnUnloaded(object sender, RoutedEventArgs e)
      {
          _storyboard?.Stop();
          _storyboard = null;
      }
  }
  ```

- [ ] **Step 3.3: 在目标页面替换 Loading 图标**

  替换 `ServerDetectionPage.xaml` 第 825 行与 `ConfigEditorPage.xaml` 第 632 行的 `<materialDesign:PackIcon Kind="Loading" ...>` 为：
  ```xml
  <controls:IndependentLoadingIcon IconSize="48" Foreground="White" />
  ```
  （`ConfigEditorPage` 使用 `Foreground="{DynamicResource PrimaryHueMidBrush}"`）

- [ ] **Step 3.4: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 3.5: Commit**

  ```bash
  git add src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml.cs src/McServerGuard/Views/ServerDetectionPage.xaml src/McServerGuard/Views/ConfigEditorPage.xaml
  git commit -m "perf: replace dependent rotation animations with independent loading spinner"
  ```

---

## Task 4: ServerDetectionPage 列表虚拟化与状态指示器简化

**Files:**
- Modify: `src/McServerGuard/Views/ServerDetectionPage.xaml`

**目标：** 将服务器列表改为虚拟化布局，状态点从 3 层 Ellipse 简化为 1 个 Ellipse + Brush 切换动画。

- [ ] **Step 4.1: 为 Running/Known 服务器列表启用虚拟化**

  将：
  ```xml
  <ItemsControl x:Name="RunningServersList" ItemsSource="{Binding FilteredRunningServers}" Margin="0,4,0,0">
  ```
  改为：
  ```xml
  <ItemsControl x:Name="RunningServersList"
                ItemsSource="{Binding FilteredRunningServers}"
                Margin="0,4,0,0"
                VirtualizingStackPanel.IsVirtualizing="True"
                VirtualizingStackPanel.VirtualizationMode="Recycling"
                ScrollViewer.CanContentScroll="True">
      <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
              <VirtualizingStackPanel IsVirtualizing="True" VirtualizationMode="Recycling" />
          </ItemsPanelTemplate>
      </ItemsControl.ItemsPanel>
  </ItemsControl>
  ```

  对 `KnownServersList` 做同样修改。

- [ ] **Step 4.2: 简化状态点为单 Ellipse + Brush 动画**

  替换运行中项的状态点 Grid：
  ```xml
  <Grid Grid.Column="0" Width="8" Height="8" VerticalAlignment="Center" Margin="0,0,6,0">
      <Ellipse x:Name="GreenDot" Fill="{DynamicResource GaugeGreenBrush}" Opacity="1" />
      <Ellipse x:Name="YellowDot" Fill="{DynamicResource GaugeYellowBrush}" Opacity="0" />
      <Ellipse x:Name="RedDot" Fill="{DynamicResource GaugeRedBrush}" Opacity="0" />
  </Grid>
  ```
  为：
  ```xml
  <Ellipse x:Name="StatusDot"
           Grid.Column="0"
           Width="8" Height="8"
           VerticalAlignment="Center"
           Margin="0,0,6,0"
           Fill="{DynamicResource GaugeGreenBrush}"
           RenderOptions.EdgeMode="Aliased" />
  ```

  替换对应的 `DataTrigger`：
  ```xml
  <DataTrigger Binding="{Binding IsPortOpen}" Value="False">
      <DataTrigger.EnterActions>
          <BeginStoryboard>
              <Storyboard Duration="0:0:0.3">
                  <ObjectAnimationUsingKeyFrames Storyboard.TargetName="StatusDot"
                                                 Storyboard.TargetProperty="Fill">
                      <DiscreteObjectKeyFrame KeyTime="0:0:0"
                                              Value="{DynamicResource GaugeRedBrush}" />
                  </ObjectAnimationUsingKeyFrames>
              </Storyboard>
          </BeginStoryboard>
      </DataTrigger.EnterActions>
  </DataTrigger>
  <DataTrigger Binding="{Binding PortConflict}" Value="True">
      <DataTrigger.EnterActions>
          <BeginStoryboard>
              <Storyboard Duration="0:0:0.3">
                  <ObjectAnimationUsingKeyFrames Storyboard.TargetName="StatusDot"
                                                 Storyboard.TargetProperty="Fill">
                      <DiscreteObjectKeyFrame KeyTime="0:0:0"
                                              Value="{DynamicResource GaugeYellowBrush}" />
                  </ObjectAnimationUsingKeyFrames>
              </Storyboard>
          </BeginStoryboard>
      </DataTrigger.EnterActions>
  </DataTrigger>
  ```

- [ ] **Step 4.3: DataGrid 启用虚拟化**

  在 `NetworkMonitorPage` 的 DataGrid 与 `ServerDetectionPage` 如有 DataGrid 处添加：
  ```xml
  EnableRowVirtualization="True"
  EnableColumnVirtualization="True"
  VirtualizingStackPanel.VirtualizationMode="Recycling"
  ```

- [ ] **Step 4.4: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 4.5: Commit**

  ```bash
  git add src/McServerGuard/Views/ServerDetectionPage.xaml
  git commit -m "perf: virtualize server lists and simplify status indicator"
  ```

---

## Task 5: ConfigEditorPage 内层虚拟化与移除卡片入场动画

**Files:**
- Modify: `src/McServerGuard/Views/ConfigEditorPage.xaml`

**目标：** 每个分类下的配置项列表启用虚拟化，移除每个卡片 `Loaded` 触发的入场动画，避免大量项目同时动画。

- [ ] **Step 5.1: 移除 ConfigItemCardStyle 的 Loaded 入场动画**

  删除 `ConfigItemCardStyle` 中的：
  ```xml
  <EventTrigger RoutedEvent="Loaded">
      <BeginStoryboard>
          <Storyboard>
              <DoubleAnimation Storyboard.TargetProperty="(UIElement.Opacity)"
                               From="0" To="1"
                               Duration="0:0:0.15"
                               EasingFunction="{StaticResource ConfigCardEase}" />
          </Storyboard>
      </BeginStoryboard>
  </EventTrigger>
  ```

- [ ] **Step 5.2: 分类内 ItemsControl 启用虚拟化**

  将分类下的：
  ```xml
  <ItemsControl ItemsSource="{Binding Items}" Margin="4,8,4,8">
  ```
  改为：
  ```xml
  <ItemsControl ItemsSource="{Binding Items}"
                Margin="4,8,4,8"
                VirtualizingStackPanel.IsVirtualizing="True"
                VirtualizingStackPanel.VirtualizationMode="Recycling"
                ScrollViewer.CanContentScroll="True">
      <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
              <VirtualizingStackPanel IsVirtualizing="True" VirtualizationMode="Recycling" />
          </ItemsPanelTemplate>
      </ItemsControl.ItemsPanel>
  </ItemsControl>
  ```

- [ ] **Step 5.3: Expander 默认折叠或按需保持展开**

  如配置项数量较多（>30 项），将：
  ```xml
  <Expander IsExpanded="True" ...>
  ```
  改为：
  ```xml
  <Expander IsExpanded="False" ...>
  ```
  并在 ViewModel 中添加命令支持一次性展开/折叠所有分类（如需要）。

- [ ] **Step 5.4: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 5.5: Commit**

  ```bash
  git add src/McServerGuard/Views/ConfigEditorPage.xaml
  git commit -m "perf: virtualize config entries and remove per-card load animations"
  ```

---

## Task 6: NetworkMonitorPage 移除 Viewport3D 并启用虚拟化

**Files:**
- Modify: `src/McServerGuard/Views/NetworkMonitorPage.xaml`

**目标：** 移除或替换高消耗的 `Viewport3D` 可视化，启用 DataGrid 虚拟化，TabControl 内容延迟加载。

- [ ] **Step 6.1: 移除 Viewport3D 或替换为轻量 2D 可视化**

  方案 A（推荐）：删除整个 `<Viewport3D Grid.Row="1" ...>` 块，右侧卡片仅保留标题与图例。

  方案 B：如需保留可视化，创建基于 `DrawingVisual` 的轻量 2D 柱状图控件替代。

  本任务先实施方案 A，删除：
  ```xml
  <Viewport3D Grid.Row="1" ClipToBounds="False" IsHitTestVisible="False">
      ...
  </Viewport3D>
  ```

- [ ] **Step 6.2: DataGrid 启用行/列虚拟化**

  在所有 DataGrid 上添加：
  ```xml
  EnableRowVirtualization="True"
  EnableColumnVirtualization="True"
  VirtualizingStackPanel.VirtualizationMode="Recycling"
  ```

- [ ] **Step 6.3: TabControl 延迟加载内容**

  将 `TabControl` 替换为只在选中时加载内容的样式，或在每个 `TabItem` 使用 `x:Shared="False"` 与 `Visibility` 控制。更简单的方式是在 ViewModel 中按选中 Tab 懒加载数据。

- [ ] **Step 6.4: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 6.5: Commit**

  ```bash
  git add src/McServerGuard/Views/NetworkMonitorPage.xaml
  git commit -m "perf: remove Viewport3D and virtualize network monitor grids"
  ```

---

## Task 7: 自定义控件数据更新节流

**Files:**
- Modify: `src/McServerGuard/Views/Controls/TrendChartControl.cs`
- Modify: `src/McServerGuard/Views/Controls/ColorPickerControl.xaml.cs`

**目标：** 避免高频数据变化导致每帧重绘。

- [ ] **Step 7.1: TrendChartControl 集合变更节流**

  在 `TrendChartControl.cs` 中添加 `DispatcherTimer`：
  ```csharp
  private readonly DispatcherTimer _throttleTimer;
  private bool _pendingRedraw;

  public TrendChartControl()
  {
      ...
      _throttleTimer = new DispatcherTimer(DispatcherPriority.Render)
      {
          Interval = TimeSpan.FromMilliseconds(100)
      };
      _throttleTimer.Tick += (_, _) =>
      {
          _throttleTimer.Stop();
          if (_pendingRedraw)
          {
              _pendingRedraw = false;
              InvalidateVisual();
          }
      };
  }
  ```

  修改 `OnCollectionChanged`：
  ```csharp
  private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
      _pendingRedraw = true;
      if (!_throttleTimer.IsEnabled)
          _throttleTimer.Start();
  }
  ```

  在 `Unloaded` 时停止计时器并释放。

- [ ] **Step 7.2: ColorPickerControl 滑块节流**

  在 `ColorPickerControl.xaml.cs` 中，将 `RGB_Slider_ValueChanged` 改为：
  ```csharp
  private void RGB_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
  {
      if (_isUpdating) return;
      _pendingColorUpdate = true;
      _colorUpdateTimer.Stop();
      _colorUpdateTimer.Start();
  }
  ```

  使用一个 50ms 的 `DispatcherTimer` 在 Tick 时统一应用颜色，减少中间状态更新。

- [ ] **Step 7.3: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 7.4: Commit**

  ```bash
  git add src/McServerGuard/Views/Controls/TrendChartControl.cs src/McServerGuard/Views/Controls/ColorPickerControl.xaml.cs
  git commit -m "perf: throttle data updates in chart and color picker"
  ```

---

## Task 8: 主题服务批量更新与 Brush 复用

**Files:**
- Modify: `src/McServerGuard/Services/ThemeService.cs`
- Modify: `src/McServerGuard/ViewModels/SettingsViewModel.cs`

**目标：** 避免颜色滑动时每秒触发十几次全量主题重绘。

- [ ] **Step 8.1: ThemeService 添加批量更新模式**

  在 `ThemeService.cs` 中添加：
  ```csharp
  private bool _isBatchUpdating;

  public void BeginBatchUpdate() => _isBatchUpdating = true;
  public void EndBatchUpdate()
  {
      _isBatchUpdating = false;
      ApplyTheme();
  }
  ```

  修改各颜色 setter：
  ```csharp
  public Color PrimaryColor
  {
      get => _primaryColor;
      set
      {
          if (_primaryColor == value) return;
          _primaryColor = value;
          if (!_isBatchUpdating) ApplyTheme();
      }
  }
  ```
  对所有颜色属性做同样修改。

- [ ] **Step 8.2: SettingsViewModel 批量应用主题**

  修改 `ApplyTheme()` 命令：
  ```csharp
  [RelayCommand]
  private void ApplyTheme()
  {
      _themeService.BeginBatchUpdate();
      try
      {
          _themeService.PrimaryColor = PrimaryColor;
          _themeService.AccentColor = AccentColor;
          _themeService.BackgroundColor = BackgroundColor;
          _themeService.CardColor = CardColor;
          _themeService.TextColor = TextColor;
          _themeService.BorderColor = BorderColor;
          _themeService.CornerRadius = CornerRadius;
          _themeService.AnimationDuration = AnimationDuration;
          _themeService.EnableAnimations = EnableAnimations;
      }
      finally
      {
          _themeService.EndBatchUpdate();
      }
      StatusMessage = "主题已应用";
  }
  ```

- [ ] **Step 8.3: 颜色属性变更回调不再立即应用主题**

  在 `SettingsViewModel` 的 `OnPrimaryColorChanged` 等回调中，移除 `_themeService.PrimaryColor = value;`（因为实时预览应该通过 ViewModel 自身的 Brush 属性实现，只有点击"应用"时才写入 ThemeService）。

- [ ] **Step 8.4: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 8.5: Commit**

  ```bash
  git add src/McServerGuard/Services/ThemeService.cs src/McServerGuard/ViewModels/SettingsViewModel.cs
  git commit -m "perf: batch theme updates to reduce full-window redraws"
  ```

---

## Task 9: 简化导航项模板与 Bitmap Caching 策略

**Files:**
- Modify: `src/McServerGuard/Themes/AppResources.xaml`
- Modify: `src/McServerGuard/Views/MainWindow.xaml`
- Modify: `src/McServerGuard/Views/SettingsPage.xaml`

**目标：** 减少导航项视觉树深度；合理使用 `CacheMode`；设置页面颜色预设使用缓存。

- [ ] **Step 9.1: 简化 ModernNavListBoxItemStyle**

  移除 `SelectionGlowOuter` 发光 `Border`，将 `SelectedBg`、`SelectionIndicator`、`HoverBg` 合并为最多 3 个 `Border`。
  用 `SolidColorBrush` 替代 `RadialGradientBrush` 的 `OpacityMask`（或将发光效果删除）。

- [ ] **Step 9.2: 修正 MainWindow Bitmap Caching**

  移除 `MainContent` `ContentControl` 的 `CacheMode="BitmapCache"`（内容频繁切换时缓存失效成本高）。
  保留 `NavSidebar` 的 `CacheMode`，但仅在折叠状态时启用：
  ```xml
  <Border.CacheMode>
      <BitmapCache RenderAtScale="1.0" SnapsToDevicePixels="True" />
  </Border.CacheMode>
  ```

- [ ] **Step 9.3: 设置页面颜色预设 BitmapCache**

  在 `SettingsPage.xaml` 的 `ColorSwatchStyle` 中，为 `Bd` Border 添加：
  ```xml
  <Setter Property="CacheMode" Value="{x:Static BitmapCacheMode.BitmapCache}" />
  ```
  并为外层 `WrapPanel` 设置 `VirtualizingStackPanel.IsVirtualizing="True"`（如果未来预设数量继续增长）。

- [ ] **Step 9.4: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 9.5: Commit**

  ```bash
  git add src/McServerGuard/Themes/AppResources.xaml src/McServerGuard/Views/MainWindow.xaml src/McServerGuard/Views/SettingsPage.xaml
  git commit -m "perf: simplify nav template and tune bitmap caching strategy"
  ```

---

## Task 10: 内存泄漏防护与事件清理

**Files:**
- Modify: `src/McServerGuard/Views/MainWindow.xaml.cs`
- Modify: `src/McServerGuard/Views/Controls/TrendChartControl.cs`
- Modify: `src/McServerGuard/Views/Controls/IndependentLoadingIcon.xaml.cs`

**目标：** 确保页面切换、窗口关闭时释放动画、事件订阅与计时器。

- [ ] **Step 10.1: MainWindow 关闭时清理事件**

  在 `MainWindow_Closing` 或 `Closed` 中：
  ```csharp
  if (_vm is not null)
  {
      _vm.PropertyChanged -= Vm_PropertyChanged;
      _vm = null;
  }
  _collapseTimer.Stop();
  _collapseTimer.Tick -= CollapseTimer_Tick;
  ```

- [ ] **Step 10.2: TrendChartControl 卸载时清理集合监听**

  添加 `Unloaded` 处理：
  ```csharp
  private void OnUnloaded(object sender, RoutedEventArgs e)
  {
      if (_dataPoints is INotifyCollectionChanged ncc)
          ncc.CollectionChanged -= OnCollectionChanged;
      _dataPoints = null;
      _throttleTimer?.Stop();
  }
  ```

- [ ] **Step 10.3: 验证 IndependentLoadingIcon 已清理 Storyboard**

  确保 Task 3 中的 `OnUnloaded` 已调用 `_storyboard?.Stop()` 并置空。

- [ ] **Step 10.4: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 10.5: Commit**

  ```bash
  git add src/McServerGuard/Views/MainWindow.xaml.cs src/McServerGuard/Views/Controls/TrendChartControl.cs
  git commit -m "fix: clean up timers and event subscriptions to prevent leaks"
  ```

---

## Task 11: 全局动画开关接入 XAML 动画

**Files:**
- Modify: `src/McServerGuard/Views/MainWindow.xaml.cs`
- Modify: `src/McServerGuard/Themes/AppResources.xaml`

**目标：** 当用户关闭动画时，所有 XAML 内联动画也应被禁用或缩短为 0。

- [ ] **Step 11.1: 在 AppResources 中使用 ZeroDuration 样式作为禁用动画备选**

  定义一个附加属性或样式触发器比较复杂，简化方案：在 `MainWindow` 启动时遍历资源字典，将 `Storyboard.Duration` 为较短时间的动画在 `EnableAnimations=false` 时替换为 `0`。

  更实用的方案：在 `MainWindow_Loaded` 中调用：
  ```csharp
  Timeline.DesiredFrameRateProperty.OverrideMetadata(
      typeof(Timeline),
      new FrameworkPropertyMetadata { DefaultValue = AnimationSettings.AnimationsEnabled ? 60 : 1 });
  ```
  当禁用动画时，将全局动画帧率设为 1fps，视觉上等同于关闭动画，同时保留绑定与触发器逻辑。

- [ ] **Step 11.2: 将 Window 关闭动画也接入开关**

  `MainWindow_Closing` 中已有：
  ```csharp
  if (!_themeService.EnableAnimations) return;
  ```
  保持不变。

- [ ] **Step 11.3: 编译验证**

  Run: `dotnet build src/McServerGuard/McServerGuard.csproj`
  Expected: Build succeeded.

- [ ] **Step 11.4: Commit**

  ```bash
  git add src/McServerGuard/Views/MainWindow.xaml.cs
  git commit -m "perf: wire global animation switch into XAML animations via frame rate"
  ```

---

## Task 12: 性能回归验证

**Files:**
- Modify: `src/McServerGuard/Views/MainWindow.xaml`（临时调试用，完成后可回滚）

**目标：** 通过可观测指标确认优化生效。

- [ ] **Step 12.1: 添加临时 FPS 计数器（调试用）**

  在 `MainWindow.xaml` 底部状态栏右侧添加一个只读的 `TextBlock`，绑定到 `MainViewModel` 新增的 `FpsText` 属性。

  在 `MainViewModel.cs` 中添加：
  ```csharp
  private readonly DispatcherTimer _fpsTimer;
  private int _frameCount;

  [ObservableProperty]
  private string _fpsText = "60 FPS";

  public MainViewModel()
  {
      _fpsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
      _fpsTimer.Tick += (_, _) =>
      {
          FpsText = $"{_frameCount} FPS";
          _frameCount = 0;
      };
      _fpsTimer.Start();

      CompositionTarget.Rendering += (_, _) => Interlocked.Increment(ref _frameCount);
  }
  ```

  > 该调试代码完成后可保留为可选功能，或删除。

- [ ] **Step 12.2: 手动运行应用并验证关键场景**

  由于当前环境缺少 `dotnet` 命令，需在 Windows 开发机上执行：
  1. 启动应用，观察 FPS 计数器是否稳定在 58-60。
  2. 快速切换导航页 10 次，无卡顿。
  3. 打开配置编辑页，滚动长列表，FPS 不掉到 30 以下。
  4. 关闭设置页的"启用动画效果"，所有动画应立即停止或变为瞬切。

- [ ] **Step 12.3: 移除或保留 FPS 计数器**

  根据用户偏好决定是否保留。如保留，在 `MainViewModel` 中确保 `Unloaded` 时取消 `CompositionTarget.Rendering` 订阅。

- [ ] **Step 12.4: Commit**

  ```bash
  git add src/McServerGuard/Views/MainWindow.xaml src/McServerGuard/ViewModels/MainViewModel.cs
  git commit -m "test: add optional FPS counter for performance regression validation"
  ```

---

## 执行顺序与依赖

1. **Task 1 → Task 2 → Task 3** 是基础层，必须先完成，后续任务依赖 `AnimationSettings`。
2. **Task 3（IndependentLoadingIcon）** 完成后才能执行 **Task 4 / Task 5** 的替换。
3. **Task 4 / Task 5 / Task 6** 可并行执行。
4. **Task 7 / Task 8 / Task 9 / Task 10 / Task 11** 可并行执行。
5. **Task 12** 在所有任务完成后执行。

---

## 验证清单（最终）

- [ ] `dotnet build` 全项目成功。
- [ ] 应用启动后内存不再持续爬升。
- [ ] 配置编辑页加载 100+ 配置项时滚动流畅。
- [ ] 服务器列表包含 50+ 项时滚动与搜索无卡顿。
- [ ] 关闭"启用动画效果"后所有动画停止。
- [ ] 主题颜色滑动时界面不再频繁全局闪烁。
- [ ] 网络监控页不再因 Viewport3D 导致 GPU 占用飙升。
