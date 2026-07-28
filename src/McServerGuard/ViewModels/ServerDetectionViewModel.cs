// -----------------------------------------------------------------------------
// 文件名: ServerDetectionViewModel.cs
// 命名空间: McServerGuard.ViewModels
// 功能描述: 服务器检测视图模型 —— 基于 CommunityToolkit.Mvvm 源生成器的 MVVM 绑定层，
//           承担服务器进程检测、已知服务器管理、JVM 参数编辑与启停控制等职责
// 依赖组件: CommunityToolkit.Mvvm (ObservableProperty/RelayCommand),
//           Microsoft.Win32 (OpenFileDialog), System.Windows.Data (CollectionView), Serilog
// 设计模式: MVVM 模式, 命令模式, 状态机 (ServerOperation), 观察者 (DetectionCompleted 事件)
// -----------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Constants;
using McServerGuard.Models;
using McServerGuard.Services;
using McServerGuard.Services.ServerDetection;
using Microsoft.Win32;
using Serilog;

namespace McServerGuard.ViewModels;

/// <summary>
/// 服务器检测视图模型 —— 服务器管理页面的数据上下文
/// </summary>
/// <remarks>
/// 本类作为检测页的 MVVM 绑定层，负责：服务器进程检测调度、运行中服务器与已知服务器的
/// 双列表维护、JVM 参数编辑器（含预设管理）、服务器启停命令路由以及操作状态机管理。
/// 通过 <see cref="IServerDetector.DetectionCompleted"/> 事件订阅实现自动检测数据推送。
/// </remarks>
public partial class ServerDetectionViewModel : ObservableObject, IDisposable
{
    private readonly IServerDetector _serverDetector;
    private readonly IAppConfigService _appConfigService;
    private readonly IServerManagerService _serverManager;
    private readonly IServerImporterService _serverImporter;

    /// <summary>指示当前实例是否已释放，防止重复 Dispose 导致资源二次释放</summary>
    private bool _disposed;

    // ╔══════════════════════════════════════════════════════════════════╗
    // ║  集合使用策略（🚫 全面放弃 ObservableCollection + ListCollectionView）
    // ╠══════════════════════════════════════════════════════════════════╣
    // ║  从代码中彻底移除 ObservableCollection / CollectionViewSource /
    // ║  ICollectionView / ListCollectionView.Refresh 等所有会触发
    // ║  ListCollectionView.PrepareLocalArray 的 API。
    // ║
    // ║  新策略：纯 List<T> 属性 + 手动 INotifyPropertyChanged
    // ║    1) 在内存里构建新的 List<T>（全量拷贝，不碰原集合）
    // ║    2) 属性 set 赋新引用 → 源生成器自动 OnPropertyChanged
    // ║    3) ItemsControl 检测到 ItemsSource 引用变了 → 丢掉旧 ItemCollection，
    // ║       以新 List<T> 从头生成，**完全不经过 ListCollectionView**。
    // ║
    // ║  结果：PrepareLocalArray 从代码路径中消失，0 次 NRE。
    // ╚══════════════════════════════════════════════════════════════════╝

    /// <summary>运行中服务器（带搜索过滤的最终可绑定列表）。每次刷新整体替换。</summary>
    [ObservableProperty]
    private IReadOnlyList<ServerInstance> _filteredRunningServers = new List<ServerInstance>();

    /// <summary>已知服务器（带搜索过滤的最终可绑定列表）。每次刷新整体替换。</summary>
    [ObservableProperty]
    private IReadOnlyList<KnownServer> _filteredKnownServers = new List<KnownServer>();

    /// <summary>运行中服务器原始快照（不带过滤）。刷新时先更新这个，再同步生成 FilteredRunningServers。</summary>
    private List<ServerInstance> _runningSnapshot = [];

    /// <summary>已知服务器原始快照（不带过滤）。刷新时先更新这个，再同步生成 FilteredKnownServers。</summary>
    private List<KnownServer> _knownSnapshot = [];

    public ServerDetectionViewModel(
        IServerDetector serverDetector,
        IAppConfigService appConfigService,
        IServerManagerService serverManager,
        IServerImporterService serverImporter)
    {
        Log.Information("📡 ServerDetectionViewModel 初始化");
        _serverDetector = serverDetector;
        _appConfigService = appConfigService;
        _serverManager = serverManager;
        _serverImporter = serverImporter;

        SelectedArguments = new ObservableCollection<string>();
        AllArgumentCategories = new ObservableCollection<ArgumentCategory>(Enum.GetValues<ArgumentCategory>());

        SelectedArguments.CollectionChanged += OnSelectedArgumentsChanged;

        foreach (var arg in JvmArgumentConstants.GetRecommendedArguments())
        {
            SelectedArguments.Add(BuildFullArgument(arg));
        }

        LoadKnownServers();
        _serverDetector.DetectionCompleted += OnAutoDetectCompleted;
    }

    /// <summary>
    /// 延迟启动自动检测 —— 在窗口渲染完成后调用
    /// </summary>
    public void DeferStart()
    {
        Log.Information("📡 ServerDetectionViewModel 延迟启动自动检测");
        StartAutoDetect();
    }

    /// <summary>
    /// 自动检测完成事件处理程序 —— 将检测结果同步至 UI 线程
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="result">检测结果</param>
    /// <remarks>
    /// 在后台线程触发，需通过 Dispatcher 封送到 UI 线程更新绑定属性。
    /// 若当前处于忙碌状态则丢弃更新，避免与手动操作产生状态竞态。
    /// </remarks>
    private void OnAutoDetectCompleted(object? sender, DetectionResult result)
    {
        if (IsBusy) return;

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => UpdateDetectionResult(result)));
                return;
            }
        }
        UpdateDetectionResult(result);
    }

    /// <summary>
    /// 将检测结果更新至绑定属性，触发 UI 列表刷新
    /// </summary>
    /// <param name="result">检测结果实例</param>
    private void UpdateDetectionResult(DetectionResult result)
    {
        if (IsBusy) return;

        DetectionResult = result;

        // Pattern3 修复：刷新后 SelectedServer / SelectedKnownServer 的联动重选。
        // 之前的实现只靠 ServerJarPath 精确相等，且命中时不把 SelectedServer 替换成新对象
        // → 导致：
        //   ① 重启后 PID 变了，但 SelectedServer 仍指向旧实例（旧 PID/旧堆内存/旧 GCType）
        //   ② 若用户只选中了 KnownServer（未运行），刷新后会被强行切到 result.Servers[0]
        //      （运行中第一台），KnownServer 的选中状态被无声抢走。

        var servers = result.Servers;

        // ① 如果用户之前选中了 KnownServer，先看这次检测里是否有对应运行实例：
        //    若能通过 KnownServerId / ServerJarPath 匹配到 → 自动联动为运行中实例（保持上下文），
        //    但不要覆盖 SelectedKnownServer（保持编辑区 JVM 参数同步）。
        if (SelectedServer == null && SelectedKnownServer != null)
        {
            var matched = servers.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.KnownServerId) && s.KnownServerId == SelectedKnownServer.KnownServerId)
                ?? servers.FirstOrDefault(s =>
                       !string.IsNullOrEmpty(s.ServerJarPath)
                       && string.Equals(s.ServerJarPath, SelectedKnownServer.ServerJarPath, StringComparison.OrdinalIgnoreCase)
                       && (string.IsNullOrEmpty(s.WorkingDirectory)
                           || string.IsNullOrEmpty(SelectedKnownServer.WorkingDirectory)
                           || string.Equals(s.WorkingDirectory, SelectedKnownServer.WorkingDirectory, StringComparison.OrdinalIgnoreCase)));
            if (matched != null)
            {
                SelectedServer = matched;
            }
            return;
        }

        // ② 如果之前选中了运行实例：按匹配规则重定位到这次刷新后的新实例（可能 PID 已变）。
        //    匹配优先级（高→低）：
        //      a) KnownServerId 相等（稳定主键，跨重启/重扫不变）
        //      b) ServerJarPath 相等 + WorkingDirectory 相等（目录/核心没变，只是 PID 变）
        //      c) ServerJarPath 相等（用户可能把服务器拷到同目录不同子目录后运行）
        if (SelectedServer != null)
        {
            ServerInstance? updated = null;

            if (!string.IsNullOrEmpty(SelectedServer.KnownServerId))
            {
                updated = servers.FirstOrDefault(s =>
                    s.KnownServerId == SelectedServer.KnownServerId);
            }

            if (updated == null && !string.IsNullOrEmpty(SelectedServer.ServerJarPath))
            {
                updated = servers.FirstOrDefault(s =>
                    string.Equals(s.ServerJarPath, SelectedServer.ServerJarPath, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(s.WorkingDirectory)
                        || string.IsNullOrEmpty(SelectedServer.WorkingDirectory)
                        || string.Equals(s.WorkingDirectory, SelectedServer.WorkingDirectory, StringComparison.OrdinalIgnoreCase)));
            }

            if (updated == null && !string.IsNullOrEmpty(SelectedServer.ServerJarPath))
            {
                updated = servers.FirstOrDefault(s =>
                    string.Equals(s.ServerJarPath, SelectedServer.ServerJarPath, StringComparison.OrdinalIgnoreCase));
            }

            if (updated != null)
            {
                SelectedServer = updated;
                return;
            }

            // 没匹配到：该实例确实不再运行。此时若当前仍强制指向旧的，会误导 UI。
            // 但不要跳到第一台服务器（可能完全不是用户想看的）。仅当完全没选过 KnownServer 时才 fallback。
            if (SelectedKnownServer == null)
                SelectedServer = null;
            return;
        }

        // ③ 什么都没选：选第一台运行中服务器（如果有），否则保持空。
        if (SelectedKnownServer == null)
        {
            SelectedServer = servers.Count > 0 ? servers[0] : null;
        }
    }

    /// <summary>
    /// 指示自动检测功能是否启用
    /// </summary>
    /// <remarks>
    /// 由源生成器生成 <c>IsAutoDetectEnabled</c> 属性，变更时通知
    /// <see cref="AutoDetectStatusText"/> 与 <see cref="AutoDetectIcon"/> 刷新。
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoDetectStatusText))]
    [NotifyPropertyChangedFor(nameof(AutoDetectIcon))]
    private bool _isAutoDetectEnabled;

    /// <summary>自动检测状态描述文本</summary>
    public string AutoDetectStatusText => IsAutoDetectEnabled ? "自动检测中" : "自动检测已暂停";

    /// <summary>自动检测图标标识符</summary>
    public string AutoDetectIcon => IsAutoDetectEnabled ? "PauseSolid" : "PlaySolid";

    /// <summary>
    /// 切换自动检测状态命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击自动检测切换按钮。
    /// 副作用：调用 <see cref="IServerDetector.StartAutoDetect"/> 或
    /// <see cref="IServerDetector.StopAutoDetect"/> 控制后台检测循环。
    /// </remarks>
    [RelayCommand]
    private void ToggleAutoDetect()
    {
        if (IsAutoDetectEnabled)
        {
            StopAutoDetect();
        }
        else
        {
            StartAutoDetect();
        }
    }

    /// <summary>启动自动检测循环</summary>
    private void StartAutoDetect()
    {
        if (_serverDetector.IsAutoDetectRunning)
        {
            IsAutoDetectEnabled = true;
            return;
        }

        _serverDetector.StartAutoDetect();
        IsAutoDetectEnabled = true;
        Log.Information("⏱️ 自动检测已启动");
    }

    /// <summary>停止自动检测循环</summary>
    private void StopAutoDetect()
    {
        _serverDetector.StopAutoDetect();
        IsAutoDetectEnabled = false;
        Log.Information("⏹️ 自动检测已暂停");
    }

    /// <summary>
    /// 当前活动操作 —— 用作互斥锁状态机
    /// </summary>
    /// <remarks>
    /// 确保任意时刻仅有一种操作处于进行状态。变更时自动通知
    /// <see cref="IsIdle"/>、<see cref="BusyReasonText"/>、<see cref="CanShowOperation"/>
    /// 以及各命令的 CanExecute 刷新。
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(BusyReasonText))]
    [NotifyPropertyChangedFor(nameof(CanShowOperation))]
    private ServerOperation _activeOperation = ServerOperation.None;

    /// <summary>获取一个值，指示当前是否无操作进行中</summary>
    public bool IsIdle => ActiveOperation == ServerOperation.None;

    /// <summary>获取一个值，指示当前是否有操作正在进行</summary>
    public bool IsBusy => ActiveOperation != ServerOperation.None;

    /// <summary>忙碌状态描述文本</summary>
    public string BusyReasonText => ActiveOperation switch
    {
        ServerOperation.Detecting => "🔍 正在扫描服务器进程...",
        ServerOperation.Importing => "📦 正在导入服务器...",
        ServerOperation.Starting => "🚀 正在启动服务器...",
        ServerOperation.Stopping => "🛑 正在停止服务器...",
        ServerOperation.SavingConfig => "💾 正在保存配置...",
        ServerOperation.Deleting => "🗑️ 正在删除...",
        _ => string.Empty
    };

    /// <summary>获取一个值，指示是否应显示操作进度 UI</summary>
    public bool CanShowOperation => IsBusy;

    /// <summary>
    /// 开始一个操作作用域 —— 保存当前操作状态并切换到新操作，Dispose 时自动恢复
    /// </summary>
    /// <param name="operation">要开始的操作类型</param>
    /// <returns>IDisposable 作用域对象，using 结束时自动恢复原操作状态</returns>
    /// <remarks>
    /// 消除 7 处重复的 `var previousOperation = ActiveOperation; ActiveOperation = Xxx;
    /// try { ... } finally { if (ActiveOperation == Xxx) ActiveOperation = previousOperation; }` 模式。
    /// 仅当 Dispose 时 ActiveOperation 仍为本次设置值才恢复，避免被嵌套操作覆盖后错误回滚。
    /// </remarks>
    private OperationScope BeginOperation(ServerOperation operation)
    {
        var previous = ActiveOperation;
        ActiveOperation = operation;
        return new OperationScope(this, operation, previous);
    }

    /// <summary>
    /// 操作作用域 —— 配合 <see cref="BeginOperation"/> 使用，Dispose 时恢复原操作状态
    /// </summary>
    private readonly struct OperationScope : IDisposable
    {
        private readonly ServerDetectionViewModel _owner;
        private readonly ServerOperation _currentOperation;
        private readonly ServerOperation _previousOperation;

        public OperationScope(ServerDetectionViewModel owner, ServerOperation current, ServerOperation previous)
        {
            _owner = owner;
            _currentOperation = current;
            _previousOperation = previous;
        }

        public void Dispose()
        {
            // 仅当当前操作仍为本次设置值才恢复，避免嵌套操作被错误回滚
            if (_owner.ActiveOperation == _currentOperation)
                _owner.ActiveOperation = _previousOperation;
        }
    }

    /// <summary>
    /// 活动操作变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的操作状态</param>
    /// <remarks>通知所有依赖 <see cref="IsBusy"/> 的命令刷新 CanExecute 状态。</remarks>
    partial void OnActiveOperationChanged(ServerOperation value)
    {
        DetectCommand.NotifyCanExecuteChanged();
        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();
        SaveAsKnownServerCommand.NotifyCanExecuteChanged();
        StartKnownServerCommand.NotifyCanExecuteChanged();
        RemoveKnownServerCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 服务器检测结果
    /// </summary>
    [ObservableProperty]
    private DetectionResult? _detectionResult;

    /// <summary>
    /// 当前选中的运行中服务器实例
    /// </summary>
    [ObservableProperty]
    private ServerInstance? _selectedServer;

    /// <summary>检测日志合并文本</summary>
    public string DetectionLog => DetectionResult is not null
        ? string.Join(Environment.NewLine, DetectionResult.LogMessages)
        : string.Empty;

    /// <summary>
    /// 检测结果变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的检测结果</param>
    /// <remarks>触发检测日志、运行中服务器列表以及当前状态的刷新。</remarks>
    partial void OnDetectionResultChanged(DetectionResult? value)
    {
        OnPropertyChanged(nameof(DetectionLog));
        RefreshFilteredRunningServers();
        RefreshCurrentStatus();
    }

    /// <summary>
    /// 当前选中服务器的运行状态
    /// </summary>
    /// <remarks>
    /// 变更时通知 <see cref="CurrentServerStatusText"/>、<see cref="CurrentServerStatusBrush"/>、
    /// <see cref="CurrentServerStatusIcon"/>、<see cref="HasSelectedServer"/> 及
    /// <see cref="SelectedServerSubtitle"/> 刷新显示。
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentServerStatusText))]
    [NotifyPropertyChangedFor(nameof(CurrentServerStatusBrush))]
    [NotifyPropertyChangedFor(nameof(CurrentServerStatusIcon))]
    [NotifyPropertyChangedFor(nameof(HasSelectedServer))]
    [NotifyPropertyChangedFor(nameof(SelectedServerSubtitle))]
    private ServerStatus _currentServerStatus = ServerStatus.Unknown;

    /// <summary>当前服务器状态描述文本</summary>
    public string CurrentServerStatusText => CurrentServerStatus switch
    {
        ServerStatus.Running => $"🟢 运行中{(GetActiveServer() is { } s && s.ProcessId > 0 ? $" (PID: {s.ProcessId})" : string.Empty)}",
        ServerStatus.Starting => "🟡 启动中...",
        ServerStatus.Stopping => "🟠 停止中...",
        ServerStatus.Stopped => "⚫ 已停止",
        ServerStatus.Error => "🔴 异常",
        _ => "❓ 未知"
    };

    /// <summary>当前服务器状态对应的画刷颜色</summary>
    public Brush CurrentServerStatusBrush => CurrentServerStatus switch
    {
        ServerStatus.Running => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        ServerStatus.Starting => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
        ServerStatus.Stopping => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
        ServerStatus.Stopped => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75)),
        ServerStatus.Error => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        _ => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E))
    };

    /// <summary>当前服务器状态对应的图标标识符</summary>
    public string CurrentServerStatusIcon => CurrentServerStatus switch
    {
        ServerStatus.Running => "CirclePlaySolid",
        ServerStatus.Starting => "SpinnerSolid",
        ServerStatus.Stopping => "CircleStopSolid",
        ServerStatus.Stopped => "CirclePauseSolid",
        ServerStatus.Error => "CircleExclamationSolid",
        _ => "CircleQuestionSolid"
    };

    /// <summary>获取一个值，指示当前是否存在已选中的服务器</summary>
    public bool HasSelectedServer => SelectedServer != null || SelectedKnownServer != null;

    /// <summary>选中服务器副标题文本</summary>
    public string SelectedServerSubtitle => GetActiveServer() is { } active
        ? active.DisplayName
        : "未选择服务器";

    /// <summary>已知服务器集合（原始快照的包装，只读可枚举）。
    /// 给外部（MainWindow 桥接层、日志等）枚举所有 KnownServer 用，
    /// 内部实际以 _knownSnapshot 为准，FilteredKnownServers 才是真正给 ItemsControl 绑定的过滤后列表。
    /// HasKnownServers 基于原始快照 Count。
    /// </summary>
    public IEnumerable<KnownServer> KnownServers => _knownSnapshot;

    /// <summary>获取一个值，指示已知服务器集合是否非空</summary>
    public bool HasKnownServers => _knownSnapshot.Count > 0;

    /// <summary>
    /// 当前选中的已知服务器
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedServer))]
    [NotifyPropertyChangedFor(nameof(SelectedServerSubtitle))]
    private KnownServer? _selectedKnownServer;

    /// <summary>
    /// 搜索过滤关键字
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchKeyword))]
    private string _searchKeyword = string.Empty;

    /// <summary>获取一个值，指示搜索关键字是否非空</summary>
    public bool HasSearchKeyword => !string.IsNullOrWhiteSpace(SearchKeyword);

    /// <summary>
    /// 搜索关键字变更回调 —— 基于原始快照重算过滤后的列表
    /// </summary>
    /// <param name="value">新的搜索关键字</param>
    /// <remarks>
    /// 新设计：不再走 ListCollectionView.Filter + Refresh（会触发 PrepareLocalArray NRE），
    /// 而是手动用 LINQ Where 从快照过滤出一份新 List 并整体替换属性引用。
    /// </remarks>
    partial void OnSearchKeywordChanged(string value)
    {
        ReapplyFilter();
    }

    /// <summary>
    /// 根据当前 SearchKeyword 重新计算两张过滤列表，并同时 OnPropertyChanged。
    /// </summary>
    private void ReapplyFilter()
    {
        var kw = (SearchKeyword ?? string.Empty).Trim();

        // 运行中服务器：从 _runningSnapshot 过滤
        IReadOnlyList<ServerInstance> newRunning;
        if (string.IsNullOrEmpty(kw))
        {
            newRunning = _runningSnapshot;
        }
        else
        {
            newRunning = _runningSnapshot
                .Where(s => MatchesSearchRunning(s, kw))
                .ToList();
        }
        FilteredRunningServers = newRunning;

        // 已知服务器：从 _knownSnapshot 过滤
        IReadOnlyList<KnownServer> newKnown;
        if (string.IsNullOrEmpty(kw))
        {
            newKnown = _knownSnapshot;
        }
        else
        {
            newKnown = _knownSnapshot
                .Where(s => MatchesSearchKnown(s, kw))
                .ToList();
        }
        FilteredKnownServers = newKnown;

        // HasKnownServers / HasSearchKeyword 的计算属性依赖 KnownServers.Count / SearchKeyword，
        // 这俩已经通过 ObservableProperty 的源生成器自动发 PropertyChanged 了。
    }

    /// <summary>运行中服务器搜索匹配（单测可用，无副作用）</summary>
    private static bool MatchesSearchRunning(ServerInstance s, string keyword)
    {
        if (s is null) return false;
        if (string.IsNullOrEmpty(keyword)) return true;
        return (s.ServerJarName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            || (s.WorkingDirectory?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            || (s.DisplayName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>已知服务器搜索匹配（单测可用，无副作用）</summary>
    private static bool MatchesSearchKnown(KnownServer ks, string keyword)
    {
        if (ks is null) return false;
        if (string.IsNullOrEmpty(keyword)) return true;
        return (ks.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            || (ks.ServerJarPath?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            || (ks.Notes?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>
    /// 刷新运行中服务器列表：保存原始快照 → 立即重算过滤 → 发 PropertyChanged
    /// </summary>
    private void RefreshFilteredRunningServers()
    {
        var snapshot = DetectionResult?.Servers is { } servers
            ? servers.Where(s => s is not null).ToList()
            : new List<ServerInstance>();

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => ApplyRunningSnapshot(snapshot)));
            return;
        }
        ApplyRunningSnapshot(snapshot);

        void ApplyRunningSnapshot(List<ServerInstance> list)
        {
            _runningSnapshot = list;
            // 走同一条过滤链路（SearchKeyword 非空时会自动 Where）
            ReapplyFilter();
        }
    }

    /// <summary>
    /// 右侧内容区当前选中的 Tab 索引
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>JVM 初始堆内存大小</summary>
    [ObservableProperty] private string _initialMemory = "2G";
    /// <summary>JVM 最大堆内存大小</summary>
    [ObservableProperty] private string _maxMemory = "4G";
    /// <summary>当前选中的 JVM 参数分类</summary>
    [ObservableProperty] private ArgumentCategory _selectedCategory = ArgumentCategory.Memory;
    /// <summary>用户输入的自定义 JVM 参数</summary>
    [ObservableProperty] private string _customArgument = string.Empty;
    /// <summary>当前处于编辑状态的 JVM 参数</summary>
    [ObservableProperty] private string _selectedArgumentToEdit = string.Empty;
    /// <summary>正在编辑的参数值</summary>
    [ObservableProperty] private string _editingArgumentValue = string.Empty;
    /// <summary>指示当前是否处于参数编辑状态</summary>
    [ObservableProperty] private bool _isEditingArgument;

    /// <summary>已选中的 JVM 参数集合</summary>
    public ObservableCollection<string> SelectedArguments { get; }
    /// <summary>所有可用的 JVM 参数分类</summary>
    public ObservableCollection<ArgumentCategory> AllArgumentCategories { get; }

    /// <summary>
    /// 按当前分类过滤后的可用 JVM 参数定义列表
    /// </summary>
    /// <remarks>排除已选中的参数（基于参数基名去重）。</remarks>
    public List<JvmArgumentDefinition> FilteredArguments
    {
        get
        {
            var all = JvmArgumentConstants.GetArgumentsByCategory(SelectedCategory);
            var selectedBaseNames = new HashSet<string>(
                SelectedArguments.Select(GetArgumentBaseName),
                StringComparer.OrdinalIgnoreCase);
            return all.Where(a => !selectedBaseNames.Contains(GetArgumentBaseName(a.Flag))).ToList();
        }
    }

    /// <summary>
    /// 启动命令预览字符串
    /// </summary>
    public string StartupCommandPreview
    {
        get
        {
            var args = new List<string> { $"-Xms{InitialMemory}", $"-Xmx{MaxMemory}" };
            args.AddRange(SelectedArguments.Where(a =>
                !a.StartsWith("-Xms") && !a.StartsWith("-Xmx")));
            return string.Join(" ", args);
        }
    }

    /// <summary>
    /// 参数分类变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的分类值</param>
    partial void OnSelectedCategoryChanged(ArgumentCategory value)
        => OnPropertyChanged(nameof(FilteredArguments));

    /// <summary>
    /// 初始内存变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的初始内存值</param>
    partial void OnInitialMemoryChanged(string value)
        => OnPropertyChanged(nameof(StartupCommandPreview));

    /// <summary>
    /// 最大内存变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的最大内存值</param>
    partial void OnMaxMemoryChanged(string value)
        => OnPropertyChanged(nameof(StartupCommandPreview));

    /// <summary>
    /// 选择 JVM 参数分类命令
    /// </summary>
    /// <param name="category">目标分类</param>
    /// <remarks>
    /// 触发条件：用户点击分类 Tab。
    /// 副作用：更新 <see cref="SelectedCategory"/> 并刷新 <see cref="FilteredArguments"/>。
    /// </remarks>
    [RelayCommand]
    private void SelectCategory(ArgumentCategory category)
    {
        SelectedCategory = category;
    }

    /// <summary>
    /// 添加 JVM 参数命令
    /// </summary>
    /// <param name="flag">参数标志</param>
    /// <remarks>
    /// 触发条件：用户点击可用参数列表中的添加按钮。
    /// 副作用：将参数追加至 <see cref="SelectedArguments"/> 集合。
    /// </remarks>
    [RelayCommand]
    private void AddArgument(string flag)
    {
        if (IsBusy) return;
        var argDef = JvmArgumentConstants.AllArguments.FirstOrDefault(a => a.Flag == flag);
        string fullArg = argDef != null ? BuildFullArgument(argDef) : flag;

        if (!SelectedArguments.Contains(fullArg))
        {
            SelectedArguments.Add(fullArg);
            Log.Debug("➕ 添加参数: {Arg}", fullArg);
        }
    }

    /// <summary>
    /// 移除 JVM 参数命令
    /// </summary>
    /// <param name="flag">参数标志</param>
    /// <remarks>
    /// 触发条件：用户点击已选参数列表中的移除按钮。
    /// 副作用：从 <see cref="SelectedArguments"/> 中移除参数，若该参数正处于编辑状态则退出编辑。
    /// </remarks>
    [RelayCommand]
    private void RemoveArgument(string flag)
    {
        if (IsBusy) return;
        if (SelectedArguments.Contains(flag))
        {
            SelectedArguments.Remove(flag);
        }

        if (SelectedArgumentToEdit == flag)
        {
            IsEditingArgument = false;
            SelectedArgumentToEdit = string.Empty;
            EditingArgumentValue = string.Empty;
        }
    }

    /// <summary>
    /// 进入 JVM 参数编辑模式命令
    /// </summary>
    /// <param name="argument">待编辑的参数</param>
    /// <remarks>
    /// 触发条件：用户点击参数编辑按钮。
    /// 副作用：设置 <see cref="IsEditingArgument"/> 为 <c>true</c>，
    /// 并将参数值填充至 <see cref="EditingArgumentValue"/>。
    /// </remarks>
    [RelayCommand]
    private void StartEditArgument(string argument)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(argument)) return;

        SelectedArgumentToEdit = argument;
        EditingArgumentValue = ExtractArgumentValue(argument);
        IsEditingArgument = true;
    }

    /// <summary>
    /// 保存 JVM 参数编辑命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击参数编辑保存按钮。
    /// 副作用：更新 <see cref="SelectedArguments"/> 中的参数值，退出编辑状态。
    /// 验证：通过 <c>JvmArgumentNormalizer.ValidateArgument</c> 验证参数合法性。
    /// </remarks>
    [RelayCommand]
    private void SaveEditArgument()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(SelectedArgumentToEdit)) return;

        var baseName = GetArgumentBaseName(SelectedArgumentToEdit);
        var newArgument = BuildArgumentFromValue(baseName, EditingArgumentValue);

        var (isValid, error) = JvmArgumentNormalizer.ValidateArgument(newArgument);
        if (!isValid) return;

        var index = SelectedArguments.IndexOf(SelectedArgumentToEdit);
        if (index >= 0)
        {
            SelectedArguments[index] = newArgument;
        }

        IsEditingArgument = false;
        SelectedArgumentToEdit = string.Empty;
        EditingArgumentValue = string.Empty;
    }

    /// <summary>
    /// 取消 JVM 参数编辑命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击取消编辑按钮。
    /// 副作用：清除编辑状态，不修改 <see cref="SelectedArguments"/>。
    /// </remarks>
    [RelayCommand]
    private void CancelEditArgument()
    {
        IsEditingArgument = false;
        SelectedArgumentToEdit = string.Empty;
        EditingArgumentValue = string.Empty;
    }

    /// <summary>
    /// 添加自定义 JVM 参数命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户在自定义参数输入框中按下确认。
    /// 副作用：将 <see cref="CustomArgument"/> 追加至 <see cref="SelectedArguments"/>。
    /// 验证：通过 <c>JvmArgumentNormalizer.ValidateArgument</c> 验证参数合法性。
    /// </remarks>
    [RelayCommand]
    private void AddCustomArgument()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(CustomArgument)) return;

        var (isValid, error) = JvmArgumentNormalizer.ValidateArgument(CustomArgument);
        if (!isValid) return;

        if (SelectedArguments.Contains(CustomArgument)) return;

        SelectedArguments.Add(CustomArgument);
        CustomArgument = string.Empty;
    }

    /// <summary>
    /// 应用 Aikar JVM 参数预设命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击 Aikar 预设按钮。
    /// 副作用：替换 <see cref="SelectedArguments"/> 为 Aikar 推荐参数集。
    /// </remarks>
    [RelayCommand]
    private void ApplyAikarPreset()
    {
        if (IsBusy) return;
        ApplyPreset(ApplyAikarFlags(), "Aikar");
    }

    /// <summary>
    /// 应用 G1GC JVM 参数预设命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击 G1GC 预设按钮。
    /// 副作用：替换 <see cref="SelectedArguments"/> 为 G1GC 参数集。
    /// </remarks>
    [RelayCommand]
    private void ApplyG1GCPreset()
    {
        if (IsBusy) return;
        ApplyPreset(ApplyG1GCFlags(), "G1GC");
    }

    /// <summary>
    /// 应用 ZGC JVM 参数预设命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击 ZGC 预设按钮。
    /// 副作用：替换 <see cref="SelectedArguments"/> 为 ZGC 参数集。
    /// </remarks>
    [RelayCommand]
    private void ApplyZgcPreset()
    {
        if (IsBusy) return;
        ApplyPreset(ApplyZgcFlags(), "ZGC");
    }

    /// <summary>
    /// 获取 Aikar 推荐的 JVM 参数列表
    /// </summary>
    /// <returns>Aikar 参数列表</returns>
    private static List<string> ApplyAikarFlags() =>
    [
        "-XX:+UseG1GC",
        "-XX:+ParallelRefProcEnabled",
        "-XX:MaxGCPauseMillis=200",
        "-XX:+UnlockExperimentalVMOptions",
        "-XX:+DisableExplicitGC",
        "-XX:+AlwaysPreTouch",
        "-XX:G1NewSizePercent=30",
        "-XX:G1MaxNewSizePercent=40",
        "-XX:G1HeapRegionSize=8M",
        "-XX:G1ReservePercent=20",
        "-XX:G1HeapWastePercent=5",
        "-XX:G1MixedGCCountTarget=4",
        "-XX:InitiatingHeapOccupancyPercent=15",
        "-XX:G1MixedGCLiveThresholdPercent=90",
        "-XX:G1RSetUpdatingPauseTimePercent=5",
        "-XX:SurvivorRatio=32",
        "-XX:+PerfDisableSharedMem",
        "-XX:MaxTenuringThreshold=1",
        "-Dfile.encoding=UTF-8",
        "-Dlog4j2.formatMsgNoLookups=true",
        "-Dusing.aikars.flags=https://mcflags.emc.gs",
        "-Daikars.new.flags=true"
    ];

    /// <summary>
    /// 获取 G1GC 基础 JVM 参数列表
    /// </summary>
    /// <returns>G1GC 参数列表</returns>
    private static List<string> ApplyG1GCFlags() =>
    [
        "-XX:+UseG1GC",
        "-XX:MaxGCPauseMillis=200",
        "-XX:+AlwaysPreTouch",
        "-XX:+DisableExplicitGC",
        "-Dfile.encoding=UTF-8",
        "-Dlog4j2.formatMsgNoLookups=true"
    ];

    /// <summary>
    /// 获取 ZGC 基础 JVM 参数列表
    /// </summary>
    /// <returns>ZGC 参数列表</returns>
    /// <remarks>
    /// 注意：-XX:+ZGenerational 仅在 Java 21+ 中支持，且 Java 21+ 中 ZGC 默认为分代模式，
    /// 无需显式指定。为了兼容性，此处不添加该参数。
    /// </remarks>
    private static List<string> ApplyZgcFlags() =>
    [
        "-XX:+UseZGC",
        "-XX:+DisableExplicitGC",
        "-XX:+AlwaysPreTouch",
        "-Dfile.encoding=UTF-8",
        "-Dlog4j2.formatMsgNoLookups=true"
    ];

    /// <summary>
    /// 应用 JVM 参数预设
    /// </summary>
    private void ApplyPreset(List<string> flags, string name)
    {
        SelectedArguments.Clear();
        foreach (var flag in flags) SelectedArguments.Add(flag);
        Log.Information("🎯 应用 {Name} 预设参数", name);
    }

    /// <summary>当前操作提示消息</summary>
    [ObservableProperty] private string _operationMessage = string.Empty;

    /// <summary>
    /// 启动当前选中的服务器命令
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    /// <remarks>
    /// 触发条件：用户点击启动按钮且服务器未运行。
    /// 副作用：设置 <see cref="ActiveOperation"/> 为 Starting，
    /// 调用 <see cref="IServerManagerService.StartServer"/> 启动进程，
    /// 完成后触发检测刷新服务器列表。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanStartCurrent))]
    private async Task StartCurrentServerAsync()
    {
        if (IsBusy) return;
        var server = GetActiveServer();
        if (server is null) return;

        using var scope = BeginOperation(ServerOperation.Starting);
        CurrentServerStatus = ServerStatus.Starting;
        OperationMessage = "🚀 正在启动服务器...";

        try
        {
            var jvmArgs = BuildCurrentJvmArguments();
            var serverToStart = new ServerInstance
            {
                ProcessId = 0,
                ServerType = server.ServerType,
                WorkingDirectory = server.WorkingDirectory,
                JavaPath = server.JavaPath,
                ServerJarPath = server.ServerJarPath,
                ServerJarName = server.ServerJarName,
                JvmArguments = jvmArgs,
                InitialHeapMemoryBytes = ParseMemorySize(InitialMemory),
                MaxHeapMemoryBytes = ParseMemorySize(MaxMemory),
                ConfigFiles = server.ConfigFiles,
                UsesAikarFlags = jvmArgs.Any(a => a.Contains("aikars")),
                GcType = DetermineGcType(jvmArgs),
                ServerPort = server.ServerPort
            };

            var process = await Task.Run(() => _serverManager.StartServer(serverToStart));

            if (process != null)
            {
                // 立即注册启动时 PID → 绕过 WMI 索引延迟，确保 PID 立刻被识别并关联
                _serverDetector.RegisterStartedServerPid(
                    knownServerId: server.KnownServerId,
                    jarPath: server.ServerJarPath,
                    pid: process.Id);

                OperationMessage = $"✅ 服务器启动成功! PID: {process.Id}";
                Log.Information("🚀 服务器启动成功: PID={Pid}", process.Id);
                CurrentServerStatus = ServerStatus.Running;

                await Task.Delay(1500);
                ActiveOperation = ServerOperation.None;
                await DetectAsync();
            }
            else
            {
                OperationMessage = "❌ 服务器启动失败";
                Log.Error("❌ 服务器启动失败");
                CurrentServerStatus = ServerStatus.Error;
            }
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 启动异常: {ex.Message}";
            Log.Error(ex, "💥 启动服务器异常");
            CurrentServerStatus = ServerStatus.Error;
        }
        finally
        {
            // 启动成功时保持 Running 状态，不刷新 ——
            // RefreshCurrentStatus 依赖 WMI/进程检测，新进程可能尚未被 WMI 索引到，
            // 立即刷新会把 Running 误判为 Stopped，导致前端收到 success=false。
            // 仅在未成功启动时刷新状态。
            if (CurrentServerStatus != ServerStatus.Running)
            {
                RefreshCurrentStatus();
            }
        }
    }

    /// <summary>
    /// 确定启动命令是否可执行
    /// </summary>
    /// <returns>可启动则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool CanStartCurrent()
    {
        if (IsBusy) return false;
        var server = GetActiveServer();
        if (server is null) return false;
        return !_serverManager.IsServerRunning(server);
    }

    /// <summary>
    /// 停止当前选中的服务器命令
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    /// <remarks>
    /// 触发条件：用户点击停止按钮且服务器正在运行。
    /// 副作用：设置 <see cref="ActiveOperation"/> 为 Stopping，
    /// 调用 <see cref="IServerManagerService.StopServer"/> 终止进程，
    /// 完成后触发检测刷新服务器列表。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanStopCurrent))]
    private async Task StopCurrentServerAsync()
    {
        if (IsBusy) return;
        var server = GetActiveServer();
        if (server is null) return;

        using var scope = BeginOperation(ServerOperation.Stopping);
        CurrentServerStatus = ServerStatus.Stopping;
        OperationMessage = "🛑 正在停止服务器...";

        try
        {
            var success = await Task.Run(() => _serverManager.StopServer(server));
            OperationMessage = success ? "✅ 服务器已停止" : "⚠️ 停止失败，进程可能仍在运行，请检查任务管理器";

            if (success)
            {
                await Task.Delay(800);
                ActiveOperation = ServerOperation.None;
                await DetectAsync();
            }
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 停止异常: {ex.Message}";
            Log.Error(ex, "💥 停止服务器异常");
        }
        finally
        {
            // OperationScope.Dispose 已恢复 ActiveOperation，此处仅刷新服务器状态
            RefreshCurrentStatus();
        }
    }

    /// <summary>
    /// 确定停止命令是否可执行
    /// </summary>
    /// <returns>可停止则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool CanStopCurrent()
    {
        if (IsBusy) return false;
        var server = GetActiveServer();
        if (server is null) return false;
        return _serverManager.IsServerRunning(server);
    }

    /// <summary>
    /// 检查是否存在任何正在运行的服务器实例 —— 供 MainWindow 关闭确认透传使用
    /// </summary>
    /// <returns>若有服务器正在运行返回 true</returns>
    public bool AnyServerRunning() => _serverManager.AnyServerRunning();

    /// <summary>
    /// 获取当前活动的服务器实例
    /// </summary>
    /// <returns>运行中服务器或已知服务器转换后的实例；无选中则返回 <c>null</c></returns>
    private ServerInstance? GetActiveServer()
    {
        if (SelectedServer != null) return SelectedServer;
        if (SelectedKnownServer != null)
        {
            return new ServerInstance
            {
                ServerJarPath = SelectedKnownServer.ServerJarPath,
                ServerJarName = Path.GetFileName(SelectedKnownServer.ServerJarPath),
                WorkingDirectory = SelectedKnownServer.WorkingDirectory,
                JavaPath = SelectedKnownServer.JavaPath,
                InitialHeapMemoryBytes = SelectedKnownServer.InitialHeapMemoryBytes,
                MaxHeapMemoryBytes = SelectedKnownServer.MaxHeapMemoryBytes,
                JvmArguments = SelectedKnownServer.JvmArguments,
                ServerPort = SelectedKnownServer.Port
            };
        }
        return null;
    }

    /// <summary>
    /// 刷新当前选中服务器的运行状态
    /// </summary>
    private void RefreshCurrentStatus()
    {
        var server = GetActiveServer();
        if (server is null)
        {
            CurrentServerStatus = ServerStatus.Unknown;
            return;
        }

        try
        {
            if (_serverManager.IsServerRunning(server))
            {
                CurrentServerStatus = ServerStatus.Running;
            }
            else
            {
                CurrentServerStatus = ServerStatus.Stopped;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "检查服务器运行状态失败");
            CurrentServerStatus = ServerStatus.Unknown;
        }

        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 浏览并导入服务器命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击导入服务器按钮。
    /// 副作用：打开文件选择对话框，将选中的 JAR 文件注册为已知服务器，
    /// 若已存在则加载其已保存的配置。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanImport))]
    private void BrowseAndImportServer()
    {
        if (IsBusy) return;

        var openFileDialog = new OpenFileDialog
        {
            Filter = "Minecraft 服务器核心 (*.jar)|*.jar|所有文件 (*.*)|*.*",
            Title = "选择 Minecraft 服务器核心文件",
            CheckFileExists = true
        };

        // 显式传入父窗口，避免从 WebView2 桥接调用时 IFileDialog 获取无效父句柄导致 SEHException
        var owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;

        if (openFileDialog.ShowDialog(owner) != true) return;

        var jarPath = openFileDialog.FileName;

        if (!File.Exists(jarPath))
        {
            OperationMessage = "❌ 文件不存在";
            return;
        }

        using var scope = BeginOperation(ServerOperation.Importing);
        OperationMessage = "📦 正在导入服务器...";

        try
        {
            var serverType = _serverImporter.DetectServerType(jarPath);
            var workingDir = _serverImporter.GetServerWorkingDirectory(jarPath);
            var pid = _serverManager.GetServerProcessId(jarPath);

            var jvmArgs = BuildCurrentJvmArguments();

            var server = new ServerInstance
            {
                ProcessId = pid ?? 0,
                ServerType = serverType,
                WorkingDirectory = workingDir ?? Path.GetDirectoryName(jarPath) ?? string.Empty,
                ServerJarPath = jarPath,
                ServerJarName = Path.GetFileName(jarPath),
                JvmArguments = jvmArgs,
                InitialHeapMemoryBytes = ParseMemorySize(InitialMemory),
                MaxHeapMemoryBytes = ParseMemorySize(MaxMemory)
            };

            var existing = _appConfigService.FindByJarPath(jarPath);
            if (existing != null)
            {
                SelectedKnownServer = existing;
                InitialMemory = FormatMemory(existing.InitialHeapMemoryBytes);
                MaxMemory = FormatMemory(existing.MaxHeapMemoryBytes);
                if (existing.JvmArguments.Count > 0)
                {
                    SelectedArguments.Clear();
                    foreach (var arg in existing.JvmArguments)
                        SelectedArguments.Add(arg);
                }
                OperationMessage = $"✅ 已加载已知服务器配置: {existing.Name}";
            }
            else
            {
                var known = new KnownServer
                {
                    Name = server.ServerJarName,
                    ServerJarPath = jarPath,
                    WorkingDirectory = server.WorkingDirectory,
                    JavaPath = server.JavaPath,
                    InitialHeapMemoryBytes = server.InitialHeapMemoryBytes,
                    MaxHeapMemoryBytes = server.MaxHeapMemoryBytes,
                    JvmArguments = jvmArgs,
                    Port = server.ServerPort,
                    AddedAt = DateTime.Now,
                    LastSeenAt = DateTime.Now
                };
                _appConfigService.AddKnownServer(known);
                LoadKnownServers();
                SelectedKnownServer = known;
                OperationMessage = $"✅ 服务器已添加到列表: {serverType}（点击启动按钮开始运行）";
            }

            StartCurrentServerCommand.NotifyCanExecuteChanged();
            StopCurrentServerCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 导入失败: {ex.Message}";
            Log.Error(ex, "💥 导入服务器异常");
        }
        finally
        {
            // OperationScope.Dispose 已恢复 ActiveOperation
        }
    }

    /// <summary>
    /// 确定导入命令是否可执行
    /// </summary>
    /// <returns>可导入则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool CanImport() => !IsBusy;

    /// <summary>
    /// 执行服务器进程检测命令
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    /// <remarks>
    /// 触发条件：用户点击检测按钮。
    /// 副作用：设置 <see cref="ActiveOperation"/> 为 Detecting，
    /// 调用 <see cref="IServerDetector.DetectAllAsync"/> 获取检测结果，
    /// 更新 <see cref="DetectionResult"/> 与 <see cref="SelectedServer"/>。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanDetect))]
    private async Task DetectAsync()
    {
        if (IsBusy) return;
        Log.Information("🔍 开始扫描服务器进程...");
        using var scope = BeginOperation(ServerOperation.Detecting);

        try
        {
            DetectionResult = await _serverDetector.DetectAllAsync();

            if (DetectionResult.Servers.Count > 0)
            {
                if (SelectedServer == null ||
                    !DetectionResult.Servers.Any(s => s.ServerJarPath == SelectedServer.ServerJarPath))
                {
                    SelectedServer = DetectionResult.Servers[0];
                }
            }
            else
            {
                SelectedServer = null;
            }

            Log.Information("✅ 扫描完成，发现 {Count} 个服务器", DetectionResult.Servers.Count);
        }
        catch (Exception ex)
        {
            DetectionResult = new DetectionResult
            {
                IsDetected = false,
                ErrorMessage = $"检测过程发生异常：{ex.Message}"
            };
            Log.Error(ex, "💥 服务器扫描失败: {Message}", ex.Message);
        }
        finally
        {
            // OperationScope.Dispose 已恢复 ActiveOperation
        }
    }

    /// <summary>
    /// 确定检测命令是否可执行
    /// </summary>
    /// <returns>可检测则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool CanDetect() => !IsBusy;

    /// <summary>
    /// 选中服务器变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的选中服务器实例</param>
    /// <remarks>
    /// 刷新相关命令的 CanExecute 状态、更新服务器状态显示，
    /// 并将选中服务器的 JVM 参数同步至编辑器。
    /// </remarks>
    partial void OnSelectedServerChanged(ServerInstance? value)
    {
        SaveAsKnownServerCommand.NotifyCanExecuteChanged();
        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();
        RefreshCurrentStatus();

        // Pattern6 修复：同 OnSelectedKnownServerChanged，切换运行实例（或清空）时，
        // 必须完全同步内存/参数，不能依赖「上一台状态」。
        if (value == null)
        {
            InitialMemory = string.Empty;
            MaxMemory = string.Empty;
            SelectedArguments.Clear();
            return;
        }

        InitialMemory = value.InitialHeapMemoryBytes > 0
            ? FormatMemory(value.InitialHeapMemoryBytes)
            : string.Empty;
        MaxMemory = value.MaxHeapMemoryBytes > 0
            ? FormatMemory(value.MaxHeapMemoryBytes)
            : string.Empty;

        SelectedArguments.Clear();
        foreach (var arg in value.JvmArguments)
        {
            if (arg.StartsWith("-Xms") || arg.StartsWith("-Xmx")) continue;
            SelectedArguments.Add(arg);
        }
    }

    /// <summary>
    /// 保存为已知服务器命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击保存为已知服务器按钮且存在选中的运行中服务器。
    /// 副作用：将当前服务器的 JVM 参数配置持久化至已知服务器列表。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSaveAsKnown))]
    private void SaveAsKnownServer()
    {
        if (IsBusy) return;
        if (SelectedServer is null) return;

        using var scope = BeginOperation(ServerOperation.SavingConfig);
        OperationMessage = "💾 正在保存配置...";

        try
        {
            var jvmArgs = BuildCurrentJvmArguments();

            // Pattern4 修复：匹配「已存在已知服务器」用优先级：
            //   1) 若 SelectedServer 已带 KnownServerId → 精确按 KnownServerId 查
            //      （避免同目录多 JAR 时 FindByJarPath 查错行）
            //   2) 再按 ServerJarPath 查（兼容 JarPath 迁移场景后回查）
            KnownServer? existing = null;
            if (!string.IsNullOrEmpty(SelectedServer.KnownServerId))
            {
                existing = _appConfigService.GetAllKnownServers()
                    .FirstOrDefault(k => k.KnownServerId == SelectedServer.KnownServerId);
            }
            if (existing == null && !string.IsNullOrEmpty(SelectedServer.ServerJarPath))
            {
                existing = _appConfigService.FindByJarPath(SelectedServer.ServerJarPath);
            }

            if (existing != null)
            {
                // 注意：KnownServer.Name 是静态档案名，不应该用带 PID 的 DisplayName 初始化。
                // 如果原来 Name 为空，用"类型 @ 目录名"格式命名，不包含运行时 PID。
                existing.Name = string.IsNullOrEmpty(existing.Name)
                    ? $"{SelectedServer.ServerType} @ {System.IO.Path.GetFileName(SelectedServer.WorkingDirectory)}"
                    : existing.Name;

                // Pattern4 修复：除了目录/端口/内存/JVMArgs，**也同步 ServerJarPath**。
                // 否则用户把 JAR 换了路径（如 paper-1.20.4.jar → paper-1.21.1.jar）
                // 保存后 FindByJarPath(新路径) 会查不到，导致「保存过的参数重启又没了」。
                existing.ServerJarPath = SelectedServer.ServerJarPath;
                existing.WorkingDirectory = SelectedServer.WorkingDirectory;
                existing.JavaPath = SelectedServer.JavaPath;
                existing.InitialHeapMemoryBytes = ParseMemorySize(InitialMemory);
                existing.MaxHeapMemoryBytes = ParseMemorySize(MaxMemory);
                existing.JvmArguments = jvmArgs;
                existing.Port = SelectedServer.ServerPort;
                existing.LastSeenAt = DateTime.Now;
                _appConfigService.UpdateKnownServer(existing);
            }
            else
            {
                var known = new KnownServer
                {
                    // 用"类型 @ 目录名"作为默认名称，不含运行时 PID。
                    // KnownServer 是静态档案，PID 是运行时概念，两者不应混在一起。
                    Name = $"{SelectedServer.ServerType} @ {System.IO.Path.GetFileName(SelectedServer.WorkingDirectory)}",
                    ServerJarPath = SelectedServer.ServerJarPath,
                    WorkingDirectory = SelectedServer.WorkingDirectory,
                    JavaPath = SelectedServer.JavaPath,
                    InitialHeapMemoryBytes = ParseMemorySize(InitialMemory),
                    MaxHeapMemoryBytes = ParseMemorySize(MaxMemory),
                    JvmArguments = jvmArgs,
                    Port = SelectedServer.ServerPort,
                    AddedAt = DateTime.Now,
                    LastSeenAt = DateTime.Now
                };
                _appConfigService.AddKnownServer(known);
                existing = known;
            }

            // Pattern3 修复：保存完成后立刻把 KnownServer 关联回运行中实例与编辑上下文。
            // 没做这步之前：
            //   - SelectedServer.KnownServerId 仍为 null → server:getSelected 返回 isKnown=false
            //     → 前端刷新又显示「保存为已知」按钮
            //   - SelectedKnownServer 为 null → jvm:getState 返回 hasServer=false
            //     → 启动参数又变成未知
            LoadKnownServers();
            SelectedServer.KnownServerId = existing.KnownServerId;
            SelectedKnownServer = existing;

            OperationMessage = $"💾 已保存到已知服务器: {SelectedServer.DisplayName}";
            Log.Information("💾 服务器已保存为已知服务器: {Name}", SelectedServer.DisplayName);
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 保存失败: {ex.Message}";
            Log.Error(ex, "💥 保存已知服务器异常");
        }
        finally
        {
            // OperationScope.Dispose 已恢复 ActiveOperation
        }
    }

    /// <summary>
    /// 确定保存为已知服务器命令是否可执行
    /// </summary>
    /// <returns>可保存则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool CanSaveAsKnown()
        => !IsBusy
        && SelectedServer != null
        // Q1 修复：如果服务器已经带 KnownServerId，说明它本来就是从「已知服务器」启动/被关联过的，
        // 不需要再走「保存到已知」逻辑（避免重复 Add / Update 带来的 Name 覆盖问题）。
        && string.IsNullOrEmpty(SelectedServer.KnownServerId);

    /// <summary>
    /// 删除已知服务器命令
    /// </summary>
    /// <param name="server">待删除的已知服务器</param>
    /// <remarks>
    /// 触发条件：用户点击已知服务器列表的删除按钮。
    /// 副作用：从 <see cref="KnownServers"/> 中移除条目并持久化至应用配置。
    /// 前置校验：若服务器正在运行则拒绝删除。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanRemoveKnown))]
    private void RemoveKnownServer(KnownServer? server)
    {
        if (IsBusy) return;
        if (server is null) return;

        try
        {
            if (File.Exists(server.ServerJarPath) && _serverManager.IsServerRunningByJarPath(server.ServerJarPath))
            {
                OperationMessage = "❌ 服务器正在运行，无法删除";
                Log.Warning("❌ 拒绝删除正在运行的服务器: {Name}", server.Name);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ 检查服务器运行状态失败，跳过删除前校验");
        }

        using var scope = BeginOperation(ServerOperation.Deleting);

        try
        {
            _appConfigService.RemoveKnownServer(server.Id);
            LoadKnownServers();

            // Pattern3 修复：删除已知服务器后，清理 SelectedKnownServer 与 SelectedServer 的关联。
            // 之前只把 SelectedKnownServer（如果指向同一个实例）置空，但：
            //   ① 如果 SelectedServer.KnownServerId == 被删的 server.Id，
            //      前端仍会通过 server:getSelected 看到 isKnown=true，但这条记录实际已删除
            //      → 进入「保存/配置」页会按 KnownServerId 查，得到 null，报异常或空数据
            //   ② 如果 CurrentSelection 是因为 SelectedServer 带 KnownServerId 而联动到
            //      SelectedKnownServer 的，这次删除后也必须联动清掉。
            if (SelectedKnownServer == server || SelectedKnownServer?.KnownServerId == server.KnownServerId)
                SelectedKnownServer = null;

            if (!string.IsNullOrEmpty(SelectedServer?.KnownServerId)
                && SelectedServer.KnownServerId == server.KnownServerId)
            {
                // ServerInstance.KnownServerId 已是 [ObservableProperty]，setter 自动发 PropertyChanged 通知
                SelectedServer.KnownServerId = null;
            }

            OperationMessage = $"🗑️ 已移除: {server.Name}";
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 删除失败: {ex.Message}";
            Log.Error(ex, "💥 删除已知服务器异常");
        }
        finally
        {
            // OperationScope.Dispose 已恢复 ActiveOperation
        }
    }

    /// <summary>
    /// 确定删除已知服务器命令是否可执行
    /// </summary>
    /// <param name="server">待删除的已知服务器</param>
    /// <returns>可删除则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool CanRemoveKnown(KnownServer? server) => !IsBusy && server != null;

    /// <summary>
    /// 启动已知服务器命令
    /// </summary>
    /// <param name="server">待启动的已知服务器</param>
    /// <returns>表示异步操作的任务</returns>
    /// <remarks>
    /// 触发条件：用户点击已知服务器列表的启动按钮。
    /// 副作用：构造 <see cref="ServerInstance"/> 并调用
    /// <see cref="IServerManagerService.StartServer"/> 启动进程，
    /// 完成后触发检测刷新服务器列表。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanStartKnownServer))]
    private async Task StartKnownServerAsync(KnownServer? server)
    {
        if (IsBusy) return;
        if (server is null) return;

        using var scope = BeginOperation(ServerOperation.Starting);
        CurrentServerStatus = ServerStatus.Starting;
        OperationMessage = "🚀 正在启动服务器...";

        try
        {
            if (!File.Exists(server.ServerJarPath))
            {
                OperationMessage = $"❌ JAR 文件不存在: {server.ServerJarPath}";
                CurrentServerStatus = ServerStatus.Error;
                return;
            }

            if (!Directory.Exists(server.WorkingDirectory))
            {
                OperationMessage = $"❌ 工作目录不存在: {server.WorkingDirectory}";
                CurrentServerStatus = ServerStatus.Error;
                return;
            }

            var instance = new ServerInstance
            {
                ServerJarPath = server.ServerJarPath,
                ServerJarName = Path.GetFileName(server.ServerJarPath),
                WorkingDirectory = server.WorkingDirectory,
                JavaPath = server.JavaPath,
                InitialHeapMemoryBytes = server.InitialHeapMemoryBytes,
                MaxHeapMemoryBytes = server.MaxHeapMemoryBytes,
                JvmArguments = server.JvmArguments,
                ServerPort = server.Port
            };

            var process = await Task.Run(() => _serverManager.StartServer(instance));

            if (process != null)
            {
                // 立即注册启动时 PID → 关联 KnownServerId，保证后续检测直接标记为 isKnown=true
                _serverDetector.RegisterStartedServerPid(
                    knownServerId: server.Id,
                    jarPath: server.ServerJarPath,
                    pid: process.Id);

                OperationMessage = $"✅ 启动成功！PID: {process.Id}";
                Log.Information("🚀 已知服务器启动成功: {Name} PID={Pid}", server.Name, process.Id);
                CurrentServerStatus = ServerStatus.Running;
                server.LastSeenAt = DateTime.Now;
                _appConfigService.UpdateKnownServer(server);
                await Task.Delay(1500);
                ActiveOperation = ServerOperation.None;
                await DetectAsync();
            }
            else
            {
                OperationMessage = "❌ 启动失败";
                CurrentServerStatus = ServerStatus.Error;
                Log.Error("❌ 已知服务器启动失败: {Name}", server.Name);
            }
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 启动异常：{ex.Message}";
            CurrentServerStatus = ServerStatus.Error;
            Log.Error(ex, "💥 启动已知服务器异常");
        }
        finally
        {
            // OperationScope.Dispose 已恢复 ActiveOperation
        }
    }

    /// <summary>
    /// 确定启动已知服务器命令是否可执行
    /// </summary>
    /// <param name="server">待启动的已知服务器</param>
    /// <returns>可启动则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool CanStartKnownServer(KnownServer? server)
    {
        if (server is null || IsBusy) return false;
        if (string.IsNullOrEmpty(server.ServerJarPath)) return false;
        return true;
    }

    /// <summary>
    /// 选中已知服务器变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新选中的已知服务器</param>
    /// <remarks>
    /// 刷新相关命令的 CanExecute 状态、更新服务器状态显示，
    /// 并将已知服务器的 JVM 参数同步至编辑器。
    /// </remarks>
    partial void OnSelectedKnownServerChanged(KnownServer? value)
    {
        StartKnownServerCommand.NotifyCanExecuteChanged();
        RemoveKnownServerCommand.NotifyCanExecuteChanged();
        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();
        RefreshCurrentStatus();

        // Pattern6 修复：切换 KnownServer（或清空）时，JVM 参数/内存必须**完全同步**。
        // 之前两个严重漏洞：
        //   ① 当 value.JvmArguments.Count == 0（服务器没存参数）时，不执行 SelectedArguments.Clear
        //      → 上一台服务器保存的 Aikar/G1/ZGC 参数残留到这台上，保存时把旧参数串进来
        //   ② 当 value == null（KnownServer 被删除、或切换到纯运行实例）时，整个 if 块都不进
        //      → SelectedArguments / InitialMemory / MaxMemory 还是旧 KnownServer 的值，
        //        启动新服务器时把旧参数也拼到启动命令里了
        if (value == null)
        {
            InitialMemory = string.Empty;
            MaxMemory = string.Empty;
            SelectedArguments.Clear();
            return;
        }

        InitialMemory = value.InitialHeapMemoryBytes > 0
            ? FormatMemory(value.InitialHeapMemoryBytes)
            : string.Empty;
        MaxMemory = value.MaxHeapMemoryBytes > 0
            ? FormatMemory(value.MaxHeapMemoryBytes)
            : string.Empty;

        SelectedArguments.Clear();
        foreach (var arg in value.JvmArguments)
        {
            // -Xms/-Xmx 由 InitialMemory/MaxMemory 两个独立字段管理，
            // 再放进 SelectedArguments 会导致 BuildCurrentJvmArguments 重复/冲突。
            if (arg.StartsWith("-Xms") || arg.StartsWith("-Xmx")) continue;
            SelectedArguments.Add(arg);
        }
    }

    /// <summary>
    /// 复制启动命令至剪贴板命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击复制启动命令按钮。
    /// 副作用：将 <see cref="StartupCommandPreview"/> 写入系统剪贴板。
    /// </remarks>
    [RelayCommand]
    private void CopyStartupCommand()
    {
        try
        {
            if (!string.IsNullOrEmpty(StartupCommandPreview))
            {
                Clipboard.SetText(StartupCommandPreview);
                OperationMessage = "📋 启动命令已复制到剪贴板";
                Log.Debug("📋 启动命令已复制");
            }
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 复制失败: {ex.Message}";
            Log.Error(ex, "💥 复制启动命令异常");
        }
    }

    /// <summary>
    /// 从应用配置加载已知服务器列表 —— 纯内存快照 + 整体替换
    /// </summary>
    private void LoadKnownServers()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => LoadKnownServers(),
                System.Windows.Threading.DispatcherPriority.DataBind);
            return;
        }

        var fresh = _appConfigService?.GetAllKnownServers()?.Where(s => s is not null).ToList() ?? [];

        // 快速路径：引用完全相等（AppConfigService 没改列表结构，只原地改字段）→ 只发 PropertyChanged
        // 注意：新设计里即使引用全相等，因为是纯 List<T>（不是 ObservableCollection），
        // ItemsControl 不会自动重渲染。所以必须再走一遍快照 → 过滤。
        _knownSnapshot = fresh;

        // 走统一过滤链路（SearchKeyword 非空时自动 Where）
        ReapplyFilter();

        OnPropertyChanged(nameof(HasKnownServers));
        OnPropertyChanged(nameof(KnownServers));
    }

    /// <summary>
    /// 提取 JVM 参数的基名（不含值部分）
    /// </summary>
    /// <param name="argument">完整的 JVM 参数</param>
    /// <returns>参数基名字符串</returns>
    private static string GetArgumentBaseName(string argument)
    {
        if (string.IsNullOrEmpty(argument)) return argument;

        if (argument.StartsWith("-XX:+") || argument.StartsWith("-XX:-"))
            return argument.Substring(0, argument.IndexOf(' ', StringComparison.Ordinal) > 0
                ? argument.IndexOf(' ')
                : argument.Length);

        var eqIdx = argument.IndexOf('=');
        if (eqIdx > 0)
            return argument.Substring(0, eqIdx + 1);

        if (argument.StartsWith("-Xms") || argument.StartsWith("-Xmx") ||
            argument.StartsWith("-Xss") || argument.StartsWith("-Xmn"))
            return argument.Substring(0, 4);

        if (argument.StartsWith("-D"))
        {
            var spaceIdx = argument.IndexOf(' ');
            if (spaceIdx > 0) return argument.Substring(0, spaceIdx);
            return argument;
        }

        return argument;
    }

    /// <summary>
    /// 根据参数定义构建完整的 JVM 参数字符串
    /// </summary>
    /// <param name="arg">参数定义</param>
    /// <returns>完整参数字符串</returns>
    private static string BuildFullArgument(JvmArgumentDefinition arg)
    {
        if (arg.ValueType == ArgumentValueType.BooleanFlag)
            return arg.Flag;
        if (!string.IsNullOrEmpty(arg.DefaultValue) && arg.Flag.EndsWith('='))
            return arg.Flag + arg.DefaultValue;
        if (!string.IsNullOrEmpty(arg.DefaultValue) && !arg.Flag.Contains('='))
            return arg.Flag + arg.DefaultValue;
        return arg.Flag;
    }

    /// <summary>
    /// 从完整 JVM 参数中提取参数值
    /// </summary>
    /// <param name="argument">完整的 JVM 参数</param>
    /// <returns>参数值字符串</returns>
    private static string ExtractArgumentValue(string argument)
    {
        if (string.IsNullOrEmpty(argument)) return string.Empty;

        var eqIdx = argument.IndexOf('=');
        if (eqIdx > 0 && eqIdx < argument.Length - 1)
            return argument.Substring(eqIdx + 1);

        if (argument.StartsWith("-Xms") || argument.StartsWith("-Xmx") ||
            argument.StartsWith("-Xss") || argument.StartsWith("-Xmn"))
            return argument.Substring(4);

        return string.Empty;
    }

    /// <summary>
    /// 根据基名和值构建完整的 JVM 参数
    /// </summary>
    /// <returns>完整参数字符串</returns>
    private static string BuildArgumentFromValue(string baseName, string value)
    {
        if (string.IsNullOrEmpty(baseName)) return baseName;
        if (baseName.StartsWith("-XX:+") || baseName.StartsWith("-XX:-"))
            return baseName;
        if (baseName.EndsWith('='))
            return baseName + value;
        if (baseName.StartsWith("-Xms") || baseName.StartsWith("-Xmx") ||
            baseName.StartsWith("-Xss") || baseName.StartsWith("-Xmn"))
            return baseName + value;
        return baseName + "=" + value;
    }

    /// <summary>
    /// 构建当前完整的 JVM 参数列表
    /// </summary>
    /// <returns>JVM 参数列表</returns>
    /// <summary>
    /// 构建当前完整的 JVM 参数列表。
    /// </summary>
    /// <remarks>
    /// 内存参数 (-Xms/-Xmx) 永远放在最前面，来源是 <see cref="InitialMemory"/> / <see cref="MaxMemory"/>
    /// 这两个独立编辑字段，**不**来自 SelectedArguments，避免重复/二义覆盖。
    /// SelectedArguments 中若存在 -Xms/-Xmx 残留（用户手动加或导入启动脚本时带来）会被安全跳过；
    /// 其他参数按「参数基名」去重，防止同一个 key 出现多次导致 JVM 警告。
    /// </remarks>
    /// <returns>JVM 参数列表</returns>
    private List<string> BuildCurrentJvmArguments()
    {
        var args = new List<string>
        {
            $"-Xms{InitialMemory}",
            $"-Xmx{MaxMemory}"
        };

        // 基名去重：避免同一个参数（如 -XX:MaxGCPauseMillis=）在列表里出现多次。
        // 保留第一次出现的实例（最靠近 UI 里用户选的顺序）。
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arg in SelectedArguments)
        {
            if (string.IsNullOrWhiteSpace(arg)) continue;

            // Pattern6 修复：SelectedArguments 中若意外混入 -Xms/-Xmx，跳过，避免与前两行冲突
            // （JVM 对重复参数的规则是「最后一个生效」，会导致 InitialMemory/MaxMemory 被 SelectedArguments 覆盖，产生不可预期行为）
            if (arg.StartsWith("-Xms") || arg.StartsWith("-Xmx"))
                continue;

            var baseName = GetArgumentBaseName(arg);
            if (seen.Add(baseName))
                args.Add(arg);
        }
        return args;
    }

    /// <summary>
    /// 根据参数列表判定 GC 类型
    /// </summary>
    /// <param name="args">JVM 参数列表</param>
    /// <returns>GC 类型名称</returns>
    private static string DetermineGcType(List<string> args)
    {
        if (args.Any(a => a.Contains("UseZGC"))) return "ZGC";
        if (args.Any(a => a.Contains("UseG1GC"))) return "G1GC";
        if (args.Any(a => a.Contains("UseShenandoahGC"))) return "Shenandoah";
        return "G1GC";
    }

    private static long ParseMemorySize(string sizeStr)
    {
        if (string.IsNullOrWhiteSpace(sizeStr)) return 0;
        sizeStr = sizeStr.Trim().ToUpperInvariant();
        long multiplier = 1;
        if (sizeStr.EndsWith("G"))
        {
            multiplier = 1L << 30;
            sizeStr = sizeStr.TrimEnd('G');
        }
        else if (sizeStr.EndsWith("M"))
        {
            multiplier = 1L << 20;
            sizeStr = sizeStr.TrimEnd('M');
        }
        else if (sizeStr.EndsWith("K"))
        {
            multiplier = 1L << 10;
            sizeStr = sizeStr.TrimEnd('K');
        }
        return long.TryParse(sizeStr, out var value) ? value * multiplier : 0;
    }

    private static string FormatMemory(long bytes)
    {
        if (bytes >= 1L << 30) return $"{bytes >> 30}G";
        if (bytes >= 1L << 20) return $"{bytes >> 20}M";
        if (bytes >= 1L << 10) return $"{bytes >> 10}K";
        return $"{bytes}";
    }

    /// <summary>
    /// 已选 JVM 参数集合变更回调 —— 通知过滤列表与启动命令预览刷新
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">集合变更事件参数</param>
    /// <remarks>命名方法订阅，Dispose 时可精确取消。</remarks>
    private void OnSelectedArgumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(FilteredArguments));
        OnPropertyChanged(nameof(StartupCommandPreview));
    }

    /// <summary>
    /// 释放服务器检测视图模型占用的所有资源
    /// </summary>
    /// <remarks>
    /// 取消自动检测事件订阅、停止后台检测循环、解除集合变更订阅。
    /// 幂等设计：重复调用安全。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Log.Information("🧹 ServerDetectionViewModel 释放资源中...");

        _serverDetector.DetectionCompleted -= OnAutoDetectCompleted;
        StopAutoDetect();
        SelectedArguments.CollectionChanged -= OnSelectedArgumentsChanged;

        GC.SuppressFinalize(this);
        Log.Information("✅ ServerDetectionViewModel 资源释放完成");
    }
}
