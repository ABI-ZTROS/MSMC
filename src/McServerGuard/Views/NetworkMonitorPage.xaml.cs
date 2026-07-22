using System.Windows;
using System.Windows.Controls;
using McServerGuard.ViewModels;

namespace McServerGuard.Views;

public partial class NetworkMonitorPage : UserControl
{
    private NetworkMonitorViewModel? _viewModel;

    public NetworkMonitorPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as NetworkMonitorViewModel;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        (_viewModel as IDisposable)?.Dispose();
        _viewModel = null;
    }
}
