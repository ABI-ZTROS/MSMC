// 📋 配置文件扫描器 —— 像扫地机器人一样把服务器目录里的配置文件扫个遍
// Minecraft 服务器的配置文件遍布各处：根目录、config/ 子目录、mods/ 目录……
// 有点像你的房间，东西到处都是 🧹
namespace McServerGuard.Services.ServerDetection;

using System.IO;
using Serilog;

/// <summary>
/// 配置文件扫描器 —— 专门在服务器目录里翻箱倒柜找配置文件
/// </summary>
public class ConfigFileScanner
{
    /// <summary>配置文件扩展名扫描列表（不含 .toml，因为没有 TOML 解析器）</summary>
    private static readonly string[] ConfigFileExtensions = ["*.yml", "*.yaml", "*.properties", "*.json"];

    /// <summary>
    /// 扫描服务器工作目录下的所有配置文件
    /// 搜索范围：
    ///   - 根目录（server.properties, bukkit.yml, spigot.yml 等）
    ///   - config/ 子目录（Paper 的配置都在这里）
    ///   - mods/ 目录（模组的配置也可能会出现在这里）
    ///   - plugins/ 目录下的各插件子目录
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录</param>
    /// <returns>找到的所有配置文件的完整路径列表</returns>
    public List<string> ScanAll(string workingDirectory)
    {
        var configFiles = new List<string>();

        if (!Directory.Exists(workingDirectory))
        {
            Log.Warning("工作目录不存在: {Dir}", workingDirectory);
            return configFiles;
        }

        Log.Information("📂 ConfigFileScanner: 扫描配置文件目录: {Dir}", workingDirectory);

        // 1. 扫描根目录的配置文件
        ScanDirectory(workingDirectory, configFiles, "根目录");

        // 2. 扫描 config/ 子目录 —— Paper 服务器的主要配置都在这
        var configDir = Path.Combine(workingDirectory, "config");
        if (Directory.Exists(configDir))
        {
            ScanDirectory(configDir, configFiles, "config/");
            ScanSubDirectories(configDir, configFiles, "config/", maxDepth: 2);
        }

        // 3. 扫描 mods/ 目录 —— 模组自身的配置文件
        var modsDir = Path.Combine(workingDirectory, "mods");
        if (Directory.Exists(modsDir))
        {
            ScanDirectory(modsDir, configFiles, "mods/");
        }

        // 4. 扫描 plugins/ 目录下的各插件子目录
        var pluginsDir = Path.Combine(workingDirectory, "plugins");
        if (Directory.Exists(pluginsDir))
        {
            ScanDirectory(pluginsDir, configFiles, "plugins/");
            ScanSubDirectories(pluginsDir, configFiles, "plugins/", maxDepth: 2);
        }

        // 5. 扫描 world/ 目录下的一些配置（有些世界会有自己的配置）
        var worldDir = Path.Combine(workingDirectory, "world");
        if (Directory.Exists(worldDir))
        {
            ScanDirectory(worldDir, configFiles, "world/");
        }

        Log.Information("✅ 配置文件扫描完成，共发现 {Count} 个文件", configFiles.Count);
        return configFiles;
    }

    /// <summary>
    /// 在指定目录中扫描所有匹配扩展名的配置文件
    /// </summary>
    private void ScanDirectory(string directory, List<string> results, string label)
    {
        Log.Debug("🔄 扫描目录: {Dir}", directory);
        try
        {
            foreach (var pattern in ConfigFileExtensions)
            {
                var files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    if (ShouldIgnoreFile(file))
                        continue;

                    results.Add(file);
                    Log.Debug("📄 发现配置文件: {File}", file);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            Log.Warning("⚠️ 权限不足，跳过目录: {Dir}", directory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 扫描目录 {Dir} 失败: {Message}", directory, ex.Message);
        }
    }

    private void ScanSubDirectories(string parentDir, List<string> results, string label, int maxDepth)
    {
        ScanSubDirectoriesRecursive(parentDir, results, label, currentDepth: 0, maxDepth);
    }

    private void ScanSubDirectoriesRecursive(
        string directory, List<string> results, string label, int currentDepth, int maxDepth)
    {
        if (currentDepth >= maxDepth)
            return;

        try
        {
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var dirInfo = new DirectoryInfo(subDir);
                if (dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                ScanDirectory(subDir, results, $"{label}{dirInfo.Name}/");
                ScanSubDirectoriesRecursive(subDir, results, label, currentDepth + 1, maxDepth);
            }
        }
        catch (UnauthorizedAccessException)
        {
            Log.Warning("⚠️ 权限不足，跳过目录: {Dir}", directory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 扫描目录 {Dir} 失败: {Message}", directory, ex.Message);
        }
    }

    /// <summary>
    /// 递归扫描子目录的入口
    /// </summary>
    public async Task<List<string>> ScanAllAsync(string workingDirectory)
    {
        var configFiles = new List<string>();

        if (!Directory.Exists(workingDirectory))
        {
            Log.Warning("工作目录不存在: {Dir}", workingDirectory);
            return configFiles;
        }

        Log.Information("📂 ConfigFileScanner: 扫描配置文件目录: {Dir}", workingDirectory);

        await ScanDirectoryAsync(workingDirectory, configFiles, "根目录");

        var configDir = Path.Combine(workingDirectory, "config");
        if (Directory.Exists(configDir))
        {
            await ScanDirectoryAsync(configDir, configFiles, "config/");
            await ScanSubDirectoriesAsync(configDir, configFiles, "config/", maxDepth: 2);
        }

        var modsDir = Path.Combine(workingDirectory, "mods");
        if (Directory.Exists(modsDir))
        {
            await ScanDirectoryAsync(modsDir, configFiles, "mods/");
        }

        var pluginsDir = Path.Combine(workingDirectory, "plugins");
        if (Directory.Exists(pluginsDir))
        {
            await ScanDirectoryAsync(pluginsDir, configFiles, "plugins/");
            await ScanSubDirectoriesAsync(pluginsDir, configFiles, "plugins/", maxDepth: 2);
        }

        var worldDir = Path.Combine(workingDirectory, "world");
        if (Directory.Exists(worldDir))
        {
            await ScanDirectoryAsync(worldDir, configFiles, "world/");
        }

        Log.Information("✅ 配置文件扫描完成，共发现 {Count} 个文件", configFiles.Count);
        return configFiles;
    }

    private async Task ScanDirectoryAsync(string directory, List<string> results, string label)
    {
        Log.Debug("🔄 扫描目录: {Dir}", directory);
        try
        {
            foreach (var pattern in ConfigFileExtensions)
            {
                foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
                {
                    if (ShouldIgnoreFile(file))
                        continue;

                    results.Add(file);
                    Log.Debug("📄 发现配置文件: {File}", file);
                }
                await Task.Yield();
            }
        }
        catch (UnauthorizedAccessException)
        {
            Log.Warning("⚠️ 权限不足，跳过目录: {Dir}", directory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 扫描目录 {Dir} 失败: {Message}", directory, ex.Message);
        }
    }

    private async Task ScanSubDirectoriesAsync(string parentDir, List<string> results, string label, int maxDepth)
    {
        await ScanSubDirectoriesRecursiveAsync(parentDir, results, label, currentDepth: 0, maxDepth);
    }

    private async Task ScanSubDirectoriesRecursiveAsync(
        string directory, List<string> results, string label, int currentDepth, int maxDepth)
    {
        if (currentDepth >= maxDepth)
            return;

        try
        {
            foreach (var subDir in Directory.EnumerateDirectories(directory))
            {
                var dirInfo = new DirectoryInfo(subDir);
                if (dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                    continue;

                await ScanDirectoryAsync(subDir, results, $"{label}{dirInfo.Name}/");
                await ScanSubDirectoriesRecursiveAsync(subDir, results, label, currentDepth + 1, maxDepth);
            }
        }
        catch (UnauthorizedAccessException)
        {
            Log.Warning("⚠️ 权限不足，跳过目录: {Dir}", directory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 扫描目录 {Dir} 失败: {Message}", directory, ex.Message);
        }
    }

    /// <summary>
    /// 判断是否应该忽略该文件
    /// 有些文件虽然扩展名匹配但跟服务器配置没关系，比如日志文件
    /// </summary>
    private static bool ShouldIgnoreFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        if (fileName.Contains("log") && fileName.EndsWith(".json"))
            return true;

        return fileName switch
        {
            "eula.txt" => true,
            "README.md" => true,
            "LICENSE" => true,
            _ => false,
        };
    }
}
