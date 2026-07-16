// 🧪 PropertiesParser 单元测试
// 测试那个朴素的 key=value 解析器，确保它不会在各种奇怪情况下翻车
namespace McServerGuard.Tests.Services;

using McServerGuard.Services.ConfigManagement;
using Xunit;

/// <summary>
/// PropertiesParser 的单元测试 🎯
/// 
/// 覆盖场景：
///   - 正常解析（带注释头、空行、各种值类型）
///   - 序列化后的可逆性（Parse → Serialize → Parse 一致性）
///   - 边界情况和异常处理
/// </summary>
public class PropertiesParserTests
{
    /// <summary>
    /// 模拟一个真实的 server.properties 文件内容 🎮
    /// 包含 Mojang 自动生成的注释头、各种类型的配置项
    /// </summary>
    private const string SampleProperties = """
        #Minecraft server properties
        #Sat Jan 01 00:00:00 UTC 2026
        enable-jmx-monitoring=false
        rcon.port=25575
        level-seed=
        gamemode=survival
        enable-command-block=false
        enable-query=false
        generator-settings={}
        enforce-secure-profile=false
        level-name=world
        motd=A Minecraft Server
        query.port=25565
        pvp=true
        generate-structures=true
        max-chained-neighbor-updates=1000000
        difficulty=easy
        network-compression-threshold=256
        max-tick-time=60000
        require-resource-pack=false
        use-native-transport=true
        max-players=20
        online-mode=true
        enable-status=true
        allow-flight=false
        initial-disabled-packs=
        broadcast-rcon-to-ops=true
        view-distance=10
        server-ip=
        resource-pack-prompt=
        allow-nether=true
        server-port=25565
        enable-rcon=false
        sync-chunk-writes=true
        op-permission-level=4
        prevent-proxy-connections=false
        hide-online-players=false
        resource-pack=
        entity-broadcast-range-percentage=100
        rcon.password=
        player-idle-timeout=0
        force-gamemode=false
        rate-limit=0
        hardcore=false
        white-list=false
        broadcast-console-to-ops=true
        pvp=true
        spawn-npcs=true
        spawn-animals=true
        function-permission-level=2
        initial-enabled-packs=vanilla
        level-type=minecraft\:normal
        text-filtering-config=
        spawn-monsters=true
        enforce-whitelist=false
        spawn-protection=16
        resource-pack-sha1=
        max-world-size=29999984
        """;

    [Fact]
    public void Parse_BasicKeyValue_ReturnsCorrectDictionary()
    {
        // Arrange —— 准备最简单的测试数据
        var content = "server-port=25565\nmax-players=20\nonline-mode=true";

        // Act —— 解析！
        var result = PropertiesParser.Parse(content);

        // Assert —— 检查结果
        Assert.Equal(3, result.Count);
        Assert.Equal("25565", result["server-port"]);
        Assert.Equal("20", result["max-players"]);
        Assert.Equal("true", result["online-mode"]);
    }

    [Fact]
    public void Parse_IgnoresCommentLines()
    {
        // Arrange —— 注释应该被忽略
        var content = """
            # 这是注释
            server-port=25565
            # 这也是注释
            max-players=20
            """;

        // Act
        var result = PropertiesParser.Parse(content);

        // Assert —— 只有 2 个有效配置项
        Assert.Equal(2, result.Count);
        Assert.Equal("25565", result["server-port"]);
        Assert.Equal("20", result["max-players"]);
    }

    [Fact]
    public void Parse_IgnoresEmptyLines()
    {
        // Arrange —— 空行也应该被忽略
        var content = "server-port=25565\n\n\nmax-players=20\n";

        // Act
        var result = PropertiesParser.Parse(content);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_IgnoresLeadingAndTrailingWhitespace()
    {
        // Arrange —— 键和值的首尾空白应该被去掉
        var content = "  server-port  =  25565  \n  max-players  =  20  ";

        // Act
        var result = PropertiesParser.Parse(content);

        // Assert
        Assert.Equal("25565", result["server-port"]);
        Assert.Equal("20", result["max-players"]);
    }

    [Fact]
    public void Parse_ValueContainingEqualsSign_TakesFirstEqualsAsSeparator()
    {
        // Arrange —— 值里可能有 = 号（比如 MOTD 里有特殊字符）
        var content = "motd=Hello=World\nspecial=key=value=end";

        // Act
        var result = PropertiesParser.Parse(content);

        // Assert —— 只认第一个 = 号
        Assert.Equal("Hello=World", result["motd"]);
        Assert.Equal("key=value=end", result["special"]);
    }

    [Fact]
    public void Parse_EmptyValue_ReturnsEmptyString()
    {
        // Arrange —— 空值（如 level-seed=）应该返回空字符串
        var content = "level-seed=\nserver-ip=";

        // Act
        var result = PropertiesParser.Parse(content);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(string.Empty, result["level-seed"]);
        Assert.Equal(string.Empty, result["server-ip"]);
    }

    [Fact]
    public void Parse_ComplexValue_ReturnsAsIs()
    {
        // Arrange —— 复杂值（如 generator-settings 的 JSON）
        var content = "generator-settings={\"biome_source\":{\"type\":\"minecraft:fixed\"}}";

        // Act
        var result = PropertiesParser.Parse(content);

        // Assert —— 复杂值原样保留
        Assert.Equal("{\"biome_source\":{\"type\":\"minecraft:fixed\"}}", result["generator-settings"]);
    }

    [Fact]
    public void Parse_MinecraftServerPropertiesHeader_ParsesCorrectly()
    {
        // Arrange —— 使用模拟的真实 server.properties 内容
        // 这是最重要的测试！确保解析器能正确处理 Mojang 生成的文件

        // Act
        var result = PropertiesParser.Parse(SampleProperties);

        // Assert —— 检查关键配置项
        Assert.Equal("25565", result["server-port"]);
        Assert.Equal("20", result["max-players"]);
        Assert.Equal("true", result["online-mode"]);
        Assert.Equal("A Minecraft Server", result["motd"]);
        Assert.Equal("10", result["view-distance"]);
        Assert.Equal("easy", result["difficulty"]);
        Assert.Equal("true", result["pvp"]);
        Assert.Equal("false", result["enable-command-block"]);
        Assert.Equal("false", result["allow-flight"]);
        Assert.Equal("false", result["white-list"]);
        Assert.Equal("false", result["enable-rcon"]);
        Assert.Equal("25575", result["rcon.port"]);
        Assert.Equal("world", result["level-name"]);
        Assert.Equal(string.Empty, result["server-ip"]);
        Assert.Equal(string.Empty, result["level-seed"]);

        // 确认注释行（以 # 开头的）没有被解析为配置项
        Assert.DoesNotContain("#Minecraft", result.Keys);
        Assert.DoesNotContain("#Sat", result.Keys);
    }

    [Fact]
    public void Parse_LineWithoutEquals_ThrowsFormatException()
    {
        // Arrange —— 没有 = 号的非空非注释行，应该报错
        var content = "server-port=25565\nthis-is-not-a-valid-line\nmax-players=20";

        // Act & Assert
        var ex = Assert.Throws<FormatException>(() => PropertiesParser.Parse(content));
        Assert.Contains("缺少 = 号", ex.Message);
    }

    [Fact]
    public void Parse_EmptyKey_ThrowsFormatException()
    {
        // Arrange —— = 前面没有键名，离谱
        var content = "=25565";

        // Act & Assert
        var ex = Assert.Throws<FormatException>(() => PropertiesParser.Parse(content));
        Assert.Contains("键名为空", ex.Message);
    }

    [Fact]
    public void Parse_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert —— null 输入必须炸
        Assert.Throws<ArgumentNullException>(() => PropertiesParser.Parse(null!));
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyDictionary()
    {
        // Act —— 空字符串
        var result = PropertiesParser.Parse("");

        // Assert —— 空结果
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_OnlyComments_ReturnsEmptyDictionary()
    {
        // Act —— 全是注释
        var result = PropertiesParser.Parse("# comment 1\n# comment 2\n# comment 3");

        // Assert —— 空结果
        Assert.Empty(result);
    }

    [Fact]
    public void Serialize_BasicDictionary_ReturnsFormattedOutput()
    {
        // Arrange
        var config = new Dictionary<string, string>
        {
            ["server-port"] = "25565",
            ["max-players"] = "20",
            ["online-mode"] = "true",
        };

        // Act
        var result = PropertiesParser.Serialize(config);

        // Assert —— 应该包含所有键值对，按字母序排列
        Assert.Contains("max-players=20", result);
        Assert.Contains("online-mode=true", result);
        Assert.Contains("server-port=25565", result);

        // max-players 应该在 online-mode 前面（字母序）
        Assert.True(result.IndexOf("max-players") < result.IndexOf("online-mode"));
    }

    [Fact]
    public void Serialize_ThenParse_IsRoundTrip()
    {
        // Arrange —— 原始数据
        var original = new Dictionary<string, string>
        {
            ["server-port"] = "25565",
            ["max-players"] = "20",
            ["motd"] = "Hello=World",
            ["level-seed"] = "",
            ["online-mode"] = "true",
        };

        // Act —— 序列化后再解析回来
        var serialized = PropertiesParser.Serialize(original);
        var roundTripped = PropertiesParser.Parse(serialized);

        // Assert —— 应该和原始数据一致
        Assert.Equal(original.Count, roundTripped.Count);
        foreach (var kvp in original)
        {
            Assert.True(roundTripped.ContainsKey(kvp.Key),
                $"往返后丢失了键: {kvp.Key}");
            Assert.True(string.Equals(kvp.Value, roundTripped[kvp.Key]),
                $"键 {kvp.Key} 的值在往返后变了: 期望 [{kvp.Value}]，实际 [{roundTripped[kvp.Key]}]");
        }
    }

    [Fact]
    public void Serialize_NullDictionary_ThrowsArgumentNullException()
    {
        // Act & Assert —— null 字典必须炸
        Assert.Throws<ArgumentNullException>(() => PropertiesParser.Serialize(null!));
    }

    [Fact]
    public void Serialize_EmptyDictionary_ReturnsEmptyString()
    {
        // Act
        var result = PropertiesParser.Serialize([]);

        // Assert —— 空字典应该返回空字符串或只有换行
        Assert.True(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void Parse_CaseInsensitiveKeyLookup()
    {
        // Arrange —— PropertiesParser 的键名查找是不区分大小写的
        var content = "Server-Port=25565\nMax-Players=20";

        // Act
        var result = PropertiesParser.Parse(content);

        // Assert —— 大小写不敏感查找
        Assert.Equal("25565", result["server-port"]);
        Assert.Equal("25565", result["SERVER-PORT"]);
        Assert.Equal("25565", result["Server-Port"]);
        Assert.Equal("20", result["max-players"]);
        Assert.Equal("20", result["MAX-PLAYERS"]);
    }

    [Fact]
    public void Parse_ValueWithUnicodeCharacters_PreservesContent()
    {
        // Arrange —— 值里包含中文/Unicode（比如中文 MOTD）
        var content = "motd=一个中国服务器 \u00a7b欢迎加入！";

        // Act
        var result = PropertiesParser.Parse(content);

        // Assert —— Unicode 内容应该完整保留
        Assert.Equal("一个中国服务器 \u00a7b欢迎加入！", result["motd"]);
    }
}
