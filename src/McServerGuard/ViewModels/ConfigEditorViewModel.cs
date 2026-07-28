// -----------------------------------------------------------------------------
// 文件名: ConfigEditorViewModel.cs
// 命名空间: McServerGuard.ViewModels
// 功能描述: 配置编辑器视图模型 —— 基于 CommunityToolkit.Mvvm 源生成器的 MVVM 绑定层，
//           承担 Minecraft 服务器配置文件的加载、编辑、验证与持久化职责
// 依赖组件: CommunityToolkit.Mvvm (ObservableProperty/RelayCommand),
//           System.Collections.ObjectModel, Serilog
// 设计模式: MVVM 模式, 命令模式, 防抖模式 (分组更新计时器), 观察者 (PropertyChanged)
// -----------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Constants;
using McServerGuard.Services;
using McServerGuard.Services.ConfigManagement;
using McServerGuard.Services.ServerDetection;
using Serilog;

namespace McServerGuard.ViewModels;

/// <summary>
/// 配置文件树节点模型
/// </summary>
public sealed class ConfigFileItem
{
    /// <summary>文件名</summary>
    public string FileName { get; init; }
    /// <summary>完整路径</summary>
    public string FullPath { get; init; }
    /// <summary>相对路径</summary>
    public string RelativePath { get; init; }
    /// <summary>是否为目录</summary>
    public bool IsDirectory { get; init; }
    /// <summary>子节点集合</summary>
    public List<ConfigFileItem> Children { get; init; } = [];

    public ConfigFileItem(string fileName, string fullPath, string relativePath, bool isDirectory = false)
    {
        FileName = fileName;
        FullPath = fullPath;
        RelativePath = relativePath;
        IsDirectory = isDirectory;
    }
}

/// <summary>
/// 配置项分组模型 —— 用于 UI Expander 分组展示
/// </summary>
public sealed class ConfigEntryGroup
{
    /// <summary>分组键</summary>
    public string Key { get; init; }
    /// <summary>分组内的配置项列表</summary>
    public List<ServerConfigEntry> Items { get; init; }

    public ConfigEntryGroup(string key, List<ServerConfigEntry> items)
    {
        Key = key;
        Items = items;
    }
}

/// <summary>
/// 配置编辑器视图模型 —— 配置编辑页面的数据上下文
/// </summary>
/// <remarks>
/// 本类作为配置编辑页的 MVVM 绑定层，负责：配置文件递归扫描与目录树构建、
/// 配置条目加载与分组展示、值编辑与实时验证、脏数据追踪与持久化。
/// 支持从运行中服务器、已知服务器及手动选择目录三种数据源切换。
/// </remarks>
public partial class ConfigEditorViewModel : ObservableObject, IDisposable
{
    #region 常量

    /// <summary>跳过的目录名黑名单（任何 depth 都跳过）</summary>
    private static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mods", "world", "world_nether", "world_the_end",
        "logs", "cache", "libraries", "versions", "assets",
        "crash-reports", "timings"
    };

    /// <summary>单台服务器最大扫描配置文件数（防卡死硬上限）</summary>
    private const int MaxFilesPerServer = 500;

    #endregion

    #region 字段

    /// <summary>配置管理服务</summary>
    private readonly IConfigManager _configManager;
    /// <summary>服务器检测服务（可选）</summary>
    private readonly IServerDetector? _serverDetector;
    /// <summary>应用配置服务（可选）</summary>
    private readonly IAppConfigService? _appConfigService;

    /// <summary>原始配置快照 —— 用于重置变更与脏数据比对</summary>
    private Dictionary<string, string> _originalConfig = new();

    /// <summary>当前编辑的配置文件完整路径</summary>
    private string _currentFilePath = string.Empty;

    /// <summary>加载取消令牌源 —— 防止快速切换文件时的竞态</summary>
    private CancellationTokenSource? _loadCts;

    /// <summary>最后一次配置加载任务引用</summary>
    private Task? _lastLoadTask;

    /// <summary>分组更新防抖计时器</summary>
    private System.Timers.Timer? _groupUpdateTimer;

    /// <summary>编辑历史栈 —— 记录每次值变更前的条目引用与原始值，支持逐步撤销</summary>
    private readonly Stack<(ServerConfigEntry Entry, string PreviousValue)> _undoStack = new();

    /// <summary>已修改条目计数器 —— O(1) 替代 O(n) 的 ConfigEntries.Any(...) 扫描</summary>
    private int _modifiedCount;

    /// <summary>撤销操作进行中标志 —— 防止撤销恢复值时再次触发压栈</summary>
    private bool _isUndoing;

    /// <summary>指示当前实例是否已释放，防止重复 Dispose 导致资源二次释放</summary>
    private bool _disposed;

    /// <summary>扫描中的版本号 —— 防止旧的长扫描覆盖新结果</summary>
    private int _scanVersion;

    /// <summary>当前是否正在扫目录 —— 防止 OnServerChanged + 桥接层重复两次扫描</summary>
    private bool _scanning;

    #endregion

    /// <summary>
    /// 初始化配置编辑器视图模型的新实例（最小依赖版本）
    /// </summary>
    /// <param name="configManager">配置管理服务</param>
    public ConfigEditorViewModel(IConfigManager configManager)
    {
        Log.Information("⚙️ ConfigEditorViewModel 初始化");
        _configManager = configManager;

        _groupUpdateTimer = new System.Timers.Timer(20);
        _groupUpdateTimer.AutoReset = false;
        _groupUpdateTimer.Elapsed += OnGroupUpdateTimerElapsed;

        ConfigEntries.CollectionChanged += OnConfigEntriesChanged;
    }

    /// <summary>
    /// 初始化配置编辑器视图模型的新实例（完整依赖版本）
    /// </summary>
    /// <param name="configManager">配置管理服务</param>
    /// <param name="serverDetector">服务器检测服务</param>
    /// <param name="appConfigService">应用配置服务</param>
    public ConfigEditorViewModel(
        IConfigManager configManager,
        IServerDetector serverDetector,
        IAppConfigService appConfigService) : this(configManager)
    {
        _serverDetector = serverDetector;
        _appConfigService = appConfigService;

        _ = RefreshServerListAsync();
    }

    #region 绑定属性

    /// <summary>
    /// 可用服务器列表（运行中服务器与已知服务器的并集）
    /// </summary>
    [ObservableProperty]
    private List<ServerInstance> _availableServers = [];

    /// <summary>
    /// 当前选中的服务器名称
    /// </summary>
    [ObservableProperty]
    private string? _selectedServerName;

    /// <summary>
    /// 当前服务器的工作目录路径
    /// </summary>
    [ObservableProperty]
    private string _serverWorkingDirectory = string.Empty;

    /// <summary>
    /// 配置文件目录树结构
    /// </summary>
    [ObservableProperty]
    private List<ConfigFileItem> _configFileTree = [];

    /// <summary>
    /// 当前操作的服务器实例
    /// </summary>
    /// <remarks>设置后自动触发配置文件列表的递归扫描。</remarks>
    [ObservableProperty]
    private ServerInstance? _server;

    /// <summary>
    /// 配置文件路径列表（扁平结构，仅包含文件）
    /// </summary>
    [ObservableProperty]
    private List<string> _configFiles = [];

    /// <summary>
    /// 当前选中的配置文件相对路径
    /// </summary>
    /// <remarks>选中后自动异步加载该文件的配置内容。</remarks>
    [ObservableProperty]
    private string? _selectedConfigFile;

    /// <summary>
    /// 当前选中配置文件的纯文件名（不含路径），用于顶部标题显示
    /// </summary>
    public string? SelectedConfigFileName => string.IsNullOrEmpty(SelectedConfigFile)
        ? SelectedConfigFile
        : System.IO.Path.GetFileName(SelectedConfigFile);

    /// <summary>
    /// 当前配置文件的条目集合
    /// </summary>
    /// <remarks>
    /// 使用 ObservableCollection 支持增量 UI 更新。每个条目包含 Key、Value、Descriptor 等信息，
    /// 由 <see cref="IConfigManager.GetDescriptor"/> 提供中文说明与验证约束。
    /// </remarks>
    [ObservableProperty]
    private ObservableCollection<ServerConfigEntry> _configEntries = [];

    /// <summary>
    /// 按分类分组的配置项集合
    /// </summary>
    [ObservableProperty]
    private List<ConfigEntryGroup> _groupedConfigEntries = [];

    /// <summary>配置文件数量统计文本</summary>
    public string ConfigFileCountText => ConfigFiles.Count > 0
        ? $"共 {ConfigFiles.Count} 个配置文件"
        : "未找到配置文件";

    /// <summary>获取一个值，指示当前是否存在有效的服务器工作目录</summary>
    public bool HasServerDirectory => !string.IsNullOrEmpty(ServerWorkingDirectory) && Directory.Exists(ServerWorkingDirectory);

    /// <summary>获取或设置一个值，指示当前是否存在未保存的变更</summary>
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>保存操作的状态消息（成功/失败提示）</summary>
    [ObservableProperty]
    private string? _saveStatusMessage;

    /// <summary>指示保存状态消息是否为错误类型</summary>
    [ObservableProperty]
    private bool _isSaveError;

    /// <summary>保存错误类型，null 表示无错误</summary>
    [ObservableProperty]
    private string? _saveErrorType;

    /// <summary>是否正在加载配置文件</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>配置加载进度百分比（0-100）</summary>
    [ObservableProperty]
    private int _loadProgress;

    #endregion

    #region 服务器列表与联动

    /// <summary>
    /// 刷新可用服务器列表命令
    /// </summary>
    [RelayCommand]
    public async Task RefreshServerListAsync()
    {
        Log.Information("🔄 刷新配置编辑器的服务器列表...");
        var servers = new List<ServerInstance>();

        try
        {
            if (_serverDetector != null)
            {
                var result = await _serverDetector.DetectAllAsync();
                foreach (var s in result.Servers)
                {
                    if (!string.IsNullOrEmpty(s.WorkingDirectory) && Directory.Exists(s.WorkingDirectory))
                        servers.Add(s);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取运行中服务器列表失败");
        }

        if (_appConfigService != null)
        {
            foreach (var ks in _appConfigService.GetAllKnownServers())
            {
                if (!string.IsNullOrEmpty(ks.WorkingDirectory) && Directory.Exists(ks.WorkingDirectory))
                {
                    if (!servers.Any(s => string.Equals(s.WorkingDirectory, ks.WorkingDirectory, StringComparison.OrdinalIgnoreCase)))
                    {
                        var jarName = string.IsNullOrWhiteSpace(ks.ServerJarPath)
                            ? ks.Name
                            : Path.GetFileName(ks.ServerJarPath);

                        var inferredType = ServerType.Unknown;
                        var jarLower = jarName.ToLowerInvariant();
                        if (jarLower.Contains("paper")) inferredType = ServerType.Paper;
                        else if (jarLower.Contains("purpur")) inferredType = ServerType.Purpur;
                        else if (jarLower.Contains("spigot")) inferredType = ServerType.Spigot;
                        else if (jarLower.Contains("bukkit")) inferredType = ServerType.Bukkit;
                        else if (jarLower.Contains("fabric")) inferredType = ServerType.Fabric;
                        else if (jarLower.Contains("forge")) inferredType = ServerType.Forge;
                        else if (jarLower.Contains("neoforge")) inferredType = ServerType.NeoForge;
                        else if (jarLower.Contains("quilt")) inferredType = ServerType.Quilt;
                        else if (jarLower.Contains("velocity")) inferredType = ServerType.Velocity;
                        else if (jarLower.Contains("bungee") || jarLower.Contains("waterfall")) inferredType = ServerType.BungeeCord;
                        else if (jarLower.Contains("mohist")) inferredType = ServerType.Mohist;
                        else if (jarLower.Contains("arclight")) inferredType = ServerType.Arclight;
                        else if (jarLower.Contains("folia")) inferredType = ServerType.Folia;

                        servers.Add(new ServerInstance
                        {
                            ServerJarName = jarName,
                            WorkingDirectory = ks.WorkingDirectory,
                            ServerJarPath = ks.ServerJarPath,
                            ServerPort = ks.Port,
                            ServerType = inferredType,
                            KnownServerId = ks.KnownServerId,
                        });
                    }
                }
            }
        }

        AvailableServers = servers;
        Log.Information("✅ 服务器列表刷新完成，共 {Count} 个服务器", servers.Count);
    }

    /// <summary>
    /// 选中服务器名称变更回调
    /// </summary>
    partial void OnSelectedServerNameChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        var server = AvailableServers.FirstOrDefault(s =>
            s.DisplayName == value || s.ServerJarName == value);

        if (server == null)
        {
            var valueAsDir = value.Trim();
            server = AvailableServers.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.WorkingDirectory)
                && string.Equals(Path.GetFileName(s.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar)),
                                 valueAsDir, StringComparison.OrdinalIgnoreCase));
        }

        if (server != null)
        {
            Server = server;
        }
    }

    /// <summary>
    /// 根据 Dashboard 侧传过来的服务器上下文，选中最合适的 AvailableServers → 自动加载配置文件。
    /// </summary>
    public bool SelectServerByContext(
        string? displayName,
        string? workingDirectory,
        string? serverJarPath,
        string? knownServerId)
    {
        var candidates = AvailableServers;
        ServerInstance? best = null;

        if (!string.IsNullOrEmpty(knownServerId))
            best = candidates.FirstOrDefault(s => s.KnownServerId == knownServerId);

        if (best == null && !string.IsNullOrEmpty(workingDirectory))
            best = candidates.FirstOrDefault(s =>
                string.Equals(s.WorkingDirectory, workingDirectory, StringComparison.OrdinalIgnoreCase));

        if (best == null && !string.IsNullOrEmpty(serverJarPath))
            best = candidates.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.ServerJarPath)
                && string.Equals(s.ServerJarPath, serverJarPath, StringComparison.OrdinalIgnoreCase));

        if (best == null && !string.IsNullOrEmpty(workingDirectory))
        {
            var dirName = Path.GetFileName(workingDirectory.TrimEnd(Path.DirectorySeparatorChar));
            var jarName = string.IsNullOrEmpty(serverJarPath) ? null : Path.GetFileName(serverJarPath);
            best = candidates.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.WorkingDirectory)
                && string.Equals(Path.GetFileName(s.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar)),
                                 dirName, StringComparison.OrdinalIgnoreCase)
                && (jarName == null
                    || string.IsNullOrEmpty(s.ServerJarName)
                    || string.Equals(s.ServerJarName, jarName, StringComparison.OrdinalIgnoreCase)));
        }

        if (best == null && !string.IsNullOrEmpty(displayName))
        {
            best = candidates.FirstOrDefault(s =>
                s.DisplayName == displayName
                || s.DisplayName.StartsWith(displayName, StringComparison.OrdinalIgnoreCase)
                || displayName.StartsWith(s.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        if (best == null)
        {
            Log.Warning(
                "🔧 ConfigEditor 自动选择失败: DisplayName={DisplayName} WorkDir={WorkDir} Jar={Jar} KnownId={KnownId}",
                displayName, workingDirectory, serverJarPath, knownServerId);
            return false;
        }

        Log.Information(
            "🔧 ConfigEditor 自动选择服务器: {DisplayName} (Dir={Dir}, Jar={Jar})",
            best.DisplayName, best.WorkingDirectory, best.ServerJarName);
        Server = best;
        return true;
    }

    /// <summary>
    /// 浏览并选择服务器目录命令
    /// </summary>
    [RelayCommand]
    private void BrowseServerDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Minecraft 服务器核心 (*.jar)|*.jar|所有文件 (*.*)|*.*",
            Title = "选择服务器 JAR 文件（将自动识别所在目录）",
            CheckFileExists = true
        };

        var owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog(owner) == true)
        {
            var jarPath = dialog.FileName;
            var dirPath = Path.GetDirectoryName(jarPath);
            if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
            {
                Log.Information("📂 用户选择服务器目录: {Path}", dirPath);
                LoadServerFromDirectory(dirPath);
            }
        }
    }

    /// <summary>
    /// 从目录加载服务器实例
    /// </summary>
    private void LoadServerFromDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        var dirName = Path.GetFileName(path);
        var server = new ServerInstance
        {
            ServerJarName = dirName,
            WorkingDirectory = path,
            ServerType = ServerType.Unknown
        };

        try
        {
            var jarFiles = Directory.GetFiles(path, "*.jar", SearchOption.TopDirectoryOnly);
            if (jarFiles.Length > 0)
            {
                server.ServerJarPath = jarFiles[0];
                server.ServerJarName = Path.GetFileName(jarFiles[0]);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "自动检测 JAR 失败");
        }

        Server = server;
    }

    /// <summary>
    /// 重新扫描配置文件命令
    /// </summary>
    [RelayCommand]
    private async Task RescanConfigFilesAsync()
    {
        if (Server == null || string.IsNullOrEmpty(Server.WorkingDirectory)) return;
        await ScanDirectoryForConfigFilesAsync(Server.WorkingDirectory);
    }

    #endregion

    #region Server 切换与扫描（含并发保护 + 目录过滤重写）

    /// <summary>
    /// Server 属性变更回调 —— 同步重置状态，异步仅在「桥接层没扫过」时才触发扫目录。
    /// 
    /// ⚠️ 并发保护关键：
    ///   之前的实现 L547-584 存在竞态死锁：
    ///     ① 如果已有扫描在跑（_scanning=true），本回调先清空 ConfigFiles/Tree；
    ///     ② 然后因 _scanning=true 不启动新扫描；
    ///     ③ 若旧扫描版本号被更新的 scanVersion 丢弃 → 它的 finally 不清 _scanning；
    ///     → 结果：ConfigFiles 永远空 + _scanning 永远 true = 「已知服务器文件列表永远出不来」。
    ///   修复思路：
    ///     · 清空仅在「确定会启动新扫描 or 已有结果」时做；
    ///     · 如果 _scanning=true → 先看当前最新扫描是否就是本目录的（若是则保留旧结果等待它完成）；
    ///     · ScanDirectoryForConfigFilesAsync 的 finally 无条件清 _scanning（版本号保护只决定是否应用结果，
    ///       不决定是否释放锁——否则被丢弃的扫描会占着锁永远阻塞）。
    /// </summary>
    partial void OnServerChanged(ServerInstance? value)
    {
        if (!string.IsNullOrEmpty(value?.DisplayName) && SelectedServerName != value.DisplayName)
            SelectedServerName = value.DisplayName;

        // ── 条目侧：Clear 后立刻刷新分组（L5 修复：不等待 20ms Timer）
        foreach (var oldEntry in ConfigEntries)
        {
            oldEntry.PropertyChanging -= OnConfigEntryChanging;
            oldEntry.PropertyChanged -= OnConfigEntryChanged;
        }
        ConfigEntries.Clear();
        if (_groupUpdateTimer != null) { _groupUpdateTimer.Stop(); }
        UpdateGroupedEntries();  // 同步立刻刷，防止前端 getEntries 读到上一文件分组

        SelectedConfigFile = null;
        _currentFilePath = string.Empty;
        HasUnsavedChanges = false;
        _modifiedCount = 0;
        _undoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        _originalConfig.Clear();
        SaveStatusMessage = null;
        SaveErrorType = null;
        IsSaveError = false;

        if (value is null)
        {
            ConfigFiles = [];
            ConfigFileTree = [];
            ServerWorkingDirectory = string.Empty;
            OnPropertyChanged(nameof(ConfigFileCountText));
            OnPropertyChanged(nameof(HasServerDirectory));
            return;
        }

        ServerWorkingDirectory = value.WorkingDirectory;
        bool dirExists = !string.IsNullOrEmpty(value.WorkingDirectory) && Directory.Exists(value.WorkingDirectory);

        if (dirExists)
        {
            // 目录存在分支：
            // 竞态防护—— 如果现在正在扫，且正在扫的就是这个 WorkingDirectory → 保留旧的 ConfigFiles（空也没关系），
            // 等它完成；如果正在扫别的目录 → 旧扫描结果一定对不上，先清空，然后强制以本目录为根启动一次新扫描。
            // （另一条链路如 config:selectServer / config:selectDefaultServer 已经同步 await 扫过，
            //   进来时 _scanning=false + ConfigFiles 有值，就不会重复触发异步扫描。）
            bool needsScan;
            if (_scanning)
            {
                // 正在扫描时：先把 ConfigFiles/Tree 暂时置空（避免展示旧服务器的文件，误导点击后找不到路径），
                // 然后把需要再扫的 flag 设 true—— ScanDirectoryForConfigFilesAsync finally 无论版本号都清 _scanning，
                // 这样即使旧扫描被丢弃，锁也能释放；我们等下启动的新扫描会 bump version，旧扫描会自然被丢弃。
                ConfigFiles = [];
                ConfigFileTree = [];
                needsScan = true;
            }
            else if (ConfigFiles.Count == 0)
            {
                // 没在扫描 + 无结果：必须扫
                ConfigFiles = [];
                ConfigFileTree = [];
                needsScan = true;
            }
            else
            {
                // 已有结果（来自桥接层同步赋值）：保留
                needsScan = false;
            }

            OnPropertyChanged(nameof(ConfigFileCountText));
            OnPropertyChanged(nameof(HasServerDirectory));

            if (needsScan)
            {
                _ = ScanDirectoryAfterServerChangedAsync(value);
            }
        }
        else
        {
            // WorkingDirectory 为空或不存在：用进程返回的 ConfigFiles 兜底
            if (string.IsNullOrEmpty(value.WorkingDirectory))
            {
                ConfigFiles = value.ConfigFiles
                    .Where(f => !f.EndsWith('/') && !f.EndsWith('\\'))
                    .ToList();
            }
            else
            {
                ConfigFiles = value.ConfigFiles
                    .Where(f => !f.EndsWith('/') && !f.EndsWith('\\'))
                    .Select(f =>
                    {
                        try { return Path.GetRelativePath(value.WorkingDirectory, f); }
                        catch { return f; }
                    })
                    .ToList();
            }
            ConfigFileTree = BuildFlatFileTree(ConfigFiles);
            OnPropertyChanged(nameof(ConfigFileCountText));
            OnPropertyChanged(nameof(HasServerDirectory));
        }
    }

    /// <summary>
    /// 服务器切换后异步扫目录（仅 IO 部分），加扫描版本号防止旧结果覆盖新结果。
    /// </summary>
    private async System.Threading.Tasks.Task ScanDirectoryAfterServerChangedAsync(ServerInstance? value)
    {
        if (value is null) return;
        if (string.IsNullOrEmpty(value.WorkingDirectory) || !Directory.Exists(value.WorkingDirectory))
            return;

        try
        {
            await ScanDirectoryForConfigFilesAsync(value.WorkingDirectory);
            OnPropertyChanged(nameof(ConfigFileCountText));
            OnPropertyChanged(nameof(HasServerDirectory));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "服务器切换后扫描配置文件失败: {Dir}", value.WorkingDirectory);
        }
    }

    /// <summary>
    /// 递归扫描目录以构建配置文件列表与目录树。
    /// 新增：scanVersion 并发保护 —— 结束时若版本号对不上则丢弃结果。
    /// </summary>
    public async Task ScanDirectoryForConfigFilesAsync(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            ConfigFiles = [];
            ConfigFileTree = [];
            return;
        }

        var localVersion = System.Threading.Interlocked.Increment(ref _scanVersion);
        _scanning = true;
        Log.Information("🔍 递归扫描配置文件目录: {Path} (version={V})", rootPath, localVersion);

        try
        {
            var (flatList, treeRoot) = await Task.Run(() =>
            {
                var flat = new List<string>();
                var tree = new List<ConfigFileItem>();
                var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".properties", ".yml", ".yaml", ".json", ".cfg", ".conf",
                    ".toml", ".ini"
                };
                try
                {
                    BuildConfigFileTree(rootPath, rootPath, supportedExtensions, tree, flat, depth: 0, fileCount: ref _scanVersion /* dummy */);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "扫描配置文件目录失败: {Message}", ex.Message);
                }
                return (flat, tree);
            }).ConfigureAwait(true);

            // 版本号校验：如果中间又启动了更新的扫描，丢弃当前结果
            if (localVersion != System.Threading.Volatile.Read(ref _scanVersion))
            {
                Log.Debug("扫描结果被更新的版本覆盖（丢弃）: local={Local}, current={Current}",
                    localVersion, _scanVersion);
                return;
            }

            ConfigFiles = flatList;
            ConfigFileTree = treeRoot;
            OnPropertyChanged(nameof(ConfigFileCountText));
            Log.Information("✅ 扫描完成，找到 {Count} 个配置文件", flatList.Count);
        }
        finally
        {
            // ⚠️ 修复前：仅当自己是最新扫描才清 _scanning → 如果被丢弃则占着锁 → 后续所有 OnServerChanged
            //     进来都因 _scanning=true + ConfigFiles 被清空 → 永远拿不到新结果。
            // 修复后：无条件释放 _scanning 锁；版本号校验只管「是否应用结果」，不管「是否释放锁」。
            var curVer = System.Threading.Volatile.Read(ref _scanVersion);
            if (localVersion != curVer)
            {
                Log.Debug("扫描结果被更新版本覆盖（释放锁但丢弃结果）: local={Local}, current={Current}",
                    localVersion, curVer);
            }
            _scanning = false;
        }
    }

    /// <summary>
    /// 递归构建配置文件目录树 —— 目录过滤重写 + 文件数硬上限。
    /// </summary>
    private static void BuildConfigFileTree(
        string currentPath,
        string rootPath,
        HashSet<string> supportedExtensions,
        List<ConfigFileItem> parentList,
        List<string> flatList,
        int depth,
        ref int fileCount)  // 注意：ref 只是为了让签名区分，真实计数用 flatList.Count + MaxFilesPerServer 比较
    {
        if (depth > 10) return;
        if (flatList.Count >= MaxFilesPerServer) return;

        try
        {
            // ── 先处理文件（TopDirectoryOnly，避免一下子枚举所有子目录文件卡死）
            var files = Directory.GetFiles(currentPath);
            foreach (var file in files)
            {
                if (flatList.Count >= MaxFilesPerServer) break;

                var ext = Path.GetExtension(file);
                if (!supportedExtensions.Contains(ext)) continue;

                var fileName = Path.GetFileName(file);
                var relativePath = Path.GetRelativePath(rootPath, file);

                parentList.Add(new ConfigFileItem(fileName, file, relativePath, isDirectory: false));
                flatList.Add(relativePath);
            }

            if (flatList.Count >= MaxFilesPerServer)
            {
                Log.Warning("⚠️ 扫描配置文件达到上限 {Max}，已中断。Dir={Dir}", MaxFilesPerServer, currentPath);
                return;
            }

            // ── 再处理目录（加过滤规则）
            var directories = Directory.GetDirectories(currentPath);
            foreach (var dir in directories)
            {
                if (flatList.Count >= MaxFilesPerServer) break;

                var dirName = Path.GetFileName(dir);

                // 黑名单目录：任何 depth 都跳过
                if (SkipDirNames.Contains(dirName)) continue;

                // . 开头目录：仅在 depth=0 时跳过（避免扫 .git）；plugins/.data 这种允许进入
                if (depth == 0 && dirName.StartsWith('.')) continue;

                var dirItem = new ConfigFileItem(
                    dirName,
                    dir,
                    Path.GetRelativePath(rootPath, dir),
                    isDirectory: true);

                BuildConfigFileTree(dir, rootPath, supportedExtensions, dirItem.Children, flatList, depth + 1, ref fileCount);

                if (dirItem.Children.Count > 0 || depth == 0)
                    parentList.Add(dirItem);
            }
        }
        catch (UnauthorizedAccessException)
        {
            Log.Debug("无权限访问目录: {Path}", currentPath);
        }
        catch (Exception ex)
        {
            Log.Debug("扫描目录 {Path} 时出错: {Message}", currentPath, ex.Message);
        }
    }

    /// <summary>
    /// 把扁平相对路径列表构造成「单层文件树」—— 当 WorkingDirectory 不存在或拿不到真实目录时的兜底。
    /// 也可供桥接层 L2 调用（config:selectServer WorkingDirectory 不存在分支）。
    /// </summary>
    public static List<ConfigFileItem> BuildFlatFileTree(IEnumerable<string> relativePaths)
    {
        var list = relativePaths?.ToList() ?? [];
        var tree = new List<ConfigFileItem>(list.Count);
        foreach (var p in list)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            tree.Add(new ConfigFileItem(
                Path.GetFileName(p),
                fullPath: p,         // fullPath 兜底用相对路径，上层选择时若需要会拼接 WorkingDirectory
                relativePath: p,
                isDirectory: false));
        }
        return tree;
    }

    #endregion

    #region 配置文件加载（错误条目 + 分组时序修复）

    /// <summary>
    /// 选中配置文件变更回调
    /// </summary>
    partial void OnSelectedConfigFileChanged(string? value)
    {
        Log.Debug("📄 选中配置文件: {File}", value);
        OnPropertyChanged(nameof(SelectedConfigFileName));

        // ── 切换文件：Clear + 立刻同步刷新分组（防止显示上一个文件）
        foreach (var oldEntry in ConfigEntries)
        {
            oldEntry.PropertyChanging -= OnConfigEntryChanging;
            oldEntry.PropertyChanged -= OnConfigEntryChanged;
        }
        ConfigEntries.Clear();
        if (_groupUpdateTimer != null) { _groupUpdateTimer.Stop(); }
        UpdateGroupedEntries();

        // ⚠️ 进入新文件前先把 IsLoading=false，防止上一次加载（哪怕被 cancel 了但 finally 没跑完）
        // 的 IsLoading=true 残留导致无限转圈。真正的加载开始后会再设 true。
        IsLoading = false;
        LoadProgress = 0;

        if (Server is null || string.IsNullOrEmpty(value))
            return;

        var fullPath = Path.Combine(Server.WorkingDirectory, value);
        if (!File.Exists(fullPath))
        {
            // ── 文件不存在：显示一个错误条目提示用户（不是静默空列表）
            PushErrorEntry($"文件不存在：{value}");
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _lastLoadTask = LoadConfigAsync(fullPath, value, _loadCts.Token);
    }

    /// <summary>加载任务的默认超时（毫秒）——防止解析器死循环导致 UI 永远转圈</summary>
    private const int LoadTimeoutMs = 15000;

    /// <summary>
    /// 异步加载配置文件 —— 修复：分组最后一次性刷新、catch 注入 __ERROR__ 条目、超时保护。
    /// </summary>
    private async Task LoadConfigAsync(string fullPath, string fileName, CancellationToken cancellationToken = default)
    {
        Log.Information("📂 加载配置文件: {Path}", fullPath);

        // ⚠️ 双重保险：再设一次 false→true，避免任何竞态下的 IsLoading 残留
        IsLoading = false;
        IsLoading = true;
        LoadProgress = 0;

        // ── 超时 CTS：15 秒没完成强制取消，避免解析器/磁盘死循环
        using var timeoutCts = new CancellationTokenSource(LoadTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);
        var effectiveToken = linkedCts.Token;

        try
        {
            Dictionary<string, string> config;
            try
            {
                // 外层再包一层 Task.Run + 超时 Token，确保即便内部阻塞也能被取消
                config = await Task.Run(
                    () => _configManager.ReadConfigAsync(fullPath),
                    effectiveToken).Unwrap().ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"加载配置文件超时（>{LoadTimeoutMs / 1000}s）：{fileName}。"
                    + "建议检查文件是否损坏或被其他进程以独占模式锁定。");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("🔄 加载已取消，丢弃结果: {Path}", fullPath);
                return;
            }

            _currentFilePath = fullPath;
            var pureFileName = Path.GetFileName(fileName);

            var processedEntries = await Task.Run(() =>
            {
                return config.Select(kvp =>
                {
                    var descriptor = _configManager.GetDescriptor(kvp.Key, pureFileName);
                    return new ServerConfigEntry
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                        OriginalValue = kvp.Value,
                        SourceFile = fileName,
                        IsModified = false,
                        Descriptor = descriptor,
                        IsValid = descriptor is null ||
                                  _configManager.ValidateValue(kvp.Key, fileName, kvp.Value)
                    };
                }).ToList();
            }, effectiveToken);

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("🔄 加载已取消，丢弃结果: {Path}", fullPath);
                return;
            }

            foreach (var oldEntry in ConfigEntries)
            {
                oldEntry.PropertyChanging -= OnConfigEntryChanging;
                oldEntry.PropertyChanged -= OnConfigEntryChanged;
            }
            ConfigEntries.Clear();
            _modifiedCount = 0;
            _originalConfig = new Dictionary<string, string>(config);
            HasUnsavedChanges = false;
            _undoStack.Clear();
            UndoCommand.NotifyCanExecuteChanged();

            // ── 停止正在运行中的分组 Timer，避免中间 20ms 内读到空分组
            if (_groupUpdateTimer != null) _groupUpdateTimer.Stop();

            const int batchSize = 15;  // 增大到 15，减少调度次数
            int total = processedEntries.Count;
            int processed = 0;

            // ⚠️ 空条目特殊处理：total==0 时 for 循环不跑，LoadProgress 永远 0。
            // 直接先把进度设满，防止用户永远看到 "加载中 0%"
            if (total == 0)
            {
                LoadProgress = 100;
                UpdateGroupedEntries();
                Log.Information("✅ 配置加载完成（空文件）: {File}", fileName);
                return;
            }

            for (int i = 0; i < total; i += batchSize)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var batch = processedEntries.Skip(i).Take(batchSize).ToList();
                foreach (var entry in batch)
                {
                    entry.PropertyChanging += OnConfigEntryChanging;
                    entry.PropertyChanged += OnConfigEntryChanged;
                    ConfigEntries.Add(entry);
                    processed++;
                }
                LoadProgress = total <= 0 ? 100 : (int)(processed * 100.0 / total);
                await Task.Yield();
            }

            // ── 全部 batch 完成：立刻同步刷分组（不再等 Timer）
            UpdateGroupedEntries();

            Log.Information("✅ 配置加载完成，共 {Count} 项配置", total);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("🔄 配置加载被取消: {Path}", fullPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "配置加载失败: {Message}", ex.Message);

            // L3.3 修复：注入 __ERROR__ 错误条目，前端渲染为 Alert 而不是空列表
            ConfigEntries.Clear();
            var userMsg = ex is ConfigParseException cpe
                ? $"{cpe.Message}\n扩展名: {cpe.FileExtension}, 内容长度: {cpe.ContentLength}"
                : ex is TimeoutException te
                    ? te.Message
                    : ex.Message;
            PushErrorEntry(userMsg, hintFormat: (ex as ConfigParseException)?.HintTryFormat);
        }
        finally
        {
            IsLoading = false;
            LoadProgress = 100;
        }
    }

    /// <summary>
    /// 往 <see cref="ConfigEntries"/> 里压一个 "__ERROR__" 标记条目，用于前端 Alert 提示。
    /// </summary>
    private void PushErrorEntry(string message, string? hintFormat = null)
    {
        var entry = new ServerConfigEntry
        {
            Key = "__ERROR__",
            Value = message,
            DisplayNameOverride = "⚠️ 文件解析失败",
            Category = "__ERROR__",
            Descriptor = null,
            SourceFile = SelectedConfigFile ?? string.Empty,
            IsValid = false,
            IsModified = false,
            ErrorMessage = string.IsNullOrEmpty(hintFormat)
                ? message
                : $"{message}（建议尝试格式: {hintFormat}）"
        };
        ConfigEntries.Add(entry);
        if (_groupUpdateTimer != null) _groupUpdateTimer.Stop();
        UpdateGroupedEntries();
    }

    #endregion

    #region 保存 / 重置 / 撤销

    [RelayCommand(CanExecute = nameof(CanSaveConfig))]
    private async Task SaveConfigAsync()
    {
        if (Server is null || string.IsNullOrEmpty(_currentFilePath))
        {
            Log.Debug("🔄 SaveConfig 跳过: Server 为空或路径为空");
            return;
        }

        Log.Information("💾 开始保存配置到 {Path}", _currentFilePath);

        try
        {
            if (File.Exists(_currentFilePath))
            {
                using var fs = new FileStream(_currentFilePath, FileMode.Open, FileAccess.Write, FileShare.None);
            }
        }
        catch (IOException ioEx) when (IsFileLocked(ioEx))
        {
            IsSaveError = true;
            SaveErrorType = "FileLocked";
            SaveStatusMessage = "文件被占用，保存失败（请关闭正在使用该文件的程序）";
            System.Windows.MessageBox.Show(
                $"文件被占用，保存失败：\n\n{ioEx.Message}\n\n请关闭正在使用该文件的程序（如服务器进程或文本编辑器）后重试。",
                "保存失败 - 文件被占用",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            Log.Warning(ioEx, "⚠️ 配置文件被占用: {Path}", _currentFilePath);
            return;
        }
        catch (UnauthorizedAccessException authEx)
        {
            IsSaveError = true;
            SaveErrorType = "UnauthorizedAccess";
            SaveStatusMessage = $"权限不足，保存失败：{authEx.Message}";
            System.Windows.MessageBox.Show(
                $"权限不足，保存失败：\n\n{authEx.Message}",
                "保存失败 - 权限不足",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Log.Warning(authEx, "⚠️ 配置文件无写入权限: {Path}", _currentFilePath);
            return;
        }

        try
        {
            // 过滤掉 __ERROR__ 条目（不能把错误提示写进文件里）
            var currentConfig = ConfigEntries
                .Where(e => e.Key != "__ERROR__")
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            await _configManager.SaveConfigAsync(_currentFilePath, currentConfig);

            _originalConfig = new Dictionary<string, string>(currentConfig);
            foreach (var entry in ConfigEntries)
            {
                if (entry.Key != "__ERROR__") entry.IsModified = false;
            }
            HasUnsavedChanges = false;
            _undoStack.Clear();
            UndoCommand.NotifyCanExecuteChanged();

            IsSaveError = false;
            SaveErrorType = null;
            SaveStatusMessage = $"配置已保存，共 {currentConfig.Count} 项";
            Log.Information("✅ 配置保存成功，共保存 {Count} 项配置", currentConfig.Count);
        }
        catch (IOException ex)
        {
            IsSaveError = true;
            SaveErrorType = IsFileLocked(ex) ? "FileLocked" : "Unknown";
            SaveStatusMessage = $"保存失败：{ex.Message}（文件可能被其他程序占用）";
            System.Windows.MessageBox.Show(
                $"保存失败：\n\n{ex.Message}\n\n文件可能被其他程序占用，请关闭后重试。",
                "保存失败",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Log.Error(ex, "💥 配置保存失败（IO异常）: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            IsSaveError = true;
            SaveErrorType = "Unknown";
            SaveStatusMessage = $"保存失败：{ex.Message}";
            System.Windows.MessageBox.Show(
                $"保存失败：\n\n{ex.Message}",
                "保存失败",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Log.Error(ex, "💥 配置保存失败: {Message}", ex.Message);
        }
    }

    private static bool IsFileLocked(IOException ex)
    {
        int errorCode = ex.HResult & 0xFFFF;
        return errorCode is 32 or 33;
    }

    private bool CanSaveConfig() => HasUnsavedChanges && !string.IsNullOrEmpty(_currentFilePath);

    [RelayCommand(CanExecute = nameof(CanResetChanges))]
    private void ResetChanges()
    {
        Log.Information("🔄 重置所有配置变更");
        foreach (var entry in ConfigEntries)
        {
            if (entry.Key == "__ERROR__") continue;
            if (_originalConfig.TryGetValue(entry.Key, out var originalValue))
            {
                entry.Value = originalValue;
                entry.IsModified = false;
            }
        }
        HasUnsavedChanges = false;
        _modifiedCount = 0;
        _undoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        SaveStatusMessage = null;
        SaveErrorType = null;
        IsSaveError = false;
    }

    private bool CanResetChanges() => HasUnsavedChanges;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        _isUndoing = true;
        try
        {
            var (entry, previousValue) = _undoStack.Pop();
            entry.Value = previousValue;
            entry.IsModified = !string.Equals(entry.Value, entry.OriginalValue, StringComparison.Ordinal);
        }
        finally { _isUndoing = false; }

        HasUnsavedChanges = ConfigEntries.Any(ce => ce.IsModified && ce.Key != "__ERROR__");
        UndoCommand.NotifyCanExecuteChanged();
    }

    private bool CanUndo() => _undoStack.Count > 0;

    #endregion

    #region 分组更新

    partial void OnConfigEntriesChanged(ObservableCollection<ServerConfigEntry> value)
    {
    }

    private void ScheduleGroupUpdate()
    {
        if (_groupUpdateTimer != null)
        {
            _groupUpdateTimer.Stop();
            _groupUpdateTimer.Start();
        }
    }

    /// <summary>同步更新分组（不再走 Dispatcher 异步，减少竞态）</summary>
    private void UpdateGroupedEntries()
    {
        try
        {
            if (System.Windows.Application.Current?.CheckAccess() ?? true)
            {
                DoUpdateGroupedEntries();
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(DoUpdateGroupedEntries);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "UpdateGroupedEntries 异常（忽略）");
        }
    }

    private void DoUpdateGroupedEntries()
    {
        // "__ERROR__" 条目单独放到一个醒目分组（__ERROR__），前端会判断 key 渲染成 Alert
        GroupedConfigEntries = ConfigEntries
            .GroupBy(e => string.IsNullOrEmpty(e.Category)
                ? (e.Descriptor?.Category ?? "其他")
                : e.Category)
            .Select(g => new ConfigEntryGroup(g.Key, g.ToList()))
            .ToList();
    }

    private void OnGroupUpdateTimerElapsed(object? sender, ElapsedEventArgs e)
        => UpdateGroupedEntries();

    private void OnConfigEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScheduleGroupUpdate();

    private void OnConfigEntryChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
    {
        if (sender is not ServerConfigEntry entry || e.PropertyName != nameof(ServerConfigEntry.Value))
            return;
        if (!_isUndoing)
            _undoStack.Push((entry, entry.Value));
    }

    private void OnConfigEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ServerConfigEntry entry || e.PropertyName != nameof(ServerConfigEntry.Value))
            return;
        if (entry.Key == "__ERROR__") return;

        var wasModified = entry.IsModified;
        entry.IsModified = !string.Equals(entry.Value, entry.OriginalValue, StringComparison.Ordinal);
        if (entry.IsModified && !wasModified) _modifiedCount++;
        else if (!entry.IsModified && wasModified) _modifiedCount--;

        entry.IsValid = entry.Descriptor is null ||
                        _configManager.ValidateValue(entry.Key, entry.SourceFile, entry.Value);

        HasUnsavedChanges = _modifiedCount > 0;
        SaveConfigCommand.NotifyCanExecuteChanged();
        ResetChangesCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Log.Information("🧹 ConfigEditorViewModel 释放资源中...");

        foreach (var entry in ConfigEntries)
        {
            entry.PropertyChanging -= OnConfigEntryChanging;
            entry.PropertyChanged -= OnConfigEntryChanged;
        }
        ConfigEntries.Clear();

        ConfigEntries.CollectionChanged -= OnConfigEntriesChanged;

        if (_groupUpdateTimer != null)
        {
            _groupUpdateTimer.Elapsed -= OnGroupUpdateTimerElapsed;
            _groupUpdateTimer.Stop();
            _groupUpdateTimer.Dispose();
            _groupUpdateTimer = null;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        GC.SuppressFinalize(this);
        Log.Information("✅ ConfigEditorViewModel 资源释放完成");
    }

    #endregion
}
