using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace io.NET.ZTR_OS.Features.WebView2.Frontend;

/// <summary>
/// C 模式：Zip 解压提供器（兜底方案）
/// 从嵌入的 wwwroot.zip 解压到临时目录，然后用虚拟主机映射加载
/// 兼容性最好，作为 B 模式失败时的兜底
/// </summary>
public class ZipExtractResourceProvider : IFrontendResourceProvider
{
    private readonly Assembly _assembly;
    private readonly string _zipResourceName;
    private string? _extractedPath;

    /// <inheritdoc />
    public string ModeName => "ZipExtract";

    /// <inheritdoc />
    public bool IsAvailable { get; private set; }

    public ZipExtractResourceProvider()
    {
        _assembly = typeof(ZipExtractResourceProvider).Assembly;
        // 复用 EmbeddedResourceProvider 里的枚举兜底逻辑，保持两条链路的资源名解析一致
        _zipResourceName = EmbeddedZipResourceNameResolver.Resolve(_assembly);

        // 检查 zip 资源是否存在
        using var stream = _assembly.GetManifestResourceStream(_zipResourceName);
        IsAvailable = stream != null;

        if (!IsAvailable)
        {
            Log.Warning("未找到 wwwroot.zip 嵌入资源 (期望名称: {Name})", _zipResourceName);
            var allNames = _assembly.GetManifestResourceNames();
            Log.Warning("ZipExtractResourceProvider 程序集嵌入资源清单（共 {Count} 项）:", allNames.Length);
            foreach (var n in allNames)
                Log.Warning("  - {Name}", n);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetBasePathAsync()
    {
        if (!IsAvailable)
            return null;

        if (_extractedPath != null)
            return _extractedPath;

        try
        {
            var zipHash = await ComputeZipHashAsync();
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                "MSMC",
                $"wwwroot_{zipHash[..16]}");

            var markerFile = Path.Combine(tempDir, ".extracted");

            if (Directory.Exists(tempDir) && File.Exists(markerFile))
            {
                Log.Information("前端资源已解压，复用缓存目录: {Dir}", tempDir);
                _extractedPath = tempDir;
                return _extractedPath;
            }

            Log.Information("正在解压前端资源到临时目录: {Dir}", tempDir);

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);

            Directory.CreateDirectory(tempDir);

            using var zipStream = _assembly.GetManifestResourceStream(_zipResourceName)
                ?? throw new InvalidOperationException("wwwroot.zip 资源不存在");

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            archive.ExtractToDirectory(tempDir, overwriteFiles: true);

            // 写入标记文件，表示解压完成
            await File.WriteAllTextAsync(markerFile, DateTime.UtcNow.ToString("O"));

            _extractedPath = tempDir;
            Log.Information("前端资源解压完成，共 {Count} 个文件",
                Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length);

            return _extractedPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "解压前端资源失败");
            IsAvailable = false;
            return null;
        }
    }

    /// <inheritdoc />
    public Task<Stream?> GetResourceAsync(string relativePath)
    {
        // Zip 模式下走虚拟主机映射，不需要这个方法
        // 但接口要求实现，返回 null 即可
        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc />
    public string GetMimeType(string relativePath)
    {
        // Zip 模式下由 WebView2 自己处理 MIME 类型
        // 这里提供一个默认实现
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js" or ".mjs" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            _ => "application/octet-stream",
        };
    }

    private async Task<string> ComputeZipHashAsync()
    {
        using var zipStream = _assembly.GetManifestResourceStream(_zipResourceName)
            ?? throw new InvalidOperationException("wwwroot.zip 资源不存在");

        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(zipStream);
        var sb = new StringBuilder();
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
