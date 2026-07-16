using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Services.ConfigManagement;
using Serilog;

namespace McServerGuard.ViewModels;

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
/// </summary>
public partial class ConfigEditorViewModel : ObservableObject
{
    private readonly IConfigManager _configManager;

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

    // ─── 核心属性 ────────────────────────────────────────────────────

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
    /// 当 Server 变化时 —— 刷新配置文件列表
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
            return;
        }

        ConfigFiles = value.ConfigFiles
            .Where(f => !f.EndsWith('/') && !f.EndsWith('\\'))
            .Select(f => Path.GetRelativePath(value.WorkingDirectory, f))
            .ToList();
        OnPropertyChanged(nameof(ConfigFileCountText));
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
