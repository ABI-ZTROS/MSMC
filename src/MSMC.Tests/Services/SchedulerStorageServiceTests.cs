// -----------------------------------------------------------------------------
// 文件名: SchedulerStorageServiceTests.cs
// 项目: MSMC.Tests
// 功能描述: 调度任务持久化单元测试 —— 验证 JSON 序列化、过滤运行时状态
// -----------------------------------------------------------------------------

using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using io.NET.ZTR_OS.Features.Scheduler.Models;
using io.NET.ZTR_OS.Features.Scheduler.Services;
using TaskStatus = io.NET.ZTR_OS.Features.Scheduler.Models.TaskStatus;

namespace MSMC.Tests.Services;

public class SchedulerStorageServiceTests : IDisposable
{
    private readonly string _testPath;
    private readonly Mock<ILogger<SchedulerStorageService>> _mockLogger;

    public SchedulerStorageServiceTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"msmc_test_sched_{Guid.NewGuid():N}.json");
        _mockLogger = new Mock<ILogger<SchedulerStorageService>>();
    }

    public void Dispose()
    {
        if (File.Exists(_testPath)) File.Delete(_testPath);
        if (File.Exists(_testPath + ".tmp")) File.Delete(_testPath + ".tmp");
    }

    [Fact]
    public void Load_NoFile_ReturnsEmptyList()
    {
        var svc = new SchedulerStorageService(_mockLogger.Object, _testPath);
        var tasks = svc.LoadAll();
        
        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }

    [Fact]
    public void Save_And_Load_RoundTripsWithAllFields()
    {
        var svc = new SchedulerStorageService(_mockLogger.Object, _testPath);
        var task = new ScheduledTask
        {
            Name = "Backup Task",
            Enabled = true,
            Trigger = new TriggerConfig
            {
                Type = TriggerType.Cron,
                CronExpression = "0 2 * * *"
            },
            Action = new ActionConfig
            {
                Type = ActionType.SendNotification,
                CommandOrPath = "Backup completed"
            },
            MaxConsecutiveFailures = 5,
            TotalRunCount = 10,
            NextRunTime = DateTimeOffset.UtcNow.AddHours(1),
            LastRunTime = DateTimeOffset.UtcNow.AddHours(-1),
            LastStatus = TaskStatus.Completed
        };
        
        svc.SaveAll(new[] { task });
        var loaded = svc.LoadAll();
        
        Assert.Single(loaded);
        Assert.Equal("Backup Task", loaded[0].Name);
        Assert.True(loaded[0].Enabled);
        Assert.Equal(TriggerType.Cron, loaded[0].Trigger.Type);
        Assert.Equal("0 2 * * *", loaded[0].Trigger.CronExpression);
        Assert.Equal(ActionType.SendNotification, loaded[0].Action.Type);
    }

    [Fact]
    public void Save_ClearsRuntimeFieldsBeforePersisting()
    {
        var svc = new SchedulerStorageService(_mockLogger.Object, _testPath);
        var task = new ScheduledTask
        {
            Name = "Runtime Task",
            Trigger = new TriggerConfig { Type = TriggerType.Interval, Interval = TimeSpan.FromMinutes(5) },
            NextRunTime = DateTimeOffset.UtcNow.AddMinutes(5),
            LastRunTime = DateTimeOffset.UtcNow,
            LastStatus = TaskStatus.Running,
            ConsecutiveFailures = 3
        };
        
        svc.SaveAll(new[] { task });
        var loaded = svc.LoadAll();
        
        Assert.Single(loaded);
        // 运行时字段应被清零
        Assert.Null(loaded[0].NextRunTime);
        Assert.Null(loaded[0].LastRunTime);
        Assert.Equal(TaskStatus.Idle, loaded[0].LastStatus);
        // 配置字段应保留
        Assert.Equal(3, loaded[0].ConsecutiveFailures);
    }

    [Fact]
    public void SaveAsync_And_Load_Works()
    {
        var svc = new SchedulerStorageService(_mockLogger.Object, _testPath);
        var tasks = new[]
        {
            new ScheduledTask { Name = "Task 1", Trigger = new TriggerConfig { Type = TriggerType.Interval, Interval = TimeSpan.FromHours(1) } },
            new ScheduledTask { Name = "Task 2", Trigger = new TriggerConfig { Type = TriggerType.OneTime, OneTimeAt = DateTimeOffset.UtcNow.AddDays(1) } }
        };
        
        svc.SaveAllAsync(tasks).GetAwaiter().GetResult();
        var loaded = svc.LoadAll();
        
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, t => t.Name == "Task 1");
        Assert.Contains(loaded, t => t.Name == "Task 2");
    }

    [Fact]
    public void Save_EmptyList_WritesValidJson()
    {
        var svc = new SchedulerStorageService(_mockLogger.Object, _testPath);
        svc.SaveAll(Array.Empty<ScheduledTask>());
        
        Assert.True(File.Exists(_testPath));
        var content = File.ReadAllText(_testPath);
        Assert.Equal("[]", content.Trim());
        
        var loaded = svc.LoadAll();
        Assert.Empty(loaded);
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        var subDir = Path.Combine(Path.GetTempPath(), $"msmc_sched_dir_{Guid.NewGuid():N}", "deep");
        var path = Path.Combine(subDir, "tasks.json");
        
        try
        {
            var svc = new SchedulerStorageService(_mockLogger.Object, path);
            svc.SaveAll(new[] { new ScheduledTask { Name = "Test" } });
            
            Assert.True(Directory.Exists(subDir));
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(subDir)) Directory.Delete(subDir, true);
        }
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsEmptyList()
    {
        File.WriteAllText(_testPath, "not valid json at all{{{");
        
        var svc = new SchedulerStorageService(_mockLogger.Object, _testPath);
        var tasks = svc.LoadAll();
        
        Assert.Empty(tasks);
        
        _mockLogger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<string>(s => s.Contains("Failed to load")),
                It.IsAny<Exception>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void Logging_RecordsSaveAndLoad()
    {
        var svc = new SchedulerStorageService(_mockLogger.Object, _testPath);
        svc.SaveAll(new[] { new ScheduledTask { Name = "Logging" } });
        svc.LoadAll();
        
        _mockLogger.Verify(
            l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<string>(s => s.Contains("Saving") || s.Contains("saved")),
                It.IsAny<Exception>(), It.IsAny<string>()),
            Times.AtLeastOnce);
        
        _mockLogger.Verify(
            l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<string>(s => s.Contains("Loading") || s.Contains("Loaded")),
                It.IsAny<Exception>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }
}
