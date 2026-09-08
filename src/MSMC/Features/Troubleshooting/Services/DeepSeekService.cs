// -----------------------------------------------------------------------------
// DeepSeekService.cs — DeepSeek AI 诊断分析客户端
// 功能: 扫描报告喂给 DeepSeek（后处理模式），输出严格 JSON 结构化诊断；
//       API Key 用 DPAPI（当前用户）加密存储于 %AppData%/io.NET.ZTR_OS/diagnostic/。
// 设计约束: AI 失败不阻断主流程（返回 null 而非抛异常）；预设词约束输出 schema。
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using io.NET.ZTR_OS.Features.WebView2.Services;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public interface IDeepSeekService
{
    /// <summary>是否已配置 API Key（决定报告生成后是否自动附加 AI 分析）</summary>
    bool IsConfigured { get; }

    /// <summary>对报告做后处理 AI 分析；userQuestion 为空则输出默认诊断结论</summary>
    Task<DeepSeekAnalysis?> AnalyzeReportAsync(DiagnosticReport report, string? userQuestion, CancellationToken ct = default);

    /// <summary>
    /// Function Calling AI 诊断 —— 让 DeepSeek 自主选择工具（联网/读日志/下载核心）诊断
    /// 用于「我不会开服」教程中途一键诊断，不再强制需要选中服务器
    /// </summary>
    Task<DeepSeekAnalysis?> AnalyzeWithToolsAsync(string userPrompt, CancellationToken ct = default);

    string? GetApiKey();

    (bool Success, string? Error) SetApiKey(string key);
}

public sealed class DeepSeekService : IDeepSeekService
{
    private const string ApiUrl = "https://api.deepseek.com/chat/completions";
    private const string Model = "deepseek-chat";

    // 【诚实返回链 P4】允许的 fixId 白名单——C# 侧硬约束，AI 侧 system prompt 只是"软建议"
    // 任何不在此名单的 fixId 一律降级为 "ai.fix"（带 Warning 日志），防止模型幻觉输出
    // 不存在的修复动作导致用户点了之后静默失败或崩溃
    private static readonly HashSet<string> AllowedFixIds = new(StringComparer.Ordinal)
    {
        "backup.world",
        "server.kill",
        "port.kill.process",
        "config.edit.server-properties",
        "java.switch.version",
        "region.clean.entities",
        "player.reset.damage",
    };

    // 预设词：严格约束输出为固定 schema 的 JSON（防 DeepSeek 降智抽风输出 markdown/散文）
    private const string SystemPrompt =
        "你是一名资深 Minecraft Java 版服务器运维专家。用户会提供服务器体检结果，请给出诊断结论。\n" +
        "严格要求：只输出一个 JSON 对象，不要输出任何 JSON 之外的文字、markdown 代码块或解释。JSON 结构如下：\n" +
        "{\n" +
        "  \"summary\": \"一段不超过 100 字的中文总结\",\n" +
        "  \"keyFindings\": [\"关键发现1\", \"关键发现2\"],\n" +
        "  \"recommendedActions\": [\n" +
        "    { \"fixId\": \"修复动作 id（如 java.switch.version / config.edit.server-properties）\", \"label\": \"动作描述\", \"dangerous\": false, \"rationale\": \"理由\" }\n" +
        "  ],\n" +
        "  \"needMoreInfo\": false,\n" +
        "  \"suggestedQuestions\": [\"建议用户补充的问题\"]\n" +
        "}\n" +
        "recommendedActions 的 fixId 只能使用已知修复动作之一：backup.world / server.kill / port.kill.process / config.edit.server-properties / java.switch.version / region.clean.entities / player.reset.damage；没有合适动作时数组可为空。";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    private readonly ILogger _log;
    private readonly FunctionCallingEngine? _functionEngine;

    public DeepSeekService(
        ILogger<DeepSeekService> log,
        ToolRegistry registry,
        IWebView2BridgeService? bridge = null)
    {
        _log = log;
        var apiKey = GetApiKey();
        _functionEngine = string.IsNullOrEmpty(apiKey)
            ? null
            : new FunctionCallingEngine(registry, log, apiKey, bridge);
    }

    private static string KeyFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "io.NET.ZTR_OS", "diagnostic", "deepseek-key.bin");

    public bool IsConfigured => !string.IsNullOrEmpty(GetApiKey());

    public string? GetApiKey()
    {
        try
        {
            if (!File.Exists(KeyFilePath)) return null;
            var enc = File.ReadAllBytes(KeyFilePath);
            var dec = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[DIAG-AI] 读取 API Key 失败");
            return null;
        }
    }

    public (bool Success, string? Error) SetApiKey(string key)
    {
        try
        {
            var dir = Path.GetDirectoryName(KeyFilePath)!;
            Directory.CreateDirectory(dir);
            if (string.IsNullOrWhiteSpace(key))
            {
                if (File.Exists(KeyFilePath)) File.Delete(KeyFilePath);
                return (true, null);
            }
            var enc = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFilePath, enc);
            return (true, null);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[DIAG-AI] 保存 API Key 失败");
            return (false, ex.Message);
        }
    }

    public async Task<DeepSeekAnalysis?> AnalyzeReportAsync(
        DiagnosticReport report, string? userQuestion, CancellationToken ct = default)
    {
        var key = GetApiKey();
        if (string.IsNullOrEmpty(key)) return null;

        try
        {
            var issues = string.Join("\n", report.Checks
                .Where(c => c.Severity >= Severity.Warning)
                .Select(c => $"- [{c.CheckId}] ({c.Severity}) {c.Title}: {c.Detail}"));
            if (issues.Length == 0) issues = "- 未发现 Warning 及以上问题";

            var userContent = string.IsNullOrEmpty(userQuestion)
                ? "请根据以下服务器体检结果给出诊断结论。"
                : $"用户追问：{userQuestion}";
            userContent += "\n\n【体检结果】\n" + issues;

            var body = new
            {
                model = Model,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = userContent },
                },
                response_format = new { type = "json_object" },
                temperature = 0.3,
                max_tokens = 1200,
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            req.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var resp = await Http.SendAsync(req, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("[DIAG-AI] DeepSeek HTTP {Code}: {Raw}",
                    (int)resp.StatusCode, Truncate(raw, 300));
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
            return ParseAnalysis(content, raw);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[DIAG-AI] 调用 DeepSeek 失败");
            return null;
        }
    }

    private static DeepSeekAnalysis? ParseAnalysis(string content, string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var actions = new List<FixAction>();
            if (root.TryGetProperty("recommendedActions", out var acts) && acts.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in acts.EnumerateArray())
                {
                    // 【诚实返回链 P4】fixId 白名单硬校验
                    // 模型幻觉输出不在白名单的 fixId → 降级为 "ai.fix" 并打 Warning
                    var rawFixId = GetStr(a, "fixId");
                    var safeFixId = (rawFixId != null && AllowedFixIds.Contains(rawFixId))
                        ? rawFixId
                        : "ai.fix";
                    if (rawFixId != null && safeFixId == "ai.fix")
                    {
                        Serilog.Log.Warning(
                            "[DIAG-AI] DeepSeek 返回了不在白名单的 fixId '{FixId}'，已降级为 'ai.fix'（诚实返回链）",
                            rawFixId);
                    }

                    actions.Add(new FixAction(
                        safeFixId,
                        GetStr(a, "label") ?? "AI 建议",
                        GetBool(a, "dangerous") ?? false,
                        null,
                        0.5,
                        GetStr(a, "rationale"),
                        new List<FixStep>()));
                }
            }

            return new DeepSeekAnalysis(
                GetStr(root, "summary") ?? "（AI 未返回摘要）",
                GetStrList(root, "keyFindings"),
                actions,
                GetBool(root, "needMoreInfo") ?? false,
                GetStrList(root, "suggestedQuestions"),
                raw);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[DIAG-AI] 解析 DeepSeek 输出失败");
            return null;
        }
    }

    private static string? GetStr(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static bool? GetBool(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True
            ? true
            : e.TryGetProperty(name, out var v2) && v2.ValueKind == JsonValueKind.False ? false : null;

    private static List<string> GetStrList(JsonElement e, string name)
    {
        var list = new List<string>();
        if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in v.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(item.GetString()))
                    list.Add(item.GetString()!);
            }
        }
        return list;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";

    /// <summary>Function Calling AI 诊断入口 —— 多轮工具自主选择</summary>
    public async Task<DeepSeekAnalysis?> AnalyzeWithToolsAsync(string userPrompt, CancellationToken ct = default)
    {
        if (!IsConfigured || _functionEngine == null)
        {
            _log.LogWarning("[DIAG-AI] Function Calling 未配置（API Key 或工具）");
            return null;
        }
        return await _functionEngine.RunAsync(userPrompt, ct).ConfigureAwait(false);
    }
}
