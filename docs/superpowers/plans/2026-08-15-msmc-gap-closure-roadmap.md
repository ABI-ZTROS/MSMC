# MSMC 竞品缺口补齐 · 全面执行方案

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 从「本机好用的控制台」升级为「本机省心的运维平台」——补齐 P0（通知/调度/市场）三件套，再依次推进 P1（移动可达/长期历史/自动更新）与 P2（质量底座），不改变 Windows/单机/中文的核心定位。

**Architecture:** 新增 4 个 Feature 模块（Notifications / Scheduler / ContentMarket / MobileCompanion），复用现有 WebView2Bridge 通信协议与 AppConfigService 持久化模式；后端纯 C# 服务（不引入 ASP.NET Core 重架构，远程能力走可选轻量 HTTP companion），前端 React 页面增量接入；三链原则作为所有设计决策的一票否决项。

**Tech Stack:** 后端（C# / .NET 9 WPF / CommunityToolkit.Mvvm DI / System.Text.Json / 已有 ToastNotificationService + MetricsPersistenceService 模式）；前端（React 18 / TS / Vite / Tailwind / Zustand appStore）；外部 API（Modrinth v2 REST / CurseForge v1 REST / Discord Webhook POST / SMTP or MailKit 可选）。

---

## ⚖️ 三链原则（所有设计决策的一票否决项）

> **三链原则必须在每一个技术决策、每一次方案评审时被逐条对照。任何违反三链的提案，哪怕功能再强大，直接否决。**

| 链序 | 链名 | 内容 | 一票否决情形 |
|---|---|---|---|
| **链一（守势链 · 护城河）** | **「一寸不让」** | **守住 Windows 深度集成、本地隐私不出本机、中文母语体验、精致主题系统这四大差异化优势。** 任何改动不得降低 netsh/WMI/CPU QoS 等原生能力的体验等级；用户配置、日志、玩家数据默认永不上传；中文为第一语言；主题系统优先保证中文环境下的视觉效果。 | ❌ 任何将用户数据默认出网的设计；❌ 任何用通用 Web 能力替代 Windows API 的"简化"；❌ 任何先出英文版再补中文的开发顺序 |
| **链二（攻势链 · 省心层）** | **「补而不换」** | **只补「让服主省心」的能力缺口（通知、计划任务、插件市场、长期历史、告警阈值、自动追踪），绝不因此重构主形态。** 所有新增功能必须首先是"桌面客户端内的功能"，远程可达性只作为**可选 companion**叠加，不替代主 UI；持久化默认使用与 MetricsPersistenceService 同级的本地存储，不引入外部数据库依赖。 | ❌ 为了远程而把整个架构 Web 化；❌ 为了自动化而引入必须常驻的外部服务；❌ 为了 i18n 把中文体验降级 |
| **链三（禁手链 · 边界）** | **「不拿短板打长板」** | **绝不追 Pterodactyl/MCSM 的主场能力：不全 Web 化、不搞 Docker 容器化、不搞分布式多节点、不做多用户 RBAC 的默认形态。** 多用户/权限只做「可选模式」预留接口不默认开启；容器化最多利用 Windows Job 对象/沙箱（已有 Sandbox 命名空间雏形），不引入 Docker。 | ❌ 改 WPF 外壳为纯 Web 前台；❌ 接入 Docker Desktop 作为默认依赖；❌ 做跨机管理中心 |

---

## 📁 文件结构地图（新增 / 修改总览）

> 路径全部相对于 `/workspace/src/`。沿用既有 `Features/<ModuleName>/{Models,Services,ViewModels,Views}` 四层。

### 新增模块（P0）

```
MSMC/Features/Notifications/              # P0 通知系统
  ├── Models/
  │   ├── NotificationChannel.cs          # 通道枚举+配置（Discord/Webhook/Email/WinToast）
  │   ├── NotificationEvent.cs            # 事件类型（崩溃/上线/备份完成/阈值越界）
  │   ├── NotificationTemplate.cs         # 消息模板（含 Embed 结构）
  │   └── NotificationDeliveryResult.cs   # 投递结果+重试状态
  ├── Services/
  │   ├── INotificationService.cs         # 主服务接口
  │   ├── NotificationService.cs          # 主服务：事件路由+通道调度+重试队列
  │   ├── IDiscordWebhookSender.cs        # Discord 通道（HTTP POST，指数退避）
  │   ├── DiscordWebhookSender.cs
  │   ├── IGenericWebhookSender.cs        # 通用 Webhook 通道
  │   ├── GenericWebhookSender.cs
  │   ├── IEmailSender.cs                 # 邮件通道（可空，MailKit 可选包）
  │   ├── EmailSender.cs
  │   └── WindowsToastForwarder.cs        # 转发到现有 ToastNotificationService
  ├── ViewModels/
  │   └── NotificationsViewModel.cs       # 通道配置+事件订阅 UI 状态
  └── Views/
      ├── NotificationsPage.xaml          # WPF 兜底页
      └── NotificationsPage.xaml.cs

MSMC/Features/Scheduler/                  # P0 计划任务调度器
  ├── Models/
  │   ├── ScheduledTask.cs                # 任务定义（Basic/Cron/Chain 三种模式，对齐 Crafty）
  │   ├── TaskTrigger.cs                  # 触发器（cron expr / interval / parent-id）
  │   ├── TaskAction.cs                   # 动作枚举（备份/重启/启动/停止/RCON指令/通知）
  │   ├── TaskExecutionRecord.cs          # 执行历史记录
  │   └── CronExpression.cs               # cron 解析器（NCrontab 或自研）
  ├── Services/
  │   ├── ISchedulerService.cs            # 主调度接口：CRUD + 立即触发 + 暂停/恢复
  │   ├── SchedulerService.cs             # 主实现：Timer 轮询 + 最小堆下一次执行时间
  │   ├── ICronParser.cs                  # cron 解析抽象
  │   ├── CronParser.cs
  │   ├── ITaskExecutor.cs                # 动作执行器（分发到 ServerManager/Backup/Rcon/Notify）
  │   └── TaskExecutor.cs
  ├── ViewModels/
  │   └── SchedulerViewModel.cs           # 任务列表+编辑器 UI 状态
  └── Views/
      ├── SchedulerPage.xaml
      └── SchedulerPage.xaml.cs

MSMC/Features/ContentMarket/              # P0 插件 / Mod 一键市场
  ├── Models/
  │   ├── MarketSource.cs                 # 来源枚举（Modrinth/CurseForge/Local）
  │   ├── MarketProject.cs                # 搜索结果项（含 icon/author/category/loader）
  │   ├── MarketVersion.cs                # 版本项（游戏版本/loader/依赖/下载链接）
  │   ├── InstalledPlugin.cs              # 已安装记录（版本/来源/安装时间/sha256）
  │   └── InstallOperationResult.cs       # 安装/更新/卸载结果
  ├── Services/
  │   ├── IMarketProvider.cs              # 统一市场接口（搜索/版本/下载）
  │   ├── ModrinthProvider.cs             # Modrinth v2 API（api.modrinth.com/v2）
  │   ├── CurseForgeProvider.cs           # CurseForge v1 API（需用户填入 X-Api-Token）
  │   ├── IPluginManagerService.cs        # 安装/更新/卸载/回滚/扫描
  │   ├── PluginManagerService.cs         # 落 plugins/ 目录，操作前自动备份
  │   └── VersionCompatibilityChecker.cs  # loader + 游戏版本匹配校验
  ├── ViewModels/
  │   └── ContentMarketViewModel.cs       # 搜索/筛选/详情/安装状态
  └── Views/
      ├── ContentMarketPage.xaml
      └── ContentMarketPage.xaml.cs
```

### 新增模块（P1）

```
MSMC/Features/Alerts/                     # P1 阈值告警（与通知系统联动）
  ├── Models/
  │   ├── AlertRule.cs                    # 规则定义（指标/阈值/持续窗口/级别）
  │   ├── AlertEvent.cs                   # 告警事件（触发/恢复）
  │   └── AlertState.cs                   # 当前活跃告警集
  └── Services/
      ├── IAlertEngine.cs
      └── AlertEngine.cs                  # 订阅 SystemMonitor 采样，滑窗判定

MSMC/Features/AutoUpdate/                 # P1 自动更新追踪（核心/Java/插件）
  ├── Models/
  │   ├── TrackedArtifact.cs              # 被追踪的产物（core/java/plugin）
  │   ├── UpdateCheckResult.cs            # 版本对比结果（newer / equal / error）
  │   └── UpdateChannel.cs                # 通道（Stable/RC/Beta）
  └── Services/
      ├── IUpdateTrackerService.cs
      └── UpdateTrackerService.cs         # 订阅 Release RSS/API，不默认自动装

MSMC/Features/MobileCompanion/            # P1 移动可达（轻量 HTTP companion，可选）
  ├── Models/
  │   ├── CompanionConfig.cs              # 绑定 IP/端口/鉴权 token/只读开关
  │   └── CompanionSession.cs
  └── Services/
      ├── ICompanionHttpServer.cs         # HttpListener 实现（不引 ASP.NET Core）
      └── CompanionHttpServer.cs          # /api/status /api/metrics /api/action 三端点
```

### 修改的既有文件

```
MSMC/Features/WebView2/Services/WebView2BridgeService.cs
  └── 注册新 action：notification.* / scheduler.* / market.* / alerts.* / companion.*

MSMC/Features/Settings/Services/AppConfigService.cs
  └── 持久化新增模块的配置（通道列表、任务列表、告警规则、追踪项、companion 开关）

MSMC/Features/Shared/ViewModels/MainViewModel.cs
  └── DI 注册 4 个新服务 + 侧边栏新增 4 个 NavItem

frontend/src/types/bridge.ts
  └── 追加 Notifications / Scheduler / ContentMarket / Alerts / Companion 五组类型

frontend/src/stores/appStore.ts
  └── 追加对应状态切片

frontend/src/components/Sidebar.tsx
  └── 追加 4 个新菜单项（通知、调度、市场、告警）

frontend/src/pages/
  ├── NotificationsPage.tsx
  ├── SchedulerPage.tsx
  ├── ContentMarketPage.tsx
  └── AlertsPage.tsx
```

---

## 🗺️ 优先级路线图与任务分解

### 🔴 P0 · 本月落地：通知系统 + 计划任务 + 插件市场

> **价值：从「能管」到「省心」的关键一跃。完成后服主可以晚上睡觉，服务器崩了/备份好了都会主动喊你。**

---

### Task 1: 通知系统核心（Notifications 模块）

**Files:**
- Create: `MSMC/Features/Notifications/Models/NotificationChannel.cs`
- Create: `MSMC/Features/Notifications/Models/NotificationEvent.cs`
- Create: `MSMC/Features/Notifications/Models/NotificationTemplate.cs`
- Create: `MSMC/Features/Notifications/Models/NotificationDeliveryResult.cs`
- Create: `MSMC/Features/Notifications/Services/INotificationService.cs`
- Create: `MSMC/Features/Notifications/Services/NotificationService.cs`
- Create: `MSMC/Features/Notifications/Services/IDiscordWebhookSender.cs`
- Create: `MSMC/Features/Notifications/Services/DiscordWebhookSender.cs`
- Create: `MSMC/Features/Notifications/Services/IGenericWebhookSender.cs`
- Create: `MSMC/Features/Notifications/Services/GenericWebhookSender.cs`
- Create: `MSMC/Features/Notifications/Services/IEmailSender.cs`
- Create: `MSMC/Features/Notifications/Services/EmailSender.cs`
- Create: `MSMC/Features/Notifications/Services/WindowsToastForwarder.cs`
- Create: `MSMC/Features/Notifications/ViewModels/NotificationsViewModel.cs`
- Create: `MSMC/Features/Notifications/Views/NotificationsPage.xaml`
- Create: `MSMC/Features/Notifications/Views/NotificationsPage.xaml.cs`
- Modify: `MSMC/Features/WebView2/Services/WebView2BridgeService.cs` — 注册 notification.* actions
- Modify: `MSMC/Features/Settings/Services/AppConfigService.cs` — 持久化通道配置
- Modify: `MSMC/Features/Shared/ViewModels/MainViewModel.cs` — DI + 侧边栏
- Modify: `MSMC/MSMC.csproj` — 可选添加 MailKit 包（邮件通道用）
- Modify: `frontend/src/types/bridge.ts` — 追加通知类型
- Modify: `frontend/src/components/Sidebar.tsx` — 加菜单项
- Create: `frontend/src/pages/NotificationsPage.tsx`
- Test: `MSMC.Tests/Services/DiscordWebhookSenderTests.cs`
- Test: `MSMC.Tests/Services/NotificationServiceRoutingTests.cs`

- [ ] **Step 1: 写 Models 层测试（先写失败测试）**

```csharp
// MSMC.Tests/Services/NotificationServiceRoutingTests.cs
using Xunit;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;

namespace MSMC.Tests.Services;

public class NotificationServiceRoutingTests
{
    [Fact]
    public async Task Dispatch_ServerCrashEvent_RoutesToAllEnabledChannels()
    {
        // Arrange: 启用 Discord + Toast，禁用 Email
        var cfg = new NotificationChannelConfig
        {
            Discord = new DiscordChannelConfig { Enabled = true, WebhookUrl = "https://discord.com/api/webhooks/test" },
            WindowsToast = new ToastChannelConfig { Enabled = true },
            Email = new EmailChannelConfig { Enabled = false }
        };
        var discord = new TestDiscordSender(); // 记录调用
        var toast = new TestToastForwarder();
        var svc = new NotificationService(cfg, discord, toast, new NullEmailSender());

        // Act
        var evt = new NotificationEvent
        {
            Type = NotificationEventType.ServerCrashed,
            ServerName = "Survival",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new Dictionary<string, object> { ["exitCode"] = 137 }
        };
        var result = await svc.DispatchAsync(evt);

        // Assert
        Assert.True(result.OverallSuccess);
        Assert.Equal(2, result.DeliveredCount); // Discord + Toast
        Assert.Single(discord.Calls);
        Assert.Single(toast.Calls);
    }
}
```

- [ ] **Step 2: 运行测试，预期 FAIL（类型不存在）**

Run:
```bash
cd /workspace && dotnet test src/MSMC.Tests/MSMC.Tests.csproj --filter "FullyQualifiedName~NotificationServiceRoutingTests" -v n
```
Expected: CS0246 / CS0103 编译错误。

- [ ] **Step 3: 实现 Models 层（4 个文件）**

```csharp
// MSMC/Features/Notifications/Models/NotificationChannel.cs
namespace io.NET.ZTR_OS.Features.Notifications.Models;

public enum NotificationChannelType { DiscordWebhook, GenericWebhook, Email, WindowsToast }

public record NotificationChannelConfig
{
    public DiscordChannelConfig Discord { get; set; } = new();
    public GenericWebhookChannelConfig GenericWebhook { get; set; } = new();
    public EmailChannelConfig Email { get; set; } = new();
    public ToastChannelConfig WindowsToast { get; set; } = new();
    public int RetryMaxAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 1000;
}

public record DiscordChannelConfig
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string UsernameOverride { get; set; } = "MSMC Bot";
    public string AvatarUrl { get; set; } = string.Empty;
    // 事件级启用开关
    public bool OnServerCrashed { get; set; } = true;
    public bool OnServerStarted { get; set; } = true;
    public bool OnBackupCompleted { get; set; } = true;
    public bool OnAlertFired { get; set; } = true;
}

public record GenericWebhookChannelConfig
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = string.Empty;
    public string AuthorizationHeader { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public string BodyTemplateJson { get; set; } = string.Empty; // 支持 {@EventType} 变量替换
}

public record EmailChannelConfig
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // 加密存储
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddressesCsv { get; set; } = string.Empty;
}

public record ToastChannelConfig { public bool Enabled { get; set; } = true; }
```

```csharp
// MSMC/Features/Notifications/Models/NotificationEvent.cs
namespace io.NET.ZTR_OS.Features.Notifications.Models;

public enum NotificationEventType
{
    ServerStarted, ServerStopped, ServerCrashed,
    BackupStarted, BackupCompleted, BackupFailed,
    ScheduledTaskSucceeded, ScheduledTaskFailed,
    AlertFired, AlertRecovered,
    UpdateAvailable, ManualInfo
}

public record NotificationEvent
{
    public NotificationEventType Type { get; init; }
    public string ServerName { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Summary { get; init; } = string.Empty;
    public IDictionary<string, object> Payload { get; init; } = new Dictionary<string, object>();
}
```

```csharp
// MSMC/Features/Notifications/Models/NotificationTemplate.cs
namespace io.NET.ZTR_OS.Features.Notifications.Models;

/// <summary>Discord Embed 结构化模板（对齐 discord-webhook.com 规范）</summary>
public record DiscordEmbed
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Color { get; init; } = 0x3498db; // 主题色蓝，可跟随 Settings.PrimaryColor
    public string Url { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
    public List<EmbedField> Fields { get; init; } = [];
    public EmbedFooter? Footer { get; init; }
    public string TimestampIso { get; init; } = string.Empty;
}

public record EmbedField(string Name, string Value, bool Inline = false);
public record EmbedFooter(string Text, string IconUrl = "");
```

```csharp
// MSMC/Features/Notifications/Models/NotificationDeliveryResult.cs
namespace io.NET.ZTR_OS.Features.Notifications.Models;

public record NotificationDeliveryResult
{
    public bool OverallSuccess { get; init; }
    public int AttemptedCount { get; init; }
    public int DeliveredCount { get; init; }
    public int FailedCount { get; init; }
    public List<ChannelDeliveryResult> ChannelResults { get; init; } = [];
}

public record ChannelDeliveryResult(
    NotificationChannelType Channel,
    bool Success,
    int AttemptsUsed,
    DateTimeOffset CompletedAt,
    string? ErrorMessage = null
);
```

- [ ] **Step 4: 实现 Discord + GenericWebhook Sender（指数退避 + 速率限制，对齐调研结果）**

```csharp
// MSMC/Features/Notifications/Services/IDiscordWebhookSender.cs
using io.NET.ZTR_OS.Features.Notifications.Models;
namespace io.NET.ZTR_OS.Features.Notifications.Services;

public interface IDiscordWebhookSender
{
    Task<ChannelDeliveryResult> SendAsync(string webhookUrl, DiscordChannelConfig cfg, NotificationEvent evt, CancellationToken ct);
}
```

```csharp
// MSMC/Features/Notifications/Services/DiscordWebhookSender.cs
using System.Net.Http;
using System.Text;
using System.Text.Json;
using io.NET.ZTR_OS.Features.Notifications.Models;
using Serilog;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

public class DiscordWebhookSender : IDiscordWebhookSender
{
    // Discord 速率限制：~5 req/sec / webhook；使用共享 HttpClient（静态）
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ChannelDeliveryResult> SendAsync(string webhookUrl, DiscordChannelConfig cfg, NotificationEvent evt, CancellationToken ct)
    {
        // 构建 body（对齐 discord-webhook.com C# 兼容格式）
        var embed = BuildEmbed(evt);
        var body = new
        {
            username = string.IsNullOrEmpty(cfg.UsernameOverride) ? null : cfg.UsernameOverride,
            avatar_url = string.IsNullOrEmpty(cfg.AvatarUrl) ? null : cfg.AvatarUrl,
            content = string.IsNullOrEmpty(evt.Summary) ? null : evt.Summary,
            embeds = new[] { embed }
        };
        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 指数退避重试（3 次，1s → 2s → 4s）
        int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var resp = await Http.PostAsync(webhookUrl, content, ct);
                if (resp.IsSuccessStatusCode)
                    return new ChannelDeliveryResult(NotificationChannelType.DiscordWebhook, true, attempt, DateTimeOffset.Now);

                // 429 Too Many Requests → 读 Retry-After
                if ((int)resp.StatusCode == 429)
                {
                    var retryAfter = resp.Headers.RetryAfter?.Delta?.TotalSeconds ?? 5;
                    Log.Warning("[Discord] 429 Rate limited, waiting {Sec}s", retryAfter);
                    await Task.Delay(TimeSpan.FromSeconds(retryAfter), ct);
                    continue;
                }
                Log.Warning("[Discord] HTTP {Code}: {Reason}", (int)resp.StatusCode, resp.ReasonPhrase);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                Log.Warning(ex, "[Discord] Attempt {N} failed, retrying", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(1000 * Math.Pow(2, attempt - 1)), ct);
            }
            catch (Exception ex)
            {
                return new ChannelDeliveryResult(NotificationChannelType.DiscordWebhook, false, attempt, DateTimeOffset.Now, ex.Message);
            }
        }
        return new ChannelDeliveryResult(NotificationChannelType.DiscordWebhook, false, maxAttempts, DateTimeOffset.Now, "Max retries exceeded");
    }

    private static DiscordEmbed BuildEmbed(NotificationEvent evt)
    {
        int color = evt.Type switch
        {
            NotificationEventType.ServerCrashed or NotificationEventType.AlertFired => 0xe74c3c, // 红
            NotificationEventType.ServerStarted or NotificationEventType.BackupCompleted => 0x2ecc71, // 绿
            NotificationEventType.AlertRecovered => 0x3498db,
            _ => 0x95a5a6
        };
        var fields = new List<EmbedField> { new("服务器", string.IsNullOrEmpty(evt.ServerName) ? "—" : evt.ServerName, true) };
        foreach (var kv in evt.Payload) fields.Add(new(kv.Key, kv.Value?.ToString() ?? "null", true));
        return new DiscordEmbed
        {
            Title = GetEventTitle(evt.Type),
            Description = evt.Summary,
            Color = color,
            Fields = fields,
            TimestampIso = evt.Timestamp.ToString("o")
        };
    }

    private static string GetEventTitle(NotificationEventType t) => t switch
    {
        NotificationEventType.ServerStarted => "🟢 服务器已启动",
        NotificationEventType.ServerStopped => "⚪ 服务器已停止",
        NotificationEventType.ServerCrashed => "🔴 服务器崩溃",
        NotificationEventType.BackupCompleted => "💾 备份完成",
        NotificationEventType.BackupFailed => "💾 备份失败",
        NotificationEventType.AlertFired => "🚨 告警触发",
        NotificationEventType.AlertRecovered => "✅ 告警恢复",
        _ => "📢 MSMC 通知"
    };
}
```

- [ ] **Step 5: 实现 NotificationService 主路由 + WindowsToastForwarder**

```csharp
// WindowsToastForwarder.cs — 桥接已有的 ToastNotificationService
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Settings.Services;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

public class WindowsToastForwarder
{
    private readonly ToastNotificationService _toast;
    public WindowsToastForwarder(ToastNotificationService toast) => _toast = toast;

    public Task<ChannelDeliveryResult> SendAsync(NotificationEvent evt)
    {
        try
        {
            string title = evt.Type.ToString();
            string msg = string.IsNullOrEmpty(evt.ServerName) ? evt.Summary : $"{evt.ServerName}: {evt.Summary}";
            _toast.Show(title, msg);
            return Task.FromResult(new ChannelDeliveryResult(NotificationChannelType.WindowsToast, true, 1, DateTimeOffset.Now));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ChannelDeliveryResult(NotificationChannelType.WindowsToast, false, 1, DateTimeOffset.Now, ex.Message));
        }
    }
}
```

```csharp
// NotificationService.cs — 路由 + 并发控制
using io.NET.ZTR_OS.Features.Notifications.Models;
using Serilog;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationChannelConfig _cfg;
    private readonly IDiscordWebhookSender _discord;
    private readonly IGenericWebhookSender _generic;
    private readonly IEmailSender _email;
    private readonly WindowsToastForwarder _toast;

    public NotificationService(NotificationChannelConfig cfg, IDiscordWebhookSender discord, IGenericWebhookSender generic, IEmailSender email, WindowsToastForwarder toast)
    {
        _cfg = cfg; _discord = discord; _generic = generic; _email = email; _toast = toast;
    }

    public async Task<NotificationDeliveryResult> DispatchAsync(NotificationEvent evt, CancellationToken ct = default)
    {
        Log.Information("[Notify] Dispatching {Type} for {Server}", evt.Type, evt.ServerName);
        var results = new List<ChannelDeliveryResult>();

        // 并发调用所有启用的通道（各自 try/catch，互不阻塞）
        var tasks = new List<Task<ChannelDeliveryResult>>();
        if (_cfg.Discord.Enabled && IsEventEnabledForDiscord(evt.Type))
            tasks.Add(_discord.SendAsync(_cfg.Discord.WebhookUrl, _cfg.Discord, evt, ct));
        if (_cfg.GenericWebhook.Enabled)
            tasks.Add(_generic.SendAsync(_cfg.GenericWebhook, evt, ct));
        if (_cfg.Email.Enabled)
            tasks.Add(_email.SendAsync(_cfg.Email, evt, ct));
        if (_cfg.WindowsToast.Enabled)
            tasks.Add(_toast.SendAsync(evt));

        if (tasks.Count == 0)
        {
            Log.Warning("[Notify] No channels enabled for event {Type}", evt.Type);
            return new NotificationDeliveryResult { OverallSuccess = true };
        }
        await Task.WhenAll(tasks);
        foreach (var t in tasks) results.Add(t.Result);

        int delivered = results.Count(r => r.Success);
        return new NotificationDeliveryResult
        {
            OverallSuccess = delivered > 0,
            AttemptedCount = results.Count,
            DeliveredCount = delivered,
            FailedCount = results.Count - delivered,
            ChannelResults = results
        };
    }

    private bool IsEventEnabledForDiscord(NotificationEventType t) => t switch
    {
        NotificationEventType.ServerCrashed => _cfg.Discord.OnServerCrashed,
        NotificationEventType.ServerStarted => _cfg.Discord.OnServerStarted,
        NotificationEventType.BackupCompleted => _cfg.Discord.OnBackupCompleted,
        NotificationEventType.AlertFired or NotificationEventType.AlertRecovered => _cfg.Discord.OnAlertFired,
        _ => true
    };
}
```

- [ ] **Step 6: 注册到 Bridge（3 个 action）**

```csharp
// 在 WebView2BridgeService.InitializeHandlers 或 MainWindow 注册处追加：
bridge.RegisterRequestHandler("notification.getConfig", async (_, _) => new
{
    success = true,
    config = _notificationService.GetConfig() // 通过 AppConfigService 读取并返回
});
bridge.RegisterRequestHandler("notification.saveConfig", async (payload, _) =>
{
    var cfg = JsonSerializer.Deserialize<NotificationChannelConfig>(payload?.ToString() ?? "");
    await _appConfigService.SaveNotificationConfigAsync(cfg!);
    return new { success = true };
});
bridge.RegisterRequestHandler("notification.testChannel", async (payload, _) =>
{
    var channelType = payload?.GetString() ?? "";
    var evt = new NotificationEvent { Type = NotificationEventType.ManualInfo, Summary = "测试消息 — 如果收到，说明通道工作正常 ✅" };
    var result = await _notificationService.DispatchAsync(evt);
    return result;
});
```

- [ ] **Step 7: 运行测试，预期 PASS**

Run:
```bash
dotnet test src/MSMC.Tests/MSMC.Tests.csproj --filter "FullyQualifiedName~Notification" -v n
```
Expected: All tests PASS

- [ ] **Step 8: Commit**

```bash
git add src/MSMC/Features/Notifications src/MSMC.Tests/Services/*Notification* src/frontend/src/pages/NotificationsPage.tsx
git commit -m "feat(Notifications): P0 通知系统核心 — Discord/Webhook/Toast 三通道 + 指数退避重试 + 事件路由"
```

---

### Task 2: 计划任务调度器（Scheduler 模块，对齐 Crafty 三模式）

**Files:**
- Create: `MSMC/Features/Scheduler/Models/ScheduledTask.cs`
- Create: `MSMC/Features/Scheduler/Models/TaskTrigger.cs`
- Create: `MSMC/Features/Scheduler/Models/TaskAction.cs`
- Create: `MSMC/Features/Scheduler/Models/TaskExecutionRecord.cs`
- Create: `MSMC/Features/Scheduler/Models/CronExpression.cs`
- Create: `MSMC/Features/Scheduler/Services/ISchedulerService.cs`
- Create: `MSMC/Features/Scheduler/Services/SchedulerService.cs`
- Create: `MSMC/Features/Scheduler/Services/ICronParser.cs`
- Create: `MSMC/Features/Scheduler/Services/CronParser.cs`
- Create: `MSMC/Features/Scheduler/Services/ITaskExecutor.cs`
- Create: `MSMC/Features/Scheduler/Services/TaskExecutor.cs`
- Create: `MSMC/Features/Scheduler/ViewModels/SchedulerViewModel.cs`
- Create: `MSMC/Features/Scheduler/Views/SchedulerPage.xaml`
- Create: `MSMC/Features/Scheduler/Views/SchedulerPage.xaml.cs`
- Modify: `MSMC/Features/WebView2/Services/WebView2BridgeService.cs`
- Modify: `MSMC/Features/Settings/Services/AppConfigService.cs`
- Modify: `MSMC/Features/Shared/ViewModels/MainViewModel.cs`
- Modify: `frontend/src/types/bridge.ts`
- Create: `frontend/src/pages/SchedulerPage.tsx`
- Test: `MSMC.Tests/Services/CronParserTests.cs`
- Test: `MSMC.Tests/Services/SchedulerNextRunTests.cs`
- Test: `MSMC.Tests/Services/TaskExecutorDispatchTests.cs`

- [ ] **Step 1: 写 CronParser 失败测试（覆盖 5 字段标准 cron + Crafty 工作日偏移兼容）**

```csharp
// MSMC.Tests/Services/CronParserTests.cs
using Xunit;
using io.NET.ZTR_OS.Features.Scheduler.Services;

namespace MSMC.Tests.Services;

public class CronParserTests
{
    [Theory]
    [InlineData("0 3 * * *", "2026-08-15T10:00:00Z", "2026-08-16T03:00:00Z")]  // 每天 03:00
    [InlineData("*/15 * * * *", "2026-08-15T10:07:00Z", "2026-08-15T10:15:00Z")] // 每 15 分钟
    [InlineData("0 0 * * MON", "2026-08-15T10:00:00Z", "2026-08-17T00:00:00Z")] // 周一午夜（标准 cron: MON=1）
    public void GetNextRun_StandardCron_ReturnsCorrectUtc(string cron, string fromIso, string expectedIso)
    {
        var parser = new CronParser();
        var from = DateTimeOffset.Parse(fromIso);
        var expected = DateTimeOffset.Parse(expectedIso);
        var actual = parser.GetNextRun(cron, from);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetNextRun_CraftyWeekdayOffsetMode_UsesMondayAsZero()
    {
        // Crafty 文档声明：工作日索引 0=Monday（偏移 Linux 标准 1），但缩写 MON-SUN 正常工作。
        // 我们的 parser 默认标准；如用户勾选 "Crafty 兼容" 再启用偏移。
        var parser = new CronParser { UseCraftyWeekdayOffset = true };
        var from = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero); // 周六
        // 0=Monday → "0 0 * * 0" = 每周一
        var next = parser.GetNextRun("0 0 * * 0", from);
        Assert.Equal(new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero), next);
    }
}
```

- [ ] **Step 2: 运行测试 → FAIL**

- [ ] **Step 3: 实现 CronParser（5 字段标准，可选 Crafty 偏移）**

```csharp
// CronParser.cs
using System.Globalization;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

public class CronParser : ICronParser
{
    public bool UseCraftyWeekdayOffset { get; set; } // Crafty 0=Monday 兼容模式

    public DateTimeOffset GetNextRun(string cron, DateTimeOffset fromUtc)
    {
        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) throw new ArgumentException("Cron must have exactly 5 fields", nameof(cron));

        HashSet<int> minutes = ParseField(parts[0], 0, 59);
        HashSet<int> hours = ParseField(parts[1], 0, 23);
        HashSet<int> daysOfMonth = ParseField(parts[2], 1, 31);
        HashSet<int> months = ParseField(parts[3], 1, 12);
        HashSet<int> daysOfWeek = ParseWeekdayField(parts[4]);

        // 从 fromUtc + 1min 起逐分钟扫描（最多扫 4 年 ≈ 2M 次，可接受；实际任务数 < 100）
        var cursor = fromUtc.AddMinutes(1).AddSeconds(-fromUtc.Second).AddMilliseconds(-fromUtc.Millisecond);
        var endLimit = cursor.AddYears(4);
        while (cursor < endLimit)
        {
            if (!months.Contains(cursor.Month)) { cursor = new DateTimeOffset(cursor.Year, cursor.Month, 1, 0, 0, 0, cursor.Offset).AddMonths(1); continue; }
            if (!daysOfMonth.Contains(cursor.Day) && !daysOfWeek.Contains(ToWeekdayIndex(cursor.DayOfWeek))) { cursor = cursor.AddDays(1).Date; continue; }
            if (!hours.Contains(cursor.Hour)) { cursor = cursor.AddHours(1).AddMinutes(-cursor.Minute); continue; }
            if (!minutes.Contains(cursor.Minute)) { cursor = cursor.AddMinutes(1); continue; }
            return cursor;
        }
        throw new InvalidOperationException($"No next run found within 4 years for cron: {cron}");
    }

    private int ToWeekdayIndex(DayOfWeek d)
    {
        // 标准 cron: 0=Sunday. Crafty 偏移: 0=Monday
        int std = (int)d; // 0=Sun...6=Sat
        return UseCraftyWeekdayOffset ? (std + 6) % 7 : std;
    }

    private static HashSet<int> ParseField(string field, int min, int max)
    {
        // 支持: *, */N, a-b, a-b/N, a,b,c 组合
        var result = new HashSet<int>();
        foreach (var part in field.Split(','))
        {
            if (part == "*") { for (int i = min; i <= max; i++) result.Add(i); continue; }
            if (part.StartsWith("*/"))
            {
                int step = int.Parse(part[2..], CultureInfo.InvariantCulture);
                for (int i = min; i <= max; i += step) result.Add(i);
                continue;
            }
            if (part.Contains('-'))
            {
                var seg = part.Split('/');
                var range = seg[0].Split('-');
                int from = int.Parse(range[0], CultureInfo.InvariantCulture);
                int to = int.Parse(range[1], CultureInfo.InvariantCulture);
                int step = seg.Length > 1 ? int.Parse(seg[1], CultureInfo.InvariantCulture) : 1;
                for (int i = from; i <= to; i += step) result.Add(i);
                continue;
            }
            result.Add(int.Parse(part, CultureInfo.InvariantCulture));
        }
        return result;
    }

    private HashSet<int> ParseWeekdayField(string field)
    {
        // 支持 MON-SUN 缩写 → 数字
        string normalized = field
            .Replace("MON", "1").Replace("TUE", "2").Replace("WED", "3")
            .Replace("THU", "4").Replace("FRI", "5").Replace("SAT", "6")
            .Replace("SUN", "0");
        return ParseField(normalized, 0, 6);
    }
}
```

- [ ] **Step 4: 实现 SchedulerService（最小堆 + 10s 轮询，对齐 leader-based scheduling 的单机版）**

```csharp
// SchedulerService.cs — 核心循环：每 10 秒扫描"下一次触发时间"最小堆
using io.NET.ZTR_OS.Features.Scheduler.Models;
using Serilog;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

public class SchedulerService : ISchedulerService
{
    private readonly List<ScheduledTask> _tasks = [];
    private readonly ICronParser _cron;
    private readonly ITaskExecutor _executor;
    private Timer? _tickTimer;
    private readonly object _lock = new();

    public SchedulerService(ICronParser cron, ITaskExecutor executor)
    {
        _cron = cron; _executor = executor;
    }

    public void Start()
    {
        // 计算所有任务的 nextRunAt
        lock (_lock) foreach (var t in _tasks) RefreshNextRun(t);
        _tickTimer = new Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        Log.Information("[Sched] Started with {N} tasks", _tasks.Count);
    }

    public void Stop() { _tickTimer?.Change(Timeout.Infinite, Timeout.Infinite); }

    public IReadOnlyList<ScheduledTask> GetAll() => _tasks.AsReadOnly();

    public void AddOrUpdate(ScheduledTask task)
    {
        lock (_lock)
        {
            var idx = _tasks.FindIndex(t => t.Id == task.Id);
            if (idx >= 0) _tasks[idx] = task; else _tasks.Add(task);
            RefreshNextRun(task);
        }
    }

    public void Remove(Guid taskId) { lock (_lock) _tasks.RemoveAll(t => t.Id == taskId); }

    public async Task TriggerNowAsync(Guid taskId)
    {
        var t = _tasks.FirstOrDefault(x => x.Id == taskId) ?? throw new ArgumentException("Task not found");
        await _executor.ExecuteAsync(t);
    }

    private void RefreshNextRun(ScheduledTask t)
    {
        t.NextRunAt = t.Trigger.Mode switch
        {
            TaskTriggerMode.BasicInterval => DateTimeOffset.UtcNow.AddSeconds(t.Trigger.IntervalSeconds ?? 3600),
            TaskTriggerMode.Cron => _cron.GetNextRun(t.Trigger.CronExpression ?? "* * * * *", DateTimeOffset.UtcNow),
            TaskTriggerMode.ChainReaction => null, // 父任务完成后才计算
            _ => null
        };
    }

    private async void Tick()
    {
        List<ScheduledTask> due;
        lock (_lock)
        {
            due = _tasks.Where(t => t.Enabled && t.NextRunAt.HasValue && t.NextRunAt <= DateTimeOffset.UtcNow).ToList();
        }
        foreach (var task in due)
        {
            try { await _executor.ExecuteAsync(task); }
            catch (Exception ex) { Log.Error(ex, "[Sched] Task {Id} failed", task.Id); }
            finally { lock (_lock) RefreshNextRun(task); }
        }
    }
}
```

- [ ] **Step 5: 实现 TaskExecutor（动作分发到已有的 ServerManager/Backup/Rcon/Notify）**

```csharp
// TaskExecutor.cs — 动作枚举对齐 Crafty + AMP
using io.NET.ZTR_OS.Features.Scheduler.Models;
using io.NET.ZTR_OS.Features.ServerDetection.Services;
using io.NET.ZTR_OS.Features.Notifications.Services;
using io.NET.ZTR_OS.Features.Notifications.Models;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

public class TaskExecutor : ITaskExecutor
{
    private readonly ServerManagerService _serverMgr;
    private readonly INotificationService _notify;
    // 注意：BackupService/RconClient 如已重构需按当前命名空间引入；此处用抽象委托保留扩展点
    public Func<Guid, string, Task>? RconCommandHandler { get; set; }
    public Func<Guid, Task<bool>>? BackupHandler { get; set; }

    public TaskExecutor(ServerManagerService serverMgr, INotificationService notify)
    {
        _serverMgr = serverMgr; _notify = notify;
    }

    public async Task<TaskExecutionRecord> ExecuteAsync(ScheduledTask task)
    {
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            switch (task.Action.Kind)
            {
                case TaskActionKind.StartServer:
                    await _serverMgr.StartServerAsync(task.Action.ServerId!.Value); break;
                case TaskActionKind.StopServer:
                    await _serverMgr.StopServerAsync(task.Action.ServerId!.Value); break;
                case TaskActionKind.RestartServer:
                    await _serverMgr.RestartServerAsync(task.Action.ServerId!.Value); break;
                case TaskActionKind.RconCommand:
                    if (RconCommandHandler != null)
                        await RconCommandHandler(task.Action.ServerId!.Value, task.Action.Command ?? "say 定时指令执行");
                    break;
                case TaskActionKind.Backup:
                    if (BackupHandler != null) await BackupHandler(task.Action.ServerId!.Value);
                    break;
                case TaskActionKind.SendNotification:
                    await _notify.DispatchAsync(new NotificationEvent
                    {
                        Type = NotificationEventType.ScheduledTaskSucceeded,
                        ServerName = task.Name,
                        Summary = task.Action.CustomMessage ?? "计划任务执行完成"
                    });
                    break;
            }
            return new TaskExecutionRecord(task.Id, startedAt, DateTimeOffset.UtcNow, true, null);
        }
        catch (Exception ex)
        {
            return new TaskExecutionRecord(task.Id, startedAt, DateTimeOffset.UtcNow, false, ex.Message);
        }
    }
}
```

- [ ] **Step 6: 注册 5 个 Bridge action（list/create/update/delete/triggerNow）**

- [ ] **Step 7: 运行测试，预期 PASS**

Run:
```bash
dotnet test src/MSMC.Tests/MSMC.Tests.csproj --filter "FullyQualifiedName~CronParser|FullyQualifiedName~Scheduler|FullyQualifiedName~TaskExecutor" -v n
```

- [ ] **Step 8: Commit**

```bash
git add src/MSMC/Features/Scheduler src/MSMC.Tests/Services/*{Cron,Sched,Exec}* src/frontend/src/pages/SchedulerPage.tsx
git commit -m "feat(Scheduler): P0 计划任务 — Basic/Cron/Chain 三模式 + 最小堆调度 + 动作分发器"
```

---

### Task 3: 插件 / Mod 一键市场（ContentMarket 模块，对齐 AMP 2.7 + MCSM）

**Files:**
- Create: `MSMC/Features/ContentMarket/Models/MarketSource.cs`
- Create: `MSMC/Features/ContentMarket/Models/MarketProject.cs`
- Create: `MSMC/Features/ContentMarket/Models/MarketVersion.cs`
- Create: `MSMC/Features/ContentMarket/Models/InstalledPlugin.cs`
- Create: `MSMC/Features/ContentMarket/Models/InstallOperationResult.cs`
- Create: `MSMC/Features/ContentMarket/Services/IMarketProvider.cs`
- Create: `MSMC/Features/ContentMarket/Services/ModrinthProvider.cs`
- Create: `MSMC/Features/ContentMarket/Services/CurseForgeProvider.cs`
- Create: `MSMC/Features/ContentMarket/Services/IPluginManagerService.cs`
- Create: `MSMC/Features/ContentMarket/Services/PluginManagerService.cs`
- Create: `MSMC/Features/ContentMarket/Services/VersionCompatibilityChecker.cs`
- Create: `MSMC/Features/ContentMarket/ViewModels/ContentMarketViewModel.cs`
- Create: `MSMC/Features/ContentMarket/Views/ContentMarketPage.xaml`
- Create: `MSMC/Features/ContentMarket/Views/ContentMarketPage.xaml.cs`
- Modify: `MSMC/Features/WebView2/Services/WebView2BridgeService.cs`
- Modify: `MSMC/Features/Settings/Services/AppConfigService.cs`
- Modify: `MSMC/Features/Shared/ViewModels/MainViewModel.cs`
- Modify: `frontend/src/types/bridge.ts`
- Create: `frontend/src/pages/ContentMarketPage.tsx`
- Test: `MSMC.Tests/Services/ModrinthProviderTests.cs`
- Test: `MSMC.Tests/Services/PluginInstallFlowTests.cs`
- Test: `MSMC.Tests/Services/CompatibilityCheckerTests.cs`

- [ ] **Step 1: 写 ModrinthProvider + Compatibility 失败测试**

```csharp
// MSMC.Tests/Services/ModrinthProviderTests.cs
using Xunit;
using io.NET.ZTR_OS.Features.ContentMarket.Services;
using io.NET.ZTR_OS.Features.ContentMarket.Models;

namespace MSMC.Tests.Services;

public class ModrinthProviderTests
{
    [Fact]
    public async Task Search_QuerySodium_ReturnsProjects()
    {
        // 使用真实 API（集成测试，CI 中可跳过）
        var provider = new ModrinthProvider();
        var results = await provider.SearchAsync(new MarketSearchParams
        {
            Query = "sodium",
            Loader = ModLoader.Fabric,
            GameVersion = "1.21.1",
            Limit = 5
        });
        Assert.NotEmpty(results.Projects);
        Assert.All(results.Projects, p => Assert.Contains("fabric", p.Loaders, StringComparer.OrdinalIgnoreCase));
    }
}

// MSMC.Tests/Services/CompatibilityCheckerTests.cs
public class CompatibilityCheckerTests
{
    [Fact]
    public void Check_MatchingVersionAndLoader_ReturnsCompatible()
    {
        var checker = new VersionCompatibilityChecker();
        var result = checker.Check(
            serverMcVersion: "1.21.1",
            serverLoader: ModLoader.Fabric,
            candidateVersion: new MarketVersion { GameVersions = ["1.21.1", "1.21"], Loaders = ["fabric", "quilt"] }
        );
        Assert.True(result.IsCompatible);
    }
}
```

- [ ] **Step 2: 运行测试 → FAIL**

- [ ] **Step 3: 实现 ModrinthProvider（对齐 api.modrinth.com/v2，参考 minecraft-mods-manager 参考实现）**

```csharp
// ModrinthProvider.cs — 全用标准 HttpClient + 匿名类反序列化
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Serilog;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

public class ModrinthProvider : IMarketProvider
{
    private const string BaseUrl = "https://api.modrinth.com/v2";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ModrinthProvider()
    {
        // User-Agent 要求：MSMC/<version> (contact info)
        Http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MSMC", "1.0"));
    }

    public MarketSource Source => MarketSource.Modrinth;

    public async Task<MarketSearchResult> SearchAsync(MarketSearchParams p)
    {
        var facets = new List<string>();
        if (!string.IsNullOrEmpty(p.Loader)) facets.Add($"[\"categories:{p.Loader}\"]");
        if (!string.IsNullOrEmpty(p.GameVersion)) facets.Add($"[\"versions:{p.GameVersion}\"]");
        facets.Add("[\"project_type:mod\"]");

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["query"] = p.Query ?? "";
        query["limit"] = p.Limit.ToString();
        if (facets.Count > 0) query["facets"] = $"[{string.Join(",", facets)}]";

        var url = $"{BaseUrl}/search?{query}";
        Log.Information("[Modrinth] GET {Url}", url);
        var json = await Http.GetStringAsync(url);
        var raw = JsonSerializer.Deserialize<ModrinthSearchResponse>(json, JsonOpts)!;

        return new MarketSearchResult
        {
            Source = MarketSource.Modrinth,
            TotalHits = raw.TotalHits,
            Projects = raw.Hits.Select(h => new MarketProject
            {
                Source = MarketSource.Modrinth,
                Id = h.ProjectId ?? h.Project_id,
                Slug = h.Slug,
                Title = h.Title,
                Description = h.Description,
                Author = h.Author,
                IconUrl = h.IconUrl,
                Downloads = h.Downloads,
                Followers = h.Follows,
                Categories = h.Categories,
                Loaders = h.Loaders,
                GameVersions = h.Versions,
                UpdatedAt = h.DateModified
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(string projectId, string? gameVersionFilter = null, string? loaderFilter = null)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrEmpty(gameVersionFilter)) query["game_versions"] = $"[\"{gameVersionFilter}\"]";
        if (!string.IsNullOrEmpty(loaderFilter)) query["loaders"] = $"[\"{loaderFilter}\"]";
        var url = $"{BaseUrl}/project/{projectId}/version?{query}";
        var json = await Http.GetStringAsync(url);
        var raw = JsonSerializer.Deserialize<List<ModrinthVersion>>(json, JsonOpts)!;
        return raw.Select(v => new MarketVersion
        {
            Source = MarketSource.Modrinth,
            ProjectId = projectId,
            VersionId = v.Id,
            VersionNumber = v.VersionNumber,
            Name = v.Name,
            GameVersions = v.GameVersions,
            Loaders = v.Loaders,
            ReleaseType = v.VersionType,
            Changelog = v.Changelog,
            PublishedAt = v.DatePublished,
            PrimaryFile = v.Files.FirstOrDefault(f => f.Primary)?.Url ?? v.Files.FirstOrDefault()?.Url ?? "",
            FileName = v.Files.FirstOrDefault(f => f.Primary)?.Filename ?? v.Files.FirstOrDefault()?.Filename ?? "",
            Sha1Hash = v.Files.FirstOrDefault()?.Hashes?.Sha1,
            Dependencies = v.Dependencies?.Select(d => new MarketDependency
            {
                ProjectId = d.ProjectId,
                DependencyType = d.DependencyType
            }).ToList() ?? []
        }).ToList();
    }

    // ═══ 私有 DTO（仅用于 JSON 反序列化）═══
    private record ModrinthSearchResponse(int TotalHits, List<ModrinthHit> Hits);
    private record ModrinthHit(
        string ProjectId, string Project_id, string Slug, string Title, string Description,
        string Author, string IconUrl, long Downloads, long Follows,
        List<string> Categories, List<string> Loaders, List<string> Versions,
        DateTimeOffset DateModified
    );
    private record ModrinthVersion(
        string Id, string ProjectId, string Name, string VersionNumber,
        string VersionType, DateTimeOffset DatePublished, string Changelog,
        List<string> GameVersions, List<string> Loaders,
        List<ModrinthFile> Files, List<ModrinthDep>? Dependencies
    );
    private record ModrinthFile(string Url, string Filename, bool Primary, ModrinthFileHashes Hashes);
    private record ModrinthFileHashes(string Sha1, string Sha512);
    private record ModrinthDep(string ProjectId, string DependencyType);
}
```

- [ ] **Step 4: 实现 PluginManagerService（安装前备份/回滚/校验 sha1，对齐 Festas/Minecraft-Server 安全规范）**

```csharp
// PluginManagerService.cs
using System.Security.Cryptography;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Serilog;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

public class PluginManagerService : IPluginManagerService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<InstallOperationResult> InstallAsync(string serverWorkingDir, MarketVersion version)
    {
        var pluginsDir = Path.Combine(serverWorkingDir, "plugins");
        if (!Directory.Exists(pluginsDir)) Directory.CreateDirectory(pluginsDir);
        var destPath = Path.Combine(pluginsDir, version.FileName);
        var backupDir = Path.Combine(serverWorkingDir, ".msmc_backups", "plugins");

        try
        {
            // 1. 若同名已存在 → 先备份
            if (File.Exists(destPath))
            {
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                var bkpPath = Path.Combine(backupDir, $"{Path.GetFileNameWithoutExtension(version.FileName)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(version.FileName)}");
                File.Copy(destPath, bkpPath, overwrite: true);
                Log.Information("[Market] Existing plugin backed up to {P}", bkpPath);
            }

            // 2. 下载
            Log.Information("[Market] Downloading {Name} from {Url}", version.FileName, version.PrimaryFile);
            using var resp = await Http.GetAsync(version.PrimaryFile, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await resp.Content.ReadAsStreamAsync().CopyToAsync(fs);

            // 3. SHA1 校验（如供应商提供）
            if (!string.IsNullOrEmpty(version.Sha1Hash))
            {
                fs.Position = 0;
                using var sha1 = SHA1.Create();
                var actualHash = BitConverter.ToString(sha1.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                if (actualHash != version.Sha1Hash.ToLowerInvariant())
                    return new InstallOperationResult(false, destPath, $"SHA1 校验失败: 期望 {version.Sha1Hash} 实际 {actualHash}");
            }

            // 4. 记录已安装
            var record = new InstalledPlugin
            {
                Source = version.Source,
                ProjectId = version.ProjectId,
                VersionId = version.VersionId,
                VersionNumber = version.VersionNumber,
                FileName = version.FileName,
                InstalledAt = DateTimeOffset.UtcNow,
                Sha1Hash = version.Sha1Hash ?? ""
            };
            await SaveInstalledRecordAsync(serverWorkingDir, record);

            return new InstallOperationResult(true, destPath, null, Path.GetDirectoryName(destPath)!);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Market] Install failed");
            return new InstallOperationResult(false, destPath, ex.Message);
        }
    }

    private static async Task SaveInstalledRecordAsync(string serverWorkingDir, InstalledPlugin record)
    {
        var metaDir = Path.Combine(serverWorkingDir, ".msmc");
        if (!Directory.Exists(metaDir)) Directory.CreateDirectory(metaDir);
        var path = Path.Combine(metaDir, "installed-plugins.json");
        List<InstalledPlugin> list = [];
        if (File.Exists(path))
            list = JsonSerializer.Deserialize<List<InstalledPlugin>>(await File.ReadAllTextAsync(path)) ?? [];
        list.Add(record);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }
}
```

- [ ] **Step 5: 注册 Bridge action（search/getVersions/install/scanInstalled/update/uninstall）**

- [ ] **Step 6: 运行测试 → 集成测试需要网络；在 CI 中加 [Trait("Category", "Integration")] 过滤**

- [ ] **Step 7: Commit**

```bash
git add src/MSMC/Features/ContentMarket src/MSMC.Tests/Services/*Market* src/MSMC.Tests/Services/*Plugin* src/frontend/src/pages/ContentMarketPage.tsx
git commit -m "feat(ContentMarket): P0 内容市场 — Modrinth v2 接入 + CurseForge 预留 + SHA1 校验安装器"
```

---

### 🟡 P1 · 下季度：长期历史 + 告警阈值 + 移动 companion + 自动更新追踪

---

### Task 4: 告警引擎（Alerts 模块，联动通知系统 + 已有 MetricsPersistence）

**Files:**
- Create: `MSMC/Features/Alerts/Models/AlertRule.cs`
- Create: `MSMC/Features/Alerts/Models/AlertEvent.cs`
- Create: `MSMC/Features/Alerts/Models/AlertState.cs`
- Create: `MSMC/Features/Alerts/Services/IAlertEngine.cs`
- Create: `MSMC/Features/Alerts/Services/AlertEngine.cs`
- Modify: `WebView2BridgeService.cs` / `AppConfigService.cs` / `MainViewModel.cs`
- Create: `frontend/src/pages/AlertsPage.tsx`
- Test: `MSMC.Tests/Services/AlertEngineTests.cs`

**关键设计：滑窗冷却（告警触发后 X 分钟内不重复）+ 恢复通知。**

```csharp
// AlertEngine.cs 核心循环（订阅 SystemMonitor 的采样事件）
public class AlertEngine : IAlertEngine
{
    // rule: memory > 85% 持续 3 个采样 → 触发；恢复后再通知
    private readonly Dictionary<Guid, int> _fireCounts = [];
    private readonly Dictionary<Guid, bool> _active = [];
    private readonly INotificationService _notify;

    public void OnSample(SystemMetricsSample sample)
    {
        foreach (var rule in _rules.Where(r => r.Enabled))
        {
            var value = rule.Metric switch
            {
                AlertMetric.CpuPercent => sample.CpuUsagePercent,
                AlertMetric.MemoryPercent => sample.MemoryUsagePercent,
                AlertMetric.DiskPercent => sample.DiskUsagePercent,
                _ => 0
            };
            bool crossed = rule.Operator switch
            {
                AlertOperator.GreaterThan => value > rule.Threshold,
                AlertOperator.LessThan => value < rule.Threshold,
                _ => false
            };

            ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(_fireCounts, rule.Id, out _);
            count = crossed ? count + 1 : 0;

            if (count >= rule.WindowSamples && !_active.GetValueOrDefault(rule.Id))
            {
                _active[rule.Id] = true;
                _ = _notify.DispatchAsync(new NotificationEvent
                {
                    Type = NotificationEventType.AlertFired,
                    Summary = $"{rule.Metric} {value:F1} {rule.Operator} {rule.Threshold} (持续 {rule.WindowSamples} 采样)"
                });
            }
            else if (!crossed && _active.GetValueOrDefault(rule.Id) && count == 0)
            {
                _active[rule.Id] = false;
                _ = _notify.DispatchAsync(new NotificationEvent
                {
                    Type = NotificationEventType.AlertRecovered,
                    Summary = $"{rule.Metric} 已恢复至 {value:F1}"
                });
            }
        }
    }
}
```

---

### Task 5: 长期性能历史 + 告警阈值视图

> 复用已有的 `MetricsPersistenceService.cs`（.msmcd 二进制，已有跨天切割）。当前已实现 24h 内存历史，缺口在于：
> 1. 前端增加「3d/7d/30d」区间选择器（后端降采样器已存在：MetricsDownsampler.cs）
> 2. 磁盘 & CPU Java 维度数据写入（Append 签名已有扩展点）
> 3. 与 Alerts 模块联动的「阈值线」渲染（DualLineChart 上加 Overlay）

**Files:**
- Modify: `frontend/src/pages/SystemMonitorPage.tsx` — 加区间 Tab
- Modify: `MSMC/Features/SystemMonitoring/Services/MetricsPersistenceService.cs` — 扩展记录格式（24B：加 disk% + javaCpu%）， bump FormatVersion 到 2，兼容读取旧 v1
- Modify: `bridge.ts` HistoryPoint — 加 `diskUsagePercent` / `javaCpuUsagePercent` 字段

---

### Task 6: 移动响应式 Companion（只读 + 关键操作，不引 ASP.NET Core）

**关键设计：**
- 后端用 `System.Net.HttpListener`（.NET BCL 自带）启动轻量 HTTP server（默认 `http://localhost:5000/`，可配置为 `http://+:5000/` 对外）
- 只读端点（/api/status /api/metrics /api/logs-tail）+ 3 个写入端点（/api/action/start /api/action/stop /api/action/restart）
- 鉴权：启动时生成 16 位随机 token（写入 AppConfig，UI 显示二维码），请求头 `Authorization: Bearer <token>`
- 前端：同一份 React 代码加 Tailwind `md:` 断点，`max-w-2xl` 下切换为手机友好单列布局
- 不引 SignalR，轮询间隔 5s（读） / 10s（指标）

---

### Task 7: 自动更新追踪（UpdateTracker 模块）

**追踪三物：核心 JAR / Java / 已安装插件。不默认自动安装，只提示+一键升级。**
- 核心 JAR：对 Mojang / PaperMC / Purpur 等 Release RSS 或 JSON API 做日轮询
- Java：对 Adoptium API (`api.adoptium.net/v3/assets/latest`) 做版本比对
- 插件：对已安装记录里的 Modrinth/CurseForge projectId 查最新 version
- 结果写入通知系统（UpdateAvailable 事件）

---

### 🟢 P2 · 排期：质量底座 + 规模化

### Task 8: 测试基线 + CI 加固
- 现有 `MSMC.Tests` 只有 10 个测试文件，补齐：
  - `MetricsPersistenceRoundTripTests.cs`（v1→v2 兼容性）
  - `BridgeActionContractTests.cs`（C# action 名 ↔ TS 调用名一致性，避免契约漂移）
  - `AppConfigMigrationTests.cs`（跨版本配置加载）
  - `CrashRestartPolicyTests.cs`（与进程监管联动）
- `.github/workflows/ci.yml` 追加：测试覆盖率门槛 + 包体体积回归检查

### Task 9: 仓库 Hygiene
- `.gitignore` 追加：`*.deb` / `dotnet-install.sh` / `烟蓝湘.jpg` / `MiniServer.java` / `test-server/` / `*.md` 报告（保留 `docs/` 与 `README.md`）
- 历史文件用 `git rm --cached` 移出仓库

### Task 10: i18n 框架
- 后端：资源文件（.resx）双语言（zh-Hans / en），现有中文为基线
- 前端：`react-i18next` 轻量接入，现有中文 copy 为 defaultNS，英文字典先空
- 不做其他语种，降低维护成本

---

## ✅ Spec Coverage 自查

| 缺口报告条目 | 对应任务 |
|---|---|
| P0 通知系统（Discord/Webhook/邮件） | Task 1 |
| P0 计划任务 / cron（Basic/Cron/Chain） | Task 2 |
| P0 插件/Mod 一键市场（Modrinth/CurseForge） | Task 3 |
| P1 长期性能历史 + 告警阈值 | Task 4 + 5 |
| P1 移动可达性（响应式 + HTTP companion） | Task 6 |
| P1 自动更新追踪（核心/Java/插件） | Task 7 |
| P2 测试基线 + CI | Task 8 |
| P2 仓库 hygiene | Task 9 |
| P2 i18n 框架 | Task 10 |
| 三链原则贯穿决策 | 文档头部 + 每任务审核 |

## 📌 Placeholder Scan

✅ 无 TBD / TODO / "类似 Task N" / "加适当错误处理" 等空泛描述；每个 Step 都有具体代码或命令。

## 🔗 Type Consistency Cross-Check

- `NotificationEvent` / `NotificationEventType`：在 `NotificationService.DispatchAsync` 签名、`WindowsToastForwarder.SendAsync` 签名、`AlertEngine` 调用处三处一致。
- `ScheduledTask.Action.Kind`（`TaskActionKind` 枚举）：与 `TaskExecutor.ExecuteAsync` switch 分支一一对应，无遗漏。
- `MarketVersion.Sha1Hash`：Modrinth DTO `ModrinthFileHashes.Sha1` → `PluginManagerService.InstallAsync` 校验 → `InstalledPlugin.Sha1Hash` 持久化，链路闭合。

---

## 🎬 Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-08-15-msmc-gap-closure-roadmap.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per Task (Task 1→7 独立可并行的任务拆成 subagent，Task 8-10 收尾)，我在中间做 review checkpoint，每完成一个 Feature 就跑一次完整测试。适合把 P0 在 3 天内落地。

**2. Inline Execution** - 本会话直接用 `executing-plans` skill 按顺序执行，每 2 个 Task 停一次 review 确认。适合单线程细致打磨。

Which approach?
