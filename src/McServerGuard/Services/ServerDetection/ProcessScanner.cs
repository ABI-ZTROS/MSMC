// -----------------------------------------------------------------------------
// 文件名: ProcessScanner.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 基于WMI与进程枚举的Minecraft服务器进程指纹识别引擎，通过多维度特征（JAR关键字、Shell父进程链、客户端特征排除）从Java进程池中筛选候选服务器进程
// 依赖组件: System.Diagnostics, System.Management, Serilog, McServerGuard.Constants
// 设计模式: 策略模式（多判定策略组合）、指纹识别（进程签名去重）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ServerDetection;

using System.Diagnostics;
using System.Management;
using McServerGuard.Constants;
using Serilog;

/// <summary>
/// 进程扫描引擎 —— 负责从系统进程池中识别并提取Minecraft服务器进程
/// </summary>
/// <remarks>
/// 采用多策略级联判定架构：
/// 1. 基础筛选：枚举所有 java/javaw 进程
/// 2. 客户端排除：基于命令行特征指纹过滤客户端进程
/// 3. 服务器判定：JAR文件名关键字匹配 + Shell父进程链追溯
/// 4. 去重校验：基于JAR名称与关键JVM参数生成进程签名，避免重复条目
/// </remarks>
public class ProcessScanner
{
    /// <summary>
    /// Shell进程名称集合，用于父进程链追溯时的锚点识别
    /// </summary>
    private static readonly string[] ShellProcessNames = ["cmd", "powershell", "pwsh"];

    /// <summary>
    /// 扫描系统中所有Java进程，筛选并返回疑似Minecraft服务器进程列表
    /// </summary>
    /// <returns>进程ID与对应命令行的元组集合（不持有<see cref="Process"/>对象，避免非托管句柄泄漏）</returns>
    /// <remarks>
    /// 判定流程采用级联策略：
    /// 1. 枚举 java/javaw 进程
    /// 2. 排除具有客户端特征指纹的进程
    /// 3. JAR文件名关键字匹配 或 Shell父进程链追溯命中
    /// 4. 基于进程签名（JAR名+Xms/Xmx特征）进行去重
    /// </remarks>
    public List<(int ProcessId, string CommandLine)> ScanServerProcesses()
    {
        var results = new List<(int ProcessId, string CommandLine)>();

        var javaProcesses = Process.GetProcessesByName("java")
            .Concat(Process.GetProcessesByName("javaw"))
            .ToList();

        var shellProcessIds = GetAllShellProcessIds();

        if (javaProcesses.Count == 0)
        {
            Log.Information("没有找到任何 Java 进程，世界清静了 🌿");
            return results;
        }

        Log.Information("📡 ProcessScanner: 查询 WMI 获取 Java 进程列表...");

        foreach (var process in javaProcesses)
        {
            bool matched = false;
            try
            {
                Log.Debug("🔎 发现 Java 进程: PID={Pid} Name={Name}", process.Id, process.ProcessName);

                var commandLine = GetCommandLine(process.Id);

                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    Log.Debug("进程 PID={Pid} 没有命令行信息，跳过", process.Id);
                    continue;
                }

                if (IsClientProcess(commandLine))
                {
                    Log.Debug("⏭️ 跳过客户端进程: PID={Pid}", process.Id);
                    continue;
                }

                bool isServerJar = IsServerJar(commandLine);
                bool isLaunchedByShell = IsProcessLaunchedByShell(process.Id, shellProcessIds);

                if (isServerJar)
                {
                    Log.Information(
                        "发现疑似服务器进程 PID={Pid}: {JarHint}",
                        process.Id,
                        GetJarNameHint(commandLine));
                    results.Add((process.Id, commandLine));
                    matched = true;
                }
                else if (isLaunchedByShell)
                {
                    Log.Information(
                        "发现 Shell 启动的 Java 进程 PID={Pid}（可能是交互式启动的服务器）: {JarHint}",
                        process.Id,
                        GetJarNameHint(commandLine));
                    results.Add((process.Id, commandLine));
                    matched = true;
                }
                else
                {
                    Log.Debug(
                        "进程 PID={Pid} 的命令行中没有服务器 JAR 关键字，跳过",
                        process.Id);
                }
            }
            catch (InvalidOperationException)
            {
                Log.Debug("进程 PID={Pid} 已退出（扫描途中跑路了）", process.Id);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "进程扫描跳过: {Message}", ex.Message);
            }
            finally
            {
                // 释放Process对象的非托管句柄，防止资源泄漏
                if (!matched)
                {
                    process.Dispose();
                }
            }
        }

        // 基于进程ID进行初级去重
        results = results.DistinctBy(r => r.ProcessId).ToList();

        // 基于进程签名（JAR名称 + 关键JVM参数组合）进行二级去重
        // 用于处理同一服务器实例被多种策略重复命中的场景
        var uniqueResults = new List<(int ProcessId, string CommandLine)>();
        var seenSignatures = new HashSet<string>();
        
        foreach (var result in results)
        {
            var signature = GetProcessSignature(result.CommandLine);
            if (!seenSignatures.Contains(signature))
            {
                seenSignatures.Add(signature);
                uniqueResults.Add(result);
            }
            else
            {
                Log.Debug("⏭️ 跳过重复的服务器进程: PID={Pid}, Signature={Sig}", result.ProcessId, signature);
            }
        }

        Log.Information("✅ 扫描完成，共获取 {Count} 个唯一服务器进程", uniqueResults.Count);
        return uniqueResults;
    }

    /// <summary>
    /// 基于命令行生成进程特征签名，用于重复进程的语义去重
    /// </summary>
    /// <param name="commandLine">进程完整命令行</param>
    /// <returns>由JAR文件名与关键JVM参数组成的签名字符串</returns>
    private static string GetProcessSignature(string commandLine)
    {
        var jarName = GetJarNameHint(commandLine);
        var hasXms = commandLine.Contains("-Xms");
        var hasXmx = commandLine.Contains("-Xmx");
        return $"{jarName}|{hasXms}|{hasXmx}";
    }

    /// <summary>
    /// 获取系统中所有Shell进程的ID集合，作为父进程链追溯的锚点
    /// </summary>
    /// <returns>Shell进程ID的哈希集合</returns>
    private HashSet<int> GetAllShellProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (var name in ShellProcessNames)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                using (proc)
                {
                    ids.Add(proc.Id);
                }
            }
        }
        Log.Debug("📊 发现 {Count} 个 Shell 进程", ids.Count);
        return ids;
    }

    /// <summary>
    /// 通过WMI递归追溯父进程链，判定目标进程是否由Shell进程启动
    /// </summary>
    /// <param name="processId">目标进程ID</param>
    /// <param name="shellProcessIds">Shell进程ID集合</param>
    /// <param name="depth">当前递归深度，用于防止无限追溯</param>
    /// <returns>若父进程链中存在Shell进程则返回<c>true</c>，否则返回<c>false</c></returns>
    private bool IsProcessLaunchedByShell(int processId, HashSet<int> shellProcessIds, int depth = 0)
    {
        if (depth > 5)
        {
            Log.Debug("🔗 进程 PID={Pid} 父进程链深度超过 5 层，停止追溯", processId);
            return false;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}");

            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                if (obj["ParentProcessId"] is int parentId)
                {
                    Log.Debug("🔗 进程 PID={Pid} 的父进程 PID={ParentId}", processId, parentId);

                    if (shellProcessIds.Contains(parentId))
                    {
                        Log.Debug("✅ 进程 PID={Pid} 由 Shell 进程 PID={ParentId} 直接启动", processId, parentId);
                        return true;
                    }

                    if (IsProcessLaunchedByShell(parentId, shellProcessIds, depth + 1))
                    {
                        Log.Debug("✅ 进程 PID={Pid} 通过父进程链追溯到 Shell 进程", processId);
                        return true;
                    }
                }
            }
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Log.Debug(ex, "WMI 获取父进程信息失败（COM 异常）PID={Pid}", processId);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug(ex, "WMI 获取父进程信息失败（权限不足）PID={Pid}", processId);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "获取父进程信息失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return false;
    }

    /// <summary>
    /// 通过WMI Win32_Process类获取指定进程的完整命令行参数
    /// </summary>
    /// <param name="processId">目标进程ID</param>
    /// <returns>完整命令行字符串；若获取失败则返回空字符串</returns>
    /// <remarks>
    /// 采用WMI查询而非<see cref="Process.StartInfo"/>的原因：
    /// .NET基类库的Process.StartInfo.Arguments仅在进程由当前组件启动时有效，
    /// 对于外部已启动进程，必须通过WMI Win32_Process.CommandLine属性获取完整参数。
    /// </remarks>
    private string GetCommandLine(int processId)
    {
        Log.Debug("🔧 获取命令行: PID={Pid}", processId);
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
                    Log.Debug("🔧 原始命令行: {Raw} | 转义后: {Escaped}", 
                        cmdLine.Length > 100 ? cmdLine[..100] : cmdLine,
                        escaped.Length > 100 ? escaped[..100] : escaped);
                    return cmdLine;
                }
            }
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // WMI服务异常（如RPC服务器不可用），降级为Debug级别日志
            Log.Debug(ex, "🔧 WMI 查询失败（COM 异常）PID={Pid}", processId);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug(ex, "🔧 WMI 查询失败（权限不足）PID={Pid}", processId);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "🔧 获取命令行失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return string.Empty;
    }

    /// <summary>
    /// 基于命令行特征指纹判定目标进程是否为Minecraft客户端
    /// </summary>
    /// <param name="commandLine">进程完整命令行</param>
    /// <returns>若命中客户端特征则返回<c>true</c>，否则返回<c>false</c></returns>
    /// <remarks>
    /// 客户端进程通常携带 --version、--accessToken、--userType 等启动参数，
    /// 可作为与服务端进程区分的关键指纹。
    /// </remarks>
    private bool IsClientProcess(string commandLine)
    {
        var cmdLower = commandLine.ToLowerInvariant();
        foreach (var marker in ServerConstants.ClientProcessMarkers)
        {
            if (cmdLower.Contains(marker.ToLowerInvariant()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 基于JAR文件名关键字判定命令行是否指向服务器JAR包
    /// </summary>
    /// <param name="commandLine">进程完整命令行</param>
    /// <returns>若命中服务器JAR关键字则返回<c>true</c>，否则返回<c>false</c></returns>
    private bool IsServerJar(string commandLine)
    {
        var cmdLower = commandLine.ToLowerInvariant();
        foreach (var keyword in ServerConstants.ServerJarKeywords)
        {
            if (cmdLower.Contains(keyword.ToLowerInvariant()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 从命令行中提取JAR文件名，用于日志输出与进程签名生成
    /// </summary>
    /// <param name="commandLine">进程完整命令行</param>
    /// <returns>JAR文件名称；若无法提取则返回占位符</returns>
    private static string GetJarNameHint(string commandLine)
    {
        // 优先从 -jar 参数后提取JAR路径
        var jarIndex = commandLine.IndexOf("-jar", StringComparison.OrdinalIgnoreCase);
        if (jarIndex >= 0)
        {
            var afterJar = commandLine[(jarIndex + 4)..].TrimStart();
            var endIdx = afterJar.IndexOfAny([' ', '\t']);
            if (endIdx > 0)
            {
                var jarPath = afterJar[..endIdx];
                return System.IO.Path.GetFileName(jarPath);
            }
            return System.IO.Path.GetFileName(afterJar);
        }

        // 降级策略：搜索 .jar 扩展名并向前回溯文件名
        var dotJar = commandLine.IndexOf(".jar", StringComparison.OrdinalIgnoreCase);
        if (dotJar > 0)
        {
            var start = commandLine.LastIndexOf(' ', dotJar) + 1;
            return commandLine[start..(dotJar + 4)];
        }

        return "(未知 JAR)";
    }
}
