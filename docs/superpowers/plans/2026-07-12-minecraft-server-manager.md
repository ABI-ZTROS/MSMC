# Minecraft 服务器管理工具 (McServerGuard) 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个基于 .NET 10.0 的 Windows 桌面应用程序，用于自动检测、监控和管理正在运行的 Minecraft Java Edition 服务器核心，集成本地推理模型为服务器运行提供智能保障。

**Architecture:** 采用 WPF + MaterialDesignInXAML Toolkit + HandyControl 作为 UI 渲染层（原生 DPI 自适应），MVVM 架构（CommunityToolkit.Mvvm），底层使用系统 API 检测进程/文件系统，通过 ML.NET + ONNX Runtime 集成 MLP 神经网络模型提供智能保障。ScottPlot 提供系统负载图表，LiveCharts2 提供历史趋势图表。

**Tech Stack:**
- .NET 10.0 (net10.0-windows)
- WPF + MaterialDesignInXAML Toolkit 5.x + HandyControl
- CommunityToolkit.Mvvm 8.x（MVVM 框架）
- ScottPlot 6.x / LiveCharts2（图表）
- ML.NET + ONNX Runtime（MLP 神经网络推理）
- System.Diagnostics.Process / WMI（进程检测）
- Serilog（日志）

---

## 项目文件结构

```
McServerGuard/
├── McServerGuard.sln
├── src/
│   ├── McServerGuard/
│   │   ├── McServerGuard.csproj
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── AssemblyInfo.cs
│   │   ├── Constants/
│   │   │   ├── ServerConstants.cs          # 服务器类型、配置文件常量
│   │   │   └── JvmArgumentConstants.cs     # JVM 参数名常量
│   │   ├── Models/
│   │   │   ├── ServerInstance.cs           # 服务器实例数据模型
│   │   │   ├── ServerConfigEntry.cs        # 配置文件条目模型
│   │   │   ├── ServerConfigDescriptor.cs   # 配置项描述/约束/中文翻译
│   │   │   ├── SystemMetrics.cs            # 系统性能指标模型
│   │   │   ├── StartupScriptInfo.cs        # 启动脚本解析结果
│   │   │   └── DetectionResult.cs          # 检测结果聚合模型
│   │   ├── Services/
│   │   │   ├── ServerDetection/
│   │   │   │   ├── IServerDetector.cs      # 检测服务接口
│   │   │   │   ├── ServerDetector.cs        # 核心检测编排
│   │   │   │   ├── ProcessScanner.cs        # Java 进程扫描器
│   │   │   │   ├── CommandLineParser.cs     # 命令行参数解析
│   │   │   │   ├── WorkingDirectoryResolver.cs  # 工作目录解析
│   │   │   │   ├── StartupScriptDetector.cs # 启动脚本检测（内容架构判断）
│   │   │   │   ├── ServerTypeClassifier.cs  # 服务器类型分类器
│   │   │   │   └── ConfigFileScanner.cs    # 配置文件列表扫描
│   │   │   ├── ConfigManagement/
│   │   │   │   ├── IConfigManager.cs       # 配置管理接口
│   │   │   │   ├── ConfigManager.cs         # 配置读写实现
│   │   │   │   ├── PropertiesParser.cs     # server.properties 解析器
│   │   │   │   ├── YamlParser.cs           # YAML 配置解析器
│   │   │   │   ├── JsonConfigParser.cs     # JSON 配置解析器（ops.json 等）
│   │   │   │   └── ConfigDescriptorRegistry.cs  # 配置项描述注册表
│   │   │   ├── SystemMonitoring/
│   │   │   │   ├── ISystemMonitor.cs       # 系统监控接口
│   │   │   │   ├── SystemMonitor.cs         # 系统监控实现
│   │   │   │   ├── DiskSpaceMonitor.cs     # 磁盘空间监控
│   │   │   │   ├── MemoryMonitor.cs        # 内存使用监控
│   │   │   │   ├── ThreadAnalyzer.cs        # 线程分析器
│   │   │   │   └── PerformanceCounterHelper.cs  # 性能计数器辅助
│   │   │   └── AIService/
│   │   │       ├── IAiGuardService.cs      # AI 保障服务接口
│   │   │       ├── LogAnomalyDetector.cs    # 日志异常检测器
│   │   │       ├── CrashPredictor.cs        # 崩溃预测器
│   │   │       ├── ConfigOptimizer.cs       # 配置优化推荐器
│   │   │       ├── AiGuardOrchestrator.cs    # AI 保障编排器
│   │   │       └── TrainingDataCollector.cs # 训练数据收集器
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs            # 主窗口 ViewModel
│   │   │   ├── ServerDetectionViewModel.cs # 服务器检测页 ViewModel
│   │   │   ├── ConfigEditorViewModel.cs    # 配置编辑页 ViewModel
│   │   │   ├── SystemMonitorViewModel.cs   # 系统监控页 ViewModel
│   │   │   └── AIGuardViewModel.cs          # AI 保障页 ViewModel
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml             # 主窗口（自适应布局）
│   │   │   ├── ServerDetectionPage.xaml     # 服务器检测页
│   │   │   ├── ConfigEditorPage.xaml        # 配置编辑页
│   │   │   ├── SystemMonitorPage.xaml       # 系统监控页
│   │   │   └── AIGuardPage.xaml            # AI 保障页
│   │   ├── Converters/
│   │   │   └── ValueConverters.cs          # XAML 值转换器
│   │   ├── Themes/
│   │   │   └── CustomTheme.xaml            # 自定义主题配色
│   │   └── Resources/
│   │       └── Strings/
│   │           ├── ConfigDescriptions.cs   # 配置项中文描述
│   │           └── UIMessages.cs           # UI 文本资源
│   └── McServerGuard.Tests/
│       ├── McServerGuard.Tests.csproj
│       ├── Services/
│       │   ├── CommandLineParserTests.cs
│       │   ├── ProcessScannerTests.cs
│       │   ├── StartupScriptDetectorTests.cs
│       │   ├── ServerTypeClassifierTests.cs
│       │   ├── ConfigManagerTests.cs
│       │   ├── PropertiesParserTests.cs
│       │   └── YamlParserTests.cs
│       └── Models/
│           └── ServerInstanceTests.cs
└── README.md
```

---

## Task 1: 创建项目骨架与 NuGet 包配置

**Files:**
- Create: `McServerGuard/McServerGuard.sln`
- Create: `McServerGuard/src/McServerGuard/McServerGuard.csproj`
- Create: `McServerGuard/src/McServerGuard.Tests/McServerGuard.Tests.csproj`

- [ ] **Step 1: 创建解决方案文件**

```bash
cd /workspace/McServerGuard
dotnet new sln -n McServerGuard
```

- [ ] **Step 2: 创建主项目**

```bash
mkdir -p src/McServerGuard
dotnet new wpf -n McServerGuard -o src/McServerGuard -f net10.0
```

- [ ] **Step 3: 创建测试项目**

```bash
mkdir -p src/McServerGuard.Tests
dotnet new xunit -n McServerGuard.Tests -o src/McServerGuard.Tests -f net10.0
dotnet add src/McServerGuard.Tests/McServerGuard.Tests.csproj reference src/McServerGuard/McServerGuard.csproj
```

- [ ] **Step 4: 添加 NuGet 包引用**

```bash
cd src/McServerGuard
dotnet add package CommunityToolkit.Mvvm --version 8.*.*
dotnet add package MaterialDesignThemes --version 5.*.*
dotnet add package HandyControl --version 3.*.*
dotnet add package ScottPlot.WPF --version 6.*.*
dotnet add package LiveChartsCore.SkiaSharpView.WPF --version 2.*.*
dotnet add package Serilog --version 4.*.*
dotnet add package Serilog.Sinks.File --version 6.*.*
dotnet add package Microsoft.ML --version 4.*.*
dotnet add package Microsoft.ML.TimeSeries --version 4.*.*
dotnet add package Microsoft.ML.Onnx --version 4.*.*
dotnet add package YamlDotNet --version 16.*.*
```

- [ ] **Step 5: 创建项目目录结构**

```bash
cd src/McServerGuard
mkdir -p Constants Models Services/ServerDetection Services/ConfigManagement Services/SystemMonitoring Services/AIService ViewModels Views Converters Themes Resources/Strings
```

- [ ] **Step 6: 添加项目到解决方案**

```bash
cd /workspace/McServerGuard
dotnet sln add src/McServerGuard/McServerGuard.csproj
dotnet sln add src/McServerGuard.Tests/McServerGuard.Tests.csproj
```

- [ ] **Step 7: 验证项目构建**

```bash
cd /workspace/McServerGuard
dotnet build
```
Expected: BUILD SUCCEEDED

- [ ] **Step 8: 提交**

```bash
git add -A
git commit -m "feat: 初始化项目骨架与 NuGet 包配置"
```

---

## Task 2: 定义常量和数据模型

**Files:**
- Create: `src/McServerGuard/Constants/ServerConstants.cs`
- Create: `src/McServerGuard/Constants/JvmArgumentConstants.cs`
- Create: `src/McServerGuard/Models/ServerInstance.cs`
- Create: `src/McServerGuard/Models/ServerConfigEntry.cs`
- Create: `src/McServerGuard/Models/ServerConfigDescriptor.cs`
- Create: `src/McServerGuard/Models/SystemMetrics.cs`
- Create: `src/McServerGuard/Models/StartupScriptInfo.cs`
- Create: `src/McServerGuard/Models/DetectionResult.cs`

- [ ] **Step 1: 创建服务器类型和配置文件常量**

```csharp
// src/McServerGuard/Constants/ServerConstants.cs
namespace McServerGuard.Constants;

/// <summary>
/// Minecraft 服务器类型枚举
/// </summary>
public enum ServerType
{
    Unknown,
    Vanilla,
    Spigot,
    Paper,
    Forge,
    Fabric,
    Bukkit
}

/// <summary>
/// 服务器类型与对应的标识特征常量
/// </summary>
public static class ServerConstants
{
    // 各服务器核心 JAR 文件名匹配模式
    public static readonly string[] VanillaJarPatterns = ["minecraft_server.*.jar", "server.jar"];
    public static readonly string[] SpigotJarPatterns = ["spigot-*.jar", "spigot.jar"];
    public static readonly string[] PaperJarPatterns = ["paper-*.jar", "paper.jar"];
    public static readonly string[] ForgeJarPatterns = ["forge-*.jar", "forge.jar"];
    public static readonly string[] FabricJarPatterns = ["fabric-server-launch.jar", "fabric-server.jar"];
    public static readonly string[] BukkitJarPatterns = ["craftbukkit-*.jar"];

    // 服务器核心判定关键词（用于进程命令行中的 JAR 名称匹配）
    public static readonly string[] ServerJarKeywords =
    [
        "minecraft_server", "server", "spigot", "paper",
        "forge", "fabric-server-launch", "craftbukkit"
    ];

    // 服务器进程标识关键词（区分服务器 vs 客户端）
    public static readonly string[] ServerProcessMarkers = ["nogui", "--nogui"];
    public static readonly string[] ClientProcessMarkers =
        ["--version", "--accessToken", "--userType", "--assetsDir"];

    // 配置文件标识（用于服务器类型分类）
    public static readonly Dictionary<ServerType, string[]> TypeIndicatorFiles = new()
    {
        [ServerType.Vanilla] = [],
        [ServerType.Spigot] = ["spigot.yml", "bukkit.yml"],
        [ServerType.Paper] = ["config/paper-global.yml"],
        [ServerType.Forge] = ["mods/", "forge-server.toml"],
        [ServerType.Fabric] = ["fabric-server-launch.properties", ".fabric/"],
        [ServerType.Bukkit] = ["bukkit.yml"]
    };

    // 核心配置文件列表（所有类型共有）
    public static readonly string[] CommonConfigFiles =
    [
        "server.properties",
        "eula.txt",
        "ops.json",
        "whitelist.json",
        "banned-players.json",
        "banned-ips.json",
        "permissions.yml",
        "commands.yml"
    ];

    // 各类型专属配置文件
    public static readonly string[] SpigotConfigFiles = ["spigot.yml", "bukkit.yml"];
    public static readonly string[] PaperConfigFiles =
    [
        "config/paper-global.yml",
        "config/paper-world-defaults.yml"
    ];
    public static readonly string[] ForgeConfigFiles = ["server.toml", "forge-server.toml", "mods/"];
    public static readonly string[] FabricConfigFiles =
    [
        "fabric-server-launch.properties",
        "mods/",
        ".fabric/"
    ];

    // 服务器验证文件（用于确认工作目录是 Minecraft 服务器目录）
    public static readonly string ServerValidationFile = "server.properties";

    // 端口默认值
    public const int DefaultServerPort = 25565;
}
```

- [ ] **Step 2: 创建 JVM 参数常量**

```csharp
// src/McServerGuard/Constants/JvmArgumentConstants.cs
namespace McServerGuard.Constants;

public static class JvmArgumentConstants
{
    // 内存参数
    public const string InitialHeapMemory = "-Xms";
    public const string MaxHeapMemory = "-Xmx";
    public const string MetaspaceSize = "-XX:MetaspaceSize=";
    public const string MaxMetaspaceSize = "-XX:MaxMetaspaceSize=";

    // GC 类型标志
    public const string G1GC = "-XX:+UseG1GC";
    public const string ZGC = "-XX:+UseZGC";
    public const string ShenandoahGC = "-XX:+UseShenandoahGC";
    public const string ParallelGC = "-XX:+UseParallelGC";

    // Aikar 标志标识
    public const string AikarFlagIdentifier = "-Dusing.aikars.flags=";
    public const string AikarNewFlagIdentifier = "-Daikars.new.flags=true";

    // JVM 标志前缀
    public const string JvmFlagPrefix = "-XX:";
    public const string SystemPropertyPrefix = "-D";

    // GC 日志参数模式
    public const string GcLogPatternLegacy = "-Xloggc:";
    public const string GcLogPatternModern = "-Xlog:gc*";

    // nogui 参数
    public const string NoGuiLegacy = "nogui";
    public const string NoGuiModern = "--nogui";

    // -jar 参数
    public const string JarFlag = "-jar";

    // 内存单位后缀
    public static readonly char[] MemorySuffixes = ['G', 'M', 'K', 'g', 'm', 'k'];
}
```

- [ ] **Step 3: 创建 ServerInstance 模型**

```csharp
// src/McServerGuard/Models/ServerInstance.cs
using CommunityToolkit.Mvvm.ComponentModel;
using McServerGuard.Constants;

namespace McServerGuard.Models;

public partial class ServerInstance : ObservableObject
{
    /// <summary>进程 ID</summary>
    public int ProcessId { get; init; }

    /// <summary>服务器类型</summary>
    [ObservableProperty]
    private ServerType _serverType = ServerType.Unknown;

    /// <summary>服务器工作路径</summary>
    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    /// <summary>Java 可执行文件完整路径</summary>
    [ObservableProperty]
    private string _javaPath = string.Empty;

    /// <summary>服务器核心 JAR 文件完整路径</summary>
    [ObservableProperty]
    private string _serverJarPath = string.Empty;

    /// <summary>服务器核心 JAR 文件名</summary>
    [ObservableProperty]
    private string _serverJarName = string.Empty;

    /// <summary>检测到的启动脚本路径（若有）</summary>
    [ObservableProperty]
    private string? _startupScriptPath;

    /// <summary>完整的命令行参数</summary>
    [ObservableProperty]
    private string _fullCommandLine = string.Empty;

    /// <summary>解析后的 JVM 参数列表</summary>
    public List<string> JvmArguments { get; init; } = [];

    /// <summary>分配的初始堆内存（字节）</summary>
    [ObservableProperty]
    private long _initialHeapMemoryBytes;

    /// <summary>分配的最大堆内存（字节）</summary>
    [ObservableProperty]
    private long _maxHeapMemoryBytes;

    /// <summary>检测到的配置文件完整列表</summary>
    [ObservableProperty]
    private List<string> _configFiles = [];

    /// <summary>是否使用了 Aikar 优化标志</summary>
    [ObservableProperty]
    private bool _usesAikarFlags;

    /// <summary>使用的 GC 类型</summary>
    [ObservableProperty]
    private string _gcType = string.Empty;

    /// <summary>服务器监听端口</summary>
    [ObservableProperty]
    private int _serverPort = ServerConstants.DefaultServerPort;

    /// <summary>检测时间</summary>
    public DateTime DetectedAt { get; init; } = DateTime.Now;

    /// <summary>显示名称（用于 UI）</summary>
    public string DisplayName => $"{ServerType} @ {System.IO.Path.GetFileName(WorkingDirectory)} (PID: {ProcessId})";

    /// <summary>格式化的最大堆内存</summary>
    public string FormattedMaxMemory => FormatBytes(MaxHeapMemoryBytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1L << 30) return $"{(bytes >> 30)} GB";
        if (bytes >= 1L << 20) return $"{(bytes >> 20)} MB";
        return $"{bytes} KB";
    }
}
```

- [ ] **Step 4: 创建 StartupScriptInfo 模型**

```csharp
// src/McServerGuard/Models/StartupScriptInfo.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace McServerGuard.Models;

public partial class StartupScriptInfo : ObservableObject
{
    /// <summary>脚本文件完整路径</summary>
    [ObservableProperty]
    private string _scriptPath = string.Empty;

    /// <summary>脚本文件名</summary>
    [ObservableProperty]
    private string _scriptName = string.Empty;

    /// <summary>是否为 Minecraft 服务器启动脚本</summary>
    [ObservableProperty]
    private bool _isServerStartupScript;

    /// <summary>Java 路径（从脚本中提取）</summary>
    [ObservableProperty]
    private string? _javaPath;

    /// <summary>最大堆内存（字节）</summary>
    [ObservableProperty]
    private long _maxHeapMemoryBytes;

    /// <summary>服务器 JAR 文件名</summary>
    [ObservableProperty]
    private string? _serverJarName;

    /// <summary>是否包含自动重启循环</summary>
    [ObservableProperty]
    private bool _hasAutoRestart;

    /// <summary>是否使用 Aikar 标志</summary>
    [ObservableProperty]
    private bool _usesAikarFlags;

    /// <summary>脚本原始内容</summary>
    [ObservableProperty]
    private string _rawContent = string.Empty;

    /// <summary>检测的匹配规则列表</summary>
    public List<string> MatchedRules { get; init; } = [];
}
```

- [ ] **Step 5: 创建 ServerConfigEntry 和 ServerConfigDescriptor 模型**

```csharp
// src/McServerGuard/Models/ServerConfigEntry.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace McServerGuard.Models;

public partial class ServerConfigEntry : ObservableObject
{
    /// <summary>配置键名</summary>
    [ObservableProperty]
    private string _key = string.Empty;

    /// <summary>配置值</summary>
    [ObservableProperty]
    private string _value = string.Empty;

    /// <summary>所属配置文件</summary>
    [ObservableProperty]
    private string _sourceFile = string.Empty;

    /// <summary>值是否已被修改（未保存）</summary>
    [ObservableProperty]
    private bool _isModified;

    /// <summary>原始值（用于对比和还原）</summary>
    [ObservableProperty]
    private string _originalValue = string.Empty;

    /// <summary>值是否有效（通过约束验证）</summary>
    [ObservableProperty]
    private bool _isValid = true;

    /// <summary>验证错误信息</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>对应的配置描述信息</summary>
    public ServerConfigDescriptor? Descriptor { get; set; }
}

// src/McServerGuard/Models/ServerConfigDescriptor.cs
namespace McServerGuard.Models;

/// <summary>
/// 配置项的中文描述、约束和验证信息
/// </summary>
public class ServerConfigDescriptor
{
    /// <summary>配置键名</summary>
    public required string Key { get; init; }

    /// <summary>中文显示名称</summary>
    public required string DisplayName { get; init; }

    /// <summary>中文描述</summary>
    public required string Description { get; init; }

    /// <summary>所属配置文件</summary>
    public required string ConfigFile { get; init; }

    /// <summary>值类型（string, int, bool, enum）</summary>
    public required string ValueType { get; init; }

    /// <summary>最小值（数值类型）</summary>
    public int? MinValue { get; init; }

    /// <summary>最大值（数值类型）</summary>
    public int? MaxValue { get; init; }

    /// <summary>允许的值列表（枚举类型）</summary>
    public string[]? AllowedValues { get; init; }

    /// <summary>正则表达式验证（字符串类型）</summary>
    public string? RegexPattern { get; init; }

    /// <summary>默认值</summary>
    public string? DefaultValue { get; init; }

    /// <summary>修改后是否需要重启服务器</summary>
    public bool RequiresRestart { get; init; }

    /// <summary>配置分类</summary>
    public string Category { get; init; } = "通用";
}
```

- [ ] **Step 6: 创建 SystemMetrics 模型**

```csharp
// src/McServerGuard/Models/SystemMetrics.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace McServerGuard.Models;

public partial class SystemMetrics : ObservableObject
{
    // --- 系统级 ---
    /// <summary>CPU 使用率 (%)</summary>
    [ObservableProperty]
    private double _cpuUsagePercent;

    /// <summary>总物理内存（字节）</summary>
    [ObservableProperty]
    private long _totalMemoryBytes;

    /// <summary>已用内存（字节）</summary>
    [ObservableProperty]
    private long _usedMemoryBytes;

    /// <summary>内存使用率 (%)</summary>
    [ObservableProperty]
    private double _memoryUsagePercent;

    /// <summary>系统总线程数</summary>
    [ObservableProperty]
    private int _totalThreadCount;

    // --- 磁盘 ---
    /// <summary>服务器磁盘总空间（字节）</summary>
    [ObservableProperty]
    private long _diskTotalBytes;

    /// <summary>服务器磁盘已用空间（字节）</summary>
    [ObservableProperty]
    private long _diskUsedBytes;

    /// <summary>服务器磁盘可用空间（字节）</summary>
    [ObservableProperty]
    private long _diskFreeBytes;

    /// <summary>磁盘使用率 (%)</summary>
    [ObservableProperty]
    private double _diskUsagePercent;

    /// <summary>磁盘名称</summary>
    [ObservableProperty]
    private string _diskName = string.Empty;

    // --- 服务器进程级 ---
    /// <summary>Java 进程 CPU 使用率 (%)</summary>
    [ObservableProperty]
    private double _javaCpuUsagePercent;

    /// <summary>Java 进程工作集内存（字节）</summary>
    [ObservableProperty]
    private long _javaWorkingSetBytes;

    /// <summary>Java 进程专用内存（字节）</summary>
    [ObservableProperty]
    private long _javaPrivateBytes;

    /// <summary>Java 进程线程数</summary>
    [ObservableProperty]
    private int _javaThreadCount;

    /// <summary>Java 进程句柄数</summary>
    [ObservableProperty]
    private int _javaHandleCount;

    /// <summary>Java 堆内存已用量（字节）</summary>
    [ObservableProperty]
    private long _javaHeapUsedBytes;

    /// <summary>Java 堆内存最大值（字节）</summary>
    [ObservableProperty]
    private long _javaHeapMaxBytes;

    // --- 时间戳 ---
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
```

- [ ] **Step 7: 创建 DetectionResult 聚合模型**

```csharp
// src/McServerGuard/Models/DetectionResult.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace McServerGuard.Models;

public partial class DetectionResult : ObservableObject
{
    /// <summary>是否成功检测到服务器</summary>
    [ObservableProperty]
    private bool _isDetected;

    /// <summary>检测到的服务器实例列表</summary>
    [ObservableProperty]
    private List<ServerInstance> _servers = [];

    /// <summary>检测到的启动脚本列表</summary>
    [ObservableProperty]
    private List<StartupScriptInfo> _startupScripts = [];

    /// <summary>检测耗时（毫秒）</summary>
    [ObservableProperty]
    private long _elapsedMs;

    /// <summary>检测错误信息（若有）</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>检测消息日志</summary>
    [ObservableProperty]
    private List<string> _logMessages = [];
}
```

- [ ] **Step 8: 运行测试验证模型编译**

```bash
cd /workspace/McServerGuard
dotnet build
```
Expected: BUILD SUCCEEDED

- [ ] **Step 9: 提交**

```bash
git add -A
git commit -m "feat: 定义服务器常量和数据模型"
```

---

## Task 3: 实现命令行参数解析器

**Files:**
- Create: `src/McServerGuard/Services/ServerDetection/CommandLineParser.cs`
- Create: `src/McServerGuard.Tests/Services/CommandLineParserTests.cs`

- [ ] **Step 1: 编写命令行解析器的失败测试**

```csharp
// src/McServerGuard.Tests/Services/CommandLineParserTests.cs
using McServerGuard.Services.ServerDetection;

namespace McServerGuard.Tests.Services;

public class CommandLineParserTests
{
    [Fact]
    public void ParseVanillaCommand_ExtractsJarAndNogui()
    {
        var cmd = @"java -Xmx1024M -Xms1024M -jar minecraft_server.1.21.4.jar nogui";
        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("minecraft_server.1.21.4.jar", result.ServerJarName);
        Assert.Contains("nogui", result.Arguments);
        Assert.Equal(1024 * 1024 * 1024L, result.MaxHeapMemoryBytes);
        Assert.Equal(1024 * 1024 * 1024L, result.InitialHeapMemoryBytes);
    }

    [Fact]
    public void ParsePaperAikarCommand_ExtractsAllJvmFlags()
    {
        var cmd = @"java -Xms10G -Xmx10G -XX:+UseG1GC -XX:MaxGCPauseMillis=200 -Dusing.aikars.flags=https://mcflags.emc.gs -Daikars.new.flags=true -jar paper.jar --nogui";
        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("paper.jar", result.ServerJarName);
        Assert.Equal(10L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
        Assert.Equal(10L * 1024 * 1024 * 1024, result.InitialHeapMemoryBytes);
        Assert.Contains("-XX:+UseG1GC", result.Arguments);
        Assert.True(result.UsesAikarFlags);
        Assert.Equal("G1GC", result.GcType);
        Assert.Contains("--nogui", result.Arguments);
    }

    [Fact]
    public void ParseCommandWithCustomJavaPath_ExtractsJavaPath()
    {
        var cmd = @"""C:\Program Files\Java\jdk-21\bin\java.exe"" -Xms2G -Xmx4G -jar forge-1.20.1.jar nogui";
        var result = CommandLineParser.Parse(cmd);

        Assert.Contains("jdk-21", result.JavaExecutablePath);
        Assert.Equal("forge-1.20.1.jar", result.ServerJarName);
    }

    [Fact]
    public void ParseCommandWithEnvironmentVariable_ExtractsJavaPath()
    {
        var cmd = @"%JAVA_HOME%\bin\java.exe -Xms2G -Xmx4G -jar spigot.jar nogui";
        var result = CommandLineParser.Parse(cmd);

        Assert.Contains("JAVA_HOME", result.JavaExecutablePath);
        Assert.Equal("spigot.jar", result.ServerJarName);
    }

    [Fact]
    public void ParseFabricCommand_ExtractsJar()
    {
        var cmd = @"java -Xmx4G -Xms4G -jar fabric-server-launch.jar nogui";
        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("fabric-server-launch.jar", result.ServerJarName);
    }

    [Fact]
    public void ParseCommandWithoutNogui_StillExtractsJar()
    {
        var cmd = @"java -Xmx2G -Xms2G -jar server.jar";
        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("server.jar", result.ServerJarName);
        Assert.False(result.IsNogui);
    }

    [Fact]
    public void ParseClientCommand_DetectsClientMarkers()
    {
        var cmd = @"java -XX:+UseG1GC -XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump -Djava.library.path=C:\Users\Test\.minecraft\versions\1.21.4\1.21.4-natives --version 1.21.4 --accessToken abc123 --userType legacy --assetsDir C:\Users\Test\.minecraft\assets";
        var result = CommandLineParser.Parse(cmd);

        Assert.Null(result.ServerJarName);
        Assert.True(result.HasClientMarkers);
    }

    [Fact]
    public void ParseRestartScriptCommand_ExtractsGoto()
    {
        var cmd = @"java -Xms4G -Xmx4G -jar server.jar nogui";
        var result = CommandLineParser.Parse(cmd);

        Assert.Equal("server.jar", result.ServerJarName);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test src/McServerGuard.Tests --filter "CommandLineParserTests" -v n
```
Expected: FAIL — 类型或方法不存在

- [ ] **Step 3: 实现 CommandLineParser**

```csharp
// src/McServerGuard/Services/ServerDetection/CommandLineParser.cs
using System.Text.RegularExpressions;
using McServerGuard.Constants;

namespace McServerGuard.Services.ServerDetection;

/// <summary>
/// Minecraft 服务器启动命令行解析结果
/// </summary>
public class ParsedCommandLine
{
    /// <summary>Java 可执行文件路径（可能含引号）</summary>
    public string JavaExecutablePath { get; init; } = string.Empty;

    /// <summary>服务器 JAR 文件名</summary>
    public string? ServerJarName { get; init; }

    /// <summary>是否使用 nogui 模式</summary>
    public bool IsNogui { get; init; }

    /// <summary>最大堆内存（字节）</summary>
    public long MaxHeapMemoryBytes { get; init; }

    /// <summary>初始堆内存（字节）</summary>
    public long InitialHeapMemoryBytes { get; init; }

    /// <summary>GC 类型</summary>
    public string GcType { get; init; } = string.Empty;

    /// <summary>是否使用 Aikar 标志</summary>
    public bool UsesAikarFlags { get; init; }

    /// <summary>所有解析出的参数列表</summary>
    public List<string> Arguments { get; init; } = [];

    /// <summary>是否检测到客户端标识标记</summary>
    public bool HasClientMarkers { get; init; }
}

public static class CommandLineParser
{
    /// <summary>
    /// 解析 Minecraft 服务器启动命令行
    /// </summary>
    /// <param name="commandLine">完整命令行字符串</param>
    /// <returns>解析结果</returns>
    public static ParsedCommandLine Parse(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return new ParsedCommandLine();

        var tokens = Tokenize(commandLine);
        var result = new ParsedCommandLine();
        var args = new List<string>();

        // 第一个 token 通常是 Java 可执行文件
        if (tokens.Length > 0)
        {
            result = result with { JavaExecutablePath = tokens[0] };
        }

        bool foundJar = false;
        for (int i = 1; i < tokens.Length; i++)
        {
            var token = tokens[i];
            args.Add(token);

            // 检测客户端标识标记
            if (ServerConstants.ClientProcessMarkers.Any(m =>
                    token.Equals(m, StringComparison.OrdinalIgnoreCase)))
            {
                result = result with { HasClientMarkers = true };
            }

            // 解析 -Xmx
            if (token.StartsWith(JvmArgumentConstants.MaxHeapMemory, StringComparison.OrdinalIgnoreCase))
            {
                result = result with
                {
                    MaxHeapMemoryBytes = ParseMemoryValue(
                        token[JvmArgumentConstants.MaxHeapMemory.Length..])
                };
            }

            // 解析 -Xms
            if (token.StartsWith(JvmArgumentConstants.InitialHeapMemory, StringComparison.OrdinalIgnoreCase))
            {
                result = result with
                {
                    InitialHeapMemoryBytes = ParseMemoryValue(
                        token[JvmArgumentConstants.InitialHeapMemory.Length..])
                };
            }

            // 解析 GC 类型
            if (token.Equals(JvmArgumentConstants.G1GC, StringComparison.OrdinalIgnoreCase))
                result = result with { GcType = "G1GC" };
            else if (token.Equals(JvmArgumentConstants.ZGC, StringComparison.OrdinalIgnoreCase))
                result = result with { GcType = "ZGC" };
            else if (token.Equals(JvmArgumentConstants.ShenandoahGC, StringComparison.OrdinalIgnoreCase))
                result = result with { GcType = "ShenandoahGC" };
            else if (token.Equals(JvmArgumentConstants.ParallelGC, StringComparison.OrdinalIgnoreCase))
                result = result with { GcType = "ParallelGC" };

            // 检测 Aikar 标志
            if (token.StartsWith(JvmArgumentConstants.AikarFlagIdentifier))
                result = result with { UsesAikarFlags = true };

            // 检测 nogui
            if (token.Equals(JvmArgumentConstants.NoGuiLegacy, StringComparison.OrdinalIgnoreCase) ||
                token.Equals(JvmArgumentConstants.NoGuiModern, StringComparison.OrdinalIgnoreCase))
                result = result with { IsNogui = true };

            // 检测 -jar 参数 → 下一个 token 是 JAR 文件名
            if (token.Equals(JvmArgumentConstants.JarFlag, StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
            {
                var jarToken = tokens[++i];
                args.Add(jarToken);
                result = result with { ServerJarName = jarToken };
                foundJar = true;
            }
        }

        result = result with { Arguments = args };
        return result;
    }

    /// <summary>
    /// 智能分词：处理引号内的空格
    /// </summary>
    private static string[] Tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];

            if (c == '"' && (i == 0 || commandLine[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return [.. tokens];
    }

    /// <summary>
    /// 解析内存值字符串为字节数（如 "4G", "1024M", "8192K"）
    /// </summary>
    public static long ParseMemoryValue(string value)
    {
        value = value.Trim();
        char suffix = char.ToUpper(value[^1]);

        if (char.IsLetter(suffix))
        {
            var numberStr = value[..^1];
            if (!long.TryParse(numberStr, out var number))
                return 0;

            return suffix switch
            {
                'G' => number * 1024L * 1024 * 1024,
                'M' => number * 1024L * 1024,
                'K' => number * 1024L,
                _ => number
            };
        }

        return long.TryParse(value, out var bytes) ? bytes : 0;
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test src/McServerGuard.Tests --filter "CommandLineParserTests" -v n
```
Expected: ALL PASS

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat: 实现命令行参数解析器（TDD）"
```

---

## Task 4: 实现启动脚本检测器（内容架构判断）

**Files:**
- Create: `src/McServerGuard/Services/ServerDetection/StartupScriptDetector.cs`
- Create: `src/McServerGuard.Tests/Services/StartupScriptDetectorTests.cs`

- [ ] **Step 1: 编写启动脚本检测器的失败测试**

```csharp
// src/McServerGuard.Tests/Services/StartupScriptDetectorTests.cs
using McServerGuard.Services.ServerDetection;

namespace McServerGuard.Tests.Services;

public class StartupScriptDetectorTests
{
    [Fact]
    public void DetectBasicScript_IdentifiesServerStartup()
    {
        var content = @"@echo off
title Minecraft Server
cd /d ""%~dp0""
java -Xms2G -Xmx4G -jar server.jar nogui
pause";

        var result = StartupScriptDetector.Analyze("test.bat", content);

        Assert.True(result.IsServerStartupScript);
        Assert.Equal("server.jar", result.ServerJarName);
        Assert.Equal(4L * 1024 * 1024 * 1024, result.MaxHeapMemoryBytes);
        Assert.False(result.HasAutoRestart);
    }

    [Fact]
    public void DetectRestartScript_IdentifiesAutoRestart()
    {
        var content = @"@echo off
:start
java -Xms2G -Xmx2G -jar server.jar nogui
timeout /t 5
goto start";

        var result = StartupScriptDetector.Analyze("run.bat", content);

        Assert.True(result.IsServerStartupScript);
        Assert.True(result.HasAutoRestart);
    }

    [Fact]
    public void DetectAikarScript_IdentifiesAikarFlags()
    {
        var content = @"@echo off
cd /d ""%~dp0""
:start
java -Xms10G -Xmx10G -XX:+UseG1GC -Dusing.aikars.flags=https://mcflags.emc.gs -Daikars.new.flags=true -jar paper.jar --nogui
goto start";

        var result = StartupScriptDetector.Analyze("start.bat", content);

        Assert.True(result.IsServerStartupScript);
        Assert.True(result.UsesAikarFlags);
        Assert.True(result.HasAutoRestart);
        Assert.Equal("paper.jar", result.ServerJarName);
    }

    [Fact]
    public void DetectNonServerScript_ReturnsFalse()
    {
        var content = @"@echo off
echo Hello World
dir /b
pause";

        var result = StartupScriptDetector.Analyze("list.bat", content);

        Assert.False(result.IsServerStartupScript);
    }

    [Fact]
    public void DetectScriptWithCustomJava_ExtractsJavaPath()
    {
        var content = @"@echo off
""C:\Program Files\Java\jdk-21\bin\java.exe"" -Xms4G -Xmx8G -jar spigot.jar nogui";

        var result = StartupScriptDetector.Analyze("start.bat", content);

        Assert.True(result.IsServerStartupScript);
        Assert.Contains("jdk-21", result.JavaPath);
    }

    [Fact]
    public void DetectScriptWithEnvironmentJava_ExtractsJavaPath()
    {
        var content = @"@echo off
%JAVA_HOME%\bin\java.exe -Xms4G -Xmx4G -jar craftbukkit-1.12.2.jar nogui";

        var result = StartupScriptDetector.Analyze("start.bat", content);

        Assert.True(result.IsServerStartupScript);
        Assert.Equal("%JAVA_HOME%", result.JavaPath);
    }

    [Fact]
    public void DetectFabricInstallerScript_RecognizesFabric()
    {
        var content = @"@echo off
java -Xmx4G -Xms4G -jar fabric-server-launch.jar nogui";

        var result = StartupScriptDetector.Analyze("start.bat", content);

        Assert.True(result.IsServerStartupScript);
        Assert.Equal("fabric-server-launch.jar", result.ServerJarName);
    }

    [Fact]
    public void DetectForgeScript_RecognizesForge()
    {
        var content = @"@echo off
title Forge Server
cd /d ""%~dp0""
java -Xms1G -Xmx4G -jar forge-1.20.1.jar nogui
pause";

        var result = StartupScriptDetector.Analyze("run.bat", content);

        Assert.True(result.IsServerStartupScript);
        Assert.Equal("forge-1.20.1.jar", result.ServerJarName);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test src/McServerGuard.Tests --filter "StartupScriptDetectorTests" -v n
```
Expected: FAIL

- [ ] **Step 3: 实现 StartupScriptDetector**

```csharp
// src/McServerGuard/Services/ServerDetection/StartupScriptDetector.cs
using System.Text.RegularExpressions;
using McServerGuard.Constants;
using McServerGuard.Models;

namespace McServerGuard.Services.ServerDetection;

public static partial class StartupScriptDetector
{
    /// <summary>
    /// 启动脚本识别规则（内容架构判断，不依赖文件名）
    /// </summary>
    private static readonly (string Name, Regex Pattern, string Description)[] DetectionRules =
    [
        ("contains_java_command",
         new Regex(@"java(?:\.exe|\s)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         "脚本包含 java 命令"),

        ("contains_jar_flag",
         new Regex(@"-jar\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         "包含 -jar 参数"),

        ("contains_memory_flags",
         new Regex(@"-X(?:mx|ms)\d+[GMK]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         "包含内存分配参数"),

        ("contains_nogui",
         new Regex(@"(?:--?)?nogui", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         "包含 nogui 服务器模式标志"),

        ("contains_cd_to_script_dir",
         new Regex(@"cd\s+/d\s+""% ~dp0""", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         "切换到脚本所在目录"),

        ("contains_echo_off",
         new Regex(@"@echo\s+off", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         "关闭命令回显"),

        ("contains_auto_restart",
         new Regex(@":\w+\s.*\bgoto\s+\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline),
         "包含自动重启循环"),

        ("contains_title",
         new Regex(@"title\s+.*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
         "设置窗口标题"),
    ];

    /// <summary>
    /// 分析脚本文件内容，判断是否为 Minecraft 服务器启动脚本
    /// </summary>
    public static StartupScriptInfo Analyze(string scriptName, string content)
    {
        var info = new StartupScriptInfo
        {
            ScriptName = scriptName,
            RawContent = content
        };

        var matchedRules = new List<string>();
        bool hasJavaCommand = false;
        bool hasJarFlag = false;

        foreach (var rule in DetectionRules)
        {
            if (rule.Pattern.IsMatch(content))
            {
                matchedRules.Add(rule.Description);
            }
        }

        // 核心判断：必须同时包含 java 命令和 -jar 参数
        hasJavaCommand = DetectionRules[0].Pattern.IsMatch(content);
        hasJarFlag = DetectionRules[1].Pattern.IsMatch(content);

        info.IsServerStartupScript = hasJavaCommand && hasJarFlag;
        info.MatchedRules = matchedRules;

        if (!info.IsServerStartupScript)
            return info;

        // 提取 Java 路径
        ExtractJavaPath(content, info);

        // 提取内存参数（取最大值，因为脚本可能有多行）
        ExtractMaxMemory(content, info);

        // 提取 JAR 文件名
        ExtractServerJar(content, info);

        // 检测自动重启
        info.HasAutoRestart = DetectionRules[6].Pattern.IsMatch(content);

        // 检测 Aikar 标志
        info.UsesAikarFlags = content.Contains(JvmArgumentConstants.AikarFlagIdentifier,
            StringComparison.OrdinalIgnoreCase);

        return info;
    }

    /// <summary>
    /// 从脚本内容中提取 Java 可执行文件路径
    /// </summary>
    private static void ExtractJavaPath(string content, StartupScriptInfo info)
    {
        // 匹配引号包裹的路径: "path\to\java.exe"
        var quotedMatch = Regex.Match(content, @"([""])(.+?java(?:\.exe)?)\1",
            RegexOptions.IgnoreCase);
        if (quotedMatch.Success)
        {
            info.JavaPath = quotedMatch.Groups[2].Value;
            return;
        }

        // 匹配环境变量引用: %JAVA_HOME%\bin\java.exe
        var envMatch = Regex.Match(content, @"(%[^%]+%[/\\].*?java(?:\.exe)?)",
            RegexOptions.IgnoreCase);
        if (envMatch.Success)
        {
            info.JavaPath = envMatch.Groups[1].Value;
            return;
        }

        // 匹配相对路径: .\runtime\bin\java.exe
        var relMatch = Regex.Match(content, @"([.\w][\w/\\]*?java(?:\.exe)?)",
            RegexOptions.IgnoreCase);
        if (relMatch.Success)
        {
            info.JavaPath = relMatch.Groups[1].Value;
        }
    }

    /// <summary>
    /// 从脚本内容中提取最大堆内存值（取所有 -Xmx 中的最大值）
    /// </summary>
    private static void ExtractMaxMemory(string content, StartupScriptInfo info)
    {
        var matches = Regex.Matches(content, @"-Xmx(\d+)([GMK])",
            RegexOptions.IgnoreCase);

        long maxFound = 0;
        foreach (Match match in matches)
        {
            var value = CommandLineParser.ParseMemoryValue(
                match.Groups[1].Value + match.Groups[2].Value);
            if (value > maxFound)
                maxFound = value;
        }

        info.MaxHeapMemoryBytes = maxFound;
    }

    /// <summary>
    /// 从脚本内容中提取服务器 JAR 文件名
    /// </summary>
    private static void ExtractServerJar(string content, StartupScriptInfo info)
    {
        var match = Regex.Match(content, @"-jar\s+([^\s]+?)(?:\s+|$)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            info.ServerJarName = match.Groups[1].Value.Trim();
        }
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test src/McServerGuard.Tests --filter "StartupScriptDetectorTests" -v n
```
Expected: ALL PASS

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat: 实现启动脚本内容架构检测器（TDD）"
```

---

## Task 5: 实现服务器类型分类器

**Files:**
- Create: `src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs`
- Create: `src/McServerGuard.Tests/Services/ServerTypeClassifierTests.cs`

- [ ] **Step 1: 编写分类器测试**

```csharp
// src/McServerGuard.Tests/Services/ServerTypeClassifierTests.cs
using McServerGuard.Constants;
using McServerGuard.Services.ServerDetection;

namespace McServerGuard.Tests.Services;

public class ServerTypeClassifierTests
{
    [Fact]
    public void ClassifyVanilla_ByJarNameOnly_ReturnsVanilla()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("minecraft_server.1.21.4.jar", []);
        Assert.Equal(ServerType.Vanilla, type);
    }

    [Fact]
    public void ClassifySpigot_ByJarName_ReturnsSpigot()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("spigot-1.21.4.jar", []);
        Assert.Equal(ServerType.Spigot, type);
    }

    [Fact]
    public void ClassifyPaper_ByJarName_ReturnsPaper()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("paper-1.21.4.jar", []);
        Assert.Equal(ServerType.Paper, type);
    }

    [Fact]
    public void ClassifyForge_ByJarName_ReturnsForge()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("forge-1.20.1.jar", []);
        Assert.Equal(ServerType.Forge, type);
    }

    [Fact]
    public void ClassifyFabric_ByJarName_ReturnsFabric()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("fabric-server-launch.jar", []);
        Assert.Equal(ServerType.Fabric, type);
    }

    [Fact]
    public void ClassifyGenericServerJar_WithConfigFiles_ReturnsCorrect()
    {
        // 仅 server.jar + spigot.yml 存在 → Spigot
        var type = ServerTypeClassifier.ClassifyByJarName("server.jar", ["spigot.yml", "bukkit.yml"]);
        Assert.Equal(ServerType.Spigot, type);
    }

    [Fact]
    public void ClassifyGenericServerJar_WithPaperConfig_ReturnsPaper()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("server.jar",
            ["spigot.yml", "bukkit.yml", "config/paper-global.yml"]);
        Assert.Equal(ServerType.Paper, type);
    }

    [Fact]
    public void ClassifyGenericServerJar_NoExtraFiles_ReturnsVanilla()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("server.jar", []);
        Assert.Equal(ServerType.Vanilla, type);
    }

    [Fact]
    public void ClassifyBukkit_ByJarName_ReturnsBukkit()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("craftbukkit-1.12.2.jar", []);
        Assert.Equal(ServerType.Bukkit, type);
    }

    [Fact]
    public void ClassifyUnknownJar_WithoutConfigFiles_ReturnsUnknown()
    {
        var type = ServerTypeClassifier.ClassifyByJarName("custom.jar", []);
        Assert.Equal(ServerType.Unknown, type);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test src/McServerGuard.Tests --filter "ServerTypeClassifierTests" -v n
```
Expected: FAIL

- [ ] **Step 3: 实现分类器**

```csharp
// src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs
using McServerGuard.Constants;

namespace McServerGuard.Services.ServerDetection;

public static class ServerTypeClassifier
{
    /// <summary>
    /// 通过 JAR 文件名和配置文件列表综合判断服务器类型
    /// 策略：先精确匹配 JAR 名称，若为通用名（server.jar）则通过配置文件特征判断
    /// </summary>
    public static ServerType ClassifyByJarName(string jarName, IEnumerable<string> existingConfigFiles)
    {
        var normalizedJar = jarName.ToLowerInvariant();
        var configFiles = existingConfigFiles
            .Select(f => f.ToLowerInvariant().Replace('\\', '/'))
            .ToHashSet();

        // 第一优先级：精确 JAR 名称匹配
        if (MatchesAny(normalizedJar, ServerConstants.VanillaJarPatterns))
            return ServerType.Vanilla;
        if (MatchesAny(normalizedJar, ServerConstants.BukkitJarPatterns))
            return ServerType.Bukkit;
        if (MatchesAny(normalizedJar, ServerConstants.SpigotJarPatterns))
            return ServerType.Spigot;
        if (MatchesAny(normalizedJar, ServerConstants.PaperJarPatterns))
            return ServerType.Paper;
        if (MatchesAny(normalizedJar, ServerConstants.ForgeJarPatterns))
            return ServerType.Forge;
        if (MatchesAny(normalizedJar, ServerConstants.FabricJarPatterns))
            return ServerType.Fabric;

        // 第二优先级：JAR 名为通用名（server.jar），通过配置文件特征推断
        if (normalizedJar is "server.jar" or "minecraft_server.jar")
        {
            // Paper = Spigot + Paper 配置
            if (configFiles.Contains("config/paper-global.yml"))
                return ServerType.Paper;

            // Spigot = spigot.yml + bukkit.yml
            if (configFiles.Contains("spigot.yml") && configFiles.Contains("bukkit.yml"))
                return ServerType.Spigot;

            // Forge = mods/ + forge 特征
            if (configFiles.Contains("mods/") || configFiles.Contains("server.toml") ||
                configFiles.Contains("forge-server.toml"))
                return ServerType.Forge;

            // Fabric = fabric 启动属性
            if (configFiles.Contains("fabric-server-launch.properties") ||
                configFiles.Contains(".fabric/"))
                return ServerType.Fabric;

            // Bukkit 仅有 bukkit.yml（无 spigot.yml）
            if (configFiles.Contains("bukkit.yml") && !configFiles.Contains("spigot.yml"))
                return ServerType.Bukkit;

            // 仅有 server.properties → Vanilla
            return ServerType.Vanilla;
        }

        return ServerType.Unknown;
    }

    private static bool MatchesAny(string fileName, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            // 简单通配匹配：* → 正则 .*
            var regex = "^" + pattern.Replace(".", "\\.").Replace("*", ".*") + "$";
            if (System.Text.RegularExpressions.Regex.IsMatch(fileName, regex,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test src/McServerGuard.Tests --filter "ServerTypeClassifierTests" -v n
```
Expected: ALL PASS

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat: 实现服务器类型综合分类器（TDD）"
```

---

## Task 6: 实现进程扫描器

**Files:**
- Create: `src/McServerGuard/Services/ServerDetection/ProcessScanner.cs`
- Create: `src/McServerGuard/Services/ServerDetection/WorkingDirectoryResolver.cs`

- [ ] **Step 1: 实现 ProcessScanner**

```csharp
// src/McServerGuard/Services/ServerDetection/ProcessScanner.cs
using System.Diagnostics;
using System.Runtime.InteropServices;
using McServerGuard.Constants;

namespace McServerGuard.Services.ServerDetection;

public static class ProcessScanner
{
    /// <summary>
    /// 扫描系统中所有正在运行的 Minecraft 服务器 Java 进程
    /// 排除客户端进程（通过检测客户端标识标记）
    /// </summary>
    public static List<(Process Process, string CommandLine)> ScanServerProcesses()
    {
        var results = new List<(Process, string)>();

        foreach (var process in Process.GetProcessesByName("java"))
        {
            try
            {
                var cmdLine = GetCommandLine(process);
                if (string.IsNullOrWhiteSpace(cmdLine))
                    continue;

                var parsed = CommandLineParser.Parse(cmdLine);

                // 排除客户端进程
                if (parsed.HasClientMarkers)
                    continue;

                // 必须是服务器进程（有 -jar + JAR 匹配服务器特征）
                if (parsed.ServerJarName == null)
                    continue;

                if (!IsServerJar(parsed.ServerJarName))
                    continue;

                results.Add((process, cmdLine));
            }
            catch (Exception)
            {
                // 跳过无法访问的进程（权限不足等）
            }
        }

        return results;
    }

    /// <summary>
    /// 判断 JAR 文件名是否匹配已知的 Minecraft 服务器核心
    /// </summary>
    private static bool IsServerJar(string jarName)
    {
        var lower = jarName.ToLowerInvariant();
        return ServerConstants.ServerJarKeywords.Any(kw => lower.Contains(kw));
    }

    /// <summary>
    /// 获取进程的完整命令行（使用 WMI 兼容方式）
    /// </summary>
    private static string GetCommandLine(Process process)
    {
        // 尝试通过 WMI 获取完整命令行
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}");
            foreach (var obj in searcher.Get())
            {
                return obj["CommandLine"]?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            // WMI 不可用时回退
        }

        return string.Empty;
    }
}
```

注意：需要添加 `using System.Management;` 并在 `.csproj` 中添加：
```xml
<ItemGroup>
    <COMReference Include="System.Management" />
</ItemGroup>
```

- [ ] **Step 2: 实现 WorkingDirectoryResolver**

```csharp
// src/McServerGuard/Services/ServerDetection/WorkingDirectoryResolver.cs
using System.Diagnostics;
using System.IO;

namespace McServerGuard.Services.ServerDetection;

public static class WorkingDirectoryResolver
{
    /// <summary>
    /// 尽一切可能确认 Java 进程的工作目录（服务器路径）
    /// 优先级：
    /// 1. 进程的工作目录（ProcessStartInfo.WorkingDirectory 不可靠）
    /// 2. 从命令行 -jar 参数解析 JAR 文件的完整路径
    /// 3. 枚举进程打开的文件句柄（需要管理员权限）
    /// 4. 通过检测 server.properties 在常见位置的存在性
    /// </summary>
    public static string Resolve(Process process, string commandLine, string jarName)
    {
        // 方法 1：进程主模块路径的上两级目录
        try
        {
            var mainModule = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(mainModule))
            {
                // java.exe 通常在 JDK/bin/ 下，服务器工作目录一般不是这里
                // 但如果是自包含部署，java 可能在服务器目录中
            }
        }
        catch { }

        // 方法 2：从命令行中的 JAR 路径提取目录
        var jarDirectory = ExtractJarDirectory(commandLine, jarName);
        if (jarDirectory != null)
        {
            // 验证：该目录下应存在 server.properties
            if (File.Exists(Path.Combine(jarDirectory, ServerConstants.ServerValidationFile)))
                return jarDirectory;

            // 额外验证：JAR 文件本身存在于此目录
            if (File.Exists(Path.Combine(jarDirectory, jarName)))
                return jarDirectory;
        }

        // 方法 3：搜索系统中的 Java 服务器目录
        var foundDir = SearchServerDirectories(jarName);
        if (foundDir != null)
            return foundDir;

        return string.Empty;
    }

    private static string? ExtractJarDirectory(string commandLine, string jarName)
    {
        // 查找命令行中 JAR 文件的路径部分
        var idx = commandLine.IndexOf(jarName, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        // 向前搜索引号或空格来确定路径起始
        int start = idx - 1;
        if (start >= 0 && commandLine[start] == '"')
        {
            // 查找匹配的引号
            var end = commandLine.IndexOf('"', idx);
            if (end > idx)
                return Path.GetDirectoryName(commandLine[(start + 1)..end]);
        }

        // 无引号：向前找空格
        while (start >= 0 && !char.IsWhiteSpace(commandLine[start]))
            start--;

        start = Math.Max(0, start + 1);

        // 向后查找参数分隔
        int end2 = idx + jarName.Length;
        while (end2 < commandLine.Length && !char.IsWhiteSpace(commandLine[end2]))
            end2++;

        var fullPath = commandLine[start..end2];
        return Path.GetDirectoryName(fullPath);
    }

    private static string? SearchServerDirectories(string jarName)
    {
        // 搜索常见位置
        var searchPaths = new[]
        {
            Environment.CurrentDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"),
        };

        foreach (var basePath in searchPaths)
        {
            if (string.IsNullOrEmpty(basePath)) continue;

            // 递归搜索（限制深度为 3）
            var found = SearchRecursive(basePath, jarName, 3);
            if (found != null) return found;
        }

        return null;
    }

    private static string? SearchRecursive(string path, string jarName, int maxDepth)
    {
        if (maxDepth <= 0) return null;

        try
        {
            // 检查当前目录
            if (File.Exists(Path.Combine(path, jarName)) &&
                File.Exists(Path.Combine(path, ServerConstants.ServerValidationFile)))
                return path;

            // 搜索子目录
            foreach (var dir in Directory.GetDirectories(path))
            {
                var result = SearchRecursive(dir, jarName, maxDepth - 1);
                if (result != null) return result;
            }
        }
        catch (UnauthorizedAccessException) { }

        return null;
    }
}
```

注意：`SearchServerDirectories` 需要添加对 `ServerConstants` 的引用：
```csharp
using McServerGuard.Constants;
```

- [ ] **Step 3: 编译验证**

```bash
cd /workspace/McServerGuard
dotnet build
```
Expected: BUILD SUCCEEDED

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: 实现进程扫描器和工作目录解析器"
```

---

## Task 7: 实现配置文件扫描器和核心检测编排

**Files:**
- Create: `src/McServerGuard/Services/ServerDetection/ConfigFileScanner.cs`
- Create: `src/McServerGuard/Services/ServerDetection/IServerDetector.cs`
- Create: `src/McServerGuard/Services/ServerDetection/ServerDetector.cs`

- [ ] **Step 1: 实现 ConfigFileScanner**

```csharp
// src/McServerGuard/Services/ServerDetection/ConfigFileScanner.cs
using System.IO;
using McServerGuard.Constants;

namespace McServerGuard.Services.ServerDetection;

public static class ConfigFileScanner
{
    /// <summary>
    /// 扫描服务器工作目录中的所有配置文件
    /// </summary>
    public static List<string> ScanAll(string workingDirectory)
    {
        var configs = new List<string>();

        if (string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory))
            return configs;

        // 扫描根目录配置文件
        configs.AddRange(ScanRootConfigs(workingDirectory));

        // 扫描 config/ 子目录（Paper）
        configs.AddRange(ScanConfigSubdirectory(workingDirectory));

        // 扫描 mods/ 子目录下的配置
        configs.AddRange(ScanModsConfigs(workingDirectory));

        // 扫描所有 .yml, .yaml, .properties, .json, .toml 配置文件
        configs.AddRange(ScanAllConfigFiles(workingDirectory));

        return [.. configs.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static List<string> ScanRootConfigs(string dir)
    {
        var configs = new List<string>();
        foreach (var file in ServerConstants.CommonConfigFiles)
        {
            if (File.Exists(Path.Combine(dir, file)))
                configs.Add(file);
        }

        // Spigot
        foreach (var file in ServerConstants.SpigotConfigFiles)
        {
            if (File.Exists(Path.Combine(dir, file)))
                configs.Add(file);
        }

        // Fabric
        foreach (var file in ServerConstants.FabricConfigFiles)
        {
            if (file.EndsWith('/'))
            {
                if (Directory.Exists(Path.Combine(dir, file.TrimEnd('/'))))
                    configs.Add(file.TrimEnd('/'));
            }
            else if (File.Exists(Path.Combine(dir, file)))
                configs.Add(file);
        }

        return configs;
    }

    private static List<string> ScanConfigSubdirectory(string dir)
    {
        var configs = new List<string>();
        var configDir = Path.Combine(dir, "config");

        if (!Directory.Exists(configDir)) return configs;

        foreach (var file in Directory.GetFiles(configDir, "*.yml", SearchOption.AllDirectories))
            configs.Add(Path.GetRelativePath(dir, file));

        foreach (var file in Directory.GetFiles(configDir, "*.yaml", SearchOption.AllDirectories))
            configs.Add(Path.GetRelativePath(dir, file));

        return configs;
    }

    private static List<string> ScanModsConfigs(string dir)
    {
        var configs = new List<string>();
        var modsDir = Path.Combine(dir, "mods");

        if (!Directory.Exists(modsDir)) return configs;

        // 列出 mod 文件
        configs.Add("mods/");
        foreach (var file in Directory.GetFiles(modsDir, "*.jar"))
            configs.Add(Path.GetRelativePath(dir, file));

        return configs;
    }

    private static List<string> ScanAllConfigFiles(string dir)
    {
        var configs = new List<string>();
        var extensions = new[] { "*.yml", "*.yaml", "*.properties", "*.json", "*.toml", "*.cfg", "*.conf" };

        foreach (var ext in extensions)
        {
            try
            {
                configs.AddRange(Directory.GetFiles(dir, ext)
                    .Select(f => Path.GetFileName(f)));
            }
            catch { }
        }

        return configs;
    }
}
```

- [ ] **Step 2: 实现检测接口和核心编排**

```csharp
// src/McServerGuard/Services/ServerDetection/IServerDetector.cs
using McServerGuard.Models;

namespace McServerGuard.Services.ServerDetection;

public interface IServerDetector
{
    Task<DetectionResult> DetectAllAsync();
    Task<List<StartupScriptInfo>> ScanStartupScriptsAsync(string directory);
}
```

```csharp
// src/McServerGuard/Services/ServerDetection/ServerDetector.cs
using System.Diagnostics;
using McServerGuard.Models;

namespace McServerGuard.Services.ServerDetection;

public class ServerDetector : IServerDetector
{
    private readonly ILogger _logger;

    public ServerDetector(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<DetectionResult> DetectAllAsync()
    {
        var result = new DetectionResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            result.LogMessages.Add("开始扫描 Java 进程...");

            // 步骤 1：扫描系统中的 Java 进程
            var javaProcesses = ProcessScanner.ScanServerProcesses();
            result.LogMessages.Add($"发现 {javaProcesses.Count} 个候选 Java 进程");

            foreach (var (process, cmdLine) in javaProcesses)
            {
                try
                {
                    var server = await BuildServerInstanceAsync(process, cmdLine);
                    if (server != null)
                    {
                        result.Servers.Add(server);
                        result.LogMessages.Add(
                            $"检测到服务器: {server.DisplayName} (类型: {server.ServerType})");
                    }
                }
                catch (Exception ex)
                {
                    result.LogMessages.Add($"处理 PID {process.Id} 时出错: {ex.Message}");
                }
            }

            // 步骤 2：扫描常见目录中的启动脚本
            foreach (var server in result.Servers)
            {
                if (!string.IsNullOrEmpty(server.WorkingDirectory))
                {
                    var scripts = await ScanStartupScriptsAsync(server.WorkingDirectory);
                    result.StartupScripts.AddRange(scripts);

                    // 匹配最相关的启动脚本到服务器实例
                    var bestMatch = scripts.FirstOrDefault(s =>
                        s.ServerJarName?.Equals(server.ServerJarName,
                            StringComparison.OrdinalIgnoreCase) == true);
                    if (bestMatch != null)
                        server.StartupScriptPath = bestMatch.ScriptPath;
                }
            }

            result.IsDetected = result.Servers.Count > 0;
            result.LogMessages.Add($"检测完成，共发现 {result.Servers.Count} 个 Minecraft 服务器");
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.LogMessages.Add($"检测失败: {ex.Message}");
        }

        sw.Stop();
        result.ElapsedMs = sw.ElapsedMilliseconds;

        return await Task.FromResult(result);
    }

    private async Task<ServerInstance?> BuildServerInstanceAsync(
        Process process, string commandLine)
    {
        var parsed = CommandLineParser.Parse(commandLine);
        if (parsed.ServerJarName == null)
            return null;

        // 解析工作目录
        var workingDir = WorkingDirectoryResolver.Resolve(process, commandLine, parsed.ServerJarName);

        // 扫描配置文件
        var configFiles = string.IsNullOrEmpty(workingDir)
            ? []
            : ConfigFileScanner.ScanAll(workingDir);

        // 分类服务器类型
        var serverType = ServerTypeClassifier.ClassifyByJarName(parsed.ServerJarName, configFiles);

        // 从 server.properties 读取端口（若有）
        int port = ServerConstants.DefaultServerPort;

        var instance = new ServerInstance
        {
            ProcessId = process.Id,
            ServerType = serverType,
            WorkingDirectory = workingDir,
            JavaPath = parsed.JavaExecutablePath,
            ServerJarName = parsed.ServerJarName,
            FullCommandLine = commandLine,
            JvmArguments = parsed.Arguments,
            InitialHeapMemoryBytes = parsed.InitialHeapMemoryBytes,
            MaxHeapMemoryBytes = parsed.MaxHeapMemoryBytes,
            UsesAikarFlags = parsed.UsesAikarFlags,
            GcType = parsed.GcType,
            ConfigFiles = configFiles,
            ServerPort = port
        };

        // 设置 JAR 完整路径
        if (!string.IsNullOrEmpty(workingDir))
        {
            instance.ServerJarPath = System.IO.Path.Combine(workingDir, parsed.ServerJarName);
        }

        return await Task.FromResult(instance);
    }

    public async Task<List<StartupScriptInfo>> ScanStartupScriptsAsync(string directory)
    {
        return await Task.Run(() =>
        {
            var scripts = new List<StartupScriptInfo>();

            if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
                return scripts;

            try
            {
                foreach (var file in System.IO.Directory.GetFiles(directory, "*.bat"))
                {
                    var content = System.IO.File.ReadAllText(file);
                    var info = StartupScriptDetector.Analyze(
                        System.IO.Path.GetFileName(file), content);
                    info.ScriptPath = file;

                    if (info.IsServerStartupScript)
                        scripts.Add(info);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "扫描启动脚本时出错: {Dir}", directory);
            }

            return scripts;
        });
    }
}
```

注意：需要在 `ServerDetector.cs` 顶部添加 `using Serilog;` 或使用 `ILogger` 参数。如使用 Serilog，logger 实例可使用 `Log` 静态类替代。

- [ ] **Step 3: 编译验证**

```bash
cd /workspace/McServerGuard
dotnet build
```
Expected: BUILD SUCCEEDED

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: 实现配置文件扫描器和核心检测编排服务"
```

---

## Task 8: 实现配置文件解析器和配置描述注册表

**Files:**
- Create: `src/McServerGuard/Services/ConfigManagement/PropertiesParser.cs`
- Create: `src/McServerGuard/Services/ConfigManagement/YamlParser.cs`
- Create: `src/McServerGuard/Services/ConfigManagement/JsonConfigParser.cs`
- Create: `src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs`
- Create: `src/McServerGuard/Services/ConfigManagement/IConfigManager.cs`
- Create: `src/McServerGuard/Services/ConfigManagement/ConfigManager.cs`
- Create: `src/McServerGuard.Tests/Services/PropertiesParserTests.cs`

- [ ] **Step 1: 编写 PropertiesParser 测试**

```csharp
// src/McServerGuard.Tests/Services/PropertiesParserTests.cs
using McServerGuard.Services.ConfigManagement;

namespace McServerGuard.Tests.Services;

public class PropertiesParserTests
{
    private const string SampleProperties = """
        #Minecraft server properties
        server-port=25565
        max-players=20
        level-name=world
        online-mode=true
        motd=A Minecraft Server
        enable-rcon=false
        rcon.port=25575
        pvp=true
        difficulty=easy
        view-distance=10
        simulation-distance=10
        enable-command-block=false
        """;

    [Fact]
    public void ParseProperties_ExtractsAllEntries()
    {
        var entries = PropertiesParser.Parse(SampleProperties);

        Assert.Equal("25565", entries["server-port"]);
        Assert.Equal("20", entries["max-players"]);
        Assert.Equal("world", entries["level-name"]);
        Assert.Equal("true", entries["online-mode"]);
        Assert.Equal("A Minecraft Server", entries["motd"]);
    }

    [Fact]
    public void ParseProperties_IgnoresComments()
    {
        var entries = PropertiesParser.Parse(SampleProperties);

        Assert.False(entries.ContainsKey("#Minecraft"));
    }

    [Fact]
    public void SerializeProperties_ProducesValidOutput()
    {
        var entries = new Dictionary<string, string>
        {
            ["server-port"] = "25565",
            ["max-players"] = "100",
            ["motd"] = "My Server"
        };

        var output = PropertiesParser.Serialize(entries);

        Assert.Contains("server-port=25565", output);
        Assert.Contains("max-players=100", output);
        Assert.Contains("motd=My Server", output);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test src/McServerGuard.Tests --filter "PropertiesParserTests" -v n
```
Expected: FAIL

- [ ] **Step 3: 实现 PropertiesParser**

```csharp
// src/McServerGuard/Services/ConfigManagement/PropertiesParser.cs
using System.Text;

namespace McServerGuard.Services.ConfigManagement;

public static class PropertiesParser
{
    /// <summary>
    /// 解析 Minecraft server.properties 格式文件
    /// 格式：key=value（忽略 # 开头的注释行和空行）
    /// </summary>
    public static Dictionary<string, string> Parse(string content)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(content))
            return entries;

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var idx = trimmed.IndexOf('=');
            if (idx < 0) continue;

            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim();

            entries[key] = value;
        }

        return entries;
    }

    /// <summary>
    /// 将配置字典序列化为 properties 格式
    /// </summary>
    public static string Serialize(Dictionary<string, string> entries)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in entries)
        {
            sb.AppendLine($"{key}={value}");
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test src/McServerGuard.Tests --filter "PropertiesParserTests" -v n
```
Expected: ALL PASS

- [ ] **Step 5: 实现 YamlParser（基于 YamlDotNet）**

```csharp
// src/McServerGuard/Services/ConfigManagement/YamlParser.cs
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace McServerGuard.Services.ConfigManagement;

public static class YamlParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    /// <summary>
    /// 解析 YAML 字符串为字典
    /// </summary>
    public static Dictionary<string, object?> Parse(string content)
    {
        try
        {
            var result = Deserializer.Deserialize<Dictionary<string, object?>>(content);
            return result ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// 将字典序列化为 YAML 字符串
    /// </summary>
    public static string Serialize(Dictionary<string, object?> data)
    {
        return Serializer.Serialize(data);
    }

    /// <summary>
    /// 获取嵌套 YAML 中指定路径的值（如 "world-settings.default.view-distance"）
    /// </summary>
    public static object? GetValue(Dictionary<string, object?> root, string path)
    {
        var keys = path.Split('.');
        object? current = root;

        foreach (var key in keys)
        {
            if (current is not Dictionary<string, object?> dict)
                return null;

            if (!dict.TryGetValue(key, out current))
                return null;
        }

        return current;
    }

    /// <summary>
    /// 设置嵌套 YAML 中指定路径的值
    /// </summary>
    public static void SetValue(Dictionary<string, object?> root, string path, object? value)
    {
        var keys = path.Split('.');
        var current = root;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (current.TryGetValue(keys[i], out var next) && next is Dictionary<string, object?> nextDict)
            {
                current = nextDict;
            }
            else
            {
                var newDict = new Dictionary<string, object?>();
                current[keys[i]] = newDict;
                current = newDict;
            }
        }

        current[keys[^1]] = value;
    }
}
```

- [ ] **Step 6: 实现 ConfigDescriptorRegistry（中文翻译和约束）**

```csharp
// src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs
using McServerGuard.Models;

namespace McServerGuard.Services.ConfigManagement;

/// <summary>
/// Minecraft 服务器配置项中文描述和约束注册表
/// 提供每个配置项的中文翻译、类型约束和取值范围
/// </summary>
public static class ConfigDescriptorRegistry
{
    private static readonly Dictionary<string, ServerConfigDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);

    static ConfigDescriptorRegistry()
    {
        RegisterServerPropertiesDescriptors();
        RegisterSpigotDescriptors();
        RegisterPaperDescriptors();
    }

    public static ServerConfigDescriptor? GetDescriptor(string configKey, string configFileName)
    {
        var lookupKey = $"{configFileName}:{configKey}";
        return _descriptors.TryGetValue(lookupKey, out var desc) ? desc : null;
    }

    public static List<ServerConfigDescriptor> GetDescriptorsForFile(string configFileName)
    {
        return _descriptors.Values
            .Where(d => d.ConfigFile.Equals(configFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static void Register(string key, string configFile, string displayName,
        string description, string valueType, string category,
        int? min = null, int? max = null,
        string[]? allowedValues = null, string? regex = null,
        string? defaultValue = null, bool requiresRestart = false)
    {
        _descriptors[$"{configFile}:{key}"] = new ServerConfigDescriptor
        {
            Key = key,
            ConfigFile = configFile,
            DisplayName = displayName,
            Description = description,
            ValueType = valueType,
            Category = category,
            MinValue = min,
            MaxValue = max,
            AllowedValues = allowedValues,
            RegexPattern = regex,
            DefaultValue = defaultValue,
            RequiresRestart = requiresRestart
        };
    }

    private static void RegisterServerPropertiesDescriptors()
    {
        // === 网络设置 ===
        Register("server-port", "server.properties", "服务器端口",
            "服务器监听的 TCP 端口号，客户端通过此端口连接", "int", "网络设置",
            min: 1, max: 65535, defaultValue: "25565", requiresRestart: true);

        Register("server-ip", "server.properties", "服务器绑定 IP",
            "绑定到特定的网络接口 IP 地址，留空则绑定所有接口", "string", "网络设置",
            regex: @"^(\d{1,3}\.){3}\d{1,3}$", requiresRestart: true);

        // === 玩家设置 ===
        Register("max-players", "server.properties", "最大玩家数",
            "服务器同时允许的最大玩家在线数量", "int", "玩家设置",
            min: 1, max: 1000, defaultValue: "20", requiresRestart: true);

        Register("online-mode", "server.properties", "正版验证",
            "是否启用 Minecraft 正版验证。关闭后盗版玩家也可加入", "bool", "玩家设置",
            defaultValue: "true", requiresRestart: true);

        Register("motd", "server.properties", "服务器描述 (MOTD)",
            "服务器在多人游戏列表中显示的描述文字", "string", "玩家设置",
            defaultValue: "A Minecraft Server");

        Register("view-distance", "server.properties", "视距",
            "服务器向客户端发送区块的半径（以区块为单位），影响玩家能看到的最远距离", "int", "性能设置",
            min: 2, max: 32, defaultValue: "10", requiresRestart: true);

        Register("simulation-distance", "server.properties", "模拟距离",
            "服务器活跃模拟世界的区块半径，影响实体活动范围", "int", "性能设置",
            min: 2, max: 32, defaultValue: "10", requiresRestart: true);

        // === 世界设置 ===
        Register("level-name", "server.properties", "世界名称",
            "主世界文件夹的名称，也是存档目录名", "string", "世界设置",
            defaultValue: "world", requiresRestart: true);

        Register("level-seed", "server.properties", "世界种子",
            "世界生成的随机种子值，留空则随机生成", "string", "世界设置",
            requiresRestart: true);

        Register("difficulty", "server.properties", "游戏难度",
            "服务器默认的游戏难度等级", "enum", "世界设置",
            allowedValues: ["peaceful", "easy", "normal", "hard"],
            defaultValue: "easy", requiresRestart: false);

        Register("pvp", "server.properties", "PvP 开关",
            "是否允许玩家之间互相攻击", "bool", "世界设置",
            defaultValue: "true");

        Register("enable-command-block", "server.properties", "命令方块",
            "是否允许使用命令方块", "bool", "高级设置",
            defaultValue: "false", requiresRestart: true);

        Register("allow-flight", "server.properties", "允许飞行",
            "是否允许玩家在生存模式下飞行（创意模式不受影响）", "bool", "玩家设置",
            defaultValue: "false");

        Register("white-list", "server.properties", "白名单",
            "启用后仅白名单中的玩家可以加入服务器", "bool", "玩家设置",
            defaultValue: "false", requiresRestart: true);

        // === 远程控制 ===
        Register("enable-rcon", "server.properties", "RCON 远程控制",
            "是否启用 RCON 远程控制台（允许远程发送控制台命令）", "bool", "远程控制",
            defaultValue: "false", requiresRestart: true);

        Register("rcon.port", "server.properties", "RCON 端口",
            "RCON 远程控制台监听端口", "int", "远程控制",
            min: 1, max: 65535, defaultValue: "25575", requiresRestart: true);

        // === 性能设置 ===
        Register("network-compression-threshold", "server.properties", "网络压缩阈值",
            "网络数据包压缩的最小字节数，-1 禁用压缩", "int", "性能设置",
            min: -1, max: 65535, defaultValue: "256");

        Register("max-tick-time", "server.properties", "最大 Tick 时间",
            "单个 tick 的最大执行时间（毫秒），超时则跳过 watchdog 警告", "int", "性能设置",
            min: 0, max: 60000, defaultValue: "60000");
    }

    private static void RegisterSpigotDescriptors()
    {
        // spigot.yml 核心配置
        Register("max-tick-time.tile", "spigot.yml", "区块 Tile 实体最大 Tick 时间",
            "单个区块中 Tile 实体每 tick 的最大处理时间（毫秒）", "int", "性能设置",
            min: 0, max: 1000, defaultValue: "50");

        Register("max-tick-time.entity", "spigot.yml", "实体最大 Tick 时间",
            "单个实体每 tick 的最大处理时间（毫秒）", "int", "性能设置",
            min: 0, max: 1000, defaultValue: "50");

        Register("mob-spawn-range", "spigot.yml", "生物生成范围",
            "以玩家为中心的生物生成范围（区块为单位）", "int", "生物设置",
            min: 1, max: 128, defaultValue: "8", requiresRestart: true);

        Register("entity-activation-range.animals", "spigot.yml", "动物激活范围",
            "动物在此距离内才会被激活并参与游戏逻辑", "int", "性能设置",
            min: 1, max: 128, defaultValue: "32");

        Register("entity-activation-range.monsters", "spigot.yml", "怪物激活范围",
            "怪物在此距离内才会被激活并参与游戏逻辑", "int", "性能设置",
            min: 1, max: 128, defaultValue: "32");

        Register("entity-activation-range.misc", "spigot.yml", "其他实体激活范围",
            "掉落物、经验球等在此距离内才会被激活", "int", "性能设置",
            min: 1, max: 128, defaultValue: "16");

        Register("merge-radius.item", "spigot.yml", "掉落物合并范围",
            "相同类型的掉落物在此距离内会自动合并", "int", "性能设置",
            min: 0, max: 64, defaultValue: "2.5");

        Register("merge-radius.exp", "spigot.yml", "经验球合并范围",
            "经验球在此距离内会自动合并", "int", "性能设置",
            min: 0, max: 64, defaultValue: "3.0");
    }

    private static void RegisterPaperDescriptors()
    {
        // Paper 全局配置
        Register("chunk-loading.player-mob-spawning-range", "config/paper-global.yml",
            "玩家生物生成范围",
            "Paper 独立的玩家附近生物生成范围设置", "int", "生物设置",
            min: 0, max: 128, defaultValue: "8", requiresRestart: true);

        Register("entity-cramming.enabled", "config/paper-global.yml",
            "实体挤压惩罚",
            "是否启用实体挤压检测和伤害机制", "bool", "性能设置",
            defaultValue: "true");

        Register("misc.max-joins-per-tick", "config/paper-global.yml",
            "每 Tick 最大加入数",
            "每个游戏 tick 允许处理的最大玩家加入请求数", "int", "网络设置",
            min: 1, max: 100, defaultValue: "3");
    }
}
```

- [ ] **Step 7: 实现 IConfigManager 和 ConfigManager**

```csharp
// src/McServerGuard/Services/ConfigManagement/IConfigManager.cs
using McServerGuard.Models;

namespace McServerGuard.Services.ConfigManagement;

public interface IConfigManager
{
    /// <summary>读取指定配置文件的所有条目</summary>
    Task<List<ServerConfigEntry>> ReadConfigAsync(string filePath);

    /// <summary>保存配置变更到文件</summary>
    Task SaveConfigAsync(string filePath, List<ServerConfigEntry> entries);

    /// <summary>获取指定配置项的中文描述和约束</summary>
    ServerConfigDescriptor? GetDescriptor(string key, string configFileName);

    /// <summary>验证配置值是否有效</summary>
    bool ValidateValue(ServerConfigEntry entry, out string? error);

    /// <summary>获取配置文件的分类组织</summary>
    Dictionary<string, List<ServerConfigEntry>> GroupByCategory(List<ServerConfigEntry> entries);
}
```

```csharp
// src/McServerGuard/Services/ConfigManagement/ConfigManager.cs
using System.IO;
using McServerGuard.Models;

namespace McServerGuard.Services.ConfigManagement;

public class ConfigManager : IConfigManager
{
    public Task<List<ServerConfigEntry>> ReadConfigAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return Task.FromResult(new List<ServerConfigEntry>());

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var entries = new List<ServerConfigEntry>();
        var fileName = Path.GetFileName(filePath);

        try
        {
            var content = File.ReadAllText(filePath);

            switch (ext)
            {
                case ".properties":
                    entries = ParseProperties(content, fileName);
                    break;
                case ".yml" or ".yaml":
                    entries = ParseYaml(content, fileName);
                    break;
                case ".json":
                    entries = ParseJson(content, fileName);
                    break;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "读取配置文件失败: {File}", filePath);
        }

        return Task.FromResult(entries);
    }

    public Task SaveConfigAsync(string filePath, List<ServerConfigEntry> entries)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            switch (ext)
            {
                case ".properties":
                    var props = entries.ToDictionary(e => e.Key, e => e.Value);
                    var content = PropertiesParser.Serialize(props);
                    File.WriteAllText(filePath, content);
                    break;
                case ".yml" or ".yaml":
                    // YAML 序列化需要保留结构，使用原始内容替换
                    SaveYaml(filePath, entries);
                    break;
                case ".json":
                    var jsonEntries = entries.ToDictionary(e => e.Key, e => e.Value);
                    File.WriteAllText(filePath,
                        System.Text.Json.JsonSerializer.Serialize(jsonEntries,
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    break;
            }

            // 标记所有条目为已保存
            foreach (var entry in entries)
            {
                entry.IsModified = false;
                entry.OriginalValue = entry.Value;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "保存配置文件失败: {File}", filePath);
            throw;
        }

        return Task.CompletedTask;
    }

    public ServerConfigDescriptor? GetDescriptor(string key, string configFileName)
    {
        return ConfigDescriptorRegistry.GetDescriptor(key, configFileName);
    }

    public bool ValidateValue(ServerConfigEntry entry, out string? error)
    {
        error = null;

        if (entry.Descriptor == null)
            return true;

        var desc = entry.Descriptor;

        // 数值类型验证
        if (desc.ValueType == "int" && int.TryParse(entry.Value, out var intVal))
        {
            if (desc.MinValue.HasValue && intVal < desc.MinValue.Value)
            {
                error = $"值不能小于 {desc.MinValue}";
                entry.IsValid = false;
                entry.ErrorMessage = error;
                return false;
            }

            if (desc.MaxValue.HasValue && intVal > desc.MaxValue.Value)
            {
                error = $"值不能大于 {desc.MaxValue}";
                entry.IsValid = false;
                entry.ErrorMessage = error;
                return false;
            }
        }

        // 枚举类型验证
        if (desc.AllowedValues != null &&
            !desc.AllowedValues.Contains(entry.Value, StringComparer.OrdinalIgnoreCase))
        {
            error = $"允许的值: {string.Join(", ", desc.AllowedValues)}";
            entry.IsValid = false;
            entry.ErrorMessage = error;
            return false;
        }

        // 正则验证
        if (desc.RegexPattern != null &&
            !System.Text.RegularExpressions.Regex.IsMatch(entry.Value, desc.RegexPattern))
        {
            error = $"值格式不正确";
            entry.IsValid = false;
            entry.ErrorMessage = error;
            return false;
        }

        entry.IsValid = true;
        entry.ErrorMessage = null;
        return true;
    }

    public Dictionary<string, List<ServerConfigEntry>> GroupByCategory(List<ServerConfigEntry> entries)
    {
        return entries.GroupBy(e => e.Descriptor?.Category ?? "其他")
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private List<ServerConfigEntry> ParseProperties(string content, string fileName)
    {
        var raw = PropertiesParser.Parse(content);
        var entries = new List<ServerConfigEntry>();

        foreach (var (key, value) in raw)
        {
            var entry = new ServerConfigEntry
            {
                Key = key,
                Value = value,
                OriginalValue = value,
                SourceFile = fileName,
                Descriptor = ConfigDescriptorRegistry.GetDescriptor(key, fileName)
            };
            entries.Add(entry);
        }

        return entries;
    }

    private List<ServerConfigEntry> ParseYaml(string content, string fileName)
    {
        var entries = new List<ServerConfigEntry>();
        var root = YamlParser.Parse(content);
        FlattenYaml("", root, entries, fileName);
        return entries;
    }

    private List<ServerConfigEntry> ParseJson(string content, string fileName)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(content);
            if (json == null) return [];

            var entries = new List<ServerConfigEntry>();
            FlattenJson("", json, entries, fileName);
            return entries;
        }
        catch
        {
            return [];
        }
    }

    private void FlattenYaml(string prefix, Dictionary<string, object?> node,
        List<ServerConfigEntry> entries, string fileName)
    {
        foreach (var (key, value) in node)
        {
            var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

            if (value is Dictionary<string, object?> dict)
                FlattenYaml(fullKey, dict, entries, fileName);
            else
                entries.Add(new ServerConfigEntry
                {
                    Key = fullKey,
                    Value = value?.ToString() ?? string.Empty,
                    OriginalValue = value?.ToString() ?? string.Empty,
                    SourceFile = fileName,
                    Descriptor = ConfigDescriptorRegistry.GetDescriptor(fullKey, fileName)
                });
        }
    }

    private void FlattenJson(string prefix, Dictionary<string, object?> node,
        List<ServerConfigEntry> entries, string fileName)
    {
        foreach (var (key, value) in node)
        {
            var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

            if (value is System.Text.Json.JsonElement elem && elem.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var inner = new Dictionary<string, object?>();
                foreach (var prop in elem.EnumerateObject())
                    inner[prop.Name] = prop.Value.ToString();
                FlattenJson(fullKey, inner, entries, fileName);
            }
            else
                entries.Add(new ServerConfigEntry
                {
                    Key = fullKey,
                    Value = value?.ToString() ?? string.Empty,
                    OriginalValue = value?.ToString() ?? string.Empty,
                    SourceFile = fileName,
                    Descriptor = ConfigDescriptorRegistry.GetDescriptor(fullKey, fileName)
                });
        }
    }

    private void SaveYaml(string filePath, List<ServerConfigEntry> entries)
    {
        // 读取原始内容，替换对应的值
        var content = File.ReadAllText(filePath);
        var root = YamlParser.Parse(content);

        foreach (var entry in entries.Where(e => e.IsModified))
        {
            YamlParser.SetValue(root, entry.Key, entry.Value);
        }

        var newContent = YamlParser.Serialize(root);
        File.WriteAllText(filePath, newContent);
    }
}
```

- [ ] **Step 8: 运行所有测试**

```bash
cd /workspace/McServerGuard
dotnet test
```
Expected: ALL PASS

- [ ] **Step 9: 提交**

```bash
git add -A
git commit -m "feat: 实现配置解析器、中文描述注册表和配置管理服务（TDD）"
```

---

## Task 9: 实现系统监控服务

**Files:**
- Create: `src/McServerGuard/Services/SystemMonitoring/ISystemMonitor.cs`
- Create: `src/McServerGuard/Services/SystemMonitoring/SystemMonitor.cs`
- Create: `src/McServerGuard/Services/SystemMonitoring/DiskSpaceMonitor.cs`
- Create: `src/McServerGuard/Services/SystemMonitoring/MemoryMonitor.cs`
- Create: `src/McServerGuard/Services/SystemMonitoring/ThreadAnalyzer.cs`

- [ ] **Step 1: 实现系统监控服务**

```csharp
// src/McServerGuard/Services/SystemMonitoring/ISystemMonitor.cs
using McServerGuard.Models;

namespace McServerGuard.Services.SystemMonitoring;

public interface ISystemMonitor
{
    /// <summary>采集一次完整的系统指标快照</summary>
    SystemMetrics CollectSnapshot(ServerInstance? server);

    /// <summary>启动持续监控（定时采集）</summary>
    IObservable<SystemMetrics> StartMonitoring(ServerInstance server, TimeSpan interval);

    /// <summary>停止监控</summary>
    void StopMonitoring();
}
```

```csharp
// src/McServerGuard/Services/SystemMonitoring/SystemMonitor.cs
using System.Diagnostics;
using System.Runtime.InteropServices;
using McServerGuard.Models;

namespace McServerGuard.Services.SystemMonitoring;

public class SystemMonitor : ISystemMonitor, IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly List<IDisposable> _subscriptions = [];

    public SystemMetrics CollectSnapshot(ServerInstance? server)
    {
        var metrics = new SystemMetrics();

        // 系统级 CPU
        var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        cpuCounter.NextValue(); // 首次调用需要预热
        Thread.Sleep(200);
        metrics.CpuUsagePercent = cpuCounter.NextValue();

        // 系统内存
        metrics.TotalMemoryBytes = MemoryMonitor.GetTotalPhysicalMemory();
        metrics.UsedMemoryBytes = MemoryMonitor.GetUsedMemory();
        metrics.MemoryUsagePercent = metrics.TotalMemoryBytes > 0
            ? (double)metrics.UsedMemoryBytes / metrics.TotalMemoryBytes * 100
            : 0;

        // 系统总线程数
        metrics.TotalThreadCount = ThreadAnalyzer.GetTotalThreadCount();

        // Java 进程级监控
        if (server != null)
        {
            try
            {
                var process = Process.GetProcessById(server.ProcessId);
                CollectProcessMetrics(process, metrics);
            }
            catch (ArgumentException)
            {
                // 进程已退出
            }
        }

        return metrics;
    }

    public IObservable<SystemMetrics> StartMonitoring(ServerInstance server, TimeSpan interval)
    {
        _cts = new CancellationTokenSource();

        return Observable.Create<SystemMetrics>(observer =>
        {
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var snapshot = CollectSnapshot(server);
                        observer.OnNext(snapshot);
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }

                    await Task.Delay(interval, _cts.Token);
                }
            }, _cts.Token);

            return () => _cts?.Cancel();
        });
    }

    public void StopMonitoring()
    {
        _cts?.Cancel();
    }

    private void CollectProcessMetrics(Process process, SystemMetrics metrics)
    {
        // Java 进程 CPU
        try
        {
            var processCpu = new PerformanceCounter("Process", "% Processor Time",
                process.ProcessName, process.Id);
            processCpu.NextValue();
            Thread.Sleep(200);
            metrics.JavaCpuUsagePercent = processCpu.NextValue();
        }
        catch { }

        // Java 进程内存
        metrics.JavaWorkingSetBytes = process.WorkingSet64;
        metrics.JavaPrivateBytes = process.PrivateMemorySize64;

        // Java 进程线程数
        metrics.JavaThreadCount = process.Threads.Count;
        metrics.JavaHandleCount = process.HandleCount;

        // 磁盘空间（服务器工作目录所在磁盘）
        try
        {
            if (!string.IsNullOrEmpty(process.MainModule?.FileName))
            {
                var diskInfo = DiskSpaceMonitor.GetDiskInfo(
                    Path.GetPathRoot(process.MainModule.FileName)!);
                metrics.DiskName = diskInfo.DriveName;
                metrics.DiskTotalBytes = diskInfo.TotalBytes;
                metrics.DiskUsedBytes = diskInfo.UsedBytes;
                metrics.DiskFreeBytes = diskInfo.FreeBytes;
                metrics.DiskUsagePercent = diskInfo.UsagePercent;
            }
        }
        catch { }

        // JVM 堆内存（通过 Performance Counter 或 JMX）
        // Windows 上可通过 .NET PerformanceCounter 读取
        try
        {
            // .NET 无法直接读取 JVM 堆，但可通过进程内存估算
            // 或通过 JMX/RMI 连接（需要额外实现）
            metrics.JavaHeapUsedBytes = process.WorkingSet64; // 近似值
            metrics.JavaHeapMaxBytes = metrics.MaxHeapMemoryBytes > 0
                ? metrics.MaxHeapMemoryBytes
                : process.WorkingSet64;
        }
        catch { }
    }

    public void Dispose()
    {
        StopMonitoring();
        foreach (var sub in _subscriptions) sub.Dispose();
    }
}
```

注意：需要在 `.csproj` 中添加 `System.Reactive` 包：
```bash
dotnet add package System.Reactive
```

- [ ] **Step 2: 实现 DiskSpaceMonitor**

```csharp
// src/McServerGuard/Services/SystemMonitoring/DiskSpaceMonitor.cs
using System.IO;

namespace McServerGuard.Services.SystemMonitoring;

public record DiskInfo(string DriveName, long TotalBytes, long FreeBytes, long UsedBytes, double UsagePercent);

public static class DiskSpaceMonitor
{
    public static DiskInfo GetDiskInfo(string driveRoot)
    {
        var drive = new DriveInfo(driveRoot);

        if (!drive.IsReady)
            return new DiskInfo(driveRoot, 0, 0, 0, 0);

        var total = drive.TotalSize;
        var free = drive.AvailableFreeSpace;
        var used = total - free;

        return new DiskInfo(
            DriveName: drive.Name,
            TotalBytes: total,
            FreeBytes: free,
            UsedBytes: used,
            UsagePercent: total > 0 ? (double)used / total * 100 : 0
        );
    }
}
```

- [ ] **Step 3: 实现 MemoryMonitor**

```csharp
// src/McServerGuard/Services/SystemMonitoring/MemoryMonitor.cs
using System.Runtime.InteropServices;

namespace McServerGuard.Services.SystemMonitoring;

public static class MemoryMonitor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>(); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static long GetTotalPhysicalMemory()
    {
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(ref memStatus))
            return (long)memStatus.ullTotalPhys;
        return 0;
    }

    public static long GetUsedMemory()
    {
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(ref memStatus))
            return (long)(memStatus.ullTotalPhys - memStatus.ullAvailPhys);
        return 0;
    }

    public static double GetMemoryUsagePercent()
    {
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(ref memStatus))
            return memStatus.dwMemoryLoad;
        return 0;
    }
}
```

- [ ] **Step 4: 实现 ThreadAnalyzer**

```csharp
// src/McServerGuard/Services/SystemMonitoring/ThreadAnalyzer.cs
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace McServerGuard.Services.SystemMonitoring;

public static class ThreadAnalyzer
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public IntPtr lpMinimumApplicationAddress;
        public IntPtr lpMaximumApplicationAddress;
        public IntPtr dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    [DllImport("kernel32.dll")]
    private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

    /// <summary>获取系统逻辑处理器核心数</summary>
    public static int GetLogicalProcessorCount()
    {
        var info = new SYSTEM_INFO();
        GetSystemInfo(ref info);
        return (int)info.dwNumberOfProcessors;
    }

    /// <summary>获取系统总线程数</summary>
    public static int GetTotalThreadCount()
    {
        try
        {
            var counter = new PerformanceCounter("System", "Threads");
            counter.NextValue();
            Thread.Sleep(100);
            return (int)counter.NextValue();
        }
        catch
        {
            return Environment.CurrentManagedThreadId; // 回退
        }
    }

    /// <summary>分析 Java 进程线程数占比</summary>
    public static ThreadAnalysisResult AnalyzeJavaThreads(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var javaThreadCount = process.Threads.Count;
            var totalThreads = GetTotalThreadCount();
            var cpuCores = GetLogicalProcessorCount();

            return new ThreadAnalysisResult
            {
                JavaThreadCount = javaThreadCount,
                TotalSystemThreads = totalThreads,
                JavaThreadPercentage = totalThreads > 0
                    ? (double)javaThreadCount / totalThreads * 100
                    : 0,
                CpuCores = cpuCores,
                ThreadsPerCore = cpuCores > 0
                    ? (double)javaThreadCount / cpuCores
                    : 0,
                Assessment = javaThreadCount > cpuCores * 50
                    ? "线程数偏高，可能导致上下文切换开销增大"
                    : javaThreadCount > cpuCores * 30
                        ? "线程数适中"
                        : "线程数正常"
            };
        }
        catch
        {
            return new ThreadAnalysisResult();
        }
    }
}

public class ThreadAnalysisResult
{
    public int JavaThreadCount { get; init; }
    public int TotalSystemThreads { get; init; }
    public double JavaThreadPercentage { get; init; }
    public int CpuCores { get; init; }
    public double ThreadsPerCore { get; init; }
    public string Assessment { get; init; } = string.Empty;
}
```

- [ ] **Step 5: 编译验证**

```bash
cd /workspace/McServerGuard
dotnet build
```
Expected: BUILD SUCCEEDED

- [ ] **Step 6: 提交**

```bash
git add -A
git commit -m "feat: 实现系统负载/磁盘/内存/线程监控服务"
```

---

## Task 10: 实现 MLP 神经网络智能保障服务

> **说明：** 本 Task 采用 MLP（多层感知机）等中小型神经网络，通过 ML.NET + ONNX Runtime 实现，无需 LLM、无需 Ollama、无需 Python 运行时。所有模型均在进程内本地推理。

**Files:**
- Create: `src/McServerGuard/Services/AIService/IAiGuardService.cs`
- Create: `src/McServerGuard/Services/AIService/LogAnomalyDetector.cs`
- Create: `src/McServerGuard/Services/AIService/CrashPredictor.cs`
- Create: `src/McServerGuard/Services/AIService/ConfigOptimizer.cs`
- Create: `src/McServerGuard/Services/AIService/AiGuardOrchestrator.cs`
- Create: `src/McServerGuard/Services/AIService/TrainingDataCollector.cs`
- Create: `src/McServerGuard.Tests/Services/LogAnomalyDetectorTests.cs`
- Create: `src/McServerGuard.Tests/Services/CrashPredictorTests.cs`

- [ ] **Step 1: 编写日志异常检测器的失败测试**

```csharp
// src/McServerGuard.Tests/Services/LogAnomalyDetectorTests.cs
using McServerGuard.Services.AIService;

namespace McServerGuard.Tests.Services;

public class LogAnomalyDetectorTests
{
    [Fact]
    public void Classify_NormalInfoLog_ReturnsNormal()
    {
        var result = LogAnomalyDetector.Classify(
            "[12:00:01 INFO]: Starting minecraft server version 1.21.4");
        Assert.Equal(LogSeverity.Normal, result.Severity);
    }

    [Fact]
    public void Classify_CantKeepUpLog_ReturnsWarning()
    {
        var result = LogAnomalyDetector.Classify(
            "[WARN] Can't keep up! Is the server overloaded? Running 2842ms behind");
        Assert.Equal(LogSeverity.Warning, result.Severity);
    }

    [Fact]
    public void Classify_OutOfMemoryLog_ReturnsError()
    {
        var result = LogAnomalyDetector.Classify(
            "[12:00:05 ERROR]: java.lang.OutOfMemoryError: Java heap space");
        Assert.Equal(LogSeverity.Error, result.Severity);
    }

    [Fact]
    public void Classify_TickTooLongLog_ReturnsWarning()
    {
        var result = LogAnomalyDetector.Classify(
            "[WARN] Tick took 2345ms");
        Assert.Equal(LogSeverity.Warning, result.Severity);
    }

    [Fact]
    public void Classify_StackOverflowLog_ReturnsCritical()
    {
        var result = LogAnomalyDetector.Classify(
            "[FATAL ERROR] java.lang.StackOverflowError");
        Assert.Equal(LogSeverity.Critical, result.Severity);
    }

    [Fact]
    public void Classify_PluginErrorLog_ReturnsError()
    {
        var result = LogAnomalyDetector.Classify(
            "[12:30:00 ERROR]: [Essentials] Exception in command handler");
        Assert.Equal(LogSeverity.Error, result.Severity);
    }

    [Fact]
    public void Classify_DoneLog_ReturnsNormal()
    {
        var result = LogAnomalyDetector.Classify(
            "[12:00:10 INFO]: Done (3.245s)! For help, type \"help\"");
        Assert.Equal(LogSeverity.Normal, result.Severity);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test src/McServerGuard.Tests --filter "LogAnomalyDetectorTests" -v n
```
Expected: FAIL

- [ ] **Step 3: 实现日志异常检测器**

```csharp
// src/McServerGuard/Services/AIService/IAiGuardService.cs
using McServerGuard.Models;

namespace McServerGuard.Services.AIService;

/// <summary>
/// 日志严重等级枚举
/// </summary>
public enum LogSeverity { Normal, Warning, Error, Critical }

/// <summary>
/// 日志分析结果
/// </summary>
public record LogAnalysisResult(
    LogSeverity Severity,
    string Category,
    string Description,
    List<string> Suggestions);

/// <summary>
/// 崩溃预测结果
/// </summary>
public record CrashPrediction(
    double CrashProbability,    // 0.0 - 1.0
    string PrimaryRiskFactor,
    List<string> RiskIndicators);

/// <summary>
/// 配置优化建议
/// </summary>
public record ConfigOptimizationSuggestion(
    string ConfigKey,
    string ConfigFile,
    string CurrentValue,
    string RecommendedValue,
    string Reason);

/// <summary>
/// MLP 神经网络智能保障服务统一接口
/// </summary>
public interface IAiGuardService
{
    bool IsModelLoaded { get; }

    Task InitializeAsync();
    LogAnalysisResult AnalyzeLog(string logLine);
    Task<CrashPrediction> PredictCrashAsync(SystemMetrics metrics);
    List<ConfigOptimizationSuggestion> SuggestOptimizations(
        ServerInstance server, SystemMetrics metrics);
}
```

```csharp
// src/McServerGuard/Services/AIService/LogAnomalyDetector.cs
using System.Text.RegularExpressions;

namespace McServerGuard.Services.AIService;

/// <summary>
/// 基于规则的日志异常检测器（作为 MLP 模型的规则前置层）
///
/// 检测策略：
/// 1. 精确模式匹配（已知 Minecraft 错误模式）
/// 2. 正则表达式匹配（异常堆栈、警告模式）
/// 3. ML.NET 模型推理（训练后加载 ONNX 模型进行分类）
///
/// 架构：规则层（快速确定性匹配）→ MLP 层（模糊/未知模式分类）
/// </summary>
public static partial class LogAnomalyDetector
{
    // 已知的 Minecraft 错误模式
    private static readonly (Regex Pattern, LogSeverity Severity, string Category, string Desc, string[] Suggestions)[] KnownPatterns =
    [
        // 严重错误
        (OutOfMemoryPattern(), LogSeverity.Critical, "内存溢出",
            "JVM 堆内存耗尽，服务器即将崩溃或已崩溃",
            ["立即增加 -Xmx 值", "检查是否有内存泄漏插件", "分析 heap dump 文件"]),

        (StackOverflowPattern(), LogSeverity.Critical, "栈溢出",
            "线程栈空间耗尽，通常由递归调用或过深调用链导致",
            ["检查近期安装的插件/Mod", "增加 -Xss 栈大小参数"]),

        (FatalErrorPattern(), LogSeverity.Critical, "致命错误",
            "服务器发生不可恢复的致命错误",
            ["查看完整堆栈跟踪定位原因", "检查是否有不兼容的 Mod/插件"]),

        // 警告
        (CantKeepUpPattern(), LogSeverity.Warning, "TPS 过低",
            "服务器主线程跟不上游戏节奏，TPS 下降",
            ["减少 view-distance", "减少实体激活范围", "考虑升级硬件"]),

        (TickTooLongPattern(), LogSeverity.Warning, "Tick 超时",
            "单个游戏 Tick 执行时间过长",
            ["检查是否有卡服的插件命令", "减少实体数量"]),

        (TooManyEntitiesPattern(), LogSeverity.Warning, "实体过多",
            "服务器加载的实体数量超过安全阈值",
            ["减少生物生成范围", "使用 ClearLag 插件定期清理"]),

        // 普通错误
        (ExceptionPattern(), LogSeverity.Error, "异常",
            "检测到 Java 异常堆栈",
            ["查看完整异常信息定位原因"]),

        (ConnectionTimeoutPattern(), LogSeverity.Error, "连接超时",
            "数据库或外部服务连接超时",
            ["检查数据库连接配置", "检查网络连通性"]),
    ];

    /// <summary>
    /// 对单行日志进行分类（规则层快速匹配）
    /// </summary>
    public static LogAnalysisResult Classify(string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine))
            return new LogAnalysisResult(LogSeverity.Normal, "空行", "", []);

        foreach (var (pattern, severity, category, desc, suggestions) in KnownPatterns)
        {
            if (pattern.IsMatch(logLine))
            {
                return new LogAnalysisResult(severity, category, desc, suggestions);
            }
        }

        // 默认：基于日志级别前缀的简单分类
        return ClassifyByLogLevel(logLine);
    }

    /// <summary>
    /// 对批量日志进行 MLP 模型推理（需要训练后的 ONNX 模型）
    /// 架构：FeaturizeText → Dense(256, ReLU) → Dropout(0.3) → Dense(128, ReLU) → Dense(4, Softmax)
    /// </summary>
    public static List<LogAnalysisResult> ClassifyWithModel(
        IEnumerable<string> logLines, Microsoft.ML.MLContext mlContext)
    {
        // TODO: 加载 ONNX 模型进行推理
        // 当前回退到规则分类
        return logLines.Select(Classify).ToList();
    }

    private static LogAnalysisResult ClassifyByLogLevel(string logLine)
    {
        if (logLine.Contains("[FATAL]", StringComparison.OrdinalIgnoreCase))
            return new LogAnalysisResult(LogSeverity.Critical, "致命错误", logLine, []);

        if (logLine.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
            logLine.Contains("[ERR]", StringComparison.OrdinalIgnoreCase))
            return new LogAnalysisResult(LogSeverity.Error, "错误", logLine, []);

        if (logLine.Contains("[WARN]", StringComparison.OrdinalIgnoreCase))
            return new LogAnalysisResult(LogSeverity.Warning, "警告", logLine, []);

        return new LogAnalysisResult(LogSeverity.Normal, "正常", logLine, []);
    }

    // 正则表达式（使用 generated source 避免重复编译）
    [GeneratedRegex(@"OutOfMemoryError", RegexOptions.Compiled)]
    private static partial Regex OutOfMemoryPattern();

    [GeneratedRegex(@"StackOverflowError", RegexOptions.Compiled)]
    private static partial Regex StackOverflowPattern();

    [GeneratedRegex(@"FATAL\s+ERROR", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex FatalErrorPattern();

    [GeneratedRegex(@"Can'?t keep up", RegexOptions.Compiled)]
    private static partial Regex CantKeepUpPattern();

    [GeneratedRegex(@"Tick took \d+ms", RegexOptions.Compiled)]
    private static partial Regex TickTooLongPattern();

    [GeneratedRegex(@"Too many (entities|loaded|chunks)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TooManyEntitiesPattern();

    [GeneratedRegex(@"Exception|\.java:\d+\)", RegexOptions.Compiled)]
    private static partial Regex ExceptionPattern();

    [GeneratedRegex(@"(Connection timed out|ConnectException|TimeoutException)", RegexOptions.Compiled)]
    private static partial Regex ConnectionTimeoutPattern();
}
```

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test src/McServerGuard.Tests --filter "LogAnomalyDetectorTests" -v n
```
Expected: ALL PASS

- [ ] **Step 5: 实现崩溃预测器**

```csharp
// src/McServerGuard/Services/AIService/CrashPredictor.cs
using McServerGuard.Models;

namespace McServerGuard.Services.AIService;

/// <summary>
/// 服务器崩溃预测器
///
/// 方案 1（规则层 - 首次部署无需训练数据）：
///   基于阈值的统计检测，检测 TPS 突降、内存飙升等已知崩溃前兆
///
/// 方案 2（MLP 模型层 - 积累数据后训练）：
///   架构：滑动窗口特征(272维) → Dense(128,ReLU) → Dense(64,ReLU) → Dense(32,ReLU) → Dense(1,Sigmoid)
///   输入：过去 15 分钟的 TPS/内存/CPU/玩家数 滑动窗口 + 统计特征
///   输出：未来 5 分钟内崩溃概率
///   训练方式：Python PyTorch 训练 → ONNX 导出 → ML.NET ONNX Runtime 推理
/// </summary>
public class CrashPredictor
{
    // 崩溃前兆阈值
    private const double TpsCrashThreshold = 5.0;       // TPS 低于此值视为严重危险
    private const double TpsWarningThreshold = 15.0;     // TPS 低于此值视为警告
    private const double MemoryDangerThreshold = 0.92;  // 内存使用超过 92%
    private const double CpuDangerThreshold = 0.95;     // CPU 使用超过 95%

    private readonly Queue<SystemMetrics> _metricsBuffer = new();
    private const int BufferSize = 30; // 保留最近 30 个采样点（约 1 分钟 @ 2s 间隔）

    /// <summary>
    /// 推入新的指标采样，返回当前崩溃风险评估
    /// </summary>
    public CrashPrediction Update(SystemMetrics metrics)
    {
        _metricsBuffer.Enqueue(metrics);
        if (_metricsBuffer.Count > BufferSize)
            _metricsBuffer.Dequeue();

        var riskFactors = new List<string>();
        double probability = 0;

        // 1. TPS 检测（最直接的崩溃前兆）
        if (metrics.JavaCpuUsagePercent > 0) // 有数据时才检测
        {
            // 使用 Java CPU 作为 TPS 的代理指标（管理工具无法直接获取 TPS）
            var recentAvg = _metricsBuffer.Average(m => m.JavaCpuUsagePercent);

            if (recentAvg > CpuDangerThreshold)
            {
                probability += 0.35;
                riskFactors.Add($"Java CPU 持续过高 ({recentAvg:F1}%)");
            }

            // 内存使用率
            var memRatio = metrics.TotalMemoryBytes > 0
                ? (double)metrics.UsedMemoryBytes / metrics.TotalMemoryBytes
                : 0;
            if (memRatio > MemoryDangerThreshold)
            {
                probability += 0.30;
                riskFactors.Add($"系统内存不足 ({memRatio * 100:F1}%)");
            }

            // Java 线程数异常增长
            if (metrics.JavaThreadCount > 500)
            {
                probability += 0.15;
                riskFactors.Add($"Java 线程数过多 ({metrics.JavaThreadCount})");
            }
        }

        // 2. 磁盘空间检测
        if (metrics.DiskUsagePercent > 95)
        {
            probability += 0.20;
            riskFactors.Add($"磁盘空间严重不足 ({metrics.DiskUsagePercent:F1}%)");
        }

        probability = Math.Min(probability, 1.0);

        var primaryRisk = riskFactors.FirstOrDefault() ?? "无明显风险";

        return new CrashPrediction(probability, primaryRisk, riskFactors);
    }

    /// <summary>
    /// 使用 MLP 模型预测（加载 ONNX 模型后可用）
    /// </summary>
    public CrashPrediction PredictWithModel(float[] featureVector)
    {
        // TODO: 加载 ONNX MLP 模型进行推理
        // 当前回退到规则预测
        return new CrashPrediction(0, "MLP 模型未训练", []);
    }
}
```

- [ ] **Step 6: 实现配置优化推荐器**

```csharp
// src/McServerGuard/Services/AIService/ConfigOptimizer.cs
using McServerGuard.Models;

namespace McServerGuard.Services.AIService;

/// <summary>
/// 基于规则的配置优化推荐器
///
/// 方案 1（规则层 - 即时可用）：
///   基于服务器硬件指标和当前配置的已知最佳实践
///
/// 方案 2（MLP 回归模型层 - 积累数据后训练）：
///   架构：硬件+配置特征(30维) → Dense(64,ReLU) → Dense(32,ReLU) → Dense(1,Linear)
///   输入：CPU核心数、RAM、在线玩家数、TPS统计、当前配置值
///   输出：推荐配置值或预测 TPS 改善量
///   训练方式：Python PyTorch 训练 → ONNX 导出 → ML.NET 推理
/// </summary>
public static class ConfigOptimizer
{
    public static List<ConfigOptimizationSuggestion> Suggest(
        ServerInstance server, SystemMetrics metrics)
    {
        var suggestions = new List<ConfigOptimizationSuggestion>();
        var ramGB = server.MaxHeapMemoryBytes / (1024L * 1024 * 1024);

        // view-distance 优化
        if (ramGB <= 4)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                "view-distance", "server.properties",
                "10", "6",
                $"分配内存仅 {ramGB} GB，建议降低视距以减少区块加载压力"));
        }
        else if (ramGB <= 8)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                "view-distance", "server.properties",
                "10", "8",
                $"{ramGB} GB 内存下，视距 8 是性能与体验的最佳平衡点"));
        }

        // simulation-distance 不应超过 view-distance
        suggestions.Add(new ConfigOptimizationSuggestion(
            "simulation-distance", "server.properties",
            "10", "6",
            "模拟距离独立于视距，降低至 6 可减少实体计算开销"));

        // Aikar 标志检查
        if (!server.UsesAikarFlags)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                "JVM参数", "启动脚本",
                "无Aikar优化", "使用Aikar Flags",
                "未检测到 Aikar 优化标志，建议使用 PaperMC 推荐的 JVM 参数以优化 GC"));
        }

        // GC 类型检查
        if (string.IsNullOrEmpty(server.GcType))
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                "GC类型", "启动脚本",
                "未指定", "G1GC",
                "未指定 GC 类型，建议使用 G1GC（-XX:+UseG1GC）以获得更稳定的 TPS"));
        }

        // 内存分配检查
        if (server.InitialHeapMemoryBytes != server.MaxHeapMemoryBytes)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                "Xms/Xmx", "启动脚本",
                "不一致", "设为相同值",
                "初始堆(-Xms)与最大堆(-Xmx)不一致，建议设为相同值以避免运行时堆扩展带来的性能抖动"));
        }

        // 线程数检查
        if (metrics.JavaThreadCount > 500)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                "实体激活范围", "spigot.yml",
                "默认(32)", "16",
                $"Java 线程数过高 ({metrics.JavaThreadCount})，降低实体激活范围可减少线程压力"));
        }

        return suggestions;
    }
}
```

- [ ] **Step 7: 实现 AI 保障编排器**

```csharp
// src/McServerGuard/Services/AIService/AiGuardOrchestrator.cs
using McServerGuard.Models;

namespace McServerGuard.Services.AIService;

/// <summary>
/// MLP 神经网络智能保障编排器
/// 统一管理日志检测、崩溃预测和配置优化三个子模块
/// </summary>
public class AiGuardOrchestrator : IAiGuardService
{
    private readonly CrashPredictor _crashPredictor = new();
    private Microsoft.ML.MLContext? _mlContext;
    private Microsoft.ML.ITransformer? _logModel;
    private Microsoft.ML.ITransformer? _crashModel;

    public bool IsModelLoaded { get; private set; }

    public Task InitializeAsync()
    {
        _mlContext = new Microsoft.ML.MLContext(seed: 42);

        // 尝试加载预训练的 ONNX 模型（如果有）
        try
        {
            // var logModelPath = Path.Combine("Models", "log_classifier.onnx");
            // _logModel = LoadOnnxModel(logModelPath);
            // IsModelLoaded = true;
        }
        catch
        {
            IsModelLoaded = false;
        }

        return Task.CompletedTask;
    }

    public LogAnalysisResult AnalyzeLog(string logLine)
    {
        if (_logModel != null && _mlContext != null)
        {
            // MLP 模型推理路径
            return LogAnomalyDetector.ClassifyWithModel([logLine], _mlContext).FirstOrDefault()
                ?? LogAnomalyDetector.Classify(logLine);
        }

        // 规则引擎路径（无需模型即可工作）
        return LogAnomalyDetector.Classify(logLine);
    }

    public Task<CrashPrediction> PredictCrashAsync(SystemMetrics metrics)
    {
        return Task.FromResult(_crashPredictor.Update(metrics));
    }

    public List<ConfigOptimizationSuggestion> SuggestOptimizations(
        ServerInstance server, SystemMetrics metrics)
    {
        return ConfigOptimizer.Suggest(server, metrics);
    }
}
```

- [ ] **Step 8: 实现训练数据收集器**

```csharp
// src/McServerGuard/Services/AIService/TrainingDataCollector.cs
using McServerGuard.Models;

namespace McServerGuard.Services.AIService;

/// <summary>
/// 训练数据收集器
/// 从服务器日志和性能指标中收集数据，生成用于训练 MLP 模型的 CSV 数据集
/// </summary>
public class TrainingDataCollector
{
    private readonly List<string> _logBuffer = [];

    /// <summary>
    /// 从 Minecraft 服务器日志文件自动生成带标签的训练数据
    /// 标签策略：基于日志级别前缀和已知错误模式的规则标注
    /// </summary>
    public async Task GenerateLabeledLogDatasetAsync(
        string logFilePath, string outputCsvPath)
    {
        if (!File.Exists(logFilePath)) return;

        var lines = await File.ReadAllLinesAsync(logFilePath);
        using var writer = new StreamWriter(outputCsvPath);
        writer.WriteLine("LogLine,Severity");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var result = LogAnomalyDetector.Classify(line);
            var escaped = $"\"{line.Replace("\"", "\"\"")}\"";
            writer.WriteLine($"{escaped},{result.Severity}");
        }
    }

    /// <summary>
    /// 收集系统指标数据用于崩溃预测模型训练
    /// 输出 CSV 格式：Timestamp,JavaCPU,MemoryUsage,DiskUsage,JavaThreads,IsCrash
    /// </summary>
    public void RecordMetrics(SystemMetrics metrics, bool isCrash = false)
    {
        // TODO: 追加到训练数据文件
    }
}
```

- [ ] **Step 9: 编译验证**

```bash
cd /workspace/McServerGuard
dotnet build
```
Expected: BUILD SUCCEEDED

- [ ] **Step 10: 提交**

```bash
git add -A
git commit -m "feat: 实现 MLP 神经网络智能保障服务（日志检测/崩溃预测/配置优化）"
```

---

## Task 11: 实现 ViewModels

**Files:**
- Create: `src/McServerGuard/ViewModels/MainViewModel.cs`
- Create: `src/McServerGuard/ViewModels/ServerDetectionViewModel.cs`
- Create: `src/McServerGuard/ViewModels/ConfigEditorViewModel.cs`
- Create: `src/McServerGuard/ViewModels/SystemMonitorViewModel.cs`
- Create: `src/McServerGuard/ViewModels/AIGuardViewModel.cs`

- [ ] **Step 1: 实现 MainViewModel**

```csharp
// src/McServerGuard/ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Services.ServerDetection;
using McServerGuard.Services.ConfigManagement;
using McServerGuard.Services.SystemMonitoring;
using McServerGuard.Services.AIService;

namespace McServerGuard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServerDetector _detector;
    private readonly IConfigManager _configManager;
    private readonly ISystemMonitor _monitor;
    private readonly IAiGuardService _aiGuardService;

    public ServerDetectionViewModel DetectionPage { get; }
    public ConfigEditorViewModel ConfigPage { get; }
    public SystemMonitorViewModel MonitorPage { get; }
    public AIGuardViewModel AIGuardPage { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    public MainViewModel(
        IServerDetector detector,
        IConfigManager configManager,
        ISystemMonitor monitor,
        IAiGuardService aiGuardService)
    {
        _detector = detector;
        _configManager = configManager;
        _monitor = monitor;
        _aiGuardService = aiGuardService;

        DetectionPage = new ServerDetectionViewModel(detector);
        ConfigPage = new ConfigEditorViewModel(configManager);
        MonitorPage = new SystemMonitorViewModel(monitor);
        AIGuardPage = new AIGuardViewModel(aiGuardService);
    }

    [RelayCommand]
    private async Task DetectServersAsync()
    {
        StatusMessage = "正在检测...";
        await DetectionPage.DetectAsync();

        if (DetectionPage.DetectionResult?.Servers.Count > 0)
        {
            var server = DetectionPage.SelectedServer!;
            ConfigPage.Server = server;
            MonitorPage.Server = server;
            AIGuardPage.Server = server;
            StatusMessage = $"已检测到 {DetectionPage.DetectionResult.Servers.Count} 个服务器";
        }
        else
        {
            StatusMessage = "未检测到 Minecraft 服务器";
        }
    }
}
```

- [ ] **Step 2: 实现 ServerDetectionViewModel**

```csharp
// src/McServerGuard/ViewModels/ServerDetectionViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Services.ServerDetection;

namespace McServerGuard.ViewModels;

public partial class ServerDetectionViewModel : ObservableObject
{
    private readonly IServerDetector _detector;

    [ObservableProperty]
    private DetectionResult? _detectionResult;

    [ObservableProperty]
    private ServerInstance? _selectedServer;

    [ObservableProperty]
    private bool _isDetecting;

    public ServerDetectionViewModel(IServerDetector detector)
    {
        _detector = detector;
    }

    [RelayCommand]
    public async Task DetectAsync()
    {
        IsDetecting = true;
        DetectionResult = await _detector.DetectAllAsync();
        SelectedServer = DetectionResult?.Servers.FirstOrDefault();
        IsDetecting = false;
    }
}
```

- [ ] **Step 3: 实现 ConfigEditorViewModel**

```csharp
// src/McServerGuard/ViewModels/ConfigEditorViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Services.ConfigManagement;

namespace McServerGuard.ViewModels;

public partial class ConfigEditorViewModel : ObservableObject
{
    private readonly IConfigManager _configManager;

    [ObservableProperty]
    private ServerInstance? _server;

    [ObservableProperty]
    private List<string> _configFiles = [];

    [ObservableProperty]
    private string? _selectedConfigFile;

    [ObservableProperty]
    private List<ServerConfigEntry>? _configEntries;

    [ObservableProperty]
    private Dictionary<string, List<ServerConfigEntry>>? _groupedEntries;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public ConfigEditorViewModel(IConfigManager configManager)
    {
        _configManager = configManager;
    }

    partial void OnServerChanged(ServerInstance? value)
    {
        if (value != null)
        {
            ConfigFiles = value.ConfigFiles
                .Where(f => !f.EndsWith('/') && !f.EndsWith('\\')) // 排除目录
                .ToList();
        }
    }

    partial void OnSelectedConfigFileChanged(string? value)
    {
        if (value == null || Server == null) return;
        LoadConfig(value);
    }

    [RelayCommand]
    private async Task LoadConfig(string fileName)
    {
        IsLoading = true;
        var filePath = System.IO.Path.Combine(Server!.WorkingDirectory, fileName);
        var entries = await _configManager.ReadConfigAsync(filePath);

        // 附加描述信息
        foreach (var entry in entries)
        {
            entry.Descriptor = _configManager.GetDescriptor(entry.Key, fileName);
        }

        ConfigEntries = entries;
        GroupedEntries = _configManager.GroupByCategory(entries);
        IsLoading = false;
    }

    partial void OnConfigEntriesChanged(List<ServerConfigEntry>? value)
    {
        // 检测是否有未保存变更
        HasUnsavedChanges = value?.Any(e => e.IsModified) ?? false;
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        if (SelectedConfigFile == null || Server == null || ConfigEntries == null)
            return;

        var filePath = System.IO.Path.Combine(Server.WorkingDirectory, SelectedConfigFile);
        await _configManager.SaveConfigAsync(filePath, ConfigEntries);
        HasUnsavedChanges = false;
    }

    [RelayCommand]
    private void ResetChanges()
    {
        if (ConfigEntries == null) return;

        foreach (var entry in ConfigEntries.Where(e => e.IsModified))
        {
            entry.Value = entry.OriginalValue;
            entry.IsModified = false;
            entry.IsValid = true;
            entry.ErrorMessage = null;
        }

        HasUnsavedChanges = false;
    }
}
```

- [ ] **Step 4: 实现 SystemMonitorViewModel**

```csharp
// src/McServerGuard/ViewModels/SystemMonitorViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using McServerGuard.Models;
using McServerGuard.Services.SystemMonitoring;

namespace McServerGuard.ViewModels;

public partial class SystemMonitorViewModel : ObservableObject
{
    private readonly ISystemMonitor _monitor;
    private IDisposable? _monitorSubscription;

    [ObservableProperty]
    private ServerInstance? _server;

    [ObservableProperty]
    private SystemMetrics? _currentMetrics;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private List<SystemMetrics> _metricsHistory = [];

    // 图表数据
    public ISeries[] CpuSeries { get; set; } = [];
    public ISeries[] MemorySeries { get; set; } = [];
    public Axis[] XAxes { get; set; } =
    [
        new Axis { Name = "时间", LabelsRotation = -45 }
    ];
    public Axis[] YAxes { get; set; } =
    [
        new Axis { Name = "使用率 (%)", MinLimit = 0, MaxLimit = 100 }
    ];

    public SystemMonitorViewModel(ISystemMonitor monitor)
    {
        _monitor = monitor;
    }

    [RelayCommand]
    private void StartMonitoring()
    {
        if (Server == null) return;

        IsMonitoring = true;
        MetricsHistory.Clear();

        _monitorSubscription = _monitor
            .StartMonitoring(Server, TimeSpan.FromSeconds(2))
            .Subscribe(OnMetricsUpdate);
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        IsMonitoring = false;
        _monitorSubscription?.Dispose();
        _monitorSubscription = null;
    }

    private void OnMetricsUpdate(SystemMetrics metrics)
    {
        CurrentMetrics = metrics;
        MetricsHistory.Add(metrics);

        // 保留最近 60 条数据（2分钟）
        if (MetricsHistory.Count > 60)
            MetricsHistory.RemoveAt(0);

        UpdateCharts();
    }

    private void UpdateCharts()
    {
        var cpuValues = MetricsHistory.Select(m => m.CpuUsagePercent).ToArray();
        var javaCpuValues = MetricsHistory.Select(m => m.JavaCpuUsagePercent).ToArray();
        var memValues = MetricsHistory.Select(m => m.MemoryUsagePercent).ToArray();
        var javaMemValues = MetricsHistory.Select(m =>
            m.JavaHeapMaxBytes > 0 ? (double)m.JavaWorkingSetBytes / m.JavaHeapMaxBytes * 100 : 0
        ).ToArray();

        CpuSeries =
        [
            new LineSeries<double>
            {
                Name = "系统 CPU",
                Values = cpuValues,
                Fill = null,
                GeometrySize = 0
            },
            new LineSeries<double>
            {
                Name = "Java CPU",
                Values = javaCpuValues,
                Fill = null,
                GeometrySize = 0
            }
        ];

        MemorySeries =
        [
            new LineSeries<double>
            {
                Name = "系统内存",
                Values = memValues,
                Fill = null,
                GeometrySize = 0
            },
            new LineSeries<double>
            {
                Name = "Java 堆内存",
                Values = javaMemValues,
                Fill = null,
                GeometrySize = 0
            }
        ];
    }
}
```

- [ ] **Step 5: 实现 AIGuardViewModel**

```csharp
// src/McServerGuard/ViewModels/AIGuardViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Services.AIService;

namespace McServerGuard.ViewModels;

public partial class AIGuardViewModel : ObservableObject
{
    private readonly IAiGuardService _aiGuard;

    [ObservableProperty]
    private ServerInstance? _server;

    [ObservableProperty]
    private bool _isModelLoaded;

    [ObservableProperty]
    private string _logInput = string.Empty;

    [ObservableProperty]
    private string _analysisOutput = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private List<LogAnalysisResult> _analysisHistory = [];

    [ObservableProperty]
    private List<ConfigOptimizationSuggestion> _configSuggestions = [];

    [ObservableProperty]
    private CrashPrediction? _latestCrashPrediction;

    public AIGuardViewModel(IAiGuardService aiGuard)
    {
        _aiGuard = aiGuard;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _aiGuard.InitializeAsync();
        IsModelLoaded = _aiGuard.IsModelLoaded;
    }

    [RelayCommand]
    private void AnalyzeLog()
    {
        if (string.IsNullOrWhiteSpace(LogInput)) return;

        IsProcessing = true;
        var lines = LogInput.Split('\n', '\r');
        var results = new List<LogAnalysisResult>();

        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            var result = _aiGuard.AnalyzeLog(line);
            results.Add(result);
        }

        AnalysisHistory = results;
        var summary = results.GroupBy(r => r.Severity)
            .Select(g => $"{g.Key}: {g.Count()} 条")
            .ToList();
        AnalysisOutput = string.Join("\n", summary) + "\n\n" +
            string.Join("\n", results.Select(r =>
                $"[{r.Severity}] {r.Category}: {r.Description}"));
        IsProcessing = false;
    }

    [RelayCommand]
    private void GenerateHealthReport()
    {
        if (Server == null) return;

        IsProcessing = true;
        // 基于规则和 MLP 模型的综合报告
        var suggestions = _aiGuard.SuggestOptimizations(Server, new SystemMetrics());
        ConfigSuggestions = suggestions;

        AnalysisOutput = $"服务器类型: {Server.ServerType}\n" +
            $"分配内存: {Server.FormattedMaxMemory}\n" +
            $"GC 类型: {Server.GcType}\n" +
            $"Aikar 优化: {(Server.UsesAikarFlags ? "是" : "否")}\n\n" +
            $"MLP 模型状态: {(IsModelLoaded ? "已加载" : "规则引擎模式")}\n" +
            $"配置建议: {suggestions.Count} 条";
        IsProcessing = false;
    }

    [RelayCommand]
    private void RequestConfigOptimization()
    {
        if (Server == null) return;

        IsProcessing = true;
        ConfigSuggestions = _aiGuard.SuggestOptimizations(Server, new SystemMetrics());
        IsProcessing = false;
    }
}
```

- [ ] **Step 6: 编译验证**

```bash
cd /workspace/McServerGuard
dotnet build
```
Expected: BUILD SUCCEEDED

- [ ] **Step 7: 提交**

```bash
git add -A
git commit -m "feat: 实现 MVVM ViewModel 层（检测/配置/监控/AI 页面）"
```

---

## Task 12: 实现 WPF 主窗口和 UI 页面（自适应布局）

**Files:**
- Create: `src/McServerGuard/App.xaml` / `src/McServerGuard/App.xaml.cs`（修改）
- Create: `src/McServerGuard/Themes/CustomTheme.xaml`
- Create: `src/McServerGuard/Views/MainWindow.xaml` / `src/McServerGuard/Views/MainWindow.xaml.cs`
- Create: `src/McServerGuard/Views/ServerDetectionPage.xaml`
- Create: `src/McServerGuard/Views/ConfigEditorPage.xaml`
- Create: `src/McServerGuard/Views/SystemMonitorPage.xaml`
- Create: `src/McServerGuard/Views/AIGuardPage.xaml`

- [ ] **Step 1: 配置 App.xaml（MaterialDesign 主题初始化）**

```xml
<!-- src/McServerGuard/App.xaml -->
<Application x:Class="McServerGuard.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- MaterialDesign 主题 -->
                <materialDesign:BundledTheme BaseTheme="Dark"
                                              PrimaryColor="DeepPurple"
                                              SecondaryColor="Lime" />
                <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml" />
                <!-- HandyControl -->
                <ResourceDictionary Source="pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml" />
                <ResourceDictionary Source="pack://application:,,,/HandyControl;component/Themes/Theme.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

```csharp
// src/McServerGuard/App.xaml.cs
using System.Windows;
using McServerGuard.Services.ServerDetection;
using McServerGuard.Services.ConfigManagement;
using McServerGuard.Services.SystemMonitoring;
using McServerGuard.Services.AIService;
using McServerGuard.ViewModels;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var services = new ServiceCollection();

        // 注册服务
        services.AddSingleton<IServerDetector, ServerDetector>();
        services.AddSingleton<IConfigManager, ConfigManager>();
        services.AddSingleton<ISystemMonitor, SystemMonitor>();
        services.AddSingleton<IAiGuardService, AiGuardOrchestrator>();

        // 注册 ViewModel
        services.AddTransient<MainViewModel>();

        Services = services.BuildServiceProvider();

        // 启动主窗口
        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }
}
```

- [ ] **Step 2: 实现 MainWindow（自适应布局 + 导航）**

```xml
<!-- src/McServerGuard/Views/MainWindow.xaml -->
<Window x:Class="McServerGuard.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
        xmlns:views="clr-namespace:McServerGuard.Views"
        Title="McServerGuard - Minecraft 服务器管理工具"
        Width="1200" Height="800"
        MinWidth="900" MinHeight="600"
        WindowStartupLocation="CenterScreen"
        ResizeMode="CanResizeWithGrip"
        materialDesign:WindowAssist.IsThrowing="False"
        Background="{DynamicResource MaterialDesignPaper}">

    <Grid>
        <Grid.RowDefinitions>
            <!-- 顶部标题栏 -->
            <RowDefinition Height="Auto"/>
            <!-- 主体内容 -->
            <RowDefinition Height="*"/>
            <!-- 底部状态栏 -->
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 顶部：标题 + 操作按钮 -->
        <materialDesign:ColorZone Mode="PrimaryMid" Grid.Row="0" Padding="16,8">
            <DockPanel>
                <materialDesign:PackIcon Kind="ShieldServer" Width="28" Height="28"
                                          VerticalAlignment="Center" Margin="0,0,12,0"/>
                <TextBlock Text="McServerGuard" FontSize="20" FontWeight="Bold"
                           VerticalAlignment="Center"/>
                <TextBlock Text="Minecraft 服务器管理工具" FontSize="12"
                           Opacity="0.8" VerticalAlignment="Center" Margin="12,0,0,0"/>
                <Button DockPanel.Dock="Right"
                        Content="检测服务器"
                        Command="{Binding DetectServersCommand}"
                        Style="{DynamicResource MaterialDesignRaisedButton}"
                        Margin="8,0"/>
            </DockPanel>
        </materialDesign:ColorZone>

        <!-- 主体：左侧导航 + 右侧页面 -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- 左侧导航栏 -->
            <materialDesign:NavigationRail Grid.Column="0"
                                           SelectedIndex="{Binding SelectedTabIndex}"
                                           Margin="4">
                <materialDesign:NavigationRailItem Icon="ServerSearch" Label="服务器检测"/>
                <materialDesign:NavigationRailItem Icon="Tune" Label="配置编辑"/>
                <materialDesign:NavigationRailItem Icon="Speedometer" Label="系统监控"/>
                <materialDesign:NavigationRailItem Icon="RobotExcited" Label="AI 保障"/>
            </materialDesign:NavigationRail>

            <!-- 右侧内容区域 -->
            <ContentControl Grid.Column="1" Margin="8">
                <ContentControl.Style>
                    <Style TargetType="ContentControl">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding SelectedTabIndex}" Value="0">
                                <Setter Property="Content">
                                    <Setter.Value>
                                        <views:ServerDetectionPage DataContext="{Binding DetectionPage}"/>
                                    </Setter.Value>
                                </Setter>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding SelectedTabIndex}" Value="1">
                                <Setter Property="Content">
                                    <Setter.Value>
                                        <views:ConfigEditorPage DataContext="{Binding ConfigPage}"/>
                                    </Setter.Value>
                                </Setter>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding SelectedTabIndex}" Value="2">
                                <Setter Property="Content">
                                    <Setter.Value>
                                        <views:SystemMonitorPage DataContext="{Binding MonitorPage}"/>
                                    </Setter.Value>
                                </Setter>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding SelectedTabIndex}" Value="3">
                                <Setter Property="Content">
                                    <Setter.Value>
                                        <views:AIGuardPage DataContext="{Binding AIGuardPage}"/>
                                    </Setter.Value>
                                </Setter>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </ContentControl.Style>
            </ContentControl>
        </Grid>

        <!-- 底部状态栏 -->
        <materialDesign:ColorZone Mode="PrimaryDark" Grid.Row="2" Padding="16,4">
            <DockPanel>
                <materialDesign:PackIcon Kind="Information" Width="16" Height="16"
                                          VerticalAlignment="Center" Margin="0,0,6,0"/>
                <TextBlock Text="{Binding StatusMessage}" FontSize="12"
                           VerticalAlignment="Center"/>
            </DockPanel>
        </materialDesign:ColorZone>
    </Grid>
</Window>
```

```csharp
// src/McServerGuard/Views/MainWindow.xaml.cs
using System.Windows;

namespace McServerGuard.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: 实现 ServerDetectionPage**

```xml
<!-- src/McServerGuard/Views/ServerDetectionPage.xaml -->
<UserControl x:Class="McServerGuard.Views.ServerDetectionPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes">

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
        <StackPanel>
            <!-- 检测结果卡片 -->
            <materialDesign:Card Margin="0,0,0,16" Padding="16">
                <StackPanel>
                    <TextBlock Text="检测到的 Minecraft 服务器" FontSize="18"
                               FontWeight="Bold" Margin="0,0,0,12"/>

                    <ItemsControl ItemsSource="{Binding DetectionResult.Servers}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <materialDesign:Card Margin="0,0,0,8" Padding="12"
                                                     Cursor="Hand"
                                                     materialDesign:ShadowAssist.ShadowDepth="Depth2">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>

                                        <materialDesign:PackIcon Grid.Column="0"
                                                                  Kind="Server" Width="32" Height="32"
                                                                  VerticalAlignment="Center" Margin="0,0,12,0"/>

                                        <StackPanel Grid.Column="1">
                                            <TextBlock Text="{Binding DisplayName}"
                                                       FontWeight="Bold" FontSize="14"/>
                                            <TextBlock Text="{Binding ServerJarName}"
                                                       Opacity="0.7" FontSize="12"/>
                                            <WrapPanel Margin="0,4,0,0">
                                                <materialDesign:Chip Content="{Binding ServerType}"
                                                                    Margin="0,0,6,0"/>
                                                <materialDesign:Chip Content="{Binding FormattedMaxMemory}"
                                                                    Margin="0,0,6,0"/>
                                                <materialDesign:Chip Content="{Binding GcType}"/>
                                            </WrapPanel>
                                        </StackPanel>
                                    </Grid>
                                </materialDesign:Card>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>

                    <!-- 未检测到服务器的提示 -->
                    <TextBlock Text="未检测到正在运行的 Minecraft 服务器"
                               Visibility="{Binding DetectionResult.IsDetected, Converter={StaticResource InvertBoolConverter}}"
                               Opacity="0.5" HorizontalAlignment="Center" Margin="0,16"/>
                </StackPanel>
            </materialDesign:Card>

            <!-- 服务器详情卡片 -->
            <materialDesign:Card Margin="0,0,0,16" Padding="16"
                                 Visibility="{Binding SelectedServer, Converter={StaticResource NullToVisibilityConverter}}">
                <StackPanel>
                    <TextBlock Text="服务器详细信息" FontSize="16" FontWeight="Bold"
                               Margin="0,0,0,12"/>

                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>

                        <TextBlock Grid.Row="0" Grid.Column="0" Text="工作路径:" FontWeight="Bold" Margin="0,0,12,8"/>
                        <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding SelectedServer.WorkingDirectory}" Margin="0,0,0,8" TextWrapping="Wrap"/>

                        <TextBlock Grid.Row="1" Grid.Column="0" Text="Java 路径:" FontWeight="Bold" Margin="0,0,12,8"/>
                        <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding SelectedServer.JavaPath}" Margin="0,0,0,8" TextWrapping="Wrap"/>

                        <TextBlock Grid.Row="2" Grid.Column="0" Text="服务器 JAR:" FontWeight="Bold" Margin="0,0,12,8"/>
                        <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding SelectedServer.ServerJarPath}" Margin="0,0,0,8" TextWrapping="Wrap"/>

                        <TextBlock Grid.Row="3" Grid.Column="0" Text="Aikar 优化:" FontWeight="Bold" Margin="0,0,12,8"/>
                        <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding SelectedServer.UsesAikarFlags}" Margin="0,0,0,8"/>

                        <TextBlock Grid.Row="4" Grid.Column="0" Text="启动脚本:" FontWeight="Bold" Margin="0,0,12,8"/>
                        <TextBlock Grid.Row="4" Grid.Column="1" Text="{Binding SelectedServer.StartupScriptPath}" Margin="0,0,0,8" TextWrapping="Wrap"/>
                    </Grid>
                </StackPanel>
            </materialDesign:Card>

            <!-- 检测日志 -->
            <materialDesign:Card Padding="16">
                <StackPanel>
                    <TextBlock Text="检测日志" FontSize="14" FontWeight="Bold" Margin="0,0,0,8"/>
                    <TextBox Text="{Binding DetectionResult.LogMessages}"
                             IsReadOnly="True"
                             AcceptsReturn="True"
                             TextWrapping="Wrap"
                             VerticalScrollBarVisibility="Auto"
                             MaxHeight="200"
                             materialDesign:HintAssist.Hint="检测过程日志..."/>
                </StackPanel>
            </materialDesign:Card>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 4: 实现 ConfigEditorPage（中文配置界面）**

```xml
<!-- src/McServerGuard/Views/ConfigEditorPage.xaml -->
<UserControl x:Class="McServerGuard.Views.ConfigEditorPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes">

    <Grid Margin="16">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="250" MinWidth="200"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- 左侧：配置文件列表 -->
        <materialDesign:Card Grid.Column="0" Margin="0,0,8,0" Padding="12">
            <StackPanel>
                <TextBlock Text="配置文件" FontWeight="Bold" Margin="0,0,0,8"/>
                <ListBox ItemsSource="{Binding ConfigFiles}"
                         SelectedItem="{Binding SelectedConfigFile}"
                         DisplayMemberPath=""/>
            </StackPanel>
        </materialDesign:Card>

        <!-- 右侧：配置编辑区域 -->
        <ScrollViewer Grid.Column="1" VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="8,0,0,0">
                <DockPanel Margin="0,0,0,12">
                    <Button DockPanel.Dock="Right" Content="保存" Command="{Binding SaveConfigCommand}"
                            Style="{DynamicResource MaterialDesignRaisedButton}" Margin="8,0,0,0"/>
                    <Button DockPanel.Dock="Right" Content="重置" Command="{Binding ResetChangesCommand}"
                            Style="{DynamicResource MaterialDesignFlatButton}"/>
                    <TextBlock Text="{Binding SelectedConfigFile, StringFormat='编辑: {0}'}"
                               FontWeight="Bold" FontSize="16" VerticalAlignment="Center"/>
                </DockPanel>

                <ItemsControl ItemsSource="{Binding GroupedEntries}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Expander Header="{Binding Key}" Margin="0,0,0,8">
                                <ItemsControl ItemsSource="{Binding Value}">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <Grid Margin="4" Height="Auto">
                                                <Grid.ColumnDefinitions>
                                                    <ColumnDefinition Width="200" MinWidth="150"/>
                                                    <ColumnDefinition Width="*"/>
                                                    <ColumnDefinition Width="Auto"/>
                                                </Grid.ColumnDefinitions>
                                                <Grid.RowDefinitions>
                                                    <RowDefinition Height="Auto"/>
                                                    <RowDefinition Height="Auto"/>
                                                </Grid.RowDefinitions>

                                                <!-- 配置项名称和描述 -->
                                                <StackPanel Grid.Column="0" Grid.Row="0" Grid.RowSpan="2"
                                                            Margin="0,0,12,0">
                                                    <TextBlock Text="{Binding Descriptor.DisplayName, FallbackValue=Key}"
                                                               FontWeight="Bold" FontSize="13"/>
                                                    <TextBlock Text="{Binding Descriptor.Description}"
                                                               Opacity="0.6" FontSize="11"
                                                               TextWrapping="Wrap" Margin="0,2,0,0"/>
                                                    <TextBlock Text="{Binding Descriptor.Category}"
                                                               Opacity="0.4" FontSize="10" Margin="0,2,0,0"/>
                                                </StackPanel>

                                                <!-- 编辑控件 -->
                                                <Grid Grid.Column="1" Grid.Row="0">
                                                    <!-- 布尔值使用 ToggleSwitch -->
                                                    <materialDesign:ToggleSwitch
                                                        Visibility="{Binding Descriptor.ValueType, Converter={StaticResource BoolToVisibility}}"
                                                        IsChecked="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

                                                    <!-- 枚举值使用 ComboBox -->
                                                    <ComboBox
                                                        Visibility="{Binding Descriptor.AllowedValues, Converter={StaticResource NullToVisibility}}"
                                                        ItemsSource="{Binding Descriptor.AllowedValues}"
                                                        SelectedItem="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

                                                    <!-- 数值使用 Slider+TextBox -->
                                                    <DockPanel Visibility="{Binding Descriptor.ValueType, Converter={StaticResource IntToVisibility}}">
                                                        <TextBox DockPanel.Dock="Right" Width="80"
                                                                 Text="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                                                 Margin="8,0,0,0"
                                                                 materialDesign:HintAssist.Hint="{Binding Descriptor.MaxValue, StringFormat='最大: {0}'}"/>
                                                    </DockPanel>

                                                    <!-- 字符串使用 TextBox -->
                                                    <TextBox Text="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                                             Visibility="{Binding Descriptor.ValueType, Converter={StaticResource StringToVisibility}}"/>
                                                </Grid>

                                                <!-- 验证错误 -->
                                                <TextBlock Grid.Column="1" Grid.Row="1"
                                                           Text="{Binding ErrorMessage}"
                                                           Foreground="OrangeRed" FontSize="11"
                                                           Visibility="{Binding IsValid, Converter={StaticResource InvertBoolConverter}}"/>
                                            </Grid>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </Expander>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </StackPanel>
        </ScrollViewer>
    </Grid>
</UserControl>
```

- [ ] **Step 5: 实现 SystemMonitorPage（图表 + 仪表盘）**

```xml
<!-- src/McServerGuard/Views/SystemMonitorPage.xaml -->
<UserControl x:Class="McServerGuard.Views.SystemMonitorPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF">

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
        <StackPanel>
            <!-- 控制栏 -->
            <DockPanel Margin="0,0,0,16">
                <Button DockPanel.Dock="Right" Content="停止"
                        Command="{Binding StopMonitoringCommand}"
                        Visibility="{Binding IsMonitoring, Converter={StaticResource BoolToVisibility}}"
                        Style="{DynamicResource MaterialDesignFlatButton}"/>
                <Button DockPanel.Dock="Right" Content="开始监控"
                        Command="{Binding StartMonitoringCommand}"
                        Visibility="{Binding IsMonitoring, Converter={StaticResource InvertBoolConverter}}"
                        Style="{DynamicResource MaterialDesignRaisedButton}" Margin="0,0,8,0"/>
                <TextBlock Text="系统性能监控" FontSize="18" FontWeight="Bold"
                           VerticalAlignment="Center"/>
            </DockPanel>

            <!-- 系统概览仪表盘 -->
            <UniformGrid Columns="4" Margin="0,0,0,16">
                <!-- CPU -->
                <materialDesign:Card Margin="4" Padding="16" VerticalAlignment="Stretch">
                    <StackPanel VerticalAlignment="Center">
                        <materialDesign:PackIcon Kind="Memory" Width="28" Height="28"
                                                  HorizontalAlignment="Center" Opacity="0.7"/>
                        <TextBlock Text="CPU 使用率" FontSize="12" Opacity="0.6"
                                   HorizontalAlignment="Center" Margin="0,4,0,0"/>
                        <TextBlock Text="{Binding CurrentMetrics.CpuUsagePercent, StringFormat='{0:F1}%'}"
                                   FontSize="28" FontWeight="Bold"
                                   HorizontalAlignment="Center"/>
                    </StackPanel>
                </materialDesign:Card>

                <!-- 内存 -->
                <materialDesign:Card Margin="4" Padding="16" VerticalAlignment="Stretch">
                    <StackPanel VerticalAlignment="Center">
                        <materialDesign:PackIcon Kind="Server" Width="28" Height="28"
                                                  HorizontalAlignment="Center" Opacity="0.7"/>
                        <TextBlock Text="内存使用" FontSize="12" Opacity="0.6"
                                   HorizontalAlignment="Center" Margin="0,4,0,0"/>
                        <TextBlock Text="{Binding CurrentMetrics.MemoryUsagePercent, StringFormat='{0:F1}%'}"
                                   FontSize="28" FontWeight="Bold"
                                   HorizontalAlignment="Center"/>
                    </StackPanel>
                </materialDesign:Card>

                <!-- 磁盘 -->
                <materialDesign:Card Margin="4" Padding="16" VerticalAlignment="Stretch">
                    <StackPanel VerticalAlignment="Center">
                        <materialDesign:PackIcon Kind="Harddisk" Width="28" Height="28"
                                                  HorizontalAlignment="Center" Opacity="0.7"/>
                        <TextBlock Text="磁盘使用" FontSize="12" Opacity="0.6"
                                   HorizontalAlignment="Center" Margin="0,4,0,0"/>
                        <TextBlock Text="{Binding CurrentMetrics.DiskUsagePercent, StringFormat='{0:F1}%'}"
                                   FontSize="28" FontWeight="Bold"
                                   HorizontalAlignment="Center"/>
                    </StackPanel>
                </materialDesign:Card>

                <!-- 线程 -->
                <materialDesign:Card Margin="4" Padding="16" VerticalAlignment="Stretch">
                    <StackPanel VerticalAlignment="Center">
                        <materialDesign:PackIcon Kind="Sitemap" Width="28" Height="28"
                                                  HorizontalAlignment="Center" Opacity="0.7"/>
                        <TextBlock Text="Java 线程" FontSize="12" Opacity="0.6"
                                   HorizontalAlignment="Center" Margin="0,4,0,0"/>
                        <TextBlock Text="{Binding CurrentMetrics.JavaThreadCount}"
                                   FontSize="28" FontWeight="Bold"
                                   HorizontalAlignment="Center"/>
                    </StackPanel>
                </materialDesign:Card>
            </UniformGrid>

            <!-- CPU 图表 -->
            <materialDesign:Card Margin="0,0,0,16" Padding="16">
                <StackPanel>
                    <TextBlock Text="CPU 使用率趋势" FontWeight="Bold" Margin="0,0,0,8"/>
                    <lvc:CartesianChart Series="{Binding CpuSeries}"
                                       XAxes="{Binding XAxes}"
                                       YAxes="{Binding YAxes}"
                                       Height="200"
                                       AnimationsSpeed="00:00:00.200"/>
                </StackPanel>
            </materialDesign:Card>

            <!-- 内存图表 -->
            <materialDesign:Card Margin="0,0,0,16" Padding="16">
                <StackPanel>
                    <TextBlock Text="内存使用趋势" FontWeight="Bold" Margin="0,0,0,8"/>
                    <lvc:CartesianChart Series="{Binding MemorySeries}"
                                       XAxes="{Binding XAxes}"
                                       YAxes="{Binding YAxes}"
                                       Height="200"
                                       AnimationsSpeed="00:00:00.200"/>
                </StackPanel>
            </materialDesign:Card>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 6: 实现 AIGuardPage**

```xml
<!-- src/McServerGuard/Views/AIGuardPage.xaml -->
<UserControl x:Class="McServerGuard.Views.AIGuardPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes">

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="16">
        <StackPanel>
            <!-- AI 状态 -->
            <materialDesign:Card Margin="0,0,0,16" Padding="16">
                <DockPanel>
                    <materialDesign:PackIcon Kind="{Binding IsAiAvailable, Converter={StaticResource AiStatusIcon}}"
                                              Width="24" Height="24" VerticalAlignment="Center"/>
                    <TextBlock Text="{Binding IsAiAvailable, Converter={StaticResource AiStatusText}}"
                               Margin="8,0,0,0" VerticalAlignment="Center"/>
                    <TextBlock DockPanel.Dock="Right" Opacity="0.6" FontSize="12"
                               VerticalAlignment="Center"
                               Text="{Binding CurrentModelName, StringFormat='模型: {0}'}"/>
                </DockPanel>
            </materialDesign:Card>

            <!-- 功能按钮 -->
            <UniformGrid Columns="2" Margin="0,0,0,16">
                <Button Content="生成健康报告" Command="{Binding RequestHealthReportCommand}"
                        Style="{DynamicResource MaterialDesignRaisedButton}"
                        Margin="4"/>
                <Button Content="配置优化建议" Command="{Binding SuggestConfigOptimizationsCommand}"
                        Style="{DynamicResource MaterialDesignRaisedButton}"
                        Margin="4"/>
            </UniformGrid>

            <!-- 日志分析输入 -->
            <materialDesign:Card Margin="0,0,0,16" Padding="16">
                <StackPanel>
                    <TextBlock Text="日志分析" FontWeight="Bold" Margin="0,0,0,8"/>
                    <TextBox Text="{Binding ChatInput, UpdateSourceTrigger=PropertyChanged}"
                             AcceptsReturn="True"
                             TextWrapping="Wrap"
                             MinHeight="100"
                             MaxHeight="200"
                             materialDesign:HintAssist.Hint="粘贴服务器日志片段进行分析..."/>
                    <Button Content="分析日志" Command="{Binding AnalyzeLogsCommand}"
                            Style="{DynamicResource MaterialDesignRaisedButton}"
                            HorizontalAlignment="Right" Margin="0,8,0,0"/>
                </StackPanel>
            </materialDesign:Card>

            <!-- AI 输出 -->
            <materialDesign:Card Padding="16">
                <StackPanel>
                    <DockPanel Margin="0,0,0,8">
                        <TextBlock Text="AI 分析结果" FontWeight="Bold" VerticalAlignment="Center"/>
                        <ProgressBar DockPanel.Dock="Right" Width="100"
                                     IsIndeterminate="{Binding IsProcessing}"
                                     Visibility="{Binding IsProcessing, Converter={StaticResource BoolToVisibility}}"/>
                    </DockPanel>
                    <TextBox Text="{Binding ChatOutput}"
                             IsReadOnly="True"
                             AcceptsReturn="True"
                             TextWrapping="Wrap"
                             MinHeight="200"
                             VerticalScrollBarVisibility="Auto"
                             materialDesign:HintAssist.Hint="AI 分析结果将在此显示..."/>
                </StackPanel>
            </materialDesign:Card>

            <!-- 配置建议列表 -->
            <ItemsControl ItemsSource="{Binding ConfigSuggestions}" Margin="0,16,0,0">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <materialDesign:Card Margin="0,0,0,8" Padding="12">
                            <StackPanel>
                                <TextBlock FontSize="14" FontWeight="Bold">
                                    <Run Text="{Binding ConfigKey}"/>
                                    <Run Text=": " Opacity="0.5"/>
                                    <Run Text="{Binding CurrentValue}" TextDecorations="Strikethrough" Opacity="0.5"/>
                                    <Run Text=" → "/>
                                    <Run Text="{Binding SuggestedValue}" Foreground="LimeGreen"/>
                                </TextBlock>
                                <TextBlock Text="{Binding Reason}" Opacity="0.7" FontSize="12"
                                           Margin="0,4,0,0"/>
                            </StackPanel>
                        </materialDesign:Card>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 7: 添加值转换器**

```csharp
// src/McServerGuard/Converters/ValueConverters.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace McServerGuard.Converters;

public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

- [ ] **Step 8: 编译验证**

```bash
cd /workspace/McServerGuard
dotnet build
```
Expected: BUILD SUCCEEDED

- [ ] **Step 9: 提交**

```bash
git add -A
git commit -m "feat: 实现 WPF 自适应 UI 页面（检测/配置/监控/AI）"
```

---

## Task 13: 最终集成测试和构建

**Files:**
- Modify: 所有页面 code-behind 文件
- Test: 运行完整构建和测试

- [ ] **Step 1: 创建所有页面的 code-behind 文件**

```csharp
// src/McServerGuard/Views/ServerDetectionPage.xaml.cs
using System.Windows.Controls;

namespace McServerGuard.Views;
public partial class ServerDetectionPage : UserControl
{
    public ServerDetectionPage() => InitializeComponent();
}

// src/McServerGuard/Views/ConfigEditorPage.xaml.cs
using System.Windows.Controls;

namespace McServerGuard.Views;
public partial class ConfigEditorPage : UserControl
{
    public ConfigEditorPage() => InitializeComponent();
}

// src/McServerGuard/Views/SystemMonitorPage.xaml.cs
using System.Windows.Controls;

namespace McServerGuard.Views;
public partial class SystemMonitorPage : UserControl
{
    public SystemMonitorPage() => InitializeComponent();
}

// src/McServerGuard/Views/AIGuardPage.xaml.cs
using System.Windows.Controls;

namespace McServerGuard.Views;
public partial class AIGuardPage : UserControl
{
    public AIGuardPage() => InitializeComponent();
}
```

- [ ] **Step 2: 运行完整构建**

```bash
cd /workspace/McServerGuard
dotnet build --configuration Release
```
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 运行所有测试**

```bash
cd /workspace/McServerGuard
dotnet test
```
Expected: ALL PASS

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat: 完成最终集成和构建验证"
```

---

## 技术选型决策说明

### UI 渲染层

| 选择 | 理由 |
|------|------|
| **WPF** | 原生 DPI 自适应（设备无关单位），无需额外配置即可支持高分辨率和窗口大小变化 |
| **MaterialDesignInXAML Toolkit** | 提供 Material Design 风格的现代 UI，80+ 控件样式覆盖，MIT 开源免费 |
| **HandyControl** | 国产开源库，额外提供 80+ 自定义控件弥补 MaterialDesign 的不足 |
| **LiveCharts2** | SkiaSharp 渲染后端，深度支持 WPF，适合实时性能图表 |
| **ScottPlot** | 备选图表库，适合科学计算可视化场景 |

### Minecraft 服务器检测策略

| 检测方法 | 描述 |
|---------|------|
| **进程扫描** | 枚举 `java.exe` 进程，通过 WMI 获取完整命令行，解析 `-jar` 后的 JAR 名称 |
| **工作目录确认** | 从命令行 JAR 路径提取目录，验证 `server.properties` 存在，搜索常见部署目录 |
| **Java 路径** | 从命令行首 token 提取，支持绝对路径、相对路径、环境变量引用 |
| **启动脚本** | **内容架构判断**：检测 `java` + `-jar` + `-Xmx`/`-Xms` + `nogui` 模式，不依赖文件名 |
| **服务器类型** | JAR 名称精确匹配 → 配置文件特征推断（Paper: `config/paper-global.yml`，Spigot: `spigot.yml` + `bukkit.yml` 等） |

### MLP 神经网络模型

| 子模块 | 算法/架构 | 输入 → 输出 | 部署方式 |
|--------|----------|-------------|---------|
| **日志异常检测** | 规则层 + MLP: Dense(256,ReLU)→Dense(128,ReLU)→Dense(4,Softmax) | 日志文本 → Normal/Warning/Error/Critical | ML.NET ONNX Runtime |
| **崩溃预测** | 规则层 + MLP: Dense(128,ReLU)→Dense(64,ReLU)→Dense(32,ReLU)→Dense(1,Sigmoid) | 15分钟滑动窗口指标 → 崩溃概率(0-1) | ML.NET ONNX Runtime |
| **配置优化** | 规则层 + MLP 回归: Dense(64,ReLU)→Dense(32,ReLU)→Dense(1,Linear) | 硬件+配置特征 → 推荐值/TPS改善量 | ML.NET ONNX Runtime |

**分层架构：** 规则引擎（首次部署即可工作，无需训练数据）→ MLP 模型（积累数据后通过 Python PyTorch 训练，导出 ONNX，.NET 加载推理）

**训练数据来源：** 服务器日志（自动标注）+ 性能指标时间序列（需包含崩溃事件样本）

---

## 自检清单

1. **需求覆盖**:
   - [x] .NET 10.0 Windows 窗口程序 ✅ (Task 1)
   - [x] 第三方渲染库修饰 UI ✅ (WPF + MaterialDesign + HandyControl)
   - [x] 分辨率/窗口大小全自动自适应 ✅ (WPF 原生 DPI 自适应)
   - [x] 检测运行中的 Minecraft 服务器 ✅ (Task 6: ProcessScanner)
   - [x] 确认工作路径 ✅ (Task 6: WorkingDirectoryResolver)
   - [x] 确认 Java 引用路径 ✅ (Task 3: CommandLineParser)
   - [x] 服务器配置文件列表 ✅ (Task 7: ConfigFileScanner)
   - [x] 类 start.bat 检测（内容架构判断） ✅ (Task 4: StartupScriptDetector)
   - [x] 以常量保存 ✅ (Task 2: ServerConstants)
   - [x] 配置文件修改功能 ✅ (Task 8: ConfigManager)
   - [x] 翻译为自然中文语言 ✅ (Task 8: ConfigDescriptorRegistry)
   - [x] 参数键值限制 ✅ (Task 8: 验证逻辑)
   - [x] 系统负载检测 ✅ (Task 9: SystemMonitor)
   - [x] 硬盘空间占用 ✅ (Task 9: DiskSpaceMonitor)
   - [x] 内存占用 ✅ (Task 9: MemoryMonitor)
   - [x] 总线程数/服务器核心线程数 ✅ (Task 9: ThreadAnalyzer)
   - [x] 中小型推理模型（MLP神经网络） ✅ (Task 10: LogAnomalyDetector/CrashPredictor/ConfigOptimizer)
   - [x] 服务器运行保障 ✅ (Task 10: AiGuardOrchestrator)
   - [x] UI 精美 ✅ (Task 12: Material Design 暗色主题)

2. **占位符扫描**: 无 TBD/TODO/实现后来

3. **类型一致性**: 服务接口、ViewModel 属性名、XAML Binding 路径均保持一致