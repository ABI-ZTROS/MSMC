// -----------------------------------------------------------------------------
// 文件名: ProcessManagerService.cs
// 命名空间: McServerGuard.Services.SystemMonitoring
// 功能描述: 进程管理服务 —— 获取 Java 进程亲和性信息、杀进程、设置 CPU 亲和性
// 依赖组件: System.Diagnostics, Serilog, McServerGuard.Services.ServerDetection
// 设计模式: 仓储模式（进程信息查询），防御式编程（进程退出竞态处理）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.SystemMonitoring;

using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using McServerGuard.Models;
using McServerGuard.Services;
using McServerGuard.Services.ServerDetection;
using Serilog;

/// <summary>
/// 进程管理服务实现 —— 提供 Java 进程亲和性查询与进程管理操作
/// </summary>
/// <remarks>
/// 通过 <see cref="Process.ProcessorAffinity"/> 获取进程允许运行的 CPU 核心掩码，
/// 通过 <see cref="Process.TotalProcessorTime"/> 差分计算进程 CPU 使用率。
/// 杀进程采用优雅停止 → 3s 超时 → 强杀模式（复用 NetworkService.KillProcessByPort 模式）。
/// </remarks>
public class ProcessManagerService : IProcessManagerService
{
    private readonly ProcessScanner _processScanner;
    private readonly IPrivilegeService _privilegeService;
    private readonly TimeService _timeService;

    /// <summary>上次采样的进程 CPU 时间，用于差分计算 CPU 使用率</summary>
    private readonly Dictionary<int, (long TickMs, TimeSpan TotalProcessorTime)> _lastCpuSample = new();

    /// <summary>进程列表缓存（使用单调时钟隔离 NTP 污染）</summary>
    private (long TickMs, List<ProcessAffinityInfo> Data)? _affinityCache;

    /// <summary>缓存 TTL（毫秒）</summary>
    private const int CacheTtlMs = 2000;

    /// <summary>单次返回的最大进程数（避免前端渲染过载）</summary>
    private const int MaxProcessCount = 200;

    /// <summary>系统关键进程名集合（这些进程不允许杀/改亲和性）</summary>
    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System Idle Process", "System", "Registry", "smss", "csrss", "wininit",
        "services", "lsass", "svchost", "fontdrvhost", "dwm", "explorer",
        "Taskmgr", "spoolsv", "winlogon", "MsMpEng", "SearchIndexer",
        "sihost", "taskhostw", "ctfmon", "conhost", "RuntimeBroker"
    };

    public ProcessManagerService(
        ProcessScanner processScanner,
        IPrivilegeService privilegeService,
        TimeService timeService)
    {
        _processScanner = processScanner;
        _privilegeService = privilegeService;
        _timeService = timeService;
    }

    /// <inheritdoc/>
    public List<ProcessAffinityInfo> GetAllProcessAffinities()
    {
        // 检查缓存（使用单调时钟，隔离 NTP 时间偏移污染）
        var nowTick = Environment.TickCount64;
        if (_affinityCache.HasValue && (nowTick - _affinityCache.Value.TickMs) < CacheTtlMs)
            return _affinityCache.Value.Data;

        // 获取 Minecraft 服务器 PID 集合（用于标记 IsMinecraftServer）
        var minecraftPids = new HashSet<int>();
        try
        {
            var serverProcesses = _processScanner.ScanServerProcesses();
            foreach (var (pid, _) in serverProcesses)
                minecraftPids.Add(pid);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取 Minecraft 服务器进程列表失败");
        }

        var result = new List<ProcessAffinityInfo>();

        // 枚举所有进程（不再只枚举 java/javaw）
        Process[] allProcesses;
        try
        {
            allProcesses = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取系统进程列表失败");
            return result;
        }

        foreach (var proc in allProcesses)
        {
            try
            {
                if (proc.HasExited)
                    continue;

                var pid = proc.Id;
                var processName = proc.ProcessName;
                var isMinecraft = minecraftPids.Contains(pid);
                var isJava = processName.Equals("java", StringComparison.OrdinalIgnoreCase)
                          || processName.Equals("javaw", StringComparison.OrdinalIgnoreCase);
                var isSystem = pid <= 4 || SystemProcessNames.Contains(processName);

                // 获取 CPU 亲和性掩码
                long affinityMask = 0;
                try
                {
                    affinityMask = proc.ProcessorAffinity.ToInt64();
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
                {
                    // 权限不足，无法读取其他用户进程的亲和性
                }
                catch (Exception)
                {
                    // 其他异常，忽略
                }

                // 亲和性掩码转核心索引列表
                var allowedCores = AffinityMaskToCoreIndices(affinityMask);

                // 获取 CPU 使用率（差分计算，使用单调时钟）
                double cpuUsage = 0;
                try
                {
                    var currentCpuTime = proc.TotalProcessorTime;
                    if (_lastCpuSample.TryGetValue(pid, out var lastSample))
                    {
                        var tickDiff = nowTick - lastSample.TickMs;
                        if (tickDiff > 0)
                        {
                            var cpuTimeDiff = currentCpuTime - lastSample.TotalProcessorTime;
                            // CPU% = 进程CPU时间增量 / (经过时间 × 核心数) × 100
                            cpuUsage = Math.Round(
                                cpuTimeDiff.TotalMilliseconds / (tickDiff * Environment.ProcessorCount) * 100,
                                2);
                            cpuUsage = Math.Max(0, Math.Min(100, cpuUsage));
                        }
                    }
                    _lastCpuSample[pid] = (nowTick, currentCpuTime);
                }
                catch (Exception)
                {
                    // 读取 CPU 时间失败，跳过
                }

                // 获取工作集内存
                long workingSet = 0;
                try { workingSet = proc.WorkingSet64; }
                catch { }

                // 获取线程数
                int threadCount = 0;
                try { threadCount = proc.Threads.Count; }
                catch { }

                // 获取优先级
                string priorityClass = string.Empty;
                try { priorityClass = proc.PriorityClass.ToString(); }
                catch { }

                // 获取命令行（截断）
                string commandLine = string.Empty;
                try { commandLine = Truncate(proc.MainModule?.FileName ?? string.Empty, 200); }
                catch { }

                // 显示名：Minecraft 优先，否则用进程名
                string displayName = isMinecraft
                    ? $"Minecraft Server (PID:{pid})"
                    : processName;

                result.Add(new ProcessAffinityInfo
                {
                    ProcessId = pid,
                    ProcessName = processName,
                    IsMinecraftServer = isMinecraft,
                    IsJavaProcess = isJava,
                    IsSystemProcess = isSystem,
                    DisplayName = displayName,
                    AffinityMask = affinityMask,
                    AllowedCoreIndices = allowedCores,
                    CpuUsagePercent = cpuUsage,
                    WorkingSetBytes = workingSet,
                    ThreadCount = threadCount,
                    PriorityClass = priorityClass,
                    CommandLine = commandLine,
                });
            }
            catch (InvalidOperationException)
            {
                // 进程已退出，跳过
            }
            catch (Exception ex)
            {
                Log.Debug("读取进程信息失败: {Message}", ex.Message);
            }
            finally
            {
                proc.Dispose();
            }
        }

        // 按 CPU 使用率降序排序，取前 MaxProcessCount 个（避免前端渲染过载）
        result.Sort((a, b) => b.CpuUsagePercent.CompareTo(a.CpuUsagePercent));
        if (result.Count > MaxProcessCount)
            result = result.Take(MaxProcessCount).ToList();

        // 清理已退出进程的 CPU 采样缓存
        CleanupStaleCpuSamples(result.Select(p => p.ProcessId).ToHashSet());

        _affinityCache = (nowTick, result);
        Log.Debug("📊 获取到 {Count} 个进程亲和性信息（其中 Java={Java}, Minecraft={Mc}, System={Sys}）",
            result.Count,
            result.Count(p => p.IsJavaProcess),
            result.Count(p => p.IsMinecraftServer),
            result.Count(p => p.IsSystemProcess));

        return result;
    }

    /// <inheritdoc/>
    [Obsolete("请使用 GetAllProcessAffinities")]
    public List<ProcessAffinityInfo> GetJavaProcessAffinities()
    {
        // 旧接口实现：仅返回 Java 进程，委托新接口过滤
        return GetAllProcessAffinities()
            .Where(p => p.IsJavaProcess)
            .ToList();
    }

    /// <inheritdoc/>
    public ProcessAffinityInfo? GetProcessInfo(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            long affinityMask = 0;
            try { affinityMask = proc.ProcessorAffinity.ToInt64(); }
            catch { }

            long workingSet = 0;
            try { workingSet = proc.WorkingSet64; }
            catch { }

            int threadCount = 0;
            try { threadCount = proc.Threads.Count; }
            catch { }

            string priorityClass = string.Empty;
            try { priorityClass = proc.PriorityClass.ToString(); }
            catch { }

            // 判断是否为 Minecraft 服务器
            bool isMinecraft = false;
            try
            {
                var serverProcesses = _processScanner.ScanServerProcesses();
                isMinecraft = serverProcesses.Any(s => s.ProcessId == pid);
            }
            catch { }

            return new ProcessAffinityInfo
            {
                ProcessId = pid,
                ProcessName = proc.ProcessName,
                IsMinecraftServer = isMinecraft,
                IsJavaProcess = proc.ProcessName.Equals("java", StringComparison.OrdinalIgnoreCase)
                             || proc.ProcessName.Equals("javaw", StringComparison.OrdinalIgnoreCase),
                IsSystemProcess = pid <= 4 || SystemProcessNames.Contains(proc.ProcessName),
                DisplayName = isMinecraft ? $"Minecraft Server (PID:{pid})" : proc.ProcessName,
                AffinityMask = affinityMask,
                AllowedCoreIndices = AffinityMaskToCoreIndices(affinityMask),
                CpuUsagePercent = 0, // 单次查询无法计算差分
                WorkingSetBytes = workingSet,
                ThreadCount = threadCount,
                PriorityClass = priorityClass,
                CommandLine = string.Empty,
            };
        }
        catch (ArgumentException)
        {
            Log.Debug("进程 PID={Pid} 不存在或已退出", pid);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取进程 {Pid} 信息失败", pid);
            return null;
        }
    }

    /// <inheritdoc/>
    public (bool Success, string? Error) KillProcess(int pid, bool graceful = true)
    {
        try
        {
            using var process = Process.GetProcessById(pid);

            if (graceful)
            {
                try
                {
                    if (process.CloseMainWindow())
                    {
                        Log.Information("已请求优雅停止进程 {Name} (PID={Pid})，等待 3 秒",
                            process.ProcessName, pid);
                        if (process.WaitForExit(3000))
                        {
                            Log.Information("进程已优雅退出: {Name} (PID={Pid})",
                                process.ProcessName, pid);
                            return (true, null);
                        }
                        Log.Warning("优雅停止超时，强杀进程: {Name} (PID={Pid})",
                            process.ProcessName, pid);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "CloseMainWindow 失败，降级到强杀");
                }
            }

            process.Kill(entireProcessTree: true);
            Log.Information("已强杀进程 {Name} (PID={Pid})", process.ProcessName, pid);
            return (true, null);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            Log.Error(ex, "结束进程 PID={Pid} 失败：权限不足", pid);
            _privilegeService.EnsureAdminPrivileges($"终止进程 PID={pid}");
            return (false, "权限不足，请以管理员身份运行程序");
        }
        catch (ArgumentException)
        {
            return (false, "进程不存在或已退出");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "结束进程 PID={Pid} 失败", pid);
            return (false, ex.Message);
        }
    }

    /// <inheritdoc/>
    public (bool Success, string? Error) SetProcessAffinity(int pid, long affinityMask)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.ProcessorAffinity = new IntPtr(affinityMask);
            Log.Information("已设置进程 {Name} (PID={Pid}) 的 CPU 亲和性掩码: 0x{Mask:X}",
                process.ProcessName, pid, affinityMask);
            return (true, null);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            Log.Error(ex, "设置进程 PID={Pid} 亲和性失败：权限不足", pid);
            _privilegeService.EnsureAdminPrivileges($"设置进程 PID={pid} 的 CPU 亲和性");
            return (false, "权限不足，请以管理员身份运行程序");
        }
        catch (ArgumentException)
        {
            return (false, "进程不存在或已退出");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "设置进程 PID={Pid} 亲和性失败", pid);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 将 CPU 亲和性掩码转换为逻辑核编号列表
    /// </summary>
    private static int[] AffinityMaskToCoreIndices(long affinityMask)
    {
        var cores = new List<int>();
        for (int i = 0; i < 64; i++)
        {
            if ((affinityMask & (1L << i)) != 0)
                cores.Add(i);
        }
        return [.. cores];
    }

    /// <summary>
    /// 截断字符串到指定长度
    /// </summary>
    private static string Truncate(string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        return str.Length <= maxLength ? str : str[..maxLength];
    }

    /// <summary>
    /// 清理不再存在的进程的 CPU 采样缓存
    /// </summary>
    private void CleanupStaleCpuSamples(HashSet<int> currentPids)
    {
        var staleKeys = _lastCpuSample.Keys.Where(k => !currentPids.Contains(k)).ToList();
        foreach (var key in staleKeys)
            _lastCpuSample.Remove(key);
    }
}
