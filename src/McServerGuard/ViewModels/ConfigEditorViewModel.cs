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
using System.IO;
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
public partial class ConfigEditorViewModel : ObservableObject
{
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

    /// <summary>撤销操作进行中标志 —— 防止撤销恢复值时再次触发压栈</summary>
    private bool _isUndoing;

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
        _groupUpdateTimer.Elapsed += (s, e) => UpdateGroupedEntries();

        ConfigEntries.CollectionChanged += (s, e) => ScheduleGroupUpdate();
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
    /// <remarks>
    /// 采用显式分组模型而非 IGrouping，避免 WPF 绑定中字符串枚举为字符的问题。
    /// 通过防抖计时器延迟计算，减少频繁 Add 操作导致的性能损耗。
    /// </remarks>
    [ObservableProperty]
    private List<ConfigEntryGroup> _groupedConfigEntries = [];

    /// <summary>配置文件数量统计文本</summary>
    public string ConfigFileCountText => ConfigFiles.Count > 0
        ? $"共 {ConfigFiles.Count} 个配置文件"
        : "未找到配置文件";

    /// <summary>获取一个值，指示当前是否存在有效的服务器工作目录</summary>
    public bool HasServerDirectory => !string.IsNullOrEmpty(ServerWorkingDirectory) && Directory.Exists(ServerWorkingDirectory);

    /// <summary>
    /// 刷新可用服务器列表命令
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    /// <remarks>
    /// 触发条件：用户点击刷新按钮或 ViewModel 初始化时。
    /// 副作用：从检测服务与应用配置服务聚合服务器列表，更新 <see cref="AvailableServers"/>。
    /// </remarks>
    [RelayCommand]
    private async Task RefreshServerListAsync()
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
    /// 选中服务器名称变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的服务器名称</param>
    /// <remarks>根据名称从可用服务器列表中匹配并设置 <see cref="Server"/>。</remarks>
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
    /// 浏览并选择服务器目录命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击浏览按钮。
    /// 副作用：打开文件选择对话框，通过 JAR 文件推断服务器目录并加载配置。
    /// </remarks>
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
    /// 从目录加载服务器实例
    /// </summary>
    /// <param name="path">服务器根目录路径</param>
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
        catch
        {
        }

        Server = server;
    }

    /// <summary>
    /// 重新扫描配置文件命令
    /// </summary>
    /// <remarks>
    /// 触发条件：用户点击重新扫描按钮。
    /// 副作用：递归扫描当前服务器工作目录，更新配置文件列表与目录树。
    /// </remarks>
    [RelayCommand]
    private void RescanConfigFiles()
    {
        if (Server == null || string.IsNullOrEmpty(Server.WorkingDirectory)) return;
        ScanDirectoryForConfigFiles(Server.WorkingDirectory);
    }

    /// <summary>
    /// 获取或设置一个值，指示当前是否存在未保存的变更
    /// </summary>
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>保存操作的状态消息（成功/失败提示）</summary>
    [ObservableProperty]
    private string? _saveStatusMessage;

    /// <summary>指示保存状态消息是否为错误类型</summary>
    [ObservableProperty]
    private bool _isSaveError;

    /// <summary>是否正在加载配置文件</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>配置加载进度百分比（0-100）</summary>
    [ObservableProperty]
    private int _loadProgress;

    /// <summary>
    /// 保存配置命令
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    /// <remarks>
    /// 触发条件：<see cref="CanSaveConfig"/> 返回 true 且用户点击保存按钮。
    /// 副作用：将当前所有配置条目序列化写入磁盘，重置脏数据标记与各条目修改状态。
    /// 执行流程：构建配置字典 → 调用 <see cref="IConfigManager.SaveConfigAsync"/> 持久化 → 重置修改状态。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSaveConfig))]
    private async Task SaveConfigAsync()
    {
        if (Server is null || string.IsNullOrEmpty(_currentFilePath))
        {
            Log.Debug("🔄 SaveConfig 跳过: Server 为空或路径为空");
            return;
        }

        Log.Information("💾 开始保存配置到 {Path}", _currentFilePath);

        // 保存前检查文件是否被占用
        try
        {
            using var fs = new FileStream(
                _currentFilePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.Read);
        }
        catch (IOException ioEx)
        {
            IsSaveError = true;
            SaveStatusMessage = $"文件被占用，保存失败：{ioEx.Message}（请关闭正在使用该文件的程序，如服务器进程或文本编辑器）";
            Log.Warning(ioEx, "⚠️ 配置文件被占用: {Path}", _currentFilePath);
            return;
        }
        catch (UnauthorizedAccessException authEx)
        {
            IsSaveError = true;
            SaveStatusMessage = $"权限不足，保存失败：{authEx.Message}";
            Log.Warning(authEx, "⚠️ 配置文件无写入权限: {Path}", _currentFilePath);
            return;
        }

        try
        {
            var currentConfig = ConfigEntries
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            await _configManager.SaveConfigAsync(_currentFilePath, currentConfig);

            _originalConfig = new Dictionary<string, string>(currentConfig);
            foreach (var entry in ConfigEntries)
            {
                entry.IsModified = false;
            }
            HasUnsavedChanges = false;
            _undoStack.Clear();
            UndoCommand.NotifyCanExecuteChanged();

            IsSaveError = false;
            SaveStatusMessage = $"配置已保存，共 {currentConfig.Count} 项";

            Log.Information("✅ 配置保存成功，共保存 {Count} 项配置", currentConfig.Count);
        }
        catch (IOException ex)
        {
            IsSaveError = true;
            SaveStatusMessage = $"保存失败：{ex.Message}（文件可能被其他程序占用）";
            Log.Error(ex, "💥 配置保存失败（IO异常）: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            IsSaveError = true;
            SaveStatusMessage = $"保存失败：{ex.Message}";
            Log.Error(ex, "💥 配置保存失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 判断是否可执行保存命令
    /// </summary>
    /// <returns>若存在未保存变更且当前有选中文件则返回 true</returns>
    private bool CanSaveConfig() => HasUnsavedChanges && !string.IsNullOrEmpty(_currentFilePath);

    /// <summary>
    /// 重置配置变更命令
    /// </summary>
    /// <remarks>
    /// 触发条件：<see cref="CanResetChanges"/> 返回 true 且用户点击重置按钮。
    /// 副作用：从原始配置快照恢复所有条目的值，清除脏数据标记。
    /// </remarks>
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
        _undoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        SaveStatusMessage = null;
    }

    /// <summary>
    /// 判断是否可执行重置命令
    /// </summary>
    /// <returns>若存在未保存变更则返回 true</returns>
    private bool CanResetChanges() => HasUnsavedChanges;

    /// <summary>
    /// 撤销最近一次配置编辑命令
    /// </summary>
    /// <remarks>
    /// 从编辑历史栈弹出最近一次变更，将该条目的值恢复为变更前的值。
    /// 支持连续多次撤销，直至栈空。
    /// </remarks>
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
        finally
        {
            _isUndoing = false;
        }

        HasUnsavedChanges = ConfigEntries.Any(ce => ce.IsModified);
        UndoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 判断是否可执行撤销命令
    /// </summary>
    /// <returns>若编辑历史栈非空则返回 true</returns>
    private bool CanUndo() => _undoStack.Count > 0;

    /// <summary>
    /// 配置条目集合变更回调 —— 由源生成器在属性赋值时调用
    /// </summary>
    /// <param name="value">新的条目集合</param>
    partial void OnConfigEntriesChanged(ObservableCollection<ServerConfigEntry> value)
    {
    }

    /// <summary>
    /// 调度分组更新（防抖机制）
    /// </summary>
    /// <remarks>重置防抖计时器，延迟触发分组重新计算以避免高频 Add 操作的性能损耗。</remarks>
    private void ScheduleGroupUpdate()
    {
        if (_groupUpdateTimer != null)
        {
            _groupUpdateTimer.Stop();
            _groupUpdateTimer.Start();
        }
    }

    /// <summary>
    /// 更新分组后的配置条目集合
    /// </summary>
    /// <remarks>在 UI 线程上执行，按配置条目分类重新分组并更新 <see cref="GroupedConfigEntries"/>。</remarks>
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
    /// Server 属性变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新的服务器实例</param>
    /// <remarks>
    /// 切换服务器时清空当前配置状态，递归扫描新服务器目录下的配置文件，
    /// 并更新 <see cref="ConfigFiles"/>、<see cref="ConfigFileTree"/> 及相关派生属性。
    /// </remarks>
    partial void OnServerChanged(ServerInstance? value)
    {
        ConfigEntries.Clear();
        SelectedConfigFile = null;
        _currentFilePath = string.Empty;
        HasUnsavedChanges = false;
        _undoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
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

        if (!string.IsNullOrEmpty(value.WorkingDirectory) && Directory.Exists(value.WorkingDirectory))
        {
            ScanDirectoryForConfigFiles(value.WorkingDirectory);
        }
        else
        {
            ConfigFiles = value.ConfigFiles
                .Where(f => !f.EndsWith('/') && !f.EndsWith('\\'))
                .Select(f => Path.GetRelativePath(value.WorkingDirectory, f))
                .ToList();
        }

        OnPropertyChanged(nameof(ConfigFileCountText));
        OnPropertyChanged(nameof(HasServerDirectory));
    }

    /// <summary>
    /// 递归扫描目录以构建配置文件列表与目录树
    /// </summary>
    /// <param name="rootPath">根目录路径</param>
    /// <remarks>
    /// 支持格式：.properties、.yml、.yaml、.json、.cfg、.conf、.toml、.ini、.txt。
    /// 自动跳过 mods、world、logs、cache、libraries 等非配置目录与隐藏目录。
    /// </remarks>
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
    /// 递归构建配置文件目录树
    /// </summary>
    /// <param name="currentPath">当前扫描目录</param>
    /// <param name="rootPath">根目录路径</param>
    /// <param name="supportedExtensions">支持的文件扩展名集合</param>
    /// <param name="parentList">父级节点列表</param>
    /// <param name="flatList">扁平文件路径列表</param>
    /// <param name="depth">当前递归深度</param>
    /// <remarks>最大递归深度限制为 10，防止符号链接循环或深层目录导致的栈溢出。</remarks>
    private static void BuildConfigFileTree(
        string currentPath,
        string rootPath,
        HashSet<string> supportedExtensions,
        List<ConfigFileItem> parentList,
        List<string> flatList,
        int depth)
    {
        if (depth > 10) return;

        try
        {
            var directories = Directory.GetDirectories(currentPath);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);

                if (dirName.Equals("mods", StringComparison.OrdinalIgnoreCase) && depth > 0) continue;
                if (dirName.Equals("world", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("world_nether", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("world_the_end", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("logs", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("cache", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.Equals("libraries", StringComparison.OrdinalIgnoreCase)) continue;
                if (dirName.StartsWith('.')) continue;

                var dirItem = new ConfigFileItem(
                    dirName,
                    dir,
                    Path.GetRelativePath(rootPath, dir),
                    isDirectory: true);

                BuildConfigFileTree(dir, rootPath, supportedExtensions, dirItem.Children, flatList, depth + 1);

                if (dirItem.Children.Count > 0 || depth == 0)
                {
                    parentList.Add(dirItem);
                }
            }

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
        }
        catch (Exception ex)
        {
            Log.Debug("扫描目录 {Path} 时出错: {Message}", currentPath, ex.Message);
        }
    }

    /// <summary>
    /// 选中配置文件变更回调 —— 由源生成器在属性变更时调用
    /// </summary>
    /// <param name="value">新选中的配置文件相对路径</param>
    /// <remarks>
    /// 切换文件时取消上一次未完成的加载任务，启动新的异步加载流程。
    /// 通过 <see cref="LoadConfigAsync"/> 实现分批条目加载与进度反馈。
    /// </remarks>
    partial void OnSelectedConfigFileChanged(string? value)
    {
        Log.Debug("📄 选中配置文件: {File}", value);
        OnPropertyChanged(nameof(SelectedConfigFileName));

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

    /// <summary>
    /// 异步加载配置文件
    /// </summary>
    /// <param name="fullPath">配置文件完整路径</param>
    /// <param name="fileName">配置文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    /// <remarks>
    /// 执行流程：
    /// 1. 调用 <see cref="IConfigManager.ReadConfigAsync"/> 读取原始配置；
    /// 2. 后台线程预处理条目（附加描述符与验证状态）；
    /// 3. UI 线程分批添加至 <see cref="ConfigEntries"/>，触发入场动画与进度更新。
    /// 支持通过 <paramref name="cancellationToken"/> 取消加载。
    /// </remarks>
    private async Task LoadConfigAsync(string fullPath, string fileName, CancellationToken cancellationToken = default)
    {
        Log.Information("📂 加载配置文件: {Path}", fullPath);

        IsLoading = true;
        LoadProgress = 0;

        try
        {
            var config = await _configManager.ReadConfigAsync(fullPath);

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
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("🔄 加载已取消，丢弃结果: {Path}", fullPath);
                return;
            }

            ConfigEntries.Clear();
            _originalConfig = new Dictionary<string, string>(config);
            HasUnsavedChanges = false;
            _undoStack.Clear();
            UndoCommand.NotifyCanExecuteChanged();

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
                    entry.PropertyChanging += OnConfigEntryChanging;
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
    /// 配置条目属性变更前事件处理程序 —— 在值改变前将旧值压入撤销栈
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">属性变更前事件参数</param>
    private void OnConfigEntryChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
    {
        if (sender is not ServerConfigEntry entry || e.PropertyName != nameof(ServerConfigEntry.Value))
            return;

        if (!_isUndoing)
        {
            _undoStack.Push((entry, entry.Value));
        }
    }

    /// <summary>
    /// 配置条目属性变更事件处理程序
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">属性变更事件参数</param>
    /// <remarks>
    /// 监听 <see cref="ServerConfigEntry.Value"/> 变更，触发值验证与脏数据状态更新。
    /// </remarks>
    private void OnConfigEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ServerConfigEntry entry || e.PropertyName != nameof(ServerConfigEntry.Value))
            return;

        entry.IsModified = !string.Equals(entry.Value, entry.OriginalValue, StringComparison.Ordinal);

        entry.IsValid = entry.Descriptor is null ||
                        _configManager.ValidateValue(entry.Key, entry.SourceFile, entry.Value);

        HasUnsavedChanges = ConfigEntries.Any(ce => ce.IsModified);
        UndoCommand.NotifyCanExecuteChanged();
    }
}
