using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Services.SystemMonitoring;
using Serilog;

namespace McServerGuard.ViewModels;

/// <summary>
/// 📊 系统监控 ViewModel —— "你的服务器现在是在健步如飞还是在苟延残喘？"
///
/// 负责实时展示 CPU、内存、磁盘等系统指标。
/// 图表功能将在 Windows 环境下集成 ScottPlot 实现（Linux 上先别强求）。
///
/// 温馨提示：监控不会让你的服务器变卡，但盯着数字看太久可能会影响工作效率 ⚠️
/// </summary>
public partial class SystemMonitorViewModel : ObservableObject
{
    private readonly ISystemMonitor _systemMonitor;

    /// <summary>采样间隔 —— 每2秒采集一次（别太频繁，服务器会谢你的）</summary>
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);

    /// <summary>最多保留的历史数据点数量 —— 超出就扔掉最早的</summary>
    private const int MaxHistoryPoints = 120;

    private CancellationTokenSource? _monitoringCts;

    public SystemMonitorViewModel(ISystemMonitor systemMonitor)
    {
        Log.Information("📊 SystemMonitorViewModel 初始化");
        _systemMonitor = systemMonitor;

        // 🚀 常驻监控 —— 采集的是 CPU/内存/磁盘/Java 进程等系统级指标，
        //    与具体服务器实例无关。软件一启动就开始跑，让用户进监控页立刻看到数据。
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // 等待 UI/Dispatcher 就绪
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                try { StartMonitoring(); }
                catch (Exception ex) { Log.Error(ex, "💥 常驻监控自动启动失败"); }
            });
        });
    }

    // ─── 核心属性 ────────────────────────────────────────────────────

    /// <summary>当前操作的服务器实例</summary>
    [ObservableProperty]
    private ServerInstance? _server;

    /// <summary>当前最新的系统指标快照 —— 数字仪表盘的数据源</summary>
    [ObservableProperty]
    private SystemMetrics? _currentMetrics;

    /// <summary>是否正在监控中</summary>
    [ObservableProperty]
    private bool _isMonitoring;

    /// <summary>历史数据列表（最多保留120条）</summary>
    [ObservableProperty]
    private List<SystemMetrics> _metricsHistory = [];

    /// <summary>CPU 历史数据（格式化字符串列表，用于简易图表绑定）</summary>
    public string CpuHistoryText => string.Join(", ", MetricsHistory.Select(m => $"{m.CpuUsagePercent:F1}"));

    /// <summary>内存历史数据（格式化字符串列表，用于简易图表绑定）</summary>
    public string MemoryHistoryText => string.Join(", ", MetricsHistory.Select(m => $"{m.MemoryUsagePercent:F1}"));

    /// <summary>CPU 趋势图数据点 —— 给折线图控件吃的 List&lt;double&gt; 📈</summary>
    public List<double> CpuDataPoints => MetricsHistory.Select(m => m.CpuUsagePercent).ToList();

    /// <summary>内存趋势图数据点 —— 同上，只不过这次是内存 💾</summary>
    public List<double> MemoryDataPoints => MetricsHistory.Select(m => m.MemoryUsagePercent).ToList();

    /// <summary>
    /// 内存信息文本 —— "已用 XX GB / 共 XX GB"，一眼看出内存够不够用 💾
    /// 数据还没来的时候显示"等待数据..."，别急嘛
    /// </summary>
    public string MemoryInfoText => CurrentMetrics is not null
        ? $"{(CurrentMetrics.UsedMemoryBytes >> 30):F1} GB / {(CurrentMetrics.TotalMemoryBytes >> 30):F1} GB"
        : "等待数据...";

    /// <summary>
    /// 磁盘信息文本 —— "盘符: 已用 XX GB / 共 XX GB"，磁盘空间一目了然 💿
    /// 数据还没来的时候显示"等待数据..."
    /// </summary>
    public string DiskInfoText => CurrentMetrics is not null
        ? $"{CurrentMetrics.DiskName}: {(CurrentMetrics.DiskUsedBytes >> 30):F1} GB / {(CurrentMetrics.DiskTotalBytes >> 30):F1} GB"
        : "等待数据...";

    // ─── 命令 ──────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
    private void StartMonitoring()
    {
        Log.Information("▶️ 开始系统监控，间隔 {Interval} 秒", MonitorInterval.TotalSeconds);
        StopMonitoringInternal();
        _monitoringCts = new CancellationTokenSource();

        _systemMonitor.StartMonitoring(MonitorInterval, metrics =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                OnMetricsUpdate(metrics);
            });
        }, _monitoringCts.Token);

        IsMonitoring = true;
        MetricsHistory = [];
    }

    // 注意：监控的是系统级指标（CPU/内存/磁盘/Java 进程），不依赖具体服务器实例。
    // 因此不再要求 Server 非空——软件启动即可常驻监控。
    private bool CanStartMonitoring() => !IsMonitoring;

    [RelayCommand(CanExecute = nameof(CanStopMonitoring))]
    private void StopMonitoring()
    {
        Log.Information("⏹️ 停止系统监控");
        StopMonitoringInternal();
        IsMonitoring = false;
    }

    private bool CanStopMonitoring() => IsMonitoring;

    // ─── 属性变更响应 ────────────────────────────────────────────────

    partial void OnServerChanged(ServerInstance? value)
    {
        // 监控的是系统级指标（CPU/内存/磁盘/Java 进程），与具体服务器实例无关。
        // 切换/取消选中服务器不应启停监控、也不应清空历史曲线——数据连续性更重要。
        Log.Information("📡 关注的服务器切换为: {Name}（系统监控不受影响，继续常驻运行）",
            value is null ? "(无)" : value.ServerType.ToString());
    }

    // ─── 私有方法 ────────────────────────────────────────────────────

    private void OnMetricsUpdate(SystemMetrics metrics)
    {
        Log.Debug("📈 采集到系统指标: CPU={Cpu}% 内存={Mem}%", metrics.CpuUsagePercent, metrics.MemoryUsagePercent);
        CurrentMetrics = metrics;

        // 📜 记录历史，超出上限就砍掉最早的（FIFO 大法好）
        var history = new List<SystemMetrics>(MetricsHistory) { metrics };
        while (history.Count > MaxHistoryPoints)
            history.RemoveAt(0);
        MetricsHistory = history;

        // 通知图表文本 + 数据点更新
        OnPropertyChanged(nameof(CpuHistoryText));
        OnPropertyChanged(nameof(MemoryHistoryText));
        OnPropertyChanged(nameof(CpuDataPoints));
        OnPropertyChanged(nameof(MemoryDataPoints));
        // 通知内存/磁盘信息文本刷新 💾💿
        OnPropertyChanged(nameof(MemoryInfoText));
        OnPropertyChanged(nameof(DiskInfoText));
    }

    private void StopMonitoringInternal()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
        _systemMonitor.StopMonitoring();
    }
}
