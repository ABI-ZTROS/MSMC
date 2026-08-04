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
            Assert.Matches("^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", p.PrimaryColorHex,
                $"预设 {p.Key} 主色不是有效的 HEX: {p.PrimaryColorHex}");
            Assert.Matches("^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", p.AccentColorHex,
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
        var preset = ThemePresetRegistry.GetAllPresets()
            .First(p => !p.PrimaryColorHex.EndsWith(originalPrimary.ToString().Trim('#'), StringComparison.OrdinalIgnoreCase));

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
}
