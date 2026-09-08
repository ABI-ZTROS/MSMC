// -----------------------------------------------------------------------------
// 文件名: DownloadCoreTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools
// 功能描述: 下载 Minecraft 服务端核心 jar —— P7 资源原子化 (.part → 下载 → Move)
// 设计原则: P7 资源原子化 —— 先写 .part 临时文件 → 下载完成后 Move 到目标路径，避免半截落地
// -----------------------------------------------------------------------------
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools;

public sealed class DownloadCoreTool : IAiTool, IToolFactory
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    public string Name => "download_core";
    public string Description => "从官方源下载 Minecraft 服务端核心 jar（Paper/Spigot/Forge/Fabric/Quilt/NeoForge）。使用原子化写入：先写 .part 临时文件 → 下载完成后 Move 到目标路径，避免半截落地。";

    public JsonDocument GetParametersSchema() => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "type": { "type": "string", "enum": ["paper","spigot","forge","fabric","quilt","neoForge"], "description": "服务端类型" },
            "version": { "type": "string", "description": "目标版本号，如 1.21.1 或 1.20.4" },
            "outputDir": { "type": "string", "description": "下载到哪个目录" }
        },
        "required": ["type", "version", "outputDir"]
    }
    """);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("缺少必需参数 type");
        if (!args.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("缺少必需参数 version");
        if (!args.TryGetProperty("outputDir", out var o) || o.ValueKind != JsonValueKind.String)
            return ToolResult.Fail("缺少必需参数 outputDir");

        var type = t.GetString()!;
        var version = v.GetString()!;
        var outputDir = o.GetString()!;
        var fileName = $"{type}-{version}.jar";
        var finalPath = Path.Combine(outputDir, fileName);
        var partPath = finalPath + ".part";

        try
        {
            Directory.CreateDirectory(outputDir);

            var downloadUrl = type switch
            {
                "paper" => $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/latest/downloads/paper-{version}.jar",
                _ => throw new NotSupportedException($"暂不支持自动下载 {type}，需手动下载")
            };

            using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return ToolResult.Fail($"下载失败 HTTP {(int)response.StatusCode}");

            // P7 原子化：写 .part → Move
            var content = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(partPath, content, ct).ConfigureAwait(false);

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(partPath, finalPath);

            var result = new
            {
                success = true,
                type, version,
                finalPath,
                sizeBytes = content.Length,
                atomicWrite = true
            };
            return ToolResult.Ok(JsonSerializer.Serialize(result));
        }
        catch (NotSupportedException ex)
        {
            return ToolResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            if (File.Exists(partPath)) { try { File.Delete(partPath); } catch { } }
            return ToolResult.Fail($"下载失败: {ex.Message}");
        }
    }
}
