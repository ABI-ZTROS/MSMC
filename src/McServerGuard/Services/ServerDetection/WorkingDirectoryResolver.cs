// -----------------------------------------------------------------------------
// 文件名: WorkingDirectoryResolver.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 服务器工作目录多策略解析器，通过JAR路径推导、父进程脚本提取、WMI进程目录查询、文件系统搜索等多级策略，精准定位Minecraft服务器的工作目录
// 依赖组件: System.Diagnostics, System.IO, System.Management, Serilog, McServerGuard.Constants
// 设计模式: 策略模式（多解析策略级联）、责任链模式（策略按优先级依次尝试）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ServerDetection;

using System.Diagnostics;
using System.IO;
using System.Management;
using McServerGuard.Constants;
using Serilog;

/// <summary>
/// 工作目录解析器 —— 基于多策略级联架构，精准定位Minecraft服务器的工作目录
/// </summary>
/// <remarks>
/// 采用策略级联架构，按优先级依次尝试以下解析策略：
/// 1. JAR参数推导：从命令行-jar参数中提取JAR路径，推导工作目录
/// 2. 父进程脚本追溯：从父进程Shell的启动脚本路径中提取目录
/// 3. WMI进程目录查询：通过WMI获取进程的CurrentDirectory
/// 4. 文件系统搜索：在常见位置递归搜索包含server.properties的目录
/// 
/// 每级策略均支持server.properties存在性验证，确保解析结果的可靠性。
/// </remarks>
public class WorkingDirectoryResolver
{
    /// <summary>
    /// 解析服务器工作目录，按策略优先级依次尝试，返回首个有效结果
    /// </summary>
    /// <param name="processId">目标Java进程ID</param>
    /// <param name="commandLine">进程完整命令行</param>
    /// <param name="jarName">JAR文件名</param>
    /// <returns>解析得到的工作目录；所有策略均失败时返回占位符字符串</returns>
    /// <remarks>
    /// 策略优先级：
    /// 1. JAR路径解析（最高优先级）—— 从-jar参数推导并验证
    /// 2. 父进程脚本追溯 —— 从Shell启动脚本路径推导
    /// 3. 进程模块目录 —— 从MainModule获取（通常不可靠，仅作参考）
    /// 4. 进程工作目录 —— 通过WMI查询CurrentDirectory
    /// 5. 文件系统搜索 —— 在常见位置递归搜索（终极大招）
    /// </remarks>
    public string Resolve(int processId, string commandLine, string jarName)
    {
        Log.Information("🗺️ WorkingDirectoryResolver: 解析工作目录 PID={Pid}, CmdLine={Cmd}", processId, 
            commandLine?.Length > 150 ? commandLine[..150] + "..." : commandLine);
        string? workingDir = null;

        // 策略1：从JAR路径解析
        Log.Debug("策略1: 从 JAR 路径解析");
        workingDir = ResolveFromJarArgument(commandLine, processId);
        if (!string.IsNullOrEmpty(workingDir))
        {
            Log.Debug("方法1成功：从 -jar 参数解析到目录: {Dir}", workingDir);

            // 验证目录下是否存在server.properties
            if (ValidateServerDirectory(workingDir))
            {
                Log.Information("✅ 工作目录解析成功: {Dir}", workingDir);
                return workingDir;
            }

            Log.Warning(
                "方法2验证失败: {Dir} 下没有 server.properties，可能是软链接或者非标准目录",
                workingDir);
            // 即使未通过验证，仍返回该目录（高置信度候选）
            Log.Information("✅ 工作目录解析成功: {Dir}", workingDir);
            return workingDir;
        }

        // 策略1.5：从父进程Shell的启动脚本路径推断
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

        // 策略2：从进程MainModule获取（参考价值有限）
        // 注意：跨位数访问MainModule会抛出Win32Exception，进程已退出时抛出InvalidOperationException
        Log.Debug("策略2: 从进程 MainModule 获取");
        try
        {
            using var process = Process.GetProcessById(processId);
            var mainModule = process.MainModule;
            if (mainModule is not null)
            {
                var processDir = Path.GetDirectoryName(mainModule.FileName);
                if (!string.IsNullOrEmpty(processDir) && Directory.Exists(processDir))
                {
                    Log.Debug("进程模块目录: {Dir}（通常不是服务器目录）", processDir);
                }
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log.Debug(ex, "策略2跳过（跨位数或权限不足）: PID={Pid}", processId);
        }
        catch (InvalidOperationException ex)
        {
            Log.Debug(ex, "策略2跳过（进程已退出）: PID={Pid}", processId);
        }
        catch (IOException ex)
        {
            Log.Debug(ex, "策略2跳过（IO 异常）: PID={Pid}", processId);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "解析策略跳过: {Message}", ex.Message);
        }

        // 策略3：通过WMI查询进程的当前工作目录
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

                Log.Debug("进程工作目录 {Dir} 未通过验证，但仍作为候选目录", procWorkingDir);
                return procWorkingDir;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "解析策略跳过: {Message}", ex.Message);
        }

        // 策略4：在常见位置进行递归搜索（终级策略）
        Log.Debug("策略4: 启动地毯式搜索 SearchServerDirectories...");
        var found = SearchServerDirectories(jarName);
        if (!string.IsNullOrEmpty(found))
        {
            Log.Information("✅ 工作目录解析成功: {Dir}", found);
            return found;
        }

        // 所有策略均失败，返回占位符
        Log.Warning("⚠️ 所有策略均失败，无法解析工作目录");
        return "(无法解析工作目录)";
    }

    /// <summary>
    /// 获取Java进程的当前工作目录，支持父进程链追溯
    /// </summary>
    /// <param name="processId">目标进程ID</param>
    /// <returns>工作目录路径；获取失败返回<c>null</c></returns>
    /// <remarks>
    /// 若当前进程目录不可获取，则沿父进程链向上追溯Shell进程的工作目录。
    /// 使用哈希集合防止循环引用导致的无限递归。
    /// </remarks>
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
                Log.Debug(ex, "获取进程当前目录失败 PID={Pid}: {Message}", currentId, ex.Message);
                break;
            }
        }

        return null;
    }

    /// <summary>
    /// 通过WMI查询单个进程的CurrentDirectory属性
    /// </summary>
    /// <param name="processId">目标进程ID</param>
    /// <returns>当前工作目录路径；查询失败返回<c>null</c></returns>
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
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Log.Debug(ex, "WMI 查询 CurrentDirectory 失败（COM 异常）PID={Pid}", processId);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug(ex, "WMI 查询 CurrentDirectory 失败（权限不足）PID={Pid}", processId);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "查询 CurrentDirectory 失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// 通过WMI获取进程的父进程ID
    /// </summary>
    /// <param name="processId">目标进程ID</param>
    /// <returns>父进程ID；获取失败返回0</returns>
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
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Log.Debug(ex, "WMI 查询 ParentProcessId 失败（COM 异常）PID={Pid}", processId);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug(ex, "WMI 查询 ParentProcessId 失败（权限不足）PID={Pid}", processId);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "获取父进程失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return 0;
    }

    /// <summary>
    /// 从父进程命令行中提取批处理脚本路径，推导服务器工作目录
    /// </summary>
    /// <param name="processId">目标进程ID</param>
    /// <returns>脚本所在目录；未找到返回<c>null</c></returns>
    /// <remarks>
    /// 用于处理通过双击start.bat启动服务器的场景：
    /// 沿父进程链向上追溯，在Shell进程的命令行中提取.bat/.cmd脚本路径。
    /// </remarks>
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
    /// 从命令行字符串中提取.bat/.cmd脚本的完整路径
    /// </summary>
    /// <param name="commandLine">命令行字符串</param>
    /// <returns>脚本文件路径；未匹配返回<c>null</c></returns>
    /// <example>
    /// 输入：cmd /c ""H:\MyServer\start.bat""
    /// 输出：H:\MyServer\start.bat
    /// </example>
    private string? ExtractBatchScriptPath(string commandLine)
    {
        try
        {
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
    /// 通过WMI获取指定进程的完整命令行
    /// </summary>
    /// <param name="processId">目标进程ID</param>
    /// <returns>完整命令行字符串；获取失败返回<c>null</c></returns>
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
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Log.Debug(ex, "WMI 获取命令行失败（COM 异常）PID={Pid}", processId);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug(ex, "WMI 获取命令行失败（权限不足）PID={Pid}", processId);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "获取命令行失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// 从命令行的-jar参数中解析JAR文件路径，推导工作目录
    /// </summary>
    /// <param name="commandLine">进程完整命令行</param>
    /// <param name="processId">目标进程ID（用于相对路径拼接时获取进程工作目录）</param>
    /// <returns>推导的工作目录；解析失败返回<c>null</c></returns>
    /// <remarks>
    /// 路径处理逻辑：
    /// 1. 绝对路径且文件存在 → 直接取目录
    /// 2. 相对路径 → 使用进程工作目录拼接后验证
    /// 3. 拼接后不存在 → 向上递归搜索祖先目录（最多3层）
    /// 4. 无法获取进程工作目录 → 使用当前程序目录兜底
    /// </remarks>
    private string? ResolveFromJarArgument(string commandLine, int processId)
    {
        try
        {
            var jarIdx = commandLine.IndexOf("-jar", StringComparison.OrdinalIgnoreCase);
            if (jarIdx < 0)
                return null;

            var afterJar = commandLine[(jarIdx + 4)..].TrimStart();

            // JAR路径截取至下一个空白字符
            var endIdx = afterJar.IndexOfAny([' ', '\t']);
            var jarPath = endIdx > 0 ? afterJar[..endIdx] : afterJar;

            // 去除路径两端的引号（Windows路径含空格时会被引号包裹）
            jarPath = jarPath.Trim('"');

            if (string.IsNullOrWhiteSpace(jarPath))
                return null;

            // 绝对路径且文件存在 → 直接返回目录
            if (Path.IsPathRooted(jarPath) && File.Exists(jarPath))
            {
                return Path.GetDirectoryName(jarPath);
            }

            // 相对路径 → 使用进程工作目录拼接
            var processWorkingDir = GetProcessCurrentDirectory(processId);
            if (!string.IsNullOrWhiteSpace(processWorkingDir))
            {
                var fullPath = Path.GetFullPath(Path.Combine(processWorkingDir, jarPath));
                if (File.Exists(fullPath))
                {
                    return Path.GetDirectoryName(fullPath);
                }

                // 当前工作目录下未找到JAR，向上递归搜索祖先目录（最多3层）
                var foundDir = FindJarInAncestors(processWorkingDir, jarPath, maxDepth: 3);
                if (!string.IsNullOrEmpty(foundDir))
                {
                    return foundDir;
                }

                Log.Debug("JAR 文件 {Jar} 不存在，但仍尝试使用其目录", fullPath);
                return Path.GetDirectoryName(fullPath);
            }

            // 无法获取进程工作目录时，使用本程序目录作为兜底
            Log.Debug("无法获取进程 PID={Pid} 的工作目录，使用本程序目录作为最后手段", processId);
            var fallbackPath = Path.GetFullPath(jarPath);
            return Path.GetDirectoryName(fallbackPath) ?? Path.GetDirectoryName(jarPath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "解析策略跳过: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 在指定目录的祖先路径中搜索JAR文件
    /// </summary>
    /// <param name="startDirectory">起始目录</param>
    /// <param name="jarName">JAR文件名</param>
    /// <param name="maxDepth">最大向上搜索层数</param>
    /// <returns>找到JAR文件的目录；未找到返回<c>null</c></returns>
    /// <remarks>
    /// 用于处理cd至子目录后启动服务器的场景，向上回溯查找JAR所在的真实工作目录。
    /// </remarks>
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
    /// 验证目录是否为有效的服务器目录（以server.properties存在性为判定依据）
    /// </summary>
    /// <param name="directory">待验证的目录路径</param>
    /// <returns>目录存在且包含server.properties则返回<c>true</c>，否则返回<c>false</c></returns>
    private bool ValidateServerDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        return File.Exists(Path.Combine(directory, ServerConstants.ServerValidationFile));
    }

    /// <summary>
    /// 在预定义的常见位置中搜索服务器目录
    /// </summary>
    /// <param name="jarName">目标JAR文件名</param>
    /// <returns>找到的服务器目录；未找到返回<c>null</c></returns>
    /// <remarks>
    /// 搜索策略：在预定义目录列表中按顺序递归搜索，
    /// 深度限制为MaxSearchDepth，找到首个匹配目录即返回。
    /// </remarks>
    private string? SearchServerDirectories(string jarName)
    {
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
    /// 递归搜索目录，查找包含指定JAR文件且存在server.properties的服务器目录
    /// </summary>
    /// <param name="directory">当前搜索目录</param>
    /// <param name="jarName">目标JAR文件名</param>
    /// <param name="currentDepth">当前递归深度</param>
    /// <returns>匹配的服务器目录；未找到返回<c>null</c></returns>
    private string? SearchDirectoryRecursive(string directory, string jarName, int currentDepth)
    {
        const int maxSearchDepth = 3; // 最大递归深度，防止无限制搜索

        if (currentDepth > maxSearchDepth)
            return null;

        try
        {
            // 优先验证当前目录
            if (ValidateServerDirectory(directory))
            {
                // 检查目录中是否存在匹配的JAR文件
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
                // 跳过隐藏目录与系统目录
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
            Log.Debug("无权访问目录: {Dir}", directory);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "解析策略跳过: {Message}", ex.Message);
        }

        return null;
    }
}
