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
/// 🏠 主窗口 ViewModel —— 整个应用的大脑/指挥中心
/// 
/// 负责各大子页面的调度和导航，像指挥官一样把检测到的 Server
/// 分发给各个子页面。毕竟 Server 只需检测一次，但谁都要用它。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // 🔧 注入的服务 —— 依赖注入的魔法，不用 new 就能用
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
    /// 主窗口 ViewModel 构造函数
    /// 通过构造函数注入所有服务 + 初始化子页面
    /// </summary>
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

        // 📦 初始化子页面 ViewModel
        DetectionPage = new ServerDetectionViewModel(serverDetector, appConfigService, serverManager, serverImporter);
        ConfigPage = new ConfigEditorViewModel(configManager, serverDetector, appConfigService);
        MonitorPage = new SystemMonitorViewModel(systemMonitor);
        SettingsPage = new SettingsViewModel(themeService, toastService);

        // 📡 订阅检测页的选中服务器变化，同步到配置页/监控页
        DetectionPage.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ServerDetectionViewModel.SelectedServer))
            {
                var server = DetectionPage.SelectedServer;
                ConfigPage.Server = server;
                MonitorPage.Server = server;
            }
        };

        // 🔔 初始化通知服务
        _toastService.Initialize();

        // ⏰ 启动状态栏时钟
        var clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        clockTimer.Start();

        // 🚀 启动时自动检测服务器 —— 让用户一上来就能看到结果
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

    // ─── Snackbar 通知队列 ──────────────────────────────────────
    // MaterialDesign 的 Snackbar，弹出来告诉用户"嘿，事情办完了" 📢

    /// <summary>
    /// Snackbar 消息队列 —— 弹出式通知用的
    /// 比状态栏更显眼，适合用来通知"检测完成"这类一次性消息
    /// </summary>
    public SnackbarMessageQueue SnackbarMessages { get; } = new(TimeSpan.FromSeconds(3));

    // ─── 子页面 ViewModel ─────────────────────────────────────────

    /// <summary>🔍 服务器检测页 —— "你的服务器在哪？让我找找"</summary>
    public ServerDetectionViewModel DetectionPage { get; }

    /// <summary>⚙️ 配置编辑页 —— "让我改改你的配置"</summary>
    public ConfigEditorViewModel ConfigPage { get; }

    /// <summary>📊 系统监控页 —— "你的服务器还能撑多久？"</summary>
    public SystemMonitorViewModel MonitorPage { get; }

    /// <summary>⚙️ 设置页 —— "自定义外观和行为"</summary>
    public SettingsViewModel SettingsPage { get; }

    // ─── 导航 ────────────────────────────────────────────────────────

    /// <summary>
    /// 当前选中的 Tab 索引
    /// 0=检测, 1=配置, 2=监控, 3=设置
    /// 改变它就能切换页面
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// 当前页面 ViewModel —— 根据 SelectedTabIndex 返回对应的子 ViewModel
    /// ContentControl 绑定此属性，配合 DataTemplate 自动切换页面
    /// </summary>
    public object CurrentPage => SelectedTabIndex switch
    {
        0 => DetectionPage,
        1 => ConfigPage,
        2 => MonitorPage,
        3 => SettingsPage,
        _ => DetectionPage
    };

    // ─── 状态栏 ──────────────────────────────────────────────────────

    /// <summary>
    /// 底部状态栏文本 —— 告诉用户"我在干嘛"
    /// 比如检测中、检测完成、出错了等等
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "准备就绪，点击「开始检测」寻找 Minecraft 服务器 🎯";

    /// <summary>
    /// 状态栏右下角实时时钟 —— 每秒刷新一次
    /// </summary>
    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 当前权限模式文本 —— 显示在状态栏
    /// </summary>
    public string PrivilegeStatusText => _privilegeService.IsRunningAsAdmin
        ? "🔒 管理员模式"
        : "⚠️ 受限模式";

    /// <summary>
    /// 是否为管理员模式
    /// </summary>
    public bool IsAdminMode => _privilegeService.IsRunningAsAdmin;

    [RelayCommand]
    private void RequestElevation()
    {
        Log.Information("🔐 用户请求提权...");
        _privilegeService.RequestElevation();
    }

    // ─── 检测命令 ────────────────────────────────────────────────────

    /// <summary>
    /// 是否正在检测中 —— 用来控制按钮的 IsEnabled，防止用户狂点
    /// </summary>
    [ObservableProperty]
    private bool _isDetecting;

    /// <summary>
    /// 异步检测服务器 —— 大招！检测到 Server 后分发给各子页面
    /// 
    /// 流程：
    /// 1. 调用 DetectionPage.DetectCommand（子页面自己会更新 UI）
    /// 2. 等检测结果回来
    /// 3. 如果检测到了服务器，把第一个（或用户选中的）塞给其他子页面
    /// 
    /// 为什么不直接在这里调 IServerDetector？因为 DetectionPage 自己也要显示进度啊
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDetectServers))]
    private async Task DetectServersAsync()
    {
        Log.Information("🔍 开始检测服务器...");
        IsDetecting = true;
        StatusMessage = "正在扫描系统中的 Minecraft 服务器... 🔍";

        try
        {
            // 先让检测页干活
            await DetectionPage.DetectCommand.ExecuteAsync(null);

            // 检查结果
            var result = DetectionPage.DetectionResult;
            if (result?.Servers.Count > 0)
            {
                StatusMessage = $"✅ 检测完成！找到 {result.Servers.Count} 个服务器实例";
                // 📢 Snackbar 弹一个通知，让用户更直观地知道结果
                SnackbarMessages.Enqueue($"🎉 找到 {result.Servers.Count} 个 Minecraft 服务器！");

                // 📤 把第一个服务器分发下去（后续用户可以手动切换）
                var firstServer = result.Servers[0];
                ConfigPage.Server = firstServer;
                MonitorPage.Server = firstServer;

                // 自动选中第一个服务器
                DetectionPage.SelectedServer = firstServer;

                Log.Information("✅ 服务器检测完成，发现 {Count} 个服务器", result.Servers.Count);
            }
            else
            {
                StatusMessage = result?.ErrorMessage ?? "未检测到正在运行的 Minecraft 服务器 😢";
                // 😢 没找到也要通知一下，别让用户干等着
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
    /// 能不能执行检测 —— 检测中就别再点了
    /// </summary>
    private bool CanDetectServers()
    {
        Log.Debug("🔄 CanDetectServers 检查: IsDetecting={IsDetecting}", IsDetecting);
        return !IsDetecting;
    }

    // ─── 辅助方法 ────────────────────────────────────────────────────

    /// <summary>
    /// 局部方法：当 SelectedTabIndex 变化时更新状态栏
    /// 虽然可以放 OnSelectedTabIndexChanged 里，但 partial method
    /// 被源生成器用了，咱就别手动写了，用 PropertyChanged 订阅
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
        Log.Debug("🔄 SelectedTabIndex 变更为 {TabIndex}", value);

        // 通知 CurrentPage 属性已变更，触发 ContentControl 切换页面
        OnPropertyChanged(nameof(CurrentPage));

        // 🗺️ 根据当前 Tab 切换状态栏提示语
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
