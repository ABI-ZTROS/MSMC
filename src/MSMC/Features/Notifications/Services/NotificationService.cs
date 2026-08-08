// -----------------------------------------------------------------------------
// 文件名: NotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Notifications.Services
// 功能描述: 通知服务核心 —— 事件路由 + 通道调度
// 设计模式: 三链原则 - 因果链：事件触发；执行链：通道并行投递；返回链：结构化日志
// -----------------------------------------------------------------------------

using io.NET.ZTR_OS.Features.Notifications.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

/// <summary>
/// 通知服务 —— 单例服务，负责事件到通道的路由
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IDiscordWebhookSender _discordSender;
    private readonly NotificationChannelConfig _config;

    public NotificationService(
        ILogger<NotificationService> logger,
        IDiscordWebhookSender discordSender,
        NotificationChannelConfig config)
    {
        _logger = logger;
        _discordSender = discordSender;
        _config = config;
    }

    /// <summary>
    /// 调度通知事件到所有已启用的通道
    /// </summary>
    public async Task<NotificationDispatchResult> DispatchAsync(NotificationEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation("[Notify] Dispatching event {EventType} (Id={EventId}) from {Source}",
            evt.EventType, evt.Id, evt.SourceModule);

        var results = new Dictionary<NotificationChannelType, bool>();

        // Discord 通道
        if (_config.Discord.Enabled)
        {
            bool shouldSend = evt.EventType switch
            {
                NotificationEventType.ServerCrashed => _config.Discord.EnableOnCrash,
                NotificationEventType.ServerStarted or NotificationEventType.ServerStopped => _config.Discord.EnableOnStartStop,
                NotificationEventType.BackupCompleted or NotificationEventType.BackupFailed => _config.Discord.EnableOnBackup,
                NotificationEventType.PluginInstalled or NotificationEventType.PluginUpdateAvailable => _config.Discord.EnableOnPlugin,
                NotificationEventType.ScheduleCompleted => _config.Discord.EnableOnSchedule,
                _ => true
            };

            if (shouldSend)
            {
                try
                {
                    var embed = BuildEmbed(evt);
                    var success = await _discordSender.SendEmbedAsync(_config.Discord.WebhookUrl, embed, ct);
                    results[NotificationChannelType.DiscordWebhook] = success;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Notify] Discord dispatch failed for event {EventId}", evt.Id);
                    results[NotificationChannelType.DiscordWebhook] = false;
                }
            }
        }

        // Generic Webhook 通道
        if (_config.GenericWebhook.Enabled)
        {
            try
            {
                bool success = await SendGenericWebhookAsync(evt, ct);
                results[NotificationChannelType.GenericWebhook] = success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Notify] Generic webhook dispatch failed for event {EventId}", evt.Id);
                results[NotificationChannelType.GenericWebhook] = false;
            }
        }

        // Windows Toast 通道
        if (_config.WindowsToast.Enabled)
        {
            try
            {
                // 注：Windows Toast 通知依赖 Microsoft.Toolkit.Uwp.Notifications，
                // 在实际 WPF 集成中由 ToastNotificationService 处理，此处为占位
                _logger.LogDebug("[Notify] Windows Toast would fire for event {EventId}", evt.Id);
                results[NotificationChannelType.WindowsToast] = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Notify] Windows Toast dispatch failed");
                results[NotificationChannelType.WindowsToast] = false;
            }
        }

        int successCount = results.Values.Count(v => v);
        int totalCount = results.Count;
        _logger.LogInformation("[Notify] Event {EventId} dispatched: {Success}/{Total} channels succeeded",
            evt.Id, successCount, totalCount);

        return new NotificationDispatchResult
        {
            EventId = evt.Id,
            EventType = evt.EventType,
            Timestamp = DateTimeOffset.UtcNow,
            ChannelResults = results,
            TotalChannels = totalCount,
            SuccessfulChannels = successCount
        };
    }

    private EmbeddedMessage BuildEmbed(NotificationEvent evt)
    {
        int color = evt.EventType switch
        {
            NotificationEventType.ServerCrashed or NotificationEventType.BackupFailed => 0xe74c3c,
            NotificationEventType.ServerStarted => 0x2ecc71,
            NotificationEventType.BackupCompleted => 0x3498db,
            NotificationEventType.PluginInstalled => 0x9b59b6,
            _ => 0x95a5a6
        };

        var embed = new EmbeddedMessage
        {
            Title = evt.Title,
            Description = evt.Message,
            Color = color,
            Timestamp = DateTimeOffset.UtcNow
        };

        foreach (var kv in evt.Metadata)
        {
            embed.Fields.Add(new EmbedField
            {
                Name = kv.Key,
                Value = kv.Value.Length > 1024 ? kv.Value[..1020] + "..." : kv.Value,
                Inline = true
            });
        }

        return embed;
    }

    private static readonly HttpClient _genericHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private async Task<bool> SendGenericWebhookAsync(NotificationEvent evt, CancellationToken ct)
    {
        var payload = new
        {
            eventId = evt.Id,
            eventType = evt.EventType.ToString(),
            title = evt.Title,
            message = evt.Message,
            timestamp = evt.CreatedAt.ToString("o"),
            metadata = evt.Metadata
        };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _genericHttpClient.PostAsync(_config.GenericWebhook.Url, content, ct);
        return response.IsSuccessStatusCode;
    }
}

/// <summary>
/// 通知调度结果
/// </summary>
public class NotificationDispatchResult
{
    public Guid EventId { get; set; }
    public NotificationEventType EventType { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<NotificationChannelType, bool> ChannelResults { get; set; } = new();
    public int TotalChannels { get; set; }
    public int SuccessfulChannels { get; set; }
    public bool IsSuccess => TotalChannels == 0 || SuccessfulChannels > 0;
}
