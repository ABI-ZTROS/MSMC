using System.Text.RegularExpressions;
using System.Windows.Media;
using io.NET.ZTR_OS.Features.Settings.Services;
using Xunit;

namespace io.NET.ZTR_OS.Tests.Services;

/// <summary>🧪 ThemePresetRegistry 7 套新主题 + 完整 12 色覆盖测试</summary>
public class ThemePresetsTests
{
    [Fact]
    public void ThemePresetRegistry_ReturnsExactly7Presets()
    {
        var presets = ThemePresetRegistry.GetAllPresets();
        Assert.Equal(7, presets.Count);
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
            Assert.True(Regex.IsMatch(p.PrimaryColorHex, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 主色不是有效的 HEX: {p.PrimaryColorHex}");
            Assert.True(Regex.IsMatch(p.AccentColorHex, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 强调色不是有效的 HEX: {p.AccentColorHex}");

            primaries.Add(p.PrimaryColorHex);
            accents.Add(p.AccentColorHex);
        }

        // 7 套预设，去重后主色至少应有 >= 5 种
        Assert.True(primaries.Count >= 5,
            $"7 套预设主色去重后只有 {primaries.Count} 种，重复过多");
    }

    [Fact]
    public void PresetNames_MatchNewSevenKeys()
    {
        var actual = ThemePresetRegistry.GetAllPresets()
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expected = new[]
        {
            "ColorOSBlue",    // ColorOS 蓝
            "FurinaBlue",     // 芙宁娜蓝
            "Dragonfruit",    // 火龙果
            "GreenApple",     // 青苹果
            "BloodRed",       // 血红
            "SunsetYellow",   // 日落黄
            "PrecePurple",    // 普瑞赛斯紫
        };

        var missing = expected.Where(k => !actual.Contains(k)).ToList();
        var extra = actual.Where(k => !expected.Contains(k)).ToList();
        Assert.Empty(missing);
        Assert.Empty(extra);
    }

    [Fact]
    public void EveryPreset_HasCompleteTwelveColorScheme()
    {
        var presets = ThemePresetRegistry.GetAllPresets();
        Assert.Equal(7, presets.Count);

        foreach (var p in presets)
        {
            // 6 个主题色字段必须全部非 null 且为合法 HEX
            Assert.NotNull(p.BackgroundColorHex);
            Assert.NotNull(p.CardColorHex);
            Assert.NotNull(p.TextColorHex);
            Assert.NotNull(p.BorderColorHex);
            Assert.True(Regex.IsMatch(p.BackgroundColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 背景色不是有效 HEX: {p.BackgroundColorHex}");
            Assert.True(Regex.IsMatch(p.CardColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 卡片色不是有效 HEX: {p.CardColorHex}");
            Assert.True(Regex.IsMatch(p.TextColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 文字色不是有效 HEX: {p.TextColorHex}");
            Assert.True(Regex.IsMatch(p.BorderColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 边框色不是有效 HEX: {p.BorderColorHex}");

            // 6 个语义/仪表色字段必须全部非 null 且为合法 HEX
            Assert.NotNull(p.SuccessColorHex);
            Assert.NotNull(p.WarningColorHex);
            Assert.NotNull(p.ErrorColorHex);
            Assert.NotNull(p.GaugeGreenColorHex);
            Assert.NotNull(p.GaugeYellowColorHex);
            Assert.NotNull(p.GaugeRedColorHex);
            Assert.True(Regex.IsMatch(p.SuccessColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 成功色不是有效 HEX: {p.SuccessColorHex}");
            Assert.True(Regex.IsMatch(p.WarningColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 警告色不是有效 HEX: {p.WarningColorHex}");
            Assert.True(Regex.IsMatch(p.ErrorColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 错误色不是有效 HEX: {p.ErrorColorHex}");
            Assert.True(Regex.IsMatch(p.GaugeGreenColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 仪表盘绿不是有效 HEX: {p.GaugeGreenColorHex}");
            Assert.True(Regex.IsMatch(p.GaugeYellowColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 仪表盘黄不是有效 HEX: {p.GaugeYellowColorHex}");
            Assert.True(Regex.IsMatch(p.GaugeRedColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"),
                $"预设 {p.Key} 仪表盘红不是有效 HEX: {p.GaugeRedColorHex}");
        }
    }

    [Fact]
    public void EveryPreset_WhenApplied_SetsAllTwelveColors()
    {
        var presets = ThemePresetRegistry.GetAllPresets();

        foreach (var p in presets)
        {
            var svc = new ThemeService();
            Assert.True(ThemePresetRegistry.ApplyPreset(svc, p.Key),
                $"预设 {p.Key} 应用失败");

            // ── 6 个主题色 ──
            Assert.Equal(p.PrimaryColor, svc.PrimaryColor);
            Assert.Equal(p.AccentColor, svc.AccentColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.BackgroundColorHex!)!,
                svc.BackgroundColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.CardColorHex!)!,
                svc.CardColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.TextColorHex!)!,
                svc.TextColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.BorderColorHex!)!,
                svc.BorderColor);

            // ── 6 个语义/仪表色 ──
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.SuccessColorHex!)!,
                svc.SuccessColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.WarningColorHex!)!,
                svc.WarningColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.ErrorColorHex!)!,
                svc.ErrorColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.GaugeGreenColorHex!)!,
                svc.GaugeGreenColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.GaugeYellowColorHex!)!,
                svc.GaugeYellowColor);
            Assert.Equal(
                (Color)ColorConverter.ConvertFromString(p.GaugeRedColorHex!)!,
                svc.GaugeRedColor);
        }
    }

    [Fact]
    public void ApplyPreset_UnknownKey_ReturnsFalseAndLeavesColorsUnchanged()
    {
        var svc = new ThemeService();

        var defaultPrimary = svc.PrimaryColor;
        var defaultAccent = svc.AccentColor;
        var defaultBackground = svc.BackgroundColor;
        var defaultCard = svc.CardColor;
        var defaultText = svc.TextColor;
        var defaultBorder = svc.BorderColor;
        var defaultSuccess = svc.SuccessColor;
        var defaultWarning = svc.WarningColor;
        var defaultError = svc.ErrorColor;
        var defaultGaugeGreen = svc.GaugeGreenColor;
        var defaultGaugeYellow = svc.GaugeYellowColor;
        var defaultGaugeRed = svc.GaugeRedColor;

        Assert.False(ThemePresetRegistry.ApplyPreset(svc, "NonExistentPresetKey"));

        Assert.Equal(defaultPrimary, svc.PrimaryColor);
        Assert.Equal(defaultAccent, svc.AccentColor);
        Assert.Equal(defaultBackground, svc.BackgroundColor);
        Assert.Equal(defaultCard, svc.CardColor);
        Assert.Equal(defaultText, svc.TextColor);
        Assert.Equal(defaultBorder, svc.BorderColor);
        Assert.Equal(defaultSuccess, svc.SuccessColor);
        Assert.Equal(defaultWarning, svc.WarningColor);
        Assert.Equal(defaultError, svc.ErrorColor);
        Assert.Equal(defaultGaugeGreen, svc.GaugeGreenColor);
        Assert.Equal(defaultGaugeYellow, svc.GaugeYellowColor);
        Assert.Equal(defaultGaugeRed, svc.GaugeRedColor);
    }
}
