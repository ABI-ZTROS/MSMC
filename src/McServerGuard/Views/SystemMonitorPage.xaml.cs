// -----------------------------------------------------------------------------
// 文件名: SystemMonitorPage.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 系统监控页面代码隐藏类，负责页面级入场动画的触发。
//           折线图控件及命名空间声明均位于 XAML 中。
// 依赖组件: PresentationFramework, System.Windows.Media.Animation
// 设计模式: 代码隐藏模式
// -----------------------------------------------------------------------------
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Views.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

/// <summary>
/// 系统监控页面代码隐藏类。
/// 折线图控件及其数据绑定在 XAML 中声明，代码隐藏负责页面入场动画控制
/// 以及卡片元素的错落入场动画。
/// </summary>
public partial class SystemMonitorPage : UserControl
{
    private readonly IThemeService _themeService;
    private bool _animationPlayed;

    public SystemMonitorPage()
    {
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        Loaded += OnLoaded;
    }

    // 页面 Loaded 事件处理：首次加载触发入场动画，重复加载直接显示
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_animationPlayed)
        {
            Opacity = 1;
            return;
        }
        _animationPlayed = true;

        var duration = _themeService.EnableAnimations ? _themeService.AnimationDuration : 0;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            AnimationHelper.FadeAndSlideIn(this, duration);

            if (_themeService.EnableAnimations)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    PlayStaggeredEntrance();
                });
            }
        });
    }

    // 播放卡片错落入场动画
    private void PlayStaggeredEntrance()
    {
        int delay = 0;
        const int stepMs = 60;
        const int itemDurationMs = 300;

        AnimationHelper.FadeAndSlideInWithDelay(ControlButtonsPanel, itemDurationMs, delay);
        delay += stepMs;

        // 依次动画 4 个仪表盘卡片
        if (GaugesGrid != null)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(GaugesGrid); i++)
            {
                if (VisualTreeHelper.GetChild(GaugesGrid, i) is UIElement child)
                {
                    AnimationHelper.FadeAndSlideInWithDelay(child, itemDurationMs, delay);
                    delay += stepMs;
                }
            }
        }

        AnimationHelper.FadeAndSlideInWithDelay(CpuChartCard, itemDurationMs, delay);
        delay += stepMs;
        AnimationHelper.FadeAndSlideInWithDelay(MemoryChartCard, itemDurationMs, delay);
    }
}
