using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace McServerGuard.Views.Controls;

public partial class ColorPickerControl : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorPickerControl),
            new PropertyMetadata(Color.FromRgb(0x3B, 0x82, 0xF6), OnSelectedColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private bool _isUpdating;

    public ColorPickerControl()
    {
        InitializeComponent();

        RSlider.ValueChanged += RGB_Slider_ValueChanged;
        GSlider.ValueChanged += RGB_Slider_ValueChanged;
        BSlider.ValueChanged += RGB_Slider_ValueChanged;
        HexTextBox.LostFocus += HexTextBox_LostFocus;
        HexTextBox.KeyDown += HexTextBox_KeyDown;

        UpdateUI(SelectedColor);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerControl picker)
        {
            picker.UpdateUI((Color)e.NewValue);
        }
    }

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

    private void RGB_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
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

    private void HexTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            ApplyHexColor();
        }
    }

    private void HexTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyHexColor();
    }

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
}
