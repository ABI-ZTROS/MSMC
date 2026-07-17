// -----------------------------------------------------------------------------
// 文件名: TrendChartControl.cs
// 命名空间: McServerGuard.Views.Controls
// 功能描述: 折线趋势图自定义控件，基于 DrawingVisual 自绘实现。
//           支持折线面积填充、网格线与 Y 轴标注、最新数据点高亮。
//           采用静态 Freeze 缓存 + Geometry.Freeze 策略优化渲染性能。
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
/// 静态 Brush/Pen/Typeface 均 Freeze 缓存，消除每帧 GC 分配。
/// </summary>
public class TrendChartControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private IList? _dataPoints;

    // ─── 静态缓存：Brush/Pen/Typeface 只创建一次并 Freeze，避免每帧 GC ───

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

    // ─── 依赖属性 ──────────────────────────────────────────────────────

    /// <summary>
    /// 数据点集合依赖属性。
    /// 支持实现 INotifyCollectionChanged 的集合，集合变更时自动触发重绘。
    /// 集合元素应为 double 类型，取值范围 0-100。
    /// </summary>
    public static readonly DependencyProperty DataPointsProperty =
        DependencyProperty.Register(nameof(DataPoints), typeof(IList), typeof(TrendChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataPointsChanged));

    /// <summary>
    /// 折线颜色依赖属性。控制折线与数据点的绘制颜色，默认为透明。
    /// 若未显式设置，控件加载时将尝试从主题资源读取主色作为默认值。
    /// </summary>
    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register(nameof(LineColor), typeof(Brush), typeof(TrendChartControl),
            new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 填充不透明度依赖属性。控制面积填充区域的不透明度，默认值 0.15。
    /// </summary>
    public static readonly DependencyProperty FillOpacityProperty =
        DependencyProperty.Register(nameof(FillOpacity), typeof(double), typeof(TrendChartControl),
            new FrameworkPropertyMetadata(0.15, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 最大数据点数依赖属性。超过该数量时截断最旧的数据点，默认值 120。
    /// </summary>
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

    /// <summary>
    /// DataPoints 依赖属性变更回调。
    /// 取消旧集合的 CollectionChanged 监听，注册新集合的事件监听。
    /// </summary>
    /// <param name="d">依赖对象实例</param>
    /// <param name="e">属性变更事件参数</param>
    private static void OnDataPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TrendChartControl)d;

        // 取消旧集合的监听
        if (e.OldValue is INotifyCollectionChanged oldNcc)
            oldNcc.CollectionChanged -= ctrl.OnCollectionChanged;

        // 监听新集合
        if (e.NewValue is INotifyCollectionChanged newNcc)
            newNcc.CollectionChanged += ctrl.OnCollectionChanged;

        ctrl._dataPoints = e.NewValue as IList;
    }

    // 集合变更事件处理：触发控件重绘
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    // 控件 Loaded 事件处理：若 LineColor 未显式设置则回退到主题主色
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 若调用方未设置 LineColor（含 DynamicResource/StaticResource/Binding 均视为已设置），
        // 则让默认线色跟随主色（消除硬编码 #7B1FA2）。
        // 使用 ReadLocalValue 而非 BindingOperations.GetBinding：后者无法识别 DynamicResource，
        // 会把 XAML 的 DynamicResource 误判为"未设置"从而覆盖掉，破坏主题绑定。
        if (ReadLocalValue(LineColorProperty) == DependencyProperty.UnsetValue)
        {
            if (TryFindResource("PrimaryHueMidBrush") is Brush primaryBrush)
                SetValue(LineColorProperty, primaryBrush);
        }
        InvalidateVisual();
    }

    // ─── 绘制 ─────────────────────────────────────────────────────────

    /// <summary>
    /// OnRender 重写：调用 DrawChart 完成趋势图绘制。
    /// </summary>
    /// <param name="dc">绘制上下文</param>
    protected override void OnRender(DrawingContext dc)
    {
        DrawChart();
        base.OnRender(dc);
    }

    /// <summary>
    /// 绘制完整趋势图：网格线与 Y 轴标注、面积填充、折线、最新数据点高亮。
    /// 数据点数不足 2 时显示"暂无数据"提示。
    /// </summary>
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

        // 提取数据点
        var points = new List<double>();
        if (_dataPoints is not null)
        {
            foreach (var item in _dataPoints)
            {
                if (item is double d)
                    points.Add(Math.Clamp(d, 0, 100));
            }
        }

        // 限制数据点数量
        if (points.Count > MaxPoints)
            points = points.Skip(points.Count - MaxPoints).ToList();

        // ─── 网格线和 Y 轴标注 ───────────────────────────────────────

        var labelFontSize = 10.0;

        foreach (var pct in new[] { 0, 50, 100 })
        {
            var y = margin.Top + plotH * (1 - pct / 100.0);

            // 网格线
            drawing.DrawLine(GridPen, new Point(margin.Left, y), new Point(margin.Left + plotW, y));

            // Y 轴标注
            var label = new FormattedText($"{pct}%",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, labelFontSize, AxisLabelBrush, null, 1.0);
            drawing.DrawText(label, new Point(margin.Left - label.Width - 6, y - label.Height / 2));
        }

        // ─── 无数据提示 ─────────────────────────────────────────────

        if (points.Count < 2)
        {
            var noData = new FormattedText("暂无数据",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, 16, NoDataBrush, null, 1.0);
            drawing.DrawText(noData, new Point(w / 2 - noData.Width / 2, h / 2 - noData.Height / 2));
            return;
        }

        // ─── 计算坐标点 ─────────────────────────────────────────────

        var coords = new List<Point>();
        for (var i = 0; i < points.Count; i++)
        {
            var x = margin.Left + (points.Count > 1 ? (double)i / (points.Count - 1) * plotW : plotW / 2);
            var y = margin.Top + plotH * (1 - points[i] / 100.0);
            coords.Add(new Point(x, y));
        }

        // ─── 面积填充（半透明） ─────────────────────────────────────

        var areaGeom = new StreamGeometry();
        using (var ctx = areaGeom.Open())
        {
            var baseline = margin.Top + plotH;

            ctx.BeginFigure(coords[0], false, false);
            ctx.LineTo(new Point(coords[0].X, baseline), true, true);

            for (var i = 1; i < coords.Count; i++)
            {
                ctx.LineTo(coords[i], true, true);
            }

            ctx.LineTo(new Point(coords[^1].X, baseline), true, true);
            ctx.LineTo(coords[0], true, true);
        }
        areaGeom.Freeze();

        Brush? fillBrush = null;
        if (LineColor is SolidColorBrush solidBrush)
        {
            var fillColor = Color.FromArgb((byte)(FillOpacity * 255), solidBrush.Color.R, solidBrush.Color.G, solidBrush.Color.B);
            // 填充色依赖 LineColor，每次创建后立即 Freeze
            var fb = new SolidColorBrush(fillColor);
            fb.Freeze();
            fillBrush = fb;
        }
        drawing.DrawGeometry(fillBrush, null, areaGeom);

        // ─── 折线（Pen 依赖 LineColor，每次创建后 Freeze） ──────────

        var lineGeom = new StreamGeometry();
        using (var ctx = lineGeom.Open())
        {
            ctx.BeginFigure(coords[0], false, false);
            for (var i = 1; i < coords.Count; i++)
            {
                ctx.LineTo(coords[i], true, true);
            }
        }
        lineGeom.Freeze();

        var linePen = new Pen(LineColor, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        linePen.Freeze();
        drawing.DrawGeometry(null, linePen, lineGeom);

        // ─── 最新数据点高亮（使用缓存 HaloBrush） ───────────────────

        var lastPt = coords[^1];
        drawing.DrawEllipse(HaloBrush, null, lastPt, 6, 6);
        drawing.DrawEllipse(LineColor, null, lastPt, 3, 3);
    }
}
