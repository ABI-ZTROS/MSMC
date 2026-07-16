// 🖥️ 系统监控实现 —— 像贴身管家一样时刻关注你服务器的健康状况
// 采集 CPU、内存、磁盘、Java 进程等各项指标
// 注意：PerformanceCounter 在 Linux 上不可用，所以我们用 try-catch 保护，优雅降级 😌
namespace McServerGuard.Services.SystemMonitoring;

using System.Diagnostics;
using McServerGuard.Models;
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

    public SystemMonitor(
        DiskSpaceMonitor diskMonitor,
        MemoryMonitor memoryMonitor,
        ThreadAnalyzer threadAnalyzer)
    {
        // 日志：初始化
        Log.Information("📊 SystemMonitor 初始化");
        _diskMonitor = diskMonitor;
        _memoryMonitor = memoryMonitor;
        _threadAnalyzer = threadAnalyzer;
        
        // 预热 CPU 计数器
        try
        {
            _cpuCounter = new PerformanceCounter(
                "Processor", "% Processor Time", "_Total", true);
            _cpuCounter.NextValue(); // 预热
            Log.Debug("CPU 计数器已预热");
        }
        catch (Exception ex)
        {
            Log.Debug("CPU 计数器预热失败（可能是 Linux 环境）: {Msg}", ex.Message);
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
        var (javaCount, javaWorkingSet, javaThreadCount) = GetJavaProcessStats();

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
    /// 使用 PerformanceCounter，Linux 上不可用所以需要 try-catch
    /// 注意：不再使用 Thread.Sleep，而是使用预热的计数器
    /// </summary>
    private double GetCpuUsage()
    {
        try
        {
            // 使用缓存的计数器避免每次重新创建
            if (_cpuCounter == null)
            {
                _cpuCounter = new PerformanceCounter(
                    "Processor", "% Processor Time", "_Total", true);
                // 第一次调用 NextValue() 返回 0，预热一下
                _cpuCounter.NextValue();
            }
            // 第二次调用返回真实值
            return _cpuCounter.NextValue();
        }
        catch (Exception ex)
        {
            // Linux 或者其他不支持 PerformanceCounter 的环境下会走到这里
            Log.Debug("PerformanceCounter 不可用（可能是 Linux 环境）: {Msg}", ex.Message);
            return 0;
        }
    }

    // CPU 计数器缓存
    private PerformanceCounter? _cpuCounter;

    /// <summary>
    /// 获取 Java 进程统计信息 —— 进程数量、总内存占用、总线程数
    /// </summary>
    private static (int ProcessCount, long WorkingSetBytes, int ThreadCount) GetJavaProcessStats()
    {
        var javaProcesses = Process.GetProcessesByName("java")
            .Concat(Process.GetProcessesByName("javaw"))
            .ToList();

        long totalWorkingSet = 0;
        int totalThreadCount = 0;

        foreach (var proc in javaProcesses)
        {
            using (proc)
            {
                try
                {
                    totalWorkingSet += proc.WorkingSet64;
                    totalThreadCount += proc.Threads.Count;
                    // 日志：Java 进程统计
                    Log.Debug("☕ Java 进程: PID={Pid} 线程数={Threads}", proc.Id, proc.Threads.Count);
                }
                catch (InvalidOperationException ex)
                {
                    // 进程已退出 —— 这不是错误，只是进程生命周期正常现象
                    Log.Debug("Java 进程已退出，跳过统计: {Message}", ex.Message);
                }
            }
        }

        return (javaProcesses.Count, totalWorkingSet, totalThreadCount);
    }
}
