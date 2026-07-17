using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace McServerGuard.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly Services.IThemeService _themeService;
    private readonly Services.IToastNotificationService _toastService;

    [ObservableProperty]
    private Color _primaryColor = Color.FromRgb(0x3B, 0x82, 0xF6);

    [ObservableProperty]
    private Color _accentColor = Color.FromRgb(0xFB, 0x71, 0x85);

    [ObservableProperty]
    private Color _backgroundColor = Color.FromRgb(0x02, 0x06, 0x17);

    [ObservableProperty]
    private Color _cardColor = Color.FromRgb(0x0F, 0x17, 0x2A);

    [ObservableProperty]
    private Color _textColor = Color.FromRgb(0xE2, 0xE8, 0xF0);

    [ObservableProperty]
    private Color _borderColor = Color.FromRgb(0x33, 0x41, 0x55);

    /// <summary>主色画刷 —— 给 Border.Background 绑定用的</summary>
    public SolidColorBrush PrimaryColorBrush => new SolidColorBrush(PrimaryColor);

    /// <summary>强调色画刷 —— 给 Border.Background 绑定用的</summary>
    public SolidColorBrush AccentColorBrush => new SolidColorBrush(AccentColor);

    /// <summary>背景色画刷 —— 给预览 Border 绑定用的</summary>
    public SolidColorBrush BackgroundColorBrush => new SolidColorBrush(BackgroundColor);

    /// <summary>卡片色画刷 —— 给预览 Border 绑定用的</summary>
    public SolidColorBrush CardColorBrush => new SolidColorBrush(CardColor);

    /// <summary>文字色画刷 —— 给预览 Border 绑定用的</summary>
    public SolidColorBrush TextColorBrush => new SolidColorBrush(TextColor);

    /// <summary>边框色画刷 —— 给预览 Border 绑定用的</summary>
    public SolidColorBrush BorderColorBrush => new SolidColorBrush(BorderColor);

    [ObservableProperty]
    private int _cornerRadius = 12;

    [ObservableProperty]
    private int _animationDuration = 300;

    [ObservableProperty]
    private bool _enableAnimations = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string PrimaryColorHex
    {
        get => $"#{PrimaryColor.A:X2}{PrimaryColor.R:X2}{PrimaryColor.G:X2}{PrimaryColor.B:X2}";
        set
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                PrimaryColor = color;
            }
            catch
            {
                StatusMessage = "无效的颜色值";
            }
        }
    }

    public string AccentColorHex
    {
        get => $"#{AccentColor.A:X2}{AccentColor.R:X2}{AccentColor.G:X2}{AccentColor.B:X2}";
        set
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                AccentColor = color;
            }
            catch
            {
                StatusMessage = "无效的颜色值";
            }
        }
    }

    public string BackgroundColorHex
    {
        get => $"#{BackgroundColor.A:X2}{BackgroundColor.R:X2}{BackgroundColor.G:X2}{BackgroundColor.B:X2}";
        set
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                BackgroundColor = color;
            }
            catch
            {
                StatusMessage = "无效的颜色值";
            }
        }
    }

    public string CardColorHex
    {
        get => $"#{CardColor.A:X2}{CardColor.R:X2}{CardColor.G:X2}{CardColor.B:X2}";
        set
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                CardColor = color;
            }
            catch
            {
                StatusMessage = "无效的颜色值";
            }
        }
    }

    public string TextColorHex
    {
        get => $"#{TextColor.A:X2}{TextColor.R:X2}{TextColor.G:X2}{TextColor.B:X2}";
        set
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                TextColor = color;
            }
            catch
            {
                StatusMessage = "无效的颜色值";
            }
        }
    }

    public string BorderColorHex
    {
        get => $"#{BorderColor.A:X2}{BorderColor.R:X2}{BorderColor.G:X2}{BorderColor.B:X2}";
        set
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                BorderColor = color;
            }
            catch
            {
                StatusMessage = "无效的颜色值";
            }
        }
    }

    public SettingsViewModel(Services.IThemeService themeService, Services.IToastNotificationService toastService)
    {
        _themeService = themeService;
        _toastService = toastService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        _themeService.LoadSettings();
        
        PrimaryColor = _themeService.PrimaryColor;
        AccentColor = _themeService.AccentColor;
        BackgroundColor = _themeService.BackgroundColor;
        CardColor = _themeService.CardColor;
        TextColor = _themeService.TextColor;
        BorderColor = _themeService.BorderColor;
        CornerRadius = _themeService.CornerRadius;
        AnimationDuration = _themeService.AnimationDuration;
        EnableAnimations = _themeService.EnableAnimations;

        StatusMessage = "设置已加载";
        Log.Information("⚙️ 设置页面已加载");
    }

    [RelayCommand]
    private void SetPrimaryColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            PrimaryColor = color;
            StatusMessage = "主色调已更新";
        }
        catch (Exception ex)
        {
            StatusMessage = $"颜色设置失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetAccentColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            AccentColor = color;
            StatusMessage = "强调色已更新";
        }
        catch (Exception ex)
        {
            StatusMessage = $"颜色设置失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetBackgroundColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            BackgroundColor = color;
            StatusMessage = "背景色已更新";
        }
        catch (Exception ex)
        {
            StatusMessage = $"颜色设置失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetCardColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            CardColor = color;
            StatusMessage = "卡片色已更新";
        }
        catch (Exception ex)
        {
            StatusMessage = $"颜色设置失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetTextColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            TextColor = color;
            StatusMessage = "文字色已更新";
        }
        catch (Exception ex)
        {
            StatusMessage = $"颜色设置失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetBorderColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            BorderColor = color;
            StatusMessage = "边框色已更新";
        }
        catch (Exception ex)
        {
            StatusMessage = $"颜色设置失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ApplyTheme()
    {
        try
        {
            _themeService.PrimaryColor = PrimaryColor;
            _themeService.AccentColor = AccentColor;
            _themeService.BackgroundColor = BackgroundColor;
            _themeService.CardColor = CardColor;
            _themeService.TextColor = TextColor;
            _themeService.BorderColor = BorderColor;
            _themeService.CornerRadius = CornerRadius;
            _themeService.AnimationDuration = AnimationDuration;
            _themeService.EnableAnimations = EnableAnimations;

            StatusMessage = "主题已应用";
            Log.Information("🎨 主题设置已应用");
        }
        catch (Exception ex)
        {
            StatusMessage = $"主题应用失败: {ex.Message}";
            Log.Error(ex, "❌ 主题应用失败");
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            _themeService.SaveSettings();
            StatusMessage = "设置已保存";
            _toastService.ShowSuccess("设置已保存", "所有设置已保存到本地");
            Log.Information("💾 设置已保存");
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
            _toastService.ShowError("保存失败", ex.Message);
            Log.Error(ex, "❌ 设置保存失败");
        }
    }

    [RelayCommand]
    private void SetPreset(string preset)
    {
        switch (preset)
        {
            case "SkyBlue":
                PrimaryColor = Color.FromRgb(0x3B, 0x82, 0xF6);
                AccentColor = Color.FromRgb(0xFB, 0x71, 0x85);
                BackgroundColor = Color.FromRgb(0x02, 0x06, 0x17);
                CardColor = Color.FromRgb(0x0F, 0x17, 0x2A);
                TextColor = Color.FromRgb(0xE2, 0xE8, 0xF0);
                BorderColor = Color.FromRgb(0x33, 0x41, 0x55);
                break;
            case "OceanBlue":
                PrimaryColor = Color.FromRgb(0x00, 0x97, 0xA7);
                AccentColor = Color.FromRgb(0xFF, 0xD7, 0x40);
                BackgroundColor = Color.FromRgb(0x0A, 0x19, 0x29);
                CardColor = Color.FromRgb(0x13, 0x2F, 0x4C);
                TextColor = Color.FromRgb(0xE3, 0xF2, 0xFD);
                BorderColor = Color.FromRgb(0x1E, 0x49, 0x70);
                break;
            case "BlueOrange":
                PrimaryColor = Color.FromRgb(21, 101, 192);
                AccentColor = Color.FromRgb(255, 152, 0);
                BackgroundColor = Color.FromRgb(0x0A, 0x14, 0x28);
                CardColor = Color.FromRgb(0x16, 0x20, 0x3A);
                TextColor = Color.FromRgb(0xE6, 0xED, 0xF3);
                BorderColor = Color.FromRgb(0x1E, 0x3A, 0x5F);
                break;
            case "TealPink":
                PrimaryColor = Color.FromRgb(0, 137, 123);
                AccentColor = Color.FromRgb(233, 30, 99);
                BackgroundColor = Color.FromRgb(0x0A, 0x1F, 0x1C);
                CardColor = Color.FromRgb(0x14, 0x30, 0x2B);
                TextColor = Color.FromRgb(0xE0, 0xF5, 0xF0);
                BorderColor = Color.FromRgb(0x1F, 0x4A, 0x42);
                break;
            case "RedYellow":
                PrimaryColor = Color.FromRgb(198, 40, 40);
                AccentColor = Color.FromRgb(255, 214, 0);
                BackgroundColor = Color.FromRgb(0x1A, 0x0F, 0x0A);
                CardColor = Color.FromRgb(0x2E, 0x1E, 0x16);
                TextColor = Color.FromRgb(0xF5, 0xE6, 0xE0);
                BorderColor = Color.FromRgb(0x4A, 0x2A, 0x1F);
                break;
        }
        StatusMessage = $"已应用预设: {preset}";
        Log.Information("🎨 已应用预设: {Preset}", preset);
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        try
        {
            _themeService.ResetToDefault();
            
            PrimaryColor = _themeService.PrimaryColor;
            AccentColor = _themeService.AccentColor;
            BackgroundColor = _themeService.BackgroundColor;
            CardColor = _themeService.CardColor;
            TextColor = _themeService.TextColor;
            BorderColor = _themeService.BorderColor;
            CornerRadius = _themeService.CornerRadius;
            AnimationDuration = _themeService.AnimationDuration;
            EnableAnimations = _themeService.EnableAnimations;
            OnPropertyChanged(nameof(PrimaryColorHex));
            OnPropertyChanged(nameof(AccentColorHex));
            OnPropertyChanged(nameof(BackgroundColorHex));
            OnPropertyChanged(nameof(CardColorHex));
            OnPropertyChanged(nameof(TextColorHex));
            OnPropertyChanged(nameof(BorderColorHex));

            StatusMessage = "已重置为默认值";
            _toastService.ShowInfo("设置已重置", "所有设置已恢复为默认值");
            Log.Information("🔄 设置已重置");
        }
        catch (Exception ex)
        {
            StatusMessage = $"重置失败: {ex.Message}";
            Log.Error(ex, "❌ 设置重置失败");
        }
    }

    [RelayCommand]
    private void TestNotification()
    {
        _toastService.ShowSuccess("测试通知", "这是一条测试通知，通知功能正常工作！");
        StatusMessage = "测试通知已发送";
        Log.Information("🔔 测试通知已发送");
    }

    [RelayCommand]
    private void ApplyAndSave()
    {
        ApplyTheme();
        SaveSettings();
    }

    partial void OnPrimaryColorChanged(Color value)
    {
        _themeService.PrimaryColor = value;
        OnPropertyChanged(nameof(PrimaryColorHex));
        OnPropertyChanged(nameof(PrimaryColorBrush));
    }

    partial void OnAccentColorChanged(Color value)
    {
        _themeService.AccentColor = value;
        OnPropertyChanged(nameof(AccentColorHex));
        OnPropertyChanged(nameof(AccentColorBrush));
    }

    partial void OnBackgroundColorChanged(Color value)
    {
        _themeService.BackgroundColor = value;
        OnPropertyChanged(nameof(BackgroundColorHex));
        OnPropertyChanged(nameof(BackgroundColorBrush));
    }

    partial void OnCardColorChanged(Color value)
    {
        _themeService.CardColor = value;
        OnPropertyChanged(nameof(CardColorHex));
        OnPropertyChanged(nameof(CardColorBrush));
    }

    partial void OnTextColorChanged(Color value)
    {
        _themeService.TextColor = value;
        OnPropertyChanged(nameof(TextColorHex));
        OnPropertyChanged(nameof(TextColorBrush));
    }

    partial void OnBorderColorChanged(Color value)
    {
        _themeService.BorderColor = value;
        OnPropertyChanged(nameof(BorderColorHex));
        OnPropertyChanged(nameof(BorderColorBrush));
    }

    partial void OnCornerRadiusChanged(int value)
    {
        _themeService.CornerRadius = value;
    }
}
