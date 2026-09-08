// -----------------------------------------------------------------------------
// 文件名: SearchDocTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools
// 功能描述: 联网搜 Minecraft 官方文档 —— 超时 10s，网络不通时返回失败
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools;

public sealed class SearchDocTool : IAiTool, IToolFactory
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public string Name => "search_doc";
    public string Description => "联网搜索 Minecraft 官方文档（Paper/Spigot/Forge/Fabric wiki）。当遇到不认识的错误码、参数格式、插件 API 时用。超时 10 秒，网络不通时返回失败。";

    public JsonDocument GetParametersSchema() => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "query": { "type": "string", "description": "搜索关键词，如 'OutOfMemoryError Cannot allocate zero bytes'" },
            "source": { "type": "string", "enum": ["paper","spigot","forge","fabric","general"], "description": "搜索来源，默认 general" }
        },
        "required": ["query"]
    }
    """);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("query", out var q) || q.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("缺少必需参数 query");

        var query = q.GetString()!;
        var source = args.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()! : "general";

        var searchUrls = source switch
        {
            "paper" => new[] { $"https://docs.papermc.io/_next/data/v1/search.json?q={Uri.EscapeDataString(query)}" },
            "spigot" => new[] { $"https://hub.spigotmc.org/javadocs/spigot/search?q={Uri.EscapeDataString(query)}" },
            _ => new[]
            {
                $"https://www.google.com/search?q=site%3Adocs.papermc.io+{Uri.EscapeDataString(query)}",
                $"https://www.google.com/search?q=site%3Aminecraft.wiki+{Uri.EscapeDataString(query)}"
            }
        };

        try
        {
            var results = new List<object>();
            foreach (var url in searchUrls.Take(2))
            {
                try
                {
                    using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                        results.Add(new { source = source, url, status = (int)resp.StatusCode });
                }
                catch { /* 某个源失败不影响其他 */ }
            }

            if (results.Count == 0)
                return ToolResult.Fail("所有搜索源均失败，请检查网络");

            return ToolResult.Ok(JsonSerializer.Serialize(new { query, source, results }));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"搜索失败: {ex.Message}");
        }
    }
}
