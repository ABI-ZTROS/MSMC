// 🧭 工作目录解析器 —— 哪怕命令行里藏了再多路径，也要把它挖出来
// 有时候 Java 进程的 WorkingDirectory 就是不可靠，所以我们需要"尽一切可能"来确认
// 这个类的名字应该叫"死皮赖脸目录侦探"更贴切 🕵️
namespace McServerGuard.Services.ServerDetection;

using System.Diagnostics;
using System.IO;
using System.Management;
using McServerGuard.Constants;
using Serilog;

/// <summary>
/// 工作目录解析器 —— 服务器到底在哪个目录？这是本文的核心问题
/// </summary>
public class WorkingDirectoryResolver
{
    /// <summary>
    /// 尽一切可能确认服务器的工作目录
    /// 策略优先级：
    ///   1. 从命令行的 -jar 参数中解析 JAR 路径，取其所在目录
    ///   2. 验证该目录下是否存在 server.properties
    ///   3. 如果以上都失败，就在常见位置搜索
    /// 简直是"找不到就搜家"的架势 🔍
    /// </summary>
    /// <param name="processId">Java 进程 ID</param>
    /// <param name="commandLine">完整命令行</param>
    /// <param name="jarName">JAR 文件名</param>
    /// <returns>确认的工作目录，如果实在找不到就返回进程的当前目录</returns>
    public string Resolve(int processId, string commandLine, string jarName)
    {
        Log.Information("🗺️ WorkingDirectoryResolver: 解析工作目录 PID={Pid}, CmdLine={Cmd}", processId, 
            commandLine?.Length > 150 ? commandLine[..150] + "..." : commandLine);
        string? workingDir = null;

        // 策略1: 从 JAR 路径解析（相对路径会用 java/shell 的真实工作目录拼接）
        Log.Debug("策略1: 从 JAR 路径解析");
        workingDir = ResolveFromJarArgument(commandLine, processId);
        if (!string.IsNullOrEmpty(workingDir))
        {
            Log.Debug("方法1成功：从 -jar 参数解析到目录: {Dir}", workingDir);

            // 方法 2：验证该目录下是否存在 server.properties
            if (ValidateServerDirectory(workingDir))
            {
                Log.Information("✅ 工作目录解析成功: {Dir}", workingDir);
                return workingDir;
            }

            Log.Warning(
                "方法2验证失败: {Dir} 下没有 server.properties，可能是软链接或者非标准目录",
                workingDir);
            // 即使没有 server.properties 也先返回这个目录，毕竟大概率是对的
            Log.Information("✅ 工作目录解析成功: {Dir}", workingDir);
            return workingDir;
        }

        // 策略1.5: 如果 JAR 是相对路径且解析失败，尝试从父进程 cmd 的命令行里找到 start.bat 所在目录
        Log.Debug("策略1.5: 从父进程 Shell 的启动脚本路径推断工作目录");
        var scriptDir = ResolveFromParentBatchScript(processId);
        if (!string.IsNullOrEmpty(scriptDir))
        {
            if (ValidateServerDirectory(scriptDir))
            {
                Log.Information("✅ 工作目录解析成功: {Dir}", scriptDir);
                return scriptDir;
            }

            Log.Debug("脚本目录 {Dir} 未通过验证，但仍作为候选目录", scriptDir);
            return scriptDir;
        }

        // 策略2: 从进程的 MainModule 获取
        Log.Debug("策略2: 从进程 MainModule 获取");
        try
        {
            using var process = Process.GetProcessById(processId);
            var processDir = Path.GetDirectoryName(process.MainModule?.FileName);
            if (!string.IsNullOrEmpty(processDir) && Directory.Exists(processDir))
            {
                Log.Debug("进程模块目录: {Dir}（通常不是服务器目录）", processDir);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 解析策略失败: {Message}", ex.Message);
        }

        // 策略3: 用进程自己的当前工作目录（通过WMI查询，而不是本程序的工作目录）
        Log.Debug("策略3: 尝试使用进程的工作目录...");
        try
        {
            var procWorkingDir = GetProcessCurrentDirectory(processId);
            if (!string.IsNullOrEmpty(procWorkingDir) && Directory.Exists(procWorkingDir))
            {
                if (ValidateServerDirectory(procWorkingDir))
                {
                    Log.Information("✅ 工作目录解析成功: {Dir}", procWorkingDir);
                    return procWorkingDir;
                }

                // 即使没找到 server.properties，也记录到日志里
                Log.Debug("进程工作目录 {Dir} 未通过验证，但仍作为候选目录", procWorkingDir);
                return procWorkingDir;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 解析策略失败: {Message}", ex.Message);
        }

        // 策略4（终极大招）：在常见位置搜索
        Log.Debug("策略4: 启动地毯式搜索 SearchServerDirectories...");
        var found = SearchServerDirectories(jarName);
        if (!string.IsNullOrEmpty(found))
        {
            Log.Information("✅ 工作目录解析成功: {Dir}", found);
            return found;
        }

        // 实在找不到就返回 null 的替代值，让调用方知道解析失败
        // 注意：不要返回 Environment.CurrentDirectory，那会误导成本程序目录
        Log.Warning("⚠️ 所有策略均失败，无法解析工作目录");
        return "(无法解析工作目录)";
    }

    /// <summary>
    /// 获取 java 进程自己的当前工作目录
    /// 如果查询失败，会尝试追溯父进程链，找到 shell 的工作目录
    /// </summary>
    private string? GetProcessCurrentDirectory(int processId)
    {
        var triedIds = new HashSet<int>();
        var currentId = processId;

        while (currentId != 0 && triedIds.Add(currentId))
        {
            try
            {
                var dir = QueryCurrentDirectory(currentId);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    Log.Debug("进程 PID={Pid} 的当前工作目录: {Dir}", currentId, dir);
                    return dir;
                }

                var parentId = GetParentProcessId(currentId);
                if (parentId == currentId)
                    break;

                currentId = parentId;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "💥 fuck: 获取进程当前目录失败 PID={Pid}: {Message}", currentId, ex.Message);
                break;
            }
        }

        return null;
    }

    /// <summary>
    /// 查询单个进程的 CurrentDirectory
    /// </summary>
    private string? QueryCurrentDirectory(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CurrentDirectory FROM Win32_Process WHERE ProcessId = {processId}");

            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                var dir = obj["CurrentDirectory"]?.ToString();
                if (!string.IsNullOrWhiteSpace(dir))
                    return dir;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 查询 CurrentDirectory 失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// 获取进程的父进程 ID
    /// </summary>
    private int GetParentProcessId(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}");

            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                if (obj["ParentProcessId"] is int parentId)
                    return parentId;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 获取父进程失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return 0;
    }

    /// <summary>
    /// 从父进程 cmd 的命令行中提取启动脚本（.bat/.cmd）的路径，返回其所在目录
    /// 用于处理双击 start.bat 启动服务器的情况
    /// </summary>
    private string? ResolveFromParentBatchScript(int processId)
    {
        var currentId = processId;
        var triedIds = new HashSet<int>();

        while (currentId != 0 && triedIds.Add(currentId))
        {
            var parentId = GetParentProcessId(currentId);
            if (parentId == 0 || parentId == currentId)
                break;

            var parentCommandLine = GetProcessCommandLine(parentId);
            if (!string.IsNullOrWhiteSpace(parentCommandLine))
            {
                var scriptPath = ExtractBatchScriptPath(parentCommandLine);
                if (!string.IsNullOrWhiteSpace(scriptPath) && File.Exists(scriptPath))
                {
                    var dir = Path.GetDirectoryName(scriptPath);
                    Log.Debug("从父进程 PID={ParentId} 的命令行中提取到脚本目录: {Dir}", parentId, dir);
                    return dir;
                }
            }

            currentId = parentId;
        }

        return null;
    }

    /// <summary>
    /// 从命令行字符串中提取 .bat/.cmd 脚本路径
    /// 例如 cmd /c ""H:\MyServer\start.bat"" → H:\MyServer\start.bat
    /// </summary>
    private string? ExtractBatchScriptPath(string commandLine)
    {
        try
        {
            // 先找 .bat 或 .cmd 扩展名
            var match = System.Text.RegularExpressions.Regex.Match(
                commandLine,
                @"[""']?([a-zA-Z]:[^""'\s]*\.(?:bat|cmd))[""']?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var path = match.Groups[1].Value.Trim('"', '\'');
                if (File.Exists(path))
                    return path;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 提取批处理脚本路径失败: {Message}", ex.Message);
        }

        return null;
    }

    /// <summary>
    /// 通过 WMI 获取进程的完整命令行
    /// </summary>
    private string? GetProcessCommandLine(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");

            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                var cmdLine = obj["CommandLine"]?.ToString();
                if (!string.IsNullOrWhiteSpace(cmdLine))
                {
                    var escaped = cmdLine.Replace("\t", "\\t").Replace("\f", "\\f").Replace("\b", "\\b")
                        .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\0", "\\0");
                    Log.Debug("🔧 WorkingDirectoryResolver 获取命令行: {Raw} | 转义后: {Escaped}", 
                        cmdLine.Length > 100 ? cmdLine[..100] : cmdLine,
                        escaped.Length > 100 ? escaped[..100] : escaped);
                    return cmdLine;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 获取命令行失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// 方法 1：从命令行的 -jar 参数中解析 JAR 文件的完整路径，取其所在目录
    /// 这是最高优先级的方法，因为 -jar 后面通常跟的是完整路径
    /// </summary>
    private string? ResolveFromJarArgument(string commandLine, int processId)
    {
        try
        {
            var jarIdx = commandLine.IndexOf("-jar", StringComparison.OrdinalIgnoreCase);
            if (jarIdx < 0)
                return null;

            var afterJar = commandLine[(jarIdx + 4)..].TrimStart();

            // JAR 路径到下一个空格为止（或者到字符串末尾）
            var endIdx = afterJar.IndexOfAny([' ', '\t']);
            var jarPath = endIdx > 0 ? afterJar[..endIdx] : afterJar;

            // 去除可能的引号 —— Windows 路径有空格时会被引号包裹
            jarPath = jarPath.Trim('"');

            if (string.IsNullOrWhiteSpace(jarPath))
                return null;

            // 如果是绝对路径，直接取目录
            if (Path.IsPathRooted(jarPath) && File.Exists(jarPath))
            {
                return Path.GetDirectoryName(jarPath);
            }

            // 如果是相对路径，必须用 java 进程自己或父进程 shell 的工作目录来拼接，
            // 不能用本程序的当前目录，否则就硬编码成了 McServerGuard 的启动目录
            var processWorkingDir = GetProcessCurrentDirectory(processId);
            if (!string.IsNullOrWhiteSpace(processWorkingDir))
            {
                var fullPath = Path.GetFullPath(Path.Combine(processWorkingDir, jarPath));
                if (File.Exists(fullPath))
                {
                    return Path.GetDirectoryName(fullPath);
                }

                // 当前工作目录下没有 JAR，可能是 cd 到子目录后启动的，
                // 在父目录中向上搜索这个 JAR（最多向上 3 层）
                var foundDir = FindJarInAncestors(processWorkingDir, jarPath, maxDepth: 3);
                if (!string.IsNullOrEmpty(foundDir))
                {
                    return foundDir;
                }

                Log.Debug("JAR 文件 {Jar} 不存在，但仍尝试使用其目录", fullPath);
                return Path.GetDirectoryName(fullPath);
            }

            // 拿不到进程工作目录时，用本程序目录兜底
            Log.Debug("无法获取进程 PID={Pid} 的工作目录，使用本程序目录作为最后手段", processId);
            var fallbackPath = Path.GetFullPath(jarPath);
            return Path.GetDirectoryName(fallbackPath) ?? Path.GetDirectoryName(jarPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 解析策略失败: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 在指定目录的祖先目录中搜索指定的 JAR 文件
    /// 用于处理 cd 到子目录后启动服务器的情况
    /// </summary>
    private string? FindJarInAncestors(string startDirectory, string jarName, int maxDepth)
    {
        var currentDir = startDirectory;
        for (var i = 0; i <= maxDepth && !string.IsNullOrWhiteSpace(currentDir); i++)
        {
            var jarPath = Path.Combine(currentDir, jarName);
            if (File.Exists(jarPath))
            {
                Log.Debug("在父目录 {Dir} 中找到 JAR 文件 {Jar}", currentDir, jarName);
                return currentDir;
            }

            var parent = Directory.GetParent(currentDir);
            currentDir = parent?.FullName ?? string.Empty;
        }

        return null;
    }

    /// <summary>
    /// 方法 2：验证目录下是否存在 server.properties
    /// 这是最可靠的服务器目录标识 —— 没有这个文件的基本不可能是服务器
    /// </summary>
    private bool ValidateServerDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        return File.Exists(Path.Combine(directory, ServerConstants.ServerValidationFile));
    }

    /// <summary>
    /// 方法 3：在常见位置搜索服务器 JAR 文件
    /// 搜索策略：在预定义的目录列表中递归搜索，深度限制为 MaxSearchDepth
    /// 这招虽然暴力但有效 —— 找到一个 JAR 名字匹配的目录就收手
    /// </summary>
    private string? SearchServerDirectories(string jarName)
    {
        // 定义要搜索的常见位置
        var searchPaths = new[]
        {
            ".",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "server"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "minecraft-server"),
            "C:\\MinecraftServer",
            "D:\\MinecraftServer",
        };

        foreach (var searchRoot in searchPaths)
        {
            if (!Directory.Exists(searchRoot))
                continue;

            Log.Debug("在 {Root} 中搜索服务器目录...", searchRoot);
            var found = SearchDirectoryRecursive(searchRoot, jarName, currentDepth: 0);
            if (found is not null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// 递归搜索目录，寻找包含指定 JAR 文件且存在 server.properties 的目录
    /// </summary>
    private string? SearchDirectoryRecursive(string directory, string jarName, int currentDepth)
    {
        const int maxSearchDepth = 3; // 递归搜索的最大深度 —— 不然它能把整个硬盘翻个底朝天 💀

        if (currentDepth > maxSearchDepth)
            return null;

        try
        {
            // 先检查当前目录
            if (ValidateServerDirectory(directory))
            {
                // 检查这个目录里有没有匹配的 JAR 文件
                if (string.IsNullOrWhiteSpace(jarName) ||
                    Directory.GetFiles(directory, "*.jar").Any(f =>
                        Path.GetFileName(f).Equals(jarName, StringComparison.OrdinalIgnoreCase)))
                {
                    return directory;
                }
            }

            // 递归搜索子目录
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                // 跳过隐藏目录和系统目录 —— 它们里面不会有服务器的（大概）
                var dirInfo = new DirectoryInfo(subDir);
                if (dirInfo.Attributes.HasFlag(FileAttributes.Hidden | FileAttributes.System))
                    continue;

                var result = SearchDirectoryRecursive(subDir, jarName, currentDepth + 1);
                if (result is not null)
                    return result;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 没权限访问的目录就跳过，别纠结
            Log.Debug("无权访问目录: {Dir}", directory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 解析策略失败: {Message}", ex.Message);
        }

        return null;
    }
}
