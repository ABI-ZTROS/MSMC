// -----------------------------------------------------------------------------
// 文件名: ColorPickerControl.xaml.cs
// 命名空间: McServerGuard.Views.Controls
// 功能描述: 颜色拾取器自定义控件代码隐藏类，基于依赖属性 SelectedColor
//           实现 RGB 滑块、十六进制文本输入与颜色预览的双向同步。
// 依赖组件: PresentationFramework, System.Windows.Media
// 设计模式: 代码隐藏模式, 自定义控件 (DependencyProperty)
// -----------------------------------------------------------------------------
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace McServerGuard.Views.Controls;

/// <summary>
/// 颜色拾取器自定义控件。
/// 通过 SelectedColor 依赖属性对外暴露颜色值，内部维护 RGB 滑块、
/// 十六进制文本框与颜色预览三者之间的同步更新，
/// 使用 _isUpdating 标志位防止递归更新导致的栈溢出。
/// </summary>
public partial class ColorPickerControl : UserControl
{
    /// <summary>
    /// 选中颜色依赖属性。支持双向绑定，属性变更时触发 UI 同步更新。
    /// 默认值为蓝色 (#3B82F6)。
    /// </summary>
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorPickerControl),
            new PropertyMetadata(Color.FromRgb(0x3B, 0x82, 0xF6), OnSelectedColorChanged));

    /// <summary>
    /// 获取或设置当前选中的颜色值。
    /// </summary>
    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private bool _isUpdating;

    // ─── 滑块节流字段 ───

    private DispatcherTimer? _colorUpdateTimer;
    private bool _pendingColorUpdate;

    public ColorPickerControl()
    {
        InitializeComponent();

        RSlider.ValueChanged += RGB_Slider_ValueChanged;
        GSlider.ValueChanged += RGB_Slider_ValueChanged;
        BSlider.ValueChanged += RGB_Slider_ValueChanged;
        HexTextBox.LostFocus += HexTextBox_LostFocus;
        HexTextBox.KeyDown += HexTextBox_KeyDown;

        Unloaded += OnUnloaded;

        UpdateUI(SelectedColor);
    }

    /// <summary>
    /// SelectedColor 依赖属性变更回调。
    /// 当属性值由外部绑定更改时，同步更新所有 UI 元素的显示状态。
    /// </summary>
    /// <param name="d">依赖对象实例</param>
    /// <param name="e">属性变更事件参数</param>
    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerControl picker)
        {
            picker.UpdateUI((Color)e.NewValue);
        }
    }

    /// <summary>
    /// 根据指定颜色更新所有 UI 元素（滑块、数值文本、预览刷、十六进制文本）。
    /// 使用 _isUpdating 标志位阻止 ValueChanged 等事件触发反向更新。
    /// </summary>
    /// <param name="color">目标颜色值</param>
    private void UpdateUI(Color color)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        try
        {
            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;

            RValueText.Text = color.R.ToString();
            GValueText.Text = color.G.ToString();
            BValueText.Text = color.B.ToString();

            PreviewBrush.Color = color;
            HexTextBox.Text = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        finally
        {
            _isUpdating = false;
        }
    }

    // RGB 滑块 ValueChanged 事件处理：使用节流机制延迟更新颜色
    private void RGB_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        // 节流处理：避免滑块拖动时高频更新
        _pendingColorUpdate = true;

        // 确保计时器已创建
        _colorUpdateTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _colorUpdateTimer.Tick -= ColorUpdateTimer_Tick; // 避免重复订阅
        _colorUpdateTimer.Tick += ColorUpdateTimer_Tick;

        _colorUpdateTimer.Stop();
        _colorUpdateTimer.Start();
    }

    private void ColorUpdateTimer_Tick(object? sender, EventArgs e)
    {
        _colorUpdateTimer?.Stop();

        if (_pendingColorUpdate)
        {
            _pendingColorUpdate = false;
            ApplySliderColor();
        }
    }

    /// <summary>
    /// 应用滑块当前值到颜色属性。
    /// 统一从滑块读取 RGB 值并同步至 SelectedColor 与 UI 元素。
    /// </summary>
    private void ApplySliderColor()
    {
        _isUpdating = true;

        try
        {
            var r = (byte)RSlider.Value;
            var g = (byte)GSlider.Value;
            var b = (byte)BSlider.Value;

            var color = Color.FromRgb(r, g, b);
            PreviewBrush.Color = color;

            RValueText.Text = r.ToString();
            GValueText.Text = g.ToString();
            BValueText.Text = b.ToString();

            HexTextBox.Text = $"#FF{r:X2}{g:X2}{b:X2}";

            SelectedColor = color;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    // 十六进制文本框 KeyDown 事件处理：按下回车键时应用颜色
    private void HexTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            ApplyHexColor();
        }
    }

    // 十六进制文本框 LostFocus 事件处理：失去焦点时应用颜色
    private void HexTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyHexColor();
    }

    /// <summary>
    /// 解析十六进制文本并应用为当前选中颜色。
    /// 解析失败时恢复为当前颜色的文本表示，避免非法值残留。
    /// </summary>
    private void ApplyHexColor()
    {
        try
        {
            var text = HexTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            var color = (Color)ColorConverter.ConvertFromString(text);
            SelectedColor = color;
        }
        catch
        {
            HexTextBox.Text = $"#{SelectedColor.A:X2}{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // 清理节流计时器
        if (_colorUpdateTimer != null)
        {
            _colorUpdateTimer.Stop();
            _colorUpdateTimer.Tick -= ColorUpdateTimer_Tick;
            _colorUpdateTimer = null;
        }
    }
}
