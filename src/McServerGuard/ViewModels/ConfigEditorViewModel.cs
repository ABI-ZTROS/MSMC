using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Services;
using McServerGuard.Services.ConfigManagement;
using McServerGuard.Services.ServerDetection;
using Serilog;

namespace McServerGuard.ViewModels;

/// <summary>
/// 配置文件项 —— 用于左侧文件树展示
/// </summary>
public sealed class ConfigFileItem
{
    public string FileName { get; init; }
    public string FullPath { get; init; }
    public string RelativePath { get; init; }
    public bool IsDirectory { get; init; }
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
/// 配置项分组 —— 用于 UI 的 Expander 展示
/// </summary>
public sealed class ConfigEntryGroup
{
    public string Key { get; init; }
    public List<ServerConfigEntry> Items { get; init; }

    public ConfigEntryGroup(string key, List<ServerConfigEntry> items)
    {
        Key = key;
        Items = items;
    }
}

/// <summary>
/// ⚙️ 配置编辑器 ViewModel —— "改配置就像改菜单，所见即所得"
/// 
/// 负责加载、编辑、保存 Minecraft 服务器的配置文件。
/// 支持加载 ServerConfigDescriptor（中文说明+约束验证），
/// 让用户知道每个参数到底是干嘛的，而不是对着 server.properties 一脸懵。
/// 
/// 增强：支持选择服务器 + 目录遍历 + 递归扫描配置文件
/// </summary>
public partial class ConfigEditorViewModel : ObservableObject
{
    private readonly IConfigManager _configManager;
    private readonly IServerDetector? _serverDetector;
    private readonly IAppConfigService? _appConfigService;

    /// <summary>记住原始配置的副本 —— 用于"撤销修改"（ResetChanges）</summary>
    private Dictionary<string, string> _originalConfig = new();

    /// <summary>当前正在编辑的配置文件完整路径（LoadConfig 时记录）</summary>
    private string _currentFilePath = string.Empty;

    /// <summary>取消之前未完成的加载任务 —— 防止快速切换文件时的竞态</summary>
    private CancellationTokenSource? _loadCts;

    /// <summary>记录最后一次配置加载任务 —— 避免 fire-and-forget 扫描误报</summary>
    private Task? _lastLoadTask;

    /// <summary>分组更新防抖计时器 —— 避免每次 Add 都重新计算分组</summary>
    private System.Timers.Timer? _groupUpdateTimer;

    public ConfigEditorViewModel(IConfigManager configManager)
    {
        Log.Information("⚙️ ConfigEditorViewModel 初始化");
        _configManager = configManager;

        _groupUpdateTimer = new System.Timers.Timer(20);
        _groupUpdateTimer.AutoReset = false;
        _groupUpdateTimer.Elapsed += (s, e) => UpdateGroupedEntries();

        ConfigEntries.CollectionChanged += (s, e) => ScheduleGroupUpdate();
    }

    public ConfigEditorViewModel(
        IConfigManager configManager,
        IServerDetector serverDetector,
        IAppConfigService appConfigService) : this(configManager)
    {
        _serverDetector = serverDetector;
        _appConfigService = appConfigService;
    }

    // ─── 核心属性 ────────────────────────────────────────────────────

    /// <summary>
    /// 可选服务器列表（运行中的 + 已知的）
    /// </summary>
    [ObservableProperty]
    private List<ServerInstance> _availableServers = [];

    /// <summary>
    /// 当前选中的服务器名称（用于下拉框显示）
    /// </summary>
    [ObservableProperty]
    private string? _selectedServerName;

    /// <summary>
    /// 当前服务器的工作目录
    /// </summary>
    [ObservableProperty]
    private string _serverWorkingDirectory = string.Empty;

    /// <summary>
    /// 配置文件树（递归扫描后的目录结构）
    /// </summary>
    [ObservableProperty]
    private List<ConfigFileItem> _configFileTree = [];

    /// <summary>
    /// 当前操作的服务器实例
    /// 设置后会自动刷新 ConfigFiles 列表
    /// </summary>
    [ObservableProperty]
    private ServerInstance? _server;

    /// <summary>
    /// 配置文件列表 —— 从 Server.ConfigFiles 中过滤出"文件"（排除目录）
    /// 
    /// 为什么要排除目录？因为 Server.ConfigFiles 里可能有 "mods/" 这种目录，
    /// 你总不能打开一个目录来编辑吧...虽然也不一定不能，但咱不做这种奇怪的事 🤔
    /// </summary>
    [ObservableProperty]
    private List<string> _configFiles = [];

    /// <summary>
    /// 当前选中的配置文件名 —— 用户在左侧文件列表里选了哪个
    /// 选中后自动加载该文件的配置内容
    /// </summary>
    [ObservableProperty]
    private string? _selectedConfigFile;

    /// <summary>
    /// 当前配置文件的所有条目 —— 核心数据！
    /// 每个 ServerConfigEntry 包含 Key、Value、Descriptor 等信息，
    /// UI 上就是一个可编辑的配置项列表
    /// 使用 ObservableCollection 支持增量添加，实现逐条动画入场
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServerConfigEntry> _configEntries = [];

    /// <summary>
    /// 按分类分组的配置项 —— 用于 UI 的 Expander 展示
    /// 使用明确的分组模型，避免 IGrouping 在 WPF 绑定中被当作 string 迭代为 char
    /// 缓存计算结果，只在 ConfigEntries 变化时重新计算
    /// </summary>
    [ObservableProperty]
    private List<ConfigEntryGroup> _groupedConfigEntries = [];

    /// <summary>
    /// 配置文件数量统计文本
    /// </summary>
    public string ConfigFileCountText => ConfigFiles.Count > 0
        ? $"共 {ConfigFiles.Count} 个配置文件"
        : "未找到配置文件";

    /// <summary>
    /// 是否有选中的服务器目录
    /// </summary>
    public bool HasServerDirectory => !string.IsNullOrEmpty(ServerWorkingDirectory) && Directory.Exists(ServerWorkingDirectory);

    // ─── 服务器选择和目录扫描命令 ─────────────────────────────────────

    /// <summary>
    /// 刷新可用服务器列表
    /// </summary>
    [RelayCommand]
    private async Task RefreshServerListAsync()
    {
        Log.Information("🔄 刷新配置编辑器的服务器列表...");
        var servers = new List<ServerInstance>();

        try
        {
            // 从检测器获取运行中的服务器
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

        // 从已知服务器添加
        if (_appConfigService != null)
        {
            foreach (var ks in _appConfigService.GetAllKnownServers())
            {
                if (!string.IsNullOrEmpty(ks.WorkingDirectory) && Directory.Exists(ks.WorkingDirectory))
                {
                    // 避免重复（检查是否已经有相同工作目录的）
                    if (!servers.Any(s => string.Equals(s.WorkingDirectory, ks.WorkingDirectory, StringComparison.OrdinalIgnoreCase)))
                    {
                        servers.Add(new ServerInstance
                        {
                            ServerJarName = ks.Name,
                            WorkingDirectory = ks.WorkingDirectory,
                            ServerJarPath = ks.ServerJarPath,
                            ServerPort = ks.Port,
                            ServerType = ServerType.Unknown
                        });
                    }
                }
            }
        }

        AvailableServers = servers;
        Log.Information("✅ 服务器列表刷新完成，共 {Count} 个服务器", servers.Count);
    }

    /// <summary>
    /// 选择指定的服务器并扫描配置文件
    /// </summary>
    partial void OnSelectedServerNameChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        var server = AvailableServers.FirstOrDefault(s =>
            s.DisplayName == value || s.ServerJarName == value);

        if (server != null)
        {
            Server = server;
        }
    }

    /// <summary>
    /// 浏览并选择服务器目录（通过选择 JAR 文件推断）
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

        if (dialog.ShowDialog() == true)
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
    /// 从目录加载服务器配置
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

        // 尝试查找 JAR 文件
        try
        {
            var jarFiles = Directory.GetFiles(path, "*.jar", SearchOption.TopDirectoryOnly);
            if (jarFiles.Length > 0)
            {
                server.ServerJarPath = jarFiles[0];
                server.ServerJarName = Path.GetFileName(jarFiles[0]);
            }
        }
        catch
        {
            // 忽略 JAR 搜索错误
        }

        Server = server;
    }

    /// <summary>
    /// 重新扫描当前服务器目录的配置文件
    /// </summary>
    [RelayCommand]
    private void RescanConfigFiles()
    {
        if (Server == null || string.IsNullOrEmpty(Server.WorkingDirectory)) return;
        ScanDirectoryForConfigFiles(Server.WorkingDirectory);
    }

    /// <summary>
    /// 是否有未保存的修改 —— 用户改了配置但还没保存时的"脏数据"标志
    /// 用来提醒用户"你改的东西还没保存，走了可就丢了哦"
    /// </summary>
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>是否正在加载配置</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>加载进度百分比 (0-100)</summary>
    [ObservableProperty]
    private int _loadProgress;

    // ─── 命令 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 保存配置 —— 把用户修改过的配置写回文件
    /// 只保存 IsModified 为 true 的条目...不对，是全部保存（因为是全量替换）
    /// 
    /// 流程：
    /// 1. 从 ConfigEntries 中提取当前值
    /// 2. 调用 IConfigManager.SaveConfigAsync 写回
    /// 3. 刷新 OriginalConfig 和 IsModified 状态
    /// </summary>
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
            // 📝 构建当前配置字典
            var currentConfig = ConfigEntries
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            // 💾 写回文件 —— IConfigManager 会自动选择合适的序列化器
            await _configManager.SaveConfigAsync(_currentFilePath, currentConfig);

            // 🔄 更新原始配置副本 + 清除修改标记
            _originalConfig = new Dictionary<string, string>(currentConfig);
            foreach (var entry in ConfigEntries)
            {
                entry.IsModified = false;
            }
            HasUnsavedChanges = false;

            Log.Information("✅ 配置保存成功，共保存 {Count} 项配置", currentConfig.Count);
        }
        catch (Exception ex)
        {
            // 😅 保存失败 —— 配置文件可能被服务器锁定了
            System.Diagnostics.Debug.WriteLine($"保存配置失败：{ex.Message}");
            Log.Error(ex, "💥 fuck: 配置保存失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 能不能保存 —— 有未保存修改 + 有选中文件才能保存
    /// </summary>
    private bool CanSaveConfig() => HasUnsavedChanges && !string.IsNullOrEmpty(_currentFilePath);

    /// <summary>
    /// 重置修改 —— "我改错了我不要了给我恢复！"
    /// 从 _originalConfig 中恢复所有配置项的原始值
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanResetChanges))]
    private void ResetChanges()
    {
        Log.Information("🔄 重置所有配置变更");

        foreach (var entry in ConfigEntries)
        {
            if (_originalConfig.TryGetValue(entry.Key, out var originalValue))
            {
                entry.Value = originalValue;
                entry.IsModified = false;
            }
        }

        HasUnsavedChanges = false;
    }

    /// <summary>
    /// 能不能重置 —— 有未保存修改才能重置（没修改你重置个啥）
    /// </summary>
    private bool CanResetChanges() => HasUnsavedChanges;

    // ─── 属性变更响应 ────────────────────────────────────────────────

    /// <summary>
    /// 当配置条目列表变化时 —— 仅做清理工作，分组更新由 CollectionChanged 触发
    /// </summary>
    partial void OnConfigEntriesChanged(ObservableCollection<ServerConfigEntry> value)
    {
    }

    /// <summary>
    /// 调度分组更新 —— 防抖，避免每次 Add 都重新计算
    /// </summary>
    private void ScheduleGroupUpdate()
    {
        if (_groupUpdateTimer != null)
        {
            _groupUpdateTimer.Stop();
            _groupUpdateTimer.Start();
        }
    }

    /// <summary>
    /// 更新分组 —— 在 UI 线程执行
    /// </summary>
    private void UpdateGroupedEntries()
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            GroupedConfigEntries = ConfigEntries
                .GroupBy(e => e.Descriptor?.Category ?? "其他")
                .Select(g => new ConfigEntryGroup(g.Key, g.ToList()))
                .ToList();
        });
    }

    /// <summary>
    /// 当 Server 变化时 —— 刷新配置文件列表（递归扫描目录）
    /// 新服务器来了，它的配置文件列表当然也要换新的
    /// </summary>
    partial void OnServerChanged(ServerInstance? value)
    {
        ConfigEntries.Clear();
        SelectedConfigFile = null;
        _currentFilePath = string.Empty;
        HasUnsavedChanges = false;
        _originalConfig.Clear();

        if (value is null)
        {
            ConfigFiles = [];
            ConfigFileTree = [];
            ServerWorkingDirectory = string.Empty;
            return;
        }

        ServerWorkingDirectory = value.WorkingDirectory;
        SelectedServerName = value.DisplayName;

        // 递归扫描服务器目录下的配置文件
        if (!string.IsNullOrEmpty(value.WorkingDirectory) && Directory.Exists(value.WorkingDirectory))
        {
            ScanDirectoryForConfigFiles(value.WorkingDirectory);
        }
        else
        {
            // 回退到旧方式：从 Server.ConfigFiles 中获取
            ConfigFiles = value.ConfigFiles
                .Where(f => !f.EndsWith('/') && !f.EndsWith('\\'))
                .Select(f => Path.GetRelativePath(value.WorkingDirectory, f))
                .ToList();
        }

        OnPropertyChanged(nameof(ConfigFileCountText));
        OnPropertyChanged(nameof(HasServerDirectory));
    }

    /// <summary>
    /// 递归扫描目录，查找所有配置文件
    /// 支持的格式：.properties, .yml, .yaml, .json, .cfg, .conf, .toml
    /// </summary>
    private void ScanDirectoryForConfigFiles(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            ConfigFiles = [];
            ConfigFileTree = [];
            return;
        }

        Log.Information("🔍 递归扫描配置文件目录: {Path}", rootPath);

        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".properties", ".yml", ".yaml", ".json", ".cfg", ".conf",
            ".toml", ".ini", ".txt"
        };

        var flatList = new List<string>();
        var treeRoot = new List<ConfigFileItem>();

        try
        {
            BuildConfigFileTree(rootPath, rootPath, supportedExtensions, treeRoot, flatList, 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "扫描配置文件目录失败: {Message}", ex.Message);
        }

        ConfigFiles = flatList;
        ConfigFileTree = treeRoot;
        OnPropertyChanged(nameof(ConfigFileCountText));

        Log.Information("✅ 扫描完成，找到 {Count} 个配置文件", flatList.Count);
    }

    /// <summary>
    /// 递归构建配置文件树
    /// </summary>
    private static void BuildConfigFileTree(
        string currentPath,
        string rootPath,
        HashSet<string> supportedExtensions,
        List<ConfigFileItem> parentList,
        List<string> flatList,
        int depth)
    {
        // 限制最大深度，防止目录太深或符号链接循环
        if (depth > 10) return;

        try
        {
            // 先添加子目录
            var directories = Directory.GetDirectories(currentPath);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);

                // 跳过一些常见的非配置目录
                if (dirName.Equals("mods", StringComparison.OrdinalIgnoreCase) && depth > 0) continue;
                if (dirName.Equals("world", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("world_nether", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("world_the_end", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("logs", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("cache", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("libraries", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.StartsWith('.')) continue; // 隐藏目录

                var dirItem = new ConfigFileItem(
                    dirName,
                    dir,
                    Path.GetRelativePath(rootPath, dir),
                    isDirectory: true);

                BuildConfigFileTree(dir, rootPath, supportedExtensions, dirItem.Children, flatList, depth + 1);

                // 只添加有配置文件的目录（或者是根目录下的一级目录）
                if (dirItem.Children.Count > 0 || depth == 0)
                {
                    parentList.Add(dirItem);
                }
            }

            // 再添加文件
            var files = Directory.GetFiles(currentPath);
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (!supportedExtensions.Contains(ext)) continue;

                var fileName = Path.GetFileName(file);
                var relativePath = Path.GetRelativePath(rootPath, file);

                var fileItem = new ConfigFileItem(
                    fileName,
                    file,
                    relativePath,
                    isDirectory: false);

                parentList.Add(fileItem);
                flatList.Add(relativePath);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 没有权限访问的目录，跳过
        }
        catch (Exception ex)
        {
            Log.Debug("扫描目录 {Path} 时出错: {Message}", currentPath, ex.Message);
        }
    }

    /// <summary>
    /// 当选中的配置文件变化时 —— 加载配置内容
    /// 把文件里的配置项读取出来，附带中文 Descriptor，排列整齐给用户看
    /// </summary>
    partial void OnSelectedConfigFileChanged(string? value)
    {
        Log.Debug("📄 选中配置文件: {File}", value);

        if (Server is null || string.IsNullOrEmpty(value))
        {
            ConfigEntries.Clear();
            return;
        }

        var fullPath = Path.Combine(Server.WorkingDirectory, value);
        if (!File.Exists(fullPath))
        {
            ConfigEntries.Clear();
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _lastLoadTask = LoadConfigAsync(fullPath, value, _loadCts.Token);
    }

    // ─── 私有方法 ────────────────────────────────────────────────────

    /// <summary>
    /// 加载配置文件 —— 异步读取 + 后台预处理 + UI线程逐条动画入场
    /// 后台线程：读取文件、创建所有 ServerConfigEntry（不绑定 UI）
    /// UI线程：逐条添加到 ObservableCollection，触发入场动画
    /// 每批添加多个条目并让出UI线程，保证流畅性
    /// </summary>
    private async Task LoadConfigAsync(string fullPath, string fileName, CancellationToken cancellationToken = default)
    {
        Log.Information("📂 加载配置文件: {Path}", fullPath);

        IsLoading = true;
        LoadProgress = 0;

        try
        {
            // 📖 步骤1：异步读取配置文件
            var config = await _configManager.ReadConfigAsync(fullPath);

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("🔄 加载已取消，丢弃结果: {Path}", fullPath);
                return;
            }

            _currentFilePath = fullPath;
            var pureFileName = Path.GetFileName(fileName);

            // 🔧 步骤2：后台线程预处理所有配置项（不涉及UI绑定）
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
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("🔄 加载已取消，丢弃结果: {Path}", fullPath);
                return;
            }

            // 🧹 清空旧数据
            ConfigEntries.Clear();
            _originalConfig = new Dictionary<string, string>(config);
            HasUnsavedChanges = false;

            // 🎬 步骤3：UI线程逐条添加，触发入场动画
            const int batchSize = 5;
            int total = processedEntries.Count;
            int processed = 0;

            for (int i = 0; i < total; i += batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Log.Debug("🔄 加载已取消: {Path}", fullPath);
                    return;
                }

                var batch = processedEntries.Skip(i).Take(batchSize).ToList();

                foreach (var entry in batch)
                {
                    entry.PropertyChanged += OnConfigEntryChanged;
                    ConfigEntries.Add(entry);
                    processed++;
                    LoadProgress = (int)(processed * 100.0 / total);
                }

                await Task.Delay(8, cancellationToken);
            }

            Log.Information("✅ 配置加载完成，共 {Count} 项配置", total);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("🔄 配置加载被取消: {Path}", fullPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置文件失败 [{fullPath}]：{ex.Message}");
            Log.Error(ex, "💥 fuck: 配置加载失败: {Message}", ex.Message);
            ConfigEntries.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 配置项属性变更回调 —— 用户改了一个值，标记为已修改
    /// 同时验证新值是否合法
    /// </summary>
    private void OnConfigEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ServerConfigEntry entry || e.PropertyName != nameof(ServerConfigEntry.Value))
            return;

        // ✅ 重新验证
        entry.IsValid = entry.Descriptor is null ||
                        _configManager.ValidateValue(entry.Key, entry.SourceFile, entry.Value);

        // 📝 检查是否有任何未保存的修改
        HasUnsavedChanges = ConfigEntries.Any(ce => ce.IsModified);
    }
}
