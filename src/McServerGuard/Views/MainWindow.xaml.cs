// 🪟 主窗口 Code-Behind —— 启动淡入 + 页面切换动画
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using McServerGuard.Services;
using McServerGuard.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace McServerGuard.Views;

public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private MainViewModel? _vm;

    public MainWindow()
    {
        Log.Information("🏗️ MainWindow 正在初始化...");
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        MainContent.RenderTransform = new TranslateTransform();
        Loaded += MainWindow_Loaded;
        DataContextChanged += MainWindow_DataContextChanged;
        Log.Information("✅ MainWindow 初始化完成");
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= Vm_PropertyChanged;

        _vm = e.NewValue as MainViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += Vm_PropertyChanged;
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            AnimatePageTransition();
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var duration = _themeService.EnableAnimations ? _themeService.AnimationDuration : 0;

        if (duration > 0)
        {
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(duration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            MainContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
        else
        {
            MainContent.Opacity = 1;
        }
    }

    private void AnimatePageTransition()
    {
        if (!_themeService.EnableAnimations)
        {
            MainContent.Opacity = 1;
            if (MainContent.RenderTransform is TranslateTransform transform)
                transform.X = 0;
            return;
        }

        var duration = _themeService.AnimationDuration;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(duration * 0.8),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var slideIn = new DoubleAnimation
        {
            From = 20,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(duration),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        MainContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        if (MainContent.RenderTransform is TranslateTransform tt)
            tt.BeginAnimation(TranslateTransform.XProperty, slideIn);
    }
}
