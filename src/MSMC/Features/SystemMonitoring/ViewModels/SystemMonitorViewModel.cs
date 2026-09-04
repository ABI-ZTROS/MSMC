// -----------------------------------------------------------------------------
// 文件名: SystemMonitorViewModel.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.ViewModels
// 功能描述: 系统监控视图模型 —— 基于 CommunityToolkit.Mvvm 源生成器的 MVVM 绑定层，
//           承担系统级指标（CPU、内存、磁盘、Java 进程）的实时采集、历史缓存与可视化数据供给职责
// 依赖组件: CommunityToolkit.Mvvm (ObservableProperty/RelayCommand),
//           io.NET.ZTR_OS.Services.SystemMonitoring, Serilog
// 设计模式: MVVM 模式, 观察者模式 (指标推送回调), 生产者-消费者 (采样队列), 循环采样器
// -----------------------------------------------------------------------------

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using io.NET.ZTR_OS.Features.ServerDetection.Models;
using io.NET.ZTR_OS.Features.SystemMonitoring.Models;
using io.NET.ZTR_OS.Features.SystemMonitoring.Services;
using Serilog;
using SkiaSharp;

namespace io.NET.ZTR_OS.Features.SystemMonitoring.ViewModels;

/// <summary>
/// 系统监控视图模型 —— 系统监控页面的数据上下文
/// </summary>
/// <remarks>
/// 本类作为系统监控页的 MVVM 绑定层，负责：按固定采样周期采集系统级指标（CPU、内存、磁盘、Java 进程）、
/// 维护环形历史缓冲区（FIFO，上限 120 点）、向 UI 层提供格式化文本与数据点序列以供图表控件绑定。
/// 监控为常驻模式，与具体服务器实例解耦，应用启动后即自动开始采集。
/// </remarks>
public partial class SystemMonitorViewModel : ObservableObject, IDisposable
{
    /// <summary>系统监控服务</summary>
    private readonly ISystemMonitor _systemMonitor;

    /// <summary>指标持久化服务</summary>
    private readonly IMetricsPersistenceService _persistence;

    /// <summary>采样间隔（2 秒）</summary>
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);

    /// <summary>历史数据点最大保留数量（环形缓冲区容量）</summary>
    private const int MaxHistoryPoints = 120;

    /// <summary>持久化批量大小：攒 5 条（≈10s）一次落盘，降低 HDD 写入频率（P10 弱机优化）。
    /// 采样精度不受影响——环形缓冲/图表仍按 2s 粒度刷新，仅磁盘写入从每 2s 降到每 10s。</summary>
    private const int PersistBatchSize = 5;

    /// <summary>待落盘的批量缓冲（P10：每 2s 唤醒磁盘 → 每 10s 一次）</summary>
    private readonly List<SystemMetrics> _pendingPersist = [];

    /// <summary>批量落盘计数</summary>
    private int _persistPendingCount;

    /// <summary>监控取消令牌源</summary>
    private CancellationTokenSource? _monitoringCts;

    /// <summary>指示当前实例是否已释放，防止重复 Dispose 导致资源二次释放</summary>
    private bool _disposed;

    // CPU/内存趋势图底层集合（被 LiveCharts2 LineSeries 直接绑定，FIFO 截断）
    private readonly ObservableCollection<double> _cpuValues = [];
    private readonly ObservableCollection<double> _memoryValues = [];

    /// <summary>
    /// 历史指标环形缓冲区 —— 固定容量数组 + head/tail 指针，替代原 List 的 O(n) 复制
    /// </summary>
    /// <remarks>
    /// 原 OnMetricsUpdate 每次采样都 new List(MetricsHistory) { metrics } + RemoveAt(0)，
    /// 120 点时每帧复制 120 个 SystemMetrics 引用 + 移动数组，O(n) 开销。
    /// 环形缓冲追加为 O(1)（写入槽位后移动 tail 指针），仅在 UI 读取时按需快照。
    /// </remarks>
    private readonly SystemMetrics[] _ringBuffer = new SystemMetrics[MaxHistoryPoints];

    /// <summary>环形缓冲区当前元素数（未满时 &lt; <see cref="MaxHistoryPoints"/>）</summary>
    private int _ringCount;

    /// <summary>环形缓冲区下一个写入位置（tail 指针）</summary>
    private int _ringTail;

    /// <summary>
    /// 初始化系统监控视图模型的新实例
    /// </summary>
    /// <param name="systemMonitor">系统监控服务</param>
    /// <param name="persistence">指标持久化服务</param>
    /// <remarks>构造完成后自动延迟启动常驻监控任务，确保进入页面时已有数据呈现。</remarks>
    public SystemMonitorViewModel(ISystemMonitor systemMonitor, IMetricsPersistenceService persistence)
    {
        Log.Information("[METRIC] SystemMonitorViewModel 初始化");
        _systemMonitor = systemMonitor;
        _persistence = persistence;

        // 初始化 LiveCharts2 折线图：CPU 绿色、内存蓝色，均带半透明面积填充与最新点光晕
        CpuSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "CPU",
                Values = _cpuValues,
                Fill = new SolidColorPaint(new SKColor(0x4C, 0xAF, 0x50, 0x40)),
                Stroke = new SolidColorPaint(new SKColor(0x4C, 0xAF, 0x50)) { StrokeThickness = 2 },
                GeometrySize = 6,
                GeometryFill = new SolidColorPaint(new SKColor(0x4C, 0xAF, 0x50)),
                GeometryStroke = null
            }
        };
        MemorySeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "内存",
                Values = _memoryValues,
                Fill = new SolidColorPaint(new SKColor(0x21, 0x96, 0xF3, 0x40)),
                Stroke = new SolidColorPaint(new SKColor(0x21, 0x96, 0xF3)) { StrokeThickness = 2 },
                GeometrySize = 6,
                GeometryFill = new SolidColorPaint(new SKColor(0x21, 0x96, 0xF3)),
                GeometryStroke = null
            }
        };

        // 深色主题共享色：文字 slate-200，分离线 10% 不透明白
        var axisTextPaint = new SolidColorPaint(new SKColor(0xE2, 0xE8, 0xF0));
        var axisSeparatorPaint = new SolidColorPaint(new SKColor(255, 255, 255, 26)) { StrokeThickness = 1 };

        TrendYAxis = new ICartesianAxis[]
        {
            new Axis
            {
                TextSize = 10,
                LabelsPaint = axisTextPaint,
                SeparatorsPaint = axisSeparatorPaint,
                TicksPaint = axisSeparatorPaint,
                MinLimit = 0,
                MaxLimit = 100,
                Labeler = value => $"{value:F0}%"
            }
        };

        TrendXAxis = new ICartesianAxis[]
        {
            new Axis
            {
                IsVisible = false,
                SeparatorsPaint = null
            }
        };

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                try { StartMonitoring(); }
                catch (Exception ex) { Log.Error(ex, "[FATAL] 常驻监控自动启动失败"); }
            });
        });
    }

    /// <summary>
    /// 当前关联的服务器实例
    /// </summary>
    /// <remarks>仅用于展示标注，不影响监控采集——监控对象为系统级全局指标。</remarks>
    [ObservableProperty]
    private ServerInstance? _server;

    /// <summary>
    /// 当前最新的系统指标快照
    /// </summary>
    [ObservableProperty]
    private SystemMetrics? _currentMetrics;

    /// <summary>
    /// 获取或设置一个值，指示监控是否正在运行
    /// </summary>
    [ObservableProperty]
    private bool _isMonitoring;

    /// <summary>
    /// 历史指标数据集合（环形缓冲区的快照视图，上限由 <see cref="MaxHistoryPoints"/> 定义）
    /// </summary>
    /// <remarks>
    /// 每次采样后从环形缓冲区按 FIFO 顺序生成新 List 触发变更通知。
    /// 内部存储使用固定容量数组 + head/tail 指针，追加为 O(1)。
    /// </remarks>
    [ObservableProperty]
    private List<SystemMetrics> _metricsHistory = [];

    /// <summary>CPU 趋势图 LiveCharts2 系列（绿色折线 + 半透明面积填充，绑定 _cpuValues FIFO 集合）。</summary>
    public ISeries[] CpuSeries { get; }

    /// <summary>内存趋势图 LiveCharts2 系列（蓝色折线 + 半透明面积填充，绑定 _memoryValues FIFO 集合）。</summary>
    public ISeries[] MemorySeries { get; }

    /// <summary>CPU/内存趋势图 Y 轴（百分比，浅色文字适配深色主题）。</summary>
    public ICartesianAxis[] TrendYAxis { get; }

    /// <summary>CPU/内存趋势图 X 轴（隐藏标签和分离线，仅作时间轴占位）。</summary>
    public ICartesianAxis[] TrendXAxis { get; }

    /// <summary>内存信息摘要文本（已用 GB / 总 GB）</summary>
    public string MemoryInfoText => CurrentMetrics is not null
        ? $"{(CurrentMetrics.UsedMemoryBytes >> 30):F1} GB / {(CurrentMetrics.TotalMemoryBytes >> 30):F1} GB"
        : "等待数据...";

    /// <summary>磁盘信息摘要文本（盘符: 已用 GB / 总 GB）</summary>
    public string DiskInfoText => CurrentMetrics is not null
        ? $"{CurrentMetrics.DiskName}: {(CurrentMetrics.DiskUsedBytes >> 30):F1} GB / {(CurrentMetrics.DiskTotalBytes >> 30):F1} GB"
        : "等待数据...";

    /// <summary>
    /// 启动监控命令
    /// </summary>
    /// <remarks>
    /// 触发条件：<see cref="CanStartMonitoring"/> 返回 true 且用户点击启动按钮。
    /// 副作用：停止上一轮监控（若存在），创建新的取消令牌源，调用 <see cref="ISystemMonitor.StartMonitoring"/>
    /// 启动周期性采样，重置历史数据并设置 <see cref="IsMonitoring"/> 为 true。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
    private void StartMonitoring()
    {
        Log.Information("▶️ 开始系统监控，间隔 {Interval} 秒", MonitorInterval.TotalSeconds);
        StopMonitoringInternal();
        _monitoringCts = new CancellationTokenSource();

        // 重置环形缓冲区指针（数组槽位中的旧引用由 GC 回收，无需显式清零）
        _ringCount = 0;
        _ringTail = 0;

        _systemMonitor.StartMonitoring(MonitorInterval, metrics =>
        {
            _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnMetricsUpdate(metrics);
            });
        }, _monitoringCts.Token);

        IsMonitoring = true;
        MetricsHistory = [];
        _cpuValues.Clear();
        _memoryValues.Clear();

        // 启动时清理过期数据文件
        _persistence.CleanupOldFiles();
    }

    /// <summary>
    /// 判断是否可启动监控
    /// </summary>
    /// <returns>若监控未运行则返回 true</returns>
    /// <remarks>监控对象为系统级指标，不依赖具体服务器实例。</remarks>
    private bool CanStartMonitoring() => !IsMonitoring;

    /// <summary>
    /// 停止监控命令
    /// </summary>
    /// <remarks>
    /// 触发条件：<see cref="CanStopMonitoring"/> 返回 true 且用户点击停止按钮。
    /// 副作用：取消监控令牌，释放资源，设置 <see cref="IsMonitoring"/> 为 false。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanStopMonitoring))]
    private void StopMonitoring()
    {
        Log.Information("⏹️ 停止系统监控");
        StopMonitoringInternal();
        IsMonitoring = false;
    }

    /// <summary>
    /// 判断是否可停止监控
    /// </summary>
    /// <returns>若监控正在运行则返回 true</returns>
    private bool CanStopMonitoring() => IsMonitoring;

    /// <summary>
    /// Server 属性变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的服务器实例</param>
    /// <remarks>
    /// 监控对象为系统级指标，与具体服务器实例无关。切换服务器不会启停监控
    /// 或清空历史曲线，以保证数据连续性。
    /// </remarks>
    partial void OnServerChanged(ServerInstance? value)
    {
        Log.Information("[BRDG] 关注的服务器切换为: {Name}（系统监控不受影响，继续常驻运行）",
            value is null ? "(无)" : value.ServerType.ToString());
    }

    /// <summary>
    /// 指标更新回调 —— 由监控服务在每次采样完成后调用
    /// </summary>
    /// <param name="metrics">新采集的系统指标快照</param>
    /// <remarks>
    /// 在 UI 线程上执行。更新当前快照，将新数据点写入环形缓冲区槽位（O(1)），
    /// 随后从环形缓冲区按 FIFO 顺序生成快照 List 触发 MetricsHistory 变更通知，
    /// 同步维护 LiveCharts2 ObservableCollection 的 FIFO 截断。
    /// </remarks>
    private void OnMetricsUpdate(SystemMetrics metrics)
    {
        Log.Debug("[METRIC] 采集到系统指标: CPU={Cpu}% 内存={Mem}%", metrics.CpuUsagePercent, metrics.MemoryUsagePercent);
        CurrentMetrics = metrics;

        // 持久化到磁盘（P10 弱机优化：批量缓冲攒 PersistBatchSize 条一次落盘，
        // 避免每 2s 唤醒 HDD；异步执行不阻塞 UI 线程）
        _pendingPersist.Add(metrics);
        _persistPendingCount++;
        if (_persistPendingCount >= PersistBatchSize)
        {
            _persistPendingCount = 0;
            var batch = _pendingPersist.ToList();
            _pendingPersist.Clear();
            _ = Task.Run(() =>
            {
                try
                {
                    foreach (var m in batch)
                        _persistence.Append(m.Timestamp, m.CpuUsagePercent, m.MemoryUsagePercent);
                }
                catch (Exception ex) { Log.Error(ex, "持久化监控数据失败"); }
            });
        }

        // 写入环形缓冲区槽位并推进 tail 指针（O(1)，无数组复制）
        _ringBuffer[_ringTail] = metrics;
        _ringTail = (_ringTail + 1) % MaxHistoryPoints;
        if (_ringCount < MaxHistoryPoints) _ringCount++;

        // 从环形缓冲区按 FIFO 顺序生成快照 List（仅在 UI 读取时按需复制一次）
        MetricsHistory = SnapshotRingBuffer();

        // 维护 LiveCharts2 ObservableCollection（FIFO，触发图表自动刷新）
        _cpuValues.Add(metrics.CpuUsagePercent);
        while (_cpuValues.Count > MaxHistoryPoints)
            _cpuValues.RemoveAt(0);
        _memoryValues.Add(metrics.MemoryUsagePercent);
        while (_memoryValues.Count > MaxHistoryPoints)
            _memoryValues.RemoveAt(0);

        OnPropertyChanged(nameof(MemoryInfoText));
        OnPropertyChanged(nameof(DiskInfoText));
    }

    /// <summary>
    /// 从环形缓冲区按 FIFO 顺序生成快照列表
    /// </summary>
    /// <returns>包含当前所有有效数据点的列表（按时间升序）</returns>
    /// <remarks>
    /// 环形缓冲区满后，head 指针 = tail（最旧元素位置）；未满时 head = 0。
    /// 仅在 OnMetricsUpdate 触发 UI 刷新时调用一次，避免原实现每次刷新都全量复制。
    /// </remarks>
    private List<SystemMetrics> SnapshotRingBuffer()
    {
        if (_ringCount == 0) return [];

        var snapshot = new List<SystemMetrics>(_ringCount);
        int head = _ringCount < MaxHistoryPoints ? 0 : _ringTail;
        for (int i = 0; i < _ringCount; i++)
        {
            snapshot.Add(_ringBuffer[(head + i) % MaxHistoryPoints]);
        }
        return snapshot;
    }

    /// <summary>
    /// 停止监控的内部实现（释放令牌与服务资源）
    /// </summary>
    private void StopMonitoringInternal()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
        _systemMonitor.StopMonitoring();
    }

    /// <summary>
    /// 释放系统监控视图模型占用的所有资源
    /// </summary>
    /// <remarks>
    /// 停止常驻监控循环、释放取消令牌源、停止系统监控服务。
    /// 幂等设计：重复调用安全。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Log.Information("[CLEAN] SystemMonitorViewModel 释放资源中...");
        StopMonitoringInternal();
        GC.SuppressFinalize(this);
        Log.Information("[OK] SystemMonitorViewModel 资源释放完成");
    }
}
