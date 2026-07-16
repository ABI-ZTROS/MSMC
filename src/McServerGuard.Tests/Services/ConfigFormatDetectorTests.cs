// 🧪 ConfigFormatDetector 单元测试
namespace McServerGuard.Tests.Services;

using McServerGuard.Services.ConfigManagement;
using Xunit;

public class ConfigFormatDetectorTests
{
    [Fact]
    public void Detect_FromPropertiesContent_ReturnsProperties()
    {
        var content = "#Minecraft server properties\nserver-port=25565\nmax-players=20\n";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Properties, format);
    }

    [Fact]
    public void Detect_FromYamlContent_ReturnsYaml()
    {
        var content = "settings:\n  name: test\n  port: 25565\n";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Yaml, format);
    }

    [Fact]
    public void Detect_FromJsonContent_ReturnsJson()
    {
        var content = "{\"server\":{\"port\":25565},\"name\":\"test\"}";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Json, format);
    }

    [Fact]
    public void Detect_FromTomlLikeContent_ReturnsYaml()
    {
        // TOML 和 YAML 都用 key: value 或 key = value，但 TOML 有 [section] 头
        // 这里确保含冒号缩进的 YAML 不会被误判
        var content = "world-settings:\n  default:\n    mob-spawn-range: 4\n";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Yaml, format);
    }

    [Fact]
    public void Detect_FromEmptyContent_ReturnsUnknown()
    {
        var format = ConfigFormatDetector.Detect("");
        Assert.Equal(ConfigFormat.Unknown, format);
    }

    [Fact]
    public void Detect_FromAmbiguousKeyValueContent_ReturnsProperties()
    {
        // 纯 key=value 无缩进、无冒号 → properties
        var content = "enable-rcon=false\nrcon.port=25575\n";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Properties, format);
    }

    [Fact]
    public void DetectFormat_FromFileExtension_ReturnsExpected()
    {
        Assert.Equal(ConfigFormat.Properties, ConfigFormatDetector.DetectByExtension(".properties"));
        Assert.Equal(ConfigFormat.Yaml, ConfigFormatDetector.DetectByExtension(".yml"));
        Assert.Equal(ConfigFormat.Yaml, ConfigFormatDetector.DetectByExtension(".yaml"));
        Assert.Equal(ConfigFormat.Json, ConfigFormatDetector.DetectByExtension(".json"));
        Assert.Equal(ConfigFormat.Unknown, ConfigFormatDetector.DetectByExtension(".toml"));
    }

    [Fact]
    public void Resolve_CombinesExtensionAndContent_PrefersContent()
    {
        // 扩展名是 .conf（Unknown），内容是 properties → 结果应为 Properties
        var content = "server-port=25565\nmax-players=20\n";
        var format = ConfigFormatDetector.Resolve(content, ".conf");
        Assert.Equal(ConfigFormat.Properties, format);
    }
}
