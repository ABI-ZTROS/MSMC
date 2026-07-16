// 🎛️ 配置编辑页面的 Code-Behind
// 页面级入场动画由 code-behind 控制，跟随 ThemeService 设置
// 卡片级入场动画保留在 XAML Style 里（快速的视觉修饰）
using System.Windows;
using System.Windows.Controls;
using McServerGuard.Services;
using McServerGuard.Views.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

public partial class ConfigEditorPage : UserControl
{
    private readonly IThemeService _themeService;

    public ConfigEditorPage()
    {
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var duration = _themeService.EnableAnimations ? _themeService.AnimationDuration : 0;
        AnimationHelper.FadeAndSlideIn(this, duration);
    }
}
