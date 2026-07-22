// -----------------------------------------------------------------------------
// 文件名: SettingsViewModel.cs
// 命名空间: McServerGuard.ViewModels
// 功能描述: 设置视图模型 —— 基于 CommunityToolkit.Mvvm 源生成器的 MVVM 绑定层，
//           承担主题配色、圆角、动画等 UI 设置的编辑、预览、持久化与预设切换职责
// 依赖组件: CommunityToolkit.Mvvm (ObservableProperty/RelayCommand),
//           System.Windows.Media, Serilog
// 设计模式: MVVM 模式, 命令模式, 策略模式 (主题预设切换), 观察者 (PropertyChanged)
// -----------------------------------------------------------------------------

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace McServerGuard.ViewModels;

/// <summary>
/// 设置视图模型 —— 设置页面的数据上下文
/// </summary>
/// <remarks>
/// 本类作为设置页的 MVVM 绑定层，负责：主题配色（主色、强调色、背景色、卡片色、文字色、边框色）、
/// 视觉参数（圆角半径、动画时长、动画开关）的双向绑定与实时预览；
/// 提供主题预设一键切换、应用与保存、重置默认值、通知测试等命令。
/// 颜色值以 Color 结构存储，同时提供十六进制字符串属性与 SolidColorBrush 画刷属性供不同绑定场景使用。
/// </remarks>
public partial class SettingsViewModel : ObservableObject
{
    /// <summary>主题服务</summary>
    private readonly Services.IThemeService _themeService;
    /// <summary>吐司通知服务</summary>
    private readonly Services.IToastNotificationService _toastService;

    /// <summary>主题主色</summary>
    [ObservableProperty]
    private Color _primaryColor = Color.FromRgb(0x3B, 0x82, 0xF6);

    /// <summary>主题强调色</summary>
    [ObservableProperty]
    private Color _accentColor = Color.FromRgb(0xFB, 0x71, 0x85);

    /// <summary>背景色</summary>
    [ObservableProperty]
    private Color _backgroundColor = Color.FromRgb(0x02, 0x06, 0x17);

    /// <summary>卡片背景色</summary>
    [ObservableProperty]
    private Color _cardColor = Color.FromRgb(0x0F, 0x17, 0x2A);

    /// <summary>文字颜色</summary>
    [ObservableProperty]
    private Color _textColor = Color.FromRgb(0xE2, 0xE8, 0xF0);

    /// <summary>边框颜色</summary>
    [ObservableProperty]
    private Color _borderColor = Color.FromRgb(0x33, 0x41, 0x55);

    /// <summary>主色画刷（供 Border.Background 等画刷属性绑定）</summary>
    public SolidColorBrush PrimaryColorBrush => new SolidColorBrush(PrimaryColor);

    /// <summary>强调色画刷</summary>
    public SolidColorBrush AccentColorBrush => new SolidColorBrush(AccentColor);

    /// <summary>背景色画刷</summary>
    public SolidColorBrush BackgroundColorBrush => new SolidColorBrush(BackgroundColor);

    /// <summary>卡片色画刷</summary>
    public SolidColorBrush CardColorBrush => new SolidColorBrush(CardColor);

    /// <summary>文字色画刷</summary>
    public SolidColorBrush TextColorBrush => new SolidColorBrush(TextColor);

    /// <summary>边框色画刷</summary>
    public SolidColorBrush BorderColorBrush => new SolidColorBrush(BorderColor);

    /// <summary>控件圆角半径（像素）</summary>
    [ObservableProperty]
    private int _cornerRadius = 12;

    /// <summary>过渡动画时长（毫秒）</summary>
    [ObservableProperty]
    private int _animationDuration = 300;

    /// <summary>是否启用过渡动画</summary>
    [ObservableProperty]
    private bool _enableAnimations = true;

    /// <summary>状态栏消息文本</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 主色的十六进制字符串表示（#AARRGGBB 格式）
    /// </summary>
    /// <remarks>设置时自动解析颜色值，解析失败则更新状态消息。</remarks>
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

    /// <summary>
    /// 强调色的十六进制字符串表示
    /// </summary>
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

    /// <summary>
    /// 背景色的十六进制字符串表示
    /// </summary>
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

    /// <summary>
    /// 卡片色的十六进制字符串表示
    /// </summary>
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

    /// <summary>
    /// 文字色的十六进制字符串表示
    /// </summary>
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

    /// <summary>
    /// 边框色的十六进制字符串表示
    /// </summary>
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

    /// <summary>
    /// 初始化设置视图模型的新实例
    /// </summary>
    /// <param name="themeService">主题服务</param>
    /// <param name="toastService">吐司通知服务</param>
    /// <remarks>构造时从主题服务加载已持久化的设置。</remarks>
    public SettingsViewModel(Services.IThemeService themeService, Services.IToastNotificationService toastService)
    {
        _themeService = themeService;
        _toastService = toastService;
        LoadSettings();
    }

    /// <summary>
    /// 从主题服务加载设置到 ViewModel 属性
    /// </summary>
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

    /// <summary>
    /// 设置主色命令
    /// </summary>
    /// <param name="hex">十六进制颜色字符串</param>
    /// <remarks>触发条件：用户选择预设颜色或输入十六进制值。副作用：更新 <see cref="PrimaryColor"/>。</remarks>
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

    /// <summary>
    /// 设置强调色命令
    /// </summary>
    /// <param name="hex">十六进制颜色字符串</param>
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

    /// <summary>
    /// 设置背景色命令
    /// </summary>
    /// <param name="hex">十六进制颜色字符串</param>
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

    /// <summary>
    /// 设置卡片色命令
    /// </summary>
    /// <param name="hex">十六进制颜色字符串</param>
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

    /// <summary>
    /// 设置文字色命令
    /// </summary>
    /// <param name="hex">十六进制颜色字符串</param>
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

    /// <summary>
    /// 设置边框色命令
    /// </summary>
    /// <param name="hex">十六进制颜色字符串</param>
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

    /// <summary>
    /// 应用主题命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击应用按钮。
    /// 副作用：将当前 ViewModel 属性同步到 <see cref="Services.IThemeService"/>，使主题实时生效。
    /// 使用批量更新模式避免多次全量重绘。
    /// </remarks>
    [RelayCommand]
    private void ApplyTheme()
    {
        _themeService.BeginBatchUpdate();
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
        finally
        {
            _themeService.EndBatchUpdate();
        }
    }

    /// <summary>
    /// 保存设置命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击保存按钮。
    /// 副作用：调用 <see cref="Services.IThemeService.SaveSettings"/> 持久化当前主题配置，
    /// 并通过吐司通知反馈结果。
    /// </remarks>
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

    /// <summary>
    /// 应用主题预设命令
    /// </summary>
    /// <param name="preset">预设名称（SkyBlue / OceanBlue / BlueOrange / TealPink / RedYellow）</param>
    /// <remarks>
    /// 触发条件：用户选择预设主题。
    /// 副作用：一次性更新所有颜色属性为预设值，触发实时预览。
    /// </remarks>
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

    /// <summary>
    /// 重置为默认设置命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击重置按钮。
    /// 副作用：调用 <see cref="Services.IThemeService.ResetToDefault"/> 恢复默认配置，
    /// 同步到 ViewModel 所有属性并触发通知。
    /// </remarks>
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

    /// <summary>
    /// 测试通知命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击测试通知按钮。
    /// 副作用：调用 <see cref="Services.IToastNotificationService.ShowSuccess"/> 弹出测试通知。
    /// </remarks>
    [RelayCommand]
    private void TestNotification()
    {
        _toastService.ShowSuccess("测试通知", "这是一条测试通知，通知功能正常工作！");
        StatusMessage = "测试通知已发送";
        Log.Information("🔔 测试通知已发送");
    }

    /// <summary>
    /// 应用并保存设置命令
    /// </summary>
    /// <remarks>依次调用 <see cref="ApplyTheme"/> 与 <see cref="SaveSettings"/> 的复合命令。</remarks>
    [RelayCommand]
    private void ApplyAndSave()
    {
        ApplyTheme();
        SaveSettings();
    }

    /// <summary>
    /// PrimaryColor 变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的主色值</param>
    /// <remarks>触发派生属性 <see cref="PrimaryColorHex"/> 与 <see cref="PrimaryColorBrush"/> 的变更通知，实现实时预览。</remarks>
    partial void OnPrimaryColorChanged(Color value)
    {
        OnPropertyChanged(nameof(PrimaryColorHex));
        OnPropertyChanged(nameof(PrimaryColorBrush));
    }

    /// <summary>
    /// AccentColor 变更回调
    /// </summary>
    /// <param name="value">新的强调色值</param>
    partial void OnAccentColorChanged(Color value)
    {
        OnPropertyChanged(nameof(AccentColorHex));
        OnPropertyChanged(nameof(AccentColorBrush));
    }

    /// <summary>
    /// BackgroundColor 变更回调
    /// </summary>
    /// <param name="value">新的背景色值</param>
    partial void OnBackgroundColorChanged(Color value)
    {
        OnPropertyChanged(nameof(BackgroundColorHex));
        OnPropertyChanged(nameof(BackgroundColorBrush));
    }

    /// <summary>
    /// CardColor 变更回调
    /// </summary>
    /// <param name="value">新的卡片色值</param>
    partial void OnCardColorChanged(Color value)
    {
        OnPropertyChanged(nameof(CardColorHex));
        OnPropertyChanged(nameof(CardColorBrush));
    }

    /// <summary>
    /// TextColor 变更回调
    /// </summary>
    /// <param name="value">新的文字色值</param>
    partial void OnTextColorChanged(Color value)
    {
        OnPropertyChanged(nameof(TextColorHex));
        OnPropertyChanged(nameof(TextColorBrush));
    }

    /// <summary>
    /// BorderColor 变更回调
    /// </summary>
    /// <param name="value">新的边框色值</param>
    partial void OnBorderColorChanged(Color value)
    {
        OnPropertyChanged(nameof(BorderColorHex));
        OnPropertyChanged(nameof(BorderColorBrush));
    }

    /// <summary>
    /// CornerRadius 变更回调
    /// </summary>
    /// <param name="value">新的圆角半径值</param>
    partial void OnCornerRadiusChanged(int value)
    {
        // 实时预览由 ViewModel 自身属性支持，不立即写入 ThemeService
    }
}
