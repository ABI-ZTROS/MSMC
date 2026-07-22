namespace McServerGuard.Services;

using System.Windows.Media;
using System.Windows.Media.Animation;

public static class AnimationSettings
{
    private static readonly CubicEase StandardEase = new() { EasingMode = EasingMode.EaseOut };
    private static readonly QuarticEase EmphasizedEase = new() { EasingMode = EasingMode.EaseOut };
    private static readonly QuarticEase EmphasizedEaseIn = new() { EasingMode = EasingMode.EaseIn };

    static AnimationSettings()
    {
        StandardEase.Freeze();
        EmphasizedEase.Freeze();
        EmphasizedEaseIn.Freeze();
    }

    public static IEasingFunction Standard => StandardEase;
    public static IEasingFunction Emphasized => EmphasizedEase;
    public static IEasingFunction EmphasizedIn => EmphasizedEaseIn;

    public static IThemeService? ThemeService { get; set; }

    public static bool AnimationsEnabled => ThemeService?.EnableAnimations ?? true;

    public static int DurationMs(int baseMs)
    {
        var configured = ThemeService?.AnimationDuration ?? baseMs;
        return AnimationsEnabled ? configured : 0;
    }
}