// -----------------------------------------------------------------------------
// 文件名: SystemMonitor.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Services
// 功能描述: 系统指标监控引擎 —— 采集 CPU、内存、磁盘、Java 进程等系统运行时指标
//           支持单次快照采集与定时轮询两种模式，采用防御式编程实现跨平台降级
// 依赖组件: System.Diagnostics.PerformanceCounter, System.Management (WMI)
// 设计模式: 策略模式 (PerformanceCounter / WMI 双采集链路),
//           观察者模式 (指标更新回调), 防御式编程 (优雅降级)
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SystemMonitoring.Services;

using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using io.NET.ZTR_OS.Features.SystemMonitoring.Models;
using io.NET.ZTR_OS.Features.SystemMonitoring.Models.Hardware;
using io.NET.ZTR_OS.Features.Startup.Services;
using io.NET.ZTR_OS.Features.Startup.Services.Privilege;
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
    /// 时间服务 —— 统一时间来源
    /// </summary>
    private readonly TimeService _timeService;

    /// <summary>
    /// 监控循环的取消令牌源
    /// </summary>
    private CancellationTokenSource? _monitoringCts;

    private Timer? _monitoringTimer;

    private bool _isMonitoring;

    /// <summary>
    /// 当前正在执行的回调计数（Timer 回调 + 异步采集延续）
    /// 用于 StopMonitoringAsync 等待所有在途回调完成后再释放资源，
    /// 避免回调访问已释放的 CTS 导致 ObjectDisposedException。
    /// </summary>
    private int _activeCallbackCount;

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
    public SystemMonitor(
        DiskSpaceMonitor diskMonitor,
        MemoryMonitor memoryMonitor,
        ThreadAnalyzer threadAnalyzer,
        TimeService timeService)
    {
        Log.Information("[METRIC] SystemMonitor 初始化");
        Log.Information("[UI] 系统版本: {Version}", AdminPrivilegeService.GetWindowsVersion());
        _diskMonitor = diskMonitor;
        _memoryMonitor = memoryMonitor;
        _threadAnalyzer = threadAnalyzer;
        _timeService = timeService;
        
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

        // 每核 CPU 计数器初始化
        InitPerCoreCounters();
    }

    /// <summary>
    /// 初始化每核 CPU 性能计数器
    /// </summary>
    private void InitPerCoreCounters()
    {
        try
        {
            var coreCount = Environment.ProcessorCount;
            _perCoreCpuCounters = new PerformanceCounter[coreCount];
            for (int i = 0; i < coreCount; i++)
            {
                _perCoreCpuCounters[i] = new PerformanceCounter(
                    "Processor",
                    "% Processor Time",
                    i.ToString(),
                    true);
                _perCoreCpuCounters[i].NextValue();
            }
            Log.Debug("每核 CPU 计数器已初始化，共 {Count} 个核心", coreCount);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "初始化每核 CPU 计数器失败: {Msg}", ex.Message);
            _perCoreCpuCounters = null;
        }
    }

    /// <summary>
    /// 采集一次系统指标快照
    /// </summary>
    /// <remarks>
    /// 采集流程并非原子操作，各项指标存在微小时差，但在监控场景下可接受。
    /// </remarks>
    public SystemMetrics CollectSnapshot()
    {
        Log.Debug("[SNAP] 采集系统快照...");
        // v2: 壁钟直接用 DateTime.Now（不再叠加 NTP 偏移）
        var timestamp = DateTime.Now;

        var cpuUsage = GetCpuUsage();
        var perCoreUsages = GetPerCoreCpuUsage();

        var totalMemory = GetCachedTotalMemory();
        var usedMemory = _memoryMonitor.GetUsedMemory();
        var memoryUsagePercent = totalMemory > 0
            ? Math.Round((double)usedMemory / totalMemory * 100, 2)
            : 0;

        var memoryInfo = GetCachedMemoryInfo();

        var diskRoot = System.IO.Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\";
        var diskInfo = _diskMonitor.GetDiskInfo(diskRoot);

        var (javaCount, javaWorkingSet, javaPrivateBytes, javaThreadCount) = GetJavaProcessStats();

        var totalThreads = _threadAnalyzer.GetTotalThreadCount();

        Log.Debug("[OK] 快照采集完成: CPU={Cpu}% 内存={Mem}% 磁盘={Disk}%",
            cpuUsage, memoryUsagePercent, diskInfo.UsagePercent);

        return new SystemMetrics
        {
            Timestamp = timestamp,
            CpuUsagePercent = cpuUsage,
            PerCoreCpuUsages = perCoreUsages,
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

        // P2 修复：使用 lock 保护 _isMonitoring 的读-检查-写操作，防止多线程并发 Start 导致 Timer/CTS 泄漏
        lock (_monitorLock)
        {
            if (_isMonitoring)
            {
                Log.Warning("监控已经在运行中，不要重复启动哦");
                throw new InvalidOperationException("监控已经在运行中了，先 StopMonitoring 再重新启动");
            }

            _isMonitoring = true;
            _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

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
            // P1 修复：在途回调计数，确保 StopMonitoringAsync 等待所有回调完成后再释放资源
            Interlocked.Increment(ref _activeCallbackCount);
            bool startedAsync = false;
            try
            {
                if (_monitoringCts?.IsCancellationRequested == true)
                    return;

                if (_isCollecting)
                {
                    Log.Debug("上一次采集尚未完成，跳过本次");
                    return;
                }

                _isCollecting = true;
                startedAsync = true;
                _ = CollectSnapshotAsync().ContinueWith(t =>
                {
                    try
                    {
                        _isCollecting = false;
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
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _activeCallbackCount);
                    }
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                // 周期采集失败
                Log.Error(ex, "定时采集失败: {Message}", ex.Message);
            }
            finally
            {
                // 若未启动异步采集（提前返回或异常），在此处递减计数
                if (!startedAsync)
                {
                    Interlocked.Decrement(ref _activeCallbackCount);
                }
            }
        }, null, TimeSpan.Zero, interval);
    }

    /// <summary>
    /// 异步停止持续监控
    /// </summary>
    /// <remarks>
    /// 采用防御式编程：重复调用 Stop 不会导致异常。
    /// 执行链安全保证（严格按以下顺序执行）：
    /// 1. 停止 Timer（Change(Timeout.Infinite)），防止新的回调触发
    /// 2. 取消 CTS，通知在途操作尽早退出
    /// 3. 等待在途回调完成（带 10 秒超时保护，防止死锁）
    /// 4. 释放 Timer（DisposeAsync 等待 Timer 自身回调完全结束）
    /// 5. 最后释放 CTS（此时所有回调均已完成，不会再访问 CTS）
    /// 确保方法返回后，Timer 回调不再访问已释放的资源。
    /// 失败均有日志记录，不静默失败（返回链原则）。
    /// </remarks>
    public async Task StopMonitoringAsync()
    {
        // 停止监控入口
        Log.Information("⏹️ 停止监控（异步）");

        Timer? timerToDispose;
        CancellationTokenSource? ctsToDispose;
        bool needWaitForDrain;

        lock (_monitorLock)
        {
            if (!_isMonitoring)
            {
                Log.Debug("监控未处于运行状态，直接返回");
                return;
            }

            // 1. 先停止 Timer，防止新的回调触发
            _monitoringTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            timerToDispose = _monitoringTimer;
            _monitoringTimer = null;

            // 2. 取消 CTS，通知正在进行的操作停止
            _monitoringCts?.Cancel();
            ctsToDispose = _monitoringCts;
            _monitoringCts = null;

            _isMonitoring = false;

            // 3. 检查是否有在途回调需要等待
            needWaitForDrain = Volatile.Read(ref _activeCallbackCount) > 0;
        }

        // 重置降级状态，下次启动时重新评估主链路可用性
        _cpuFallbackMode = false;
        _cpuPrimaryFailureCount = 0;
        Log.Debug("CPU 采集链路降级状态已重置");

        // 4. 等待在途回调完成（带超时保护，防止死锁）
        if (needWaitForDrain)
        {
            var timeout = TimeSpan.FromSeconds(10);
            using var timeoutCts = new CancellationTokenSource(timeout);
            bool timedOut = false;
            while (Volatile.Read(ref _activeCallbackCount) > 0)
            {
                try
                {
                    await Task.Delay(10, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    timedOut = true;
                    break;
                }
            }

            if (timedOut)
            {
                Log.Warning("等待监控回调完成超时（{Timeout}秒），仍有 {Count} 个回调在执行，强制释放资源",
                    timeout.TotalSeconds, Volatile.Read(ref _activeCallbackCount));
            }
            else
            {
                Log.Debug("所有在途监控回调已完成");
            }
        }

        // 5. 释放 Timer（DisposeAsync 确保 Timer 自身回调完全结束）
        if (timerToDispose != null)
        {
            await timerToDispose.DisposeAsync().ConfigureAwait(false);
        }

        // 6. 最后释放 CTS —— 此时所有回调均已完成，不会再访问 CTS，
        //    从根源上避免 ObjectDisposedException
        ctsToDispose?.Dispose();

        Log.Information("系统监控已停止");
    }

    /// <summary>
    /// 停止持续监控（同步兼容版）
    /// </summary>
    /// <remarks>
    /// 内部调用 <see cref="StopMonitoringAsync"/> 并同步等待，仅用于兼容旧调用方。
    /// 推荐优先使用异步版本以获得更好的执行链安全保证。
    /// </remarks>
    public void StopMonitoring()
    {
        StopMonitoringAsync().GetAwaiter().GetResult();
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
        // 若已降级，跳过主链路直接走 WMI
        if (!_cpuFallbackMode)
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
                {
                    // 成功后重置失败计数
                    _cpuPrimaryFailureCount = 0;
                    return Math.Round(value, 2);
                }
            }
            catch (Exception ex)
            {
                _cpuPrimaryFailureCount++;
                Log.Debug("PerformanceCounter 获取 CPU 失败 ({Count}/{Threshold}): {Msg}",
                    _cpuPrimaryFailureCount, CpuFallbackThreshold, ex.Message);

                // 连续失败达到阈值，触发降级
                if (_cpuPrimaryFailureCount >= CpuFallbackThreshold)
                {
                    _cpuFallbackMode = true;
                    Log.Warning("CPU 主链路连续失败 {Threshold} 次，已降级至 WMI 备用链路", CpuFallbackThreshold);
                }
            }
        }
        else
        {
            Log.Debug("CPU 采集处于降级模式，跳过 PerformanceCounter");
        }

        // 备用链路：WMI（降级后直接使用此链路）
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
                    using (obj)
                    {
                        if (obj["LoadPercentage"] is ushort load && load <= 100)
                        {
                            totalLoad += load;
                            coreCount++;
                        }
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
    /// 获取每个 CPU 核心的使用率
    /// </summary>
    /// <returns>每核使用率数组，索引对应核心编号；获取失败返回空数组</returns>
    private double[] GetPerCoreCpuUsage()
    {
        if (_perCoreCpuCounters == null || _perCoreCpuCounters.Length == 0)
            return [];

        try
        {
            var result = new double[_perCoreCpuCounters.Length];
            for (int i = 0; i < _perCoreCpuCounters.Length; i++)
            {
                var value = _perCoreCpuCounters[i].NextValue();
                result[i] = Math.Round(Math.Max(0, Math.Min(100, value)), 2);
            }
            return result;
        }
        catch (Exception ex)
        {
            Log.Debug("获取每核 CPU 使用率失败: {Msg}", ex.Message);
            return [];
        }
    }

    private long GetCachedTotalMemory()
    {
        if (_cachedTotalMemory >= 0)
            return _cachedTotalMemory;

        lock (_hardwareCacheLock)
        {
            if (_cachedTotalMemory < 0)
                _cachedTotalMemory = _memoryMonitor.GetTotalPhysicalMemory();
        }

        return _cachedTotalMemory;
    }

    private MemorySystemInfo GetCachedMemoryInfo()
    {
        if (_cachedMemoryInfo != null)
            return _cachedMemoryInfo;

        lock (_hardwareCacheLock)
        {
            _cachedMemoryInfo ??= _memoryMonitor.GetMemorySystemInfo();
        }

        return _cachedMemoryInfo;
    }

    /// <summary>
    /// CPU 性能计数器实例缓存 —— 避免重复创建导致的性能开销
    /// </summary>
    private PerformanceCounter? _cpuCounter;

    /// <summary>
    /// 每个 CPU 核心的性能计数器数组
    /// 数组索引对应核心编号（0 开始）
    /// </summary>
    private PerformanceCounter[]? _perCoreCpuCounters;

    private long _cachedTotalMemory = -1;
    private MemorySystemInfo? _cachedMemoryInfo;
    private readonly object _hardwareCacheLock = new();

    /// <summary>P2 修复：volatile 修饰确保多线程可见性（Timer 回调线程池 vs ContinueWith 线程池）</summary>
    private volatile bool _isCollecting;

    // ═════════════════════════════════════════════════════════════════════
    // CPU 采集链路降级控制
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CPU 采集是否处于降级模式（主链路持续失败后切换到 WMI 备用链路）
    /// volatile 修饰确保多线程可见性：Timer 回调线程池写入，采集线程读取
    /// </summary>
    private volatile bool _cpuFallbackMode;

    /// <summary>
    /// CPU 主链路连续失败计数器，达到阈值后触发降级
    /// volatile 修饰确保多线程可见性
    /// </summary>
    private volatile int _cpuPrimaryFailureCount;

    /// <summary>
    /// 触发降级的连续失败阈值
    /// </summary>
    private const int CpuFallbackThreshold = 3;

    // ═════════════════════════════════════════════════════════════════════
    // Java 进程统计缓存
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Java 进程统计缓存，避免每次采集都枚举系统进程。
    /// v2：TickMs 使用 Environment.TickCount64（单调时钟）做 TTL 比较，
    /// 彻底隔离 NTP / 系统时间跳变；Timestamp 仅用于 UI 展示，不参与 TTL 计算。
    /// </summary>
    private (long TickMs, DateTime Timestamp, int ProcessCount, long WorkingSetBytes, long PrivateBytes, int ThreadCount)? _javaProcessCache;

    /// <summary>
    /// Java 进程统计缓存 TTL（毫秒）
    /// </summary>
    private const int JavaCacheTtlMs = 5000;

    /// <summary>
    /// 获取 Java 进程统计信息
    /// </summary>
    /// <remarks>
    /// 正确释放 Process 对象以避免资源泄漏；
    /// 处理进程退出的竞态条件——枚举过程中进程可能随时退出。
    /// 采用防御式编程，单个进程读取失败不影响整体统计结果。
    /// </remarks>
    private (int ProcessCount, long WorkingSetBytes, long PrivateBytes, int ThreadCount) GetJavaProcessStats()
    {
        // v2: 使用单调时钟 Environment.TickCount64 做 TTL 比较，完全隔离 NTP 或系统时间跳变
        var nowTick = Environment.TickCount64;
        if (_javaProcessCache.HasValue)
        {
            var elapsed = nowTick - _javaProcessCache.Value.TickMs;
            if (elapsed < JavaCacheTtlMs)
            {
                Log.Debug("[JAVA] 使用 Java 进程统计缓存（剩余 {RemainingMs}ms）", JavaCacheTtlMs - elapsed);
                return (_javaProcessCache.Value.ProcessCount,
                        _javaProcessCache.Value.WorkingSetBytes,
                        _javaProcessCache.Value.PrivateBytes,
                        _javaProcessCache.Value.ThreadCount);
            }
        }

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
                Log.Debug("[JAVA] Java 进程: PID={Pid} 工作集={Ws}MB 私有内存={Pm}MB 线程数={Threads}",
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

        // v2: 写入缓存（TickMs 用单调时钟做 TTL；Timestamp 仅用于 UI 展示）
        _javaProcessCache = (nowTick, DateTime.Now, validProcessCount, totalWorkingSet, totalPrivateBytes, totalThreadCount);
        Log.Debug("[JAVA] Java 进程统计已缓存: {Count} 个进程", validProcessCount);

        return (validProcessCount, totalWorkingSet, totalPrivateBytes, totalThreadCount);
    }

    /// <summary>
    /// 是否已释放
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 释放监控资源：停止监控、释放定时器、取消令牌源、释放性能计数器
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Log.Information("[CLEAN] 释放 SystemMonitor 资源");
        StopMonitoring();

        _cpuCounter?.Dispose();
        _cpuCounter = null;

        if (_perCoreCpuCounters != null)
        {
            foreach (var counter in _perCoreCpuCounters)
                counter.Dispose();
            _perCoreCpuCounters = null;
        }

        _disposed = true;
    }
}
