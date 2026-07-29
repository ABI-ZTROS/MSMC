using System.IO;
using Serilog;

namespace io.NET.ZTR_OS.Features.WebView2.Frontend;

/// <summary>
/// 前端资源提供器工厂
/// 按优先级选择最合适的资源提供器
/// 优先级：开发文件夹 > 嵌入资源(B模式) > Zip解压(C模式)
/// </summary>
public static class FrontendResourceProviderFactory
{
    private static IFrontendResourceProvider? _cachedProvider;

    /// <summary>
    /// 获取当前环境下最优的前端资源提供器
    /// </summary>
    public static IFrontendResourceProvider Create()
    {
        if (_cachedProvider != null)
            return _cachedProvider;

        Log.Information("🔍 正在查找前端资源提供器...");

        // 1. 优先：开发环境本地文件夹
        var folderProvider = TryCreateFolderProvider();
        if (folderProvider != null && folderProvider.IsAvailable)
        {
            Log.Information("✅ 使用本地文件夹模式加载前端");
            _cachedProvider = folderProvider;
            return _cachedProvider;
        }

        // 2. 其次：嵌入资源（B 模式）
        var embeddedProvider = new EmbeddedResourceProvider();
        if (embeddedProvider.IsAvailable)
        {
            Log.Information("✅ 使用嵌入资源模式加载前端 (B 模式)");
            _cachedProvider = embeddedProvider;
            return _cachedProvider;
        }

        // 3. 兜底：Zip 解压（C 模式）
        var zipProvider = new ZipExtractResourceProvider();
        if (zipProvider.IsAvailable)
        {
            Log.Information("✅ 使用 Zip 解压模式加载前端 (C 模式/兜底)");
            _cachedProvider = zipProvider;
            return _cachedProvider;
        }

        // 4. 都不行，返回一个不可用的提供器
        Log.Warning("⚠️ 未找到任何可用的前端资源提供器，将加载测试页面");
        _cachedProvider = new NullResourceProvider();
        return _cachedProvider;
    }

    private static FolderResourceProvider? TryCreateFolderProvider()
    {
        // 尝试多个可能的路径
        var candidates = GetCandidatePaths();

        foreach (var path in candidates)
        {
            try
            {
                var provider = new FolderResourceProvider(path);
                if (provider.IsAvailable)
                    return provider;
            }
            catch (Exception ex)
            {
                Log.Debug("检查前端目录失败 {Path}: {Error}", path, ex.Message);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        // 1. 程序目录下的 wwwroot
        yield return Path.Combine(AppContext.BaseDirectory, "wwwroot");

        // 2. 开发环境：尝试找解决方案目录下的 src/frontend/dist
        var solutionDir = FindSolutionDir();
        if (solutionDir != null)
        {
            yield return Path.Combine(solutionDir, "src", "frontend", "dist");
        }
    }

    private static string? FindSolutionDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        var maxDepth = 10;
        while (dir != null && maxDepth-- > 0)
        {
            try
            {
                if (Directory.GetFiles(dir.FullName, "*.sln").Length > 0)
                    return dir.FullName;
            }
            catch (UnauthorizedAccessException)
            {
                break;
            }
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// 重置缓存（测试用）
    /// </summary>
    public static void ResetCache()
    {
        _cachedProvider = null;
    }
}

/// <summary>
/// 空提供器（所有方式都失败时使用）
/// </summary>
internal class NullResourceProvider : IFrontendResourceProvider
{
    public string ModeName => "None";
    public bool IsAvailable => false;

    public Task<string?> GetBasePathAsync() => Task.FromResult<string?>(null);
    public Task<Stream?> GetResourceAsync(string relativePath) => Task.FromResult<Stream?>(null);
    public string GetMimeType(string relativePath) => "text/plain";
}
