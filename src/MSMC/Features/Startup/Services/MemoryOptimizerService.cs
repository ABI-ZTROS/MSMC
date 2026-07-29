// -----------------------------------------------------------------------------
// 文件名: MemoryOptimizerService.cs
// 命名空间: io.NET.ZTR_OS.Features.Startup.Services
// 功能描述: 提供应用内存优化与监控服务，支持定时 GC、阈值触发深度回收、LOH 压缩
// 依赖组件: System.Runtime, System.Windows.Threading, Serilog
// 设计模式: 单例模式（DI容器注册）、观察者模式（系统内存事件监听）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.Startup.Services;

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
        Log.Information("[CLEAN] MemoryOptimizerService 初始化");

        // 定时优化（每 5 分钟执行一次轻量回收）
        _optimizeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(5)
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
        _ = Task.Run(() => MonitorFullGCNotificationAsync());

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
        Log.Information("[CLEAN] 内存优化服务已启动");
    }

    /// <summary>
    /// 停止内存优化服务
    /// 停止定时优化与内存监控定时器
    /// </summary>
    public void Stop()
    {
        _optimizeTimer.Stop();
        _memoryMonitorTimer.Stop();
        Log.Information("[CLEAN] 内存优化服务已停止");
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
                    Log.Information("[CLEAN] 执行深度垃圾回收 (LOH 压缩)...");

                    // 设置 LOH 压缩模式
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                    // 强制完整回收，包括所有代
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                }
                else
                {
                    Log.Debug("[CLEAN] 执行轻量垃圾回收...");
                    GC.Collect(2, GCCollectionMode.Optimized, false, false);
                }

                var after = GC.GetTotalMemory(true);
                var freed = before - after;

                if (freed > 0)
                {
                    Log.Information("[CLEAN] 垃圾回收完成，释放 {FreedMB:F2} MB ({BeforeMB:F2} → {AfterMB:F2} MB)",
                        freed / (1024.0 * 1024.0),
                        before / (1024.0 * 1024.0),
                        after / (1024.0 * 1024.0));
                }

                _lastMemoryBytes = after;
                _lastCollectTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[CLEAN] 垃圾回收执行异常: {Message}", ex.Message);
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
            Log.Debug("[CLEAN] 工作集整理: {BeforeMB:F2} → {AfterMB:F2} MB",
                before / (1024.0 * 1024.0),
                after / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            Log.Debug("[CLEAN] 工作集整理失败: {Message}", ex.Message);
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

        // 每 6 次轻量回收后执行一次深度回收（约 30 分钟）
        if ((DateTime.Now - _lastCollectTime).TotalMinutes >= 30)
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
                Log.Warning("[WARN] 内存占用超过阈值: {CurrentMB:F2} MB > {ThresholdMB:F2} MB，触发深度回收",
                    currentMB, MemoryThresholdMB);
                ForceGC(deep: true);
                TrimWorkingSet();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "内存监控采样异常");
        }
    }

    /// <summary>
    /// 监控完整 GC 通知（后台线程）
    /// 监听系统即将触发完整 GC 的事件，提前执行工作集整理
    /// </summary>
    /// <remarks>P1 修复：从 async void 改为 async Task，未捕获异常不再导致进程崩溃</remarks>
    private async Task MonitorFullGCNotificationAsync()
    {
        try
        {
            while (true)
            {
                var status = GC.WaitForFullGCApproach(5000);
                if (status == GCNotificationStatus.Succeeded)
                {
                    Log.Debug("[CLEAN] 系统即将触发完整 GC，准备提前优化...");
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
        catch (Exception ex)
        {
            Log.Debug(ex, "GC 监控线程退出");
        }
    }

    /// <summary>
    /// 应用程序退出事件处理
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">退出事件参数</param>
    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        Log.Information("[CLEAN] 应用退出，停止内存优化服务");
        Stop();
        // 取消 GC 通知注册，使 MonitorFullGCNotification 的 WaitForFullGCApproach 返回 Canceled 从而退出循环
        try { GC.CancelFullGCNotification(); } catch { /* 未注册时忽略 */ }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <remarks>
    /// 停止定时器、取消 GC 通知注册（使后台监控线程的 while 循环收到 Canceled 状态而退出）、
    /// 解除应用退出事件订阅，确保后台线程不再持有实例引用。
    /// </remarks>
    public void Dispose()
    {
        Stop();
        try { GC.CancelFullGCNotification(); } catch { /* 未注册时忽略 */ }
        Application.Current.Exit -= OnApplicationExit;
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
