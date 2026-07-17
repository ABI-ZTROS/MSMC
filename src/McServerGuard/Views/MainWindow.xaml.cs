// 🪟 主窗口 Code-Behind —— 自定义标题栏 + 鼠标悬停侧边栏 + 关闭确认
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

public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private readonly IServerManagerService _serverManager;
    private MainViewModel? _vm;
    private readonly DispatcherTimer _collapseTimer;
    private bool _isSidebarExpanded;

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

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isSidebarExpanded = false;
        SetTextElementsOpacity(0);

        MainContent.Opacity = 1;
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= Vm_PropertyChanged;

        _vm = e.NewValue as MainViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += Vm_PropertyChanged;
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            AnimatePageTransition();
        }
    }

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

    // ═══════════════════════════════════════════════════════════════
    // 🎨 自定义标题栏 —— 拖动 + 双击最大化
    // ═══════════════════════════════════════════════════════════════
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

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeIcon != null)
        {
            MaximizeIcon.Kind = WindowState == WindowState.Maximized
                ? MahApps.Metro.IconPacks.PackIconFontAwesome6Kind.WindowRestoreSolid
                : MahApps.Metro.IconPacks.PackIconFontAwesome6Kind.WindowMaximizeSolid;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 🧭 侧边栏 —— 鼠标悬停自动展开/折叠
    // ═══════════════════════════════════════════════════════════════
    private void NavSidebar_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        ExpandSidebar();
    }

    private void NavSidebar_MouseLeave(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void CollapseTimer_Tick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (!NavSidebar.IsMouseOver)
        {
            CollapseSidebar();
        }
    }

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

    private void SetTextElementsOpacity(double opacity)
    {
        NavHeaderText.Opacity = opacity;
        NavFooter.Opacity = opacity;
        NavItemText1.Opacity = opacity;
        NavItemText2.Opacity = opacity;
        NavItemText3.Opacity = opacity;
        NavItemText5.Opacity = opacity;
    }

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
        Animate(NavItemText5);
    }

    // ═══════════════════════════════════════════════════════════════
    // ⚠️ 关闭确认 —— 若有服务器在运行则弹窗警告
    // ═══════════════════════════════════════════════════════════════
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
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
            }
        }
    }
}
