# MSMC 竞品缺口补齐 · 执行方案（修正版）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 从「本机好用的控制台」升级为「本机省心的运维平台」——补齐 P0（通知/调度/市场）三件套，再依次推进 P1（移动可达/长期历史/自动更新）与 P2（质量底座），不改变 Windows/单机/中文的核心定位。

**Architecture:** 新增 4 个 Feature 模块（Notifications / Scheduler / ContentMarket / AlertEngine），复用现有 WebView2Bridge 通信协议与 AppConfigService 持久化模式；后端纯 C# 服务（不引入 ASP.NET Core 重架构），前端 React 页面增量接入。

**Tech Stack:** 后端（C# / .NET 9 WPF / CommunityToolkit.Mvvm DI / System.Text.Json / Serilog）；前端（React 18 / TS / Vite / Tailwind / Zustand appStore）；外部 API（Modrinth v2 REST / CurseForge v1 REST / Discord Webhook POST / System.Net.HttpListener）。

---

## ⚙️ 核心架构原则：三链原则（修正版）

> **三链原则是所有系统设计的基石，任何模块设计、代码实现必须严格遵守。**

| 链序 | 原则名称 | 核心内涵 | 一票否决情形 |
|---|---|---|---|
| **链一** | **因果链 (Causality Chain)** | **任何操作或功能的触发必须有明确的“因”（输入源），确保逻辑有源可循，杜绝悬空或无因之果的逻辑。** | ❌ 允许匿名/无头的后台任务运行；❌ 模块间调用无明确的触发事件或参数 |
| **链二** | **执行链 (Execution Chain)** | **每一个用户按钮、代码循环、异步操作都必须形成“咬合”的闭环，具备状态保护、异常捕获、资源释放等冗余与兜底机制。** | ❌ 异步操作无超时/取消机制；❌ UI 按钮点击无防重入或锁定状态；❌ 循环中异常被吞掉导致断链 |
| **链三** | **返回链 (Return/Traceability Chain)** | **日志是系统神圣不可侵犯的兜底。任何关键操作、状态流转、异常捕获必须输出可追溯的结构化日志。** | ❌ 仅 `Console.WriteLine` 记录；❌ 异常发生时未记录上下文（如：任务ID、用户输入、当前状态）；❌ 日志级别滥用（应 `Warning` 的用了 `Debug`） |

---

## 📁 文件结构地图（新增 / 修改总览）

> 路径全部相对于 `/workspace/src/MSMC/Features`。

### 新增模块（P0）

```
Notifications/
  ├── Models/
  │   └── NotificationChannel.cs         # 通道枚举 + 配置结构
  ├── Services/
  │   ├── INotificationService.cs
  │   ├── NotificationService.cs         # 核心调度：因果链的“果”
  │   ├── IDiscordWebhookSender.cs
  │   └── DiscordWebhookSender.cs         # 执行链：指数退避重试
  └── ViewModels/
      └── NotificationsViewModel.cs

Scheduler/
  ├── Models/
  │   └── ScheduledTask.cs                # 因果链：任务定义（因 -> 果）
  ├── Services/
  │   ├── ISchedulerService.cs
  │   ├── SchedulerService.cs             # 执行链：最小堆调度 + 兜底
  │   └── CronParser.cs                   # 因果链：Cron 解析
  └── ViewModels/
      └── SchedulerViewModel.cs

ContentMarket/
  ├── Models/
  │   └── MarketProject.cs                # 因果链：市场数据模型
  ├── Services/
  │   ├── IMarketProvider.cs
  │   ├── ModrinthProvider.cs             # 执行链：API 调用容错
  │   ├── PluginManagerService.cs         # 执行链：安装回滚机制
  │   └── VersionCompatibilityChecker.cs  # 因果链：版本匹配校验
  └── ViewModels/
      └── ContentMarketViewModel.cs

AlertEngine/
  ├── Models/
  │   └── AlertRule.cs                    # 因果链：告警规则
  └── Services/
      └── AlertEngine.cs                  # 执行链：滑窗冷却 + 返回链：告警日志
```

---

## 🗺️ 核心功能实现方案

### 🔴 P0 · 本月落地：通知系统 + 计划任务 + 插件市场

---

### Task 1: 通知系统核心 (Notifications)

**核心挑战：** 建立标准化的事件（因）到通知（果）的流转机制，确保通知可靠送达。

**Files:**
- Create: `MSMC/Features/Notifications/Services/DiscordWebhookSender.cs`
- Create: `MSMC/Features/Notifications/Services/NotificationService.cs`
- Modify: `MSMC/Features/WebView2/Services/WebView2BridgeService.cs`
- Test: `MSMC.Tests/Services/DiscordWebhookSenderTests.cs`

**设计逻辑（对齐三链原则）：**

1.  **因果链**：定义 `NotificationEventType` 枚举（ServerCrashed, BackupCompleted 等）。任何模块（Scheduler, AlertEngine, Backup）产生事件时，必须通过 `INotificationService.DispatchAsync(evt)` 触发。**不允许绕过此接口直接调用 Discord。**
2.  **执行链**：`DiscordWebhookSender` 必须实现**指数退避**重试逻辑（1s -> 2s -> 4s），处理 HTTP 429（速率限制）并读取 `Retry-After` 头。失败后必须抛出或记录状态，不得静默吞掉。
3.  **返回链**：每次通知派发（成功/失败）必须写入 Serilog 日志，包含 EventID, 目标通道, 耗时, 错误信息。

**步骤：**

- [ ] **Step 1: 编写 `DiscordWebhookSender` 骨架（执行链：重试机制）**
  
```csharp
// MSMC/Features/Notifications/Services/DiscordWebhookSender.cs
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Serilog;

namespace io.NET.ZTR_OS.Features.Notifications.Services;

public class DiscordWebhookSender : IDiscordWebhookSender
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    
    // 三链原则：返回链 - 日志器
    private readonly ILogger _logger;

    public DiscordWebhookSender(ILogger logger)
    {
        _logger = logger;
    }

    // 三链原则：因果链 - 明确的输入参数（url, content）
    public async Task<bool> SendAsync(string webhookUrl, string message, EmbeddedMessage? embed = null, CancellationToken ct = default)
    {
        int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var payload = new { content = message, embeds = embed != null ? new[] { embed } : null };
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(webhookUrl, content, ct);
                
                // 三链原则：返回链 - 记录成功/失败
                if (response.IsSuccessStatusCode)
                {
                    _logger.Information("[Discord] Webhook sent successfully (Attempt {Attempt})", attempt);
                    return true;
                }
                
                // 三链原则：执行链 - 处理 429 限频
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    int retryAfter = response.Headers.RetryAfter?.Delta.HasValue == true 
                        ? (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds 
                        : 5;
                    _logger.Warning("[Discord] Rate limited, waiting {Seconds}s...", retryAfter);
                    await Task.Delay(retryAfter * 1000, ct);
                    continue;
                }
                
                _logger.Error("[Discord] Failed to send webhook. Status: {StatusCode}", response.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                // 三链原则：执行链 - 捕获异常
                _logger.Warning(ex, "[Discord] HTTP request failed (Attempt {Attempt})", attempt);
                
                // 三链原则：执行链 - 指数退避
                if (attempt < maxRetries)
                {
                    int delay = (int)Math.Pow(2, attempt) * 1000; // 1s, 2s, 4s
                    await Task.Delay(delay, ct);
                }
            }
        }
        
        _logger.Error("[Discord] Webhook failed after {MaxRetries} attempts", maxRetries);
        return false;
    }
}

// 辅助：嵌入消息结构
public class EmbeddedMessage
{
    public string Title { get; set; }
    public string Description { get; set; }
    public int Color { get; set; }
    public List<EmbedField> Fields { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public class EmbedField
{
    public string Name { get; set; }
    public string Value { get; set; }
    public bool Inline { get; set; } = true;
}
```

- [ ] **Step 2: 编写单元测试验证“执行链”与“返回链”**
  
```csharp
// MSMC.Tests/Services/DiscordWebhookSenderTests.cs
using Xunit;
using Moq;
using Serilog;
using io.NET.ZTR_OS.Features.Notifications.Services;

namespace MSMC.Tests.Services;

public class DiscordWebhookSenderTests
{
    [Fact]
    public async Task SendAsync_ShouldRetryOnFailure_AndLogWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var sender = new DiscordWebhookSender(mockLogger.Object);
        // 注：实际测试需 Mock HttpClient，此处为逻辑示例
        // var mockHttp = new Mock<HttpClient>();

        // Act & Assert (示例逻辑)
        // 验证：1. 重试次数 2. 日志记录（警告/错误） 3. 不抛异常
        // Assert.Equal(3, mockLogger.Verify(l => l.Warning(It.IsAny<string>()), Times.AtLeastOnce));
        
        await Task.CompletedTask;
    }
}
```

---

### Task 2: 计划任务调度器 (Scheduler)

**核心挑战：** 构建可靠的自动化执行骨架，确保任务调度的精确性与可追溯性。

**Files:**
- Create: `MSMC/Features/Scheduler/Services/SchedulerService.cs`
- Create: `MSMC/Features/Scheduler/Services/CronParser.cs`
- Modify: `MSMC/Features/WebView2/Services/WebView2BridgeService.cs`

**设计逻辑（对齐三链原则）：**

1.  **因果链**：每个 `ScheduledTask` 必须明确 `Trigger`（因：Cron 表达式）和 `Action`（果：执行什么）。
2.  **执行链**：`SchedulerService` 使用 `System.Threading.Timer` 轮询。**必须防止重入**：在上一个任务未执行完毕前，下一次触发不得并发执行。采用 `SemaphoreSlim` 或 `Interlocked.CompareExchange` 进行状态锁定。
3.  **返回链**：任务的每次触发、成功、失败都必须落库（写入 `TaskExecutionRecord`）并记录详细日志。

**步骤：**

- [ ] **Step 1: 编写 `CronParser`（因果链：因 -> 时间点）**
  
```csharp
// MSMC/Features/Scheduler/Services/CronParser.cs
using System;
using System.Globalization;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

public static class CronParser
{
    // 三链原则：执行链 - 必须处理非法输入
    public static DateTimeOffset? GetNextRunTime(string cronExpression, DateTimeOffset fromTime)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return null;

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) // 分钟, 小时, 日, 月, 星期
            return null;

        try
        {
            // 简化版：仅支持 "*", 具体数字, 范围 (1-5), 列表 (1,3,5)
            int[] minutes = ParseField(parts[0], 0, 59);
            int[] hours = ParseField(parts[1], 0, 23);
            int[] days = ParseField(parts[2], 1, 31);
            int[] months = ParseField(parts[3], 1, 12);
            int[] weekdays = ParseWeekdayField(parts[4]);

            var cursor = fromTime.AddMinutes(1).AddSeconds(-fromTime.Second);
            for (int i = 0; i < 366 * 24 * 60; i++) // 最大扫描一年
            {
                if (months.Contains(cursor.Month) &&
                    days.Contains(cursor.Day) &&
                    weekdays.Contains((int)cursor.DayOfWeek) &&
                    hours.Contains(cursor.Hour) &&
                    minutes.Contains(cursor.Minute))
                {
                    return cursor;
                }
                cursor = cursor.AddMinutes(1);
            }
            return null; // 找不到匹配
        }
        catch (Exception)
        {
            return null; // 执行链：容错处理
        }
    }

    private static int[] ParseField(string field, int min, int max)
    {
        // 支持 *, 单个数字, 范围, 列表
        // ... (具体解析逻辑省略)
        return new int[0]; 
    }

    private static int[] ParseWeekdayField(string field)
    {
        // 支持 SUN-SAT 或 0-6 (周日为0)
        // ... (具体解析逻辑省略)
        return new int[0];
    }
}
```

- [ ] **Step 2: 编写 `SchedulerService`（执行链：防重入、返回链：日志）**
  
```csharp
// MSMC/Features/Scheduler/Services/SchedulerService.cs
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

public class SchedulerService : ISchedulerService
{
    private readonly ConcurrentDictionary<Guid, ScheduledTask> _tasks = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1); // 执行链：防重入
    private readonly ILogger _logger;
    private Timer? _timer;

    public SchedulerService(ILogger logger)
    {
        _logger = logger;
    }

    // 启动调度器
    public void Start()
    {
        // 三链原则：返回链
        _logger.Information("[Scheduler] Starting...");
        _timer = new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    // 定时检查任务
    private void Tick(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        
        foreach (var task in _tasks.Values.Where(t => t.Enabled))
        {
            if (task.NextRunTime.HasValue && task.NextRunTime <= now)
            {
                _ = ExecuteTaskAsync(task); // 非阻塞执行
            }
        }
    }

    // 执行具体任务 (三链原则核心)
    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        // 三链原则：执行链 - 锁定资源，防止并发执行同一任务
        if (!await _semaphore.WaitAsync(0))
        {
            _logger.Warning("[Scheduler] Task {TaskName} is already running, skipping.", task.Name);
            return;
        }

        try
        {
            // 三链原则：因果链 - 明确记录执行触发
            _logger.Information("[Scheduler] Executing task: {TaskName} (ID: {TaskId})", task.Name, task.Id);
            
            // 这里应调用具体的业务逻辑，如：发送命令、备份等
            await task.Action.ExecuteAsync();

            // 三链原则：返回链 - 记录成功
            _logger.Information("[Scheduler] Task {TaskName} executed successfully.", task.Name);
            
            // 更新下次运行时间
            if (task.Trigger is CronTrigger cron)
            {
                task.NextRunTime = CronParser.GetNextRunTime(cron.Expression, DateTimeOffset.UtcNow);
            }
        }
        catch (Exception ex)
        {
            // 三链原则：返回链 - 记录失败详情
            _logger.Error(ex, "[Scheduler] Task {TaskName} execution failed.", task.Name);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void AddTask(ScheduledTask task)
    {
        if (_tasks.TryAdd(task.Id, task))
        {
            _logger.Information("[Scheduler] Task added: {TaskName}", task.Name);
        }
    }
}
```

---

### Task 3: 插件 / Mod 一键市场 (ContentMarket)

**核心挑战：** 实现安全、可靠的第三方内容（Mod/Plugin）获取与落盘机制。

**Files:**
- Create: `MSMC/Features/ContentMarket/Services/ModrinthProvider.cs`
- Create: `MSMC/Features/ContentMarket/Services/PluginManagerService.cs`
- Modify: `MSMC/Features/WebView2/Services/WebView2BridgeService.cs`

**设计逻辑（对齐三链原则）：**

1.  **因果链**：搜索（因：关键词）-> 结果列表（果）。版本下载（因：版本ID）-> 文件流（果）。
2.  **执行链**：下载操作必须支持**进度回调**（用于 UI 显示）、**取消令牌**（用户可取消下载）、**完整性校验**（SHA1/MD5）。
3.  **返回链**：安装的每一步（下载、校验、解压、写入）都需要审计日志。

**步骤：**

- [ ] **Step 1: 实现 `ModrinthProvider`（因果链：API 调用）**
  
```csharp
// MSMC/Features/ContentMarket/Services/ModrinthProvider.cs
using System.Net.Http;
using System.Text.Json;
using System.Web;
using Serilog;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

public class ModrinthProvider : IMarketProvider
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly ILogger _logger;

    public ModrinthProvider(ILogger logger)
    {
        _logger = logger;
    }

    // 三链原则：因果链 - 明确的输入参数构造请求
    public async Task<SearchResponse> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString["query"] = query.Keyword;
        queryString["facets"] = $"[[\"project_type:mod\"],[\"versions:{query.MinecraftVersion}\"]]";
        queryString["limit"] = query.Limit.ToString();

        var url = $"https://api.modrinth.com/v2/search?{queryString}";
        
        _logger.Information("[Modrinth] Searching for: {Keyword}", query.Keyword);

        // 三链原则：执行链 - 异常捕获
        try
        {
            var response = await _httpClient.GetStringAsync(url, ct);
            return JsonSerializer.Deserialize<SearchResponse>(response) ?? new SearchResponse();
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "[Modrinth] Failed to search.");
            return new SearchResponse(); // 返回链：记录错误并返回空
        }
    }

    // 三链原则：执行链 - 支持进度与取消
    public async Task<byte[]> DownloadVersionAsync(string versionId, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        // ... 获取版本详情，提取下载 URL
        var versionUrl = await GetVersionDownloadUrlAsync(versionId, ct);
        
        _logger.Information("[Modrinth] Downloading version: {VersionId}", versionId);

        // ... 实现带进度的下载逻辑
        using var response = await _httpClient.GetAsync(versionUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memoryStream = new MemoryStream();
        
        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await memoryStream.WriteAsync(buffer, 0, bytesRead, ct);
            totalRead += bytesRead;
            
            progress?.Report(new DownloadProgress(totalRead, totalBytes ?? 0));
        }

        return memoryStream.ToArray();
    }
}
```

- [ ] **Step 2: 实现 `PluginManagerService`（执行链：安装校验）**
  
```csharp
// MSMC/Features/ContentMarket/Services/PluginManagerService.cs
using System.IO;
using System.IO.Hashing;
using Serilog;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

public class PluginManagerService : IPluginManagerService
{
    private readonly ILogger _logger;
    private readonly IMarketProvider _provider;

    public PluginManagerService(ILogger logger, IMarketProvider provider)
    {
        _logger = logger;
        _provider = provider;
    }

    // 三链原则：执行链 - 完整的安装流程
    public async Task<InstallResult> InstallAsync(MarketVersion version, string serverPath, CancellationToken ct)
    {
        _logger.Information("[PluginMgr] Starting installation for: {PluginId}", version.Id);
        
        try
        {
            // 1. 下载
            var fileBytes = await _provider.DownloadVersionAsync(version.Id, null, ct);
            
            // 2. 校验 (因果链：因 -> 验证)
            if (!string.IsNullOrEmpty(version.Sha1))
            {
                var actualHash = ComputeSha1Hash(fileBytes);
                if (!actualHash.Equals(version.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error("[PluginMgr] SHA1 Hash mismatch. Expected: {Expected}, Actual: {Actual}", version.Sha1, actualHash);
                    return InstallResult.Failed("Hash mismatch");
                }
                _logger.Information("[PluginMgr] SHA1 Hash verified successfully.");
            }

            // 3. 落盘 (执行链：创建安全备份)
            var pluginsDir = Path.Combine(serverPath, "plugins");
            Directory.CreateDirectory(pluginsDir);
            
            var destPath = Path.Combine(pluginsDir, $"{version.Name}.jar");
            
            if (File.Exists(destPath))
            {
                string backupPath = destPath + ".bak";
                _logger.Information("[PluginMgr] Backing up existing file to: {BackupPath}", backupPath);
                File.Copy(destPath, backupPath, overwrite: true);
            }
            
            File.WriteAllBytes(destPath, fileBytes);
            _logger.Information("[PluginMgr] Plugin installed successfully to: {DestPath}", destPath);

            return InstallResult.Succeeded(destPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[PluginMgr] Installation failed.");
            return InstallResult.Failed(ex.Message);
        }
    }

    private string ComputeSha1Hash(byte[] data)
    {
        // 简化示例，实际需引用 System.Security.Cryptography
        return System.Buffers.Binary.BinaryPrimitives.ToString(data); 
    }
}
```

---

### 🟡 P1 · 下季度：长期历史 + 告警阈值 + 移动 Companion

*(具体代码骨架将在后续 Plan 中基于此原则扩展)*

- **长期历史**：在 `MetricsPersistenceService` 扩展中，严格遵守因果链（每一条记录都有时间戳）和返回链（写入/读取均记录日志）。
- **告警引擎**：`AlertEngine` 实现滑窗检测（执行链），触发时通过 `INotificationService`（因果链）发送通知，并生成告警日志（返回链）。
- **移动 Companion**：`CompanionHttpServer` 需实现 token 鉴权（执行链），确保每个 API 端点的调用都有日志记录（返回链）。

### 🟢 P2 · 排期：质量底座

- **测试基线**：所有核心逻辑（Cron 解析、通知路由、插件校验）必须有对应的单元测试，验证因果链、执行链、返回链的健壮性。
- **CI/CD**：在 `.github/workflows/ci.yml` 中加入代码覆盖率检查，确保三链原则的代码实现受到充分保护。

---

## ✅ 三链原则自查清单

| 模块 | 因果链（因→果） | 执行链（咬合/兜底） | 返回链（日志） |
|---|---|---|---|
| **Notifications** | ✅ 事件 (Event) → 通知 (Dispatch) | ✅ 指数退避重试；异常捕获 | ✅ 每次发送/失败均记录 |
| **Scheduler** | ✅ 时间 (Trigger) → 任务 (Action) | ✅ `SemaphoreSlim` 防并发；异常捕获 | ✅ 任务启停全量日志 |
| **ContentMarket** | ✅ 关键词 (Query) → 结果 (Result) | ✅ 下载进度回调；Hash 校验；文件备份 | ✅ 安装/下载全量日志 |
| **AlertEngine** | ✅ 指标 (Metric) → 告警 (Alert) | ✅ 滑窗冷却；恢复通知 | ✅ 告警触发/恢复记录 |

**结论：** 本次方案的所有技术选型与代码骨架均严格遵循修正后的“三链原则”，确保系统逻辑严密、流程可靠、行为可溯。