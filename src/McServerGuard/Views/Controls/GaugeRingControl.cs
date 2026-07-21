// -----------------------------------------------------------------------------
// 文件名: GaugeRingControl.cs
// 命名空间: McServerGuard.Views.Controls
// 功能描述: 圆环仪表盘自定义控件，基于 DrawingVisual 自绘实现。
//           以 270 度圆弧展示 0-100 的百分比值，颜色随数值档位变化。
//           采用静态 Brush/Pen/Typeface Freeze 缓存 + 几何缓存优化渲染性能。
//           新增数值平滑动画：Value 变化时 DisplayValue 平滑过渡，圆弧与数字同步动画。
// 依赖组件: PresentationFramework, System.Windows.Media, System.Windows.Media.Animation
// 设计模式: 自定义控件 (DependencyProperty), WPF 可视化树
// -----------------------------------------------------------------------------
namespace McServerGuard.Views.Controls;

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

/// <summary>
/// 圆环仪表盘自定义控件。
/// 继承自 FrameworkElement，通过 DrawingVisual 进行低开销自绘。
/// 使用 270 度圆弧（底部留有缺口）展示百分比数值，
/// 数值在 0-60%、60-85%、85-100% 三档分别对应绿、黄、红语义色。
/// Value 变化时通过 DisplayValue 依赖属性进行 DoubleAnimation 平滑过渡，
/// 所有静态画笔与字体均在构造前 Freeze 缓存，轨道几何按尺寸缓存，以消除每帧 GC 分配。
/// </summary>
public class GaugeRingControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();

    // ─── 静态缓存：语义色 Brush/Pen/Typeface 只创建一次并 Freeze，避免每帧 GC ───

    private static readonly Brush GreenBrush = CreateFrozenBrush(Color.FromArgb(255, 76, 175, 80));
    private static readonly Brush YellowBrush = CreateFrozenBrush(Color.FromArgb(255, 255, 193, 7));
    private static readonly Brush RedBrush = CreateFrozenBrush(Color.FromArgb(255, 244, 67, 54));

    private static readonly Pen GreenPen = CreateFrozenPen(GreenBrush, 12.0);
    private static readonly Pen YellowPen = CreateFrozenPen(YellowBrush, 12.0);
    private static readonly Pen RedPen = CreateFrozenPen(RedBrush, 12.0);

    private static readonly Brush FallbackTrackBrush = CreateFrozenBrush(Color.FromArgb(255, 45, 45, 61));
    private static readonly Brush FallbackLabelBrush = CreateFrozenBrush(Color.FromArgb(180, 180, 180, 180));
    private static readonly Brush FallbackWhiteBrush = CreateFrozenBrush(Colors.White);
    private const double TrackPenThickness = 12.0;

    private static readonly Typeface NumTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface LabelTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static Brush CreateFrozenBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    private static Pen CreateFrozenPen(Brush brush, double thickness)
    {
        var p = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        p.Freeze();
        return p;
    }

    private Brush GetTrackBrush() => TryFindResource("CardHoverBrush") as Brush ?? FallbackTrackBrush;
    private Brush GetLabelBrush() => TryFindResource("MaterialDesignBodyLight") as Brush ?? FallbackLabelBrush;
    private Brush GetWhiteBrush() => TryFindResource("MaterialDesignBody") as Brush ?? FallbackWhiteBrush;

    // ─── 实例级绘制缓存（按尺寸/颜色失效） ───

    private Size _cachedSize = Size.Empty;
    private StreamGeometry? _cachedTrackGeom;
    private Pen? _cachedTrackPen;
    private Brush? _cachedTrackBrush;
    private FormattedText? _cachedNumText;
    private string _cachedNumStr = string.Empty;
    private double _cachedNumFontSize;
    private Brush? _cachedWhiteBrush;
    private FormattedText? _cachedLabelText;
    private string _cachedLabelStr = string.Empty;
    private double _cachedLabelFontSize;
    private Brush? _cachedLabelBrush;

    // ─── 依赖属性 ──────────────────────────────────────────────────────

    /// <summary>
    /// 目标数值依赖属性。设置此值将触发平滑动画过渡到 DisplayValue。
    /// </summary>
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

    /// <summary>
    /// 显示数值依赖属性（动画驱动）。绘制实际读取此值。
    /// </summary>
    public static readonly DependencyProperty DisplayValueProperty =
        DependencyProperty.Register(nameof(DisplayValue), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnLabelChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata("%", FrameworkPropertyMetadataOptions.AffectsRender, OnUnitChanged));

    public static readonly DependencyProperty ArcThicknessProperty =
        DependencyProperty.Register(nameof(ArcThickness), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 动画时长依赖属性（毫秒）。Value 变化时的平滑过渡时长。
    /// </summary>
    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.Register(nameof(AnimationDuration), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(600d));

    /// <summary>
    /// 是否启用动画。关闭时 Value 直接赋值给 DisplayValue。
    /// </summary>
    public static readonly DependencyProperty EnableAnimationProperty =
        DependencyProperty.Register(nameof(EnableAnimation), typeof(bool), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(true, OnEnableAnimationChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double DisplayValue
    {
        get => (double)GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public double ArcThickness
    {
        get => (double)GetValue(ArcThicknessProperty);
        set => SetValue(ArcThicknessProperty, value);
    }

    public double AnimationDuration
    {
        get => (double)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    public bool EnableAnimation
    {
        get => (bool)GetValue(EnableAnimationProperty);
        set => SetValue(EnableAnimationProperty, value);
    }

    // ─── 依赖属性变更回调 ──────────────────────────────────────────────

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (GaugeRingControl)d;
        var newValue = (double)e.NewValue;

        if (!ctrl.EnableAnimation)
        {
            ctrl.DisplayValue = newValue;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(ctrl.AnimationDuration);
        var anim = new DoubleAnimation(newValue, duration)
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) => ctrl.DisplayValue = newValue;

        ctrl.BeginAnimation(DisplayValueProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (GaugeRingControl)d;
        ctrl._cachedLabelText = null;
        ctrl._cachedLabelStr = (string)e.NewValue ?? string.Empty;
    }

    private static void OnUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (GaugeRingControl)d;
        ctrl._cachedNumText = null;
    }

    private static void OnEnableAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (GaugeRingControl)d;
        if (!(bool)e.NewValue)
        {
            ctrl.BeginAnimation(DisplayValueProperty, null);
            ctrl.DisplayValue = ctrl.Value;
        }
    }

    // ─── 构造与可视化子元素 ────────────────────────────────────────────

    public GaugeRingControl()
    {
        Width = 160;
        Height = 160;
        MinWidth = 120;
        MinHeight = 120;

        AddVisualChild(_visual);
        Loaded += (_, _) => InvalidateVisual();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    // ─── 颜色档位映射 ─────────────────────────────────────────────────

    private static Pen GetPenForValue(double v)
    {
        if (v < 60) return GreenPen;
        if (v < 85) return YellowPen;
        return RedPen;
    }

    // ─── 绘制 ─────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        DrawGauge();
        base.OnRender(dc);
    }

    /// <summary>
    /// 绘制完整仪表盘。尽可能复用缓存对象（几何、画笔、文本）。
    /// </summary>
    private void DrawGauge()
    {
        var w = RenderSize.Width;
        var h = RenderSize.Height;
        if (w < 10 || h < 10)
            return;

        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(cx, cy) - ArcThickness - 4;
        if (radius < 10)
            radius = 10;

        var size = new Size(w, h);
        var trackBrush = GetTrackBrush();

        // 尺寸或轨道色变化时，失效轨道几何与轨道 Pen 缓存
        if (size != _cachedSize || !ReferenceEquals(trackBrush, _cachedTrackBrush))
        {
            _cachedTrackGeom = null;
            _cachedTrackPen = null;
            _cachedSize = size;
            _cachedTrackBrush = trackBrush;
        }

        // 构建/复用轨道几何
        if (_cachedTrackGeom is null)
        {
            var bgGeom = new StreamGeometry();
            using (var ctx = bgGeom.Open())
            {
                var startAngle = -135;
                var endAngle = 135;
                var startRad = startAngle * Math.PI / 180;
                var endRad = endAngle * Math.PI / 180;

                ctx.BeginFigure(new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad)), false, false);
                ctx.ArcTo(new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad)),
                    new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            }
            bgGeom.Freeze();
            _cachedTrackGeom = bgGeom;
        }

        // 构建/复用轨道 Pen
        if (_cachedTrackPen is null)
        {
            var pen = new Pen(trackBrush, TrackPenThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            pen.Freeze();
            _cachedTrackPen = pen;
        }

        using var drawing = _visual.RenderOpen();

        // 第一步：绘制灰色背景轨道
        drawing.DrawGeometry(null, _cachedTrackPen, _cachedTrackGeom);

        // 第二步：绘制彩色进度弧（使用 DisplayValue 作为动画驱动值）
        var displayValue = Math.Clamp(DisplayValue, 0, 100);
        if (displayValue > 0.1)
        {
            var sweepAngle = (displayValue / 100) * 270;
            var valueRad = sweepAngle * Math.PI / 180;
            var startRad2 = -135 * Math.PI / 180;
            var endRad2 = startRad2 + valueRad;

            var fgPen = GetPenForValue(displayValue);

            var fgGeom = new StreamGeometry();
            using (var ctx = fgGeom.Open())
            {
                ctx.BeginFigure(new Point(cx + radius * Math.Cos(startRad2), cy + radius * Math.Sin(startRad2)), false, false);
                ctx.ArcTo(new Point(cx + radius * Math.Cos(endRad2), cy + radius * Math.Sin(endRad2)),
                    new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise, true, false);
            }
            fgGeom.Freeze();
            drawing.DrawGeometry(null, fgPen, fgGeom);
        }

        // 第三步：绘制中心数字（缓存 FormattedText）
        var valueText = displayValue.ToString("F1", CultureInfo.CurrentCulture);
        var unitText = Unit ?? "%";
        var numStr = $"{valueText}{unitText}";
        var whiteBrush = GetWhiteBrush();
        var numFontSize = Math.Min(28, radius * 0.5);

        if (_cachedNumText is null || _cachedNumStr != numStr || _cachedNumFontSize != numFontSize || !ReferenceEquals(whiteBrush, _cachedWhiteBrush))
        {
            _cachedNumText = new FormattedText(numStr,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                NumTypeface,
                numFontSize,
                whiteBrush, null, 1.0);
            _cachedNumStr = numStr;
            _cachedNumFontSize = numFontSize;
            _cachedWhiteBrush = whiteBrush;
        }

        drawing.DrawText(_cachedNumText,
            new Point(cx - _cachedNumText.Width / 2, cy - _cachedNumText.Height / 2 - 4));

        // 底部标签文本（缓存 FormattedText）
        var labelText = Label ?? "";
        if (!string.IsNullOrEmpty(labelText))
        {
            var labelBrush = GetLabelBrush();
            var labelFontSize = Math.Min(12, radius * 0.22);

            if (_cachedLabelText is null || _cachedLabelStr != labelText || _cachedLabelFontSize != labelFontSize || !ReferenceEquals(labelBrush, _cachedLabelBrush))
            {
                _cachedLabelText = new FormattedText(labelText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    LabelTypeface,
                    labelFontSize,
                    labelBrush, null, 1.0);
                _cachedLabelStr = labelText;
                _cachedLabelFontSize = labelFontSize;
                _cachedLabelBrush = labelBrush;
            }

            drawing.DrawText(_cachedLabelText,
                new Point(cx - _cachedLabelText.Width / 2, cy + radius * 0.35));
        }
    }
}
