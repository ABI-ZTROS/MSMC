using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Constants;
using McServerGuard.Models;
using McServerGuard.Services.Network;
using MaterialDesignThemes.Wpf;
using Serilog;

namespace McServerGuard.ViewModels;

public class NetworkMonitorViewModel : INotifyPropertyChanged
{
    private readonly NetworkService _networkService;
    private readonly IPortBridgeService _portBridgeService;
    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<PortInfo> ListeningPorts { get; } = [];
    public ObservableCollection<PortBridgeRule> BridgeRules { get; } = [];

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

    /// <summary>
    /// 常见端口列表（供 XAML 绑定，避免 x:Static 泛型问题）
    /// </summary>
    public System.Collections.IList CommonPortsList => CommonPorts.All;

    /// <summary>
    /// 可用 IP 地址列表（供 XAML 绑定，避免 x:Static 泛型问题）
    /// </summary>
    public System.Collections.IList IpAddressesList => IpAddresses.All;

    public NetworkMonitorViewModel(NetworkService networkService, IPortBridgeService portBridgeService)
    {
        _networkService = networkService;
        _portBridgeService = portBridgeService;

        RefreshCommand = new RelayCommand(async () => await RefreshPorts());
        KillProcessCommand = new RelayCommand(async () => await KillSelectedProcess());
        AddBridgeCommand = new RelayCommand(async () => await AddBridge());
        RemoveBridgeCommand = new RelayCommand<PortBridgeRule>(async r => await RemoveBridge(r));
        LoadCommonPortsCommand = new RelayCommand(LoadCommonPorts);

        TotalPorts = _networkService.GetTotalPortCount();
        StatusMessage = "准备就绪";

        Task.Run(StartAutoRefresh);
    }

    private async void StartAutoRefresh()
    {
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        while (!token.IsCancellationRequested)
        {
            await RefreshPorts();
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
                ListeningPorts.Clear();
                foreach (var port in ports)
                    ListeningPorts.Add(port);

                BridgeRules.Clear();
                foreach (var rule in rules)
                    BridgeRules.Add(rule);

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
    }
}