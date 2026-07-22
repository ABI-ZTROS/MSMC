// -----------------------------------------------------------------------------
// 文件名: SystemMonitorViewModel.cs
// 命名空间: McServerGuard.ViewModels
// 功能描述: 系统监控视图模型 —— 基于 CommunityToolkit.Mvvm 源生成器的 MVVM 绑定层，
//           承担系统级指标（CPU、内存、磁盘、Java 进程）的实时采集、历史缓存与可视化数据供给职责
// 依赖组件: CommunityToolkit.Mvvm (ObservableProperty/RelayCommand),
//           McServerGuard.Services.SystemMonitoring, Serilog
// 设计模式: MVVM 模式, 观察者模式 (指标推送回调), 生产者-消费者 (采样队列), 循环采样器
// -----------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using McServerGuard.Models;
using McServerGuard.Services.SystemMonitoring;
using Serilog;
using SkiaSharp;

namespace McServerGuard.ViewModels;

/// <summary>
/// 系统监控视图模型 —— 系统监控页面的数据上下文
/// </summary>
/// <remarks>
/// 本类作为系统监控页的 MVVM 绑定层，负责：按固定采样周期采集系统级指标（CPU、内存、磁盘、Java 进程）、
/// 维护环形历史缓冲区（FIFO，上限 120 点）、向 UI 层提供格式化文本与数据点序列以供图表控件绑定。
/// 监控为常驻模式，与具体服务器实例解耦，应用启动后即自动开始采集。
/// </remarks>
public partial class SystemMonitorViewModel : ObservableObject
{
    /// <summary>系统监控服务</summary>
    private readonly ISystemMonitor _systemMonitor;

    /// <summary>采样间隔（2 秒）</summary>
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);

    /// <summary>历史数据点最大保留数量（环形缓冲区容量）</summary>
    private const int MaxHistoryPoints = 120;

    /// <summary>监控取消令牌源</summary>
    private CancellationTokenSource? _monitoringCts;

    // CPU/内存趋势图底层集合（被 LiveCharts2 LineSeries 直接绑定，FIFO 截断）
    private readonly ObservableCollection<double> _cpuValues = [];
    private readonly ObservableCollection<double> _memoryValues = [];

    /// <summary>
    /// 初始化系统监控视图模型的新实例
    /// </summary>
    /// <param name="systemMonitor">系统监控服务</param>
    /// <remarks>构造完成后自动延迟启动常驻监控任务，确保进入页面时已有数据呈现。</remarks>
    public SystemMonitorViewModel(ISystemMonitor systemMonitor)
    {
        Log.Information("📊 SystemMonitorViewModel 初始化");
        _systemMonitor = systemMonitor;

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
                catch (Exception ex) { Log.Error(ex, "💥 常驻监控自动启动失败"); }
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
    /// 历史指标数据集合（环形缓冲区，上限由 <see cref="MaxHistoryPoints"/> 定义）
    /// </summary>
    [ObservableProperty]
    private List<SystemMetrics> _metricsHistory = [];

    /// <summary>CPU 使用率历史序列的格式化字符串（逗号分隔，用于简易文本图表绑定）</summary>
    public string CpuHistoryText => string.Join(", ", MetricsHistory.Select(m => $"{m.CpuUsagePercent:F1}"));

    /// <summary>内存使用率历史序列的格式化字符串（逗号分隔，用于简易文本图表绑定）</summary>
    public string MemoryHistoryText => string.Join(", ", MetricsHistory.Select(m => $"{m.MemoryUsagePercent:F1}"));

    /// <summary>CPU 使用率数据点序列（供折线图控件绑定）</summary>
    public List<double> CpuDataPoints => MetricsHistory.Select(m => m.CpuUsagePercent).ToList();

    /// <summary>内存使用率数据点序列（供折线图控件绑定）</summary>
    public List<double> MemoryDataPoints => MetricsHistory.Select(m => m.MemoryUsagePercent).ToList();

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
        Log.Information("📡 关注的服务器切换为: {Name}（系统监控不受影响，继续常驻运行）",
            value is null ? "(无)" : value.ServerType.ToString());
    }

    /// <summary>
    /// 指标更新回调 —— 由监控服务在每次采样完成后调用
    /// </summary>
    /// <param name="metrics">新采集的系统指标快照</param>
    /// <remarks>
    /// 在 UI 线程上执行。更新当前快照，将新数据点追加到历史缓冲区，
    /// 超出 <see cref="MaxHistoryPoints"/> 时移除最早数据（FIFO 策略），
    /// 并触发所有派生属性的变更通知。
    /// </remarks>
    private void OnMetricsUpdate(SystemMetrics metrics)
    {
        Log.Debug("📈 采集到系统指标: CPU={Cpu}% 内存={Mem}%", metrics.CpuUsagePercent, metrics.MemoryUsagePercent);
        CurrentMetrics = metrics;

        var history = new List<SystemMetrics>(MetricsHistory) { metrics };
        while (history.Count > MaxHistoryPoints)
            history.RemoveAt(0);
        MetricsHistory = history;

        // 维护 LiveCharts2 ObservableCollection（FIFO，触发图表自动刷新）
        _cpuValues.Add(metrics.CpuUsagePercent);
        while (_cpuValues.Count > MaxHistoryPoints)
            _cpuValues.RemoveAt(0);
        _memoryValues.Add(metrics.MemoryUsagePercent);
        while (_memoryValues.Count > MaxHistoryPoints)
            _memoryValues.RemoveAt(0);

        OnPropertyChanged(nameof(CpuHistoryText));
        OnPropertyChanged(nameof(MemoryHistoryText));
        OnPropertyChanged(nameof(CpuDataPoints));
        OnPropertyChanged(nameof(MemoryDataPoints));
        OnPropertyChanged(nameof(MemoryInfoText));
        OnPropertyChanged(nameof(DiskInfoText));
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
}
