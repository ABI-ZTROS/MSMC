// -----------------------------------------------------------------------------
// 文件名: ReadLogTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools
// 功能描述: 读 server.log 最后 N 行
// -----------------------------------------------------------------------------
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools;

public sealed class ReadLogTool : IAiTool, IToolFactory
{
    public string Name => "read_log";
    public string Description => "读取服务器日志文件最后 N 行内容，用于诊断启动失败、崩溃、插件异常等。path 可以是绝对路径（如 C:\\servers\\paper\\server.log）。默认读最后 100 行，最大 500 行。";

    public JsonDocument GetParametersSchema() => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "path": { "type": "string", "description": "日志文件路径，如 C:\\servers\\paper\\server.log" },
            "lines": { "type": "integer", "description": "读最后多少行，默认 100，最大 500" }
        },
        "required": ["path"]
    }
    """);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("path", out var pathEl) || pathEl.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("缺少必需参数 path");

        var path = pathEl.GetString()!;
        var lines = args.TryGetProperty("lines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Number
            ? Math.Clamp(linesEl.GetInt32(), 1, 500)
            : 100;

        try
        {
            if (!File.Exists(path))
                return ToolResult.Fail($"文件不存在: {path}");

            var allLines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
            var tail = allLines.Length <= lines ? allLines : allLines[^lines..];
            var result = new
            {
                path,
                totalLines = allLines.Length,
                returnedLines = tail.Length,
                content = string.Join("\n", tail)
            };
            return ToolResult.Ok(JsonSerializer.Serialize(result));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"读取日志失败: {ex.Message}");
        }
    }
}
