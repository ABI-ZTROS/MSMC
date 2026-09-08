// -----------------------------------------------------------------------------
// 文件名: FunctionCallingEngine.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek
// 功能描述: DeepSeek Function Calling 多轮对话引擎 —— 让 AI 自主选择工具诊断
// 设计原则:
//   P4 诚实返回链: 10 轮硬限 + 60s 超时 + 失败显式返回 null 不编造
//   P5 契约一致性: SystemPrompt 里 fixId 白名单 + ParseAnalysis HashSet 硬校验
//   P6 异步纪律: ConfigureAwait(false) + OperationCanceledException 单独处理
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using io.NET.ZTR_OS.Features.Troubleshooting.Services;
using io.NET.ZTR_OS.Features.WebView2.Services;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek;

public sealed class FunctionCallingEngine
{
    private const string ApiUrl = "https://api.deepseek.com/chat/completions";
    private const string Model = "deepseek-chat";
    private const int MaxRounds = 10;
    private const int TotalTimeoutSeconds = 60;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(TotalTimeoutSeconds)
    };

    private readonly ToolRegistry _registry;
    private readonly ILogger<FunctionCallingEngine> _log;
    private readonly string _apiKey;
    private readonly IWebView2BridgeService? _bridge;

    // SystemPrompt 完整定义（含 fixId 白名单软约束）
    private const string SystemPrompt = @"你是一名资深 Minecraft Java 版服务器运维专家。你可以调用工具来检查服务器状态、读日志、下载核心、搜官方文档。

严格规则：
1. 最多调用 10 轮工具后必须给出最终回答
2. 最终回答必须是纯 JSON，结构：
   {
     ""summary"": ""一段不超过 100 字的中文总结"",
     ""keyFindings"": [""关键发现1"", ""关键发现2""],
     ""recommendedActions"": [
       { ""fixId"": ""修复动作 id"", ""label"": ""动作描述"", ""dangerous"": false, ""rationale"": ""理由"" }
     ],
     ""needMoreInfo"": false,
     ""suggestedQuestions"": [""建议用户补充的问题""]
   }
3. recommendedActions 的 fixId 只能用白名单：backup.world / server.kill / port.kill.process / config.edit.server-properties / java.switch.version / region.clean.entities / player.reset.damage
4. 工具返回 Success=false 时如实告诉用户，不要编造结果
5. 不要一次调所有工具，只调能帮你诊断当前问题的那几个";

    public FunctionCallingEngine(
        ToolRegistry registry,
        ILogger<FunctionCallingEngine> log,
        string apiKey,
        IWebView2BridgeService? bridge = null)
    {
        _registry = registry;
        _log = log;
        _apiKey = apiKey;
        _bridge = bridge;
    }

    /// <summary>启动多轮 Function Calling 诊断</summary>
    public async Task<DeepSeekAnalysis?> RunAsync(string userPrompt, CancellationToken externalCt = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _log.LogWarning("[DIAG-AI] DeepSeek API Key 未配置");
            return null;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        cts.CancelAfter(TimeSpan.FromSeconds(TotalTimeoutSeconds));

        try
        {
            var messages = new List<object>
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userPrompt }
            };

            JsonElement tools;
            try
            {
                tools = JsonDocument.Parse(_registry.BuildToolsSchemaJson()).RootElement;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[DIAG-AI] 组装 tools schema 失败");
                return null;
            }

            for (int round = 0; round < MaxRounds; round++)
            {
                var (toolCalls, content) = await SendCompletionAsync(messages, tools, cts.Token)
                    .ConfigureAwait(false);

                if (toolCalls == null || toolCalls.Length == 0)
                {
                    // 最终回答
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        _log.LogWarning("[DIAG-AI] DeepSeek 返回空回答，round={Round}", round);
                        return null;
                    }
                    return ParseFinalAnalysis(content, round + 1);
                }

                // 处理 tool_calls
                messages.Add(new
                {
                    role = "assistant",
                    tool_calls = toolCalls.Select(c => new
                    {
                        id = c.Id,
                        type = "function",
                        function = new { name = c.Name, arguments = c.Arguments.GetRawText() }
                    }).ToArray()
                });

                foreach (var call in toolCalls)
                {
                    // Bridge 推事件：工具调用开始
                    if (_bridge?.IsInitialized == true)
                    {
                        _ = _bridge.SendEventAsync("troubleshooting.aiToolExec", new
                        {
                            toolName = call.Name,
                            args = call.Arguments.ValueKind == JsonValueKind.Object
                                ? JsonSerializer.Deserialize<object>(call.Arguments.GetRawText())
                                : null,
                            round = round + 1
                        });
                    }

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var result = await _registry.ExecuteAsync(call.Name, call.Arguments, cts.Token)
                        .ConfigureAwait(false);
                    sw.Stop();

                    // Bridge 推事件：工具执行完成
                    if (_bridge?.IsInitialized == true)
                    {
                        _ = _bridge.SendEventAsync("troubleshooting.aiStream", new
                        {
                            toolName = call.Name,
                            status = result.Success ? "done" : "failed",
                            elapsedMs = sw.ElapsedMilliseconds,
                            round = round + 1,
                            error = result.Error
                        });
                    }

                    messages.Add(new
                    {
                        role = "tool",
                        content = result.ToJson(),
                        tool_call_id = call.Id
                    });
                }
            }

            _log.LogWarning("[DIAG-AI] Function Calling 超出 {MaxRounds} 轮上限，强制终止", MaxRounds);
            return null;
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("[DIAG-AI] Function Calling 超时或被取消（{TotalTimeout}s）", TotalTimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[DIAG-AI] Function Calling 未预期异常");
            return null;
        }
    }

    private async Task<(ToolCall[]? toolCalls, string? content)> SendCompletionAsync(
        List<object> messages, JsonElement tools, CancellationToken ct)
    {
        var body = new
        {
            model = Model,
            messages,
            tools = tools.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<object>(tools.GetRawText())
                : null,
            tool_choice = "auto",
            temperature = 0.3,
            max_tokens = 1200
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("[DIAG-AI] DeepSeek HTTP {Code}: {Raw}",
                (int)resp.StatusCode, raw.Length > 300 ? raw[..300] : raw);
            return (null, null);
        }

        using var doc = JsonDocument.Parse(raw);
        var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

        var hasToolCalls = msg.TryGetProperty("tool_calls", out var toolCallsElem)
                          && toolCallsElem.ValueKind == JsonValueKind.Array
                          && toolCallsElem.GetArrayLength() > 0;

        ToolCall[]? calls = null;
        if (hasToolCalls)
        {
            calls = new ToolCall[toolCallsElem.GetArrayLength()];
            for (int i = 0; i < calls.Length; i++)
            {
                var tc = toolCallsElem[i];
                calls[i] = new ToolCall(
                    tc.GetProperty("id").GetString()!,
                    tc.GetProperty("function").GetProperty("name").GetString()!,
                    JsonDocument.Parse(tc.GetProperty("function").GetProperty("arguments").GetString() ?? "{}").RootElement);
            }
        }

        var content = msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() : null;

        return (calls, content);
    }

    private DeepSeekAnalysis? ParseFinalAnalysis(string content, int roundsUsed)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // fixId 白名单 —— 双重保险：SystemPrompt 软约束 + HashSet 硬校验
            var allowedFixIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "backup.world", "server.kill", "port.kill.process",
                "config.edit.server-properties", "java.switch.version",
                "region.clean.entities", "player.reset.damage"
            };

            var actions = new List<FixAction>();
            if (root.TryGetProperty("recommendedActions", out var acts)
                && acts.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in acts.EnumerateArray())
                {
                    var rawFixId = TryGetStr(a, "fixId");
                    var safeFixId = (rawFixId != null && allowedFixIds.Contains(rawFixId)) ? rawFixId : "ai.fix";
                    if (rawFixId != null && safeFixId == "ai.fix")
                        _log.LogWarning("[DIAG-AI] DeepSeek fixId '{FixId}' 不在白名单，降级为 ai.fix", rawFixId);

                    actions.Add(new FixAction(
                        safeFixId,                                    // FixId
                        TryGetStr(a, "label") ?? "AI 建议",            // Label
                        TryGetBool(a, "dangerous") ?? false,          // Dangerous
                        null,                                         // DiffPreview (Function Calling 不直接生成 diff)
                        0.5,                                          // Confidence
                        TryGetStr(a, "rationale"),                    // Rationale
                        new List<FixStep>()                           // Steps (由 FixExecutor 内部展开)
                    ));
                }
            }

            return new DeepSeekAnalysis(
                TryGetStr(root, "summary") ?? "(AI 未返回摘要)",
                TryGetStrList(root, "keyFindings"),
                actions,
                TryGetBool(root, "needMoreInfo") ?? false,
                TryGetStrList(root, "suggestedQuestions"),
                content);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[DIAG-AI] 解析 DeepSeek 最终输出失败");
            return null;
        }
    }

    private static string? TryGetStr(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool? TryGetBool(JsonElement e, string name)
    {
        if (e.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        return null;
    }

    private static List<string> TryGetStrList(JsonElement e, string name)
    {
        var list = new List<string>();
        if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array)
            foreach (var item in v.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(item.GetString()))
                    list.Add(item.GetString()!);
        return list;
    }

    private readonly record struct ToolCall(string Id, string Name, JsonElement Arguments);
}
