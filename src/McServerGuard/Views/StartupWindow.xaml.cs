// -----------------------------------------------------------------------------
// 文件名: StartupWindow.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 启动等待窗口，显示启动日志、进度条，支持主题色跟随
// 设计模式: Observer（观察主题服务变更）
// -----------------------------------------------------------------------------
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using McServerGuard.Services;
using Serilog;

namespace McServerGuard.Views;

/// <summary>
/// 启动日志条目
/// </summary>
public class StartupLogEntry
{
    public string Message { get; set; } = string.Empty;
    public Brush ColorBrush { get; set; } = Brushes.LightGray;
}

/// <summary>
/// 启动等待窗口
/// </summary>
public partial class StartupWindow : Window
{
    private readonly IThemeService _themeService;
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// 日志条目集合
    /// </summary>
    public ObservableCollection<StartupLogEntry> LogEntries { get; } = new();

    /// <summary>
    /// 是否已失败
    /// </summary>
    public bool IsFailed { get; private set; }

    public StartupWindow(IThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;
        _dispatcher = Dispatcher;

        DataContext = this;

        // 订阅主题变更
        _themeService.ThemeChanged += OnThemeChanged;

        // 启动 Logo 呼吸动画（简单透明度动画，不用 Storyboard）
        StartLogoPulseAnimation();

        // 设置版本号
        VersionText.Text = $"v{GetVersion()}";

        Log.Information("🪟 StartupWindow 已创建");
    }

    private static string GetVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString(3) ?? "1.0.0";
    }

    private void StartLogoPulseAnimation()
    {
        // 简单透明度脉动，不用 Storyboard（减少动画系统开销）
        var anim = new DoubleAnimation
        {
            From = 0.06,
            To = 0.20,
            Duration = TimeSpan.FromSeconds(1.8),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        LogoGlow.BeginAnimation(OpacityProperty, anim);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // 主题色通过 DynamicResource 自动更新，无需额外处理
    }

    /// <summary>
    /// 追加日志
    /// </summary>
    public void AppendLog(string message, bool isError = false, bool isSuccess = false)
    {
        if (!_dispatcher.CheckAccess())
        {
            // 用 Background 优先级，不抢占渲染和输入消息
            _dispatcher.InvokeAsync(() => AppendLog(message, isError, isSuccess), DispatcherPriority.Background);
            return;
        }

        Brush brush;
        if (isError)
            brush = TryFindResource("DangerBrush") as Brush ?? Brushes.OrangeRed;
        else if (isSuccess)
            brush = TryFindResource("GaugeGreenBrush") as Brush ?? Brushes.LightGreen;
        else
            brush = TryFindResource("MaterialDesignBody") as Brush ?? Brushes.LightGray;

        LogEntries.Add(new StartupLogEntry
        {
            Message = message,
            ColorBrush = brush
        });

        // 自动滚动到底部
        LogScrollViewer.ScrollToEnd();
    }

    /// <summary>
    /// 设置进度（带丝滑动画）
    /// </summary>
    public void SetProgress(int percent, string status)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() => SetProgress(percent, status), DispatcherPriority.Background);
            return;
        }

        // 用动画平滑过渡到目标值
        var animation = new DoubleAnimation
        {
            To = Math.Clamp(percent, 0, 100),
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        StartupProgressBar.BeginAnimation(System.Windows.Controls.System.Windows.Controls.ProgressBar.ValueProperty, animation);
        StatusText.Text = status;
    }

    /// <summary>
    /// 更新状态文本
    /// </summary>
    public void UpdateStatus(string status)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() => UpdateStatus(status), DispatcherPriority.Background);
            return;
        }

        StatusText.Text = status;
    }

    /// <summary>
    /// 标记为失败状态
    /// </summary>
    public void MarkFailed(string errorMessage)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() => MarkFailed(errorMessage), DispatcherPriority.Background);
            return;
        }

        IsFailed = true;

        // 平滑动画到 100%
        var anim = new DoubleAnimation
        {
            To = 100,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        StartupProgressBar.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, anim);

        if (TryFindResource("DangerBrush") is Brush errorBrush)
        {
            StartupProgressBar.Foreground = errorBrush;
            StatusDot.Fill = errorBrush;
        }

        StatusText.Text = "启动失败";
        AppendLog($"❌ {errorMessage}", isError: true);
        CloseButton.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 标记为完成状态
    /// </summary>
    public void MarkCompleted()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(MarkCompleted, DispatcherPriority.Background);
            return;
        }

        // 平滑动画到 100%
        var anim = new DoubleAnimation
        {
            To = 100,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        StartupProgressBar.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, anim);
        StatusText.Text = "启动完成";
        AppendLog("✅ 初始化完成，正在启动主界面...", isSuccess: true);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // 启动失败时点击退出，直接关闭整个应用
        if (IsFailed)
        {
            System.Windows.Application.Current.Shutdown();
        }
        else
        {
            Close();
        }
    }

    private void WindowBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _themeService.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }
}
