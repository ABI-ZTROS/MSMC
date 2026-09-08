// -----------------------------------------------------------------------------
// 文件名: ReadServerPropertiesTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools
// 功能描述: 读 server.properties 并解析 key
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools;

public sealed class ReadServerPropertiesTool : IAiTool, IToolFactory
{
    public string Name => "read_server_properties";
    public string Description => "读取并解析 server.properties 文件，返回所有 key-value 配置对。用于检查 server-port/max-players/online-mode/level-name 等关键配置。";

    public JsonDocument GetParametersSchema() => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "path": { "type": "string", "description": "server.properties 文件路径" }
        },
        "required": ["path"]
    }
    """);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("path", out var p) || p.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("缺少必需参数 path");

        var path = p.GetString()!;
        if (!File.Exists(path)) return ToolResult.Fail($"文件不存在: {path}");

        try
        {
            var dict = new Dictionary<string, string>();
            foreach (var line in await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                var eq = trimmed.IndexOf('=');
                if (eq > 0) dict[trimmed[..eq].Trim()] = trimmed[(eq + 1)..].Trim();
            }
            return ToolResult.Ok(JsonSerializer.Serialize(new { path, properties = dict }));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"解析失败: {ex.Message}");
        }
    }
}
