// -----------------------------------------------------------------------------
// 文件名: MemoryOptimizerService.cs
// 命名空间: McServerGuard.Services
// 功能描述: 提供应用内存优化与监控服务，支持定时 GC、阈值触发深度回收、LOH 压缩
// 依赖组件: System.Runtime, System.Windows.Threading, Serilog
// 设计模式: 单例模式（DI容器注册）、观察者模式（系统内存事件监听）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services;

using System.Runtime;
using System.Windows;
using System.Windows.Threading;
using Serilog;

/// <summary>
/// 内存优化服务
/// 提供定时垃圾回收、内存占用监控、系统内存不足事件响应等功能
/// 支持大对象堆（LOH）压缩与工作集整理
/// </summary>
public class MemoryOptimizerService : IDisposable
{
    /// <summary>
    /// 定时优化定时器
    /// </summary>
    private readonly DispatcherTimer _optimizeTimer;

    /// <summary>
    /// 内存监控定时器
    /// </summary>
    private readonly DispatcherTimer _memoryMonitorTimer;

    /// <summary>
    /// GC 执行锁，防止并发回收
    /// </summary>
    private readonly object _gcLock = new();

    /// <summary>
    /// 是否正在执行优化
    /// </summary>
    private bool _isOptimizing;

    /// <summary>
    /// 上次回收后的内存占用（字节）
    /// </summary>
    private long _lastMemoryBytes;

    /// <summary>
    /// 上次完整回收时间
    /// </summary>
    private DateTime _lastCollectTime = DateTime.MinValue;

    /// <summary>
    /// 当前应用内存占用（MB）
    /// </summary>
    public double CurrentMemoryMB => GC.GetTotalMemory(false) / (1024.0 * 1024.0);

    /// <summary>
    /// 内存优化阈值（MB），超过此值触发深度回收
    /// 默认值：500MB
    /// </summary>
    public double MemoryThresholdMB { get; set; } = 500;

    /// <summary>
    /// 是否启用自动优化
    /// </summary>
    public bool AutoOptimizeEnabled { get; set; } = true;

    /// <summary>
    /// 初始化内存优化服务
    /// 配置定时器、注册系统 GC 通知、绑定应用退出事件
    /// </summary>
    public MemoryOptimizerService()
    {
        Log.Information("🧹 MemoryOptimizerService 初始化");

        // 定时优化（每 30 秒执行一次轻量回收）
        _optimizeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _optimizeTimer.Tick += OnOptimizeTimerTick;

        // 内存监控（每 5 秒检查一次）
        _memoryMonitorTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _memoryMonitorTimer.Tick += OnMemoryMonitorTimerTick;

        // 系统内存不足事件
        GC.RegisterForFullGCNotification(10, 10);
        _ = Task.Run(MonitorFullGCNotification);

        // 应用程序关闭时清理
        Application.Current.Exit += OnApplicationExit;
    }

    /// <summary>
    /// 启动内存优化服务
    /// 启动定时优化与内存监控定时器
    /// </summary>
    public void Start()
    {
        _optimizeTimer.Start();
        _memoryMonitorTimer.Start();
        Log.Information("🧹 内存优化服务已启动");
    }

    /// <summary>
    /// 停止内存优化服务
    /// 停止定时优化与内存监控定时器
    /// </summary>
    public void Stop()
    {
        _optimizeTimer.Stop();
        _memoryMonitorTimer.Stop();
        Log.Information("🧹 内存优化服务已停止");
    }

    /// <summary>
    /// 强制执行垃圾回收（异步执行，避免阻塞 UI 线程）
    /// </summary>
    /// <param name="deep">是否深度回收（压缩 LOH + 等待终结器完成）</param>
    public void ForceGC(bool deep = false)
    {
        lock (_gcLock)
        {
            if (_isOptimizing) return;
            _isOptimizing = true;
        }

        // 将 GC 操作封送到线程池执行，避免阻塞 UI 线程
        _ = Task.Run(() =>
        {
            try
            {
                var before = GC.GetTotalMemory(false);

                if (deep)
                {
                    Log.Information("🧹 执行深度垃圾回收 (LOH 压缩)...");

                    // 设置 LOH 压缩模式
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                    // 强制完整回收，包括所有代
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                }
                else
                {
                    Log.Debug("🧹 执行轻量垃圾回收...");
                    GC.Collect(2, GCCollectionMode.Optimized, false, false);
                }

                var after = GC.GetTotalMemory(true);
                var freed = before - after;

                if (freed > 0)
                {
                    Log.Information("🧹 垃圾回收完成，释放 {FreedMB:F2} MB ({BeforeMB:F2} → {AfterMB:F2} MB)",
                        freed / (1024.0 * 1024.0),
                        before / (1024.0 * 1024.0),
                        after / (1024.0 * 1024.0));
                }

                _lastMemoryBytes = after;
                _lastCollectTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "🧹 垃圾回收执行异常: {Message}", ex.Message);
            }
            finally
            {
                lock (_gcLock)
                {
                    _isOptimizing = false;
                }
            }
        });
    }

    /// <summary>
    /// 尝试减少进程工作集（Working Set）
    /// 通知操作系统可将部分物理内存换出到页面文件
    /// </summary>
    public void TrimWorkingSet()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var before = process.WorkingSet64;

            // 调用 Win32 API 减少工作集
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                NativeMethods.SetProcessWorkingSetSize(
                    process.Handle,
                    new UIntPtr(ulong.MaxValue),
                    new UIntPtr(ulong.MaxValue));
            }

            var after = process.WorkingSet64;
            Log.Debug("🧹 工作集整理: {BeforeMB:F2} → {AfterMB:F2} MB",
                before / (1024.0 * 1024.0),
                after / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            Log.Debug("🧹 工作集整理失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 定时优化定时器回调
    /// 执行轻量回收，定期执行深度回收与工作集整理
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">事件参数</param>
    private void OnOptimizeTimerTick(object? sender, EventArgs e)
    {
        if (!AutoOptimizeEnabled) return;

        // 轻量回收
        ForceGC(deep: false);

        // 每 5 次轻量回收后执行一次深度回收
        if ((DateTime.Now - _lastCollectTime).TotalMinutes >= 5)
        {
            ForceGC(deep: true);
            TrimWorkingSet();
        }
    }

    /// <summary>
    /// 内存监控定时器回调
    /// 检测内存占用是否超过阈值，超限时触发深度回收
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">事件参数</param>
    private void OnMemoryMonitorTimerTick(object? sender, EventArgs e)
    {
        if (!AutoOptimizeEnabled) return;

        try
        {
            var currentMB = CurrentMemoryMB;

            // 超过阈值时强制深度回收
            if (currentMB > MemoryThresholdMB && !_isOptimizing)
            {
                Log.Warning("⚠️ 内存占用超过阈值: {CurrentMB:F2} MB > {ThresholdMB:F2} MB，触发深度回收",
                    currentMB, MemoryThresholdMB);
                ForceGC(deep: true);
                TrimWorkingSet();
            }
        }
        catch
        {
            // 忽略监控错误
        }
    }

    /// <summary>
    /// 监控完整 GC 通知（后台线程）
    /// 监听系统即将触发完整 GC 的事件，提前执行工作集整理
    /// </summary>
    private async void MonitorFullGCNotification()
    {
        try
        {
            while (true)
            {
                var status = GC.WaitForFullGCApproach(5000);
                if (status == GCNotificationStatus.Succeeded)
                {
                    Log.Debug("🧹 系统即将触发完整 GC，准备提前优化...");
                    _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (AutoOptimizeEnabled)
                            TrimWorkingSet();
                    }, DispatcherPriority.Background);
                }

                if (status == GCNotificationStatus.Canceled) break;
                await Task.Delay(1000);
            }
        }
        catch
        {
            // 线程退出
        }
    }

    /// <summary>
    /// 应用程序退出事件处理
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">退出事件参数</param>
    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        Log.Information("🧹 应用退出，停止内存优化服务");
        Stop();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 本地 P/Invoke 方法封装
    /// </summary>
    private static class NativeMethods
    {
        /// <summary>
        /// 设置进程工作集大小
        /// </summary>
        /// <param name="proc">进程句柄</param>
        /// <param name="min">最小工作集大小</param>
        /// <param name="max">最大工作集大小</param>
        /// <returns>是否执行成功</returns>
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(
            IntPtr proc,
            UIntPtr min,
            UIntPtr max);
    }
}
