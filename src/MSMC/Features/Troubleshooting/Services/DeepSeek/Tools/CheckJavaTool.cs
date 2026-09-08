// -----------------------------------------------------------------------------
// 文件名: CheckJavaTool.cs
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools
// 功能描述: Java 版本 + JAVA_HOME + 已安装 JDK 列表
// -----------------------------------------------------------------------------
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using io.NET.ZTR_OS.Features.JavaInstallation.Services;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services.DeepSeek.Tools;

public sealed class CheckJavaTool : IAiTool, IToolFactory
{
    private readonly IJavaFinderService _javaFinder;

    public CheckJavaTool(IJavaFinderService javaFinder) => _javaFinder = javaFinder;

    public string Name => "check_java";
    public string Description => "检查当前系统的 Java 版本、JAVA_HOME 环境变量、已安装的 JDK 列表。用于诊断 JDK 版本不兼容或未安装 Java 导致启动失败。";

    public JsonDocument GetParametersSchema() => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "path": { "type": "string", "description": "可选：指定某个 Java 路径来检查，不填则检查 JAVA_HOME 和 PATH" }
        },
        "required": []
    }
    """);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        var path = args.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        try
        {
            var home = Environment.GetEnvironmentVariable("JAVA_HOME");
            var javaExe = path ?? "java";
            string? version = null;

            try
            {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null)
                {
                    await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                    version = (await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false)).Trim();
                }
            }
            catch { version = null; }

            var result = new
            {
                javaHome = home,
                javaPath = path ?? "(JAVA_HOME/PATH)",
                currentVersion = version,
                installedJdks = _javaFinder.FindAll().Select(j => new
                {
                    jdkPath = j.JavaHome,
                    jdkVersion = j.Version?.ToString() ?? "unknown",
                    jdkVendor = j.Vendor
                }).ToList()
            };
            return ToolResult.Ok(JsonSerializer.Serialize(result));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"检查 Java 失败: {ex.Message}");
        }
    }
}
