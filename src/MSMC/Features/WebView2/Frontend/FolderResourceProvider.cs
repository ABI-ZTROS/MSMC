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
        // 说明：Folder 模式下主通路走 SetVirtualHostNameToFolderMapping（不调此方法），
        // 但某些兜底/辅助调用路径（如 fallback 资源探测/懒加载拦截器）会走这里，
        // 因此仍然做防御性规范化，与 EmbeddedResourceProvider 保持一致。
        if (!IsAvailable)
            return Task.FromResult<Stream?>(null);

        // ── 防御性规范化（与 WebView2BridgeService / EmbeddedResourceProvider 保持一致）
        var rp = relativePath;
        // ① 剥离 query(?xxx) 和 fragment(#xxx)
        int qIdx = rp.IndexOf('?');
        int hIdx = rp.IndexOf('#');
        int stripTo = rp.Length;
        if (qIdx >= 0) stripTo = Math.Min(stripTo, qIdx);
        if (hIdx >= 0) stripTo = Math.Min(stripTo, hIdx);
        if (stripTo < rp.Length)
            rp = rp[..stripTo];
        // ② 合并连续斜杠 // → /
        while (rp.Contains("//"))
            rp = rp.Replace("//", "/");
        // ③ 去掉首尾多余的 / \
        rp = rp.TrimStart('/', '\\').TrimEnd('/', '\\');
        if (string.IsNullOrEmpty(rp))
            rp = "index.html";

        var fullPath = Path.Combine(_basePath, rp);

        if (File.Exists(fullPath))
        {
            var stream = File.OpenRead(fullPath);
            return Task.FromResult<Stream?>(stream);
        }

        Log.Debug("FolderResourceProvider 未找到文件: {RelativePath} (full={FullPath})", relativePath, fullPath);
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
