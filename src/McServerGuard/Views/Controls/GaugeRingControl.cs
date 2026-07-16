// 🔵 GaugeRingControl —— 圆环仪表盘
// 纯 DrawingVisual 自绘，零外部依赖，就是这么硬核 💪
// ⚡ 性能优化：所有 Brush/Pen/Typeface 全部 Freeze 缓存为静态字段，消除每帧 GC
namespace McServerGuard.Views.Controls;

using System.Globalization;
using System.Windows;
using System.Windows.Media;

/// <summary>
/// 圆环仪表盘控件 —— 用弧线展示 0-100 的百分比值
/// 270度圆弧（底部留缺口），颜色随值变化（绿→黄→红）
/// </summary>
public class GaugeRingControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();

    // ─── 静态缓存：语义色 Brush/Pen/Typeface 只创建一次并 Freeze，避免每帧 GC ───
    // 语义色（绿/黄/红）不随主题变，保持静态；中性色（轨道/标签/白字）改为动态读取主题资源

    // 三档语义色（不跟随主题，合理静态）
    private static readonly Brush GreenBrush = CreateFrozenBrush(Color.FromArgb(255, 76, 175, 80));   // #4CAF50
    private static readonly Brush YellowBrush = CreateFrozenBrush(Color.FromArgb(255, 255, 193, 7));  // #FFC107
    private static readonly Brush RedBrush = CreateFrozenBrush(Color.FromArgb(255, 244, 67, 54));     // #F44336

    private static readonly Pen GreenPen = CreateFrozenPen(GreenBrush, 12.0);
    private static readonly Pen YellowPen = CreateFrozenPen(YellowBrush, 12.0);
    private static readonly Pen RedPen = CreateFrozenPen(RedBrush, 12.0);

    // 静态回退默认值（TryFindResource 在控件未加入逻辑树时返回 null）
    private static readonly Brush FallbackTrackBrush = CreateFrozenBrush(Color.FromArgb(255, 45, 45, 61));
    private static readonly Brush FallbackLabelBrush = CreateFrozenBrush(Color.FromArgb(180, 180, 180, 180));
    private static readonly Brush FallbackWhiteBrush = CreateFrozenBrush(Colors.White);
    private static readonly double TrackPenThickness = 12.0;

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

    /// <summary>
    /// 从主题资源读取笔刷，读不到则回退到静态默认值
    /// </summary>
    private Brush GetTrackBrush() => TryFindResource("CardHoverBrush") as Brush ?? FallbackTrackBrush;
    private Brush GetLabelBrush() => TryFindResource("MaterialDesignBodyLight") as Brush ?? FallbackLabelBrush;
    private Brush GetWhiteBrush() => TryFindResource("MaterialDesignBody") as Brush ?? FallbackWhiteBrush;

    // ─── 依赖属性 ──────────────────────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata("%", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ArcThicknessProperty =
        DependencyProperty.Register(nameof(ArcThickness), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double ArcThickness { get => (double)GetValue(ArcThicknessProperty); set => SetValue(ArcThicknessProperty, value); }

    // ─── 尺寸 ──────────────────────────────────────────────────────────

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

    // ─── 颜色档位 → 对应 Pen ───────────────────────────────────────────

    private static Pen GetPenForValue(double v)
    {
        // 0-60% 绿，60-85% 黄，85-100% 红
        if (v < 60) return GreenPen;
        if (v < 85) return YellowPen;
        return RedPen;
    }

    // ─── 绘制 ──────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        DrawGauge();
        base.OnRender(dc);
    }

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

        using var drawing = _visual.RenderOpen();

        // 🎨 动态读取轨道色（跟随主题），每次创建 TrackPen（Pen 轻量）
        var trackBrush = GetTrackBrush();
        var trackPen = new Pen(trackBrush, TrackPenThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        // 1️⃣ 灰色背景轨道（270度弧）—— 使用动态 TrackPen
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
        drawing.DrawGeometry(null, trackPen, bgGeom);

        // 2️⃣ 彩色进度弧 —— 使用缓存的三档 Pen
        var clampedValue = Math.Clamp(Value, 0, 100);
        if (clampedValue > 0.1)
        {
            var sweepAngle = (clampedValue / 100) * 270;
            var valueRad = sweepAngle * Math.PI / 180;
            var startRad2 = -135 * Math.PI / 180;
            var endRad2 = startRad2 + valueRad;

            var fgPen = GetPenForValue(clampedValue);

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

        // 3️⃣ 中心数字 —— FormattedText 必须每次创建（值会变），但 Typeface 已缓存
        // 颜色动态读取主题（跟随主色变化）
        var valueText = clampedValue.ToString("F1", CultureInfo.CurrentCulture);
        var unitText = Unit ?? "%";
        var labelText = Label ?? "";
        var whiteBrush = GetWhiteBrush();
        var labelBrush = GetLabelBrush();

        var numFontSize = Math.Min(28, radius * 0.5);
        var numFormatted = new FormattedText($"{valueText}{unitText}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            NumTypeface,
            numFontSize,
            whiteBrush, null, 1.0);

        drawing.DrawText(numFormatted,
            new Point(cx - numFormatted.Width / 2, cy - numFormatted.Height / 2 - 4));

        // 标签（底部小字）—— 同样用缓存的 Typeface + 动态 LabelBrush
        if (!string.IsNullOrEmpty(labelText))
        {
            var labelFontSize = Math.Min(12, radius * 0.22);
            var labelFormatted = new FormattedText(labelText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                labelFontSize,
                labelBrush, null, 1.0);

            drawing.DrawText(labelFormatted,
                new Point(cx - labelFormatted.Width / 2, cy + radius * 0.35));
        }
    }
}
