namespace io.NET.ZTR_OS.Tests.Services;

using io.NET.ZTR_OS.Features.ConfigPreview.Models;
using io.NET.ZTR_OS.Features.ConfigPreview.Services;
using Xunit;

public class ConfigPreviewTests
{
    private readonly ConfigImpactAnalyzer _analyzer = new();

    private static List<(string key, string? before, string? after)> MakeParams(
        string key, string? before, string? after)
    {
        return [new() { key = key, before = before, after = after }];
    }

    [Fact]
    public void OnlineMode_TrueToFalse_HighRisk()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/online-mode", "true", "false"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.High, result[0].ImpactSeverity);
        Assert.Contains("关闭正版验证", result[0].Description);
        Assert.Contains("任何玩家都能以任意昵称进入", result[0].Description);
        Assert.Equal("🔴", result[0].Icon);
    }

    [Fact]
    public void Pvp_TrueToFalse_Medium()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/pvp", "true", "false"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("全服玩家互相 PvP 被禁用", result[0].Description);
        Assert.Equal("🟡", result[0].Icon);
    }

    [Fact]
    public void Whitelist_FalseToTrue_Medium_WithSelfWarn()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/white-list", "false", "true"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("请确认你自己在白名单内", result[0].Description);
        Assert.Equal("🟡", result[0].Icon);
    }

    [Fact]
    public void Difficulty_PeacefulToHard_Medium()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/difficulty", "peaceful", "hard"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("全服刷怪难度提升到 hard", result[0].Description);
        Assert.Equal("🟡", result[0].Icon);
    }

    [Fact]
    public void ViewDistance_10To16_High()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/view-distance", "10", "16"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.High, result[0].ImpactSeverity);
        Assert.Contains("内存占用和 CPU 会显著上涨", result[0].Description);
        Assert.Equal("🔴", result[0].Icon);
    }

    [Fact]
    public void MaxAutosaveChunks_Large_Medium()
    {
        var result = _analyzer.Analyze(MakeParams(
            "paper-global.yml/chunk-loading/max-autosave-chunks", "50", "200"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("更大的自动保存批次", result[0].Description);
        Assert.Equal("🟡", result[0].Icon);
    }

    [Fact]
    public void SpawnMonsters_High_Medium()
    {
        var result = _analyzer.Analyze(MakeParams("bukkit.yml/spawn-limits/monsters", "30", "100"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("世界刷怪上限提高", result[0].Description);
        Assert.Equal("🟡", result[0].Icon);
    }

    [Fact]
    public void UnknownKey_ReturnsInfo()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/unknown.foo", "a", "b"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Info, result[0].ImpactSeverity);
        Assert.Contains("未找到该键的预定义规则", result[0].Description);
        Assert.Equal("⚪", result[0].Icon);
    }

    [Fact]
    public void Gamemode_SurvivalToCreative_Medium()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/gamemode", "survival", "creative"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("默认游戏模式", result[0].Description);
        Assert.Contains("creative", result[0].Description);
    }

    [Fact]
    public void LevelSeed_Changed_Info()
    {
        var before = "12345";
        var after = "67890";
        var result = _analyzer.Analyze(MakeParams("server.properties/level-seed", before, after));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Info, result[0].ImpactSeverity);
        Assert.Contains("地图种子", result[0].Description);
    }

    [Fact]
    public void MaxPlayers_SmallToLarge_Medium()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/max-players", "20", "100"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("最大玩家数", result[0].Description);
    }

    [Fact]
    public void ServerPort_Changed_Medium()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/server-port", "25565", "25566"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("服务器端口", result[0].Description);
    }

    [Fact]
    public void Motd_Changed_Info()
    {
        var result = _analyzer.Analyze(MakeParams(
            "server.properties/motd", "A Minecraft Server", "Welcome!"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Info, result[0].ImpactSeverity);
        Assert.Contains("服务器标语", result[0].Description);
    }

    [Fact]
    public void AllowFlight_FalseToTrue_Info()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/allow-flight", "false", "true"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Info, result[0].ImpactSeverity);
        Assert.Contains("允许飞行", result[0].Description);
    }

    [Fact]
    public void EnableCommandBlock_FalseToTrue_Medium()
    {
        var result = _analyzer.Analyze(MakeParams(
            "server.properties/enable-command-block", "false", "true"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("命令方块", result[0].Description);
    }

    [Fact]
    public void ResourcePack_Set_Info()
    {
        var result = _analyzer.Analyze(MakeParams(
            "server.properties/resource-pack", "", "https://example.com/pack.zip"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Info, result[0].ImpactSeverity);
        Assert.Contains("资源包", result[0].Description);
    }

    [Fact]
    public void ForceGamemode_FalseToTrue_Medium()
    {
        var result = _analyzer.Analyze(MakeParams(
            "server.properties/force-gamemode", "false", "true"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("强制游戏模式", result[0].Description);
    }

    [Fact]
    public void SpawnProtection_Increased_Medium()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/spawn-protection", "16", "64"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("出生点保护范围", result[0].Description);
    }

    [Fact]
    public void PlayerIdleTimeout_Set_Medium()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/player-idle-timeout", "0", "30"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Medium, result[0].ImpactSeverity);
        Assert.Contains("玩家挂机超时", result[0].Description);
    }

    [Fact]
    public void Hardcore_FalseToTrue_High()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/hardcore", "false", "true"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.High, result[0].ImpactSeverity);
        Assert.Contains("极限模式", result[0].Description);
        Assert.Contains("死亡后无法复活", result[0].Description);
    }

    [Fact]
    public void MultipleKeys_SortedBySeverity()
    {
        var kvs = new List<(string key, string? before, string? after)>
        {
            new() { key = "server.properties/motd", before = "A", after = "B" },
            new() { key = "server.properties/online-mode", before = "true", after = "false" },
            new() { key = "server.properties/pvp", before = "true", after = "false" },
        };

        var result = _analyzer.Analyze(kvs);

        Assert.Equal(3, result.Count);
        Assert.Equal(ImpactSeverity.High, result[0].ImpactSeverity);
        Assert.Equal(ImpactSeverity.Medium, result[1].ImpactSeverity);
        Assert.Equal(ImpactSeverity.Info, result[2].ImpactSeverity);
    }

    [Fact]
    public void OnlineMode_FalseToTrue_Info()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/online-mode", "false", "true"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Info, result[0].ImpactSeverity);
        Assert.Contains("开启正版验证", result[0].Description);
    }

    [Fact]
    public void ViewDistance_LargeToSmall_Info()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/view-distance", "16", "8"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Info, result[0].ImpactSeverity);
        Assert.Contains("视距减小", result[0].Description);
    }

    [Fact]
    public void Difficulty_HardToPeaceful_Info()
    {
        var result = _analyzer.Analyze(MakeParams("server.properties/difficulty", "hard", "peaceful"));

        Assert.Single(result);
        Assert.Equal(ImpactSeverity.Info, result[0].ImpactSeverity);
        Assert.Contains("难度降低", result[0].Description);
    }

    [Fact]
    public void EmptyChanges_ReturnsEmpty()
    {
        var result = _analyzer.Analyze([]);

        Assert.Empty(result);
    }
}
