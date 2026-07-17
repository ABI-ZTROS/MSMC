using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace McServerGuard.Services;

public interface IThemeService
{
    Color PrimaryColor { get; set; }
    Color AccentColor { get; set; }
    Color BackgroundColor { get; set; }
    Color CardColor { get; set; }
    Color TextColor { get; set; }
    Color BorderColor { get; set; }
    int CornerRadius { get; set; }
    int AnimationDuration { get; set; }
    bool EnableAnimations { get; set; }

    void ApplyTheme();
    void LoadSettings();
    void SaveSettings();
    void ResetToDefault();
}

public class ThemeSettings
{
    public string PrimaryColor { get; set; } = "#FF3B82F6";
    public string AccentColor { get; set; } = "#FFFB7185";
    public string BackgroundColor { get; set; } = "#FF020617";
    public string CardColor { get; set; } = "#FF0F172A";
    public string TextColor { get; set; } = "#FFE2E8F0";
    public string BorderColor { get; set; } = "#FF334155";
    public int CornerRadius { get; set; } = 12;
    public int AnimationDuration { get; set; } = 300;
    public bool EnableAnimations { get; set; } = true;
}

public class ThemeService : IThemeService
{
    private readonly PaletteHelper _paletteHelper = new();
    private Color _primaryColor = Color.FromRgb(0x3B, 0x82, 0xF6);
    private Color _accentColor = Color.FromRgb(0xFB, 0x71, 0x85);
    private Color _backgroundColor = Color.FromRgb(0x02, 0x06, 0x17);
    private Color _cardColor = Color.FromRgb(0x0F, 0x17, 0x2A);
    private Color _textColor = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private Color _borderColor = Color.FromRgb(0x33, 0x41, 0x55);
    private int _cornerRadius = 12;
    private int _animationDuration = 300;
    private bool _enableAnimations = true;

    private static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "McServerGuard",
        "theme-settings.json");

    public Color PrimaryColor
    {
        get => _primaryColor;
        set
        {
            _primaryColor = value;
            ApplyTheme();
        }
    }

    public Color AccentColor
    {
        get => _accentColor;
        set
        {
            _accentColor = value;
            ApplyTheme();
        }
    }

    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            ApplyTheme();
        }
    }

    public Color CardColor
    {
        get => _cardColor;
        set
        {
            _cardColor = value;
            ApplyTheme();
        }
    }

    public Color TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            ApplyTheme();
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            ApplyTheme();
        }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Clamp(value, 0, 24);
            ApplyTheme();
        }
    }

    public int AnimationDuration
    {
        get => _animationDuration;
        set => _animationDuration = Math.Clamp(value, 0, 2000);
    }

    public bool EnableAnimations
    {
        get => _enableAnimations;
        set => _enableAnimations = value;
    }

    public void ApplyTheme()
    {
        try
        {
            var theme = _paletteHelper.GetTheme();

            theme.SetPrimaryColor(_primaryColor);
            theme.SetSecondaryColor(_accentColor);

            _paletteHelper.SetTheme(theme);

            UpdateResources();

            Log.Information("🎨 主题已更新: 主色={Primary}, 强调色={Accent}, 圆角={Radius}",
                _primaryColor, _accentColor, _cornerRadius);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 主题应用失败");
        }
    }

    private void UpdateResources()
    {
        var resources = System.Windows.Application.Current.Resources;

        // 🃏 卡片色族 —— 从 CardColor 派生（悬停更亮、终端更深）
        var cardBg = _cardColor;
        var cardHover = LightenColor(cardBg, 0.08);
        var navHover = LightenColor(cardBg, 0.15);
        var terminalBg = DarkenColor(cardBg, 0.1);

        var cardBgBrush = new SolidColorBrush(cardBg);
        cardBgBrush.Freeze();
        var cardHoverBrush = new SolidColorBrush(cardHover);
        cardHoverBrush.Freeze();
        var navHoverBrush = new SolidColorBrush(navHover);
        navHoverBrush.Freeze();
        var terminalBgBrush = new SolidColorBrush(terminalBg);
        terminalBgBrush.Freeze();
        var loadingOverlayBrush = new SolidColorBrush(Color.FromArgb(0xCC, cardBg.R, cardBg.G, cardBg.B));
        loadingOverlayBrush.Freeze();

        resources["CardBackgroundBrush"] = cardBgBrush;
        resources["CardHoverBrush"] = cardHoverBrush;
        resources["NavItemHoverBrush"] = navHoverBrush;
        resources["TerminalBackgroundBrush"] = terminalBgBrush;
        resources["LoadingOverlayBrush"] = loadingOverlayBrush;

        // 🪟 背景色 —— 覆盖 MaterialDesign 窗口底色 + 深背景（用于更深的分区）
        var bgBrush = new SolidColorBrush(_backgroundColor);
        bgBrush.Freeze();
        var deepBgBrush = new SolidColorBrush(DarkenColor(_backgroundColor, 0.3));
        deepBgBrush.Freeze();
        resources["MaterialDesignPaper"] = bgBrush;
        resources["DeepBackgroundBrush"] = deepBgBrush;

        // ✏️ 文字色 —— 覆盖 MaterialDesign 正文 + 次要文字（alpha 180 派生）
        var textBrush = new SolidColorBrush(_textColor);
        textBrush.Freeze();
        var textLightBrush = new SolidColorBrush(Color.FromArgb(180, _textColor.R, _textColor.G, _textColor.B));
        textLightBrush.Freeze();
        resources["MaterialDesignBody"] = textBrush;
        resources["MaterialDesignBodyLight"] = textLightBrush;

        // 📏 边框色 —— 柔和边框 + 卡片半透明边框
        var borderBrush = new SolidColorBrush(_borderColor);
        borderBrush.Freeze();
        var cardBorderBrush = new SolidColorBrush(Color.FromArgb(0x33, _borderColor.R, _borderColor.G, _borderColor.B));
        cardBorderBrush.Freeze();
        resources["SubtleBorderBrush"] = borderBrush;
        resources["CardSubtleBorderBrush"] = cardBorderBrush;

        // 🟣 主色族 —— 保持从 PrimaryColor 派生
        var primaryBrush = new SolidColorBrush(_primaryColor);
        primaryBrush.Freeze();
        var primarySubtleBrush = new SolidColorBrush(Color.FromArgb(0x33, _primaryColor.R, _primaryColor.G, _primaryColor.B));
        primarySubtleBrush.Freeze();
        var primaryIndicatorBrush = new SolidColorBrush(LightenColor(_primaryColor, 0.15));
        primaryIndicatorBrush.Freeze();
        var primaryHoverBrush = new SolidColorBrush(LightenColor(_primaryColor, 0.05));
        primaryHoverBrush.Freeze();
        resources["NavItemSelectedBrush"] = primaryBrush;
        resources["PrimarySubtleBorderBrush"] = primarySubtleBrush;
        resources["NavItemSelectedIndicatorBrush"] = primaryIndicatorBrush;
        resources["NavItemSelectedHoverBrush"] = primaryHoverBrush;

        // 💚 强调色族 —— 保持从 AccentColor 派生
        var accentBrush = new SolidColorBrush(_accentColor);
        accentBrush.Freeze();
        var accentSubtleBrush = new SolidColorBrush(Color.FromArgb(0x33, _accentColor.R, _accentColor.G, _accentColor.B));
        accentSubtleBrush.Freeze();
        resources["AccentTextBrush"] = accentBrush;
        resources["AccentSubtleBorderBrush"] = accentSubtleBrush;

        var accentGradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(_primaryColor, 0),
                new GradientStop(LightenColor(_primaryColor, 0.2), 1)
            }
        };
        accentGradient.Freeze();
        resources["AccentGradientBrush"] = accentGradient;

        // 🚦 信号灯色族 + 危险色 + 主色半透明
        var gaugeGreen = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        gaugeGreen.Freeze();
        var gaugeYellow = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
        gaugeYellow.Freeze();
        var gaugeRed = new SolidColorBrush(Color.FromRgb(0xF4, 0x36, 0x4C));
        gaugeRed.Freeze();
        var dangerBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
        dangerBrush.Freeze();
        var errorTextBrush = new SolidColorBrush(_accentColor);
        errorTextBrush.Freeze();
        var primarySubtleBgBrush = new SolidColorBrush(
            Color.FromArgb(0x1A, _primaryColor.R, _primaryColor.G, _primaryColor.B));
        primarySubtleBgBrush.Freeze();

        resources["GaugeGreenBrush"] = gaugeGreen;
        resources["GaugeYellowBrush"] = gaugeYellow;
        resources["GaugeRedBrush"] = gaugeRed;
        resources["DangerBrush"] = dangerBrush;
        resources["ErrorTextBrush"] = errorTextBrush;
        resources["PrimarySubtleBackgroundBrush"] = primarySubtleBgBrush;

        // ✏️ 字体 —— 嵌入 Space Grotesk
        try
        {
            var fontFamily = new FontFamily("pack://application:,,,/McServerGuard;component/Resources/Fonts/#Space Grotesk");
            resources["AppFontFamily"] = fontFamily;

            // 覆盖 MaterialDesign 字体
            if (resources.Contains("MaterialDesignFontFamily"))
                resources["MaterialDesignFontFamily"] = fontFamily;

            // 设置主窗口字体（如果存在）
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                System.Windows.Application.Current.MainWindow.FontFamily = fontFamily;
            }
        }
        catch
        {
            var defaultFont = new FontFamily("Segoe UI");
            resources["AppFontFamily"] = defaultFont;
        }
    }

    private static Color DarkenColor(Color color, double amount)
    {
        var r = (byte)Math.Max(0, color.R * (1 - amount));
        var g = (byte)Math.Max(0, color.G * (1 - amount));
        var b = (byte)Math.Max(0, color.B * (1 - amount));
        return Color.FromArgb(color.A, r, g, b);
    }

    private static Color LightenColor(Color color, double amount)
    {
        var r = (byte)Math.Min(255, color.R + (255 - color.R) * amount);
        var g = (byte)Math.Min(255, color.G + (255 - color.G) * amount);
        var b = (byte)Math.Min(255, color.B + (255 - color.B) * amount);
        return Color.FromArgb(color.A, r, g, b);
    }

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<ThemeSettings>(json);

                if (settings != null)
                {
                    if (!string.IsNullOrEmpty(settings.PrimaryColor))
                        _primaryColor = (Color)ColorConverter.ConvertFromString(settings.PrimaryColor);

                    if (!string.IsNullOrEmpty(settings.AccentColor))
                        _accentColor = (Color)ColorConverter.ConvertFromString(settings.AccentColor);

                    if (!string.IsNullOrEmpty(settings.BackgroundColor))
                        _backgroundColor = (Color)ColorConverter.ConvertFromString(settings.BackgroundColor);

                    if (!string.IsNullOrEmpty(settings.CardColor))
                        _cardColor = (Color)ColorConverter.ConvertFromString(settings.CardColor);

                    if (!string.IsNullOrEmpty(settings.TextColor))
                        _textColor = (Color)ColorConverter.ConvertFromString(settings.TextColor);

                    if (!string.IsNullOrEmpty(settings.BorderColor))
                        _borderColor = (Color)ColorConverter.ConvertFromString(settings.BorderColor);

                    _cornerRadius = Math.Clamp(settings.CornerRadius, 0, 24);
                    _animationDuration = Math.Clamp(settings.AnimationDuration, 0, 2000);
                    _enableAnimations = settings.EnableAnimations;
                }
            }

            ApplyTheme();
            Log.Information("📂 主题设置已加载");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 加载主题设置失败，使用默认值");
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var bakPath = SettingsFilePath + ".corrupt.bak";
                    File.Copy(SettingsFilePath, bakPath, true);
                    Log.Warning("📦 已备份损坏的主题设置到: {BakPath}", bakPath);
                }
            }
            catch { /* 备份失败就算了 */ }
            ResetToDefault();
        }
    }

    public void SaveSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new ThemeSettings
            {
                PrimaryColor = _primaryColor.ToString(),
                AccentColor = _accentColor.ToString(),
                BackgroundColor = _backgroundColor.ToString(),
                CardColor = _cardColor.ToString(),
                TextColor = _textColor.ToString(),
                BorderColor = _borderColor.ToString(),
                CornerRadius = _cornerRadius,
                AnimationDuration = _animationDuration,
                EnableAnimations = _enableAnimations
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(SettingsFilePath, json);

            Log.Information("💾 主题设置已保存");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 保存主题设置失败");
        }
    }

    public void ResetToDefault()
    {
        _primaryColor = Color.FromRgb(0x3B, 0x82, 0xF6);
        _accentColor = Color.FromRgb(0xFB, 0x71, 0x85);
        _backgroundColor = Color.FromRgb(0x02, 0x06, 0x17);
        _cardColor = Color.FromRgb(0x0F, 0x17, 0x2A);
        _textColor = Color.FromRgb(0xE2, 0xE8, 0xF0);
        _borderColor = Color.FromRgb(0x33, 0x41, 0x55);
        _cornerRadius = 12;
        _animationDuration = 300;
        _enableAnimations = true;

        ApplyTheme();
        SaveSettings();
        Log.Information("🔄 主题已重置为默认值");
    }
}
