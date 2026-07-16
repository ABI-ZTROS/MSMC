using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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

        LoadKnownServers();
    }

    // ─── 检测相关属性 ─────────────────────────────────────────────

    [ObservableProperty] private DetectionResult? _detectionResult;

    [ObservableProperty] private ServerInstance? _selectedServer;

    [ObservableProperty] private bool _isDetecting;

    public string DetectionLog => DetectionResult is not null
        ? string.Join(Environment.NewLine, DetectionResult.LogMessages)
        : string.Empty;

    // ─── 已知服务器 ────────────────────────────────────────────────

    public ObservableCollection<KnownServer> KnownServers { get; } = [];

    public bool HasKnownServers => KnownServers.Count > 0;

    [ObservableProperty] private KnownServer? _selectedKnownServer;

    [ObservableProperty] private bool _isStartingKnown;

    [ObservableProperty] private string _startStatusMessage = string.Empty;

    private void LoadKnownServers()
    {
        KnownServers.Clear();
        foreach (var server in _appConfigService.GetAllKnownServers())
        {
            KnownServers.Add(server);
        }
        OnPropertyChanged(nameof(HasKnownServers));
    }

    // ─── JVM 参数编辑 ──────────────────────────────────────────────

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
        if (string.IsNullOrWhiteSpace(argument)) return;

        SelectedArgumentToEdit = argument;
        EditingArgumentValue = ExtractArgumentValue(argument);
        IsEditingArgument = true;
    }

    [RelayCommand]
    private void SaveEditArgument()
    {
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
        SelectedArguments.Clear();
        var aikarFlags = new List<string>
        {
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
        };
        foreach (var flag in aikarFlags) SelectedArguments.Add(flag);
        Log.Information("🎯 应用 Aikar 预设参数");
    }

    [RelayCommand]
    private void ApplyG1GCPreset()
    {
        SelectedArguments.Clear();
        var g1gcFlags = new List<string>
        {
            "-XX:+UseG1GC",
            "-XX:MaxGCPauseMillis=200",
            "-XX:+AlwaysPreTouch",
            "-XX:+DisableExplicitGC",
            "-Dfile.encoding=UTF-8",
            "-Dlog4j2.formatMsgNoLookups=true"
        };
        foreach (var flag in g1gcFlags) SelectedArguments.Add(flag);
        Log.Information("🎯 应用 G1GC 预设参数");
    }

    [RelayCommand]
    private void ApplyZgcPreset()
    {
        SelectedArguments.Clear();
        var zgcFlags = new List<string>
        {
            "-XX:+UseZGC",
            "-XX:+ZGenerational",
            "-XX:+DisableExplicitGC",
            "-XX:+AlwaysPreTouch",
            "-Dfile.encoding=UTF-8",
            "-Dlog4j2.formatMsgNoLookups=true"
        };
        foreach (var flag in zgcFlags) SelectedArguments.Add(flag);
        Log.Information("🎯 应用 ZGC 预设参数");
    }

    // ─── 启动/停止当前选中服务器 ────────────────────────────────────

    [ObservableProperty] private bool _isStarting;
    [ObservableProperty] private bool _isStopping;
    [ObservableProperty] private string _operationMessage = string.Empty;

    [RelayCommand(CanExecute = nameof(CanStartCurrent))]
    private async Task StartCurrentServerAsync()
    {
        var server = GetActiveServer();
        if (server is null) return;

        IsStarting = true;
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

                SaveCurrentToKnown(jvmArgs);

                await Task.Delay(1500);
                await DetectAsync();
            }
            else
            {
                OperationMessage = "❌ 服务器启动失败";
                Log.Error("❌ 服务器启动失败");
            }
        }
        catch (Exception ex)
        {
            OperationMessage = $"❌ 启动异常: {ex.Message}";
            Log.Error(ex, "💥 启动服务器异常");
        }
        finally
        {
            IsStarting = false;
        }
    }

    private bool CanStartCurrent()
    {
        var server = GetActiveServer();
        if (server is null || IsStarting || IsStopping) return false;
        return !_serverManager.IsServerRunning(server);
    }

    [RelayCommand(CanExecute = nameof(CanStopCurrent))]
    private async Task StopCurrentServerAsync()
    {
        var server = GetActiveServer();
        if (server is null) return;

        IsStopping = true;
        OperationMessage = "🛑 正在停止服务器...";

        try
        {
            var success = await Task.Run(() => _serverManager.StopServer(server));
            OperationMessage = success ? "✅ 服务器已停止" : "⚠️ 停止失败，可能需要手动关闭";

            if (success)
            {
                await Task.Delay(800);
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
            IsStopping = false;
        }
    }

    private bool CanStopCurrent()
    {
        var server = GetActiveServer();
        if (server is null || IsStarting || IsStopping) return false;
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

    private void SaveCurrentToKnown(List<string> jvmArgs)
    {
        var server = GetActiveServer();
        if (server is null) return;

        var existing = _appConfigService.FindByJarPath(server.ServerJarPath);
        if (existing != null)
        {
            existing.WorkingDirectory = server.WorkingDirectory;
            existing.JavaPath = server.JavaPath;
            existing.InitialHeapMemoryBytes = ParseMemorySize(InitialMemory);
            existing.MaxHeapMemoryBytes = ParseMemorySize(MaxMemory);
            existing.JvmArguments = jvmArgs;
            existing.Port = server.ServerPort;
            existing.LastSeenAt = DateTime.Now;
            _appConfigService.UpdateKnownServer(existing);
        }
        else
        {
            var known = new KnownServer
            {
                Name = string.IsNullOrEmpty(server.ServerJarName) ? "未命名服务器" : server.ServerJarName,
                ServerJarPath = server.ServerJarPath,
                WorkingDirectory = server.WorkingDirectory,
                JavaPath = server.JavaPath,
                InitialHeapMemoryBytes = ParseMemorySize(InitialMemory),
                MaxHeapMemoryBytes = ParseMemorySize(MaxMemory),
                JvmArguments = jvmArgs,
                Port = server.ServerPort,
                AddedAt = DateTime.Now,
                LastSeenAt = DateTime.Now
            };
            _appConfigService.AddKnownServer(known);
        }

        LoadKnownServers();
    }

    // ─── 导入服务器（选 JAR） ─────────────────────────────────────

    [RelayCommand]
    private void BrowseAndImportServer()
    {
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
            OperationMessage = $"✅ 服务器已添加到列表: {serverType}";
        }

        _ = Task.Run(async () =>
        {
            try { await _aiLearning.AutoLearnFromServerAsync(server); }
            catch (Exception ex) { Log.Error(ex, "❌ AI 自学习失败: {Message}", ex.Message); }
        });

        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();
    }

    // ─── 检测命令 ──────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanDetect))]
    private async Task DetectAsync()
    {
        Log.Information("🔍 开始扫描服务器进程...");
        IsDetecting = true;
        DetectionResult = null;

        try
        {
            DetectionResult = await _serverDetector.DetectAllAsync();

            if (DetectionResult.Servers.Count > 0)
                SelectedServer = DetectionResult.Servers[0];

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
            IsDetecting = false;
            StartCurrentServerCommand.NotifyCanExecuteChanged();
            StopCurrentServerCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDetect() => !IsDetecting;

    partial void OnDetectionResultChanged(DetectionResult? value)
        => OnPropertyChanged(nameof(DetectionLog));

    partial void OnSelectedServerChanged(ServerInstance? value)
    {
        SaveAsKnownServerCommand.NotifyCanExecuteChanged();
        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();

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

    // ─── 已知服务器命令 ────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSaveAsKnown))]
    private void SaveAsKnownServer()
    {
        if (SelectedServer is null) return;

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
        Log.Information("💾 服务器已保存为已知服务器: {Name}", SelectedServer.DisplayName);
    }

    private bool CanSaveAsKnown() => SelectedServer != null;

    [RelayCommand(CanExecute = nameof(CanRemoveKnown))]
    private void RemoveKnownServer(KnownServer? server)
    {
        if (server is null) return;
        _appConfigService.RemoveKnownServer(server.Id);
        LoadKnownServers();
        if (SelectedKnownServer == server)
            SelectedKnownServer = null;
    }

    private bool CanRemoveKnown(KnownServer? server) => server != null;

    [RelayCommand(CanExecute = nameof(CanStartKnownServer))]
    private async Task StartKnownServerAsync(KnownServer? server)
    {
        if (server is null) return;

        IsStartingKnown = true;
        StartStatusMessage = "正在启动...";

        try
        {
            if (!File.Exists(server.ServerJarPath))
            {
                StartStatusMessage = $"❌ JAR 文件不存在";
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
                StartStatusMessage = "✅ 启动成功！";
                server.LastSeenAt = DateTime.Now;
                _appConfigService.UpdateKnownServer(server);
                await Task.Delay(1500);
                await DetectAsync();
            }
            else
            {
                StartStatusMessage = "❌ 启动失败";
            }
        }
        catch (Exception ex)
        {
            StartStatusMessage = $"❌ 启动异常：{ex.Message}";
            Log.Error(ex, "💥 启动已知服务器异常");
        }
        finally
        {
            IsStartingKnown = false;
        }
    }

    private bool CanStartKnownServer(KnownServer? server)
    {
        if (server is null || IsStartingKnown) return false;
        if (string.IsNullOrEmpty(server.ServerJarPath)) return false;
        return true;
    }

    partial void OnSelectedKnownServerChanged(KnownServer? value)
    {
        StartKnownServerCommand.NotifyCanExecuteChanged();
        RemoveKnownServerCommand.NotifyCanExecuteChanged();
        StartCurrentServerCommand.NotifyCanExecuteChanged();
        StopCurrentServerCommand.NotifyCanExecuteChanged();

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

    partial void OnIsStartingKnownChanged(bool value)
        => StartKnownServerCommand.NotifyCanExecuteChanged();

    // ─── 辅助方法 ──────────────────────────────────────────────────

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
