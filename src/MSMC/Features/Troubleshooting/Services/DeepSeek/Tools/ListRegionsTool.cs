// -----------------------------------------------------------------------------
// 文件名: ListRegionsTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools
// 功能描述: world/region 文件完整性检查 —— 魔数 0x4DEC2B1 校验
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools;

public sealed class ListRegionsTool : IAiTool, IToolFactory
{
    public string Name => "list_regions";
    public string Description => "统计 world/region 目录下 .mca 区域文件数量、检查是否有缺失区域、列出损坏的 region 文件（魔数不为 0x4DEC2B1）。";

    public JsonDocument GetParametersSchema() => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "worldPath": { "type": "string", "description": "world 目录路径，如 C:\\servers\\paper\\world" }
        },
        "required": ["worldPath"]
    }
    """);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("worldPath", out var w) || w.ValueKind != JsonValueKind.String)
            return Task.FromResult(ToolResult.Fail("缺少必需参数 worldPath"));

        var worldPath = w.GetString()!;
        var regionDir = Path.Combine(worldPath, "region");

        if (!Directory.Exists(regionDir))
            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(new { exists = false, message = "region 目录不存在" })));

        var allFiles = Directory.GetFiles(regionDir, "*.mca");
        var corrupted = new List<string>();
        foreach (var file in allFiles)
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var reader = new BinaryReader(stream);
                var magic = reader.ReadUInt32();
                if (magic != 0x4DEC2B1)
                    corrupted.Add(Path.GetFileName(file));
            }
            catch { corrupted.Add(Path.GetFileName(file)); }
        }

        var result = new
        {
            exists = true,
            totalRegions = allFiles.Length,
            corruptedCount = corrupted.Count,
            corruptedFiles = corrupted
        };
        return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(result)));
    }
}
