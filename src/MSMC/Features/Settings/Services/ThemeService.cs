// -----------------------------------------------------------------------------
// 文件名: ThemeService.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: 提供应用主题管理功能，支持颜色方案、圆角、动画等视觉元素的动态配置与持久化
// 依赖组件: MaterialDesignThemes.Wpf.PaletteHelper, System.Windows.Media, System.Text.Json
// 设计模式: 单例模式（DI容器注册）、观察者模式（属性变更触发主题应用）
// -----------------------------------------------------------------------------
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using io.NET.ZTR_OS.Features.Shared.Native.Services;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace io.NET.ZTR_OS.Features.Settings.Services;

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
    /// 成功色 😄（用于成功提示、通过状态等）
    /// </summary>
    Color SuccessColor { get; set; }

    /// <summary>
    /// 警告色 ⚠️（用于警告提示、低危状态等）
    /// </summary>
    Color WarningColor { get; set; }

    /// <summary>
    /// 错误色 ❌（用于错误提示、失败状态等）
    /// </summary>
    Color ErrorColor { get; set; }

    /// <summary>
    /// 仪表盘绿色 🟢（用于指标正向区间）
    /// </summary>
    Color GaugeGreenColor { get; set; }

    /// <summary>
    /// 仪表盘黄色 🟡（用于指标中间区间）
    /// </summary>
    Color GaugeYellowColor { get; set; }

    /// <summary>
    /// 仪表盘红色 🔴（用于指标负向区间）
    /// </summary>
    Color GaugeRedColor { get; set; }

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
    /// 是否为深色模式
    /// </summary>
    bool IsDarkMode { get; }

    /// <summary>
    /// 主题变更事件
    /// </summary>
    event EventHandler? ThemeChanged;

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
    /// 成功色值（十六进制字符串）——成功提示、通过状态
    /// </summary>
    public string SuccessColor { get; set; } = "#FF4CAF50";

    /// <summary>
    /// 警告色值（十六进制字符串）——警告提示、低危状态
    /// </summary>
    public string WarningColor { get; set; } = "#FFFFC107";

    /// <summary>
    /// 错误色值（十六进制字符串）——错误提示、失败状态
    /// </summary>
    public string ErrorColor { get; set; } = "#FFE53935";

    /// <summary>
    /// 仪表盘绿色值（十六进制字符串）——指标正向区间
    /// </summary>
    public string GaugeGreenColor { get; set; } = "#FF4CAF50";

    /// <summary>
    /// 仪表盘黄色值（十六进制字符串）——指标中间区间
    /// </summary>
    public string GaugeYellowColor { get; set; } = "#FFFFC107";

    /// <summary>
    /// 仪表盘红色值（十六进制字符串）——指标负向区间
    /// </summary>
    public string GaugeRedColor { get; set; } = "#FFF4364C";

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
    /// 窗口效果服务（DWM Mica/深色标题栏/圆角），只在 Windows 下可用；非 Windows 或 DI 未注册时为 null。
    /// 通过 ServiceProvider 懒解析，避免 ThemeService 在单测 / 非窗口场景下创建时强依赖。
    /// </summary>
    private readonly IWindowEffectsService? _windowEffects;

    /// <summary>上一次 ApplyTheme 时是否是深色模式（用于判断是否需要重新调用 DWM）。</summary>
    private bool _lastAppliedDarkMode;

    /// <summary>
    /// 默认构造（未注入 WindowEffectsService 时：兼容单测 / 降级场景）。
    /// </summary>
    public ThemeService() { }

    /// <summary>
    /// DI 构造函数（Windows 场景下 WindowEffectsService 由 App.xaml.cs 内联注册）。
    /// </summary>
    public ThemeService(IWindowEffectsService? windowEffects = null)
    {
        _windowEffects = windowEffects;
    }

    private Color _primaryColor = Color.FromRgb(0x3B, 0x82, 0xF6);

    private Color _accentColor = Color.FromRgb(0xFB, 0x71, 0x85);

    private Color _backgroundColor = Color.FromRgb(0x02, 0x06, 0x17);

    /// <summary>
    /// 卡片背景色字段
    /// </summary>
    private Color _cardColor = Color.FromRgb(0x0F, 0x17, 0x2A);

    private Color _textColor = Color.FromRgb(0xE2, 0xE8, 0xF0);

    private Color _borderColor = Color.FromRgb(0x33, 0x41, 0x55);

    /// <summary>
    /// 成功色字段（成功提示、通过状态）🎉
    /// </summary>
    private Color _successColor = Color.FromRgb(0x4C, 0xAF, 0x50);

    /// <summary>
    /// 警告色字段（警告提示、低危状态）⚠️
    /// </summary>
    private Color _warningColor = Color.FromRgb(0xFF, 0xC1, 0x07);

    /// <summary>
    /// 错误色字段（错误提示、失败状态）🚨
    /// </summary>
    private Color _errorColor = Color.FromRgb(0xE5, 0x39, 0x35);

    /// <summary>
    /// 仪表盘绿色字段（指标正向区间）🟢
    /// </summary>
    private Color _gaugeGreenColor = Color.FromRgb(0x4C, 0xAF, 0x50);

    /// <summary>
    /// 仪表盘黄色字段（指标中间区间）🟡
    /// </summary>
    private Color _gaugeYellowColor = Color.FromRgb(0xFF, 0xC1, 0x07);

    /// <summary>
    /// 仪表盘红色字段（指标负向区间）🔴
    /// </summary>
    private Color _gaugeRedColor = Color.FromRgb(0xF4, 0x36, 0x4C);

    private int _cornerRadius = 12;

    private int _animationDuration = 300;

    private bool _enableAnimations = true;

    /// <summary>
    /// 主题变更事件
    /// </summary>
    public event EventHandler? ThemeChanged;

    /// <summary>
    /// 批量更新模式标记
    /// </summary>
    private bool _isBatchUpdating;

    /// <summary>
    /// 主题配置文件路径
    /// </summary>
    private static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "io.NET.ZTR_OS",
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

    /// <summary>
    /// 成功色（成功提示、通过状态）🎉
    /// </summary>
    public Color SuccessColor
    {
        get => _successColor;
        set
        {
            _successColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <summary>
    /// 警告色（警告提示、低危状态）⚠️
    /// </summary>
    public Color WarningColor
    {
        get => _warningColor;
        set
        {
            _warningColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <summary>
    /// 错误色（错误提示、失败状态）🚨
    /// </summary>
    public Color ErrorColor
    {
        get => _errorColor;
        set
        {
            _errorColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <summary>
    /// 仪表盘绿色（指标正向区间）🟢
    /// </summary>
    public Color GaugeGreenColor
    {
        get => _gaugeGreenColor;
        set
        {
            _gaugeGreenColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <summary>
    /// 仪表盘黄色（指标中间区间）🟡
    /// </summary>
    public Color GaugeYellowColor
    {
        get => _gaugeYellowColor;
        set
        {
            _gaugeYellowColor = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <summary>
    /// 仪表盘红色（指标负向区间）🔴
    /// </summary>
    public Color GaugeRedColor
    {
        get => _gaugeRedColor;
        set
        {
            _gaugeRedColor = value;
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
        set
        {
            _animationDuration = Math.Clamp(value, 0, 2000);
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public bool EnableAnimations
    {
        get => _enableAnimations;
        set
        {
            _enableAnimations = value;
            if (!_isBatchUpdating) ApplyTheme();
        }
    }

    /// <inheritdoc />
    public bool IsDarkMode
    {
        get
        {
            // 通过背景色亮度判断：亮度 < 0.5 为深色模式
            var brightness = (_backgroundColor.R * 0.299 +
                            _backgroundColor.G * 0.587 +
                            _backgroundColor.B * 0.114) / 255.0;
            return brightness < 0.5;
        }
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

            // ⭐ ColorOS 视觉包联动：深浅模式切换时同步刷新 DWM 标题栏颜色、云母效果
            if (_windowEffects != null && OperatingSystem.IsWindows())
            {
                try
                {
                    var app = System.Windows.Application.Current;
                    var mainWindow = app?.MainWindow ?? app?.Windows.OfType<Window>().FirstOrDefault();
                    if (mainWindow != null && new System.Windows.Interop.WindowInteropHelper(mainWindow).EnsureHandle() is var hWnd && hWnd != IntPtr.Zero)
                    {
                        // 深浅切了 → 重新 ApplyColorOSVisualPack（DWM 标题栏色 / 云母 / 圆角一次性生效）
                        if (_lastAppliedDarkMode != IsDarkMode || _windowEffects.IsApplied(hWnd) == false)
                        {
                            _windowEffects.ApplyColorOSVisualPack(hWnd, darkTitleBar: IsDarkMode);
                            _lastAppliedDarkMode = IsDarkMode;
                        }
                    }
                }
                catch (Exception fxEx)
                {
                    Log.Warning(fxEx, "[THEME] ApplyTheme → 同步 WindowEffects 失败（忽略，继续渲染）");
                }
            }

            Log.Information("[THEME] 主题已更新: 主色={Primary}, 强调色={Accent}, 圆角={Radius}",
                _primaryColor, _accentColor, _cornerRadius);

            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 主题应用失败");
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

        // 信号灯色族 + 危险色 + 主色半透明（均由对应颜色通道派生）
        var gaugeGreenBrush = new SolidColorBrush(_gaugeGreenColor);
        gaugeGreenBrush.Freeze();
        var gaugeYellowBrush = new SolidColorBrush(_gaugeYellowColor);
        gaugeYellowBrush.Freeze();
        var gaugeRedBrush = new SolidColorBrush(_gaugeRedColor);
        gaugeRedBrush.Freeze();
        var dangerBrush = new SolidColorBrush(_errorColor);
        dangerBrush.Freeze();
        var errorTextBrush = new SolidColorBrush(LightenColor(_errorColor, 0.35));
        errorTextBrush.Freeze();
        // 成功/警告/错误 subtle 色系（背景 0x1A、边框 0x4D 半透明）
        var successSubtleBgBrush = new SolidColorBrush(
            Color.FromArgb(0x1A, _successColor.R, _successColor.G, _successColor.B));
        successSubtleBgBrush.Freeze();
        var successSubtleBorderBrush = new SolidColorBrush(
            Color.FromArgb(0x4D, _successColor.R, _successColor.G, _successColor.B));
        successSubtleBorderBrush.Freeze();
        var warningSubtleBgBrush = new SolidColorBrush(
            Color.FromArgb(0x1A, _warningColor.R, _warningColor.G, _warningColor.B));
        warningSubtleBgBrush.Freeze();
        var warningSubtleBorderBrush = new SolidColorBrush(
            Color.FromArgb(0x4D, _warningColor.R, _warningColor.G, _warningColor.B));
        warningSubtleBorderBrush.Freeze();
        var dangerSubtleBgBrush = new SolidColorBrush(
            Color.FromArgb(0x1A, _errorColor.R, _errorColor.G, _errorColor.B));
        dangerSubtleBgBrush.Freeze();
        var dangerSubtleBorderBrush = new SolidColorBrush(
            Color.FromArgb(0x4D, _errorColor.R, _errorColor.G, _errorColor.B));
        dangerSubtleBorderBrush.Freeze();
        var primarySubtleBgBrush = new SolidColorBrush(
            Color.FromArgb(0x1A, _primaryColor.R, _primaryColor.G, _primaryColor.B));
        primarySubtleBgBrush.Freeze();

        resources["GaugeGreenBrush"] = gaugeGreenBrush;
        resources["GaugeYellowBrush"] = gaugeYellowBrush;
        resources["GaugeRedBrush"] = gaugeRedBrush;
        resources["DangerBrush"] = dangerBrush;
        resources["ErrorTextBrush"] = errorTextBrush;
        resources["PrimarySubtleBackgroundBrush"] = primarySubtleBgBrush;
        resources["SuccessSubtleBackgroundBrush"] = successSubtleBgBrush;
        resources["SuccessSubtleBorderBrush"] = successSubtleBorderBrush;
        resources["WarningSubtleBackgroundBrush"] = warningSubtleBgBrush;
        resources["WarningSubtleBorderBrush"] = warningSubtleBorderBrush;
        resources["DangerSubtleBackgroundBrush"] = dangerSubtleBgBrush;
        resources["DangerSubtleBorderBrush"] = dangerSubtleBorderBrush;

        // 字体 —— 嵌入 Space Grotesk + 简体中文回退
        // Space Grotesk 是纯英文字体，不含中文字形。
        // 如果不指定回退字体，WPF 会走系统字体回退，可能选到繁体字体（如 MingLiU），
        // 导致界面中文显示为繁体字形。这里显式指定 Microsoft YaHei UI 作为中文回退。
        try
        {
            var fontFamily = new FontFamily(
                new Uri("pack://application:,,,/MSMC;component/Resources/Fonts/"),
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

                    if (!string.IsNullOrEmpty(settings.SuccessColor))
                        _successColor = (Color)ColorConverter.ConvertFromString(settings.SuccessColor);

                    if (!string.IsNullOrEmpty(settings.WarningColor))
                        _warningColor = (Color)ColorConverter.ConvertFromString(settings.WarningColor);

                    if (!string.IsNullOrEmpty(settings.ErrorColor))
                        _errorColor = (Color)ColorConverter.ConvertFromString(settings.ErrorColor);

                    if (!string.IsNullOrEmpty(settings.GaugeGreenColor))
                        _gaugeGreenColor = (Color)ColorConverter.ConvertFromString(settings.GaugeGreenColor);

                    if (!string.IsNullOrEmpty(settings.GaugeYellowColor))
                        _gaugeYellowColor = (Color)ColorConverter.ConvertFromString(settings.GaugeYellowColor);

                    if (!string.IsNullOrEmpty(settings.GaugeRedColor))
                        _gaugeRedColor = (Color)ColorConverter.ConvertFromString(settings.GaugeRedColor);

                    _cornerRadius = Math.Clamp(settings.CornerRadius, 0, 24);
                    _animationDuration = Math.Clamp(settings.AnimationDuration, 0, 2000);
                    _enableAnimations = settings.EnableAnimations;
                }
            }

            ApplyTheme();
            Log.Information("[FS] 主题设置已加载");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 加载主题设置失败，使用默认值");
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var bakPath = SettingsFilePath + ".corrupt.bak";
                    File.Copy(SettingsFilePath, bakPath, true);
                    Log.Warning("[PKG] 已备份损坏的主题设置到: {BakPath}", bakPath);
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
                SuccessColor = _successColor.ToString(),
                WarningColor = _warningColor.ToString(),
                ErrorColor = _errorColor.ToString(),
                GaugeGreenColor = _gaugeGreenColor.ToString(),
                GaugeYellowColor = _gaugeYellowColor.ToString(),
                GaugeRedColor = _gaugeRedColor.ToString(),
                CornerRadius = _cornerRadius,
                AnimationDuration = _animationDuration,
                EnableAnimations = _enableAnimations
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // 原子写：先写临时文件再替换，防止写入中途崩溃损坏配置
            var tmpPath = SettingsFilePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(SettingsFilePath))
                File.Replace(tmpPath, SettingsFilePath, null);
            else
                File.Move(tmpPath, SettingsFilePath);

            Log.Information("[SAVE] 主题设置已保存");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 保存主题设置失败");
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
        _successColor = Color.FromRgb(0x4C, 0xAF, 0x50);
        _warningColor = Color.FromRgb(0xFF, 0xC1, 0x07);
        _errorColor = Color.FromRgb(0xE5, 0x39, 0x35);
        _gaugeGreenColor = Color.FromRgb(0x4C, 0xAF, 0x50);
        _gaugeYellowColor = Color.FromRgb(0xFF, 0xC1, 0x07);
        _gaugeRedColor = Color.FromRgb(0xF4, 0x36, 0x4C);
        _cornerRadius = 12;
        _animationDuration = 300;
        _enableAnimations = true;

        ApplyTheme();
        SaveSettings();
        Log.Information("[REFRESH] 主题已重置为默认值");
    }
}
