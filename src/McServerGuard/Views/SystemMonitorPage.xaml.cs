// 🏎️ 系统监控页面的 Code-Behind —— LiveCharts 靠你撑场面
// 折线图的 namespace 都在 XAML 里声明好了，code-behind 就安安静静待着就好 📈
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Views.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

public partial class SystemMonitorPage : UserControl
{
    private readonly IThemeService _themeService;

    public SystemMonitorPage()
    {
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var duration = _themeService.EnableAnimations ? _themeService.AnimationDuration : 0;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            AnimationHelper.FadeAndSlideIn(this, duration);
        });
    }
}
