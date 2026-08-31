// -----------------------------------------------------------------------------
// 文件名: PluginManagerServiceTests.cs
// 项目: MSMC.Tests
// 功能描述: 插件市场 - PluginManagerService 单元测试
//          验证因果链(版本→安装) 与 返回链(结果诚实)
// -----------------------------------------------------------------------------

using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using io.NET.ZTR_OS.Features.ContentMarket.Services;

namespace MSMC.Tests.Services;

public class PluginManagerServiceTests : IDisposable
{
    private readonly string _tempDir;

    public PluginManagerServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MSMC_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* 测试结束不影响 */ }
    }

    private PluginManagerService CreateService()
    {
        var logger = new Mock<ILogger<PluginManagerService>>();
        var mockProvider = new Mock<IMarketProvider>();
        return new PluginManagerService(logger.Object, mockProvider.Object);
    }

    [Fact]
    public async Task InstallAsync_EmptyServerPath_ReturnsFailed()
    {
        var svc = CreateService();
        var version = new MarketVersion
        {
            Id = "v1", ProjectId = "p1", VersionNumber = "1.0",
            Name = "TestMod", DownloadUrl = "https://example.com/test.jar"
        };

        var result = await svc.InstallAsync(version, "");

        Assert.False(result.Success);
        Assert.Contains("Server path", result.Error);
    }

    [Fact]
    public async Task InstallAsync_NoDownloadUrl_ReturnsFailed()
    {
        var svc = CreateService();
        var version = new MarketVersion
        {
            Id = "v1", ProjectId = "p1", VersionNumber = "1.0",
            Name = "NoUrlMod"
        };

        var result = await svc.InstallAsync(version, _tempDir);

        Assert.False(result.Success);
        Assert.Contains("download URL", result.Error);
    }

    [Fact]
    public void GetInstalledPlugins_NoMetaFile_ReturnsEmptyList()
    {
        var svc = CreateService();
        var plugins = svc.GetInstalledPlugins(_tempDir);

        Assert.NotNull(plugins);
        Assert.Empty(plugins);
    }

    [Fact]
    public async Task UninstallAsync_NonExistentFile_ReturnsFalse()
    {
        var svc = CreateService();
        var ok = await svc.UninstallAsync(_tempDir, "non_existent.jar");

        Assert.False(ok);
    }

    [Fact]
    public void GetInstalledPlugins_InvalidPath_ReturnsEmptyList()
    {
        var svc = CreateService();
        var plugins = svc.GetInstalledPlugins("Z:\\nonexistent_path_xyz");

        Assert.NotNull(plugins);
        Assert.Empty(plugins);
    }
}
