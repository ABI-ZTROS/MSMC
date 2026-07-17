using System.Windows;
using System.Windows.Threading;
using McServerGuard.Services;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

public partial class UserAgreementWindow : Window
{
    private readonly IUserAgreementService _userAgreementService;
    private readonly DispatcherTimer _countdownTimer;
    private int _remainingSeconds = 120;
    private bool _hasScrolledToBottom = false;
    private const string AgreementVersion = "2.0.0";

    private readonly DispatcherTimer _shakeTimer;
    private readonly List<Window> _trollWindows = [];
    private int _shakeRemainingMs;
    private double _originalLeft;
    private double _originalTop;
    private static readonly Random _random = new();

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

    private void UserAgreementWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCountdownDisplay();
        _countdownTimer.Start();
        AgreeButton.IsEnabled = false;

        // 自定义标题栏拖动
        TitleBar.MouseLeftButtonDown += (_, _) => DragMove();

        var scrollViewer = FindScrollViewer(AgreementContent);
        if (scrollViewer != null)
        {
            scrollViewer.ScrollChanged += AgreementScrollViewer_ScrollChanged;
        }
    }

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

    private void CheckCanAgree()
    {
        if (_remainingSeconds <= 0 && _hasScrolledToBottom)
        {
            AgreeButton.IsEnabled = true;
            CountdownText.Text = "已阅读完毕，请选择是否同意";
        }
    }

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

    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        _userAgreementService.SetAgreed(AgreementVersion);
        DialogResult = true;
        Close();
    }

    private void DisagreeButton_Click(object sender, RoutedEventArgs e)
    {
        // 保存原始位置
        _originalLeft = Left;
        _originalTop = Top;
        _shakeRemainingMs = 5000;

        // 禁用按钮防止重复点击
        DisagreeButton.IsEnabled = false;
        AgreeButton.IsEnabled = false;

        // 先启动抖动（和弹窗同时执行）
        _shakeTimer.Start();

        // 弹出 20 个错误窗口（非模态，随机位置，无标题栏无关闭按钮）
        for (int i = 0; i < 20; i++)
        {
            var troll = CreateTrollWindow();
            _trollWindows.Add(troll);
            troll.Show();
        }
    }

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
            Text = "没同意用户协议你你妈呢傻逼玩意???",
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

    private void ShakeTimer_Tick(object? sender, EventArgs e)
    {
        _shakeRemainingMs -= 50;

        // 随机偏移抖动（±50px 大幅度）
        int offsetX = _random.Next(-50, 51);
        int offsetY = _random.Next(-50, 51);
        Left = _originalLeft + offsetX;
        Top = _originalTop + offsetY;

        // 抖动时间到
        if (_shakeRemainingMs <= 0)
        {
            _shakeTimer.Stop();

            // 恢复位置
            Left = _originalLeft;
            Top = _originalTop;

            // 关闭所有恶作剧窗口
            foreach (var w in _trollWindows)
            {
                try { w.Close(); } catch { }
            }
            _trollWindows.Clear();

            // 关闭主窗口 + 退出程序
            DialogResult = false;
            Application.Current.Shutdown();
        }
    }
}
