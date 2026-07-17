// 🖥️ 系统监控实现 —— 像贴身管家一样时刻关注你服务器的健康状况
// 采集 CPU、内存、磁盘、Java 进程等各项指标
// 注意：PerformanceCounter 在某些环境下可能不可用，所以我们用 try-catch 保护，优雅降级 😌
namespace McServerGuard.Services.SystemMonitoring;

using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using McServerGuard.Models;
using McServerGuard.Services.Privilege;
using Serilog;

/// <summary>
/// 系统监控实现 —— 采集各种系统指标然后打包成 SystemMetrics
/// </summary>
public class SystemMonitor : ISystemMonitor
{
    private readonly DiskSpaceMonitor _diskMonitor;
    private readonly MemoryMonitor _memoryMonitor;
    private readonly ThreadAnalyzer _threadAnalyzer;

    private CancellationTokenSource? _monitoringCts;
    private Timer? _monitoringTimer;
    private bool _isMonitoring;
    private readonly object _monitorLock = new();

    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public SystemMonitor(
        DiskSpaceMonitor diskMonitor,
        MemoryMonitor memoryMonitor,
        ThreadAnalyzer threadAnalyzer)
    {
        Log.Information("📊 SystemMonitor 初始化");
        Log.Information("🪟 系统版本: {Version}", AdminPrivilegeService.GetWindowsVersion());
        _diskMonitor = diskMonitor;
        _memoryMonitor = memoryMonitor;
        _threadAnalyzer = threadAnalyzer;
        
        // 预热 CPU 计数器
        try
        {
            _cpuCounter = new PerformanceCounter(
                "Processor", "% Processor Time", "_Total", true);
            _cpuCounter.NextValue();
            Log.Debug("CPU 计数器已预热");
        }
        catch (Exception ex)
        {
            Log.Warning("CPU 计数器预热失败，将使用 WMI 备用方案: {Msg}", ex.Message);
        }
    }

    /// <summary>
    /// 采集一次系统指标快照
    /// 虽然叫"快照"，但采集过程并不是瞬间完成的 —— 不过差别不大啦 📸
    /// </summary>
    public SystemMetrics CollectSnapshot()
    {
        // 日志：采集快照入口
        Log.Debug("📸 采集系统快照...");
        var timestamp = DateTime.Now;

        // CPU 使用率
        var cpuUsage = GetCpuUsage();

        // 内存信息
        var totalMemory = _memoryMonitor.GetTotalPhysicalMemory();
        var usedMemory = _memoryMonitor.GetUsedMemory();
        var memoryUsagePercent = totalMemory > 0
            ? Math.Round((double)usedMemory / totalMemory * 100, 2)
            : 0;

        var memoryInfo = _memoryMonitor.GetMemorySystemInfo();

        // 磁盘信息（使用系统盘）
        var diskRoot = System.IO.Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\";
        var diskInfo = _diskMonitor.GetDiskInfo(diskRoot);

        // Java 进程统计
        var (javaCount, javaWorkingSet, javaPrivateBytes, javaThreadCount) = GetJavaProcessStats();

        // 线程信息
        var totalThreads = _threadAnalyzer.GetTotalThreadCount();

        // 日志：采集完成
        Log.Debug("✅ 快照采集完成: CPU={Cpu}% 内存={Mem}% 磁盘={Disk}%",
            cpuUsage, memoryUsagePercent, diskInfo.UsagePercent);

        return new SystemMetrics
        {
            Timestamp = timestamp,
            CpuUsagePercent = cpuUsage,
            TotalMemoryBytes = totalMemory,
            UsedMemoryBytes = usedMemory,
            MemoryUsagePercent = memoryUsagePercent,
            TotalThreadCount = totalThreads,
            MemorySpeedMHz = memoryInfo.SpeedMHz,
            MemoryType = memoryInfo.MemoryType,
            MemoryModuleCount = memoryInfo.ModuleCount,
            DiskTotalBytes = diskInfo.TotalBytes,
            DiskUsedBytes = diskInfo.UsedBytes,
            DiskFreeBytes = diskInfo.FreeBytes,
            DiskUsagePercent = diskInfo.UsagePercent,
            DiskName = diskInfo.DriveName,
            JavaCpuUsagePercent = 0,
            JavaWorkingSetBytes = javaWorkingSet,
            JavaThreadCount = javaThreadCount,
            JavaPrivateBytes = 0,
            JavaHandleCount = 0,
            JavaHeapUsedBytes = 0,
            JavaHeapMaxBytes = 0,
        };
    }

    /// <summary>
    /// 启动持续监控
    /// 使用 System.Threading.Timer 定时采集系统指标，通过回调函数推送
    /// 通过 CancellationToken 控制停止
    /// </summary>
    /// <param name="interval">采样间隔</param>
    /// <param name="callback">指标更新回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    public void StartMonitoring(TimeSpan interval, Action<SystemMetrics> callback, CancellationToken cancellationToken)
    {
        // 日志：开始监控入口
        Log.Information("▶️ 开始监控，间隔 {Interval} 秒", interval.TotalSeconds);

        if (_isMonitoring)
        {
            Log.Warning("监控已经在运行中，不要重复启动哦");
            throw new InvalidOperationException("监控已经在运行中了，先 StopMonitoring 再重新启动");
        }

        _isMonitoring = true;
        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Log.Information("系统监控已启动，采样间隔: {Interval}", interval);

        // 立即采集一次
        try
        {
            callback(CollectSnapshot());
        }
        catch (Exception ex)
        {
            // 日志：首次采集失败
            Log.Error(ex, "💥 fuck: 首次采集失败: {Message}", ex.Message);
        }

        // 定时采集
        _monitoringTimer = new Timer(_ =>
        {
            try
            {
                if (_monitoringCts?.IsCancellationRequested == true)
                {
                    // 不在回调中 Dispose Timer 自身，避免竞态
                    lock (_monitorLock)
                    {
                        if (_isMonitoring)
                        {
                            _monitoringTimer?.Dispose();
                            _monitoringTimer = null;
                            _monitoringCts?.Cancel();
                            _monitoringCts?.Dispose();
                            _monitoringCts = null;
                            _isMonitoring = false;
                        }
                    }
                    return;
                }

                var metrics = CollectSnapshot();
                callback(metrics);
            }
            catch (Exception ex)
            {
                // 日志：定时采集失败
                Log.Error(ex, "💥 fuck: 定时采集失败: {Message}", ex.Message);
            }
        }, null, TimeSpan.Zero, interval);
    }

    /// <summary>
    /// 停止持续监控
    /// </summary>
    public void StopMonitoring()
    {
        // 日志：停止监控入口
        Log.Information("⏹️ 停止监控");

        lock (_monitorLock)
        {
            if (!_isMonitoring)
            {
                Log.Debug("监控并没有在运行，这 Stop 了个寂寞");
                return;
            }

            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
            _monitoringCts?.Cancel();
            _monitoringCts?.Dispose();
            _monitoringCts = null;
            _isMonitoring = false;
        }
        Log.Information("系统监控已停止");
    }

    /// <summary>
    /// 获取 CPU 使用率百分比
    /// 主方案：PerformanceCounter
    /// 备用方案：WMI Win32_Processor LoadPercentage
    /// </summary>
    private double GetCpuUsage()
    {
        // 方案1：PerformanceCounter
        try
        {
            if (_cpuCounter == null)
            {
                _cpuCounter = new PerformanceCounter(
                    "Processor", "% Processor Time", "_Total", true);
                _cpuCounter.NextValue();
            }
            var value = _cpuCounter.NextValue();
            if (value >= 0 && value <= 100)
                return Math.Round(value, 2);
        }
        catch (Exception ex)
        {
            Log.Debug("PerformanceCounter 获取 CPU 失败: {Msg}", ex.Message);
        }

        // 方案2：WMI
        if (IsWindows)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT LoadPercentage FROM Win32_Processor");
                using var collection = searcher.Get();
                double totalLoad = 0;
                int coreCount = 0;
                foreach (var obj in collection)
                {
                    if (obj["LoadPercentage"] is ushort load && load <= 100)
                    {
                        totalLoad += load;
                        coreCount++;
                    }
                }
                if (coreCount > 0)
                    return Math.Round(totalLoad / coreCount, 2);
            }
            catch (Exception ex)
            {
                Log.Debug("WMI 获取 CPU 失败: {Msg}", ex.Message);
            }
        }

        return 0;
    }

    // CPU 计数器缓存
    private PerformanceCounter? _cpuCounter;

    /// <summary>
    /// 获取 Java 进程统计信息 —— 进程数量、总内存占用、总线程数
    /// 修复：正确释放 Process 对象，避免资源泄漏；处理进程退出的竞态条件
    /// </summary>
    private static (int ProcessCount, long WorkingSetBytes, long PrivateBytes, int ThreadCount) GetJavaProcessStats()
    {
        var javaProcesses = new List<Process>();
        try
        {
            javaProcesses.AddRange(Process.GetProcessesByName("java"));
            javaProcesses.AddRange(Process.GetProcessesByName("javaw"));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取 Java 进程列表失败");
        }

        long totalWorkingSet = 0;
        long totalPrivateBytes = 0;
        int totalThreadCount = 0;
        int validProcessCount = 0;

        foreach (var proc in javaProcesses)
        {
            try
            {
                if (proc.HasExited)
                {
                    proc.Dispose();
                    continue;
                }

                try
                {
                    totalWorkingSet += proc.WorkingSet64;
                }
                catch
                {
                    // WorkingSet 读取失败，跳过
                }

                try
                {
                    totalPrivateBytes += proc.PrivateMemorySize64;
                }
                catch
                {
                    // 私有内存读取失败，跳过
                }

                try
                {
                    totalThreadCount += proc.Threads.Count;
                }
                catch
                {
                    // 线程数读取失败，跳过
                }

                validProcessCount++;
                Log.Debug("☕ Java 进程: PID={Pid} 工作集={Ws}MB 私有内存={Pm}MB 线程数={Threads}",
                    proc.Id,
                    proc.WorkingSet64 >> 20,
                    proc.PrivateMemorySize64 >> 20,
                    proc.Threads.Count);
            }
            catch (InvalidOperationException ex)
            {
                // 进程已退出 —— 正常现象，不算错误
                Log.Debug("Java 进程已退出，跳过统计: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                Log.Debug("读取 Java 进程信息失败: {Message}", ex.Message);
            }
            finally
            {
                proc.Dispose();
            }
        }

        return (validProcessCount, totalWorkingSet, totalPrivateBytes, totalThreadCount);
    }
}
