namespace McServerGuard.Views.Controls;

using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

/// <summary>
/// 自绘双系列柱状图控件。
/// 继承 FrameworkElement，通过 DrawingVisual 低开销自绘。
/// 支持上传/下载两组数据并排柱状图、Y 轴自适应、X 轴标签、当前小时高亮。
/// 遵循项目静态 Freeze 缓存 + 集合变更监听模式。
/// </summary>
public class BarChartControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private IList? _uploadData;
    private IList? _downloadData;

    private static readonly Brush GridPenBrush = CreateFrozenBrush(Color.FromArgb(40, 255, 255, 255));
    private static readonly Pen GridPen = CreateFrozenPen(GridPenBrush, 1);
    private static readonly Brush AxisLabelBrush = CreateFrozenBrush(Color.FromArgb(120, 200, 200, 200));
    private static readonly Brush FallbackUploadBrush = CreateFrozenBrush(Color.FromArgb(200, 85, 136, 255));
    private static readonly Brush FallbackDownloadBrush = CreateFrozenBrush(Color.FromArgb(200, 85, 221, 136));
    private static readonly Brush HighlightBorderBrush = CreateFrozenBrush(Colors.White);

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

    public static readonly DependencyProperty UploadDataProperty =
        DependencyProperty.Register(nameof(UploadData), typeof(IList), typeof(BarChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnUploadDataChanged));

    public static readonly DependencyProperty DownloadDataProperty =
        DependencyProperty.Register(nameof(DownloadData), typeof(IList), typeof(BarChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDownloadDataChanged));

    public static readonly DependencyProperty UploadColorProperty =
        DependencyProperty.Register(nameof(UploadColor), typeof(Color), typeof(BarChartControl),
            new FrameworkPropertyMetadata(Color.FromArgb(200, 85, 136, 255), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DownloadColorProperty =
        DependencyProperty.Register(nameof(DownloadColor), typeof(Color), typeof(BarChartControl),
            new FrameworkPropertyMetadata(Color.FromArgb(200, 85, 221, 136), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightIndexProperty =
        DependencyProperty.Register(nameof(HighlightIndex), typeof(int), typeof(BarChartControl),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public IList? UploadData
    {
        get => (IList?)GetValue(UploadDataProperty);
        set => SetValue(UploadDataProperty, value);
    }

    public IList? DownloadData
    {
        get => (IList?)GetValue(DownloadDataProperty);
        set => SetValue(DownloadDataProperty, value);
    }

    public Color UploadColor
    {
        get => (Color)GetValue(UploadColorProperty);
        set => SetValue(UploadColorProperty, value);
    }

    public Color DownloadColor
    {
        get => (Color)GetValue(DownloadColorProperty);
        set => SetValue(DownloadColorProperty, value);
    }

    public int HighlightIndex
    {
        get => (int)GetValue(HighlightIndexProperty);
        set => SetValue(HighlightIndexProperty, value);
    }

    private static void OnUploadDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (BarChartControl)d;
        if (ctrl._uploadData is INotifyCollectionChanged oldNcc)
            oldNcc.CollectionChanged -= ctrl.OnCollectionChanged;
        ctrl._uploadData = e.NewValue as IList;
        if (ctrl._uploadData is INotifyCollectionChanged newNcc)
            newNcc.CollectionChanged += ctrl.OnCollectionChanged;
        ctrl.InvalidateVisual();
    }

    private static void OnDownloadDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (BarChartControl)d;
        if (ctrl._downloadData is INotifyCollectionChanged oldNcc)
            oldNcc.CollectionChanged -= ctrl.OnCollectionChanged;
        ctrl._downloadData = e.NewValue as IList;
        if (ctrl._downloadData is INotifyCollectionChanged newNcc)
            newNcc.CollectionChanged += ctrl.OnCollectionChanged;
        ctrl.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    // ─── 构造与可视化子元素 ────────────────────────────────────────────

    public BarChartControl()
    {
        MinHeight = 120;
        MinWidth = 200;

        AddVisualChild(_visual);
        Loaded += (_, _) => InvalidateVisual();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    // ─── 绘制 ─────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        DrawBars();
        base.OnRender(dc);
    }

    private static double GetMaxValue(IList? data)
    {
        if (data is null || data.Count == 0)
            return 0;
        double max = 0;
        foreach (var item in data)
        {
            if (item is double d && d > max)
                max = d;
        }
        return max;
    }

    private static string FormatDataValue(double bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824:F1}G";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576:F1}M";
        if (bytes >= 1024) return $"{bytes / 1024:F1}K";
        return $"{bytes:F0}B";
    }

    private void DrawBars()
    {
        var w = RenderSize.Width;
        var h = RenderSize.Height;
        if (w < 20 || h < 20)
            return;

        const double leftPad = 42;
        const double rightPad = 8;
        const double topPad = 8;
        const double bottomPad = 24;

        var chartW = w - leftPad - rightPad;
        var chartH = h - topPad - bottomPad;
        if (chartW < 10 || chartH < 10)
            return;

        using var drawing = _visual.RenderOpen();

        int count = Math.Max(
            _uploadData?.Count ?? 0,
            _downloadData?.Count ?? 0);
        if (count == 0)
            return;

        double maxVal = Math.Max(GetMaxValue(_uploadData), GetMaxValue(_downloadData));
        if (maxVal < 1) maxVal = 1;

        // 绘制 Y 轴网格线和标签（4 条线）
        for (int i = 0; i <= 4; i++)
        {
            var y = topPad + chartH * i / 4.0;
            var val = maxVal * (1 - (double)i / 4);

            drawing.DrawLine(GridPen,
                new Point(leftPad, y),
                new Point(w - rightPad, y));

            var labelText = new FormattedText(FormatDataValue(val),
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                LabelTypeface, 9, AxisLabelBrush, null, 1.0);
            drawing.DrawText(labelText, new Point(2, y - labelText.Height / 2));
        }

        // 计算柱宽
        var groupWidth = chartW / count;
        var barGap = 2;
        var barWidth = Math.Max(2, (groupWidth - barGap * 3) / 2.0);

        var uploadBrush = new SolidColorBrush(UploadColor);
        uploadBrush.Freeze();
        var downloadBrush = new SolidColorBrush(DownloadColor);
        downloadBrush.Freeze();

        for (int i = 0; i < count; i++)
        {
            var groupX = leftPad + i * groupWidth;
            var highlight = i == HighlightIndex;

            // 上传柱
            if (_uploadData is not null && i < _uploadData.Count && _uploadData[i] is double upVal && upVal > 0)
            {
                var barH = upVal / maxVal * chartH;
                var barX = groupX + barGap;
                var barY = topPad + chartH - barH;
                var rect = new Rect(barX, barY, barWidth, barH);
                drawing.DrawRectangle(uploadBrush, highlight ? new Pen(HighlightBorderBrush, 1.5) : null, rect);
            }

            // 下载柱
            if (_downloadData is not null && i < _downloadData.Count && _downloadData[i] is double downVal && downVal > 0)
            {
                var barH = downVal / maxVal * chartH;
                var barX = groupX + barGap * 2 + barWidth;
                var barY = topPad + chartH - barH;
                var rect = new Rect(barX, barY, barWidth, barH);
                drawing.DrawRectangle(downloadBrush, highlight ? new Pen(HighlightBorderBrush, 1.5) : null, rect);
            }

            // X 轴标签（每 6 小时显示一次，或者数据量少时全部显示）
            if (count <= 12 || i % 6 == 0)
            {
                var label = $"{i}h";
                var labelText = new FormattedText(label,
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    LabelTypeface, 9, AxisLabelBrush, null, 1.0);
                drawing.DrawText(labelText,
                    new Point(groupX + groupWidth / 2 - labelText.Width / 2, h - bottomPad + 4));
            }
        }

        // 图例
        var legendY = 4;
        var legendFontSize = 9;

        var upLegendRect = new Rect(leftPad, legendY, 8, 8);
        drawing.DrawRectangle(uploadBrush, null, upLegendRect);
        var upLegendText = new FormattedText("上传",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            LabelTypeface, legendFontSize, AxisLabelBrush, null, 1.0);
        drawing.DrawText(upLegendText, new Point(leftPad + 12, legendY - 1));

        var downLegendX = leftPad + 12 + upLegendText.Width + 12;
        var downLegendRect = new Rect(downLegendX, legendY, 8, 8);
        drawing.DrawRectangle(downloadBrush, null, downLegendRect);
        var downLegendText = new FormattedText("下载",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            LabelTypeface, legendFontSize, AxisLabelBrush, null, 1.0);
        drawing.DrawText(downLegendText, new Point(downLegendX + 12, legendY - 1));
    }
}
