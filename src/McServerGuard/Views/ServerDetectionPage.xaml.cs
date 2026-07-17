// 🔍 服务器检测页面的 Code-Behind
// 页面级入场动画由 code-behind 控制，跟随 ThemeService 设置
// 列表项点击事件也在此处理（避免复杂 binding 逻辑）
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using McServerGuard.Models;
using McServerGuard.Services;
using McServerGuard.ViewModels;
using McServerGuard.Views.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

public partial class ServerDetectionPage : UserControl
{
    private readonly IThemeService _themeService;
    private bool _animationPlayed;

    public ServerDetectionPage()
    {
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 防止 Loaded 重复触发（Tab 切换/布局变化都会触发）导致页面被反复设为透明
        if (_animationPlayed)
        {
            Opacity = 1;
            return;
        }
        _animationPlayed = true;

        var duration = _themeService.EnableAnimations ? _themeService.AnimationDuration : 0;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            AnimationHelper.FadeAndSlideInFromLeft(this, duration);
        });
    }

    /// <summary>
    /// 🖱️ 点击运行中服务器项 → 设为当前选中
    /// </summary>
    private void RunningItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe &&
            fe.Tag is ServerInstance server &&
            DataContext is ServerDetectionViewModel vm)
        {
            vm.SelectedServer = server;
        }
    }

    /// <summary>
    /// 🖱️ 点击已知服务器项 → 设为当前选中
    /// </summary>
    private void KnownItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe &&
            fe.Tag is KnownServer server &&
            DataContext is ServerDetectionViewModel vm)
        {
            vm.SelectedKnownServer = server;
        }
    }

    /// <summary>
    /// 🧹 清空搜索关键字
    /// </summary>
    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ServerDetectionViewModel vm)
        {
            vm.SearchKeyword = string.Empty;
        }
    }
}
