// -----------------------------------------------------------------------------
// 文件名: SchedulerServiceTests.cs
// 项目: MSMC.Tests
// 功能描述: 调度服务单元测试 —— 验证执行链（防重入）与返回链（日志记录）
// -----------------------------------------------------------------------------

using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;
using io.NET.ZTR_OS.Features.Scheduler.Models;
using io.NET.ZTR_OS.Features.Scheduler.Services;

namespace MSMC.Tests.Services;

public class SchedulerServiceTests
{
    private SchedulerService CreateService(
        Mock<ILogger<SchedulerService>>? logger = null,
        Mock<INotificationService>? notificationService = null,
        Mock<ISchedulerStorageService>? storage = null)
    {
        logger ??= new Mock<ILogger<SchedulerService>>();
        notificationService ??= new Mock<INotificationService>();
        storage ??= new Mock<ISchedulerStorageService>();
        return new SchedulerService(logger.Object, notificationService.Object, storage.Object);
    }

    [Fact]
    public void AddTask_ValidTask_SuccessfullyAdded()
    {
        var service = CreateService();
        var task = new ScheduledTask
        {
            Name = "Test Task",
            Trigger = new TriggerConfig { Type = TriggerType.Interval, Interval = TimeSpan.FromMinutes(5) },
            Action = new ActionConfig { Type = ActionType.SendNotification }
        };

        service.AddTask(task);

        var retrieved = service.GetTask(task.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Task", retrieved!.Name);
        Assert.True(retrieved.NextRunTime.HasValue);
    }

    [Fact]
    public void AddTask_LogsInformationMessage()
    {
        var mockLogger = new Mock<ILogger<SchedulerService>>();
        var service = CreateService(mockLogger);

        var task = new ScheduledTask
        {
            Name = "Logged Task",
            Trigger = new TriggerConfig { Type = TriggerType.Interval, Interval = TimeSpan.FromMinutes(1) }
        };

        service.AddTask(task);

        mockLogger.Verify(
            l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<string>(s => s.Contains("Task added")),
                It.IsAny<Exception>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void DeleteTask_ExistingTask_ReturnsTrueAndRemoves()
    {
        var service = CreateService();
        var task = new ScheduledTask { Name = "Deletable" };
        service.AddTask(task);

        var result = service.DeleteTask(task.Id);

        Assert.True(result);
        Assert.Null(service.GetTask(task.Id));
    }

    [Fact]
    public void DeleteTask_NonExistentTask_ReturnsFalse()
    {
        var service = CreateService();
        var result = service.DeleteTask(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task RunNowAsync_SendNotificationTask_TriggersNotification()
    {
        var mockLogger = new Mock<ILogger<SchedulerService>>();
        var mockNotifService = new Mock<INotificationService>();
        mockNotifService.Setup(n => n.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult
            {
                EventId = Guid.NewGuid(),
                TotalChannels = 1,
                SuccessfulChannels = 1
            });

        var mockStorage = new Mock<ISchedulerStorageService>();
        var service = new SchedulerService(mockLogger.Object, mockNotifService.Object, mockStorage.Object);

        var task = new ScheduledTask
        {
            Name = "Notification Test",
            Trigger = new TriggerConfig { Type = TriggerType.Interval, Interval = TimeSpan.FromHours(1) },
            Action = new ActionConfig
            {
                Type = ActionType.SendNotification,
                CommandOrPath = "Test notification message"
            }
        };

        service.AddTask(task);
        var result = await service.RunNowAsync(task.Id);

        Assert.True(result);
        mockNotifService.Verify(
            n => n.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "Notification should have been dispatched once");
    }

    [Fact]
    public async Task RunNowAsync_ConsecutiveFailures_DisablesTask()
    {
        var mockLogger = new Mock<ILogger<SchedulerService>>();
        var mockNotifService = new Mock<INotificationService>();
        // 模拟通知失败
        mockNotifService.Setup(n => n.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult
            {
                TotalChannels = 0,
                SuccessfulChannels = 0
            });

        var mockStorage = new Mock<ISchedulerStorageService>();
        var service = new SchedulerService(mockLogger.Object, mockNotifService.Object, mockStorage.Object);

        var task = new ScheduledTask
        {
            Name = "Failing Task",
            MaxConsecutiveFailures = 2,
            Trigger = new TriggerConfig { Type = TriggerType.Interval, Interval = TimeSpan.FromHours(1) },
            Action = new ActionConfig { Type = ActionType.SendNotification }
        };

        service.AddTask(task);

        // 执行两次使其超过阈值
        await service.RunNowAsync(task.Id);
        await service.RunNowAsync(task.Id);

        var retrieved = service.GetTask(task.Id);
        Assert.NotNull(retrieved);
        Assert.False(retrieved!.Enabled, "Task should be disabled after exceeding max consecutive failures");
        Assert.Equal(2, retrieved.ConsecutiveFailures);
    }

    [Fact]
    public async Task RunNowAsync_NonExistentTask_ReturnsFalse()
    {
        var service = CreateService();
        var result = await service.RunNowAsync(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public void GetAllTasks_MultipleTasks_ReturnsAll()
    {
        var service = CreateService();
        service.AddTask(new ScheduledTask { Name = "Task 1" });
        service.AddTask(new ScheduledTask { Name = "Task 2" });
        service.AddTask(new ScheduledTask { Name = "Task 3" });

        var tasks = service.GetAllTasks();
        Assert.Equal(3, tasks.Count);
    }

    [Fact]
    public void UpdateTask_ModifiesExistingTask()
    {
        var service = CreateService();
        var task = new ScheduledTask { Name = "Original", Enabled = true };
        service.AddTask(task);

        task.Name = "Updated";
        task.Enabled = false;
        service.UpdateTask(task);

        var retrieved = service.GetTask(task.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Updated", retrieved!.Name);
        Assert.False(retrieved.Enabled);
    }
}
