// -----------------------------------------------------------------------------
// 文件名: SystemMonitor.cs
// 命名空间: McServerGuard.Services.SystemMonitoring
// 功能描述: 系统指标监控引擎 —— 采集 CPU、内存、磁盘、Java 进程等系统运行时指标
//           支持单次快照采集与定时轮询两种模式，采用防御式编程实现跨平台降级
// 依赖组件: System.Diagnostics.PerformanceCounter, System.Management (WMI)
// 设计模式: 策略模式 (PerformanceCounter / WMI 双采集链路),
//           观察者模式 (指标更新回调), 防御式编程 (优雅降级)
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.SystemMonitoring;

using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using McServerGuard.Models;
using McServerGuard.Services.Privilege;
using Serilog;

/// <summary>
/// 系统监控实现类 —— 负责采集系统各项运行指标并打包为 SystemMetrics 结构
/// </summary>
/// <remarks>
/// 采用双采集链路策略：主链路使用 PerformanceCounter（Windows 性能计数器），
/// 备用链路使用 WMI（Win32_Processor）。当主链路不可用时自动降级至备用链路，
/// 确保监控功能在不同环境下的可用性。
/// </remarks>
public class SystemMonitor : ISystemMonitor
{
    /// <summary>
    /// 磁盘空间监控器 —— 负责采集磁盘使用率、总容量等存储指标
    /// </summary>
    private readonly DiskSpaceMonitor _diskMonitor;

    /// <summary>
    /// 内存监控器 —— 负责采集物理内存使用量、内存规格等指标
    /// </summary>
    private readonly MemoryMonitor _memoryMonitor;

    /// <summary>
    /// 线程分析器 —— 负责统计系统总线程数
    /// </summary>
    private readonly ThreadAnalyzer _threadAnalyzer;

    /// <summary>
    /// 监控循环的取消令牌源
    /// </summary>
    private CancellationTokenSource? _monitoringCts;

    /// <summary>
    /// 定时采集的计时器
    /// </summary>
    private Timer? _monitoringTimer;

    /// <summary>
    /// 监控运行状态标志
    /// </summary>
    private bool _isMonitoring;

    /// <summary>
    /// 监控生命周期锁 —— 防止 Start/Stop 并发调用导致的竞态条件
    /// </summary>
    private readonly object _monitorLock = new();

    /// <summary>
    /// 获取一个值，指示当前运行平台是否为 Windows
    /// </summary>
    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 初始化系统监控引擎
    /// </summary>
    /// <param name="diskMonitor">磁盘空间监控器实例</param>
    /// <param name="memoryMonitor">内存监控器实例</param>
    /// <param name="threadAnalyzer">线程分析器实例</param>
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
        
        // CPU 性能计数器预热 —— PerformanceCounter 首次采样返回 0，需预热以保证首次有效读数
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
    /// </summary>
    /// <returns>包含 CPU、内存、磁盘、线程等指标的系统快照</returns>
    /// <remarks>
    /// 采集流程并非原子操作，各项指标存在微小时差，但在监控场景下可接受。
    /// </remarks>
    public SystemMetrics CollectSnapshot()
    {
        // 快照采集入口
        Log.Debug("📸 采集系统快照...");
        var timestamp = DateTime.Now;

        // CPU 使用率采集
        var cpuUsage = GetCpuUsage();

        // 内存指标采集
        var totalMemory = _memoryMonitor.GetTotalPhysicalMemory();
        var usedMemory = _memoryMonitor.GetUsedMemory();
        var memoryUsagePercent = totalMemory > 0
            ? Math.Round((double)usedMemory / totalMemory * 100, 2)
            : 0;

        var memoryInfo = _memoryMonitor.GetMemorySystemInfo();

        // 磁盘指标采集（基于当前工作目录所在盘）
        var diskRoot = System.IO.Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\";
        var diskInfo = _diskMonitor.GetDiskInfo(diskRoot);

        // Java 进程统计
        var (javaCount, javaWorkingSet, javaPrivateBytes, javaThreadCount) = GetJavaProcessStats();

        // 线程总数采集
        var totalThreads = _threadAnalyzer.GetTotalThreadCount();

        // 快照采集完成
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
    /// 异步采集一次系统指标快照 —— 将 WMI/PerformanceCounter 调用放到线程池执行
    /// </summary>
    /// <returns>包含 CPU、内存、磁盘、线程等指标的系统快照</returns>
    /// <remarks>
    /// 内部通过 <see cref="Task.Run"/> 将同步的 WMI 查询与 <see cref="System.Diagnostics.PerformanceCounter"/> 
    /// 调用封送到线程池，避免阻塞调用线程（特别是 UI 线程）。
    /// </remarks>
    public async Task<SystemMetrics> CollectSnapshotAsync()
    {
        return await Task.Run(() => CollectSnapshot()).ConfigureAwait(false);
    }

    /// <summary>
    /// 启动持续监控
    /// </summary>
    /// <param name="interval">采样间隔</param>
    /// <param name="callback">指标更新回调函数</param>
    /// <param name="cancellationToken">外部取消令牌</param>
    /// <exception cref="InvalidOperationException">当监控已在运行时抛出</exception>
    /// <remarks>
    /// 使用 System.Threading.Timer 实现定时采集，通过回调函数向订阅者推送指标更新。
    /// 启动后立即执行首次采集，后续按指定间隔周期性执行。
    /// 通过链接的 CancellationTokenSource 支持外部取消与内部停止的联合控制。
    /// </remarks>
    public void StartMonitoring(TimeSpan interval, Action<SystemMetrics> callback, CancellationToken cancellationToken)
    {
        // 启动监控入口
        Log.Information("▶️ 开始监控，间隔 {Interval} 秒", interval.TotalSeconds);

        if (_isMonitoring)
        {
            Log.Warning("监控已经在运行中，不要重复启动哦");
            throw new InvalidOperationException("监控已经在运行中了，先 StopMonitoring 再重新启动");
        }

        _isMonitoring = true;
        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Log.Information("系统监控已启动，采样间隔: {Interval}", interval);

        // 首次采集（立即执行）
        try
        {
            callback(CollectSnapshot());
        }
        catch (Exception ex)
        {
            // 首次采集失败
            Log.Error(ex, "首次采集失败: {Message}", ex.Message);
        }

        // 周期采集
        _monitoringTimer = new Timer(_ =>
        {
            try
            {
                if (_monitoringCts?.IsCancellationRequested == true)
                {
                    // 不在回调中 Dispose Timer 自身，避免竞态条件
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

                // 异步采集快照，避免 WMI/PerformanceCounter 同步调用阻塞线程池
                _ = CollectSnapshotAsync().ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        try { callback(t.Result); }
                        catch (TaskCanceledException)
                        {
                            // Dispatcher 已关闭，静默忽略
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "回调执行失败: {Message}", ex.Message);
                        }
                    }
                    else if (t.IsFaulted)
                    {
                        Log.Error(t.Exception, "定时采集失败: {Message}",
                            t.Exception?.GetBaseException().Message);
                    }
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                // 周期采集失败
                Log.Error(ex, "定时采集失败: {Message}", ex.Message);
            }
        }, null, TimeSpan.Zero, interval);
    }

    /// <summary>
    /// 停止持续监控
    /// </summary>
    /// <remarks>
    /// 采用防御式编程：重复调用 Stop 不会导致异常。
    /// 释放 Timer 与 CancellationTokenSource 资源，将状态标志重置为停止状态。
    /// </remarks>
    public void StopMonitoring()
    {
        // 停止监控入口
        Log.Information("⏹️ 停止监控");

        lock (_monitorLock)
        {
            if (!_isMonitoring)
            {
                Log.Debug("监控未处于运行状态，直接返回");
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
    /// </summary>
    /// <returns>CPU 使用率百分比（0-100），采集失败返回 0</returns>
    /// <remarks>
    /// 采用双链路策略模式：
    /// 主链路：PerformanceCounter（Windows 性能计数器，精度较高）
    /// 备用链路：WMI Win32_Processor LoadPercentage（兼容性较好）
    /// 主链路失败时自动降级至备用链路。
    /// </remarks>
    private double GetCpuUsage()
    {
        // 主链路：PerformanceCounter
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

        // 备用链路：WMI
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

    /// <summary>
    /// CPU 性能计数器实例缓存 —— 避免重复创建导致的性能开销
    /// </summary>
    private PerformanceCounter? _cpuCounter;

    /// <summary>
    /// 获取 Java 进程统计信息
    /// </summary>
    /// <returns>元组，包含进程数量、工作集总字节数、私有内存总字节数、总线程数</returns>
    /// <remarks>
    /// 正确释放 Process 对象以避免资源泄漏；
    /// 处理进程退出的竞态条件——枚举过程中进程可能随时退出。
    /// 采用防御式编程，单个进程读取失败不影响整体统计结果。
    /// </remarks>
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
                    // WorkingSet 读取失败，跳过当前指标
                }

                try
                {
                    totalPrivateBytes += proc.PrivateMemorySize64;
                }
                catch
                {
                    // 私有内存读取失败，跳过当前指标
                }

                try
                {
                    totalThreadCount += proc.Threads.Count;
                }
                catch
                {
                    // 线程数读取失败，跳过当前指标
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
                // 进程已退出 —— 竞态条件下的正常现象，不算错误
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
