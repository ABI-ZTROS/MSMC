// -----------------------------------------------------------------------------
// 文件名: BridgeActionRegistrar.cs
// 命名空间: io.NET.ZTR_OS.Features.WebView2.Services
// 功能描述: WebView2 桥接 action 注册中心 —— 统一注册通知/调度/市场模块的 JS→C# 动作
// 设计模式: 三链原则 - 因果链：action 名称 → Service 方法；执行链：try/catch/finally；返回链：结构化日志
// -----------------------------------------------------------------------------

using System.Text.Json;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using io.NET.ZTR_OS.Features.ContentMarket.Services;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;
using io.NET.ZTR_OS.Features.Scheduler.Models;
using io.NET.ZTR_OS.Features.Scheduler.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace io.NET.ZTR_OS.Features.WebView2.Services;

/// <summary>
/// 桥接 action 注册中心 —— 在 WebView2BridgeService 初始化完成后调用
/// </summary>
public static class BridgeActionRegistrar
{
    /// <summary>
    /// 注册三大模块的所有 action 到桥接服务
    /// </summary>
    public static void RegisterAll(
        IWebView2BridgeService bridge,
        IServiceProvider serviceProvider,
        Serilog.ILogger logger)
    {
        Log.Information("[BRDG-REG] 开始注册桥接 actions (NOTIFY + SCHED + MARKET)...");

        int registered = 0;
        int failed = 0;

        // ════════════ 通知模块 actions ════════════
        registered += SafeRegister(bridge, "notify.dispatch", async payload =>
        {
            var notifService = serviceProvider.GetRequiredService<INotificationService>();
            var evt = JsonSerializer.Deserialize<NotificationEvent>(payload ?? "{}");
            if (evt == null) throw new InvalidOperationException("Invalid notification event payload");
            Log.Information("[BRDG-REG] [NOTIFY] notify.dispatch: {EventType} ({EventId})", evt.EventType, evt.Id);
            return await notifService.DispatchAsync(evt);
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "notify.test", async payload =>
        {
            var notifService = serviceProvider.GetRequiredService<INotificationService>();
            Log.Information("[BRDG-REG] [NOTIFY] notify.test: manual test dispatch");
            var evt = new NotificationEvent
            {
                EventType = NotificationEventType.ManualTest,
                Title = "手动测试通知",
                Message = payload ?? "这是一条测试通知"
            };
            return await notifService.DispatchAsync(evt);
        }, logger, ref registered, ref failed);

        // ════════════ 调度模块 actions ════════════
        registered += SafeRegister(bridge, "scheduler.list", _ =>
        {
            var schedService = serviceProvider.GetRequiredService<ISchedulerService>();
            var tasks = schedService.GetAllTasks();
            Log.Information("[BRDG-REG] [SCHED] scheduler.list: {Count} tasks", tasks.Count);
            return Task.FromResult<object?>(tasks);
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "scheduler.add", payload =>
        {
            var schedService = serviceProvider.GetRequiredService<ISchedulerService>();
            var task = JsonSerializer.Deserialize<ScheduledTask>(payload ?? "{}");
            if (task == null) throw new InvalidOperationException("Invalid task payload");
            Log.Information("[BRDG-REG] [SCHED] scheduler.add: {TaskName}", task.Name);
            schedService.AddTask(task);
            return Task.FromResult<object?>(new { success = true, id = task.Id });
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "scheduler.delete", payload =>
        {
            var schedService = serviceProvider.GetRequiredService<ISchedulerService>();
            var idStr = payload?.Trim('"') ?? string.Empty;
            var id = Guid.Parse(idStr);
            Log.Information("[BRDG-REG] [SCHED] scheduler.delete: {Id}", id);
            var ok = schedService.DeleteTask(id);
            return Task.FromResult<object?>(new { success = ok });
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "scheduler.runNow", async payload =>
        {
            var schedService = serviceProvider.GetRequiredService<ISchedulerService>();
            var idStr = payload?.Trim('"') ?? string.Empty;
            var id = Guid.Parse(idStr);
            Log.Information("[BRDG-REG] [SCHED] scheduler.runNow: {Id}", id);
            var ok = await schedService.RunNowAsync(id);
            return new { success = ok };
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "scheduler.history", _ =>
        {
            var schedService = serviceProvider.GetRequiredService<ISchedulerService>();
            var history = schedService.GetExecutionHistory(50);
            Log.Information("[BRDG-REG] [SCHED] scheduler.history: {Count} records", history.Count);
            return Task.FromResult<object?>(history);
        }, logger, ref registered, ref failed);

        // ════════════ 市场模块 actions —— 【不在这里注册】
        // 因果链（契约一致性 P5 / 执行链确定性）：
        // MainWindow.xaml.cs 的 RegisterBridgeApis() 也会注册同一组 market.* handlers，
        // 而且那里的实现更完整：
        //   - market.search     → 优先走 MarketProviderFactory（多源并行聚合），而非单 IMarketProvider
        //   - market.versions   → MarketProviderFactory + source 参数路由
        //   - market.install    → JsonElement 解析（与前端 bridge.invoke 的 payload 结构匹配）
        //   - market.listInstalled → 同上
        // 如果这里再注册一遍，执行顺序（RegisterBridgeApis → BridgeActionRegistrar.RegisterAll）
        // 会让本文件中弱实现覆盖掉 MainWindow 中的强实现 →
        // 实际表现为: 搜索只剩 Modrinth 单一源，MarketProviderFactory 完全不生效。
        // 因此市场模块 4 个 action 全部移到 MainWindow 注册，这里不重复。
        const string MARKET_MODULE_SKIP_REASON =
            "市场模块 actions 在 MainWindow.RegisterBridgeApis() 中注册（强实现：MarketProviderFactory 多源 + JsonElement 解析），避免重复覆盖弱实现。";
        Log.Information("[BRDG-REG] [MARKET] SKIP: {Rsn}", MARKET_MODULE_SKIP_REASON);

        Log.Information("[BRDG-REG] [OK] 桥接 actions 注册完成: {Ok} OK / {Fail} FAIL", registered, failed);
    }

    /// <summary>
    /// 安全注册单个 action handler —— 执行链的兜底，单个 handler 失败不影响其他
    /// </summary>
    private static int SafeRegister(
        IWebView2BridgeService bridge,
        string actionName,
        Func<string?, Task<object?>> handler,
        Serilog.ILogger logger,
        ref int registered,
        ref int failed)
    {
        try
        {
            bridge.RegisterRequestHandler(actionName, async payload =>
            {
                Log.Debug("[BRDG-REG] [HOOK] Executing action: {Action}", actionName);
                try
                {
                    var result = await handler(payload?.ToString());
                    Log.Debug("[BRDG-REG] [HOOK] Action {Action} completed", actionName);
                    return result;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[BRDG-REG] [HOOK] Action {Action} failed", actionName);
                    throw;
                }
            });
            registered++;
            Log.Information("[BRDG-REG] [OK] Registered action: {Action}", actionName);
            return 1;
        }
        catch (Exception ex)
        {
            failed++;
            Log.Error(ex, "[BRDG-REG] [ERR] Failed to register action: {Action}", actionName);
            return 0;
        }
    }
}

/// <summary>
/// 市场安装 payload（桥接专用）
/// </summary>
public class MarketInstallPayload
{
    public MarketVersion Version { get; set; } = new();
    public string ServerPath { get; set; } = string.Empty;
}
