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
    private const string AgreementVersion = "1.0.0";

    public UserAgreementWindow()
    {
        InitializeComponent();
        _userAgreementService = App.Services.GetRequiredService<IUserAgreementService>();
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += CountdownTimer_Tick;
        Loaded += UserAgreementWindow_Loaded;
    }

    private void UserAgreementWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCountdownDisplay();
        _countdownTimer.Start();
        AgreeButton.IsEnabled = false;

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
        var result = MessageBox.Show(
            "如果您不同意本协议，软件将无法继续使用。确定要退出吗？",
            "确认退出",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            DialogResult = false;
            Application.Current.Shutdown();
        }
    }
}
