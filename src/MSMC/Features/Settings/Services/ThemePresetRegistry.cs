// -----------------------------------------------------------------------------
// 文件名: ThemePresetRegistry.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: 13 套品牌主题预设注册表（README L3 营销名 ↔ 颜色色阶映射）
// 依赖组件: System.Windows.Media (Color)
// 设计模式: 注册表模式（只读字典）、策略模式（ApplyPreset 应用到 IThemeService）
// -----------------------------------------------------------------------------
using System.Windows.Media;

namespace io.NET.ZTR_OS.Features.Settings.Services;

/// <summary>
/// 主题预设记录
/// </summary>
/// <param name="Key">英文标识（与前端 TypeScript ThemePreset 枚举对齐）</param>
/// <param name="Label">中文展示名（设置页面预设卡片标题）</param>
/// <param name="PrimaryColorHex">主色 HEX（#RRGGBB 或 #AARRGGBB）</param>
/// <param name="AccentColorHex">强调色 HEX</param>
/// <param name="BackgroundColorHex">背景色 HEX（可选，null 时沿用当前背景）</param>
/// <param name="CardColorHex">卡片背景色 HEX（可选）</param>
public record ThemePreset(
    string Key,
    string Label,
    string PrimaryColorHex,
    string AccentColorHex,
    string? BackgroundColorHex = null,
    string? CardColorHex = null)
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
            // 兜底：README 里写的颜色都合法；如果被人改乱就用默认蓝
            return Color.FromRgb(0x3B, 0x82, 0xF6);
        }
    }
}

/// <summary>
/// 13 套品牌主题预设注册表
/// </summary>
/// <remarks>
/// README L3 声明的 13 套颜色系统：
/// 5 套沿用旧名称（SkyBlue / BlueOrange / TealPink / RedYellow / OceanBlue）
/// + 8 套新增 ColorOS 品牌系统：ColorOSBlue / AquarioCyan / AuroraPurple /
///   SunsetOrange / MintGreen / SakuraPink / MidnightGold / ArcticGray
/// </remarks>
public static class ThemePresetRegistry
{
    /// <summary>
    /// 所有 13 套预设（按 README 列出的顺序，方便前端 for 循环渲染卡片顺序与文档一致）
    /// </summary>
    private static readonly List<ThemePreset> _all = new()
    {
        // ── 原有 5 套（保持向后兼容，TypeScript 旧 ThemePreset 类型已声明）──
        new(
            Key: "SkyBlue",
            Label: "苍穹蓝",
            PrimaryColorHex: "#3B82F6",
            AccentColorHex:  "#FB7185",
            BackgroundColorHex: "#020617",
            CardColorHex:      "#0F172A"),

        new(
            Key: "BlueOrange",
            Label: "科技蓝",
            PrimaryColorHex: "#1565C0",
            AccentColorHex:  "#FF9800",
            BackgroundColorHex: "#0A0F1E",
            CardColorHex:      "#172033"),

        new(
            Key: "TealPink",
            Label: "清新绿",
            PrimaryColorHex: "#00897B",
            AccentColorHex:  "#E91E63",
            BackgroundColorHex: "#0B1F1A",
            CardColorHex:      "#122B25"),

        new(
            Key: "RedYellow",
            Label: "火焰红",
            PrimaryColorHex: "#C62828",
            AccentColorHex:  "#FFD600",
            BackgroundColorHex: "#1A0A0A",
            CardColorHex:      "#2B1616"),

        new(
            Key: "OceanBlue",
            Label: "海洋蓝",
            PrimaryColorHex: "#0097A7",
            AccentColorHex:  "#FFD740",
            BackgroundColorHex: "#04181C",
            CardColorHex:      "#0E2A30"),

        // ── README L3 品牌系统新增 8 套 ──
        new(
            Key: "ColorOSBlue",
            Label: "ColorOS 蓝",
            PrimaryColorHex: "#1677FF",  // ColorOS 官方蓝
            AccentColorHex:  "#FF6B81",  // ColorOS 樱花粉强调色
            BackgroundColorHex: "#050B1A",
            CardColorHex:      "#0E1C35"),

        new(
            Key: "AquarioCyan",
            Label: "Aquario 蓝绿",
            PrimaryColorHex: "#06B6D4",  // Cyan-500
            AccentColorHex:  "#F472B6",  // Pink-400
            BackgroundColorHex: "#041218",
            CardColorHex:      "#0C2430"),

        new(
            Key: "AuroraPurple",
            Label: "极光紫",
            PrimaryColorHex: "#8B5CF6",  // Violet-500
            AccentColorHex:  "#22D3EE",  // Cyan-400
            BackgroundColorHex: "#0C0820",
            CardColorHex:      "#1E1440"),

        new(
            Key: "SunsetOrange",
            Label: "日落橙",
            PrimaryColorHex: "#F97316",  // Orange-500
            AccentColorHex:  "#FACC15",  // Amber-400
            BackgroundColorHex: "#1A0D04",
            CardColorHex:      "#301E0E"),

        new(
            Key: "MintGreen",
            Label: "薄荷青",
            PrimaryColorHex: "#10B981",  // Emerald-500
            AccentColorHex:  "#A78BFA",  // Violet-400
            BackgroundColorHex: "#041813",
            CardColorHex:      "#0C2A22"),

        new(
            Key: "SakuraPink",
            Label: "樱花粉",
            PrimaryColorHex: "#EC4899",  // Pink-500
            AccentColorHex:  "#60A5FA",  // Blue-400
            BackgroundColorHex: "#1A0A14",
            CardColorHex:      "#2E1726"),

        new(
            Key: "MidnightGold",
            Label: "暗夜金",
            PrimaryColorHex: "#D4A017",  // 深金
            AccentColorHex:  "#F8FAFC",  // 近白
            BackgroundColorHex: "#0A0A05",
            CardColorHex:      "#1C1A10"),

        new(
            Key: "ArcticGray",
            Label: "北极灰",
            PrimaryColorHex: "#64748B",  // Slate-500
            AccentColorHex:  "#38BDF8",  // Sky-400
            BackgroundColorHex: "#0B1220",
            CardColorHex:      "#1A2234"),
    };

    /// <summary>
    /// 获取全部 13 套预设（返回副本，防止外部修改内部列表）
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
            service.PrimaryColor = preset.PrimaryColor;
            service.AccentColor = preset.AccentColor;
            if (!string.IsNullOrEmpty(preset.BackgroundColorHex))
                service.BackgroundColor = ParseHexSafe(preset.BackgroundColorHex);
            if (!string.IsNullOrEmpty(preset.CardColorHex))
                service.CardColor = ParseHexSafe(preset.CardColorHex);
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
