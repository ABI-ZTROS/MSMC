// -----------------------------------------------------------------------------
// 文件名: UserAgreementWindow.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 用户协议窗口代码隐藏类，实现协议阅读确认与用户同意状态管理
// 依赖组件: System.Windows, System.Windows.Threading, McServerGuard.Services, Microsoft.Extensions.DependencyInjection
// 设计模式: 视觉反馈机制、多实例通知系统、状态机流程控制
// -----------------------------------------------------------------------------
using System.Windows;
using System.Windows.Threading;
using McServerGuard.Services;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

/// <summary>
/// 用户协议窗口
/// </summary>
/// <remarks>
/// 提供用户协议的展示与确认交互功能，包含阅读倒计时、
/// 滚动到底部验证、同意状态持久化等核心机制。
/// 集成视觉警示动画与多实例提示窗口系统，
/// 用于强化用户对协议重要性的认知。
/// </remarks>
public partial class UserAgreementWindow : Window
{
    /// <summary>用户协议服务接口，负责协议同意状态的持久化管理</summary>
    private readonly IUserAgreementService _userAgreementService;

    /// <summary>倒计时计时器，控制用户最小阅读时长</summary>
    private readonly DispatcherTimer _countdownTimer;

    /// <summary>剩余阅读秒数</summary>
    private int _remainingSeconds = 120;

    /// <summary>指示用户是否已滚动至协议底部</summary>
    private bool _hasScrolledToBottom = false;

    /// <summary>当前协议版本号</summary>
    private const string AgreementVersion = "2.0.0";

    /// <summary>视觉警示动画计时器，用于驱动窗口位置微扰动效果</summary>
    private readonly DispatcherTimer _shakeTimer;

    /// <summary>多实例提示窗口集合，用于批量展示通知信息</summary>
    private readonly List<Window> _trollWindows = [];

    /// <summary>视觉警示动画剩余执行时长（毫秒）</summary>
    private int _shakeRemainingMs;

    /// <summary>窗口原始水平位置坐标，用于动画结束后复位</summary>
    private double _originalLeft;

    /// <summary>窗口原始垂直位置坐标，用于动画结束后复位</summary>
    private double _originalTop;

    /// <summary>随机数生成器，用于通知窗口的位置分布计算</summary>
    private static readonly Random _random = new();

    /// <summary>
    /// 初始化用户协议窗口
    /// </summary>
    /// <remarks>
    /// 初始化服务依赖、倒计时计时器、视觉警示动画计时器，
    /// 并注册窗口加载事件处理程序。
    /// </remarks>
    public UserAgreementWindow()
    {
        InitializeComponent();
        _userAgreementService = App.Services.GetRequiredService<IUserAgreementService>();
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += CountdownTimer_Tick;
        _shakeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _shakeTimer.Tick += ShakeTimer_Tick;
        Loaded += UserAgreementWindow_Loaded;
    }

    /// <summary>
    /// 窗口加载事件处理程序
    /// </summary>
    /// <param name="sender">事件源对象</param>
    /// <param name="e">路由事件参数</param>
    /// <remarks>
    /// 初始化倒计时显示、启动倒计时、禁用同意按钮、
    /// 配置自定义标题栏拖动功能、注册滚动监听事件。
    /// </remarks>
    private void UserAgreementWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCountdownDisplay();
        _countdownTimer.Start();
        AgreeButton.IsEnabled = false;

        // 自定义标题栏拖动支持
        TitleBar.MouseLeftButtonDown += (_, _) => DragMove();

        var scrollViewer = FindScrollViewer(AgreementContent);
        if (scrollViewer != null)
        {
            scrollViewer.ScrollChanged += AgreementScrollViewer_ScrollChanged;
        }
    }

    /// <summary>
    /// 倒计时计时器 Tick 事件处理程序
    /// </summary>
    /// <param name="sender">事件源对象</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 每秒递减剩余阅读时间，时间归零时停止倒计时
    /// 并检查是否满足同意条件。
    /// </remarks>
    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        UpdateCountdownDisplay();

        if (_remainingSeconds <= 0)
        {
            _countdownTimer.Stop();
            CheckCanAgree();
        }
    }

    /// <summary>
    /// 协议内容滚动变更事件处理程序
    /// </summary>
    /// <param name="sender">事件源对象</param>
    /// <param name="e">滚动变更事件参数</param>
    /// <remarks>
    /// 检测滚动位置，当用户滚动至协议底部时
    /// 标记阅读状态并检查是否满足同意条件。
    /// </remarks>
    private void AgreementScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ScrollViewer sv)
        {
            if (sv.VerticalOffset >= sv.ScrollableHeight - 1)
            {
                _hasScrolledToBottom = true;
                CheckCanAgree();
            }
        }
    }

    /// <summary>
    /// 更新倒计时显示文本
    /// </summary>
    /// <remarks>
    /// 格式化剩余时间为分:秒格式，并根据阅读状态
    /// 更新滚动提示信息的可见性。
    /// </remarks>
    private void UpdateCountdownDisplay()
    {
        var minutes = _remainingSeconds / 60;
        var seconds = _remainingSeconds % 60;
        CountdownText.Text = $"请仔细阅读协议（{minutes:D2}:{seconds:D2}）";

        if (!_hasScrolledToBottom)
        {
            ScrollHintText.Text = "请滚动至协议底部";
            ScrollHintText.Visibility = Visibility.Visible;
        }
        else
        {
            ScrollHintText.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 检查是否满足同意协议的条件
    /// </summary>
    /// <remarks>
    /// 当阅读倒计时结束且用户已滚动至底部时，
    /// 启用同意按钮并更新提示文本。
    /// </remarks>
    private void CheckCanAgree()
    {
        if (_remainingSeconds <= 0 && _hasScrolledToBottom)
        {
            AgreeButton.IsEnabled = true;
            CountdownText.Text = "已阅读完毕，请选择是否同意";
        }
    }

    /// <summary>
    /// 在视觉树中查找 ScrollViewer 控件
    /// </summary>
    /// <param name="parent">父依赖对象</param>
    /// <returns>找到的 ScrollViewer 实例；未找到则返回 null</returns>
    /// <remarks>采用递归深度优先遍历策略在视觉树中搜索</remarks>
    private static System.Windows.Controls.ScrollViewer? FindScrollViewer(System.Windows.DependencyObject parent)
    {
        if (parent is System.Windows.Controls.ScrollViewer sv)
            return sv;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            var result = FindScrollViewer(child);
            if (result != null)
                return result;
        }
        return null;
    }

    /// <summary>
    /// 同意按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件源对象</param>
    /// <param name="e">路由事件参数</param>
    /// <remarks>
    /// 持久化用户同意状态，设置对话框结果为 true，
    /// 并关闭当前窗口。
    /// </remarks>
    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        _userAgreementService.SetAgreed(AgreementVersion);
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 不同意按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件源对象</param>
    /// <param name="e">路由事件参数</param>
    /// <remarks>
    /// 记录窗口初始位置，启动视觉警示动画，
    /// 并创建多实例提示窗口以强化用户认知。
    /// 同时禁用交互按钮防止重复触发。
    /// </remarks>
    private void DisagreeButton_Click(object sender, RoutedEventArgs e)
    {
        // 保存窗口初始位置坐标
        _originalLeft = Left;
        _originalTop = Top;
        _shakeRemainingMs = 5000;

        // 禁用交互按钮防止重复触发
        DisagreeButton.IsEnabled = false;
        AgreeButton.IsEnabled = false;

        // 启动视觉警示动画（与通知窗口并行执行）
        _shakeTimer.Start();

        // 创建并展示多实例提示窗口（非模态、随机分布、无标题栏样式）
        for (int i = 0; i < 40; i++)
        {
            var troll = CreateTrollWindow();
            _trollWindows.Add(troll);
            troll.Show();
        }
    }

    /// <summary>
    /// 创建单实例提示窗口
    /// </summary>
    /// <returns>配置完成的提示窗口实例</returns>
    /// <remarks>
    /// 在屏幕范围内随机分布窗口位置，采用置顶显示、
    /// 无标题栏、不可调整大小的样式配置。
    /// 用于多实例通知场景下的信息强化展示。
    /// </remarks>
    private Window CreateTrollWindow()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        const int w = 320;
        const int h = 160;

        var window = new Window
        {
            Title = "⚠️ 错误",
            Width = w,
            Height = h,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = _random.Next(0, (int)(screenWidth - w)),
            Top = _random.Next(0, (int)(screenHeight - h)),
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = false,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1a, 0x00, 0x00)),
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(2),
            BorderBrush = System.Windows.Media.Brushes.Red,
        };

        var panel = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(20),
            Orientation = System.Windows.Controls.Orientation.Horizontal
        };

        var icon = new System.Windows.Controls.TextBlock
        {
            Text = "❌",
            FontSize = 36,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };

        var textPanel = new System.Windows.Controls.StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var title = new System.Windows.Controls.TextBlock
        {
            Text = "没同意用户协议用你妈呢傻逼玩意???",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.OrangeRed,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var sub = new System.Windows.Controls.TextBlock
        {
            Text = "爱用就用不用给老子爬",
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap
        };

        textPanel.Children.Add(title);
        textPanel.Children.Add(sub);
        panel.Children.Add(icon);
        panel.Children.Add(textPanel);
        window.Content = panel;

        return window;
    }

    /// <summary>
    /// 视觉警示动画计时器 Tick 事件处理程序
    /// </summary>
    /// <param name="sender">事件源对象</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 通过随机偏移窗口位置实现微扰动视觉效果，
    /// 动画结束后复位窗口位置、关闭所有提示窗口，
    /// 并终止应用程序运行。
    /// </remarks>
    private void ShakeTimer_Tick(object? sender, EventArgs e)
    {
        _shakeRemainingMs -= 50;

        // 位置微扰动计算（±50px 范围内随机偏移）
        int offsetX = _random.Next(-50, 51);
        int offsetY = _random.Next(-50, 51);
        Left = _originalLeft + offsetX;
        Top = _originalTop + offsetY;

        // 动画执行时长耗尽，执行收尾流程
        if (_shakeRemainingMs <= 0)
        {
            _shakeTimer.Stop();

            // 复位窗口位置至初始坐标
            Left = _originalLeft;
            Top = _originalTop;

            // 关闭所有多实例提示窗口
            foreach (var w in _trollWindows)
            {
                try { w.Close(); } catch { }
            }
            _trollWindows.Clear();

            // 设置对话框结果并退出应用程序
            DialogResult = false;
            Application.Current.Shutdown();
        }
    }
}
