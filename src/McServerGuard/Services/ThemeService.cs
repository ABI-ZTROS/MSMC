// -----------------------------------------------------------------------------
// 文件名: ThemeService.cs
// 命名空间: McServerGuard.Services
// 功能描述: 提供应用主题管理功能，支持颜色方案、圆角、动画等视觉元素的动态配置与持久化
// 依赖组件: MaterialDesignThemes.Wpf.PaletteHelper, System.Windows.Media, System.Text.Json
// 设计模式: 单例模式（DI容器注册）、观察者模式（属性变更触发主题应用）
// -----------------------------------------------------------------------------
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace McServerGuard.Services;

/// <summary>
/// 主题服务接口
/// 定义主题颜色、圆角、动画等视觉参数的配置与应用契约
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// 主色调
    /// </summary>
    Color PrimaryColor { get; set; }

    /// <summary>
    /// 强调色
    /// </summary>
    Color AccentColor { get; set; }

    /// <summary>
    /// 背景色
    /// </summary>
    Color BackgroundColor { get; set; }

    /// <summary>
    /// 卡片背景色
    /// </summary>
    Color CardColor { get; set; }

    /// <summary>
    /// 文本颜色
    /// </summary>
    Color TextColor { get; set; }

    /// <summary>
    /// 边框颜色
    /// </summary>
    Color BorderColor { get; set; }

    /// <summary>
    /// 圆角半径（像素）
    /// </summary>
    int CornerRadius { get; set; }

    /// <summary>
    /// 动画时长（毫秒）
    /// </summary>
    int AnimationDuration { get; set; }

    /// <summary>
    /// 是否启用动画效果
    /// </summary>
    bool EnableAnimations { get; set; }

    /// <summary>
    /// 应用当前主题配置到界面
    /// </summary>
    void ApplyTheme();

    /// <summary>
    /// 从本地配置文件加载主题设置
    /// </summary>
    void LoadSettings();

    /// <summary>
    /// 保存当前主题设置到本地配置文件
    /// </summary>
    void SaveSettings();

    /// <summary>
    /// 重置为默认主题配置
    /// </summary>
    void ResetToDefault();

    /// <summary>
    /// 开始批量更新模式
    /// 在此模式下，属性变更不会立即触发主题应用
    /// </summary>
    void BeginBatchUpdate();

    /// <summary>
    /// 结束批量更新模式
    /// 调用此方法时会立即应用一次主题
    /// </summary>
    void EndBatchUpdate();
}

/// <summary>
/// 主题设置数据传输对象
/// 用于 JSON 序列化/反序列化主题配置
/// </summary>
public class ThemeSettings
{
    /// <summary>
    /// 主色值（十六进制字符串）
    /// </summary>
    public string PrimaryColor { get; set; } = "#FF3B82F6";

    /// <summary>
    /// 强调色值（十六进制字符串）
    /// </summary>
    public string AccentColor { get; set; } = "#FFFB7185";

    /// <summary>
    /// 背景色值（十六进制字符串）
    /// </summary>
    public string BackgroundColor { get; set; } = "#FF020617";

    /// <summary>
    /// 卡片背景色值（十六进制字符串）
    /// </summary>
    public string CardColor { get; set; } = "#FF0F172A";

    /// <summary>
    /// 文本颜色值（十六进制字符串）
    /// </summary>
    public string TextColor { get; set; } = "#FFE2E8F0";

    /// <summary>
    /// 边框颜色值（十六进制字符串）
    /// </summary>
    public string BorderColor { get; set; } = "#FF334155";

    /// <summary>
    /// 圆角半径（像素）
    /// </summary>
    public int CornerRadius { get; set; } = 12;

    /// <summary>
    /// 动画时长（毫秒）
    /// </summary>
    public int AnimationDuration { get; set; } = 300;

    /// <summary>
    /// 是否启用动画效果
    /// </summary>
    public bool EnableAnimations { get; set; } = true;
}

/// <summary>
/// 主题管理服务
/// 负责应用视觉主题的配置、应用与持久化，集成 MaterialDesign 主题系统
/// </summary>
public class ThemeService : IThemeService
{
    /// <summary>
    /// MaterialDesign 调色板辅助工具
    /// </summary>
    private readonly PaletteHelper _paletteHelper = new();

    /// <summary>
    /// 主色字段
    /// </summary>
    private Color _primaryColor = Color.FromRgb(0x3B, 0x82, 0xF6);

    /// <summary>
    /// 强调色字段
    /// </summary>
    private Color _accentColor = Color.FromRgb(0xFB, 0x71, 0x85);

    /// <summary>
    /// 背景色字段
    /// </summary>
    private Color _backgroundColor = Color.FromRgb(0x02, 0x06, 0x17);

    /// <summary>
    /// 卡片背景色字段
    /// </summary>
    private Color _cardColor = Color.FromRgb(0x0F, 0x17, 0x2A);

    /// <summary>
    /// 文本颜色字段
    /// </summary>
    private Color _textColor = Color.FromRgb(0xE2, 0xE8, 0xF0);

    /// <summary>
    /// 边框颜色字段
    /// </summary>
    private Color _borderColor = Color.FromRgb(0x33, 0x41, 0x55);

    /// <summary>
    /// 圆角半径字段
    /// </summary>
    private int _cornerRadius = 12;

    /// <summary>
    /// 动画时长字段
    /// </summary>
    private int _animationDuration = 300;

    /// <summary>
    /// 是否启用动画字段
    /// </summary>
    private bool _enableAnimations = true;

    /// <summary>
    /// 批量更新模式标记
    /// </summary>
    private bool _isBatchUpdating;

    /// <summary>
    /// 主题配置文件路径
    /// </summary>
    private static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "McServerGuard",
        "theme-settings.json");

    /// <inheritdoc />
    public Color PrimaryColor
    {
        get => _primaryColor;
        set
        {
            _primaryColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public Color AccentColor
    {
        get => _accentColor;
        set
        {
            _accentColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public Color CardColor
    {
        get => _cardColor;
        set
        {
            _cardColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public Color TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Clamp(value, 0, 24);
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public int AnimationDuration
    {
        get => _animationDuration;
        set => _animationDuration = Math.Clamp(value, 0, 2000);
    }

    /// <inheritdoc />
    public bool EnableAnimations
    {
        get => _enableAnimations;
        set => _enableAnimations = value;
    }

    /// <inheritdoc />
    public void BeginBatchUpdate() => _isBatchUpdating = true;

    /// <inheritdoc />
    public void EndBatchUpdate()
    {
        _isBatchUpdating = false;
        ApplyTheme();
    }

    /// <summary>
    /// 应用当前主题配置
    /// 更新 MaterialDesign 调色板并同步应用级资源字典
    /// </summary>
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

    /// <summary>
    /// 更新应用级资源字典
    /// 将主题参数派生的各类画刷写入 Application.Current.Resources
    /// </summary>
    private void UpdateResources()
    {
        var resources = System.Windows.Application.Current.Resources;

        // 卡片色族 —— 从 CardColor 派生（悬停更亮、终端更深）
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

        // 背景色 —— 覆盖 MaterialDesign 窗口底色 + 深背景（用于更深的分区）
        var bgBrush = new SolidColorBrush(_backgroundColor);
        bgBrush.Freeze();
        var deepBgBrush = new SolidColorBrush(DarkenColor(_backgroundColor, 0.3));
        deepBgBrush.Freeze();
        resources["MaterialDesignPaper"] = bgBrush;
        resources["MaterialDesignCardBackground"] = cardBgBrush;
        resources["MaterialDesignCardBackgroundBrush"] = cardBgBrush;
        resources["MaterialDesignToolBarBackground"] = cardBgBrush;
        resources["MaterialDesignToolBarBackgroundBrush"] = cardBgBrush;
        resources["MaterialDesignPaperBrush"] = bgBrush;
        resources["DeepBackgroundBrush"] = deepBgBrush;

        // 边框色 —— 柔和边框 + 卡片半透明边框
        var borderBrush = new SolidColorBrush(_borderColor);
        borderBrush.Freeze();
        var cardBorderBrush = new SolidColorBrush(Color.FromArgb(0x33, _borderColor.R, _borderColor.G, _borderColor.B));
        cardBorderBrush.Freeze();
        resources["SubtleBorderBrush"] = borderBrush;
        resources["CardSubtleBorderBrush"] = cardBorderBrush;

        // 主色族 —— 保持从 PrimaryColor 派生
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

        // 文字色 —— 覆盖 MaterialDesign 全套文字色（正文/次要/三级/标题/副标题/说明）
        var textBrush = new SolidColorBrush(_textColor);
        textBrush.Freeze();
        var textLightBrush = new SolidColorBrush(Color.FromArgb(180, _textColor.R, _textColor.G, _textColor.B));
        textLightBrush.Freeze();
        var textMediumBrush = new SolidColorBrush(Color.FromArgb(220, _textColor.R, _textColor.G, _textColor.B));
        textMediumBrush.Freeze();
        var textDimBrush = new SolidColorBrush(Color.FromArgb(120, _textColor.R, _textColor.G, _textColor.B));
        textDimBrush.Freeze();
        resources["MaterialDesignBody"] = textBrush;
        resources["MaterialDesignBodyLight"] = textLightBrush;
        resources["MaterialDesignColumnHeader"] = textMediumBrush;
        resources["MaterialDesignSubtitleTextBlock"] = textBrush;
        resources["MaterialDesignCaptionTextBlock"] = textLightBrush;
        resources["MaterialDesignTextFieldBoxBackground"] = cardBgBrush;
        resources["MaterialDesignTextBoxBorder"] = cardBorderBrush;
        resources["MaterialDesignComboBoxItemHoverBackground"] = cardHoverBrush;
        resources["MaterialDesignComboBoxItemSelectedBackground"] = navHoverBrush;
        resources["MaterialDesignComboBoxItemSelectedHoverBackground"] = primaryHoverBrush;
        resources["MaterialDesignComboBoxItemSelectedText"] = textBrush;
        resources["MaterialDesignComboBoxItemText"] = textBrush;
        resources["MaterialDesignFlatButtonClick"] = cardHoverBrush;
        resources["MaterialDesignFlatButtonHover"] = cardHoverBrush;

        // 强调色族 —— 保持从 AccentColor 派生
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

        // 信号灯色族 + 危险色 + 主色半透明
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

        // 字体 —— 嵌入 Space Grotesk + 简体中文回退
        // Space Grotesk 是纯英文字体，不含中文字形。
        // 如果不指定回退字体，WPF 会走系统字体回退，可能选到繁体字体（如 MingLiU），
        // 导致界面中文显示为繁体字形。这里显式指定 Microsoft YaHei UI 作为中文回退。
        try
        {
            var fontFamily = new FontFamily(
                new Uri("pack://application:,,,/McServerGuard;component/Resources/Fonts/"),
                "./#Space Grotesk Light, Microsoft YaHei UI");
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
            var defaultFont = new FontFamily("Microsoft YaHei UI, Segoe UI");
            resources["AppFontFamily"] = defaultFont;
        }

        // 圆角 —— 三级体系：Small / Default / Large，所有控件统一通过 DynamicResource 引用
        var defaultRadius = new System.Windows.CornerRadius(_cornerRadius);
        var smallRadius = new System.Windows.CornerRadius(Math.Max(0, _cornerRadius - 4));
        var largeRadius = new System.Windows.CornerRadius(_cornerRadius + 4);
        resources["AppCornerRadius"] = defaultRadius;
        resources["AppCornerRadiusSmall"] = smallRadius;
        resources["AppCornerRadiusLarge"] = largeRadius;
        // double 版本，给 Card.UniformCornerRadius 等只接受单一数值的属性用
        resources["AppCornerRadiusValue"] = (double)_cornerRadius;
        resources["AppCornerRadiusSmallValue"] = (double)Math.Max(0, _cornerRadius - 4);
        resources["AppCornerRadiusLargeValue"] = (double)(_cornerRadius + 4);

        // MaterialDesign 控件圆角统一
        resources["MaterialDesignButtonCornerRadius"] = defaultRadius;
        resources["MaterialDesignTextBoxCornerRadius"] = smallRadius;
        resources["MaterialDesignCardCornerRadius"] = (double)_cornerRadius;
    }

    /// <summary>
    /// 按比例加深颜色
    /// </summary>
    /// <param name="color">原始颜色</param>
    /// <param name="amount">加深比例（0-1）</param>
    /// <returns>加深后的颜色</returns>
    private static Color DarkenColor(Color color, double amount)
    {
        var r = (byte)Math.Max(0, color.R * (1 - amount));
        var g = (byte)Math.Max(0, color.G * (1 - amount));
        var b = (byte)Math.Max(0, color.B * (1 - amount));
        return Color.FromArgb(color.A, r, g, b);
    }

    /// <summary>
    /// 按比例加亮颜色
    /// </summary>
    /// <param name="color">原始颜色</param>
    /// <param name="amount">加亮比例（0-1）</param>
    /// <returns>加亮后的颜色</returns>
    private static Color LightenColor(Color color, double amount)
    {
        var r = (byte)Math.Min(255, color.R + (255 - color.R) * amount);
        var g = (byte)Math.Min(255, color.G + (255 - color.G) * amount);
        var b = (byte)Math.Min(255, color.B + (255 - color.B) * amount);
        return Color.FromArgb(color.A, r, g, b);
    }

    /// <summary>
    /// 从本地配置文件加载主题设置
    /// 加载失败时自动备份损坏文件并重置为默认值
    /// </summary>
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

    /// <summary>
    /// 保存当前主题设置到本地配置文件
    /// </summary>
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

    /// <summary>
    /// 重置为默认主题配置
    /// 重置后立即应用并保存
    /// </summary>
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
