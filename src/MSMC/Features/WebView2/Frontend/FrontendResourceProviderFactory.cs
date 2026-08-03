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

        Log.Information("[FIND] 正在查找前端资源提供器...");

        // 1. 优先：开发环境本地文件夹
        var folderProvider = TryCreateFolderProvider();
        if (folderProvider != null && folderProvider.IsAvailable)
        {
            Log.Information("[OK] 使用本地文件夹模式加载前端");
            _cachedProvider = folderProvider;
            return _cachedProvider;
        }

        // 2. 其次：嵌入资源（B 模式）
        var embeddedProvider = new EmbeddedResourceProvider();
        if (embeddedProvider.IsAvailable)
        {
            Log.Information("[OK] 使用嵌入资源模式加载前端 (B 模式)");
            _cachedProvider = embeddedProvider;
            return _cachedProvider;
        }

        // 3. 兜底：Zip 解压（C 模式）
        var zipProvider = new ZipExtractResourceProvider();
        if (zipProvider.IsAvailable)
        {
            Log.Information("[OK] 使用 Zip 解压模式加载前端 (C 模式/兜底)");
            _cachedProvider = zipProvider;
            return _cachedProvider;
        }

        // 4. 都不行，返回一个不可用的提供器
        Log.Warning("[WARN] 未找到任何可用的前端资源提供器，将加载测试页面");
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
        // 封装一个安全的 Combine, 跳过任何 null 或空的片段防止 Path.Combine 直接炸
        static string SafeCombine(params string?[] parts)
        {
            var nonEmpty = parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
            return nonEmpty.Count == 0 ? string.Empty : Path.Combine(nonEmpty.ToArray()!);
        }

        // 1. 程序输出目录下的 wwwroot （CopyToOutputDirectory 复制过去的）
        yield return SafeCombine(AppContext.BaseDirectory, "wwwroot");

        // 2. 工作目录（开发时 dotnet run 的当前目录可能在项目根）
        var curDir = Environment.CurrentDirectory;
        yield return SafeCombine(curDir, "wwwroot");
        yield return SafeCombine(curDir, "dist");
        yield return SafeCombine(curDir, "src", "frontend", "dist");
        // Path.GetDirectoryName(根目录) 返回 null，必须用 SafeCombine 过滤
        var parentOfCur = Path.GetDirectoryName(curDir);
        if (!string.IsNullOrEmpty(parentOfCur))
            yield return SafeCombine(parentOfCur, "src", "frontend", "dist");

        // 3. 从 AppContext.BaseDirectory 向上逐级搜索（覆盖 bin/Debug/netX.X/rid 这样的深目录）
        //    每一层都尝试拼接 ../src/frontend/dist 和 ../../src/frontend/dist 等
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        var searchDir = baseDir;
        var depth = 0;
        while (searchDir != null && depth++ < 12)
        {
            // a) 经典结构：sln -> src/frontend/dist
            var p1 = SafeCombine(searchDir.FullName, "src", "frontend", "dist");
            if (Directory.Exists(Path.GetDirectoryName(p1)))
                yield return p1;

            // b) 扁平结构：sln -> frontend/dist
            var p2 = SafeCombine(searchDir.FullName, "frontend", "dist");
            if (Directory.Exists(Path.GetDirectoryName(p2)))
                yield return p2;

            // c) 直接把该层 wwwroot 也纳入（有人会手动复制一份）
            var p3 = SafeCombine(searchDir.FullName, "wwwroot");
            yield return p3;

            searchDir = searchDir.Parent;
        }

        // 4. 兜底：用 FindSolutionDir 的结果（保留原逻辑）
        var solutionDir = FindSolutionDir();
        if (solutionDir != null)
        {
            yield return SafeCombine(solutionDir, "src", "frontend", "dist");
            yield return SafeCombine(solutionDir, "frontend", "dist");
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
