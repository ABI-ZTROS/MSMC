// -----------------------------------------------------------------------------
// 文件名: MainViewModel.cs
// 命名空间: McServerGuard.ViewModels
// 功能描述: 主窗口视图模型 —— 基于 CommunityToolkit.Mvvm 源生成器的 MVVM 绑定层，
//           承担子页面导航调度、服务器检测协调与状态分发的核心职责
// 依赖组件: CommunityToolkit.Mvvm (ObservableProperty/RelayCommand),
//           MaterialDesignThemes.Wpf (Snackbar), Microsoft.Extensions.DependencyInjection, Serilog
// 设计模式: MVVM 模式, 命令模式, 发布-订阅 (PropertyChanged 事件)
// -----------------------------------------------------------------------------

using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using McServerGuard.Models;
using McServerGuard.Services;
using McServerGuard.Services.ConfigManagement;
using McServerGuard.Services.ServerDetection;
using McServerGuard.Services.SystemMonitoring;
using Serilog;

namespace McServerGuard.ViewModels;

/// <summary>
/// 主窗口视图模型 —— 应用 UI 层的核心协调器
/// </summary>
/// <remarks>
/// 作为主窗口的数据上下文，本类负责子页面 ViewModel 的生命周期管理、
/// 导航状态机维护、跨页面服务器实例分发以及状态栏信息聚合。
/// 通过订阅 <see cref="ServerDetectionViewModel.PropertyChanged"/> 事件
/// 实现选中服务器在配置编辑页与系统监控页之间的同步。
/// </remarks>
public partial class MainViewModel : ObservableObject
{
    private readonly IServerDetector _serverDetector;
    private readonly IConfigManager _configManager;
    private readonly ISystemMonitor _systemMonitor;
    private readonly IServerImporterService _serverImporter;
    private readonly IServerManagerService _serverManager;
    private readonly IThemeService _themeService;
    private readonly IToastNotificationService _toastService;
    private readonly IAppConfigService _appConfigService;
    private readonly IPrivilegeService _privilegeService;

    /// <summary>
    /// 初始化主窗口视图模型的新实例
    /// </summary>
    /// <param name="serverDetector">服务器检测服务</param>
    /// <param name="configManager">配置管理服务</param>
    /// <param name="systemMonitor">系统监控服务</param>
    /// <param name="serverImporter">服务器导入服务</param>
    /// <param name="serverManager">服务器管理服务</param>
    /// <param name="themeService">主题服务</param>
    /// <param name="toastService">吐司通知服务</param>
    /// <param name="appConfigService">应用配置服务</param>
    /// <param name="privilegeService">权限提升服务</param>
    /// <remarks>
    /// 通过构造函数依赖注入获取所有外部依赖项，完成子页面 ViewModel 的实例化、
    /// 跨页面属性变更订阅、通知服务初始化以及状态栏时钟启动。
    /// </remarks>
    public MainViewModel(
        IServerDetector serverDetector,
        IConfigManager configManager,
        ISystemMonitor systemMonitor,
        IServerImporterService serverImporter,
        IServerManagerService serverManager,
        IThemeService themeService,
        IToastNotificationService toastService,
        IAppConfigService appConfigService,
        IPrivilegeService privilegeService)
    {
        Log.Information("🧠 MainViewModel 初始化，注入 {ServiceCount} 个服务", 9);

        _serverDetector = serverDetector;
        _configManager = configManager;
        _systemMonitor = systemMonitor;
        _serverImporter = serverImporter;
        _serverManager = serverManager;
        _themeService = themeService;
        _toastService = toastService;
        _appConfigService = appConfigService;
        _privilegeService = privilegeService;

        DetectionPage = new ServerDetectionViewModel(serverDetector, appConfigService, serverManager, serverImporter);
        ConfigPage = new ConfigEditorViewModel(configManager, serverDetector, appConfigService);
        MonitorPage = new SystemMonitorViewModel(systemMonitor);
        SettingsPage = new SettingsViewModel(themeService, toastService);

        DetectionPage.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ServerDetectionViewModel.SelectedServer))
            {
                var server = DetectionPage.SelectedServer;
                ConfigPage.Server = server;
                MonitorPage.Server = server;
            }
        };

        _toastService.Initialize();

        var clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        clockTimer.Start();

        Log.Information("🚀 启动时自动检测服务器...");
        _ = Task.Run(async () =>
        {
            // 等待 UI 完全加载后执行
            await Task.Delay(500);
            System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
            {
                try
                {
                    await DetectServersAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "💥 fuck: 自动检测失败: {Message}", ex.Message);
                }
            });
        });
    }

    /// <summary>
    /// Snackbar 消息队列 —— 基于 MaterialDesign 的弹出式通知通道
    /// </summary>
    /// <remarks>
    /// 用于承载检测完成、操作结果等临时性通知，显示时长为 3 秒。
    /// 与状态栏文本相比具有更高的视觉优先级。
    /// </remarks>
    public SnackbarMessageQueue SnackbarMessages { get; } = new(TimeSpan.FromSeconds(3));

    /// <summary>
    /// 服务器检测页视图模型
    /// </summary>
    public ServerDetectionViewModel DetectionPage { get; }

    /// <summary>
    /// 配置编辑页视图模型
    /// </summary>
    public ConfigEditorViewModel ConfigPage { get; }

    /// <summary>
    /// 系统监控页视图模型
    /// </summary>
    public SystemMonitorViewModel MonitorPage { get; }

    /// <summary>
    /// 设置页视图模型
    /// </summary>
    public SettingsViewModel SettingsPage { get; }

    /// <summary>
    /// 当前选中的 Tab 索引（0=检测, 1=配置, 2=监控, 3=设置）
    /// </summary>
    /// <remarks>
    /// 由源生成器生成 <c>SelectedTabIndex</c> 属性，变更时触发
    /// <see cref="OnSelectedTabIndexChanged(int)"/> 部分方法以更新导航状态。
    /// </remarks>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// 当前页面数据上下文 —— 基于 Tab 索引的导航状态机
    /// </summary>
    /// <remarks>
    /// <c>ContentControl</c> 绑定此属性，配合 <c>DataTemplate</c> 资源字典
    /// 实现子页面的动态切换。
    /// </remarks>
    public object CurrentPage => SelectedTabIndex switch
    {
        0 => DetectionPage,
        1 => ConfigPage,
        2 => MonitorPage,
        3 => SettingsPage,
        _ => DetectionPage
    };

    /// <summary>
    /// 状态栏状态文本 —— 反映当前应用级操作状态
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "准备就绪，点击「开始检测」寻找 Minecraft 服务器 🎯";

    /// <summary>
    /// 状态栏实时时钟 —— 每秒刷新一次的时间戳显示
    /// </summary>
    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 当前权限模式描述文本
    /// </summary>
    public string PrivilegeStatusText => _privilegeService.IsRunningAsAdmin
        ? "🔒 管理员模式"
        : "⚠️ 受限模式";

    /// <summary>
    /// 获取一个值，指示当前进程是否以管理员权限运行
    /// </summary>
    public bool IsAdminMode => _privilegeService.IsRunningAsAdmin;

    /// <summary>
    /// 进程扫描跳过警告文本（用于状态栏提示）
    /// </summary>
    /// <remarks>
    /// 当 WMI 查询因权限不足或跨用户访问失败时，ProcessScanner 会跳过对应进程。
    /// 此属性将跳过计数暴露给 UI，帮助用户理解"扫不到"的原因。
    /// </remarks>
    public string ScanSkipWarning => _serverDetector.LastSkippedProcessCount > 0
        ? $"⚠️ 已跳过 {_serverDetector.LastSkippedProcessCount} 个无法访问的进程（{_serverDetector.LastSkipReason ?? "未知原因"}）"
        : string.Empty;

    /// <summary>
    /// 请求管理员权限提升命令
    /// </summary>
    /// <remarks>
    /// 调用 <see cref="IPrivilegeService.RequestElevation"/> 触发 UAC 提权流程。
    /// 触发条件：用户点击状态栏权限提示区域。
    /// 副作用：可能启动新的高权限进程实例。
    /// </remarks>
    [RelayCommand]
    private void RequestElevation()
    {
        Log.Information("🔐 用户请求提权...");
        _privilegeService.RequestElevation();
    }

    /// <summary>
    /// 指示当前是否正在执行服务器检测
    /// </summary>
    /// <remarks>
    /// 用作检测命令的 CanExecute 判定依据，防止重复触发检测操作。
    /// </remarks>
    [ObservableProperty]
    private bool _isDetecting;

    /// <summary>
    /// 异步执行服务器检测并将结果分发至各子页面
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    /// <remarks>
    /// <para>执行流程：</para>
    /// <list type="number">
    /// <item>调用 <see cref="ServerDetectionViewModel.DetectCommand"/> 启动检测</item>
    /// <item>等待检测结果返回</item>
    /// <item>若检测到服务器，将首个实例同步至配置页与监控页</item>
    /// </list>
    /// <para>触发条件：用户点击「开始检测」按钮或应用启动后自动触发。</para>
    /// <para>副作用：更新 <see cref="IsDetecting"/>、<see cref="StatusMessage"/>
    /// 以及各子页面的 Server 属性。</para>
    /// <para>通过子页面命令而非直接调用 <c>IServerDetector</c>，以确保检测页 UI 状态同步更新。</para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanDetectServers))]
    private async Task DetectServersAsync()
    {
        Log.Information("🔍 开始检测服务器...");
        IsDetecting = true;
        StatusMessage = "正在扫描系统中的 Minecraft 服务器... 🔍";

        try
        {
            await DetectionPage.DetectCommand.ExecuteAsync(null);

            var result = DetectionPage.DetectionResult;
            if (result?.Servers.Count > 0)
            {
                StatusMessage = $"✅ 检测完成！找到 {result.Servers.Count} 个服务器实例";
                SnackbarMessages.Enqueue($"🎉 找到 {result.Servers.Count} 个 Minecraft 服务器！");

                var firstServer = result.Servers[0];
                ConfigPage.Server = firstServer;
                MonitorPage.Server = firstServer;

                DetectionPage.SelectedServer = firstServer;

                Log.Information("✅ 服务器检测完成，发现 {Count} 个服务器", result.Servers.Count);
            }
            else
            {
                StatusMessage = result?.ErrorMessage ?? "未检测到正在运行的 Minecraft 服务器 😢";
                SnackbarMessages.Enqueue("😔 未检测到正在运行的 Minecraft 服务器");
                Log.Information("✅ 服务器检测完成，发现 0 个服务器");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 检测过程出错：{ex.Message}";
            Log.Error(ex, "💥 fuck: 服务器检测失败: {Message}", ex.Message);
        }
        finally
        {
            IsDetecting = false;
        }
    }

    /// <summary>
    /// 确定检测命令是否可执行
    /// </summary>
    /// <returns>当未处于检测状态时返回 <c>true</c>，否则返回 <c>false</c></returns>
    /// <remarks>用作 <see cref="DetectServersCommand"/> 的 CanExecute 谓词。</remarks>
    private bool CanDetectServers()
    {
        Log.Debug("🔄 CanDetectServers 检查: IsDetecting={IsDetecting}", IsDetecting);
        return !IsDetecting;
    }

    /// <summary>
    /// Tab 索引变更回调 —— 由 CommunityToolkit.Mvvm 源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的 Tab 索引值</param>
    /// <remarks>
    /// 触发 <see cref="CurrentPage"/> 属性变更通知以驱动 ContentControl 页面切换，
    /// 并根据当前导航上下文更新状态栏提示文本。
    /// </remarks>
    partial void OnSelectedTabIndexChanged(int value)
    {
        Log.Debug("🔄 SelectedTabIndex 变更为 {TabIndex}", value);

        OnPropertyChanged(nameof(CurrentPage));

        StatusMessage = value switch
        {
            0 => "服务器管理 —— 检测、导入、启动你的 Minecraft 服务器 🎮",
            1 => ConfigPage.Server is not null
                ? $"配置编辑 —— 正在编辑 {ConfigPage.Server.DisplayName} 的配置 ⚙️"
                : "配置编辑 —— 选择左侧的配置文件即可开始编辑（无需服务器运行）📝",
            2 => "系统监控 —— 常驻采集 CPU / 内存 / 磁盘 / Java 进程指标 📊",
            3 => "设置 —— 自定义外观、主题和行为 ⚙️",
            _ => StatusMessage
        };
    }
}
