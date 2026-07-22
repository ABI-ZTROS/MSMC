// -----------------------------------------------------------------------------
// 文件名: NetworkMonitorPage.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 网络监控页面的代码隐藏类，负责 ViewModel 生命周期管理。
// -----------------------------------------------------------------------------
using System;
using System.Windows.Controls;
using McServerGuard.ViewModels;

namespace McServerGuard.Views;

/// <summary>
/// 网络监控页面代码隐藏类。
/// DataContext 由 MainWindow 的 DataTemplate 自动注入 NetworkMonitorViewModel。
/// </summary>
public partial class NetworkMonitorPage : UserControl
{
    private NetworkMonitorViewModel? _viewModel;

    public NetworkMonitorPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel = DataContext as NetworkMonitorViewModel;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        (_viewModel as IDisposable)?.Dispose();
        _viewModel = null;
    }
}
