// -----------------------------------------------------------------------------
// 文件名: MainWindow.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 主窗口代码隐藏类，实现自定义标题栏交互、鼠标悬停侧边栏展开/折叠动画、
//           页面切换过渡动画及关闭确认逻辑。
// 依赖组件: PresentationFramework, MaterialDesignThemes,
//           MahApps.Metro.IconPacks, System.Windows.Media
// 设计模式: 代码隐藏模式, 依赖属性
// -----------------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Services.ServerDetection;
using McServerGuard.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace McServerGuard.Views;

/// <summary>
/// 主窗口代码隐藏类。
/// 负责自定义标题栏拖动与最大化交互、侧边栏悬停展开/折叠动画、
/// 页面切换过渡效果以及关闭前服务器运行状态确认。
/// </summary>
public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private readonly IServerManagerService _serverManager;
    private MainViewModel? _vm;
    private readonly DispatcherTimer _collapseTimer;
    private bool _isSidebarExpanded;
    private bool _isClosing;

    public MainWindow()
    {
        Log.Information("🏗️ MainWindow 正在初始化...");
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        _serverManager = App.Services.GetRequiredService<IServerManagerService>();

        MainContent.RenderTransform = new TranslateTransform();

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _collapseTimer.Tick += CollapseTimer_Tick;

        Loaded += MainWindow_Loaded;
        DataContextChanged += MainWindow_DataContextChanged;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;

        Log.Information("✅ MainWindow 初始化完成");
    }

    // 窗口 Loaded 事件处理：初始化侧边栏折叠状态与文本透明度，播放入场动画
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isSidebarExpanded = false;
        SetTextElementsOpacity(0);

        MainContent.Opacity = 1;
    }

    // DataContext 变更事件处理：订阅/取消订阅 ViewModel 的 PropertyChanged 事件
    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= Vm_PropertyChanged;

        _vm = e.NewValue as MainViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += Vm_PropertyChanged;
    }

    // ViewModel 属性变更事件处理：当前页面切换时触发过渡动画
    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            AnimatePageTransition();
        }
    }

    /// <summary>
    /// 执行页面切换过渡动画，包含淡入与位移动画。
    /// 动画参数（时长、缓动函数）由主题服务统一配置。
    /// </summary>
    private void AnimatePageTransition()
    {
        if (!_themeService.EnableAnimations)
        {
            MainContent.Opacity = 1;
            if (MainContent.RenderTransform is TranslateTransform transform)
                transform.X = 0;
            return;
        }

        var duration = _themeService.AnimationDuration;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(duration * 0.8),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var slideIn = new DoubleAnimation
        {
            From = 20,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(duration),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        MainContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        if (MainContent.RenderTransform is TranslateTransform tt)
            tt.BeginAnimation(TranslateTransform.XProperty, slideIn);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 自定义标题栏交互：拖动移动 + 双击最大化/还原
    // ─────────────────────────────────────────────────────────────────────

    // 标题栏鼠标左键按下事件处理：双击切换最大化状态，单击拖动窗口
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>
    /// 切换窗口最大化与正常状态。
    /// </summary>
    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    // 最小化按钮点击事件处理
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    // 最大化按钮点击事件处理
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    // 关闭按钮点击事件处理
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // 窗口状态变更事件处理：同步最大化按钮图标
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeIcon != null)
        {
            MaximizeIcon.Kind = WindowState == WindowState.Maximized
                ? MahApps.Metro.IconPacks.PackIconFontAwesome6Kind.WindowRestoreSolid
                : MahApps.Metro.IconPacks.PackIconFontAwesome6Kind.WindowMaximizeSolid;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 侧边栏交互：鼠标悬停自动展开，延迟自动折叠
    // ─────────────────────────────────────────────────────────────────────

    // 侧边栏鼠标进入事件处理：停止折叠计时器并展开侧边栏
    private void NavSidebar_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        ExpandSidebar();
    }

    // 侧边栏鼠标离开事件处理：启动折叠计时器
    private void NavSidebar_MouseLeave(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    // 折叠计时器 Tick 事件处理：延迟后若鼠标已离开则折叠侧边栏
    private void CollapseTimer_Tick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (!NavSidebar.IsMouseOver)
        {
            CollapseSidebar();
        }
    }

    /// <summary>
    /// 展开侧边栏动画。通过 Width 属性动画驱动（56→240），
    /// 同时配合文本透明度渐变实现平滑过渡效果。
    /// </summary>
    private void ExpandSidebar()
    {
        if (_isSidebarExpanded) return;
        _isSidebarExpanded = true;

        var durationMs = _themeService.EnableAnimations ? 200 : 0;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            NavSidebar.UpdateLayout();
            if (durationMs > 0)
            {
                var widthAnim = new DoubleAnimation(240, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                };
                widthAnim.Completed += (_, _) =>
                {
                    NavSidebar.Width = 240;
                };
                NavSidebar.BeginAnimation(WidthProperty, widthAnim, HandoffBehavior.SnapshotAndReplace);
                AnimateTextOpacity(1, durationMs);
            }
            else
            {
                NavSidebar.Width = 240;
                SetTextElementsOpacity(1);
            }
        });
    }

    /// <summary>
    /// 折叠侧边栏动画。与展开动画对称，Width 从 240 收回 56。
    /// </summary>
    private void CollapseSidebar()
    {
        if (!_isSidebarExpanded) return;
        _isSidebarExpanded = false;

        var durationMs = _themeService.EnableAnimations ? 200 : 0;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            NavSidebar.UpdateLayout();
            if (durationMs > 0)
            {
                var widthAnim = new DoubleAnimation(56, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                };
                widthAnim.Completed += (_, _) =>
                {
                    NavSidebar.Width = 56;
                };
                NavSidebar.BeginAnimation(WidthProperty, widthAnim, HandoffBehavior.SnapshotAndReplace);
                AnimateTextOpacity(0, durationMs);
            }
            else
            {
                NavSidebar.Width = 56;
                SetTextElementsOpacity(0);
            }
        });
    }

    /// <summary>
    /// 直接设置侧边栏所有文本元素的不透明度。
    /// </summary>
    /// <param name="opacity">目标不透明度值（0.0 - 1.0）</param>
    private void SetTextElementsOpacity(double opacity)
    {
        NavHeaderText.Opacity = opacity;
        NavFooter.Opacity = opacity;
        NavItemText1.Opacity = opacity;
        NavItemText2.Opacity = opacity;
        NavItemText3.Opacity = opacity;
        NavItemText4.Opacity = opacity;
        NavItemText5.Opacity = opacity;
    }

    /// <summary>
    /// 以动画形式过渡侧边栏文本元素的不透明度。
    /// 使用 CubicEase 缓动函数实现自然的加速-减速曲线。
    /// </summary>
    /// <param name="toOpacity">目标不透明度</param>
    /// <param name="durationMs">动画时长（毫秒）</param>
    private void AnimateTextOpacity(double toOpacity, int durationMs)
    {
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        void Animate(UIElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(toOpacity, duration) { EasingFunction = ease });
        }

        Animate(NavHeaderText);
        Animate(NavFooter);
        Animate(NavItemText1);
        Animate(NavItemText2);
        Animate(NavItemText3);
        Animate(NavItemText4);
        Animate(NavItemText5);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 关闭确认：检测服务器运行状态，必要时弹出确认对话框
    // ─────────────────────────────────────────────────────────────────────

    // 窗口 Closing 事件处理：若存在运行中的服务器则弹出确认提示
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
            return;

        // 清理事件订阅和计时器，防止内存泄漏
        if (_vm is not null)
        {
            _vm.PropertyChanged -= Vm_PropertyChanged;
            _vm = null;
        }
        _collapseTimer.Stop();
        _collapseTimer.Tick -= CollapseTimer_Tick;

        if (_serverManager.AnyServerRunning())
        {
            var result = MessageBox.Show(
                "⚠️ 警告：关闭 MSMC 将导致正在运行的 Minecraft 服务器失去管理，可能直接崩溃或导致数据丢失、存档损坏。确定要关闭吗？",
                "确认关闭",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        if (!_themeService.EnableAnimations)
            return;

        e.Cancel = true;
        _isClosing = true;

        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var fadeOut = new DoubleAnimation(0, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        fadeOut.Completed += (_, _) =>
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, null);
            Close();
        };
        BeginAnimation(OpacityProperty, fadeOut, HandoffBehavior.SnapshotAndReplace);
    }
}
