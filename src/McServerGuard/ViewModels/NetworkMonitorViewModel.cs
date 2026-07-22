using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Constants;
using McServerGuard.Models;
using McServerGuard.Services.Network;
using McServerGuard.Views.Controls;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace McServerGuard.ViewModels;

public class NetworkMonitorViewModel : INotifyPropertyChanged
{
    private readonly NetworkService _networkService;
    private readonly IPortBridgeService _portBridgeService;
    private readonly NetworkTrafficService _trafficService;
    private CancellationTokenSource? _refreshCts;
    private double _peakSpeedBytesPerSec = 1048576.0;

    public ObservableCollection<PortInfo> ListeningPorts { get; } = [];
    public ObservableCollection<PortBridgeRule> BridgeRules { get; } = [];
    public ObservableCollection<PieSlice> PortDistributionSlices { get; } = [];
    public ObservableCollection<double> HourlyUploadData { get; } = new(Enumerable.Repeat(0.0, 24));
    public ObservableCollection<double> HourlyDownloadData { get; } = new(Enumerable.Repeat(0.0, 24));

    private int _totalPorts;
    public int TotalPorts
    {
        get => _totalPorts;
        set => SetProperty(ref _totalPorts, value);
    }

    private int _usedPorts;
    public int UsedPorts
    {
        get => _usedPorts;
        set => SetProperty(ref _usedPorts, value);
    }

    private int _usedPercentage;
    public int UsedPercentage
    {
        get => _usedPercentage;
        set => SetProperty(ref _usedPercentage, value);
    }

    private int _systemPorts;
    public int SystemPorts
    {
        get => _systemPorts;
        set => SetProperty(ref _systemPorts, value);
    }

    private int _registeredPorts;
    public int RegisteredPorts
    {
        get => _registeredPorts;
        set => SetProperty(ref _registeredPorts, value);
    }

    private int _dynamicPorts;
    public int DynamicPorts
    {
        get => _dynamicPorts;
        set => SetProperty(ref _dynamicPorts, value);
    }

    private bool _isRefreshing;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    private string _statusMessage;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // ── 网速仪表盘属性（MB/s，供 GaugeRingControl 绑定）──

    private double _uploadSpeedMB;
    public double UploadSpeedMB
    {
        get => _uploadSpeedMB;
        set => SetProperty(ref _uploadSpeedMB, value);
    }

    private double _downloadSpeedMB;
    public double DownloadSpeedMB
    {
        get => _downloadSpeedMB;
        set => SetProperty(ref _downloadSpeedMB, value);
    }

    private double _speedMaximumMB = 1.5;
    public double SpeedMaximumMB
    {
        get => _speedMaximumMB;
        set => SetProperty(ref _speedMaximumMB, value);
    }

    // ── 格式化文本 ──

    private string _uploadSpeedText = "0 B/s";
    public string UploadSpeedText
    {
        get => _uploadSpeedText;
        set => SetProperty(ref _uploadSpeedText, value);
    }

    private string _downloadSpeedText = "0 B/s";
    public string DownloadSpeedText
    {
        get => _downloadSpeedText;
        set => SetProperty(ref _downloadSpeedText, value);
    }

    private string _todayUploadText = "0 B";
    public string TodayUploadText
    {
        get => _todayUploadText;
        set => SetProperty(ref _todayUploadText, value);
    }

    private string _todayDownloadText = "0 B";
    public string TodayDownloadText
    {
        get => _todayDownloadText;
        set => SetProperty(ref _todayDownloadText, value);
    }

    private string _dailyAnalysisText = "";
    public string DailyAnalysisText
    {
        get => _dailyAnalysisText;
        set => SetProperty(ref _dailyAnalysisText, value);
    }

    public int CurrentHour => DateTime.Now.Hour;

    // ── 桥接属性 ──

    private string _bridgeListenAddress = "127.0.0.1";
    public string BridgeListenAddress
    {
        get => _bridgeListenAddress;
        set => SetProperty(ref _bridgeListenAddress, value);
    }

    private int _bridgeListenPort;
    public int BridgeListenPort
    {
        get => _bridgeListenPort;
        set => SetProperty(ref _bridgeListenPort, value);
    }

    private string _bridgeConnectAddress = "127.0.0.1";
    public string BridgeConnectAddress
    {
        get => _bridgeConnectAddress;
        set => SetProperty(ref _bridgeConnectAddress, value);
    }

    private int _bridgeConnectPort;
    public int BridgeConnectPort
    {
        get => _bridgeConnectPort;
        set => SetProperty(ref _bridgeConnectPort, value);
    }

    private bool _bridgeAddFirewall;
    public bool BridgeAddFirewall
    {
        get => _bridgeAddFirewall;
        set => SetProperty(ref _bridgeAddFirewall, value);
    }

    private PortInfo? _selectedPort;
    public PortInfo? SelectedPort
    {
        get => _selectedPort;
        set => SetProperty(ref _selectedPort, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand KillProcessCommand { get; }
    public ICommand AddBridgeCommand { get; }
    public ICommand RemoveBridgeCommand { get; }
    public ICommand LoadCommonPortsCommand { get; }

    public System.Collections.IList CommonPortsList => CommonPorts.All;
    public System.Collections.IList IpAddressesList => IpAddresses.All;

    public NetworkMonitorViewModel(
        NetworkService networkService,
        IPortBridgeService portBridgeService,
        NetworkTrafficService trafficService)
    {
        _networkService = networkService;
        _portBridgeService = portBridgeService;
        _trafficService = trafficService;

        RefreshCommand = new RelayCommand(async () => await RefreshPorts());
        KillProcessCommand = new RelayCommand(async () => await KillSelectedProcess());
        AddBridgeCommand = new RelayCommand(async () => await AddBridge());
        RemoveBridgeCommand = new RelayCommand<PortBridgeRule>(async r => await RemoveBridge(r));
        LoadCommonPortsCommand = new RelayCommand(LoadCommonPorts);

        TotalPorts = _networkService.GetTotalPortCount();
        StatusMessage = "准备就绪";

        LoadHourlyData();
        Task.Run(StartAutoRefresh);
    }

    private async void StartAutoRefresh()
    {
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        while (!token.IsCancellationRequested)
        {
            await RefreshPorts();
            RefreshTraffic();
            await Task.Delay(1000, token);
        }
    }

    public async Task RefreshPorts()
    {
        IsRefreshing = true;

        try
        {
            var selectedPort = SelectedPort;
            var selectedKey = selectedPort != null
                ? (selectedPort.Port, selectedPort.Protocol, selectedPort.ProcessId)
                : ((int Port, string Protocol, int? ProcessId)?)null;

            var (ports, rules) = await Task.Run(() =>
                (_networkService.GetAllListeningPorts(), _portBridgeService.GetAllBridgeRules()));

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // 智能更新：仅当集合内容真正变化时才 Clear+Add，
                // 避免每秒全量重建导致 DataGrid 虚拟化容器频繁回收触发模板密封崩溃
                UpdateCollection(ListeningPorts, ports);
                UpdateCollection(BridgeRules, rules);

                if (selectedKey.HasValue)
                {
                    var (p, proto, pid) = selectedKey.Value;
                    var match = ListeningPorts.FirstOrDefault(x =>
                        x.Port == p && x.Protocol == proto && x.ProcessId == pid);
                    if (match != null)
                        SelectedPort = match;
                }
            });

            UsedPorts = ports.Count;
            UsedPercentage = _networkService.GetUsedPercentage();

            var dist = _networkService.GetPortDistribution();
            SystemPorts = dist.System;
            RegisteredPorts = dist.Registered;
            DynamicPorts = dist.Dynamic;

            UpdatePieSlices();

            // 注意：自动刷新每秒触发，不应覆盖用户操作反馈（如"桥接成功"）。
            // 仅在首次刷新或检测到端口数变化时更新状态栏。
            if (StatusMessage == "准备就绪" || StatusMessage.StartsWith("已检测"))
                StatusMessage = $"已检测 {UsedPorts} 个占用端口";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "刷新端口失败");
            StatusMessage = "刷新失败";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void UpdatePieSlices()
    {
        // PortDistributionSlices 绑定到 PieChartControl，CollectionChanged 必须在 UI 线程触发
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            PortDistributionSlices.Clear();
            if (SystemPorts > 0)
                PortDistributionSlices.Add(new PieSlice { Label = "系统端口", Value = SystemPorts, Color = Color.FromRgb(255, 85, 85) });
            if (RegisteredPorts > 0)
                PortDistributionSlices.Add(new PieSlice { Label = "注册端口", Value = RegisteredPorts, Color = Color.FromRgb(85, 136, 255) });
            if (DynamicPorts > 0)
                PortDistributionSlices.Add(new PieSlice { Label = "动态端口", Value = DynamicPorts, Color = Color.FromRgb(85, 221, 136) });
        });
    }

    private void RefreshTraffic()
    {
        _trafficService.Sample();

        var uploadBps = _trafficService.CurrentUploadSpeed;
        var downloadBps = _trafficService.CurrentDownloadSpeed;

        UploadSpeedMB = uploadBps / 1048576.0;
        DownloadSpeedMB = downloadBps / 1048576.0;

        UploadSpeedText = FormatSpeed(uploadBps);
        DownloadSpeedText = FormatSpeed(downloadBps);

        var peak = Math.Max(uploadBps, downloadBps);
        if (peak > _peakSpeedBytesPerSec)
        {
            _peakSpeedBytesPerSec = peak;
            SpeedMaximumMB = Math.Max(1.0, peak * 1.5 / 1048576.0);
        }

        UpdateHourlyData();
        UpdateAnalysis();
    }

    private void LoadHourlyData()
    {
        var today = _trafficService.GetTodayTraffic();
        for (int i = 0; i < 24; i++)
        {
            HourlyUploadData[i] = today.HourlyUpload[i];
            HourlyDownloadData[i] = today.HourlyDownload[i];
        }
    }

    private void UpdateHourlyData()
    {
        var today = _trafficService.GetTodayTraffic();
        var hour = DateTime.Now.Hour;
        var upVal = today.HourlyUpload[hour];
        var downVal = today.HourlyDownload[hour];

        // HourlyUploadData/HourlyDownloadData 绑定到 BarChartControl，索引器触发的 CollectionChanged 必须在 UI 线程
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            HourlyUploadData[hour] = upVal;
            HourlyDownloadData[hour] = downVal;
        });

        TodayUploadText = FormatBytes(today.TotalUpload);
        TodayDownloadText = FormatBytes(today.TotalDownload);
    }

    private void UpdateAnalysis()
    {
        var today = _trafficService.GetTodayTraffic();

        int peakHour = 0;
        long peakValue = 0;
        long totalAll = 0;
        for (int i = 0; i < 24; i++)
        {
            var combined = today.HourlyUpload[i] + today.HourlyDownload[i];
            totalAll += combined;
            if (combined > peakValue)
            {
                peakValue = combined;
                peakHour = i;
            }
        }

        var totalFormatted = FormatBytes(today.TotalUpload + today.TotalDownload);
        var upFormatted = FormatBytes(today.TotalUpload);
        var downFormatted = FormatBytes(today.TotalDownload);

        DailyAnalysisText = $"总计 {totalFormatted} | 上传 {upFormatted} | 下载 {downFormatted} | 峰值时段 {peakHour:00}:00";
    }

    private static string FormatSpeed(double bytesPerSec) =>
        bytesPerSec >= 1_048_576 ? $"{bytesPerSec / 1_048_576:F1} MB/s"
        : bytesPerSec >= 1024 ? $"{bytesPerSec / 1024:F1} KB/s"
        : $"{bytesPerSec:F0} B/s";

    private static string FormatBytes(long bytes) =>
        bytes >= 1_073_741_824 ? $"{bytes / 1_073_741_824:F2} GB"
        : bytes >= 1_048_576 ? $"{bytes / 1_048_576:F1} MB"
        : bytes >= 1024 ? $"{bytes / 1024:F1} KB"
        : $"{bytes} B";

    /// <summary>
    /// 智能更新集合：仅当内容（数量或元素）真正变化时才 Clear+Add，
    /// 避免 DataGrid 虚拟化容器频繁回收触发模板密封崩溃。
    /// </summary>
    private static void UpdateCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        if (target.Count == source.Count)
        {
            bool same = true;
            for (int i = 0; i < source.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(target[i], source[i]))
                {
                    same = false;
                    break;
                }
            }
            if (same)
                return; // 内容完全一致，跳过更新
        }

        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private async Task KillSelectedProcess()
    {
        if (SelectedPort?.ProcessId == null)
            return;

        var success = await Task.Run(() => _networkService.KillProcessByPort(SelectedPort.Port));
        StatusMessage = success
            ? $"已结束进程 {SelectedPort.ProcessName} (PID={SelectedPort.ProcessId})"
            : "结束进程失败";

        await RefreshPorts();
    }

    private async Task AddBridge()
    {
        var rule = new PortBridgeRule
        {
            ListenAddress = BridgeListenAddress,
            ListenPort = BridgeListenPort,
            ConnectAddress = BridgeConnectAddress,
            ConnectPort = BridgeConnectPort
        };

        var success = await Task.Run(() => _portBridgeService.AddBridgeRule(rule));

        if (success && BridgeAddFirewall)
            _portBridgeService.EnableFirewallRule(BridgeListenPort);

        StatusMessage = success
            ? $"桥接成功: {rule.ListenAddress}:{rule.ListenPort} -> {rule.ConnectAddress}:{rule.ConnectPort}"
            : "桥接失败，请确保以管理员身份运行";

        await RefreshPorts();
    }

    private async Task RemoveBridge(PortBridgeRule? rule)
    {
        if (rule == null)
            return;

        var success = await Task.Run(() => _portBridgeService.RemoveBridgeRule(rule.ListenAddress, rule.ListenPort));

        if (success)
            _portBridgeService.DisableFirewallRule(rule.ListenPort);

        StatusMessage = success
            ? $"已删除桥接规则: {rule.ListenAddress}:{rule.ListenPort}"
            : "删除失败";

        await RefreshPorts();
    }

    private void LoadCommonPorts()
    {
        StatusMessage = $"已加载 {CommonPorts.All.Count} 个常见端口";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public void Dispose()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _trafficService.Save();
    }
}
