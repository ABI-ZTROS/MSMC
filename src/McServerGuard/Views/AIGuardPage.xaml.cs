// 🤖 AI 守护页面的 Code-Behind —— AI 在这里不会思考，思考在 Service 层
// 这个页面只负责"好看"，像模像样的那种好看 ✨
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Views.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

public partial class AIGuardPage : UserControl
{
    private readonly IThemeService _themeService;
    private bool _animationPlayed;

    public AIGuardPage()
    {
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        Loaded += OnLoaded;
    }

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
