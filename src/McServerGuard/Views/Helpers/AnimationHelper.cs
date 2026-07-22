// -----------------------------------------------------------------------------
// 文件名: AnimationHelper.cs
// 命名空间: McServerGuard.Views.Helpers
// 功能描述: 动画工具类，封装常用入场与过渡动画方法。
//           所有动画均通过代码动态创建 DoubleAnimation 实现，
//           动画时长由参数传入，天然支持主题服务配置。
// 依赖组件: PresentationFramework, System.Windows.Media.Animation
// 设计模式: 工具类 (静态方法), 缓动函数
// -----------------------------------------------------------------------------
namespace McServerGuard.Views.Helpers;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using McServerGuard.Services;

/// <summary>
/// 动画工具类。
/// 封装页面入场、元素过渡等常用动画效果。
/// 全部通过代码隐藏动态创建 DoubleAnimation 时间线，
/// 动画时长以参数形式传入，便于跟随主题服务配置统一调整。
/// 不采用 XAML Storyboard 的原因是其 Duration 属性为 Freezable 类型，
/// 无法进行数据绑定。
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// 淡入 + 从下方滑入组合动画（页面入场标准效果）。
    /// 动画完成后清除动画对象持有，释放相关资源。
    /// </summary>
    /// <param name="element">目标 UI 元素</param>
    /// <param name="durationMs">动画时长（毫秒），小于等于 0 时直接显示不播放动画</param>
    /// <param name="slideDistance">滑动距离（像素），默认 20</param>
    public static void FadeAndSlideIn(UIElement element, int durationMs, double slideDistance = 20)
    {
        if (!AnimationSettings.AnimationsEnabled || durationMs <= 0)
        {
            element.Opacity = 1;
            if (element.RenderTransform is TranslateTransform t) t.Y = 0;
            return;
        }

        element.Opacity = 0;
        var translate = new TranslateTransform(0, slideDistance);
        element.RenderTransform = translate;

        var duration = TimeSpan.FromMilliseconds(durationMs);

        // 使用 HoldEnd（默认 FillBehavior）而非 Stop：动画结束后保持终值，
        // 即使 Completed 事件丢失，Opacity 仍为 1
        var opacityAnim = new DoubleAnimation(1, duration)
        {
            EasingFunction = AnimationSettings.Standard
        };
        var yAnim = new DoubleAnimation(0, duration)
        {
            EasingFunction = AnimationSettings.Standard
        };
        yAnim.Completed += (_, _) =>
        {
            translate.Y = 0;
            translate.BeginAnimation(TranslateTransform.YProperty, null); // 清除动画持有，释放资源
        };
        opacityAnim.Completed += (_, _) =>
        {
            element.Opacity = 1;
            element.BeginAnimation(UIElement.OpacityProperty, null);
        };

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.YProperty, yAnim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 淡入 + 从左侧滑入组合动画（卡片/列表项入场效果）。
    /// </summary>
    /// <param name="element">目标 UI 元素</param>
    /// <param name="durationMs">动画时长（毫秒）</param>
    /// <param name="slideDistance">滑动距离（像素），默认 20</param>
    public static void FadeAndSlideInFromLeft(UIElement element, int durationMs, double slideDistance = 20)
    {
        if (!AnimationSettings.AnimationsEnabled || durationMs <= 0)
        {
            element.Opacity = 1;
            if (element.RenderTransform is TranslateTransform t) t.X = 0;
            return;
        }

        element.Opacity = 0;
        var translate = new TranslateTransform(slideDistance, 0);
        element.RenderTransform = translate;

        var duration = TimeSpan.FromMilliseconds(durationMs);

        var opacityAnim = new DoubleAnimation(1, duration)
        {
            EasingFunction = AnimationSettings.Standard
        };
        var xAnim = new DoubleAnimation(0, duration)
        {
            EasingFunction = AnimationSettings.Standard
        };
        xAnim.Completed += (_, _) =>
        {
            translate.X = 0;
            translate.BeginAnimation(TranslateTransform.XProperty, null);
        };
        opacityAnim.Completed += (_, _) =>
        {
            element.Opacity = 1;
            element.BeginAnimation(UIElement.OpacityProperty, null);
        };

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.XProperty, xAnim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 纯淡入动画。
    /// </summary>
    /// <param name="element">目标 UI 元素</param>
    /// <param name="durationMs">动画时长（毫秒）</param>
    public static void FadeIn(UIElement element, int durationMs)
    {
        if (!AnimationSettings.AnimationsEnabled || durationMs <= 0)
        {
            element.Opacity = 1;
            return;
        }

        element.Opacity = 0;
        var anim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = AnimationSettings.Standard
        };
        anim.Completed += (_, _) =>
        {
            element.Opacity = 1;
            element.BeginAnimation(UIElement.OpacityProperty, null);
        };
        element.BeginAnimation(UIElement.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 淡入 + 从下方滑入组合动画（带延迟，用于列表项错落入场）。
    /// </summary>
    /// <param name="element">目标 UI 元素</param>
    /// <param name="durationMs">动画时长（毫秒）</param>
    /// <param name="delayMs">延迟开始时间（毫秒）</param>
    /// <param name="slideDistance">滑动距离（像素），默认 16</param>
    public static void FadeAndSlideInWithDelay(UIElement element, int durationMs, int delayMs, double slideDistance = 16)
    {
        if (!AnimationSettings.AnimationsEnabled || (durationMs <= 0 && delayMs <= 0))
        {
            element.Opacity = 1;
            if (element.RenderTransform is TranslateTransform t) t.Y = 0;
            return;
        }

        element.Opacity = 0;
        var translate = new TranslateTransform(0, slideDistance);
        element.RenderTransform = translate;

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var beginTime = TimeSpan.FromMilliseconds(delayMs);

        var opacityAnim = new DoubleAnimation(1, duration)
        {
            BeginTime = beginTime,
            EasingFunction = AnimationSettings.Standard
        };
        var yAnim = new DoubleAnimation(0, duration)
        {
            BeginTime = beginTime,
            EasingFunction = AnimationSettings.Standard
        };
        yAnim.Completed += (_, _) =>
        {
            translate.Y = 0;
            translate.BeginAnimation(TranslateTransform.YProperty, null);
        };
        opacityAnim.Completed += (_, _) =>
        {
            element.Opacity = 1;
            element.BeginAnimation(UIElement.OpacityProperty, null);
        };

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);
        translate.BeginAnimation(TranslateTransform.YProperty, yAnim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// 页面交叉淡入淡出切换（CrossFade）。
    /// 前半段淡出旧元素，后半段淡入新元素，总时长由参数指定。
    /// </summary>
    /// <param name="oldElement">旧内容元素（可为 null）</param>
    /// <param name="newElement">新内容元素（可为 null）</param>
    /// <param name="durationMs">总时长（毫秒）</param>
    public static void CrossFade(UIElement? oldElement, UIElement? newElement, int durationMs)
    {
        if (!AnimationSettings.AnimationsEnabled || durationMs <= 0)
        {
            if (oldElement != null) oldElement.Opacity = 0;
            if (newElement != null) newElement.Opacity = 1;
            return;
        }

        var halfMs = durationMs / 2;

        if (oldElement != null)
        {
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(halfMs))
            {
                EasingFunction = AnimationSettings.Standard,
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
                EasingFunction = AnimationSettings.Standard,
                FillBehavior = FillBehavior.Stop
            };
            fadeIn.Completed += (_, _) => newElement.Opacity = 1;
            newElement.BeginAnimation(UIElement.OpacityProperty, fadeIn, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
