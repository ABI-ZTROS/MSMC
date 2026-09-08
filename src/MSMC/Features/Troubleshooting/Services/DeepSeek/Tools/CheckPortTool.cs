// -----------------------------------------------------------------------------
// 文件名: CheckPortTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools
// 功能描述: 端口占用检查 —— netstat -ano
// -----------------------------------------------------------------------------
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools;

public sealed class CheckPortTool : IAiTool, IToolFactory
{
    public string Name => "check_port";
    public string Description => "检查指定端口是否被占用、被哪个进程（PID + 进程名）占用。用于诊断 Minecraft 默认端口 25565 冲突。";

    public JsonDocument GetParametersSchema() => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "port": { "type": "integer", "description": "端口号，如 25565" }
        },
        "required": ["port"]
    }
    """);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("port", out var portEl) || portEl.ValueKind != JsonValueKind.Number)
            return Task.FromResult(ToolResult.Fail("缺少必需参数 port"));

        var port = portEl.GetInt32();
        if (port < 1 || port > 65535)
            return Task.FromResult(ToolResult.Fail($"端口超出范围: {port}"));

        var active = IPGlobalProperties.GetIPGlobalProperties();
        var listeners = active.GetActiveTcpListeners();
        var occupant = listeners.FirstOrDefault(p => p.Port == port);

        if (occupant == default)
        {
            var free = new { port, occupied = false };
            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(free)));
        }

        var (pid, procName) = FindProcessForPort(port);
        var occupantInfo = new
        {
            port,
            occupied = true,
            ip = occupant.Address.ToString(),
            pid,
            processName = procName
        };
        return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(occupantInfo)));
    }

    private static (int pid, string name) FindProcessForPort(int port)
    {
        try
        {
            using var netstat = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (netstat == null) return (0, "unknown");

            var output = netstat.StandardOutput.ReadToEnd();
            netstat.WaitForExit();
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains($":{port}") && line.Contains("LISTENING"))
                {
                    var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5 && int.TryParse(parts[^1], out var pid))
                    {
                        try
                        {
                            using var proc = System.Diagnostics.Process.GetProcessById(pid);
                            return (pid, proc.ProcessName);
                        }
                        catch { return (pid, "unknown"); }
                    }
                }
            }
        }
        catch { }
        return (0, "unknown");
    }
}
