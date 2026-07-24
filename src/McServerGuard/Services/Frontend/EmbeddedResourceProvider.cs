using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Serilog;

namespace McServerGuard.Services.Frontend;

/// <summary>
/// B 模式：内存 Zip 资源提供器
/// 从嵌入的 wwwroot.zip 读取前端资源，配合 WebResourceRequested 拦截使用
/// 零磁盘写入，纯内存提供
/// </summary>
public class EmbeddedResourceProvider : IFrontendResourceProvider
{
    private readonly Assembly _assembly;
    private readonly string _zipResourceName;
    private readonly ConcurrentDictionary<string, ZipArchiveEntry> _entryMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _mimeMap = CreateMimeMap();
    private ZipArchive? _archive;

    /// <inheritdoc />
    public string ModeName => "EmbeddedZip";

    /// <inheritdoc />
    public bool IsAvailable { get; private set; }

    public EmbeddedResourceProvider()
    {
        _assembly = typeof(EmbeddedResourceProvider).Assembly;
        _zipResourceName = $"{_assembly.GetName().Name}.wwwroot.zip";

        LoadZipArchive();
    }

    private void LoadZipArchive()
    {
        try
        {
            var stream = _assembly.GetManifestResourceStream(_zipResourceName);
            if (stream == null)
            {
                Log.Warning("未找到嵌入资源: {Name}", _zipResourceName);
                IsAvailable = false;
                return;
            }

            // 将资源流复制到内存流，支持随机访问
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            _archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            // 建立路径 → 条目 映射
            foreach (var entry in _archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue; // 跳过目录条目

                // 统一路径格式：/ 开头，前向斜杠
                var path = "/" + entry.FullName.Replace('\\', '/').TrimStart('/');
                _entryMap[path] = entry;
            }

            IsAvailable = _entryMap.Count > 0;
            Log.Information("已加载 {Count} 个嵌入资源 (zip 模式)", _entryMap.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载嵌入 Zip 资源失败");
            IsAvailable = false;
        }
    }

    /// <inheritdoc />
    public Task<string?> GetBasePathAsync()
    {
        // 内存模式下，没有本地文件夹，返回 null 表示使用拦截模式
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task<Stream?> GetResourceAsync(string relativePath)
    {
        if (!IsAvailable || _archive == null)
            return Task.FromResult<Stream?>(null);

        // 规范化路径
        var path = relativePath.TrimStart('/');
        if (string.IsNullOrEmpty(path))
            path = "index.html";

        var key = $"/{path}";

        // 直接查找
        if (_entryMap.TryGetValue(key, out var entry))
        {
            return Task.FromResult<Stream?>(entry.Open());
        }

        // 尝试不带前导斜杠
        if (_entryMap.TryGetValue(path, out entry))
        {
            return Task.FromResult<Stream?>(entry.Open());
        }

        // 兜底：目录路径 → index.html
        if (!path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains('.'))
        {
            var indexPath = $"/{path}/index.html".Replace("//", "/");
            if (_entryMap.TryGetValue(indexPath, out entry))
            {
                return Task.FromResult<Stream?>(entry.Open());
            }
        }

        Log.Debug("嵌入资源未找到: {Path}", relativePath);
        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc />
    public string GetMimeType(string relativePath)
    {
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();
        return _mimeMap.TryGetValue(ext, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }

    private static ConcurrentDictionary<string, string> CreateMimeMap()
    {
        return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".htm"] = "text/html; charset=utf-8",
            [".js"] = "application/javascript; charset=utf-8",
            [".mjs"] = "application/javascript; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".svg"] = "image/svg+xml",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".ico"] = "image/x-icon",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
            [".ttf"] = "font/ttf",
            [".otf"] = "font/otf",
            [".eot"] = "application/vnd.ms-fontobject",
            [".map"] = "application/json; charset=utf-8",
            [".txt"] = "text/plain; charset=utf-8",
            [".xml"] = "application/xml; charset=utf-8",
            [".wasm"] = "application/wasm",
        };
    }
}
