namespace McServerGuard.Views.Controls;

using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

/// <summary>
/// 饼图数据项。
/// </summary>
public class PieSlice
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public Color Color { get; set; }
}

/// <summary>
/// 自绘饼图控件。
/// 继承 FrameworkElement，通过 DrawingVisual 低开销自绘。
/// 支持 Slices 集合绑定（监听 INotifyCollectionChanged），自动重绘。
/// 中心显示总计数值，各扇形按值比例分配角度。
/// </summary>
public class PieChartControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private IList? _slices;

    private static readonly Brush FallbackWhiteBrush = CreateFrozenBrush(Colors.White);
    private static readonly Brush FallbackLabelBrush = CreateFrozenBrush(Color.FromArgb(180, 180, 180, 180));
    private static readonly Pen SliceSeparatorPen = CreateFrozenPen(CreateFrozenBrush(Color.FromArgb(255, 30, 30, 45)), 2.0);

    private static readonly Typeface CenterTypeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static readonly Typeface LabelTypeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static Brush CreateFrozenBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    private static Pen CreateFrozenPen(Brush brush, double thickness)
    {
        var p = new Pen(brush, thickness);
        p.Freeze();
        return p;
    }

    // ─── 依赖属性 ──────────────────────────────────────────────────────

    public static readonly DependencyProperty SlicesProperty =
        DependencyProperty.Register(nameof(Slices), typeof(IList), typeof(PieChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSlicesChanged));

    public static readonly DependencyProperty CenterTextProperty =
        DependencyProperty.Register(nameof(CenterText), typeof(string), typeof(PieChartControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterSubTextProperty =
        DependencyProperty.Register(nameof(CenterSubText), typeof(string), typeof(PieChartControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public IList? Slices
    {
        get => (IList?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public string CenterText
    {
        get => (string)GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string CenterSubText
    {
        get => (string)GetValue(CenterSubTextProperty);
        set => SetValue(CenterSubTextProperty, value);
    }

    private static void OnSlicesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (PieChartControl)d;

        if (ctrl._slices is INotifyCollectionChanged oldNcc)
            oldNcc.CollectionChanged -= ctrl.OnSlicesCollectionChanged;

        ctrl._slices = e.NewValue as IList;

        if (ctrl._slices is INotifyCollectionChanged newNcc)
            newNcc.CollectionChanged += ctrl.OnSlicesCollectionChanged;

        ctrl.InvalidateVisual();
    }

    private void OnSlicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    // ─── 构造与可视化子元素 ────────────────────────────────────────────

    public PieChartControl()
    {
        Width = 200;
        Height = 200;
        MinWidth = 120;
        MinHeight = 120;

        AddVisualChild(_visual);
        Loaded += (_, _) => InvalidateVisual();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    // ─── 绘制 ─────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        DrawPie();
        base.OnRender(dc);
    }

    private void DrawPie()
    {
        var w = RenderSize.Width;
        var h = RenderSize.Height;
        if (w < 10 || h < 10)
            return;

        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(cx, cy) - 4;
        if (radius < 10)
            radius = 10;

        using var drawing = _visual.RenderOpen();

        if (_slices is null || _slices.Count == 0)
        {
            var noDataBrush = TryFindResource("MaterialDesignBodyLight") as Brush ?? FallbackLabelBrush;
            var noDataText = new FormattedText("暂无数据",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, 14, noDataBrush, null, 1.0);
            drawing.DrawText(noDataText, new Point(cx - noDataText.Width / 2, cy - noDataText.Height / 2));
            return;
        }

        double total = 0;
        foreach (var item in _slices)
        {
            if (item is PieSlice slice)
                total += slice.Value;
        }

        if (total <= 0)
            return;

        // 绘制饼图扇形
        double startAngle = -90; // 从顶部开始
        var center = new Point(cx, cy);

        foreach (var item in _slices)
        {
            if (item is not PieSlice slice || slice.Value <= 0)
                continue;

            var sweepAngle = slice.Value / total * 360;
            var endAngle = startAngle + sweepAngle;

            var startRad = startAngle * Math.PI / 180;
            var endRad = endAngle * Math.PI / 180;

            var startPoint = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
            var endPoint = new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));

            var sliceBrush = new SolidColorBrush(slice.Color);
            sliceBrush.Freeze();

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(center, true, true);
                ctx.LineTo(startPoint, true, false);
                ctx.ArcTo(endPoint, new Size(radius, radius), 0,
                    sweepAngle > 180, SweepDirection.Clockwise, true, false);
            }
            geom.Freeze();

            drawing.DrawGeometry(sliceBrush, SliceSeparatorPen, geom);

            // 绘制百分比标签（在扇形中心位置）
            if (sweepAngle > 15)
            {
                var midAngle = (startAngle + endAngle) / 2 * Math.PI / 180;
                var labelRadius = radius * 0.65;
                var labelX = cx + labelRadius * Math.Cos(midAngle);
                var labelY = cy + labelRadius * Math.Sin(midAngle);

                var pct = slice.Value / total * 100;
                var pctText = new FormattedText($"{pct:F0}%",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    LabelTypeface, 11, FallbackWhiteBrush, null, 1.0);
                drawing.DrawText(pctText, new Point(labelX - pctText.Width / 2, labelY - pctText.Height / 2));
            }

            startAngle = endAngle;
        }

        // 绘制中心文字
        var whiteBrush = TryFindResource("MaterialDesignBody") as Brush ?? FallbackWhiteBrush;
        if (!string.IsNullOrEmpty(CenterText))
        {
            var centerFontSize = Math.Min(24, radius * 0.35);
            var centerText = new FormattedText(CenterText,
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                CenterTypeface, centerFontSize, whiteBrush, null, 1.0);
            drawing.DrawText(centerText, new Point(cx - centerText.Width / 2, cy - centerText.Height / 2 - 6));
        }

        if (!string.IsNullOrEmpty(CenterSubText))
        {
            var subBrush = TryFindResource("MaterialDesignBodyLight") as Brush ?? FallbackLabelBrush;
            var subFontSize = Math.Min(11, radius * 0.16);
            var subText = new FormattedText(CenterSubText,
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, subFontSize, subBrush, null, 1.0);
            drawing.DrawText(subText, new Point(cx - subText.Width / 2, cy + 4));
        }
    }
}
