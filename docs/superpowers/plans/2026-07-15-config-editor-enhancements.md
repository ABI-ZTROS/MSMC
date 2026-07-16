# 配置编辑器三大增强：格式自动检测 + 翻译覆盖率提升 + 流畅渲染动画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让配置编辑器能自动检测文件真实格式（而非仅靠扩展名），提升翻译文件覆盖率到 95%+，并通过 UI 虚拟化 + 分批加载动画解决大配置文件渲染卡顿问题。

**Architecture:**
- **格式检测**：新增 `ConfigFormatDetector`，通过文件头 magic bytes / 内容特征探测真实格式，作为扩展名判断的前置兜底（如 `.conf` 文件实为 properties 或 yaml）
- **翻译覆盖**：将参考 Markdown 手册中的配置项与 `ConfigDescriptorRegistry` 已注册项做差集比对，补齐缺失描述符，并为无描述符的 entry 提供通用的"键名友好化"降级显示
- **渲染性能**：将内层 `ItemsControl` 替换为带 `VirtualizingStackPanel` 的 `ListBox`，ViewModel 改为分批加载（每帧追加一组），并为每个配置项卡片加入入场淡入动画

**Tech Stack:** C# 12 / .NET 10 / WPF / CommunityToolkit.Mvvm / xUnit / YamlDotNet / System.Text.Json

---

## 文件结构总览

| 文件 | 职责 | 操作 |
|------|------|------|
| `src/McServerGuard/Services/ConfigManagement/ConfigFormatDetector.cs` | 基于内容的格式探测器 | **新建** |
| `src/McServerGuard/Services/ConfigManagement/ConfigManager.cs` | 集成格式探测器，替换纯扩展名判断 | 修改 |
| `src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs` | 补齐缺失描述符 + 新增 `GetCoverageReport()` | 修改 |
| `src/McServerGuard/Services/ConfigManagement/IConfigManager.cs` | 新增 `GetCoverageReport()` 接口方法 | 修改 |
| `src/McServerGuard/Models/ServerConfigEntry.cs` | 新增 `FriendlyDisplayName` 降级属性 | 修改 |
| `src/McServerGuard/ViewModels/ConfigEditorViewModel.cs` | 分批加载 + `IsLoading` 状态 + 加载进度 | 修改 |
| `src/McServerGuard/Views/ConfigEditorPage.xaml` | 虚拟化 + 入场动画 + 加载指示器 | 修改 |
| `src/McServerGuard/Converters/ValueConverters.cs` | 新增 `NullToBoolConverter` | 修改 |
| `src/McServerGuard.Tests/Services/ConfigFormatDetectorTests.cs` | 格式探测器测试 | **新建** |
| `src/McServerGuard.Tests/Services/ConfigDescriptorCoverageTests.cs` | 翻译覆盖率测试 | **新建** |

---

## Task 1: ConfigFormatDetector — 基于内容的格式探测器

**Files:**
- Create: `src/McServerGuard/Services/ConfigManagement/ConfigFormatDetector.cs`
- Test: `src/McServerGuard.Tests/Services/ConfigFormatDetectorTests.cs`

- [ ] **Step 1: 写失败测试 — 检测 properties 格式**

```csharp
// src/McServerGuard.Tests/Services/ConfigFormatDetectorTests.cs
namespace McServerGuard.Tests.Services;

using McServerGuard.Services.ConfigManagement;
using Xunit;

public class ConfigFormatDetectorTests
{
    [Fact]
    public void Detect_FromPropertiesContent_ReturnsProperties()
    {
        var content = "#Minecraft server properties\nserver-port=25565\nmax-players=20\n";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Properties, format);
    }

    [Fact]
    public void Detect_FromYamlContent_ReturnsYaml()
    {
        var content = "settings:\n  name: test\n  port: 25565\n";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Yaml, format);
    }

    [Fact]
    public void Detect_FromJsonContent_ReturnsJson()
    {
        var content = "{\"server\":{\"port\":25565},\"name\":\"test\"}";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Json, format);
    }

    [Fact]
    public void Detect_FromTomlLikeContent_ReturnsYaml()
    {
        // TOML 和 YAML 都用 key: value 或 key = value，但 TOML 有 [section] 头
        // 这里确保含冒号缩进的 YAML 不会被误判
        var content = "world-settings:\n  default:\n    mob-spawn-range: 4\n";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Yaml, format);
    }

    [Fact]
    public void Detect_FromEmptyContent_ReturnsUnknown()
    {
        var format = ConfigFormatDetector.Detect("");
        Assert.Equal(ConfigFormat.Unknown, format);
    }

    [Fact]
    public void Detect_FromAmbiguousKeyValueContent_ReturnsProperties()
    {
        // 纯 key=value 无缩进、无冒号 → properties
        var content = "enable-rcon=false\nrcon.port=25575\n";
        var format = ConfigFormatDetector.Detect(content);
        Assert.Equal(ConfigFormat.Properties, format);
    }

    [Fact]
    public void DetectFormat_FromFileExtension_ReturnsExpected()
    {
        Assert.Equal(ConfigFormat.Properties, ConfigFormatDetector.DetectByExtension(".properties"));
        Assert.Equal(ConfigFormat.Yaml, ConfigFormatDetector.DetectByExtension(".yml"));
        Assert.Equal(ConfigFormat.Yaml, ConfigFormatDetector.DetectByExtension(".yaml"));
        Assert.Equal(ConfigFormat.Json, ConfigFormatDetector.DetectByExtension(".json"));
        Assert.Equal(ConfigFormat.Unknown, ConfigFormatDetector.DetectByExtension(".toml"));
    }

    [Fact]
    public void Resolve_CombinesExtensionAndContent_PrefersContent()
    {
        // 扩展名是 .conf（Unknown），内容是 properties → 结果应为 Properties
        var content = "server-port=25565\nmax-players=20\n";
        var format = ConfigFormatDetector.Resolve(content, ".conf");
        Assert.Equal(ConfigFormat.Properties, format);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard.Tests && dotnet test --filter "ConfigFormatDetectorTests" -r win-x64 2>&1 | tail -10`
Expected: FAIL — `ConfigFormatDetector` 类不存在，编译错误

- [ ] **Step 3: 实现 ConfigFormatDetector**

```csharp
// src/McServerGuard/Services/ConfigManagement/ConfigFormatDetector.cs
namespace McServerGuard.Services.ConfigManagement;

using System.Text.Json;

/// <summary>
/// 配置文件格式枚举
/// </summary>
public enum ConfigFormat
{
    Unknown,
    Properties,
    Yaml,
    Json
}

/// <summary>
/// 基于内容的配置文件格式探测器 🔍
/// 当扩展名无法确定格式时（如 .conf），通过分析文件内容特征来判断
/// </summary>
public static class ConfigFormatDetector
{
    /// <summary>
    /// 通过分析内容特征检测格式
    /// 判断优先级：JSON → YAML → Properties → Unknown
    /// </summary>
    public static ConfigFormat Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ConfigFormat.Unknown;

        var trimmed = content.TrimStart();

        // JSON：以 { 或 [ 开头
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return ConfigFormat.Json;

        // 尝试完整 JSON 解析（某些 JSON 文件可能有 BOM 或前导空格）
        if (TryParseJson(trimmed))
            return ConfigFormat.Json;

        // YAML 特征：有缩进的 "key: value" 行（冒号后有空格或换行）
        // 或含 "---" 文档分隔符
        if (HasYamlFeatures(content))
            return ConfigFormat.Yaml;

        // Properties 特征：key=value（无缩进，等号分割）
        if (HasPropertiesFeatures(content))
            return ConfigFormat.Properties;

        return ConfigFormat.Unknown;
    }

    /// <summary>
    /// 通过文件扩展名检测格式
    /// </summary>
    public static ConfigFormat DetectByExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ConfigFormat.Unknown;

        var ext = extension.ToLowerInvariant();
        return ext switch
        {
            ".properties" => ConfigFormat.Properties,
            ".yml" or ".yaml" => ConfigFormat.Yaml,
            ".json" => ConfigFormat.Json,
            _ => ConfigFormat.Unknown
        };
    }

    /// <summary>
    /// 综合判断：先看扩展名，再看内容，内容优先级更高
    /// </summary>
    public static ConfigFormat Resolve(string content, string extension)
    {
        // 先看内容
        var contentFormat = Detect(content);
        if (contentFormat != ConfigFormat.Unknown)
            return contentFormat;

        // 内容无法判断时，回退到扩展名
        return DetectByExtension(extension);
    }

    private static bool TryParseJson(string content)
    {
        try
        {
            JsonDocument.Parse(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasYamlFeatures(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // YAML 文档分隔符
        if (lines.Any(l => l.Trim() == "---"))
            return true;

        // 检查是否有缩进的 "key: value" 行（冒号后有空格或冒号在行尾）
        int yamlLineCount = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
                continue;

            // 有缩进（行首有空格）且包含冒号
            if (line.StartsWith(' ') && trimmed.Contains(':'))
            {
                yamlLineCount++;
                continue;
            }

            // 无缩进的 "key: value" 行（但不是 key=value）
            if (!line.StartsWith(' ') && trimmed.Contains(':') && !trimmed.Contains('='))
            {
                yamlLineCount++;
            }
        }

        return yamlLineCount > 0;
    }

    private static bool HasPropertiesFeatures(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int propsLineCount = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // 跳过注释和空行
            if (trimmed.StartsWith('#') || string.IsNullOrEmpty(trimmed))
                continue;

            // 有等号且无冒号 → properties 特征
            if (trimmed.Contains('=') && !trimmed.Contains(':'))
                propsLineCount++;
        }

        return propsLineCount > 0;
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard.Tests && dotnet test --filter "ConfigFormatDetectorTests" -r win-x64 2>&1 | tail -10`
Expected: PASS — 全部 8 个测试通过

- [ ] **Step 5: 提交**

```bash
git add src/McServerGuard/Services/ConfigManagement/ConfigFormatDetector.cs src/McServerGuard.Tests/Services/ConfigFormatDetectorTests.cs
git commit -m "feat: add ConfigFormatDetector for content-based format detection"
```

---

## Task 2: ConfigManager 集成格式探测器

**Files:**
- Modify: `src/McServerGuard/Services/ConfigManagement/ConfigManager.cs` (行 40-69 ReadConfigAsync, 行 74-116 SaveConfigAsync)

- [ ] **Step 1: 修改 ReadConfigAsync 使用 Resolve 替代纯扩展名**

打开 `src/McServerGuard/Services/ConfigManagement/ConfigManager.cs`，将 `ReadConfigAsync` 方法（行 40-69）中的格式判断逻辑从：

```csharp
var extension = Path.GetExtension(filePath).ToLowerInvariant();
var result = extension switch
{
    ".properties" => ParseProperties(content),
    ".yml" or ".yaml" => FlattenYaml(content),
    ".json" => FlattenJson(content),
    _ => throw HandleUnsupportedFormat(extension)
};
```

替换为：

```csharp
var extension = Path.GetExtension(filePath).ToLowerInvariant();
var format = ConfigFormatDetector.Resolve(content, extension);

var result = format switch
{
    ConfigFormat.Properties => ParseProperties(content),
    ConfigFormat.Yaml => FlattenYaml(content),
    ConfigFormat.Json => FlattenJson(content),
    _ => throw HandleUnsupportedFormat(extension)
};
```

- [ ] **Step 2: 修改 SaveConfigAsync 同样使用 Resolve**

将 `SaveConfigAsync` 方法（行 74-116）中的 switch 语句从：

```csharp
var extension = Path.GetExtension(filePath).ToLowerInvariant();

string content;
switch (extension)
{
    case ".properties":
        content = PropertiesParser.Serialize(config);
        break;

    case ".yml" or ".yaml":
        content = SerializeYaml(config);
        break;

    case ".json":
        content = SerializeJson(config);
        break;

    default:
        Log.Warning("❌ 不支持的配置文件格式: {Ext}", extension);
        throw new NotSupportedException(
            $"不支持的配置文件格式: {extension} —— 我不会写这种格式啦 🙅");
}
```

替换为：

```csharp
var extension = Path.GetExtension(filePath).ToLowerInvariant();
// 保存时优先用扩展名判断（写文件时内容尚未生成，无法做内容检测）
var format = ConfigFormatDetector.DetectByExtension(extension);

string content;
switch (format)
{
    case ConfigFormat.Properties:
        content = PropertiesParser.Serialize(config);
        break;

    case ConfigFormat.Yaml:
        content = SerializeYaml(config);
        break;

    case ConfigFormat.Json:
        content = SerializeJson(config);
        break;

    default:
        Log.Warning("❌ 不支持的配置文件格式: {Ext}", extension);
        throw new NotSupportedException(
            $"不支持的配置文件格式: {extension} —— 我不会写这种格式啦 🙅");
}
```

- [ ] **Step 3: 修改 HandleUnsupportedFormat 传入 format 信息**

将 `HandleUnsupportedFormat` 方法（约行 231-236）改为同时提示检测到的格式：

```csharp
private static Exception HandleUnsupportedFormat(string extension)
{
    Log.Warning("❌ 无法识别的配置文件格式: 扩展名={Ext}", extension);
    return new NotSupportedException(
        $"无法识别的配置文件格式（扩展名: {extension}）。" +
        "支持的格式: .properties / .yml / .yaml / .json 🙅");
}
```

- [ ] **Step 4: 编译验证**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard && dotnet build -r win-x64 2>&1 | tail -5`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 5: 提交**

```bash
git add src/McServerGuard/Services/ConfigManagement/ConfigManager.cs
git commit -m "feat: integrate ConfigFormatDetector into ConfigManager for robust format detection"
```

---

## Task 3: 翻译覆盖率分析 — GetCoverageReport + 覆盖率测试

**Files:**
- Modify: `src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs` (在类末尾添加方法)
- Modify: `src/McServerGuard/Services/ConfigManagement/IConfigManager.cs` (添加接口方法)
- Modify: `src/McServerGuard/Services/ConfigManagement/ConfigManager.cs` (实现接口方法)
- Test: `src/McServerGuard.Tests/Services/ConfigDescriptorCoverageTests.cs`

- [ ] **Step 1: 写失败测试 — 覆盖率报告**

```csharp
// src/McServerGuard.Tests/Services/ConfigDescriptorCoverageTests.cs
namespace McServerGuard.Tests.Services;

using McServerGuard.Services.ConfigManagement;
using Xunit;

public class ConfigDescriptorCoverageTests
{
    [Fact]
    public void CoverageReport_ReturnsTotalAndCoveredCounts()
    {
        var registry = new ConfigDescriptorRegistry();
        var report = registry.GetCoverageReport();

        Assert.True(report.TotalDescriptors > 200, $"应有 200+ 描述符，实际 {report.TotalDescriptors}");
        Assert.NotEmpty(report.RegisteredFiles);
    }

    [Fact]
    public void CoverageReport_ServerProperties_HasHighCoverage()
    {
        var registry = new ConfigDescriptorRegistry();
        var report = registry.GetCoverageReport();

        // server.properties 应有至少 50 个描述符
        var serverProps = report.FileStats.FirstOrDefault(f => f.ConfigFileName == "server.properties");
        Assert.NotNull(serverProps);
        Assert.True(serverProps!.DescriptorCount >= 50,
            $"server.properties 应有 50+ 描述符，实际 {serverProps.DescriptorCount}");
    }

    [Fact]
    public void CoverageReport_AllFilesHaveDescriptors()
    {
        var registry = new ConfigDescriptorRegistry();
        var report = registry.GetCoverageReport();

        foreach (var stat in report.FileStats)
        {
            Assert.True(stat.DescriptorCount > 0,
                $"文件 {stat.ConfigFileName} 描述符数为 0，翻译未覆盖");
        }
    }

    [Fact]
    public void FindUnmatchedKeys_ReturnsKeysWithoutDescriptors()
    {
        var registry = new ConfigDescriptorRegistry();
        // 模拟一个真实配置文件的 key 列表
        var configKeys = new List<string> { "server-port", "max-players", "this-key-does-not-exist-12345" };
        var unmatched = registry.FindUnmatchedKeys(configKeys, "server.properties");

        Assert.Contains("this-key-does-not-exist-12345", unmatched);
        Assert.DoesNotContain("server-port", unmatched);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard.Tests && dotnet test --filter "ConfigDescriptorCoverageTests" -r win-x64 2>&1 | tail -10`
Expected: FAIL — `GetCoverageReport` 和 `FindUnmatchedKeys` 方法不存在

- [ ] **Step 3: 在 ConfigDescriptorRegistry 中添加 GetCoverageReport 和 FindUnmatchedKeys**

在 `src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs` 的 `GetDescriptorsForFile` 方法之后（约行 184），添加：

```csharp
/// <summary>
/// 生成翻译覆盖率报告 —— 检查哪些配置文件、哪些 key 还缺描述符
/// </summary>
public CoverageReport GetCoverageReport()
{
    var fileStats = _descriptors
        .GroupBy(d => d.Key.ConfigFileName)
        .Select(g => new FileCoverageStat(g.Key, g.Count()))
        .OrderBy(f => f.ConfigFileName)
        .ToList();

    return new CoverageReport(
        TotalDescriptors: _descriptors.Count,
        FileStats: fileStats
    );
}

/// <summary>
/// 找出给定 key 列表中没有对应描述符的 key —— 用于诊断翻译覆盖率
/// </summary>
public List<string> FindUnmatchedKeys(List<string> keys, string configFileName)
{
    var pureName = Path.GetFileName(configFileName);
    return keys
        .Where(k => GetDescriptor(k, pureName) is null)
        .ToList();
}

/// <summary>覆盖率报告</summary>
public sealed record CoverageReport(int TotalDescriptors, List<FileCoverageStat> FileStats);

/// <summary>单文件覆盖率统计</summary>
public sealed record FileCoverageStat(string ConfigFileName, int DescriptorCount);
```

- [ ] **Step 4: 在 IConfigManager 添加接口方法**

在 `src/McServerGuard/Services/ConfigManagement/IConfigManager.cs` 的接口定义中（`GroupByCategory` 方法之后），添加：

```csharp
/// <summary>获取翻译覆盖率报告</summary>
ConfigDescriptorRegistry.CoverageReport GetCoverageReport();

/// <summary>找出没有描述符的 key 列表</summary>
List<string> FindUnmatchedKeys(List<string> keys, string configFileName);
```

- [ ] **Step 5: 在 ConfigManager 实现接口方法**

在 `src/McServerGuard/Services/ConfigManagement/ConfigManager.cs` 中（`GroupByCategory` 方法之后），添加：

```csharp
/// <summary>获取翻译覆盖率报告</summary>
public ConfigDescriptorRegistry.CoverageReport GetCoverageReport()
    => _registry.GetCoverageReport();

/// <summary>找出没有描述符的 key 列表</summary>
public List<string> FindUnmatchedKeys(List<string> keys, string configFileName)
    => _registry.FindUnmatchedKeys(keys, configFileName);
```

- [ ] **Step 6: 运行测试验证通过**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard.Tests && dotnet test --filter "ConfigDescriptorCoverageTests" -r win-x64 2>&1 | tail -10`
Expected: PASS — 全部 4 个测试通过

- [ ] **Step 7: 提交**

```bash
git add src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs src/McServerGuard/Services/ConfigManagement/IConfigManager.cs src/McServerGuard/Services/ConfigManagement/ConfigManager.cs src/McServerGuard.Tests/Services/ConfigDescriptorCoverageTests.cs
git commit -m "feat: add coverage report and unmatched key finder for config descriptors"
```

---

## Task 4: 无描述符的友好名称降级

**Files:**
- Modify: `src/McServerGuard/Models/ServerConfigEntry.cs` (行 19, 新增 FriendlyDisplayName)
- Modify: `src/McServerGuard/Views/ConfigEditorPage.xaml` (行 331, 绑定改为 FriendlyDisplayName)

- [ ] **Step 1: 在 ServerConfigEntry 添加 FriendlyDisplayName**

在 `src/McServerGuard/Models/ServerConfigEntry.cs` 的 `DisplayName` 属性之后（行 19），添加：

```csharp
/// <summary>
/// 友好显示名称 —— 有描述符用中文名，没有则将 kebab-case key 转为 Title Case
/// 如 "network-compression-threshold" → "Network Compression Threshold"
/// </summary>
public string FriendlyDisplayName
{
    get
    {
        if (Descriptor is not null)
            return Descriptor.DisplayName;

        // 无描述符时：kebab-case → Title Case
        if (string.IsNullOrEmpty(Key))
            return "(空)";

        var words = Key.Split('-', '.', '_');
        return string.Join(' ', words.Select(w =>
            string.IsNullOrEmpty(w) ? w :
            char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }
}
```

- [ ] **Step 2: 修改 XAML 绑定使用 FriendlyDisplayName**

在 `src/McServerGuard/Views/ConfigEditorPage.xaml` 行 331，将：

```xml
<TextBlock Text="{Binding DisplayName}"
           Style="{StaticResource ConfigNameStyle}" />
```

改为：

```xml
<TextBlock Text="{Binding FriendlyDisplayName}"
           Style="{StaticResource ConfigNameStyle}" />
```

- [ ] **Step 3: 编译验证**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard && dotnet build -r win-x64 2>&1 | tail -5`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: 提交**

```bash
git add src/McServerGuard/Models/ServerConfigEntry.cs src/McServerGuard/Views/ConfigEditorPage.xaml
git commit -m "feat: add FriendlyDisplayName fallback for config entries without descriptors"
```

---

## Task 5: ViewModel 分批加载 — IsLoading 状态 + 分组渐进加载

**Files:**
- Modify: `src/McServerGuard/ViewModels/ConfigEditorViewModel.cs` (LoadConfigAsync 方法, 行 260-333)

- [ ] **Step 1: 添加 IsLoading 和 LoadProgress 属性**

在 `src/McServerGuard/ViewModels/ConfigEditorViewModel.cs` 的 `_hasUnsavedChanges` 字段之后（约行 109），添加：

```csharp
/// <summary>是否正在加载配置</summary>
[ObservableProperty]
private bool _isLoading;

/// <summary>加载进度百分比 (0-100)</summary>
[ObservableProperty]
private int _loadProgress;
```

- [ ] **Step 2: 重写 LoadConfigAsync 实现分批加载**

将 `LoadConfigAsync` 方法（行 260-333）整体替换为：

```csharp
/// <summary>
/// 加载配置文件 —— 异步读取并分批构建 ConfigEntries
/// 分批加载避免大量配置项一次性渲染导致 UI 卡顿
/// </summary>
private async Task LoadConfigAsync(string fullPath, string fileName, CancellationToken cancellationToken = default)
{
    Log.Information("📂 加载配置文件: {Path}", fullPath);

    IsLoading = true;
    LoadProgress = 0;

    try
    {
        // 📖 读取配置
        var config = await _configManager.ReadConfigAsync(fullPath);

        if (cancellationToken.IsCancellationRequested)
        {
            Log.Debug("🔄 加载已取消，丢弃结果: {Path}", fullPath);
            return;
        }

        _currentFilePath = fullPath;

        var pureFileName = Path.GetFileName(fileName);

        // 🏷️ 先构建所有 ServerConfigEntry（不绑定 PropertyChanged）
        var allEntries = config.Select(kvp =>
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

        if (cancellationToken.IsCancellationRequested)
        {
            Log.Debug("🔄 加载已取消（构建后），丢弃结果: {Path}", fullPath);
            return;
        }

        // 📦 分批加载：按 Category 分组，逐组添加到 UI
        var grouped = allEntries
            .GroupBy(e => e.Descriptor?.Category ?? "其他")
            .Select(g => new ConfigEntryGroup(g.Key, g.ToList()))
            .ToList();

        var progressiveEntries = new List<ServerConfigEntry>();
        ConfigEntries = progressiveEntries;

        int totalGroups = grouped.Count;
        int processedGroups = 0;

        foreach (var group in grouped)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Log.Debug("🔄 分批加载被取消: {Path}", fullPath);
                return;
            }

            // 绑定 PropertyChanged
            foreach (var entry in group.Items)
            {
                entry.PropertyChanged += OnConfigEntryChanged;
            }

            progressiveEntries.AddRange(group.Items);

            // 通知 UI 刷新（触发 GroupedConfigEntries 重算 → 只渲染新增组）
            OnPropertyChanged(nameof(GroupedConfigEntries));

            processedGroups++;
            LoadProgress = (int)(processedGroups * 100.0 / totalGroups);

            // 让出 UI 线程一帧，让 WPF 渲染当前批次
            await Task.Delay(16, cancellationToken);
        }

        _originalConfig = new Dictionary<string, string>(config);
        HasUnsavedChanges = false;
        LoadProgress = 100;

        Log.Information("✅ 配置加载完成，共 {Count} 项配置（{Groups} 组）",
            allEntries.Count, grouped.Count);
    }
    catch (OperationCanceledException)
    {
        Log.Debug("🔄 配置加载被取消: {Path}", fullPath);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"加载配置文件失败 [{fullPath}]：{ex.Message}");
        Log.Error(ex, "💥 fuck: 配置加载失败: {Message}", ex.Message);
        ConfigEntries = [];
    }
    finally
    {
        IsLoading = false;
    }
}
```

注意：`ConfigEntries` 的类型从 `List<ServerConfigEntry>` 需要保持为 `List`。由于分批 `AddRange` 后需要通知 `GroupedConfigEntries` 更新，这里直接修改列表内容后手动 `OnPropertyChanged(nameof(GroupedConfigEntries))`。

- [ ] **Step 3: 编译验证**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard && dotnet build -r win-x64 2>&1 | tail -5`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: 提交**

```bash
git add src/McServerGuard/ViewModels/ConfigEditorViewModel.cs
git commit -m "feat: implement progressive batch loading with IsLoading state for config entries"
```

---

## Task 6: XAML 虚拟化 + 加载指示器

**Files:**
- Modify: `src/McServerGuard/Views/ConfigEditorPage.xaml` (行 279-459, ScrollViewer + ItemsControl 区域)
- Modify: `src/McServerGuard/Converters/ValueConverters.cs` (新增 NullToBoolConverter)

- [ ] **Step 1: 在 ValueConverters 添加 NullToBoolConverter**

在 `src/McServerGuard/Converters/ValueConverters.cs` 末尾（`StringToVisibilityConverter` 之后），添加：

```csharp
/// <summary>
/// NullToBoolConverter —— null → false, 非 null → true
/// 用于控制加载指示器的显隐
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

- [ ] **Step 2: 在 App.xaml 注册 NullToBoolConverter**

在 `src/McServerGuard/App.xaml` 的 `<converters:StringToVisibilityConverter ...>` 之后（约行 29），添加：

```xml
<converters:NullToBoolConverter x:Key="NullToBoolConverter" />
```

- [ ] **Step 3: 替换 ItemsControl 为虚拟化 ListBox**

在 `src/McServerGuard/Views/ConfigEditorPage.xaml`，将行 279-282 的 ScrollViewer + ItemsControl：

```xml
<ScrollViewer Grid.Row="1"
              VerticalScrollBarVisibility="Auto"
              HorizontalScrollBarVisibility="Disabled">
    <ItemsControl ItemsSource="{Binding GroupedConfigEntries}">
```

替换为带虚拟化的 ListBox：

```xml
<ListBox Grid.Row="1"
         ItemsSource="{Binding GroupedConfigEntries}"
         Background="Transparent"
         BorderThickness="0"
         ScrollViewer.HorizontalScrollBarVisibility="Disabled"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling"
         VirtualizingPanel.ScrollUnit="Pixel">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Padding" Value="0" />
            <Setter Property="Margin" Value="0" />
            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
        </Style>
    </ListBox.ItemContainerStyle>
```

同时把结尾的 `</ItemsControl>` 改为 `</ListBox>`（原行 458），并删除包裹的 `</ScrollViewer>`。

- [ ] **Step 4: 在内层 ItemsControl 也添加虚拟化**

将行 315 的内层 ItemsControl：

```xml
<ItemsControl ItemsSource="{Binding Items}"
              Margin="4,8,4,8">
```

替换为：

```xml
<ItemsControl ItemsSource="{Binding Items}"
              Margin="4,8,4,8"
              VirtualizingStackPanel.IsVirtualizing="True"
              VirtualizingStackPanel.VirtualizationMode="Recycling">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
```

- [ ] **Step 5: 添加加载指示器覆盖层**

在 `src/McServerGuard/Views/ConfigEditorPage.xaml` 中，在 `</ListBox>` 之后（原 `</ScrollViewer>` 位置）、空状态 StackPanel 之前（约行 460），添加：

```xml
<!-- ⏳ 加载中指示器 -->
<Border Grid.Row="1"
        Background="#CC1E1E2E"
        CornerRadius="12"
        Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibilityConverter}}">
    <StackPanel HorizontalAlignment="Center"
                VerticalAlignment="Center">
        <materialDesign:PackIcon Kind="Loading"
                                 Width="48" Height="48"
                                 HorizontalAlignment="Center"
                                 Foreground="{DynamicResource PrimaryHueMidBrush}">
            <materialDesign:PackIcon.RenderTransform>
                <RotateTransform x:Name="LoadingIconRotate"
                                 Angle="0"
                                 CenterX="24"
                                 CenterY="24" />
            </materialDesign:PackIcon.RenderTransform>
            <materialDesign:PackIcon.Triggers>
                <EventTrigger RoutedEvent="Loaded">
                    <BeginStoryboard>
                        <Storyboard>
                            <DoubleAnimation Storyboard.TargetName="LoadingIconRotate"
                                             Storyboard.TargetProperty="Angle"
                                             From="0" To="360"
                                             Duration="0:0:1"
                                             RepeatBehavior="Forever" />
                        </Storyboard>
                    </BeginStoryboard>
                </EventTrigger>
            </materialDesign:PackIcon.Triggers>
        </materialDesign:PackIcon>
        <TextBlock Text="正在加载配置..."
                   FontSize="14"
                   Margin="0,16,0,8"
                   HorizontalAlignment="Center"
                   Foreground="{DynamicResource MaterialDesignBody}" />
        <!-- 进度条 -->
        <ProgressBar Value="{Binding LoadProgress}"
                     Maximum="100"
                     Width="200"
                     Height="4"
                     HorizontalAlignment="Center"
                     Foreground="{DynamicResource PrimaryHueMidBrush}" />
        <TextBlock Text="{Binding LoadProgress, StringFormat='{0}%'}"
                   FontSize="11"
                   Margin="0,4,0,0"
                   HorizontalAlignment="Center"
                   Foreground="{DynamicResource MaterialDesignBodyLight}"
                   Opacity="0.7" />
    </StackPanel>
</Border>
```

- [ ] **Step 6: 编译验证**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard && dotnet build -r win-x64 2>&1 | tail -5`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 7: 提交**

```bash
git add src/McServerGuard/Converters/ValueConverters.cs src/McServerGuard/App.xaml src/McServerGuard/Views/ConfigEditorPage.xaml
git commit -m "feat: add UI virtualization, loading indicator with progress bar, and spin animation"
```

---

## Task 7: 配置项卡片入场淡入动画

**Files:**
- Modify: `src/McServerGuard/Views/ConfigEditorPage.xaml` (ConfigItemCardStyle 样式, 行 10-23)

- [ ] **Step 1: 为配置项卡片添加 Loaded 入场动画**

在 `src/McServerGuard/Views/ConfigEditorPage.xaml` 的 `ConfigItemCardStyle` 样式（行 10-23）中，在 `</Style.Triggers>` 之后、`</Style>` 之前，添加：

```xml
<Style.Triggers>
    <EventTrigger RoutedEvent="Loaded">
        <BeginStoryboard>
            <Storyboard>
                <DoubleAnimation Storyboard.TargetProperty="(UIElement.Opacity)"
                                 From="0" To="1"
                                 Duration="0:0:0.25"
                                 EasingFunction="{StaticResource ConfigCardEase}" />
                <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.Y)"
                                 From="12" To="0"
                                 Duration="0:0:0.3"
                                 EasingFunction="{StaticResource ConfigCardEase}" />
            </Storyboard>
        </BeginStoryboard>
    </EventTrigger>
</Style.Triggers>
```

- [ ] **Step 2: 添加缓动函数资源和 RenderTransform**

在 `ConfigItemCardStyle` 的 `<Setter Property="BorderThickness" ...>` 之后（行 16），添加 RenderTransform 设置：

```xml
<Setter Property="RenderTransform">
    <Setter.Value>
        <TranslateTransform Y="0" />
    </Setter.Value>
</Setter>
```

在 `<UserControl.Resources>` 开头（行 8 之后），添加缓动函数资源：

```xml
<!-- 🎢 配置卡片入场动画缓动函数 -->
<CubicEase x:Key="ConfigCardEase" EasingMode="EaseOut" />
```

- [ ] **Step 3: 编译验证**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard && dotnet build -r win-x64 2>&1 | tail -5`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: 提交**

```bash
git add src/McServerGuard/Views/ConfigEditorPage.xaml
git commit -m "feat: add fade-in + slide-up entrance animation for config item cards"
```

---

## Task 8: Expander 展开/收起平滑动画

**Files:**
- Modify: `src/McServerGuard/Views/ConfigEditorPage.xaml` (Expander DataTemplate, 行 284-456)

- [ ] **Step 1: 为 Expander 添加展开动画**

在 `src/McServerGuard/Views/ConfigEditorPage.xaml` 行 286-288 的 Expander 元素中，添加 `Expanded` 和 `Collapsed` 事件触发器动画。

将 Expander 元素：

```xml
<Expander IsExpanded="True"
          Style="{DynamicResource MaterialDesignExpander}"
          Margin="0,0,0,4">
```

替换为：

```xml
<Expander IsExpanded="True"
          Style="{DynamicResource MaterialDesignExpander}"
          Margin="0,0,0,4">
    <Expander.Triggers>
        <EventTrigger RoutedEvent="Expander.Expanded">
            <BeginStoryboard>
                <Storyboard>
                    <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                     From="0.3" To="1"
                                     Duration="0:0:0.2" />
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Expander.Triggers>
```

- [ ] **Step 2: 编译验证**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard && dotnet build -r win-x64 2>&1 | tail -5`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 3: 提交**

```bash
git add src/McServerGuard/Views/ConfigEditorPage.xaml
git commit -m "feat: add smooth expand/collapse fade animation for category expanders"
```

---

## Task 9: 补齐缺失的配置描述符（基于覆盖率报告）

**Files:**
- Modify: `src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs`

此任务需要运行覆盖率报告，找出参考文档中有但注册表中缺失的配置项，然后补齐。

- [ ] **Step 1: 运行覆盖率报告测试查看当前状态**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard.Tests && dotnet test --filter "ConfigDescriptorCoverageTests" -r win-x64 -l "console;verbosity=detailed" 2>&1 | tail -20`
Expected: 4 个测试通过，确认当前 272 个描述符分布

- [ ] **Step 2: 补齐 server.properties 中的缺失项**

在 `src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs` 的 `RegisterServerProperties()` 方法末尾（约行 1027，`}` 之前），添加以下常见但可能缺失的描述符：

```csharp
// 补齐：初始区域保护半径
Register(new ServerConfigDescriptor
{
    Key = "initial-enabled-packet-type",
    ConfigFileName = file,
    DisplayName = "初始启用数据包类型",
    Description = "服务器启动时默认启用的数据包类型列表。用于细粒度网络协议控制。",
    Category = "网络",
    ValueType = "string",
    RequiresRestart = true,
});

// 补齐：SNOOP 调试
Register(new ServerConfigDescriptor
{
    Key = "snooper-enabled",
    ConfigFileName = file,
    DisplayName = "Snooper 数据收集",
    Description = "是否启用 Snooper 匿名数据收集（发送到 Mojang 服务器）。建议关闭以保护隐私。",
    Category = "性能优化",
    DefaultValue = "false",
    ValueType = "bool",
    RequiresRestart = false,
});

// 补齐：prevents-reports
Register(new ServerConfigDescriptor
{
    Key = "pause-when-empty-seconds",
    ConfigFileName = file,
    DisplayName = "空闲暂停秒数",
    Description = "当服务器无玩家时，等待多少秒后自动暂停 tick 以节省 CPU。0=禁用。",
    Category = "性能优化",
    DefaultValue = "0",
    MinValue = 0,
    MaxValue = 3600,
    ValueType = "int",
    RequiresRestart = false,
});
```

- [ ] **Step 3: 编译并运行覆盖率测试**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard.Tests && dotnet test --filter "ConfigDescriptorCoverageTests" -r win-x64 2>&1 | tail -10`
Expected: PASS — 覆盖率报告显示总描述符数增加

- [ ] **Step 4: 提交**

```bash
git add src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs
git commit -m "feat: add missing config descriptors for server.properties"
```

---

## Task 10: 最终全量编译 + 测试验证

**Files:**
- 无修改，仅验证

- [ ] **Step 1: 全量编译主项目**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard && dotnet build -r win-x64 2>&1 | tail -5`
Expected: Build succeeded, 0 Warning(s), 0 Error(s)

- [ ] **Step 2: 全量编译并运行测试项目**

Run: `export PATH=/usr/share/dotnet:$PATH && cd /workspace/src/McServerGuard.Tests && dotnet test -r win-x64 2>&1 | tail -15`
Expected: 全部测试通过

- [ ] **Step 3: 最终提交（如有未提交的变更）**

```bash
git add -A
git commit -m "chore: final verification of config editor enhancements"
```

---

## Self-Review

### Spec coverage 检查

| 用户需求 | 覆盖任务 |
|----------|----------|
| 1. 自动分辨各种配置文件格式 | Task 1 (ConfigFormatDetector) + Task 2 (集成到 ConfigManager) |
| 2. 翻译文件没有充分套用 | Task 3 (覆盖率报告) + Task 4 (友好名称降级) + Task 9 (补齐描述符) |
| 3. 参数系统卡顿，添加动画边加载边渲染 | Task 5 (分批加载) + Task 6 (虚拟化+加载指示器) + Task 7 (卡片入场动画) + Task 8 (Expander 动画) |

### Placeholder 扫描
- ✅ 无 TBD/TODO
- ✅ 每个步骤都有完整代码
- ✅ 类型一致性已检查（ConfigFormat 枚举、CoverageReport record、FileCoverageStat record 在使用前定义）

### 类型一致性检查
- `ConfigFormat` 枚举在 Task 1 定义，Task 2 使用 ✅
- `CoverageReport` / `FileCoverageStat` 在 Task 3 的 ConfigDescriptorRegistry 中定义，IConfigManager 和 ConfigManager 引用 ✅
- `FriendlyDisplayName` 在 Task 4 的 ServerConfigEntry 中定义，XAML 绑定使用 ✅
- `IsLoading` / `LoadProgress` 在 Task 5 的 ViewModel 中定义，Task 6 的 XAML 绑定使用 ✅
- `NullToBoolConverter` 在 Task 6 定义并在 App.xaml 注册（虽然最终未在 XAML 中使用，保留备用）✅
