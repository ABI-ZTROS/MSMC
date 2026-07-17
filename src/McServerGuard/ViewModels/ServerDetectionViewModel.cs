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
public partial class ServerDetectionViewModel : ObservableObject
{
    /// <summary>服务器检测服务</summary>
    private readonly IServerDetector _serverDetector;
    /// <summary>应用配置服务</summary>
    private readonly IAppConfigService _appConfigService;
    /// <summary>服务器管理服务</summary>
    private readonly IServerManagerService _serverManager;
    /// <summary>服务器导入服务</summary>
    private readonly IServerImporterService _serverImporter;
    /// <summary>运行中服务器内部集合（作为 CollectionView 的 Source）</summary>
    private readonly ObservableCollection<ServerInstance> _runningServersInternal;

    /// <summary>
    /// 初始化服务器检测视图模型的新实例
    /// </summary>
    /// <param name="serverDetector">服务器检测服务</param>
    /// <param name="appConfigService">应用配置服务</param>
    /// <param name="serverManager">服务器管理服务</param>
    /// <param name="serverImporter">服务器导入服务</param>
    /// <remarks>
    /// 完成 JVM 参数初始化、服务器列表 CollectionView 构建、已知服务器加载、
    /// 自动检测事件订阅以及自动检测循环启动。
    /// </remarks>
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

        SelectedArguments.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(FilteredArguments));
            OnPropertyChanged(nameof(StartupCommandPreview));
        };

        foreach (var arg in JvmArgumentConstants.GetRecommendedArguments())
        {
            SelectedArguments.Add(BuildFullArgument(arg));
        }

        var runningSource = new ObservableCollection<ServerInstance>();
        FilteredRunningServers = new CollectionViewSource { Source = runningSource }.View;
        FilteredRunningServers.Filter = obj => MatchesSearch(obj, true);
        FilteredKnownServers = new CollectionViewSource { Source = KnownServers }.View;
        FilteredKnownServers.Filter = obj => MatchesSearch(obj, false);

        _runningServersInternal = runningSource;

        LoadKnownServers();

        _serverDetector.DetectionCompleted += OnAutoDetectCompleted;

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

        if (SelectedServer == null ||
            !result.Servers.Any(s => s.ServerJarPath == SelectedServer.ServerJarPath))
        {
            SelectedServer = result.Servers.Count > 0 ? result.Servers[0] : null;
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

    /// <summary>已知服务器集合</summary>
    public ObservableCollection<KnownServer> KnownServers { get; } = [];

    /// <summary>获取一个值，指示已知服务器集合是否非空</summary>
    public bool HasKnownServers => KnownServers.Count > 0;

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

    /// <summary>运行中服务器的过滤视图</summary>
    public ICollectionView FilteredRunningServers { get; }
    /// <summary>已知服务器的过滤视图</summary>
    public ICollectionView FilteredKnownServers { get; }

    /// <summary>
    /// 搜索关键字变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的搜索关键字</param>
    /// <remarks>触发运行中服务器与已知服务器的 CollectionView 重新过滤。</remarks>
    partial void OnSearchKeywordChanged(string value)
    {
        FilteredRunningServers.Refresh();
        FilteredKnownServers.Refresh();
    }

    /// <summary>
    /// 判定对象是否匹配当前搜索关键字
    /// </summary>
    /// <param name="obj">待判定对象</param>
    /// <param name="isRunning">是否为运行中服务器</param>
    /// <returns>匹配则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool MatchesSearch(object obj, bool isRunning)
    {
        if (obj is null) return false;
        if (string.IsNullOrWhiteSpace(SearchKeyword))
            return true;

        var keyword = SearchKeyword.Trim();
        if (isRunning && obj is ServerInstance si)
        {
            return (si.ServerJarName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (si.WorkingDirectory?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (si.DisplayName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
        }
        if (!isRunning && obj is KnownServer ks)
        {
            return (ks.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (ks.ServerJarPath?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (ks.Notes?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
        }
        return false;
    }

    /// <summary>
    /// 刷新运行中服务器的过滤视图
    /// </summary>
    /// <remarks>
    /// 采用防御性拷贝策略避免枚举时集合被修改，通过重建 ObservableCollection 内容
    /// 触发 CollectionView 自动响应。若当前不在 UI 线程，则通过 Dispatcher 封送。
    /// </remarks>
    private void RefreshFilteredRunningServers()
    {
        var snapshot = DetectionResult?.Servers is { } servers
            ? servers.Where(s => s is not null).ToList()
            : new List<ServerInstance>();

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(RebuildRunningServers));
            return;
        }
        RebuildRunningServers();

        void RebuildRunningServers()
        {
            _runningServersInternal.Clear();
            foreach (var s in snapshot)
            {
                _runningServersInternal.Add(s);
            }
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                try { FilteredRunningServers.Refresh(); }
                catch (Exception ex)
                {
                    Log.Warning(ex, "⚠️ FilteredRunningServers.Refresh 失败，已忽略");
                }
            }
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
    private static List<string> ApplyZgcFlags() =>
    [
        "-XX:+UseZGC",
        "-XX:+ZGenerational",
        "-XX:+DisableExplicitGC",
        "-XX:+AlwaysPreTouch",
        "-Dfile.encoding=UTF-8",
        "-Dlog4j2.formatMsgNoLookups=true"
    ];

    /// <summary>
    /// 应用 JVM 参数预设
    /// </summary>
    /// <param name="flags">参数列表</param>
    /// <param name="name">预设名称</param>
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

        var previousOperation = ActiveOperation;
        ActiveOperation = ServerOperation.Starting;
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
            if (ActiveOperation == ServerOperation.Starting)
            {
                ActiveOperation = previousOperation;
            }
            RefreshCurrentStatus();
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

        var previousOperation = ActiveOperation;
        ActiveOperation = ServerOperation.Stopping;
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
            if (ActiveOperation == ServerOperation.Stopping)
            {
                ActiveOperation = previousOperation;
            }
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
        catch
        {
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

        if (openFileDialog.ShowDialog() != true) return;

        var jarPath = openFileDialog.FileName;

        if (!File.Exists(jarPath))
        {
            OperationMessage = "❌ 文件不存在";
            return;
        }

        var previousOperation = ActiveOperation;
        ActiveOperation = ServerOperation.Importing;
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
            ActiveOperation = previousOperation;
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
        ActiveOperation = ServerOperation.Detecting;

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
            ActiveOperation = ServerOperation.None;
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

        if (value != null)
        {
            if (value.InitialHeapMemoryBytes > 0)
                InitialMemory = FormatMemory(value.InitialHeapMemoryBytes);
            if (value.MaxHeapMemoryBytes > 0)
                MaxMemory = FormatMemory(value.MaxHeapMemoryBytes);
            if (value.JvmArguments.Count > 0)
            {
                SelectedArguments.Clear();
                foreach (var arg in value.JvmArguments)
                    if (!arg.StartsWith("-Xms") && !arg.StartsWith("-Xmx"))
                        SelectedArguments.Add(arg);
            }
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

        var previousOperation = ActiveOperation;
        ActiveOperation = ServerOperation.SavingConfig;
        OperationMessage = "💾 正在保存配置...";

        try
        {
            var jvmArgs = BuildCurrentJvmArguments();
            var existing = _appConfigService.FindByJarPath(SelectedServer.ServerJarPath);

            if (existing != null)
            {
                // 注意：KnownServer.Name 是静态档案名，不应该用带 PID 的 DisplayName 初始化。
                // 如果原来 Name 为空，用"类型 @ 目录名"格式命名，不包含运行时 PID。
                existing.Name = string.IsNullOrEmpty(existing.Name)
                    ? $"{SelectedServer.ServerType} @ {System.IO.Path.GetFileName(SelectedServer.WorkingDirectory)}"
                    : existing.Name;
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
            }

            LoadKnownServers();
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
            ActiveOperation = previousOperation;
        }
    }

    /// <summary>
    /// 确定保存为已知服务器命令是否可执行
    /// </summary>
    /// <returns>可保存则返回 <c>true</c>，否则返回 <c>false</c></returns>
    private bool CanSaveAsKnown() => !IsBusy && SelectedServer != null;

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

        var previousOperation = ActiveOperation;
        ActiveOperation = ServerOperation.Deleting;

        try
        {
            _appConfigService.RemoveKnownServer(server.Id);
            LoadKnownServers();
            if (SelectedKnownServer == server)
                SelectedKnownServer = null;
            OperationMessage = $"🗑️ 已移除: {server.Name}";
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 删除失败: {ex.Message}";
            Log.Error(ex, "💥 删除已知服务器异常");
        }
        finally
        {
            ActiveOperation = previousOperation;
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

        var previousOperation = ActiveOperation;
        ActiveOperation = ServerOperation.Starting;

        try
        {
            if (!File.Exists(server.ServerJarPath))
            {
                OperationMessage = $"❌ JAR 文件不存在: {server.ServerJarPath}";
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
                OperationMessage = $"✅ 启动成功！PID: {process.Id}";
                server.LastSeenAt = DateTime.Now;
                _appConfigService.UpdateKnownServer(server);
                await Task.Delay(1500);
                ActiveOperation = ServerOperation.None;
                await DetectAsync();
            }
            else
            {
                OperationMessage = "❌ 启动失败";
            }
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 启动异常：{ex.Message}";
            Log.Error(ex, "💥 启动已知服务器异常");
        }
        finally
        {
            if (ActiveOperation == ServerOperation.Starting)
            {
                ActiveOperation = previousOperation;
            }
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

        if (value != null)
        {
            if (value.InitialHeapMemoryBytes > 0)
                InitialMemory = FormatMemory(value.InitialHeapMemoryBytes);
            if (value.MaxHeapMemoryBytes > 0)
                MaxMemory = FormatMemory(value.MaxHeapMemoryBytes);
            if (value.JvmArguments.Count > 0)
            {
                SelectedArguments.Clear();
                foreach (var arg in value.JvmArguments)
                    if (!arg.StartsWith("-Xms") && !arg.StartsWith("-Xmx"))
                        SelectedArguments.Add(arg);
            }
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
    /// 从应用配置加载已知服务器列表
    /// </summary>
    private void LoadKnownServers()
    {
        KnownServers.Clear();
        foreach (var server in _appConfigService.GetAllKnownServers())
        {
            KnownServers.Add(server);
        }
        OnPropertyChanged(nameof(HasKnownServers));
        FilteredKnownServers.Refresh();
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
    /// <param name="baseName">参数基名</param>
    /// <param name="value">参数值</param>
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
    private List<string> BuildCurrentJvmArguments()
    {
        var args = new List<string>
        {
            $"-Xms{InitialMemory}",
            $"-Xmx{MaxMemory}"
        };
        foreach (var arg in SelectedArguments)
        {
            if (!arg.StartsWith("-Xms") && !arg.StartsWith("-Xmx"))
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
}
