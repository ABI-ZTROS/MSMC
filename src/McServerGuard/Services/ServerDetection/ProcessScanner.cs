// 🔍 进程扫描器 —— 潜入系统进程列表，揪出所有偷偷运行的 Minecraft 服务器
// 话说 Minecraft 服务器的 java.exe 进程和客户端长得一模一样，
// 区分它们简直是世界上最有趣的猜谜游戏（并不是）🫠
namespace McServerGuard.Services.ServerDetection;

using System.Diagnostics;
using System.Management;
using McServerGuard.Constants;
using Serilog;

/// <summary>
/// 进程扫描器 —— 专治各种"我的服务器进程到底在哪"的疑难杂症
/// </summary>
public class ProcessScanner
{
    private static readonly string[] ShellProcessNames = ["cmd", "powershell", "pwsh"];

    /// <summary>
    /// 扫描所有 java 进程，返回可能的 Minecraft 服务器进程列表
    /// 原理很简单：先找 java.exe，然后排除掉客户端，剩下的就是服务器（大概率）
    /// </summary>
    /// <returns>进程ID和对应命令行的元组列表（不持有 Process 对象，避免句柄泄漏）</returns>
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
                Log.Error(ex, "💥 fuck: 处理进程 PID={Pid} 失败: {Message}", process.Id, ex.Message);
            }
            finally
            {
                // 不保留 Process 对象，全部释放句柄
                if (!matched)
                {
                    process.Dispose();
                }
            }
        }

        // 去重：确保同一个 PID 只添加一次
        results = results.DistinctBy(r => r.ProcessId).ToList();

        // 🔧 修复：再次检查是否已经有相同的命令行（可能是同一个服务器）
        // 如果两个结果的 JAR 名称和关键参数相同，可能是同一个进程的不同表示
        var uniqueResults = new List<(int ProcessId, string CommandLine)>();
        var seenSignatures = new HashSet<string>();
        
        foreach (var result in results)
        {
            // 创建一个签名：JAR 名称 + 关键 JVM 参数组合
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
    /// 为进程命令行创建一个签名，用于检测重复
    /// </summary>
    private static string GetProcessSignature(string commandLine)
    {
        var jarName = GetJarNameHint(commandLine);
        // 提取关键 JVM 参数
        var hasXms = commandLine.Contains("-Xms");
        var hasXmx = commandLine.Contains("-Xmx");
        return $"{jarName}|{hasXms}|{hasXmx}";
    }

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
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 获取父进程信息失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return false;
    }

    /// <summary>
    /// 通过 WMI Win32_Process 获取进程的完整命令行
    /// .NET 自带的 Process.StartInfo.Arguments 只能看到一部分，WMI 才能拿到完整的
    /// 这是 Windows 专属操作，Linux 上需要用 /proc/{pid}/cmdline —— 不过这是 WPF 项目所以无所谓 🤷
    /// </summary>
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
        catch (Exception ex)
        {
            Log.Error(ex, "💥 fuck: 获取命令行失败 PID={Pid}: {Message}", processId, ex.Message);
        }

        return string.Empty;
    }

    /// <summary>
    /// 判断命令行是否属于客户端进程
    /// 客户端的命令行通常带有 --version, --accessToken, --userType 等特征
    /// 如果你同时开了客户端和服务器，这个方法能帮你分清谁是谁 👀
    /// </summary>
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
    /// 判断命令行中是否包含服务器 JAR 的关键字
    /// 只要 JAR 文件名里带了 server/paper/forge 等关键字就认为是服务器
    /// 虽然有可能误判（比如你的 jar 叫 `my-server-utils.jar`），但概率不大啦
    /// </summary>
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
    /// 从命令行中提取 JAR 文件名的提示 —— 用于日志输出
    /// </summary>
    private static string GetJarNameHint(string commandLine)
    {
        // 尝试从 -jar 参数后面提取 JAR 名
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

        // 退而求其次，找 .jar 扩展名
        var dotJar = commandLine.IndexOf(".jar", StringComparison.OrdinalIgnoreCase);
        if (dotJar > 0)
        {
            // 往前找到文件名开头
            var start = commandLine.LastIndexOf(' ', dotJar) + 1;
            return commandLine[start..(dotJar + 4)];
        }

        return "(未知 JAR)";
    }
}
