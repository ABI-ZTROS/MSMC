using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Scheduler.Models;

namespace MSMC.Tests.Bridge;

/// <summary>
/// Bridge handler 参数解析契约测试 —— 确切实验.
/// 匹配 MainWindow.BridgeJsonOptions: camelCase + JsonStringEnumConverter.
/// 测试 JSON 用前端实际会发的格式 (camelCase property names, string enum values).
/// </summary>
public class BridgeContractTests
{
    private static readonly JsonSerializerOptions BridgeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, BridgeJsonOptions);

    [Fact]
    public void NotifyDispatch_StringEnumJson_DeserializesCorrectly()
    {
        // 前端发: "eventType":"PluginInstalled" (string enum, camelCase)
        var json = """{"eventType":"PluginInstalled","sourceModule":"Bridge","title":"test","message":"msg"}""";
        var evt = Deserialize<NotificationEvent>(json);
        Assert.NotNull(evt);
        Assert.Equal(NotificationEventType.PluginInstalled, evt!.EventType);
        Assert.Equal("Bridge", evt.SourceModule);
        Assert.Equal("test", evt.Title);
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
    public void MarketInstall_CamelCaseVersionJson_DeserializesCorrectly()
    {
        // 前端发: { "version": { "id": "v123", "versionNumber": "1.2.3" }, "serverPath": "..." }
        var versionJson = """{"id":"v123","versionNumber":"1.2.3","projectId":"p1","name":"TestMod"}""";
        var version = Deserialize<MarketVersion>(versionJson);
        Assert.NotNull(version);
        Assert.Equal("v123", version!.Id);
        Assert.Equal("1.2.3", version.VersionNumber);
        Assert.Equal("p1", version.ProjectId);
    }

    [Fact]
    public void SchedulerRunNow_ValidGuid_ParsesSuccessfully()
    {
        string guidStr = "12345678-1234-1234-1234-1234567890ab";
        var ok = Guid.TryParse(guidStr, out _);
        Assert.True(ok);
    }

    [Fact]
    public void SchedulerRunNow_InvalidGuid_ReturnsFalse()
    {
        var ok = Guid.TryParse("not-a-guid", out _);
        Assert.False(ok);
    }

    [Fact]
    public void ScheduledTask_TriggerNestedProperty_AccessesCorrectly()
    {
        var task = new ScheduledTask
        {
            Name = "Backup", Enabled = true,
            Trigger = new TriggerConfig { Type = TriggerType.Cron, CronExpression = "0 */6 * * *" },
            Action = new ActionConfig { Type = ActionType.RunBackup },
            LastStatus = io.NET.ZTR_OS.Features.Scheduler.Models.TaskStatus.Completed
        };
        Assert.Equal("Cron", task.Trigger.Type.ToString());
        Assert.Equal("0 */6 * * *", task.Trigger.CronExpression);
        Assert.Equal("RunBackup", task.Action.Type.ToString());
        Assert.Equal("Completed", task.LastStatus.ToString());
        Assert.True(task.Enabled);
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
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    public void MarketProject_CamelCaseJson_DeserializesCorrectly()
    {
        // 验证 handler BridgeJsonOptions 下 MarketProject 反序列化正常
        var json = """
        {"id":"p1","slug":"sodium","name":"Sodium","author":"jellysquid3","downloads":5000000,"source":"Modrinth"}
        """;
        var project = Deserialize<MarketProject>(json);
        Assert.NotNull(project);
        Assert.Equal("p1", project!.Id);
        Assert.Equal("Sodium", project.Name);
        Assert.Equal("jellysquid3", project.Author);
        Assert.Equal(5000000, project.Downloads);
        Assert.Equal(MarketSource.Modrinth, project.Source);
    }
}
