// -----------------------------------------------------------------------------
// 文件名: ConfigEditorPage.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 配置编辑页面代码隐藏类，负责页面级入场动画的触发与控制。
//           页面级动画由代码隐藏动态创建以支持主题服务配置，
//           卡片级装饰性动画保留于 XAML 样式中。
// 依赖组件: PresentationFramework, System.Windows.Media.Animation
// 设计模式: 代码隐藏模式
// -----------------------------------------------------------------------------
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Views.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

/// <summary>
/// 配置编辑页面代码隐藏类。
/// 页面级入场动画由代码隐藏控制，动画参数跟随主题服务配置；
/// 卡片级装饰性入场动画在 XAML 样式中定义。
/// </summary>
public partial class ConfigEditorPage : UserControl
{
    private readonly IThemeService _themeService;
    private bool _animationPlayed;

    public ConfigEditorPage()
    {
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        Loaded += OnLoaded;
    }

    // 页面 Loaded 事件处理：首次加载时触入场动画，重复加载时直接显示
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
        });
    }
}
