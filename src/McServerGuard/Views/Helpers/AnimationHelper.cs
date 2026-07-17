// 🎬 AnimationHelper —— 统一动画工具类
// 所有页面的入场/切换/淡入淡出动画都从这里走，时长跟随 ThemeService
// 为什么不用纯 XAML Storyboard？因为 Storyboard.Duration 是 Freezable 不能绑定，无法跟随设置 😅
namespace McServerGuard.Views.Helpers;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

/// <summary>
/// 动画工具类 —— 封装常用入场/过渡动画
/// 全部用 code-behind 动态创建 DoubleAnimation，时长从参数传入，天然跟随 ThemeService 设置
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// 淡入 + 从下方滑入（页面入场标准动画）
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="durationMs">动画时长（毫秒），<=0 时直接显示不播动画</param>
    /// <param name="slideDistance">滑动距离（像素）</param>
    public static void FadeAndSlideIn(UIElement element, int durationMs, double slideDistance = 20)
    {
        if (durationMs <= 0)
        {
            element.Opacity = 1;
            return;
        }

        element.Opacity = 0;
        var translate = new TranslateTransform(0, slideDistance);
        element.RenderTransform = translate;

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var opacityAnim = new DoubleAnimation(1, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        var yAnim = new DoubleAnimation(0, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        yAnim.Completed += (_, _) => translate.Y = 0;

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.YProperty, yAnim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 淡入 + 从左侧滑入（卡片/列表项入场）
    /// </summary>
    public static void FadeAndSlideInFromLeft(UIElement element, int durationMs, double slideDistance = 20)
    {
        if (durationMs <= 0)
        {
            element.Opacity = 1;
            return;
        }

        element.Opacity = 0;
        var translate = new TranslateTransform(slideDistance, 0);
        element.RenderTransform = translate;

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var opacityAnim = new DoubleAnimation(1, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        var xAnim = new DoubleAnimation(0, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        xAnim.Completed += (_, _) => translate.X = 0;

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.XProperty, xAnim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 仅淡入
    /// </summary>
    public static void FadeIn(UIElement element, int durationMs)
    {
        if (durationMs <= 0)
        {
            element.Opacity = 1;
            return;
        }

        element.Opacity = 0;
        var anim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) => element.Opacity = 1;
        element.BeginAnimation(UIElement.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 页面切换：淡出当前 + 延迟后淡入新内容（CrossFade）
    /// </summary>
    /// <param name="oldElement">旧内容（可为 null）</param>
    /// <param name="newElement">新内容（可为 null）</param>
    /// <param name="durationMs">总时长（毫秒），前半淡出，后半淡入</param>
    public static void CrossFade(UIElement? oldElement, UIElement? newElement, int durationMs)
    {
        if (durationMs <= 0)
        {
            if (oldElement != null) oldElement.Opacity = 0;
            if (newElement != null) newElement.Opacity = 1;
            return;
        }

        var halfMs = durationMs / 2;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (oldElement != null)
        {
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(halfMs))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            fadeOut.Completed += (_, _) => oldElement.Opacity = 0;
            oldElement.BeginAnimation(UIElement.OpacityProperty, fadeOut, HandoffBehavior.SnapshotAndReplace);
        }

        if (newElement != null)
        {
            newElement.Opacity = 0;
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(halfMs))
            {
                BeginTime = TimeSpan.FromMilliseconds(halfMs),
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            fadeIn.Completed += (_, _) => newElement.Opacity = 1;
            newElement.BeginAnimation(UIElement.OpacityProperty, fadeIn, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
