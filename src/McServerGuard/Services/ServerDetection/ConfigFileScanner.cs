// -----------------------------------------------------------------------------
// 文件名: ConfigFileScanner.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 配置文件扫描器，在服务器目录中递归扫描并识别所有配置文件
// 依赖组件: System.IO, Serilog
// 设计模式: 递归遍历、文件指纹匹配、目录深度控制
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ServerDetection;

using System.IO;
using Serilog;

/// <summary>
/// 配置文件扫描器
/// </summary>
/// <remarks>
/// 在指定服务器工作目录下进行多层级配置文件扫描，
/// 支持 YAML、Properties、JSON 等多种配置格式。
/// 扫描范围涵盖根目录、config/、mods/、plugins/、world/ 等
/// 常见配置分布目录，并具备隐藏目录过滤和深度控制能力。
/// </remarks>
public class ConfigFileScanner
{
    /// <summary>
    /// 配置文件扩展名匹配模式列表
    /// </summary>
    /// <remarks>不含 .toml 格式（当前版本未集成 TOML 解析器）</remarks>
    private static readonly string[] ConfigFileExtensions = ["*.yml", "*.yaml", "*.properties", "*.json"];

    /// <summary>
    /// 同步扫描服务器工作目录下的所有配置文件
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录的绝对路径</param>
    /// <returns>所有匹配配置文件的完整路径列表</returns>
    /// <remarks>
    /// 扫描范围包括：
    /// 1. 根目录（server.properties、bukkit.yml、spigot.yml 等核心配置）
    /// 2. config/ 子目录及其下级目录（Paper 全局配置）
    /// 3. mods/ 目录（模组配置文件）
    /// 4. plugins/ 目录及其下级目录（插件配置文件）
    /// 5. world/ 目录（世界级配置文件）
    /// </remarks>
    public List<string> ScanAll(string workingDirectory)
    {
        var configFiles = new List<string>();

        if (!Directory.Exists(workingDirectory))
        {
            Log.Warning("工作目录不存在: {Dir}", workingDirectory);
            return configFiles;
        }

        Log.Information("ConfigFileScanner: 扫描配置文件目录: {Dir}", workingDirectory);

        // 扫描根目录配置文件
        ScanDirectory(workingDirectory, configFiles, "根目录");

        // 扫描 config/ 子目录（Paper 服务器主要配置）
        var configDir = Path.Combine(workingDirectory, "config");
        if (Directory.Exists(configDir))
        {
            ScanDirectory(configDir, configFiles, "config/");
            ScanSubDirectories(configDir, configFiles, "config/", maxDepth: 2);
        }

        // 扫描 mods/ 目录（模组配置文件）
        var modsDir = Path.Combine(workingDirectory, "mods");
        if (Directory.Exists(modsDir))
        {
            ScanDirectory(modsDir, configFiles, "mods/");
        }

        // 扫描 plugins/ 目录下的各插件子目录
        var pluginsDir = Path.Combine(workingDirectory, "plugins");
        if (Directory.Exists(pluginsDir))
        {
            ScanDirectory(pluginsDir, configFiles, "plugins/");
            ScanSubDirectories(pluginsDir, configFiles, "plugins/", maxDepth: 2);
        }

        // 扫描 world/ 目录（世界级配置）
        var worldDir = Path.Combine(workingDirectory, "world");
        if (Directory.Exists(worldDir))
        {
            ScanDirectory(worldDir, configFiles, "world/");
        }

        Log.Information("配置文件扫描完成，共发现 {Count} 个文件", configFiles.Count);
        return configFiles;
    }

    /// <summary>
    /// 在指定目录中扫描所有匹配扩展名的配置文件
    /// </summary>
    /// <param name="directory">待扫描的目录路径</param>
    /// <param name="results">结果收集列表</param>
    /// <param name="label">目录标签，用于日志标识</param>
    private void ScanDirectory(string directory, List<string> results, string label)
    {
        Log.Debug("扫描目录: {Dir}", directory);
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
                    Log.Debug("发现配置文件: {File}", file);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            Log.Warning("权限不足，跳过目录: {Dir}", directory);
        }
        catch (IOException ex)
        {
            Log.Debug(ex, "IO 跳过: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "扫描目录跳过: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 递归扫描子目录的入口方法
    /// </summary>
    /// <param name="parentDir">父目录路径</param>
    /// <param name="results">结果收集列表</param>
    /// <param name="label">目录标签前缀</param>
    /// <param name="maxDepth">最大递归深度</param>
    private void ScanSubDirectories(string parentDir, List<string> results, string label, int maxDepth)
    {
        ScanSubDirectoriesRecursive(parentDir, results, label, currentDepth: 0, maxDepth);
    }

    /// <summary>
    /// 递归扫描子目录的核心实现
    /// </summary>
    /// <param name="directory">当前目录路径</param>
    /// <param name="results">结果收集列表</param>
    /// <param name="label">目录标签前缀</param>
    /// <param name="currentDepth">当前递归深度</param>
    /// <param name="maxDepth">最大递归深度</param>
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
            Log.Warning("权限不足，跳过目录: {Dir}", directory);
        }
        catch (IOException ex)
        {
            Log.Debug(ex, "IO 跳过: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "扫描目录跳过: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 异步扫描服务器工作目录下的所有配置文件
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录的绝对路径</param>
    /// <returns>所有匹配配置文件的完整路径列表</returns>
    /// <remarks>
    /// 异步版本，通过 Task.Yield 实现协作式调度，
    /// 避免长时间阻塞调用线程。扫描范围与同步版本一致。
    /// </remarks>
    public async Task<List<string>> ScanAllAsync(string workingDirectory)
    {
        var configFiles = new List<string>();

        if (!Directory.Exists(workingDirectory))
        {
            Log.Warning("工作目录不存在: {Dir}", workingDirectory);
            return configFiles;
        }

        Log.Information("ConfigFileScanner: 扫描配置文件目录: {Dir}", workingDirectory);

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

        Log.Information("配置文件扫描完成，共发现 {Count} 个文件", configFiles.Count);
        return configFiles;
    }

    /// <summary>
    /// 异步扫描指定目录中的配置文件
    /// </summary>
    /// <param name="directory">待扫描的目录路径</param>
    /// <param name="results">结果收集列表</param>
    /// <param name="label">目录标签，用于日志标识</param>
    private async Task ScanDirectoryAsync(string directory, List<string> results, string label)
    {
        Log.Debug("扫描目录: {Dir}", directory);
        try
        {
            foreach (var pattern in ConfigFileExtensions)
            {
                foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
                {
                    if (ShouldIgnoreFile(file))
                        continue;

                    results.Add(file);
                    Log.Debug("发现配置文件: {File}", file);
                }
                await Task.Yield();
            }
        }
        catch (UnauthorizedAccessException)
        {
            Log.Warning("权限不足，跳过目录: {Dir}", directory);
        }
        catch (IOException ex)
        {
            Log.Debug(ex, "IO 跳过: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "扫描目录跳过: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 异步递归扫描子目录的入口方法
    /// </summary>
    /// <param name="parentDir">父目录路径</param>
    /// <param name="results">结果收集列表</param>
    /// <param name="label">目录标签前缀</param>
    /// <param name="maxDepth">最大递归深度</param>
    private async Task ScanSubDirectoriesAsync(string parentDir, List<string> results, string label, int maxDepth)
    {
        await ScanSubDirectoriesRecursiveAsync(parentDir, results, label, currentDepth: 0, maxDepth);
    }

    /// <summary>
    /// 异步递归扫描子目录的核心实现
    /// </summary>
    /// <param name="directory">当前目录路径</param>
    /// <param name="results">结果收集列表</param>
    /// <param name="label">目录标签前缀</param>
    /// <param name="currentDepth">当前递归深度</param>
    /// <param name="maxDepth">最大递归深度</param>
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
            Log.Warning("权限不足，跳过目录: {Dir}", directory);
        }
        catch (IOException ex)
        {
            Log.Debug(ex, "IO 跳过: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "扫描目录跳过: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 判断文件是否应被忽略
    /// </summary>
    /// <param name="filePath">待判断的文件路径</param>
    /// <returns>若应忽略则返回 true，否则返回 false</returns>
    /// <remarks>
    /// 过滤规则：
    /// 1. 文件名含 "log" 且扩展名为 .json 的日志文件
    /// 2. eula.txt、README.md、LICENSE 等非配置文件
    /// </remarks>
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
