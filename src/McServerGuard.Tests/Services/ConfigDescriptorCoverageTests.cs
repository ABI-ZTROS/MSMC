// 🧪 配置描述符覆盖率测试
namespace McServerGuard.Tests.Services;

using McServerGuard.Services.ConfigManagement;
using Xunit;

public class ConfigDescriptorCoverageTests
{
    [Fact]
    public void CoverageReport_ReturnsTotalAndCoveredCounts()
    {
        var registry = new ConfigDescriptorRegistry();
        var report = registry.GetCoverageReport();

        Assert.True(report.TotalDescriptors > 200, $"应有 200+ 描述符，实际 {report.TotalDescriptors}");
        Assert.NotEmpty(report.FileStats);
    }

    [Fact]
    public void CoverageReport_ServerProperties_HasHighCoverage()
    {
        var registry = new ConfigDescriptorRegistry();
        var report = registry.GetCoverageReport();

        // server.properties 应有至少 50 个描述符
        var serverProps = report.FileStats.FirstOrDefault(f => f.ConfigFileName == "server.properties");
        Assert.NotNull(serverProps);
        Assert.True(serverProps!.DescriptorCount >= 50,
            $"server.properties 应有 50+ 描述符，实际 {serverProps.DescriptorCount}");
    }

    [Fact]
    public void CoverageReport_AllFilesHaveDescriptors()
    {
        var registry = new ConfigDescriptorRegistry();
        var report = registry.GetCoverageReport();

        foreach (var stat in report.FileStats)
        {
            Assert.True(stat.DescriptorCount > 0,
                $"文件 {stat.ConfigFileName} 描述符数为 0，翻译未覆盖");
        }
    }

    [Fact]
    public void FindUnmatchedKeys_ReturnsKeysWithoutDescriptors()
    {
        var registry = new ConfigDescriptorRegistry();
        // 模拟一个真实配置文件的 key 列表
        var configKeys = new List<string> { "server-port", "max-players", "this-key-does-not-exist-12345" };
        var unmatched = registry.FindUnmatchedKeys(configKeys, "server.properties");

        Assert.Contains("this-key-does-not-exist-12345", unmatched);
        Assert.DoesNotContain("server-port", unmatched);
    }
}
