// -----------------------------------------------------------------------------
// 文件名: NotificationConfigServiceTests.cs
// 项目: MSMC.Tests
// 功能描述: 通知配置持久化单元测试 —— 验证原子写入和序列化
// -----------------------------------------------------------------------------

using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;

namespace MSMC.Tests.Services;

public class NotificationConfigServiceTests : IDisposable
{
    private readonly string _testPath;
    private readonly Mock<ILogger<NotificationConfigService>> _mockLogger;

    public NotificationConfigServiceTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"msmc_test_notif_{Guid.NewGuid():N}.json");
        _mockLogger = new Mock<ILogger<NotificationConfigService>>();
    }

    public void Dispose()
    {
        if (File.Exists(_testPath)) File.Delete(_testPath);
        if (File.Exists(_testPath + ".tmp")) File.Delete(_testPath + ".tmp");
    }

    [Fact]
    public void Load_FileNotFound_ReturnsDefaultConfig()
    {
        var svc = new NotificationConfigService(_mockLogger.Object, _testPath);
        var config = svc.Load();
        
        Assert.NotNull(config);
        Assert.NotNull(config.Discord);
        Assert.False(config.Discord.Enabled);
    }

    [Fact]
    public void Save_And_Load_RoundTrips()
    {
        var svc = new NotificationConfigService(_mockLogger.Object, _testPath);
        var original = new NotificationChannelConfig
        {
            Discord = new DiscordChannelConfig
            {
                Enabled = true,
                WebhookUrl = "https://discord.com/api/webhooks/test"
            },
            RetryMaxAttempts = 5,
            RetryBaseDelayMs = 1000
        };
        
        svc.Save(original);
        var loaded = svc.Load();
        
        Assert.True(loaded.Discord.Enabled);
        Assert.Equal("https://discord.com/api/webhooks/test", loaded.Discord.WebhookUrl);
        Assert.Equal(5, loaded.RetryMaxAttempts);
    }

    [Fact]
    public void SaveAsync_And_Load_RoundTrips()
    {
        var svc = new NotificationConfigService(_mockLogger.Object, _testPath);
        var original = new NotificationChannelConfig
        {
            Email = new EmailChannelConfig
            {
                Enabled = true,
                SmtpHost = "smtp.test.com",
                SmtpPort = 587
            }
        };
        
        svc.SaveAsync(original).GetAwaiter().GetResult();
        var loaded = svc.Load();
        
        Assert.True(loaded.Email.Enabled);
        Assert.Equal("smtp.test.com", loaded.Email.SmtpHost);
    }

    [Fact]
    public void Save_AtomicWrite_NoPartialFilesOnFailure()
    {
        // 先创建一个只读的"坏"路径来模拟写入失败
        var badPath = Path.Combine(_testPath, "cannot_create", "config.json");
        var svc = new NotificationConfigService(_mockLogger.Object, badPath);
        
        Assert.Throws<DirectoryNotFoundException>(() => svc.Save(new NotificationChannelConfig()));
        
        // 验证没有留下任何临时文件
        Assert.False(Directory.Exists(Path.GetDirectoryName(badPath)));
    }

    [Fact]
    public void Save_CreatesDirectoryIfNotExists()
    {
        var subDir = Path.Combine(Path.GetTempPath(), $"msmc_test_dir_{Guid.NewGuid():N}", "sub");
        var path = Path.Combine(subDir, "config.json");
        
        try
        {
            var svc = new NotificationConfigService(_mockLogger.Object, path);
            svc.Save(new NotificationChannelConfig { RetryMaxAttempts = 42 });
            
            Assert.True(Directory.Exists(subDir));
            Assert.True(File.Exists(path));
            
            var loaded = svc.Load();
            Assert.Equal(42, loaded.RetryMaxAttempts);
        }
        finally
        {
            if (Directory.Exists(subDir)) Directory.Delete(subDir, true);
        }
    }

    [Fact]
    public void Logging_RecordsSaveStartAndEnd()
    {
        var svc = new NotificationConfigService(_mockLogger.Object, _testPath);
        svc.Save(new NotificationChannelConfig());
        
        _mockLogger.Verify(
            l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<string>(s => s.Contains("Saving config")),
                It.IsAny<Exception>(), It.IsAny<string>()),
            Times.Once);
        
        _mockLogger.Verify(
            l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<string>(s => s.Contains("saved successfully")),
                It.IsAny<Exception>(), It.IsAny<string>()),
            Times.Once);
    }
}
