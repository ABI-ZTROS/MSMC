using System.Reflection;
using io.NET.ZTR_OS.Features.Settings.Services;
using Xunit;

namespace io.NET.ZTR_OS.Tests.Services;

/// <summary>🧪 TDD RED: ThemeService 13 套品牌预设测试 —— README 写了13套，现在只有5套硬编码</summary>
public class ThemePresetsTests
{
    [Fact]
    public void ThemeService_GetAllPresets_ReturnsAtLeast13DistinctPresets()
    {
        // 🟥 RED: ThemeService 中目前没有预设列表
        var svc = new ThemeService();

        // 通过反射或静态公开方法获取所有预设 —— 若方法不存在，本测试会编译失败（RED）
        var presets = ThemePresetRegistry.GetAllPresets();

        Assert.True(presets.Count >= 13,
            $"README 声明 13 套品牌预设，目前只有 {presets.Count} 套。缺少: {MissingPresetNames(presets)}");
    }

    [Fact]
    public void EachPreset_HasDistinctPrimaryAndAccentColors()
    {
        var presets = ThemePresetRegistry.GetAllPresets();

        var primaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in presets)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Key), "预设 Key 不能为空");
            Assert.False(string.IsNullOrWhiteSpace(p.Label), $"预设 {p.Key} 的中文名缺失");
            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(p.PrimaryColorHex, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 主色不是有效的 HEX: {p.PrimaryColorHex}");
            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(p.AccentColorHex, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 强调色不是有效的 HEX: {p.AccentColorHex}");

            primaries.Add(p.PrimaryColorHex);
            accents.Add(p.AccentColorHex);
        }

        // 13 套预设，去重后主色至少应有 >10 种（不能都是同一个蓝）
        Assert.True(primaries.Count >= 10,
            $"13 套预设主色去重后只有 {primaries.Count} 种，重复过多");
    }

    [Fact]
    public void ApplyPreset_ActuallyChangesThemeColors()
    {
        var svc = new ThemeService();
        var originalPrimary = svc.PrimaryColor;
        var originalHex = $"#{originalPrimary.R:X2}{originalPrimary.G:X2}{originalPrimary.B:X2}";

        var preset = ThemePresetRegistry.GetAllPresets()
            .First(p => !string.Equals(p.PrimaryColorHex, originalHex, StringComparison.OrdinalIgnoreCase));

        ThemePresetRegistry.ApplyPreset(svc, preset.Key);

        Assert.NotEqual(originalPrimary, svc.PrimaryColor);
    }

    [Fact]
    public void PresetNames_MatchReadmeMarketingNames()
    {
        var actual = ThemePresetRegistry.GetAllPresets()
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // README 里提到的:
        // ColorOS 蓝 / Aquario 蓝绿 / 极光紫 / 日落橙 / 薄荷青 / 苍穹蓝 / 科技蓝 / 清新绿 / 火焰红 / 海洋蓝
        // 再补 3 种: 樱花粉 / 暗夜金 / 北极灰 = 13 套
        var expected = new[]
        {
            "SkyBlue",        // 苍穹蓝 (原有)
            "BlueOrange",     // 科技蓝 (原有)
            "TealPink",       // 清新绿 (原有)
            "RedYellow",      // 火焰红 (原有)
            "OceanBlue",      // 海洋蓝 (原有)
            "ColorOSBlue",    // ColorOS 蓝 (README L3 品牌名)
            "AquarioCyan",    // Aquario 蓝绿
            "AuroraPurple",   // 极光紫
            "SunsetOrange",   // 日落橙
            "MintGreen",      // 薄荷青
            "SakuraPink",     // 樱花粉
            "MidnightGold",   // 暗夜金
            "ArcticGray",     // 北极灰
        };

        var missing = expected.Where(k => !actual.Contains(k)).ToList();
        Assert.Empty(missing);
    }

    private static string MissingPresetNames(List<ThemePreset> presets)
    {
        var have = presets.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = new[] { "SkyBlue","BlueOrange","TealPink","RedYellow","OceanBlue",
            "ColorOSBlue","AquarioCyan","AuroraPurple","SunsetOrange","MintGreen",
            "SakuraPink","MidnightGold","ArcticGray" };
        return string.Join(", ", expected.Where(k => !have.Contains(k)));
    }

    [Fact]
    public void EveryPreset_WhenApplied_SetsAllSixColors()
    {
        var presets = ThemePresetRegistry.GetAllPresets();

        foreach (var p in presets)
        {
            // 🟢 每套预设独立 new 一个 ThemeService，避免状态串扰
            var svc = new ThemeService();
            Assert.True(ThemePresetRegistry.ApplyPreset(svc, p.Key),
                $"预设 {p.Key} 应用失败");

            // 基础四通道（主色/强调色/背景/卡片）按预设落地
            // 注：ThemePreset 只暴露 PrimaryColor/AccentColor 两个 Color 派生属性，
            // 背景/卡片为 HEX 字符串，需经 ColorConverter 解析后与 svc 比对
            Assert.Equal(p.PrimaryColor, svc.PrimaryColor);
            Assert.Equal(p.AccentColor, svc.AccentColor);
            Assert.Equal(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(p.BackgroundColorHex!)!,
                svc.BackgroundColor);
            Assert.Equal(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(p.CardColorHex!)!,
                svc.CardColor);

            // 文字色与边框色必须非空且解析后与预设完全等价
            Assert.False(string.IsNullOrWhiteSpace(p.TextColorHex), $"预设 {p.Key} 文字色缺失");
            Assert.False(string.IsNullOrWhiteSpace(p.BorderColorHex), $"预设 {p.Key} 边框色缺失");
            Assert.Equal(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(p.TextColorHex)!,
                svc.TextColor);
            Assert.Equal(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(p.BorderColorHex)!,
                svc.BorderColor);
        }
    }

    [Fact]
    public void AllPresets_HaveCompleteValidSixColorScheme()
    {
        var presets = ThemePresetRegistry.GetAllPresets();

        foreach (var p in presets)
        {
            // 六通道中的文字色/边框色必须已定义且为合法 HEX（与现有测试正则风格一致）
            Assert.False(string.IsNullOrEmpty(p.TextColorHex), $"预设 {p.Key} 文字色未定义");
            Assert.False(string.IsNullOrEmpty(p.BorderColorHex), $"预设 {p.Key} 边框色未定义");
            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(p.TextColorHex, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 文字色不是有效 HEX: {p.TextColorHex}");
            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(p.BorderColorHex, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 边框色不是有效 HEX: {p.BorderColorHex}");
        }
    }

    [Fact]
    public void ApplyPreset_UnknownKey_ReturnsFalseAndLeavesColorsUnchanged()
    {
        var svc = new ThemeService();

        // 记录应用前的六色默认值
        var defaultPrimary = svc.PrimaryColor;
        var defaultAccent = svc.AccentColor;
        var defaultBackground = svc.BackgroundColor;
        var defaultCard = svc.CardColor;
        var defaultText = svc.TextColor;
        var defaultBorder = svc.BorderColor;

        // 未知 key 必须返回 false 且不抛异常
        Assert.False(ThemePresetRegistry.ApplyPreset(svc, "NonExistentPresetKey"));

        // 六色通道必须保持不变
        Assert.Equal(defaultPrimary, svc.PrimaryColor);
        Assert.Equal(defaultAccent, svc.AccentColor);
        Assert.Equal(defaultBackground, svc.BackgroundColor);
        Assert.Equal(defaultCard, svc.CardColor);
        Assert.Equal(defaultText, svc.TextColor);
        Assert.Equal(defaultBorder, svc.BorderColor);
    }
}
