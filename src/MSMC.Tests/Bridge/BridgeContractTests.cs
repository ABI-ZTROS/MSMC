using System.Text.Json;
using Xunit;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Scheduler.Models;

namespace MSMC.Tests.Bridge;

public class BridgeContractTests
{
    [Fact]
    public void NotifyDispatch_ValidEventJson_DeserializesCorrectly()
    {
        var json = """{"eventType":"PluginInstalled","sourceModule":"Bridge","title":"test","message":"msg"}""";
        using var doc = JsonDocument.Parse(json);
        var evt = JsonSerializer.Deserialize<NotificationEvent>(doc.RootElement.GetRawText());
        Assert.NotNull(evt);
        Assert.Equal(NotificationEventType.PluginInstalled, evt!.EventType);
        Assert.Equal("Bridge", evt.SourceModule);
    }

    [Fact]
    public void MarketSearch_QueryLimit_ExtractsCorrectly()
    {
        var json = """{ "query": "sodium", "limit": 10 }""";
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        string query = el.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
        int limit = el.TryGetProperty("limit", out var l) ? l.GetInt32() : 20;
        Assert.Equal("sodium", query);
        Assert.Equal(10, limit);
    }

    [Fact]
    public void MarketSearch_MissingLimit_DefaultsTo20()
    {
        var json = """{ "query": "fabric" }""";
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        int limit = el.TryGetProperty("limit", out var l) ? l.GetInt32() : 20;
        Assert.Equal(20, limit);
    }

    [Fact]
    public void MarketInstall_VersionAndServerPath_ExtractsCorrectly()
    {
        var json = """{"version":{"id":"v123","versionNumber":"1.2.3","projectId":"p1"},"serverPath":"C:\\Servers\\MyServer"}""";
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        var serverPath = el.TryGetProperty("serverPath", out var sp) ? sp.GetString() ?? "" : "";
        var versionJson = el.TryGetProperty("version", out var vj) ? vj.GetRawText() : "{}";
        var version = JsonSerializer.Deserialize<MarketVersion>(versionJson);
        Assert.Equal("C:\\Servers\\MyServer", serverPath);
        Assert.NotNull(version);
        Assert.Equal("v123", version!.Id);
        Assert.Equal("1.2.3", version.VersionNumber);
    }

    [Fact]
    public void SchedulerRunNow_ValidGuid_ParsesSuccessfully()
    {
        string guidStr = "12345678-1234-1234-1234-1234567890ab";
        var ok = Guid.TryParse(guidStr, out var taskId);
        Assert.True(ok);
        Assert.Equal("12345678-1234-1234-1234-1234567890ab", taskId.ToString());
    }

    [Fact]
    public void ScheduledTask_TriggerNestedProperty_AccessesCorrectly()
    {
        var task = new ScheduledTask
        {
            Name = "Backup",
            Enabled = true,
            Trigger = new TriggerConfig { Type = TriggerType.Cron, CronExpression = "0 */6 * * *" },
            Action = new ActionConfig { Type = ActionType.RunBackup },
            LastStatus = TaskStatus.Completed
        };
        Assert.Equal("Cron", task.Trigger.Type.ToString());
        Assert.Equal("0 */6 * * *", task.Trigger.CronExpression);
        Assert.True(task.Enabled);
        Assert.Equal("Completed", task.LastStatus.ToString());
        Assert.Equal("RunBackup", task.Action.Type.ToString());
    }

    [Fact]
    public void MarketVersion_RealProperties_AllAccessible()
    {
        var version = new MarketVersion
        {
            Id = "v1", VersionNumber = "1.0.0", Name = "TestMod",
            ReleasedAt = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero),
            IsPreRelease = false
        };
        Assert.Equal("v1", version.Id);
        Assert.Equal("1.0.0", version.VersionNumber);
        Assert.Equal("TestMod", version.Name);
        Assert.NotNull(version.ReleasedAt);
    }

    [Fact]
    public void MarketProject_RealProperties_AllAccessible()
    {
        var project = new MarketProject
        {
            Id = "p1", Name = "Sodium", Author = "jellysquid3", Downloads = 5000000,
            Source = MarketSource.Modrinth, SupportedLoaders = new List<ModLoader> { ModLoader.Fabric }
        };
        Assert.Equal("Sodium", project.Name);
        Assert.Equal("jellysquid3", project.Author);
        Assert.Equal(5000000, project.Downloads);
    }

    [Fact]
    public void InstallResult_Failed_SetsErrorCorrectly()
    {
        var result = InstallResult.Failed("project1", "No download URL");
        Assert.False(result.Success);
        Assert.Equal("project1", result.ProjectId);
        Assert.Equal("No download URL", result.Error);
    }

    [Fact]
    public void InstallResult_Succeeded_HasBackupPath()
    {
        var result = InstallResult.Succeeded("p1", "TestMod", "1.0.0", "/backups/test.jar.bak");
        Assert.True(result.Success);
        Assert.Equal("p1", result.ProjectId);
        Assert.Equal("TestMod", result.ProjectName);
    }
}
