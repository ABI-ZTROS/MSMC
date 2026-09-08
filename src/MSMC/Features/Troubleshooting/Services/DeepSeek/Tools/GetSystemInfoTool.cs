// -----------------------------------------------------------------------------
// 文件名: GetSystemInfoTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools
// 功能描述: 系统基本信息 —— OS / CPU / 内存 / C 盘剩余空间
// -----------------------------------------------------------------------------
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools;

public sealed class GetSystemInfoTool : IAiTool, IToolFactory
{
    public string Name => "get_system_info";
    public string Description => "获取当前系统基本信息：操作系统版本、CPU 核心数、当前进程内存、C 盘剩余空间、是否 64 位。用于诊断资源不足类故障。";

    public JsonDocument GetParametersSchema() => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {},
        "required": []
    }
    """);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        try
        {
            var os = Environment.OSVersion;
            var cpu = Environment.ProcessorCount;
            var procMem = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;

            string? cDriveFree = null;
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.Name.StartsWith("C:") && drive.IsReady)
                    {
                        cDriveFree = drive.AvailableFreeSpace.ToString();
                        break;
                    }
                }
            }
            catch { }

            var result = new
            {
                osVersion = os.VersionString,
                osPlatform = os.Platform.ToString(),
                cpuCores = cpu,
                processMemoryBytes = procMem,
                cDriveFreeBytes = cDriveFree,
                is64Bit = Environment.Is64BitOperatingSystem
            };
            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(result)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"获取系统信息失败: {ex.Message}"));
        }
    }
}
