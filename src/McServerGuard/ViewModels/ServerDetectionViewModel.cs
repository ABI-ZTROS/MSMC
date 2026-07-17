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
using McServerGuard.Services.AIService;
using McServerGuard.Services.ServerDetection;
using Microsoft.Win32;
using Serilog;

namespace McServerGuard.ViewModels;

public partial class ServerDetectionViewModel : ObservableObject
{
    private readonly IServerDetector _serverDetector;
    private readonly IAppConfigService _appConfigService;
    private readonly IServerManagerService _serverManager;
    private readonly IServerImporterService _serverImporter;
    private readonly IAiSelfLearningService _aiLearning;
    private readonly ObservableCollection<ServerInstance> _runningServersInternal;

    public ServerDetectionViewModel(
        IServerDetector serverDetector,
        IAppConfigService appConfigService,
        IServerManagerService serverManager,
        IServerImporterService serverImporter,
        IAiSelfLearningService aiLearning)
    {
        Log.Information("📡 ServerDetectionViewModel 初始化");
        _serverDetector = serverDetector;
        _appConfigService = appConfigService;
        _serverManager = serverManager;
        _serverImporter = serverImporter;
        _aiLearning = aiLearning;

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

        // 🔍 初始化带过滤功能的服务器列表视图
        var runningSource = new ObservableCollection<ServerInstance>();
        FilteredRunningServers = new CollectionViewSource { Source = runningSource }.View;
        FilteredRunningServers.Filter = obj => MatchesSearch(obj, true);
        FilteredKnownServers = new CollectionViewSource { Source = KnownServers }.View;
        FilteredKnownServers.Filter = obj => MatchesSearch(obj, false);

        _runningServersInternal = runningSource;

        LoadKnownServers();

        // 📡 订阅自动检测事件 —— 后台每秒检测完会通知我们，然后刷新列表
        _serverDetector.DetectionCompleted += OnAutoDetectCompleted;

        // 🔄 启动自动检测循环
        StartAutoDetect();
    }

    /// <summary>
    /// 自动检测完成回调 —— 在后台线程触发，需要切回 UI 线程更新绑定
    /// </summary>
    private void OnAutoDetectCompleted(object? sender, DetectionResult result)
    {
        // 不在 IsBusy 时才更新，避免和手动操作冲突
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
    /// 将检测结果更新到绑定属性，触发列表刷新
    /// </summary>
    private void UpdateDetectionResult(DetectionResult result)
    {
        if (IsBusy) return;

        DetectionResult = result;

        // 保留之前的选中（如果还在）
        if (SelectedServer == null ||
            !result.Servers.Any(s => s.ServerJarPath == SelectedServer.ServerJarPath))
        {
            SelectedServer = result.Servers.Count > 0 ? result.Servers[0] : null;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 🔄 自动检测控制
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoDetectStatusText))]
    [NotifyPropertyChangedFor(nameof(AutoDetectIcon))]
    private bool _isAutoDetectEnabled;

    public string AutoDetectStatusText => IsAutoDetectEnabled ? "自动检测中" : "自动检测已暂停";

    public string AutoDetectIcon => IsAutoDetectEnabled ? "PauseSolid" : "PlaySolid";

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

    private void StopAutoDetect()
    {
        _serverDetector.StopAutoDetect();
        IsAutoDetectEnabled = false;
        Log.Information("⏹️ 自动检测已暂停");
    }

    // ═══════════════════════════════════════════════════════════════
    // 🔒 统一功能锁 —— 任何时候只能有一个操作在进行
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(BusyReasonText))]
    [NotifyPropertyChangedFor(nameof(CanShowOperation))]
    private ServerOperation _activeOperation = ServerOperation.None;

    /// <summary>是否空闲（无操作进行中）</summary>
    public bool IsIdle => ActiveOperation == ServerOperation.None;

    /// <summary>是否忙碌（任何操作进行中）</summary>
    public bool IsBusy => ActiveOperation != ServerOperation.None;

    /// <summary>忙碌原因文案（显示在 UI）</summary>
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

    public bool CanShowOperation => IsBusy;

    partial void OnActiveOperationChanged(ServerOperation value)
    {
        // 任何命令的 CanExecute 都依赖 IsBusy，统统通知刷新
        DetectCommand.NotifyCanExecuteChanged();
        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();
        SaveAsKnownServerCommand.NotifyCanExecuteChanged();
        StartKnownServerCommand.NotifyCanExecuteChanged();
        RemoveKnownServerCommand.NotifyCanExecuteChanged();
    }

    // ═══════════════════════════════════════════════════════════════
    // 🔍 检测相关属性
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private DetectionResult? _detectionResult;

    [ObservableProperty]
    private ServerInstance? _selectedServer;

    public string DetectionLog => DetectionResult is not null
        ? string.Join(Environment.NewLine, DetectionResult.LogMessages)
        : string.Empty;

    partial void OnDetectionResultChanged(DetectionResult? value)
    {
        OnPropertyChanged(nameof(DetectionLog));
        RefreshFilteredRunningServers();
        RefreshCurrentStatus();
    }

    // ═══════════════════════════════════════════════════════════════
    // 🚦 选中服务器的运行状态
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentServerStatusText))]
    [NotifyPropertyChangedFor(nameof(CurrentServerStatusBrush))]
    [NotifyPropertyChangedFor(nameof(CurrentServerStatusIcon))]
    [NotifyPropertyChangedFor(nameof(HasSelectedServer))]
    [NotifyPropertyChangedFor(nameof(SelectedServerSubtitle))]
    private ServerStatus _currentServerStatus = ServerStatus.Unknown;

    public string CurrentServerStatusText => CurrentServerStatus switch
    {
        ServerStatus.Running => $"🟢 运行中{(GetActiveServer() is { } s && s.ProcessId > 0 ? $" (PID: {s.ProcessId})" : string.Empty)}",
        ServerStatus.Starting => "🟡 启动中...",
        ServerStatus.Stopping => "🟠 停止中...",
        ServerStatus.Stopped => "⚫ 已停止",
        ServerStatus.Error => "🔴 异常",
        _ => "❓ 未知"
    };

    public Brush CurrentServerStatusBrush => CurrentServerStatus switch
    {
        ServerStatus.Running => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        ServerStatus.Starting => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
        ServerStatus.Stopping => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
        ServerStatus.Stopped => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75)),
        ServerStatus.Error => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        _ => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E))
    };

    public string CurrentServerStatusIcon => CurrentServerStatus switch
    {
        ServerStatus.Running => "CirclePlaySolid",
        ServerStatus.Starting => "SpinnerSolid",
        ServerStatus.Stopping => "CircleStopSolid",
        ServerStatus.Stopped => "CirclePauseSolid",
        ServerStatus.Error => "CircleExclamationSolid",
        _ => "CircleQuestionSolid"
    };

    public bool HasSelectedServer => SelectedServer != null || SelectedKnownServer != null;

    public string SelectedServerSubtitle => GetActiveServer() is { } active
        ? active.DisplayName
        : "未选择服务器";

    // ═══════════════════════════════════════════════════════════════
    // 📚 已知服务器
    // ═══════════════════════════════════════════════════════════════

    public ObservableCollection<KnownServer> KnownServers { get; } = [];

    public bool HasKnownServers => KnownServers.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedServer))]
    [NotifyPropertyChangedFor(nameof(SelectedServerSubtitle))]
    private KnownServer? _selectedKnownServer;

    // ═══════════════════════════════════════════════════════════════
    // 🔍 搜索过滤
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchKeyword))]
    private string _searchKeyword = string.Empty;

    public bool HasSearchKeyword => !string.IsNullOrWhiteSpace(SearchKeyword);

    public ICollectionView FilteredRunningServers { get; }
    public ICollectionView FilteredKnownServers { get; }

    partial void OnSearchKeywordChanged(string value)
    {
        FilteredRunningServers.Refresh();
        FilteredKnownServers.Refresh();
    }

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

    private void RefreshFilteredRunningServers()
    {
        // 防御性拷贝：避免在枚举时 DetectionResult.Servers 被另一线程修改
        var snapshot = DetectionResult?.Servers is { } servers
            ? servers.Where(s => s is not null).ToList()
            : new List<ServerInstance>();

        // 直接重建 ObservableCollection 内容，CollectionView 会通过 CollectionChanged
        // 自动响应，无需手动 Refresh（手动 Refresh 的 PrepareLocalArray 在并发场景下
        // 会抛 NRE）。确保在 UI 线程执行。
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
            // Refresh 仅在搜索关键字非空时才需要（触发 Filter 重新过滤）
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

    // ═══════════════════════════════════════════════════════════════
    // 📑 右侧 Tab 切换
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _selectedTabIndex;

    // ═══════════════════════════════════════════════════════════════
    // 🧠 JVM 参数编辑
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private string _initialMemory = "2G";
    [ObservableProperty] private string _maxMemory = "4G";
    [ObservableProperty] private ArgumentCategory _selectedCategory = ArgumentCategory.Memory;
    [ObservableProperty] private string _customArgument = string.Empty;
    [ObservableProperty] private string _selectedArgumentToEdit = string.Empty;
    [ObservableProperty] private string _editingArgumentValue = string.Empty;
    [ObservableProperty] private bool _isEditingArgument;

    public ObservableCollection<string> SelectedArguments { get; }
    public ObservableCollection<ArgumentCategory> AllArgumentCategories { get; }

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

    partial void OnSelectedCategoryChanged(ArgumentCategory value)
        => OnPropertyChanged(nameof(FilteredArguments));

    partial void OnInitialMemoryChanged(string value)
        => OnPropertyChanged(nameof(StartupCommandPreview));

    partial void OnMaxMemoryChanged(string value)
        => OnPropertyChanged(nameof(StartupCommandPreview));

    [RelayCommand]
    private void SelectCategory(ArgumentCategory category)
    {
        SelectedCategory = category;
    }

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

    [RelayCommand]
    private void StartEditArgument(string argument)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(argument)) return;

        SelectedArgumentToEdit = argument;
        EditingArgumentValue = ExtractArgumentValue(argument);
        IsEditingArgument = true;
    }

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

    [RelayCommand]
    private void CancelEditArgument()
    {
        IsEditingArgument = false;
        SelectedArgumentToEdit = string.Empty;
        EditingArgumentValue = string.Empty;
    }

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

    [RelayCommand]
    private void ApplyAikarPreset()
    {
        if (IsBusy) return;
        ApplyPreset(ApplyAikarFlags(), "Aikar");
    }

    [RelayCommand]
    private void ApplyG1GCPreset()
    {
        if (IsBusy) return;
        ApplyPreset(ApplyG1GCFlags(), "G1GC");
    }

    [RelayCommand]
    private void ApplyZgcPreset()
    {
        if (IsBusy) return;
        ApplyPreset(ApplyZgcFlags(), "ZGC");
    }

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

    private static List<string> ApplyG1GCFlags() =>
    [
        "-XX:+UseG1GC",
        "-XX:MaxGCPauseMillis=200",
        "-XX:+AlwaysPreTouch",
        "-XX:+DisableExplicitGC",
        "-Dfile.encoding=UTF-8",
        "-Dlog4j2.formatMsgNoLookups=true"
    ];

    private static List<string> ApplyZgcFlags() =>
    [
        "-XX:+UseZGC",
        "-XX:+ZGenerational",
        "-XX:+DisableExplicitGC",
        "-XX:+AlwaysPreTouch",
        "-Dfile.encoding=UTF-8",
        "-Dlog4j2.formatMsgNoLookups=true"
    ];

    private void ApplyPreset(List<string> flags, string name)
    {
        SelectedArguments.Clear();
        foreach (var flag in flags) SelectedArguments.Add(flag);
        Log.Information("🎯 应用 {Name} 预设参数", name);
    }

    // ═══════════════════════════════════════════════════════════════
    // 🚀 启动/停止当前选中服务器（带功能锁）
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private string _operationMessage = string.Empty;

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
                // 先解除忙碌状态，否则 DetectAsync 会被 IsBusy 拦截直接 return
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
            // ⚠️ 注意：DetectAsync 完成后 ActiveOperation 已经是 Detecting，要还原
            if (ActiveOperation == ServerOperation.Starting)
            {
                ActiveOperation = previousOperation;
            }
            RefreshCurrentStatus();
        }
    }

    private bool CanStartCurrent()
    {
        if (IsBusy) return false;
        var server = GetActiveServer();
        if (server is null) return false;
        return !_serverManager.IsServerRunning(server);
    }

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
            OperationMessage = success ? "✅ 服务器已停止" : "⚠️ 停止失败，可能需要手动关闭";

            if (success)
            {
                await Task.Delay(800);
                // 先解除忙碌状态，否则 DetectAsync 会被 IsBusy 拦截直接 return
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

    private bool CanStopCurrent()
    {
        if (IsBusy) return false;
        var server = GetActiveServer();
        if (server is null) return false;
        return _serverManager.IsServerRunning(server);
    }

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

        // 通知按钮 CanExecute 刷新
        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();
    }

    // ═══════════════════════════════════════════════════════════════
    // 📦 导入服务器（仅导入到列表，不再自动启动）
    // ═══════════════════════════════════════════════════════════════

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

            // 后台异步执行 AI 自学习
            _ = Task.Run(async () =>
            {
                try { await _aiLearning.AutoLearnFromServerAsync(server); }
                catch (Exception ex) { Log.Error(ex, "❌ AI 自学习失败: {Message}", ex.Message); }
            });

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

    private bool CanImport() => !IsBusy;

    // ═══════════════════════════════════════════════════════════════
    // 🔍 检测命令
    // ═══════════════════════════════════════════════════════════════

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
                // 保留之前的选中（如果还在）
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

    private bool CanDetect() => !IsBusy;

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

    // ═══════════════════════════════════════════════════════════════
    // 💾 保存为已知服务器
    // ═══════════════════════════════════════════════════════════════

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
                existing.Name = string.IsNullOrEmpty(existing.Name)
                    ? SelectedServer.DisplayName
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
                    Name = SelectedServer.DisplayName,
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

    private bool CanSaveAsKnown() => !IsBusy && SelectedServer != null;

    // ═══════════════════════════════════════════════════════════════
    // 🗑️ 已知服务器：删除 + 启动（带功能锁）
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand(CanExecute = nameof(CanRemoveKnown))]
    private void RemoveKnownServer(KnownServer? server)
    {
        if (IsBusy) return;
        if (server is null) return;

        // 🚨 二次校验：正在运行的服务器不允许删除
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

    private bool CanRemoveKnown(KnownServer? server) => !IsBusy && server != null;

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
                // 先解除忙碌状态，否则 DetectAsync 会被 IsBusy 拦截直接 return
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

    private bool CanStartKnownServer(KnownServer? server)
    {
        if (server is null || IsBusy) return false;
        if (string.IsNullOrEmpty(server.ServerJarPath)) return false;
        return true;
    }

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

    // ═══════════════════════════════════════════════════════════════
    // 📋 复制启动命令
    // ═══════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════
    // 🔧 辅助方法
    // ═══════════════════════════════════════════════════════════════

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
