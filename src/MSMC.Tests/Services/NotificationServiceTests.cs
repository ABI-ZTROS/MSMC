// -----------------------------------------------------------------------------
// 文件名: NotificationServiceTests.cs
// 项目: MSMC.Tests
// 功能描述: 通知服务单元测试 —— 验证因果链（事件触发）与返回链（日志记录）
// -----------------------------------------------------------------------------

using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;

namespace MSMC.Tests.Services;

public class NotificationServiceTests
{
    private NotificationChannelConfig CreateTestConfig(
        bool discordEnabled = true,
        bool genericEnabled = false,
        bool toastEnabled = true)
    {
        return new NotificationChannelConfig
        {
            Discord = new DiscordChannelConfig
            {
                Enabled = discordEnabled,
                WebhookUrl = "https://discord.com/api/webhooks/test"
            },
            GenericWebhook = new GenericWebhookChannelConfig
            {
                Enabled = genericEnabled,
                Url = "https://example.com/webhook"
            },
            WindowsToast = new ToastChannelConfig
            {
                Enabled = toastEnabled
            }
        };
    }

    [Fact]
    public async Task DispatchAsync_AllChannelsEnabled_DispatchesToAll()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var mockDiscordSender = new Mock<IDiscordWebhookSender>();
        mockDiscordSender.Setup(s => s.SendEmbedAsync(It.IsAny<string>(), It.IsAny<EmbeddedMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var config = CreateTestConfig(discordEnabled: true, genericEnabled: false, toastEnabled: true);
        var service = new NotificationService(mockLogger.Object, mockDiscordSender.Object, config);

        var evt = new NotificationEvent
        {
            EventType = NotificationEventType.ServerCrashed,
            Title = "Server Crash",
            Message = "Minecraft server crashed with exit code 1",
            SourceModule = "TestModule",
            TargetServerId = "server-001"
        };

        // Act
        var result = await service.DispatchAsync(evt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.ChannelResults[NotificationChannelType.DiscordWebhook]);
        Assert.True(result.ChannelResults[NotificationChannelType.WindowsToast]);
        Assert.Equal(2, result.SuccessfulChannels);

        // 返回链：验证日志记录
        mockLogger.Verify(
            l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<string>(s => s.Contains("Dispatching event")),
                It.IsAny<Exception>(), It.IsAny<string>()),
            Times.AtLeastOnce,
            "Should log dispatch start");

        mockLogger.Verify(
            l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<string>(s => s.Contains("dispatched")),
                It.IsAny<Exception>(), It.IsAny<string>()),
            Times.AtLeastOnce,
            "Should log dispatch completion");
    }

    [Fact]
    public async Task DispatchAsync_DiscordFailure_StillReportsResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var mockDiscordSender = new Mock<IDiscordWebhookSender>();
        mockDiscordSender.Setup(s => s.SendEmbedAsync(It.IsAny<string>(), It.IsAny<EmbeddedMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Discord 失败

        var config = CreateTestConfig(discordEnabled: true, genericEnabled: false, toastEnabled: true);
        var service = new NotificationService(mockLogger.Object, mockDiscordSender.Object, config);

        var evt = new NotificationEvent
        {
            EventType = NotificationEventType.BackupCompleted,
            Title = "Backup",
            Message = "Backup completed successfully"
        };

        // Act
        var result = await service.DispatchAsync(evt);

        // Assert
        Assert.False(result.ChannelResults[NotificationChannelType.DiscordWebhook]);
        Assert.True(result.ChannelResults[NotificationChannelType.WindowsToast]);
        Assert.True(result.IsSuccess); // 只要有一个通道成功就算成功
    }

    [Fact]
    public async Task DispatchAsync_NoChannelsEnabled_ReturnsSuccessWithoutError()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var mockDiscordSender = new Mock<IDiscordWebhookSender>();
        var config = CreateTestConfig(discordEnabled: false, genericEnabled: false, toastEnabled: false);
        var service = new NotificationService(mockLogger.Object, mockDiscordSender.Object, config);

        var evt = new NotificationEvent
        {
            EventType = NotificationEventType.ManualTest,
            Title = "Test",
            Message = "Test message"
        };

        // Act
        var result = await service.DispatchAsync(evt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.TotalChannels);
        mockDiscordSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DispatchAsync_ServerCrash_UsesRedColor()
    {
        // 验证因果链：ServerCrashed 事件应使用红色（0xe74c3c）
        var mockLogger = new Mock<ILogger<NotificationService>>();
        EmbeddedMessage? capturedEmbed = null;
        var mockDiscordSender = new Mock<IDiscordWebhookSender>();
        mockDiscordSender.Setup(s => s.SendEmbedAsync(It.IsAny<string>(), It.IsAny<EmbeddedMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback<string, EmbeddedMessage, CancellationToken>((_, embed, _) => capturedEmbed = embed);

        var config = CreateTestConfig();
        var service = new NotificationService(mockLogger.Object, mockDiscordSender.Object, config);

        var evt = new NotificationEvent
        {
            EventType = NotificationEventType.ServerCrashed,
            Title = "Server Down",
            Message = "Critical failure"
        };

        await service.DispatchAsync(evt);

        Assert.NotNull(capturedEmbed);
        Assert.Equal(0xe74c3c, capturedEmbed!.Color);
    }

    [Fact]
    public async Task DispatchAsync_ServerStart_UsesGreenColor()
    {
        var mockLogger = new Mock<ILogger<NotificationService>>();
        EmbeddedMessage? capturedEmbed = null;
        var mockDiscordSender = new Mock<IDiscordWebhookSender>();
        mockDiscordSender.Setup(s => s.SendEmbedAsync(It.IsAny<string>(), It.IsAny<EmbeddedMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback<string, EmbeddedMessage, CancellationToken>((_, embed, _) => capturedEmbed = embed);

        var config = CreateTestConfig();
        var service = new NotificationService(mockLogger.Object, mockDiscordSender.Object, config);

        var evt = new NotificationEvent
        {
            EventType = NotificationEventType.ServerStarted,
            Title = "Server Up",
            Message = "Server is running"
        };

        await service.DispatchAsync(evt);

        Assert.NotNull(capturedEmbed);
        Assert.Equal(0x2ecc71, capturedEmbed!.Color);
    }

    [Fact]
    public async Task DispatchAsync_ExceptionInChannel_LogsErrorAndContinues()
    {
        // 验证执行链：一个通道抛出异常不会阻塞其他通道
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var mockDiscordSender = new Mock<IDiscordWebhookSender>();
        mockDiscordSender.Setup(s => s.SendEmbedAsync(It.IsAny<string>(), It.IsAny<EmbeddedMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var config = CreateTestConfig();
        var service = new NotificationService(mockLogger.Object, mockDiscordSender.Object, config);

        var evt = new NotificationEvent
        {
            EventType = NotificationEventType.ServerStarted,
            Title = "Test",
            Message = "Test"
        };

        var result = await service.DispatchAsync(evt);

        Assert.False(result.ChannelResults[NotificationChannelType.DiscordWebhook]);
        // Toast 通道仍应成功
        Assert.True(result.ChannelResults[NotificationChannelType.WindowsToast]);

        // 返回链：验证异常被记录
        mockLogger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.IsAny<string>(), It.IsAny<HttpRequestException>(), It.IsAny<string>()),
            Times.AtLeastOnce,
            "Should log the exception from failed channel");
    }
}
