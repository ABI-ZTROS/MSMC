using System.IO;
using Serilog;

namespace io.NET.ZTR_OS.Features.WebView2.Frontend;

/// <summary>
/// 开发模式：本地文件夹提供器
/// 直接从本地 dist 目录加载前端资源（开发调试用）
/// </summary>
public class FolderResourceProvider : IFrontendResourceProvider
{
    private readonly string _basePath;

    /// <inheritdoc />
    public string ModeName => "Folder";

    /// <inheritdoc />
    public bool IsAvailable { get; }

    public FolderResourceProvider(string basePath)
    {
        _basePath = basePath;

        var indexPath = Path.Combine(basePath, "index.html");
        IsAvailable = Directory.Exists(basePath) && File.Exists(indexPath);

        if (IsAvailable)
        {
            Log.Information("本地前端资源目录可用: {Path}", basePath);
        }
    }

    /// <inheritdoc />
    public Task<string?> GetBasePathAsync()
    {
        return Task.FromResult<string?>(IsAvailable ? _basePath : null);
    }

    /// <inheritdoc />
    public Task<Stream?> GetResourceAsync(string relativePath)
    {
        // 文件夹模式下走虚拟主机映射，不需要这个方法
        if (!IsAvailable)
            return Task.FromResult<Stream?>(null);

        var path = relativePath.TrimStart('/');
        var fullPath = Path.Combine(_basePath, path);

        if (File.Exists(fullPath))
        {
            var stream = File.OpenRead(fullPath);
            return Task.FromResult<Stream?>(stream);
        }

        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc />
    public string GetMimeType(string relativePath)
    {
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
}
