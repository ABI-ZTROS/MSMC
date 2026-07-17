// 🧹 内存优化服务 —— "定期打扫卫生，让程序轻装上阵"
namespace McServerGuard.Services;

using System.Runtime;
using System.Windows;
using System.Windows.Threading;
using Serilog;

/// <summary>
/// 内存优化服务
/// - 定期触发 GC 回收
/// - 监控应用内存占用，超过阈值时强制执行深度回收
/// - 响应系统内存不足事件
/// - 支持 Trim 大对象堆（LOH）
/// </summary>
public class MemoryOptimizerService : IDisposable
{
    private readonly DispatcherTimer _optimizeTimer;
    private readonly DispatcherTimer _memoryMonitorTimer;
    private readonly object _gcLock = new();
    private bool _isOptimizing;
    private long _lastMemoryBytes;
    private DateTime _lastCollectTime = DateTime.MinValue;

    /// <summary>
    /// 当前应用内存占用（MB）
    /// </summary>
    public double CurrentMemoryMB => GC.GetTotalMemory(false) / (1024.0 * 1024.0);

    /// <summary>
    /// 内存优化阈值（MB），超过此值触发深度回收
    /// 默认 500MB
    /// </summary>
    public double MemoryThresholdMB { get; set; } = 500;

    /// <summary>
    /// 是否启用自动优化
    /// </summary>
    public bool AutoOptimizeEnabled { get; set; } = true;

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

    public void Start()
    {
        _optimizeTimer.Start();
        _memoryMonitorTimer.Start();
        Log.Information("🧹 内存优化服务已启动");
    }

    public void Stop()
    {
        _optimizeTimer.Stop();
        _memoryMonitorTimer.Stop();
        Log.Information("🧹 内存优化服务已停止");
    }

    /// <summary>
    /// 强制执行垃圾回收
    /// </summary>
    /// <param name="deep">是否深度回收（压缩 LOH + 等待完成）</param>
    public void ForceGC(bool deep = false)
    {
        lock (_gcLock)
        {
            if (_isOptimizing) return;
            _isOptimizing = true;
        }

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
    }

    /// <summary>
    /// 尝试减少工作集（Working Set）
    /// 通知操作系统可以将部分内存换出到页面文件
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

    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        Log.Information("🧹 应用退出，停止内存优化服务");
        Stop();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 本地 P/Invoke 方法
    /// </summary>
    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(
            IntPtr proc,
            UIntPtr min,
            UIntPtr max);
    }
}
