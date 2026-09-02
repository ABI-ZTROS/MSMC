// -----------------------------------------------------------------------------
// 文件名: ThemePresetRegistry.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: 7 套品牌/参考主题预设注册表（参考物主色 ↔ 完整 12 色阶映射）
// 依赖组件: System.Windows.Media (Color)
// 设计模式: 注册表模式（只读字典）、策略模式（ApplyPreset 应用到 IThemeService）
// -----------------------------------------------------------------------------
using System.Windows.Media;

namespace io.NET.ZTR_OS.Features.Settings.Services;

/// <summary>
/// 主题预设记录（完整 12 色：6 主题色 + 6 语义/仪表色）
/// </summary>
/// <param name="Key">英文标识（与前端 TypeScript ThemePreset 枚举对齐）</param>
/// <param name="Label">中文展示名（设置页面预设卡片标题）</param>
/// <param name="PrimaryColorHex">主色 HEX（#RRGGBB 或 #AARRGGBB）</param>
/// <param name="AccentColorHex">强调色 HEX</param>
/// <param name="BackgroundColorHex">背景色 HEX（可选，null 时沿用当前背景）</param>
/// <param name="CardColorHex">卡片背景色 HEX（可选）</param>
/// <param name="TextColorHex">文字色 HEX（可选，null 时不改文字色）</param>
/// <param name="BorderColorHex">边框色 HEX（可选，null 时不改边框色）</param>
/// <param name="SuccessColorHex">成功色 HEX（可选）</param>
/// <param name="WarningColorHex">警告色 HEX（可选）</param>
/// <param name="ErrorColorHex">错误色 HEX（可选）</param>
/// <param name="GaugeGreenColorHex">仪表盘绿色 HEX（可选）</param>
/// <param name="GaugeYellowColorHex">仪表盘黄色 HEX（可选）</param>
/// <param name="GaugeRedColorHex">仪表盘红色 HEX（可选）</param>
public record ThemePreset(
    string Key,
    string Label,
    string PrimaryColorHex,
    string AccentColorHex,
    string? BackgroundColorHex = null,
    string? CardColorHex = null,
    string? TextColorHex = null,
    string? BorderColorHex = null,
    string? SuccessColorHex = null,
    string? WarningColorHex = null,
    string? ErrorColorHex = null,
    string? GaugeGreenColorHex = null,
    string? GaugeYellowColorHex = null,
    string? GaugeRedColorHex = null)
{
    /// <summary>
    /// 主色的 System.Windows.Media.Color
    /// </summary>
    public Color PrimaryColor => ParseHex(PrimaryColorHex);

    /// <summary>
    /// 强调色的 System.Windows.Media.Color
    /// </summary>
    public Color AccentColor => ParseHex(AccentColorHex);

    private static Color ParseHex(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            // 兜底：颜色都合法；如果被人改乱就用默认蓝
            return Color.FromRgb(0x3B, 0x82, 0xF6);
        }
    }
}

/// <summary>
/// 7 套主题预设注册表
/// </summary>
/// <remarks>
/// 7 套品牌/参考主题：
/// ColorOS 蓝 / 芙宁娜蓝（原神 Furina Royal Blue）/ 火龙果 / 青苹果 /
/// 血红（酒红）/ 日落黄（橙黄）/ 普瑞赛斯紫（明日方舟 Prece 紫瞳）
/// 每套预设覆盖全部 12 个颜色通道（6 主题 + 6 语义/仪表）
/// </remarks>
public static class ThemePresetRegistry
{
    /// <summary>
    /// 所有 7 套预设
    /// </summary>
    private static readonly List<ThemePreset> _all = new()
    {
        // ── ColorOS 蓝：OPPO 品牌蓝（Find X8 极光蓝配色），冷调蓝绿 ──
        new(
            Key: "ColorOSBlue",
            Label: "ColorOS 蓝",
            PrimaryColorHex: "#0066FF",
            AccentColorHex:  "#FF6B81",
            BackgroundColorHex: "#030818",
            CardColorHex:      "#0A1A32",
            TextColorHex:      "#E6F0FF",
            BorderColorHex:    "#1B3D6E",
            SuccessColorHex:   "#10B981",
            WarningColorHex:   "#F59E0B",
            ErrorColorHex:     "#EF4444",
            GaugeGreenColorHex:  "#22C55E",
            GaugeYellowColorHex: "#EAB308",
            GaugeRedColorHex:    "#F43F5E"),

        // ── 芙宁娜蓝：原神 Furina Royal Blue + 金色强调 ──
        new(
            Key: "FurinaBlue",
            Label: "芙宁娜蓝",
            PrimaryColorHex: "#1E3A8A",
            AccentColorHex:  "#D4A017",
            BackgroundColorHex: "#050717",
            CardColorHex:      "#0B1030",
            TextColorHex:      "#E8ECFB",
            BorderColorHex:    "#253B78",
            SuccessColorHex:   "#10B981",
            WarningColorHex:   "#F59E0B",
            ErrorColorHex:     "#EF4444",
            GaugeGreenColorHex:  "#22C55E",
            GaugeYellowColorHex: "#EAB308",
            GaugeRedColorHex:    "#F43F5E"),

        // ── 火龙果：深洋红 + 金色强调 ──
        new(
            Key: "Dragonfruit",
            Label: "火龙果",
            PrimaryColorHex: "#C71585",
            AccentColorHex:  "#FFD700",
            BackgroundColorHex: "#18060F",
            CardColorHex:      "#2B101F",
            TextColorHex:      "#FBE9F2",
            BorderColorHex:    "#5C2342",
            SuccessColorHex:   "#22C55E",
            WarningColorHex:   "#FBBF24",
            ErrorColorHex:     "#F43F5E",
            GaugeGreenColorHex:  "#10B981",
            GaugeYellowColorHex: "#F59E0B",
            GaugeRedColorHex:    "#DC143C"),

        // ── 青苹果：黄绿 + 天蓝强调 ──
        new(
            Key: "GreenApple",
            Label: "青苹果",
            PrimaryColorHex: "#9ACD32",
            AccentColorHex:  "#0EA5E9",
            BackgroundColorHex: "#0A1208",
            CardColorHex:      "#16220F",
            TextColorHex:      "#E8F5D8",
            BorderColorHex:    "#3E5C22",
            SuccessColorHex:   "#10B981",
            WarningColorHex:   "#FBBF24",
            ErrorColorHex:     "#EF4444",
            GaugeGreenColorHex:  "#22C55E",
            GaugeYellowColorHex: "#EAB308",
            GaugeRedColorHex:    "#F43F5E"),

        // ── 血红：酒红 + 金色强调 ──
        new(
            Key: "BloodRed",
            Label: "血红",
            PrimaryColorHex: "#722F37",
            AccentColorHex:  "#D4A017",
            BackgroundColorHex: "#0F0406",
            CardColorHex:      "#200B10",
            TextColorHex:      "#F5E8E0",
            BorderColorHex:    "#4A2A2F",
            SuccessColorHex:   "#10B981",
            WarningColorHex:   "#FBBF24",
            ErrorColorHex:     "#DC143C",
            GaugeGreenColorHex:  "#22C55E",
            GaugeYellowColorHex: "#F59E0B",
            GaugeRedColorHex:    "#E53935"),

        // ── 日落黄：橙黄 + 淡紫强调 ──
        new(
            Key: "SunsetYellow",
            Label: "日落黄",
            PrimaryColorHex: "#FF8C00",
            AccentColorHex:  "#DA70D6",
            BackgroundColorHex: "#1A0804",
            CardColorHex:      "#32180C",
            TextColorHex:      "#FDF2E0",
            BorderColorHex:    "#5E3A20",
            SuccessColorHex:   "#22C55E",
            WarningColorHex:   "#F59E0B",
            ErrorColorHex:     "#EF4444",
            GaugeGreenColorHex:  "#10B981",
            GaugeYellowColorHex: "#EAB308",
            GaugeRedColorHex:    "#DC143C"),

        // ── 普瑞赛斯紫：紫罗兰 + 青色强调（明日方舟 Prece 紫瞳） ──
        new(
            Key: "PrecePurple",
            Label: "普瑞赛斯紫",
            PrimaryColorHex: "#8B5CF6",
            AccentColorHex:  "#22D3EE",
            BackgroundColorHex: "#0C0620",
            CardColorHex:      "#1E1238",
            TextColorHex:      "#F0ECFB",
            BorderColorHex:    "#3D2A6E",
            SuccessColorHex:   "#10B981",
            WarningColorHex:   "#F59E0B",
            ErrorColorHex:     "#F43F5E",
            GaugeGreenColorHex:  "#22C55E",
            GaugeYellowColorHex: "#EAB308",
            GaugeRedColorHex:    "#EF4444"),
    };

    /// <summary>
    /// 获取全部 7 套预设（返回副本，防止外部修改内部列表）
    /// </summary>
    public static List<ThemePreset> GetAllPresets() => new(_all);

    /// <summary>
    /// 通过 Key 查找预设（不区分大小写）
    /// </summary>
    public static ThemePreset? GetPresetByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _all.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 把指定预设应用到目标 IThemeService（调用后会自动触发 ThemeChanged 事件）
    /// </summary>
    /// <param name="service">要应用的主题服务</param>
    /// <param name="presetKey">预设 Key</param>
    /// <returns>是否成功应用</returns>
    public static bool ApplyPreset(IThemeService service, string presetKey)
    {
        if (service == null) return false;
        var preset = GetPresetByKey(presetKey);
        if (preset == null) return false;

        // BeginBatchUpdate → 一次设置多个颜色 → EndBatchUpdate 只 Apply 一次
        service.BeginBatchUpdate();
        try
        {
            // ── 6 个主题色 ──
            service.PrimaryColor = preset.PrimaryColor;
            service.AccentColor = preset.AccentColor;
            if (!string.IsNullOrEmpty(preset.BackgroundColorHex))
                service.BackgroundColor = ParseHexSafe(preset.BackgroundColorHex);
            if (!string.IsNullOrEmpty(preset.CardColorHex))
                service.CardColor = ParseHexSafe(preset.CardColorHex);
            if (!string.IsNullOrEmpty(preset.TextColorHex))
                service.TextColor = ParseHexSafe(preset.TextColorHex);
            if (!string.IsNullOrEmpty(preset.BorderColorHex))
                service.BorderColor = ParseHexSafe(preset.BorderColorHex);

            // ── 6 个语义/仪表色 ──
            if (!string.IsNullOrEmpty(preset.SuccessColorHex))
                service.SuccessColor = ParseHexSafe(preset.SuccessColorHex);
            if (!string.IsNullOrEmpty(preset.WarningColorHex))
                service.WarningColor = ParseHexSafe(preset.WarningColorHex);
            if (!string.IsNullOrEmpty(preset.ErrorColorHex))
                service.ErrorColor = ParseHexSafe(preset.ErrorColorHex);
            if (!string.IsNullOrEmpty(preset.GaugeGreenColorHex))
                service.GaugeGreenColor = ParseHexSafe(preset.GaugeGreenColorHex);
            if (!string.IsNullOrEmpty(preset.GaugeYellowColorHex))
                service.GaugeYellowColor = ParseHexSafe(preset.GaugeYellowColorHex);
            if (!string.IsNullOrEmpty(preset.GaugeRedColorHex))
                service.GaugeRedColor = ParseHexSafe(preset.GaugeRedColorHex);
        }
        finally
        {
            service.EndBatchUpdate();
        }
        return true;
    }

    private static Color ParseHexSafe(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(0x02, 0x06, 0x17); // 兜底默认背景
        }
    }
}
