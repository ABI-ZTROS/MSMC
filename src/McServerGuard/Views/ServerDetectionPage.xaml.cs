// -----------------------------------------------------------------------------
// 文件名: ServerDetectionPage.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 服务器检测页面代码隐藏类，负责页面入场动画控制及列表项交互事件处理。
//           列表项点击选中逻辑在此处理以避免复杂的绑定路由。
// 依赖组件: PresentationFramework, System.Windows.Input
// 设计模式: 代码隐藏模式
// -----------------------------------------------------------------------------
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

/// <summary>
/// 服务器检测页面代码隐藏类。
/// 负责页面级入场动画控制，以及运行中服务器列表、已知服务器列表的
/// 鼠标点击选中交互与搜索框清空操作。
/// </summary>
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

    // 页面 Loaded 事件处理：首次加载触发入场动画，重复加载直接显示
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 防止 Loaded 重复触发（Tab 切换/布局变化均会触发）导致页面反复被重置为透明
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
    /// 运行中服务器列表项鼠标左键释放事件处理：
    /// 从 FrameworkElement.Tag 中提取 ServerInstance 并设为当前选中项。
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
    /// 已知服务器列表项鼠标左键释放事件处理：
    /// 从 FrameworkElement.Tag 中提取 KnownServer 并设为当前选中项。
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
    /// 清除搜索按钮点击事件处理：清空搜索关键字文本。
    /// </summary>
    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ServerDetectionViewModel vm)
        {
            vm.SearchKeyword = string.Empty;
        }
    }
}
