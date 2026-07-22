using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;
using McServerGuard.ViewModels;

namespace McServerGuard.Views;

public partial class NetworkMonitorPage : UserControl
{
    private NetworkMonitorViewModel? _viewModel;
    private Storyboard? _refreshStoryboard;

    public NetworkMonitorPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as NetworkMonitorViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            if (_viewModel.IsRefreshing)
                StartRefreshAnimation();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        StopRefreshAnimation();
        (_viewModel as IDisposable)?.Dispose();
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // IsRefreshing 现在在 UI 线程（DispatcherTimer 回调）设置，可直接操作 Storyboard
        if (e.PropertyName == nameof(NetworkMonitorViewModel.IsRefreshing) && _viewModel != null)
        {
            if (_viewModel.IsRefreshing)
                StartRefreshAnimation();
            else
                StopRefreshAnimation();
        }
    }

    private void StartRefreshAnimation()
    {
        if (_refreshStoryboard != null)
            return;

        _refreshStoryboard = new Storyboard();
        var anim = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(1),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(anim, RefreshRotateTransform);
        Storyboard.SetTargetProperty(anim, new PropertyPath(RotateTransform.AngleProperty));
        _refreshStoryboard.Children.Add(anim);
        _refreshStoryboard.Begin();
    }

    private void StopRefreshAnimation()
    {
        _refreshStoryboard?.Stop();
        _refreshStoryboard = null;
        RefreshRotateTransform.Angle = 0;
    }
}
