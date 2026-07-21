// -----------------------------------------------------------------------------
// 文件名: TrendChartControl.cs
// 命名空间: McServerGuard.Views.Controls
// 功能描述: 折线趋势图自定义控件，基于 DrawingVisual 自绘实现。
//           支持折线面积填充、网格线与 Y 轴标注、最新数据点高亮。
//           采用静态 Freeze 缓存 + 几何缓存 + 集合复用策略优化渲染性能。
// 依赖组件: PresentationFramework, System.Windows.Media
// 设计模式: 自定义控件 (DependencyProperty), WPF 可视化树
// -----------------------------------------------------------------------------
namespace McServerGuard.Views.Controls;

using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

/// <summary>
/// 折线趋势图自定义控件。
/// 继承自 FrameworkElement，通过 DrawingVisual 进行低开销自绘。
/// 支持数据点集合绑定（监听 INotifyCollectionChanged）、
/// 折线面积填充、网格线与 Y 轴标注、最新数据点高亮。
/// 静态 Brush/Pen/Typeface 均 Freeze 缓存，
/// 网格线几何与坐标列表按尺寸/数据量复用，消除每帧 GC 分配。
/// </summary>
public class TrendChartControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private IList? _dataPoints;
    private readonly List<double> _pointBuffer = [];
    private readonly List<Point> _coordBuffer = [];

    // ─── 静态缓存：Brush/Pen/Typeface 只创建一次并 Freeze ───

    private static readonly Brush GridPenBrush = CreateFrozenBrush(Color.FromArgb(40, 255, 255, 255));
    private static readonly Pen GridPen = CreateFrozenPen(GridPenBrush, 1);
    private static readonly Brush AxisLabelBrush = CreateFrozenBrush(Color.FromArgb(120, 200, 200, 200));
    private static readonly Brush NoDataBrush = CreateFrozenBrush(Color.FromArgb(100, 200, 200, 200));
    private static readonly Brush HaloBrush = CreateFrozenBrush(Color.FromArgb(60, 200, 200, 200));
    private static readonly Typeface LabelTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

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

    // ─── 实例级绘制缓存 ───

    private Size _cachedSize = Size.Empty;
    private StreamGeometry? _cachedGridGeom;
    private FormattedText[]? _cachedYLabels;
    private double _cachedLineOpacity;
    private Brush? _cachedLineColor;
    private Brush? _cachedFillBrush;
    private Pen? _cachedLinePen;
    private double _cachedFillOpacity = -1;

    // ─── 依赖属性 ──────────────────────────────────────────────────────

    public static readonly DependencyProperty DataPointsProperty =
        DependencyProperty.Register(nameof(DataPoints), typeof(IList), typeof(TrendChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataPointsChanged));

    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register(nameof(LineColor), typeof(Brush), typeof(TrendChartControl),
            new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender, OnLineColorChanged));

    public static readonly DependencyProperty FillOpacityProperty =
        DependencyProperty.Register(nameof(FillOpacity), typeof(double), typeof(TrendChartControl),
            new FrameworkPropertyMetadata(0.15, FrameworkPropertyMetadataOptions.AffectsRender, OnFillOpacityChanged));

    public static readonly DependencyProperty MaxPointsProperty =
        DependencyProperty.Register(nameof(MaxPoints), typeof(int), typeof(TrendChartControl),
            new FrameworkPropertyMetadata(120, FrameworkPropertyMetadataOptions.AffectsRender));

    public IList? DataPoints { get => (IList?)GetValue(DataPointsProperty); set => SetValue(DataPointsProperty, value); }
    public Brush LineColor { get => (Brush)GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }
    public double FillOpacity { get => (double)GetValue(FillOpacityProperty); set => SetValue(FillOpacityProperty, value); }
    public int MaxPoints { get => (int)GetValue(MaxPointsProperty); set => SetValue(MaxPointsProperty, value); }

    // ─── 构造与可视化子元素 ────────────────────────────────────────────

    public TrendChartControl()
    {
        Height = 160;
        MinHeight = 80;

        AddVisualChild(_visual);
        Loaded += OnLoaded;
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    // ─── 数据集合变更监听 ─────────────────────────────────────────────

    private static void OnDataPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TrendChartControl)d;

        if (e.OldValue is INotifyCollectionChanged oldNcc)
            oldNcc.CollectionChanged -= ctrl.OnCollectionChanged;

        if (e.NewValue is INotifyCollectionChanged newNcc)
            newNcc.CollectionChanged += ctrl.OnCollectionChanged;

        ctrl._dataPoints = e.NewValue as IList;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private static void OnLineColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TrendChartControl)d;
        ctrl._cachedFillBrush = null;
        ctrl._cachedLinePen = null;
        ctrl._cachedLineColor = e.NewValue as Brush;
    }

    private static void OnFillOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TrendChartControl)d;
        ctrl._cachedFillBrush = null;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ReadLocalValue(LineColorProperty) == DependencyProperty.UnsetValue)
        {
            if (TryFindResource("PrimaryHueMidBrush") is Brush primaryBrush)
                SetValue(LineColorProperty, primaryBrush);
        }
        InvalidateVisual();
    }

    // ─── 绘制 ─────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        DrawChart();
        base.OnRender(dc);
    }

    private void DrawChart()
    {
        var w = RenderSize.Width;
        var h = RenderSize.Height;
        if (w < 20 || h < 20)
            return;

        using var drawing = _visual.RenderOpen();

        var margin = new Thickness(40, 12, 12, 20);
        var plotW = w - margin.Left - margin.Right;
        var plotH = h - margin.Top - margin.Bottom;

        if (plotW < 10 || plotH < 10)
            return;

        var size = new Size(w, h);

        // 尺寸变化时失效网格线与 Y 轴标签缓存
        if (size != _cachedSize)
        {
            _cachedGridGeom = null;
            _cachedYLabels = null;
            _cachedSize = size;
        }

        // 构建/复用网格线几何
        if (_cachedGridGeom is null)
        {
            var gridGeom = new StreamGeometry();
            using (var ctx = gridGeom.Open())
            {
                foreach (var pct in new[] { 0, 50, 100 })
                {
                    var y = margin.Top + plotH * (1 - pct / 100.0);
                    ctx.BeginFigure(new Point(margin.Left, y), false, false);
                    ctx.LineTo(new Point(margin.Left + plotW, y), true, true);
                }
            }
            gridGeom.Freeze();
            _cachedGridGeom = gridGeom;
        }

        // 构建/复用 Y 轴标签
        if (_cachedYLabels is null)
        {
            var labels = new FormattedText[3];
            var labelFontSize = 10.0;
            var values = new[] { 0, 50, 100 };
            for (var i = 0; i < 3; i++)
            {
                labels[i] = new FormattedText($"{values[i]}%",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    LabelTypeface, labelFontSize, AxisLabelBrush, null, 1.0);
            }
            _cachedYLabels = labels;
        }

        // 绘制网格线
        drawing.DrawGeometry(null, GridPen, _cachedGridGeom);

        // 绘制 Y 轴标注
        for (var i = 0; i < 3; i++)
        {
            var pct = i * 50;
            var y = margin.Top + plotH * (1 - pct / 100.0);
            var label = _cachedYLabels[i];
            drawing.DrawText(label, new Point(margin.Left - label.Width - 6, y - label.Height / 2));
        }

        // 提取数据点（复用 List，避免每帧 new）
        _pointBuffer.Clear();
        if (_dataPoints is not null)
        {
            foreach (var item in _dataPoints)
            {
                if (item is double d)
                    _pointBuffer.Add(Math.Clamp(d, 0, 100));
            }
        }

        // 限制数据点数量
        if (_pointBuffer.Count > MaxPoints)
        {
            var skip = _pointBuffer.Count - MaxPoints;
            _pointBuffer.RemoveRange(0, skip);
        }

        // ─── 无数据提示 ─────────────────────────────────────────────
        if (_pointBuffer.Count < 2)
        {
            var noData = new FormattedText("暂无数据",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, 16, NoDataBrush, null, 1.0);
            drawing.DrawText(noData, new Point(w / 2 - noData.Width / 2, h / 2 - noData.Height / 2));
            return;
        }

        // ─── 计算坐标点（复用 List） ───────────────────────────────
        _coordBuffer.Clear();
        for (var i = 0; i < _pointBuffer.Count; i++)
        {
            var x = margin.Left + (_pointBuffer.Count > 1 ? (double)i / (_pointBuffer.Count - 1) * plotW : plotW / 2);
            var y = margin.Top + plotH * (1 - _pointBuffer[i] / 100.0);
            _coordBuffer.Add(new Point(x, y));
        }

        var lineColor = LineColor;
        var fillOpacity = FillOpacity;

        // 构建/复用填充色 Brush
        if (_cachedFillBrush is null || _cachedFillOpacity != fillOpacity || !ReferenceEquals(_cachedLineColor, lineColor))
        {
            if (lineColor is SolidColorBrush solidBrush)
            {
                var fillColor = Color.FromArgb((byte)(fillOpacity * 255), solidBrush.Color.R, solidBrush.Color.G, solidBrush.Color.B);
                var fb = new SolidColorBrush(fillColor);
                fb.Freeze();
                _cachedFillBrush = fb;
            }
            _cachedLineColor = lineColor;
            _cachedFillOpacity = fillOpacity;
        }

        // 构建/复用折线 Pen
        if (_cachedLinePen is null || !ReferenceEquals(_cachedLineColor, lineColor))
        {
            var pen = new Pen(lineColor, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            _cachedLinePen = pen;
            _cachedLineColor = lineColor;
        }

        // ─── 面积填充 ─────────────────────────────────────────────
        var areaGeom = new StreamGeometry();
        using (var ctx = areaGeom.Open())
        {
            var baseline = margin.Top + plotH;

            ctx.BeginFigure(_coordBuffer[0], false, false);
            ctx.LineTo(new Point(_coordBuffer[0].X, baseline), true, true);

            for (var i = 1; i < _coordBuffer.Count; i++)
                ctx.LineTo(_coordBuffer[i], true, true);

            ctx.LineTo(new Point(_coordBuffer[^1].X, baseline), true, true);
            ctx.LineTo(_coordBuffer[0], true, true);
        }
        areaGeom.Freeze();
        drawing.DrawGeometry(_cachedFillBrush, null, areaGeom);

        // ─── 折线 ─────────────────────────────────────────────────
        var lineGeom = new StreamGeometry();
        using (var ctx = lineGeom.Open())
        {
            ctx.BeginFigure(_coordBuffer[0], false, false);
            for (var i = 1; i < _coordBuffer.Count; i++)
                ctx.LineTo(_coordBuffer[i], true, true);
        }
        lineGeom.Freeze();
        drawing.DrawGeometry(null, _cachedLinePen, lineGeom);

        // ─── 最新数据点高亮 ───────────────────────────────────────
        var lastPt = _coordBuffer[^1];
        drawing.DrawEllipse(HaloBrush, null, lastPt, 6, 6);
        drawing.DrawEllipse(lineColor, null, lastPt, 3, 3);
    }
}
