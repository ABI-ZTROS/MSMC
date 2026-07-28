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
/// 
/// 性能优化：采用一次性 WMI 批量查询获取所有进程的 PID/父PID/命令行/名称，
/// 在内存中构建进程树，避免逐进程 WMI 查询（原 N×6 次 → 现 1 次）。
/// </remarks>
public class ProcessScanner
{
    /// <summary>
    /// Shell进程名称集合，用于父进程链追溯时的锚点识别
    /// </summary>
    private static readonly string[] ShellProcessNames = ["cmd", "powershell", "pwsh"];

    /// <summary>
    /// 启动器进程名称集合 —— MSMC 自身及常见服务器管理工具
    /// 这些进程启动的 Java 进程也应被视为服务器候选（与 Shell 同等对待）
    /// </summary>
    private static readonly string[] LauncherProcessNames = ["McServerGuard", "MSMC"];

    /// <summary>父进程链最大追溯深度，防止无限递归</summary>
    private const int MaxParentChainDepth = 5;

    /// <summary>本次扫描因权限不足或 WMI 失败而跳过的进程数</summary>
    public int LastSkippedCount { get; private set; }

    /// <summary>最近一次跳过的原因（用于 UI 提示）</summary>
    public string? LastSkipReason { get; private set; }

    /// <summary>
    /// 异步扫描系统中所有Java服务器进程 —— 将 WMI 批量查询与进程枚举放到线程池执行
    /// </summary>
    /// <returns>进程ID与对应命令行的元组集合（不持有<see cref="Process"/>对象，避免非托管句柄泄漏）</returns>
    public async Task<List<(int ProcessId, string CommandLine)>> ScanServerProcessesAsync()
    {
        return await Task.Run(() => ScanServerProcesses()).ConfigureAwait(false);
    }

    /// <summary>
    /// 扫描系统中所有Java进程，筛选并返回疑似Minecraft服务器进程列表
    /// </summary>
    /// <returns>进程ID与对应命令行的元组集合（不持有<see cref="Process"/>对象，避免非托管句柄泄漏）</returns>
    /// <remarks>
    /// 判定流程采用级联策略：
    /// 1. 一次性 WMI 批量查询获取所有进程信息（PID/父PID/命令行/名称）
    /// 2. 枚举 java/javaw 进程，从批量缓存读取命令行
    /// 3. 排除具有客户端特征指纹的进程
    /// 4. JAR文件名关键字匹配 或 Shell父进程链追溯命中（内存中遍历）
    /// 5. 基于进程签名（JAR名+Xms/Xmx特征）进行去重
    /// </remarks>
    public List<(int ProcessId, string CommandLine)> ScanServerProcesses()
    {
        LastSkippedCount = 0;
        LastSkipReason = null;

        var results = new List<(int ProcessId, string CommandLine)>();

        // 收集 Java 进程 PID（立即释放 Process 对象，仅保留 PID）
        var javaPids = CollectJavaProcessIds();
        if (javaPids.Count == 0)
        {
            Log.Information("没有找到任何 Java 进程，世界清静了 🌿");
            return results;
        }

        Log.Information("📡 ProcessScanner: 一次性 WMI 批量查询所有进程信息...");

        // 一次性 WMI 批量查询：获取所有进程的 PID / 父PID / 命令行 / 名称
        var processInfoMap = LoadAllProcessInfoBatch();
        if (processInfoMap.Count == 0)
        {
            LastSkipReason = "WMI 批量查询失败，无法获取进程信息";
            Log.Warning("⚠️ WMI 批量查询返回空结果，无法扫描服务器进程");
            return results;
        }

        // 内存中构建 Shell 进程 ID 集合（从批量结果中过滤，无需额外 Process 枚举）
        var shellProcessIds = BuildShellProcessIds(processInfoMap);

        Log.Information("📡 ProcessScanner: 使用批量缓存处理 {Count} 个 Java 进程...", javaPids.Count);

        foreach (var pid in javaPids)
        {
            try
            {
                Log.Debug("🔎 发现 Java 进程: PID={Pid}", pid);

                // 从批量缓存读取命令行（命中失败说明 WMI 未返回该进程，可能是跨用户/服务进程）
                if (!processInfoMap.TryGetValue(pid, out var info))
                {
                    Log.Warning("⚠️ 跳过 Java 进程 PID={Pid}（批量缓存中无此进程信息，可能跨用户）", pid);
                    LastSkippedCount++;
                    continue;
                }

                var commandLine = info.CommandLine;
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    Log.Warning("⚠️ 跳过 Java 进程 PID={Pid}（命令行为空，可能跨用户/服务进程）", pid);
                    LastSkipReason = "进程命令行为空（跨用户/服务进程）";
                    LastSkippedCount++;
                    continue;
                }

                if (IsClientProcess(commandLine))
                {
                    Log.Debug("⏭️ 跳过客户端进程: PID={Pid}", pid);
                    continue;
                }

                bool isServerJar = IsServerJar(commandLine);
                bool hasServerMarker = HasServerProcessMarker(commandLine);
                bool isLaunchedByShell = IsProcessLaunchedByShellInMemory(pid, processInfoMap, shellProcessIds);
                bool isJarProcess = IsJarProcess(commandLine);

                // 判定优先级：
                // 1. JAR 文件名包含服务器关键字 → 明确是服务器
                // 2. 命令行包含 nogui 等服务器标记 → 明确是服务器
                // 3. 由 Shell/启动器（含 MSMC 自身）启动 → 很可能是服务器
                // 4. 命令行含 -jar 且非客户端 → 兜底判定为服务器
                if (isServerJar)
                {
                    Log.Information(
                        "发现疑似服务器进程 PID={Pid}（JAR关键字匹配）: {JarHint}",
                        pid,
                        GetJarNameHint(commandLine));
                    results.Add((pid, commandLine));
                }
                else if (hasServerMarker)
                {
                    Log.Information(
                        "发现疑似服务器进程 PID={Pid}（nogui标记）: {JarHint}",
                        pid,
                        GetJarNameHint(commandLine));
                    results.Add((pid, commandLine));
                }
                else if (isLaunchedByShell)
                {
                    Log.Information(
                        "发现 Shell/启动器启动的 Java 进程 PID={Pid}: {JarHint}",
                        pid,
                        GetJarNameHint(commandLine));
                    results.Add((pid, commandLine));
                }
                else if (isJarProcess)
                {
                    Log.Information(
                        "发现 Java JAR 进程 PID={Pid}（-jar 兜底判定）: {JarHint}",
                        pid,
                        GetJarNameHint(commandLine));
                    results.Add((pid, commandLine));
                }
                else
                {
                    Log.Debug(
                        "进程 PID={Pid} 的命令行中没有服务器特征，跳过", pid);
                }
            }
            catch (InvalidOperationException)
            {
                Log.Debug("进程 PID={Pid} 已退出（扫描途中跑路了）", pid);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "进程扫描跳过: {Message}", ex.Message);
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
    /// 收集系统中所有 java/javaw 进程的 PID，并立即释放 Process 对象
    /// </summary>
    /// <returns>Java 进程 PID 列表</returns>
    /// <remarks>
    /// Process 对象持有非托管句柄，仅提取 PID 后立即 Dispose，
    /// 避免在后续循环中持有大量 Process 对象导致句柄泄漏。
    /// </remarks>
    private static List<int> CollectJavaProcessIds()
    {
        var pids = new List<int>();
        foreach (var process in Process.GetProcessesByName("java")
            .Concat(Process.GetProcessesByName("javaw")))
        {
            using (process)
            {
                try
                {
                    pids.Add(process.Id);
                }
                catch (InvalidOperationException)
                {
                    // 进程已退出，跳过
                }
            }
        }
        return pids;
    }

    /// <summary>
    /// 一次性 WMI 批量查询所有进程的 PID / 父PID / 命令行 / 名称
    /// </summary>
    /// <returns>PID 到进程信息的字典</returns>
    /// <remarks>
    /// 用一次批量查询替代原逐进程查询（原 N 次 CommandLine 查询 + N×5 次父进程链查询），
    /// 将 WMI 调用次数从 O(N) 降为 O(1)，显著降低自动检测循环（3 秒/次）的卡顿。
    /// </remarks>
    private static Dictionary<int, ProcessInfo> LoadAllProcessInfoBatch()
    {
        var map = new Dictionary<int, ProcessInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, ParentProcessId, CommandLine, Name FROM Win32_Process");
            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                using (obj)
                {
                    if (obj["ProcessId"] is int pid)
                    {
                        var parentId = obj["ParentProcessId"] as int? ?? 0;
                        var cmdLine = obj["CommandLine"]?.ToString() ?? string.Empty;
                        var name = obj["Name"]?.ToString() ?? string.Empty;
                        map[pid] = new ProcessInfo(parentId, cmdLine, name);
                    }
                }
            }
            Log.Debug("📊 批量 WMI 查询完成，共 {Count} 个进程", map.Count);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Log.Warning(ex, "⚠️ 批量 WMI 查询失败（COM 异常）: {Message}", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "⚠️ 批量 WMI 查询失败（权限不足）: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ 批量 WMI 查询失败: {Message}", ex.Message);
        }
        return map;
    }

    /// <summary>
    /// 进程信息结构 —— 批量 WMI 查询的单条结果
    /// </summary>
    /// <param name="ParentId">父进程 ID</param>
    /// <param name="CommandLine">完整命令行</param>
    /// <param name="Name">进程名称（含 .exe 后缀）</param>
    private readonly struct ProcessInfo(int parentId, string commandLine, string name)
    {
        public int ParentId { get; } = parentId;
        public string CommandLine { get; } = commandLine;
        public string Name { get; } = name;
    }

    /// <summary>
    /// 从批量缓存中构建 Shell + 启动器进程 ID 集合（内存过滤，无需额外进程枚举）
    /// </summary>
    /// <param name="processInfoMap">批量 WMI 查询结果</param>
    /// <returns>Shell/启动器进程 ID 的哈希集合</returns>
    private HashSet<int> BuildShellProcessIds(Dictionary<int, ProcessInfo> processInfoMap)
    {
        var ids = new HashSet<int>();
        // 合并 Shell 和启动器进程名，统一匹配
        var allLauncherNames = ShellProcessNames.Concat(LauncherProcessNames).ToArray();
        foreach (var kv in processInfoMap)
        {
            var name = kv.Value.Name;
            if (string.IsNullOrEmpty(name)) continue;

            var baseName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? name[..^4]
                : name;

            foreach (var launcher in allLauncherNames)
            {
                if (baseName.Equals(launcher, StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(kv.Key);
                    break;
                }
            }
        }
        Log.Debug("📊 发现 {Count} 个 Shell/启动器进程（内存过滤）", ids.Count);
        return ids;
    }

    /// <summary>
    /// 在内存中递归追溯父进程链，判定目标进程是否由Shell进程启动
    /// </summary>
    /// <param name="processId">目标进程ID</param>
    /// <param name="processInfoMap">批量 WMI 查询结果（PID → 进程信息）</param>
    /// <param name="shellProcessIds">Shell进程ID集合</param>
    /// <param name="depth">当前递归深度，用于防止无限追溯</param>
    /// <returns>若父进程链中存在Shell进程则返回<c>true</c>，否则返回<c>false</c></returns>
    /// <remarks>
    /// 原实现每层递归发起一次 WMI 查询，N 个进程最坏 N×5 次 WMI 调用；
    /// 现改为遍历内存中的批量缓存字典，零 WMI 调用。
    /// </remarks>
    private bool IsProcessLaunchedByShellInMemory(
        int processId,
        Dictionary<int, ProcessInfo> processInfoMap,
        HashSet<int> shellProcessIds,
        int depth = 0)
    {
        if (depth > MaxParentChainDepth)
        {
            Log.Debug("🔗 进程 PID={Pid} 父进程链深度超过 {Depth} 层，停止追溯", processId, MaxParentChainDepth);
            return false;
        }

        if (!processInfoMap.TryGetValue(processId, out var info))
            return false;

        int parentId = info.ParentId;
        if (parentId <= 0) return false;

        Log.Debug("🔗 进程 PID={Pid} 的父进程 PID={ParentId}", processId, parentId);

        if (shellProcessIds.Contains(parentId))
        {
            Log.Debug("✅ 进程 PID={Pid} 由 Shell 进程 PID={ParentId} 直接启动", processId, parentId);
            return true;
        }

        return IsProcessLaunchedByShellInMemory(parentId, processInfoMap, shellProcessIds, depth + 1);
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
    /// 检查命令行是否包含服务器进程标记（如 nogui）
    /// </summary>
    /// <param name="commandLine">进程完整命令行</param>
    /// <returns>若包含服务器标记则返回<c>true</c></returns>
    private bool HasServerProcessMarker(string commandLine)
    {
        var cmdLower = commandLine.ToLowerInvariant();
        foreach (var marker in ServerConstants.ServerProcessMarkers)
        {
            if (cmdLower.Contains(marker.ToLowerInvariant()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 兜底判定：命令行是否包含 -jar 参数且后跟 .jar 文件
    /// 在客户端已被排除的前提下，java -jar xxx.jar 大概率是服务器进程
    /// </summary>
    /// <param name="commandLine">进程完整命令行</param>
    /// <returns>若包含 -jar 且后跟 .jar 文件则返回<c>true</c></returns>
    private static bool IsJarProcess(string commandLine)
    {
        var cmdLower = commandLine.ToLowerInvariant();
        return cmdLower.Contains("-jar") && cmdLower.Contains(".jar");
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
