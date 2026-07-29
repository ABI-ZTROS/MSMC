// -----------------------------------------------------------------------------
// 文件名: ConfigEditorViewModel.cs
// 命名空间: io.NET.ZTR_OS.Features.ConfigEditor.ViewModels
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
using io.NET.ZTR_OS.Features.ConfigEditor.Models;
using io.NET.ZTR_OS.Features.ServerDetection.Models;
using io.NET.ZTR_OS.Features.JavaInstallation.Constants;
using io.NET.ZTR_OS.Features.Settings.Services;
using io.NET.ZTR_OS.Features.ConfigEditor.Services;
using io.NET.ZTR_OS.Features.ServerDetection.Services;
using Serilog;

namespace io.NET.ZTR_OS.Features.ConfigEditor.ViewModels;

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
    #region 常量（极简版：无目录过滤、无扫描上限——直接把所有符合扩展名的文件都列出来）

    /// <summary>配置文件的扩展名白名单。任何扩展名匹配都会被收录进文件列表。
    /// 用户说：直接走已保存服务器的列表然后去访问绝对路径(文件层)，不要再管是不是被占用。
    /// 所以这里也不做黑名单目录/最大扫描数的限制，直接全目录一把梭。</summary>
    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".properties", ".yml", ".yaml", ".json", ".cfg", ".conf",
        ".toml", ".ini"
    };

    #endregion

    #region 字段（极简：删 _scanVersion/_scanning/_loadCts/_lastLoadTask/_serverDetector）

    /// <summary>配置管理服务（解析 + 保存 + 翻译描述符）</summary>
    private readonly IConfigManager _configManager;
    /// <summary>应用配置服务——用户要求核心用它来拿「已保存服务器列表」</summary>
    private readonly IAppConfigService? _appConfigService;

    /// <summary>刷新代数 —— 防止并发的 RefreshServerListAsync 后完成者覆盖先完成者。
    /// 每次刷新开头 Interlocked.Increment，赋值 AvailableServers 前校验是否仍是最新代。
    /// 如果不是最新代，直接丢弃结果（说明期间又发起了更新的刷新）。</summary>
    private int _refreshGeneration;

    /// <summary>原始配置快照 —— 用于重置变更与脏数据比对（保存/重置/撤销的基础）</summary>
    private Dictionary<string, string> _originalConfig = new();

    /// <summary>当前编辑的配置文件完整路径（绝对路径）</summary>
    private string _currentFilePath = string.Empty;

    /// <summary>分组更新防抖计时器（保留：翻译分组展示还需要它）</summary>
    private System.Timers.Timer? _groupUpdateTimer;

    /// <summary>编辑历史栈 —— 记录每次值变更前的条目引用与原始值（撤销功能保留）</summary>
    private readonly Stack<(ServerConfigEntry Entry, string PreviousValue)> _undoStack = new();

    /// <summary>已修改条目计数器 —— O(1) 替代 O(n) 的 ConfigEntries.Any(...) 扫描（保存/脏计数保留）</summary>
    private int _modifiedCount;

    /// <summary>撤销操作进行中标志 —— 防止撤销恢复值时再次触发压栈（撤销功能保留）</summary>
    private bool _isUndoing;

    /// <summary>指示当前实例是否已释放，防止重复 Dispose 导致资源二次释放</summary>
    private bool _disposed;

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
    /// 初始化配置编辑器视图模型的新实例（完整依赖版本）。
    /// 注：serverDetector 参数保留以兼容 DI 容器签名，但极简版不再调用它
    /// （用户要求："直接走已保存服务器的列表然后去访问绝对路径"）。
    /// </summary>
    /// <param name="configManager">配置管理服务</param>
    /// <param name="serverDetector">服务器检测服务（保留参数兼容 DI，内部不再使用）</param>
    /// <param name="appConfigService">应用配置服务</param>
    public ConfigEditorViewModel(
        IConfigManager configManager,
        IServerDetector serverDetector,
        IAppConfigService appConfigService) : this(configManager)
    {
        // 极简版：不再使用 _serverDetector。参数保留以避免破坏 DI 注册。
        _ = serverDetector;
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

    #region 服务器列表与联动（极简版：直接取 AppConfigService 已知服务器列表 → WorkingDirectory 作为根目录绝对路径）

    /// <summary>
    /// 刷新可用服务器列表。
    /// 极简链路（用户明确要求）：
    ///   ① 只走 AppConfigService.GetAllKnownServers() —— 已保存服务器列表是唯一数据源
    ///   ② WorkingDirectory 优先；为空时从 ServerJarPath 推导（"多一个调用"）
    ///   ③ 推导出的目录不存在就跳过；存在就加入 AvailableServers
    /// 不再调 ServerDetector —— 用户明确说"直接走已保存服务器的列表然后去访问绝对路径(文件层)"。
    /// 代数保护：防止构造函数 fire-and-forget 与 getAvailableServers 兜底刷新并发执行时，
    /// 后完成者（通常是旧的空列表）覆盖先完成者（通常是新的含 Core 列表）。
    /// </summary>
    [RelayCommand]
    public async Task RefreshServerListAsync()
    {
        // ── 代数保护：记录本次刷新的代号，赋值前校验是否仍是最新代
        var generation = System.Threading.Interlocked.Increment(ref _refreshGeneration);
        Log.Information("🔄 配置编辑器：刷新可用服务器列表（极简版：仅已知服务器，gen={Gen}）", generation);

        var servers = new List<ServerInstance>();

        if (_appConfigService == null)
        {
            // 代数校验：仍是最新的才赋值
            if (generation == System.Threading.Volatile.Read(ref _refreshGeneration))
            {
                AvailableServers = servers;
                Log.Warning("⚠️ AppConfigService 未注入，可用服务器列表为空");
            }
            return;
        }

        await Task.Run(() =>
        {
            foreach (var ks in _appConfigService.GetAllKnownServers())
            {
                // ── 核心：WorkingDirectory 优先，为空时从 ServerJarPath 推导（"多一个调用"）
                var workingDir = ks.WorkingDirectory;
                if (string.IsNullOrWhiteSpace(workingDir) && !string.IsNullOrWhiteSpace(ks.ServerJarPath))
                {
                    try
                    {
                        var derived = Path.GetDirectoryName(ks.ServerJarPath);
                        if (!string.IsNullOrWhiteSpace(derived))
                        {
                            workingDir = derived;
                            Log.Information("📂 WorkingDirectory 为空，从 JarPath 推导: {Jar} → {Dir}",
                                ks.ServerJarPath, workingDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "从 ServerJarPath 推导 WorkingDirectory 失败: {Jar}", ks.ServerJarPath);
                    }
                }

                if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
                {
                    Log.Debug("⏭️ 跳过已知服务器（目录无效）: Name={Name} Dir={Dir}", ks.Name, workingDir);
                    continue;
                }

                var jarName = string.IsNullOrWhiteSpace(ks.ServerJarPath)
                    ? ks.Name
                    : Path.GetFileName(ks.ServerJarPath);

                servers.Add(new ServerInstance
                {
                    ServerJarName = jarName,
                    WorkingDirectory = workingDir,
                    ServerJarPath = ks.ServerJarPath,
                    ServerPort = ks.Port,
                    ServerType = InferServerType(jarName ?? string.Empty),
                    KnownServerId = ks.KnownServerId,
                    // 未运行状态：PID=0，DisplayName 会显示 "{Type} @ {Dir}"
                    ProcessId = 0,
                });
            }
        }).ConfigureAwait(true);

        // ── 代数校验：只有仍是最新的刷新才赋值，避免旧刷新覆盖新刷新
        if (generation == System.Threading.Volatile.Read(ref _refreshGeneration))
        {
            AvailableServers = servers;
            Log.Information("✅ 配置编辑器服务器列表刷新完成：共 {Count} 台（gen={Gen}）", servers.Count, generation);
        }
        else
        {
            Log.Information("⏭️ 刷新结果已丢弃（gen={Gen} 已被更新的 gen={Latest} 取代）",
                generation, System.Threading.Volatile.Read(ref _refreshGeneration));
        }
    }

    /// <summary>
    /// 根据 JAR 文件名推断服务器类型（派生类优先于基类，避免上游核心被误判为其上游）。
    /// 抽成方法供 RefreshServerListAsync 复用，与 ServerTypeClassifier.JarNameTypeMap 保持一致语义。
    /// </summary>
    private static ServerType InferServerType(string jarName)
    {
        var jl = (jarName ?? string.Empty).ToLowerInvariant();
        if (jl.Contains("uspigot") || jl.Contains("u-spigot")) return ServerType.USpigot;
        if (jl.Contains("luminol")) return ServerType.Luminol;
        if (jl.Contains("leafmc") || (jl.Contains("leaf") && !jl.Contains("leaves"))) return ServerType.Leaf;
        if (jl.Contains("leaves")) return ServerType.Leaves;
        if (jl.Contains("folia")) return ServerType.Folia;
        if (jl.Contains("kaiiju")) return ServerType.Kaiiju;
        if (jl.Contains("purpur")) return ServerType.Purpur;
        if (jl.Contains("pufferfish")) return ServerType.Pufferfish;
        if (jl.Contains("airplane")) return ServerType.Airplane;
        if (jl.Contains("tuinity")) return ServerType.Tuinity;
        if (jl.Contains("yatopia")) return ServerType.Yatopia;
        if (jl.Contains("akarin")) return ServerType.Akarin;
        if (jl.Contains("nachospigot") || jl.Contains("nacho-")) return ServerType.NachoSpigot;
        if (jl.Contains("spigot")) return ServerType.Spigot;
        if (jl.Contains("paper")) return ServerType.Paper;
        if (jl.Contains("bukkit")) return ServerType.Bukkit;
        if (jl.Contains("neoforge")) return ServerType.NeoForge;
        if (jl.Contains("forge")) return ServerType.Forge;
        if (jl.Contains("quilt")) return ServerType.Quilt;
        if (jl.Contains("fabric")) return ServerType.Fabric;
        if (jl.Contains("velocity")) return ServerType.Velocity;
        if (jl.Contains("bungee") || jl.Contains("waterfall") || jl.Contains("flamecord")) return ServerType.BungeeCord;
        if (jl.Contains("hexacord")) return ServerType.HexaCord;
        if (jl.Contains("mohist")) return ServerType.Mohist;
        if (jl.Contains("arclight")) return ServerType.Arclight;
        if (jl.Contains("catserver")) return ServerType.CatServer;
        if (jl.Contains("banner")) return ServerType.Banner;
        if (jl.Contains("magma")) return ServerType.Magma;
        if (jl.Contains("spongeforge")) return ServerType.SpongeForge;
        if (jl.Contains("sponge") || jl.Contains("spongevanilla")) return ServerType.Sponge;
        if (jl.Contains("powernukkit")) return ServerType.PowerNukkit;
        if (jl.Contains("nukkit")) return ServerType.Nukkit;
        if (jl.Contains("glowstone")) return ServerType.Glowstone;
        return ServerType.Unknown;
    }

    /// <summary>
    /// 选中服务器名称变更回调（极简版：DisplayName / ServerJarName / 目录名三级匹配 → 赋值 Server）
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
                server.ServerType = InferServerType(server.ServerJarName);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "自动检测 JAR 失败");
        }

        Server = server;
    }

    /// <summary>
    /// 手动定位 JAR 文件加载服务器实例。
    /// 用户明确要求：「新增一个手动定位 Jar，然后顺着路径去遍历」。
    /// 极简链路：
    ///   ① 弹 OpenFileDialog 让用户选 JAR
    ///   ② 推导 WorkingDirectory = Path.GetDirectoryName(jarPath)
    ///   ③ 构造 ServerInstance 并赋值 Server（OnServerChanged 会自动扫目录）
    ///   ④ 返回选中的 JAR 路径供前端显示
    /// </summary>
    /// <returns>选中的 JAR 路径（null 表示用户取消）</returns>
    public string? BrowseAndLoadFromJar()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Minecraft 服务器核心 (*.jar)|*.jar|所有文件 (*.*)|*.*",
            Title = "手动定位服务器 JAR 文件",
            CheckFileExists = true
        };

        var owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog(owner) != true)
        {
            Log.Information("⏭️ 用户取消了手动定位 JAR");
            return null;
        }

        var jarPath = dialog.FileName;
        var dirPath = Path.GetDirectoryName(jarPath);

        if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
        {
            Log.Warning("⚠️ 手动定位 JAR 失败：推导的目录无效: {Jar}", jarPath);
            return null;
        }

        var jarName = Path.GetFileName(jarPath);
        Log.Information("📂 手动定位 JAR: {Jar} → 目录: {Dir}", jarPath, dirPath);

        // 构造 ServerInstance 并赋值 Server（OnServerChanged 会自动扫目录）
        var server = new ServerInstance
        {
            ServerJarName = jarName,
            ServerJarPath = jarPath,
            WorkingDirectory = dirPath,
            ServerType = InferServerType(jarName),
            ProcessId = 0,
        };

        Server = server;
        return jarPath;
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

    #region Server 切换与扫描（极简版：选服务器 → 直接递归扫 → 写结果，无锁/无版本号/无过滤）

    /// <summary>
    /// Server 属性变更回调 —— 三步最小链路：
    ///   ① 清空条目/分组状态
    ///   ② 设 SelectedServerName / ServerWorkingDirectory
    ///   ③ 直接 fire-and-forget 扫目录（WorkingDirectory 在 RefreshServerListAsync 里已保证有效）
    /// 不再管：_scanning 锁、scanVersion 版本、needsScan 三态决策、目录黑名单、ConfigFiles 兜底等。
    /// </summary>
    partial void OnServerChanged(ServerInstance? value)
    {
        if (!string.IsNullOrEmpty(value?.DisplayName) && SelectedServerName != value.DisplayName)
            SelectedServerName = value.DisplayName;

        // ── 条目侧：Clear + 立刻同步刷新分组（防止显示上一文件/服务器残留）
        foreach (var oldEntry in ConfigEntries)
        {
            oldEntry.PropertyChanging -= OnConfigEntryChanging;
            oldEntry.PropertyChanged -= OnConfigEntryChanged;
        }
        ConfigEntries.Clear();
        if (_groupUpdateTimer != null) { _groupUpdateTimer.Stop(); }
        UpdateGroupedEntries();

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

        // WorkingDirectory 一定有效（RefreshServerListAsync 已保证）：直接扫，无兜底分支
        // 注意：OnServerChanged 是同步回调无法 await，用 _ = fire-and-forget；
        // ScanDirectoryForConfigFilesAsync 内部 ConfigFiles/ConfigFileTree 都是赋值型替换，
        // 切换服务器时只是覆盖结果，不会死锁。
        _ = ScanDirectoryForConfigFilesAsync(value.WorkingDirectory);
    }

    /// <summary>
    /// 直接递归扫描 rootPath 下所有符合扩展名的配置文件 → 赋值 ConfigFiles + ConfigFileTree。
    /// 极简 + 用户要求排序：
    ///   · 递归枚举每个目录：先子目录（A-Z），后文件（A-Z）
    ///   · 扩展名只和 ConfigExtensions 白名单比
    ///   · 不跳过任何目录、无版本号、无锁
    ///   · 任何异常：Log.Error + ConfigFiles=[], ConfigFileTree=[]，不吞静默。
    /// </summary>
    public async Task ScanDirectoryForConfigFilesAsync(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            ConfigFiles = [];
            ConfigFileTree = [];
            OnPropertyChanged(nameof(ConfigFileCountText));
            OnPropertyChanged(nameof(HasServerDirectory));
            return;
        }

        Log.Information("🔍 极简扫描配置文件: Root={Path}", rootPath);

        try
        {
            var (flat, tree) = await Task.Run(() =>
            {
                var flatList = new List<string>();
                var treeRoot = new List<ConfigFileItem>();
                ScanDirectoryRecursive(rootPath, rootPath, flatList, treeRoot);
                return (flatList, treeRoot);
            }).ConfigureAwait(true);

            ConfigFiles = flat;
            ConfigFileTree = tree;
            Log.Information("✅ 极简扫描完成：找到 {Count} 个配置文件", flat.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "极简扫描配置文件目录失败: Root={Path}, Message={Msg}", rootPath, ex.Message);
            ConfigFiles = [];
            ConfigFileTree = [];
        }
        finally
        {
            OnPropertyChanged(nameof(ConfigFileCountText));
            OnPropertyChanged(nameof(HasServerDirectory));
        }
    }

    /// <summary>
    /// 递归扫描单个目录：先列出子目录（A-Z），再列出符合扩展名的文件（A-Z）。
    /// flatList 顺序 = 树深度优先遍历顺序，与 UI 树展开顺序一致。
    /// </summary>
    private static void ScanDirectoryRecursive(
        string rootPath,
        string currentDir,
        List<string> flatList,
        List<ConfigFileItem> treeChildren)
    {
        // ── 子目录（A-Z 排序，忽略大小写）
        string[] subDirs;
        try { subDirs = Directory.GetDirectories(currentDir); }
        catch (UnauthorizedAccessException) { return; }
        catch (DirectoryNotFoundException) { return; }

        Array.Sort(subDirs, StringComparer.OrdinalIgnoreCase);

        foreach (var subDir in subDirs)
        {
            var dirName = Path.GetFileName(subDir);
            var relPath = Path.GetRelativePath(rootPath, subDir);
            var dirNode = new ConfigFileItem(
                dirName,
                fullPath: subDir,
                relativePath: relPath,
                isDirectory: true);
            treeChildren.Add(dirNode);

            // 递归：子目录内容追加到 dirNode.Children
            ScanDirectoryRecursive(rootPath, subDir, flatList, dirNode.Children);
        }

        // ── 文件（A-Z 排序，仅扩展名在白名单中的）
        string[] files;
        try { files = Directory.GetFiles(currentDir); }
        catch (UnauthorizedAccessException) { return; }
        catch (DirectoryNotFoundException) { return; }

        var matchedFiles = files
            .Where(f => ConfigExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in matchedFiles)
        {
            var fileName = Path.GetFileName(file);
            var relPath = Path.GetRelativePath(rootPath, file);
            flatList.Add(relPath);
            treeChildren.Add(new ConfigFileItem(
                fileName,
                fullPath: file,
                relativePath: relPath,
                isDirectory: false));
        }
    }

    /// <summary>
    /// 把扁平相对路径列表构造成「单层文件树」—— 兜底工具方法（保留供外部调用）。
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
                fullPath: p,
                relativePath: p,
                isDirectory: false));
        }
        return tree;
    }

    #endregion

    #region 配置文件加载（极简版：直接 await 解析 → 单循环写条目；取消/超时/锁 都删掉）

    /// <summary>
    /// 选中配置文件变更回调（极简）：
    ///   ① 条目 Clear + 分组立刻刷新
    ///   ② IsLoading=true
    ///   ③ await LoadConfigAsync —— 完成后 IsLoading=false 由 finally 保证
    /// 不再管：CTS cancel、_lastLoadTask 引用、File.Exists 前一次残留等。
    /// </summary>
    partial void OnSelectedConfigFileChanged(string? value)
    {
        Log.Debug("📄 选中配置文件: {File}", value);
        OnPropertyChanged(nameof(SelectedConfigFileName));

        foreach (var oldEntry in ConfigEntries)
        {
            oldEntry.PropertyChanging -= OnConfigEntryChanging;
            oldEntry.PropertyChanged -= OnConfigEntryChanged;
        }
        ConfigEntries.Clear();
        if (_groupUpdateTimer != null) { _groupUpdateTimer.Stop(); }
        UpdateGroupedEntries();

        IsLoading = false; LoadProgress = 0;

        if (Server is null || string.IsNullOrEmpty(value))
            return;

        var fullPath = Path.Combine(Server.WorkingDirectory, value);
        if (!File.Exists(fullPath))
        {
            PushErrorEntry($"文件不存在：{value}");
            return;
        }

        // 不取消前一次、不包 Task.Run——用户真的在同一个 10ms 点两次就跑两次也没关系，
        // 后一次 ConfigEntries.Clear() + Add() 会覆盖前一次，最终一致。
        _ = LoadConfigAsync(fullPath, value);
    }

    /// <summary>
    /// 加载配置文件（极简版）：
    ///   1. IsLoading=true
    ///   2. await _configManager.ReadConfigAsync(fullPath) 直接调用
    ///   3. foreach 单循环写 ConfigEntries（不分 batch，不 yield，多少条一次塞进去）
    ///   4. 最后 UpdateGroupedEntries()
    /// 任何异常 → catch 块统一 PushErrorEntry；finally 保证 IsLoading=false, LoadProgress=100。
    /// </summary>
    private async Task LoadConfigAsync(string fullPath, string fileName)
    {
        Log.Information("📂 极简加载配置文件: Path={Path}", fullPath);

        IsLoading = true;
        LoadProgress = 0;

        try
        {
            // 1) 读 + 解析（ConfigManager 内部已经三级回退：内容特征+扩展名+逐解析器探测）
            var config = await _configManager.ReadConfigAsync(fullPath).ConfigureAwait(true);

            _currentFilePath = fullPath;
            var pureFileName = Path.GetFileName(fileName);

            // 2) 构造条目（描述符翻译由 GetDescriptor 完成——翻译功能保留）
            var entries = new List<ServerConfigEntry>(config.Count);
            foreach (var kvp in config)
            {
                var descriptor = _configManager.GetDescriptor(kvp.Key, pureFileName);
                entries.Add(new ServerConfigEntry
                {
                    Key = kvp.Key,
                    Value = kvp.Value,
                    OriginalValue = kvp.Value,
                    SourceFile = fileName,
                    IsModified = false,
                    Descriptor = descriptor,
                    IsValid = descriptor is null
                              || _configManager.ValidateValue(kvp.Key, fileName, kvp.Value)
                });
            }

            // 3) 订阅事件 + 一次性写入 ConfigEntries
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

            if (_groupUpdateTimer != null) _groupUpdateTimer.Stop();

            foreach (var entry in entries)
            {
                entry.PropertyChanging += OnConfigEntryChanging;
                entry.PropertyChanged += OnConfigEntryChanged;
                ConfigEntries.Add(entry);
            }
            LoadProgress = 100;
            UpdateGroupedEntries();

            Log.Information("✅ 极简加载完成：{Count} 项配置", entries.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "极简加载失败: Path={Path}, Message={Msg}", fullPath, ex.Message);

            // 错误条目显示功能保留（用户看得到哪里错了）
            ConfigEntries.Clear();
            var userMsg = ex is ConfigParseException cpe
                ? $"{cpe.Message}\n扩展名: {cpe.FileExtension}, 内容长度: {cpe.ContentLength}"
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
    /// 往 ConfigEntries 里压一个 "__ERROR__" 标记条目（功能保留：错误 Alert 可提示）。
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

        GC.SuppressFinalize(this);
        Log.Information("✅ ConfigEditorViewModel 资源释放完成");
    }

    #endregion
}
