using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Serilog;

namespace io.NET.ZTR_OS.Features.WebView2.Frontend;

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
        // 优先尝试 AssemblyName.wwwroot.zip（csproj LogicalName 显式指定的名字）
        _zipResourceName = EmbeddedZipResourceNameResolver.Resolve(_assembly);

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
                // 再输出一次所有嵌入资源名，方便用户在 VS 输出面板直接查看
                var allNames = _assembly.GetManifestResourceNames();
                Log.Warning("程序集 {Asm} 当前嵌入资源清单（共 {Count} 项）:",
                    _assembly.GetName().Name, allNames.Length);
                foreach (var n in allNames)
                    Log.Warning("  - {Name}", n);
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

/// <summary>
/// wwwroot.zip 嵌入资源名解析器（EmbeddedResourceProvider 与 ZipExtractResourceProvider 共用）。
/// 依次尝试：AssemblyName → RootNamespace → 旧命名 McServerGuard → 模糊匹配 EndsWith wwwroot.zip。
/// 找不到时把所有嵌入资源名列到 WARNING 日志，用户在 VS 输出面板一眼看到实际名字，
/// 避免猜 LogicalName 前缀到底用的是哪个。
/// </summary>
internal static class EmbeddedZipResourceNameResolver
{
    public static string Resolve(Assembly asm)
    {
        var asmName = asm.GetName().Name ?? string.Empty;
        var candidates = new List<string>
        {
            $"{asmName}.wwwroot.zip",
            "io.NET.ZTR_OS.wwwroot.zip",
            "McServerGuard.wwwroot.zip",
        };

        // 1. 精确匹配候选
        foreach (var cand in candidates)
        {
            using var s = asm.GetManifestResourceStream(cand);
            if (s != null)
            {
                Log.Information("[PKG] 嵌入资源精确命中: {Name}", cand);
                return cand;
            }
        }

        // 2. 模糊匹配：任何以 "wwwroot.zip" 结尾的嵌入资源
        var allNames = asm.GetManifestResourceNames();
        var matched = allNames.FirstOrDefault(n =>
            n.EndsWith("wwwroot.zip", StringComparison.OrdinalIgnoreCase));

        if (matched != null)
        {
            Log.Warning("[PKG] 嵌入资源精确名未命中，回退模糊匹配: {Matched}", matched);
            Log.Warning("[LOG] 程序集 {Asm} 全部嵌入资源清单（排查 LogicalName 用）:", asmName);
            foreach (var n in allNames)
                Log.Warning("  • {Name}", n);
            return matched;
        }

        // 3. 实在找不到：列全部资源名到日志，返回最可能的默认名
        Log.Warning("[PKG] 程序集 {Asm} 中未找到任何 wwwroot.zip 嵌入资源。", asmName);
        if (allNames.Length == 0)
        {
            Log.Warning("[LOG] （程序集没有任何嵌入资源，通常意味着 csproj 的 PackFrontendToZip Target 没有执行）");
        }
        else
        {
            Log.Warning("[LOG] 程序集 {Asm} 当前所有嵌入资源清单（共 {Count} 项）:", asmName, allNames.Length);
            foreach (var n in allNames)
                Log.Warning("  - {Name}", n);
        }
        return $"{asmName}.wwwroot.zip";
    }
}
