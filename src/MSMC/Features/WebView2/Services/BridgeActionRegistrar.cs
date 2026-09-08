// -----------------------------------------------------------------------------
// 文件名: BridgeActionRegistrar.cs
// 命名空间: io.NET.ZTR_OS.Features.WebView2.Services
// 功能描述: WebView2 桥接 action 注册中心 —— 统一注册通知/调度/市场模块的 JS→C# 动作
// 设计模式: 三链原则 - 因果链：action 名称 → Service 方法；执行链：try/catch/finally；返回链：结构化日志
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using io.NET.ZTR_OS.Features.ContentMarket.Services;
using io.NET.ZTR_OS.Features.Scheduler.Models;
using io.NET.ZTR_OS.Features.Scheduler.Services;
using io.NET.ZTR_OS.Features.Troubleshooting.Services;
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
        // 注意：notify.dispatch / notify.test 的强实现位于 MainWindow.RegisterBridgeApis()
        // （BridgeJsonOptions 大小写不敏感 + 参数校验 + 完整返回结构）。
        // 这里【绝不重复注册】——RegisterAll 在 RegisterBridgeApis 之后执行，
        // 弱实现会覆盖强实现，导致前端 camelCase payload 反序列化失败（EventType 落默认值）。
        // 此前已出现过该覆盖问题，此为因果链(P5)防线：单处注册，杜绝漂移。

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

        // ════════════ 疑难解答模块 actions ════════════
        registered += SafeRegister(bridge, "diagnostic.runDiagnostic", async payload =>
        {
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
            string worldPath = args.TryGetProperty("worldPath", out var w1) ? w1.GetString() ?? "" : "";
            var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
            var report = await engine.RunQuickAsync(jarPath, string.IsNullOrEmpty(worldPath) ? null : worldPath);
            return new { success = report.Succeeded, report, error = report.ErrorMessage };
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "diagnostic.runDeepScan", async payload =>
        {
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
            string worldPath = args.TryGetProperty("worldPath", out var w1) ? w1.GetString() ?? "" : "";
            var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
            var report = await engine.RunDeepAsync(jarPath, worldPath);
            return new { success = report.Succeeded, report, error = report.ErrorMessage };
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "diagnostic.checkServerRunning", async payload =>
        {
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
            var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
            bool running = await engine.IsServerRunningAsync(jarPath);
            return new { running, pid = (int?)null };
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "diagnostic.killServerAndScan", async payload =>
        {
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
            string worldPath = args.TryGetProperty("worldPath", out var w1) ? w1.GetString() ?? "" : "";
            var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
            bool killSucceeded = await engine.KillServerAsync(jarPath);
            var report = await engine.RunDeepAsync(jarPath, worldPath);
            return new { success = report.Succeeded, report, error = report.ErrorMessage, killSucceeded };
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "diagnostic.executeFix", async payload =>
        {
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            string jarPath = args.TryGetProperty("serverJarPath", out var j1) ? j1.GetString() ?? "" : "";
            string worldPath = args.TryGetProperty("worldPath", out var w1) ? w1.GetString() ?? "" : "";
            string fixId = args.TryGetProperty("fixId", out var f1) ? f1.GetString() ?? "" : "";
            string trustModeStr = args.TryGetProperty("trustMode", out var t1) ? t1.GetString() ?? "Auto" : "Auto";
            var trustMode = Enum.Parse<FixTrustMode>(trustModeStr);

            // 从前端 payload 拿 params（pid / maxPlayers / javaPath 等），构造真实 FixAction
            var @params = new Dictionary<string, object?>();
            if (args.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in p.EnumerateObject())
                    @params[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Number when prop.Value.TryGetInt32(out var i) => i,
                        JsonValueKind.Number when prop.Value.TryGetInt64(out var l) => l,
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => null
                    };
            }
            var label = fixId switch
            {
                "backup.world" => "备份 world 目录",
                "java.switch.version" => "切换 Java 版本",
                "port.kill.process" => "杀掉占用端口的进程",
                "config.edit.server-properties" => "修改 server.properties",
                "region.clean.entities" => "清理异常区块实体",
                "player.reset.damage" => "重置玩家物品 damage",
                "server.kill" => "终止服务器进程",
                _ => fixId
            };
            // 会改动/删除存档或终止进程的修复必须标记 Dangerous，触发前端确认 + 前置备份
            bool dangerous = fixId is "region.clean.entities" or "player.reset.damage" or "server.kill" or "port.kill.process";
            // ActionType 用语义类型（backup/command/config_edit/kill_process/cleanup_*）而非 fixId：
            // FixExecutor DryRun 会把它原样放进 FixResult.StepResults，前端据此展示步骤类型
            var actionType = fixId switch
            {
                "backup.world" => "backup",
                "server.kill" => "kill_process",
                "port.kill.process" => "kill_process",
                "config.edit.server-properties" => "config_edit",
                "java.switch.version" => "command",
                "region.clean.entities" => "cleanup_region",
                "player.reset.damage" => "cleanup_player",
                _ => fixId
            };
            var step = new FixStep(label, actionType, dangerous, true, @params);
            var fix = new FixAction(fixId, label, dangerous, null, 0.8, string.Empty, new List<FixStep> { step });

            var engine = serviceProvider.GetRequiredService<IDiagnosticEngine>();
            return await engine.ExecuteFixAsync(jarPath, string.IsNullOrEmpty(worldPath) ? null : worldPath, fix, trustMode);
        }, logger, ref registered, ref failed);

        // cancelFix: P4 诚实返回 — 跨请求 CTS 取消（P1）尚未实现，明确返回失败而非假成功，
        // 避免前端误以为 step-by-step 修复已被取消
        registered += SafeRegister(bridge, "diagnostic.cancelFix", payload =>
        {
            Log.Information("[DIAG] cancelFix 收到（跨请求取消尚未实现）");
            return Task.FromResult<object?>(new { success = false, error = "跨请求取消尚不支持（P1 实现），本次修复不受影响" });
        }, logger, ref registered, ref failed);

        // exportReport: 真实现 — 把前端传回的 DiagnosticReport 完整序列化为 Markdown / JSON
        registered += SafeRegister(bridge, "diagnostic.exportReport", payload =>
        {
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            string format = args.TryGetProperty("format", out var f1) ? f1.GetString() ?? "markdown" : "markdown";
            string? customPath = args.TryGetProperty("path", out var p1) ? p1.GetString() : null;
            var report = args.TryGetProperty("report", out var r1) && r1.ValueKind == JsonValueKind.Object
                ? r1 : (JsonElement?)null;
            try
            {
                var dir = customPath ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MSMC", "diagnostic");
                Directory.CreateDirectory(dir);
                var fileName = $"diagnostic-report-{DateTime.Now:yyyyMMdd-HHmmss}.{format}";
                var fullPath = Path.Combine(dir, fileName);
                string content;
                if (report.HasValue)
                {
                    content = format == "json"
                        ? JsonSerializer.Serialize(report.Value, new JsonSerializerOptions { WriteIndented = true })
                        : BuildMarkdown(report.Value);
                }
                else
                {
                    content = format == "json"
                        ? JsonSerializer.Serialize(new { generatedAt = DateTime.Now, note = "未提供报告数据，请从前端传入 report" }, new JsonSerializerOptions { WriteIndented = true })
                        : $"# MSMC 诊断报告\n\n生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n> 未提供报告数据，请从前端传入 report\n";
                }
                File.WriteAllText(fullPath, content);
                Log.Information("[DIAG] exportReport 成功: {Path} ({Size} bytes)", fullPath, content.Length);
                return Task.FromResult<object?>(new { path = fullPath, size = content.Length });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DIAG] exportReport 失败");
                return Task.FromResult<object?>(new { path = "", size = 0, error = ex.Message });
            }
        }, logger, ref registered, ref failed);

        // ════════════ DeepSeek AI 分析 actions ════════════

        // 查询 AI 配置状态（是否已配 Key）
        registered += SafeRegister(bridge, "diagnostic.getAiStatus", _ =>
        {
            var ai = serviceProvider.GetRequiredService<IDeepSeekService>();
            return Task.FromResult<object?>(new { configured = ai.IsConfigured, hasKey = !string.IsNullOrEmpty(ai.GetApiKey()) });
        }, logger, ref registered, ref failed);

        // 设置 / 清除 API Key（DPAPI 加密存储）
        registered += SafeRegister(bridge, "diagnostic.setApiKey", payload =>
        {
            var ai = serviceProvider.GetRequiredService<IDeepSeekService>();
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            var key = args.TryGetProperty("apiKey", out var k) ? k.GetString() ?? string.Empty : string.Empty;
            var (ok, err) = ai.SetApiKey(key);
            return Task.FromResult<object?>(new { success = ok, configured = ok && !string.IsNullOrEmpty(key), error = err });
        }, logger, ref registered, ref failed);

        // 对已生成的报告做 AI 追问 / 重新分析
        registered += SafeRegister(bridge, "diagnostic.askAI", async payload =>
        {
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            string question = args.TryGetProperty("question", out var q) ? q.GetString() ?? string.Empty : string.Empty;
            var reportElement = args.TryGetProperty("report", out var r) && r.ValueKind == JsonValueKind.Object
                ? r : (JsonElement?)null;

            var ai = serviceProvider.GetRequiredService<IDeepSeekService>();
            Log.Information("[DIAG-AI] askAI 调用: IsConfigured={Configured}, HasReport={HasReport}",
                ai.IsConfigured, reportElement != null);

            if (!ai.IsConfigured)
                return new { success = false, error = "尚未配置 DeepSeek API Key，请在设置中配置后再试", needsConfig = true };
            if (reportElement is null)
                return new { success = false, error = "缺少报告数据" };

            // 把前端传回的 JSON 报告还原为 DiagnosticReport 交给 AI
            var report = JsonSerializer.Deserialize<DiagnosticReport>(
                reportElement.Value.GetRawText(), BridgeJsonOptions);
            if (report is null)
                return new { success = false, error = "报告数据无法解析" };

            var analysis = await ai.AnalyzeReportAsync(report, string.IsNullOrEmpty(question) ? null : question);
            if (analysis is null)
                return new { success = false, error = "AI 分析失败（检查网络或 API Key）" };
            return new { success = true, analysis };
        }, logger, ref registered, ref failed);

        // ════════════ Function Calling AI 诊断 actions ════════════
        // 让 DeepSeek 自主选择工具（联网/读日志/下载核心）诊断服务器
        registered += SafeRegister(bridge, "troubleshooting.aiInit", async payload =>
        {
            // 【极端保底 P0】顶层 try-catch：任何异常（DI 容器异常/IsConfigured getter 异常/
            // ToolRegistry 构造异常/任何未知异常）都返回 needsConfig=true
            // 确保用户永远不会卡在 loading，永远能看到配置卡
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
                int? tutorialStep = args.TryGetProperty("tutorialStep", out var ts) && ts.ValueKind == JsonValueKind.Number
                    ? ts.GetInt32() : null;
                string? serverPath = args.TryGetProperty("selectedServerPath", out var sp) && sp.ValueKind == JsonValueKind.String
                    ? sp.GetString() : null;
                string? userQuestion = args.TryGetProperty("userQuestion", out var uq) && uq.ValueKind == JsonValueKind.String
                    ? uq.GetString() : null;

                var ai = serviceProvider.GetRequiredService<IDeepSeekService>();

                // 【最关键路径】先查 IsConfigured，不需要任何 HTTP 调用
                // 因果链：IsConfigured 是同步读本地 DPAPI 文件，不应该异常；
                // 如果真异常了，catch 块保底返回 needsConfig=true
                bool configured;
                try
                {
                    configured = ai.IsConfigured;
                    Log.Information("[DIAG-AI] aiInit 调用: IsConfigured={Configured}, TutorialStep={Step}, ServerPath={Path}",
                        configured, tutorialStep, serverPath ?? "(null)");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DIAG-AI] aiInit IsConfigured 查询异常 → 保底 needsConfig=true");
                    return new { success = false, error = $"AI 服务状态查询异常: {ex.Message}", needsConfig = true };
                }

                if (!configured)
                    return new { success = false, error = "尚未配置 DeepSeek API Key", needsConfig = true };

                // ════════════════════════════════════════════════════════════
                // 【快速连通性预检查 P0】在进 FunctionCallingEngine 之前
                // 先调 TestKeyValidityAsync（10s 超时，只发 max_tokens=1 的最小请求）
                // 因果链修复：之前直接进 FunctionCallingEngine → HttpClient 60s 超时
                // 但前端 bridge.invoke 只等 30s 就先 timeout 了 → 用户看到 "Request timeout"
                // 现在先快速检测：Key 有效？网络通？10s 内必返回
                // ════════════════════════════════════════════════════════════
                Log.Information("[DIAG-AI] aiInit IsConfigured=true → 快速 TestKeyValidity 预检查...");
                var validity = await ai.TestKeyValidityAsync();
                if (!validity.IsValid && validity.StatusCode is 401 or 403)
                {
                    Log.Warning("[DIAG-AI] aiInit 预检查 → Key 无效 (HTTP {Status})", validity.StatusCode);
                    return new { success = false, error = $"API Key 已过期或无效（HTTP {validity.StatusCode}），请重新配置", needsConfig = true };
                }
                if (!validity.IsValid && validity.StatusCode is null)
                {
                    Log.Warning("[DIAG-AI] aiInit 预检查 → 网络异常（StatusCode=null）");
                    return new { success = false, error = "无法连接 DeepSeek API — 请检查网络后重试（或配置代理）" };
                }
                if (validity.StatusCode is 429)
                {
                    Log.Warning("[DIAG-AI] aiInit 预检查 → 限流 (429)");
                    return new { success = false, error = "API 调用频率过高，请稍后再试" };
                }
                // 200 OK 或其他非 4xx → Key 有效且网络通，继续 Function Calling
                Log.Information("[DIAG-AI] aiInit 预检查通过 → 开始 Function Calling 诊断");

                // 只有 IsConfigured=true + 连通性确认 才走到这里
                var prompt = BuildAiUserPrompt(tutorialStep, serverPath, userQuestion);
                var analysis = await ai.AnalyzeWithToolsAsync(prompt);
                if (analysis is null)
                {
                    // AI 返回 null —— 逐级排查：
                    if (!ai.IsConfigured)
                        return new { success = false, error = "API Key 已失效或被清除，请重新配置", needsConfig = true };

                    // IsConfigured=true 但 AI 失败 —— 调 TestKeyValidity 区分 Key 无效 vs 网络异常
                    Log.Information("[DIAG-AI] aiInit AI 返回 null → 调用 TestKeyValidityAsync 区分原因");
                    var validity = await ai.TestKeyValidityAsync();
                    if (!validity.IsValid && validity.StatusCode is 401 or 403)
                    {
                        Log.Warning("[DIAG-AI] aiInit → Key 无效 (HTTP {Status})", validity.StatusCode);
                        return new { success = false, error = $"API Key 已过期或无效（HTTP {validity.StatusCode}），请重新配置", needsConfig = true };
                    }
                    if (!validity.IsValid && validity.StatusCode is null)
                    {
                        Log.Warning("[DIAG-AI] aiInit → 网络异常");
                        return new { success = false, error = "AI 调用失败 — 网络异常，请检查网络后重试" };
                    }
                    Log.Warning("[DIAG-AI] aiInit → Key 有效但 AI 调用失败 (Status={Status})", validity.StatusCode);
                    return new { success = false, error = "AI 调用暂时失败 — 可能是限流或服务端问题，请稍后重试" };
                }
                Log.Information("[DIAG-AI] aiInit 成功返回分析结果");
                return new { success = true, analysis };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DIAG-AI] aiInit 顶层异常 → 保底 needsConfig=true");
                return new { success = false, error = $"AI 服务异常: {ex.Message}，请配置 API Key 后重试", needsConfig = true };
            }
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "troubleshooting.aiSend", async payload =>
        {
            // 同样的极端保底
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
                string message = args.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(message))
                    return new { success = false, error = "message 不能为空" };

                var ai = serviceProvider.GetRequiredService<IDeepSeekService>();
                bool configured;
                try
                {
                    configured = ai.IsConfigured;
                    Log.Information("[DIAG-AI] aiSend 调用: IsConfigured={Configured}, MessageLen={Len}",
                        configured, message.Length);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DIAG-AI] aiSend IsConfigured 查询异常 → 保底 needsConfig=true");
                    return new { success = false, error = $"AI 服务状态查询异常: {ex.Message}", needsConfig = true };
                }

                if (!configured)
                    return new { success = false, error = "尚未配置 DeepSeek API Key", needsConfig = true };

                // 同样的快速预检查 — aiSend 也需要 10s 内返回
                var validity = await ai.TestKeyValidityAsync();
                if (!validity.IsValid && validity.StatusCode is 401 or 403)
                    return new { success = false, error = $"API Key 已过期或无效（HTTP {validity.StatusCode}），请重新配置", needsConfig = true };
                if (!validity.IsValid && validity.StatusCode is null)
                    return new { success = false, error = "无法连接 DeepSeek API — 请检查网络后重试（或配置代理）" };
                if (validity.StatusCode is 429)
                    return new { success = false, error = "API 调用频率过高，请稍后再试" };

                var analysis = await ai.AnalyzeWithToolsAsync(message);
                if (analysis is null)
                {
                    if (!ai.IsConfigured)
                        return new { success = false, error = "API Key 已失效或被清除，请重新配置", needsConfig = true };

                    var validity = await ai.TestKeyValidityAsync();
                    if (!validity.IsValid && validity.StatusCode is 401 or 403)
                        return new { success = false, error = $"API Key 已过期或无效（HTTP {validity.StatusCode}），请重新配置", needsConfig = true };
                    if (!validity.IsValid && validity.StatusCode is null)
                        return new { success = false, error = "AI 调用失败 — 网络异常，请检查网络后重试" };
                    return new { success = false, error = "AI 调用暂时失败 — 可能是限流或服务端问题，请稍后重试" };
                }
                return new { success = true, analysis };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DIAG-AI] aiSend 顶层异常 → 保底 needsConfig=true");
                return new { success = false, error = $"AI 服务异常: {ex.Message}，请配置 API Key 后重试", needsConfig = true };
            }
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "troubleshooting.aiStop", _ =>
        {
            // CancellationToken 由外部管理，这里只返回确认
            return Task.FromResult<object?>(new { success = true });
        }, logger, ref registered, ref failed);

        registered += SafeRegister(bridge, "troubleshooting.confirmFix", async payload =>
        {
            var args = JsonSerializer.Deserialize<JsonElement>(payload ?? "{}");
            string? fixId = args.TryGetProperty("fixId", out var f) && f.ValueKind == JsonValueKind.String
                ? f.GetString() : null;
            if (string.IsNullOrEmpty(fixId))
                return new { success = false, error = "fixId 不能为空" };

            // TODO: 把 fixId 映射为 FixAction，再调用 IFixExecutor.ExecuteAsync(serverJarPath, worldPath, fix, trustMode)
            // 当前 Bridge payload 还没有 serverJarPath/worldPath 字段，暂时返回占位，后续对接
            return new { success = true, message = $"Fix action '{fixId}' queued (awaiting FixAction mapping)", fixId };
        }, logger, ref registered, ref failed);

        Log.Information("[BRDG-REG] [OK] 桥接 actions 注册完成: {Ok} OK / {Fail} FAIL", registered, failed);
    }

    /// <summary>Function Calling AI 用户 prompt 组装（教程上下文 + 用户追问）</summary>
    private static string BuildAiUserPrompt(int? tutorialStep, string? serverPath, string? userQuestion)
    {
        var parts = new List<string>();
        if (tutorialStep.HasValue)
            parts.Add($"[教程上下文] 用户在第 {tutorialStep.Value} 步");
        if (!string.IsNullOrEmpty(serverPath))
            parts.Add($"[服务器路径] {serverPath}");
        if (!string.IsNullOrEmpty(userQuestion))
            parts.Add($"[用户追问] {userQuestion}");
        parts.Add("请自主选择工具诊断。如果用户不会开服，请帮他检查 Java、端口、核心类型，并从官方源下载合适的服务端核心。");
        return string.Join("\n", parts);
    }

    /// <summary>把前端回传的 DiagnosticReport JSON 转成 Markdown 报告</summary>
    private static string BuildMarkdown(JsonElement report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# MSMC 诊断报告");
        sb.AppendLine();
        if (report.TryGetProperty("generatedAt", out var g)) sb.AppendLine($"生成时间: {g}");
        if (report.TryGetProperty("msmcVersion", out var v)) sb.AppendLine($"MSMC 版本: {v}");
        if (report.TryGetProperty("serverJarPath", out var j)) sb.AppendLine($"服务器 JAR: {j}");
        if (report.TryGetProperty("worldPath", out var w) && w.ValueKind == JsonValueKind.String)
            sb.AppendLine($"世界目录: {w.GetString()}");
        sb.AppendLine();

        if (report.TryGetProperty("summary", out var sm) && sm.TryGetProperty("totalChecks", out var tc))
        {
            sb.AppendLine("## 体检汇总");
            sb.AppendLine();
            sb.AppendLine("| 总检查 | ✅ | ⚠️ | ❌ | 🔴 | 可自动修复 | 耗时(ms) |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            sb.AppendLine($"| {tc} | {GetProp(sm, "okCount")} | {GetProp(sm, "warningCount")} | {GetProp(sm, "errorCount")} | {GetProp(sm, "criticalCount")} | {GetProp(sm, "autoFixableCount")} | {GetProp(sm, "scanDurationMs")} |");
            sb.AppendLine();
        }

        if (report.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("## 发现的问题");
            sb.AppendLine();
            foreach (var issue in issues.EnumerateArray())
            {
                sb.AppendLine($"- **[{GetProp(issue, "severity")}] {GetProp(issue, "title")}**（{GetProp(issue, "category")}）");
                sb.AppendLine($"  - {GetProp(issue, "detail")}");
                if (issue.TryGetProperty("hint", out var h) && h.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(h.GetString()))
                    sb.AppendLine($"  - 提示: {h}");
                if (issue.TryGetProperty("suggestion", out var sug) && sug.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(sug.GetString()))
                    sb.AppendLine($"  - 建议: {sug}");
            }
            sb.AppendLine();
        }

        if (report.TryGetProperty("aiAnalysis", out var ai) && ai.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("## AI 分析");
            sb.AppendLine();
            if (ai.TryGetProperty("summary", out var aSum)) sb.AppendLine($"> {aSum}");
            if (ai.TryGetProperty("keyFindings", out var kf) && kf.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine();
                sb.AppendLine("### 关键发现");
                foreach (var k in kf.EnumerateArray())
                    sb.AppendLine($"- {k}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*由 MSMC 疑难解答模块生成*");
        return sb.ToString();
    }

    /// <summary>把 JsonElement 的值转成适合 Markdown 的字符串（含嵌套对象/数组）</summary>
    private static string GetProp(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return string.Empty;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => v.GetRawText(),
        };
    }

    private static readonly JsonSerializerOptions BridgeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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
