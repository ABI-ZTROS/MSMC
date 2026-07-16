# MSMC - AI Agent 上下文文档

> 本文档帮助 AI Agent 快速理解 MSMC 项目，以便高效参与开发、审查或修改代码。

---

## 1. 项目概述

| 项目 | 说明 |
|------|------|
| 名称 | MSMC (Minecraft Server Management Client) |
| 用途 | .NET 10.0 WPF 桌面应用，用于检测、管理和保护运行中的 Minecraft Java 版服务器 |
| GitHub | https://github.com/ABI-ZTROS/MSMC |
| 技术基底 | .NET 10.0 + WPF + MaterialDesignInXAML + HandyControl |

---

## 2. 技术栈

| 库 | 版本 | 用途 |
|----|------|------|
| .NET | 10.0 (net10.0-windows) | WPF 桌面应用框架，启用 `EnableWindowsTargeting` 实现跨平台编译 |
| MaterialDesignInXAML Toolkit | 5.x | UI 主题（DeepPurple + Lime 暗色主题） |
| HandyControl | 3.x | 辅助控件库 |
| CommunityToolkit.Mvvm | 8.x | MVVM 源生成器，使用 `[ObservableProperty]`、`[RelayCommand]` |
| ML.NET | 4.x | 机器学习框架，MLP 神经网络推理 |
| OnnxRuntime | - | ONNX 模型运行时，配合 ML.NET 进行推理 |
| Serilog | 4.x | 日志框架，全局静态调用 |
| YamlDotNet | 16.x | YAML 配置文件解析 |

---

## 3. 项目结构

```
src/McServerGuard/                # 主项目 (WPF)
├── Constants/                    # 服务器类型枚举、JVM 参数常量
├── Models/                       # 数据模型
│   ├── ServerInstance            #   服务器实例
│   ├── ServerConfigEntry         #   配置条目
│   ├── SystemMetrics             #   系统指标
│   ├── DetectionResult           #   检测结果
│   └── ...
├── Services/
│   ├── ServerDetection/          # 服务器检测（进程扫描、命令行解析、启动脚本检测、类型分类、工作目录解析）
│   ├── ConfigManagement/         # 配置管理（properties/yml/json 解析器、中文描述注册表、验证）
│   ├── SystemMonitoring/         # 系统监控（CPU/内存/磁盘/线程）
│   └── AIService/               # AI 保障（日志异常检测、崩溃预测、配置优化建议、ONNX 编排）
├── ViewModels/                   # MVVM ViewModel（MainViewModel + 4 个子页面）
├── Views/                        # WPF 视图（MainWindow + 4 个页面）
└── Converters/                   # 值转换器

src/McServerGuard.Tests/          # xUnit 测试项目
.github/workflows/ci.yml          # CI 配置（Windows runner）
```

---

## 4. 核心架构决策

> 以下决策是项目的重要约定，修改代码时必须遵守。

### 4.1 日志：Serilog 静态调用

```csharp
// 正确
Log.Information("服务器已启动: {Name}", name);
Log.Debug("检测到进程 PID={Pid}", pid);

// 错误 - 不要这样做
public class SomeService(ILogger<SomeService> logger) { }  // 禁止
```

本项目使用 Serilog 的**全局静态方法** `Log.*`，不使用 `ILogger<T>` 依赖注入。

### 4.2 监控：Timer + 回调模式

```csharp
// 正确 - 使用 Timer + CancellationToken + Action<T> 回调
using var timer = new System.Threading.Timer(
    callback: metrics => UpdateMetrics((SystemMetrics)metrics),
    state: null,
    dueTime: TimeSpan.Zero,
    period: TimeSpan.FromSeconds(5));

// 错误 - 不要使用 Reactive Extensions
// Observable.Timer(...).Subscribe(...)  // 禁止
```

### 4.3 图表：纯文本显示

项目已移除 LiveCharts2（不兼容 .NET 10），监控页面使用 `TextBox` 显示文本数据，**不要**引入 LiveChartsCore 或 SkiaSharpView。

### 4.4 AI 模型：MLP 神经网络

AI 功能使用 ML.NET + ONNX Runtime 运行 **MLP 中小型神经网络**（用于日志异常检测、崩溃预测、配置优化建议），**不是** LLM 大语言模型。不要引入 OpenAI/LLM 相关依赖。

### 4.5 ServerConfigDescriptor 命名冲突

`ConfigDescriptorRegistry` 中有同名类 `ServerConfigDescriptor`（位于 `Services/ConfigManagement` 命名空间内），与 `Models/ServerConfigDescriptor` 不同。ViewModels 通过 `using` 别名解决冲突：

```csharp
using ConfigDescriptor = McServerGuard.Services.ConfigManagement.ConfigDescriptorRegistry.ServerConfigDescriptor;
```

### 4.6 PropertiesParser 大小写不敏感

`PropertiesParser` 使用**大小写不敏感**的字典来解析 Minecraft server.properties 文件，因为不同服务端对键名大小写处理不一致。

### 4.7 启动脚本检测：内容模式分析

检测服务器启动脚本时，基于**文件内容模式分析**（正则匹配 `java`、`-jar`、`nogui` 等关键字），**不是**通过文件名或扩展名判断。这使得检测能适应各种自定义脚本命名。

---

## 5. 开发规范

### 5.1 注释风格

注释使用**中文**，风格俏皮自然，可以加 emoji。例如：

```csharp
/// <summary>
/// 扫描系统中的 Minecraft 服务器进程，就像雷达扫描天空一样~
/// </summary>
public void ScanServers() { }
```

### 5.2 Git 配置

- 提交者: `Wis'adel <ABI-ZTROS@users.noreply.github.com>`

### 5.3 常用命令

```bash
# 编译
dotnet build McServerGuard.sln

# 测试（需要 Windows 环境）
dotnet test src/McServerGuard.Tests
```

---

## 6. 已知陷阱 / 踩坑记录

| 问题 | 正确做法 |
|------|----------|
| NuGet 版本通配符只能用 `8.*`，不能用 `8.*.*` | `<PackageReference Include="SomeLib" Version="8.*" />` |
| `Microsoft.ML.Onnx` 包不存在 | 正确包名是 `Microsoft.ML.OnnxRuntime` |
| LiveChartsCore.SkiaSharpView.WPF 不支持 .NET 10 | 已移除，监控页面用 TextBox 显示文本 |
| `Assert.Equal(string, string)` 在 .NET 10 xUnit 中签名变化 | 用 `Assert.True(string.Equals(expected, actual))` 替代 |
| 中文引号 "" 在 C# 字符串中需要小心处理 | 注意区分中文引号 `""` 与英文引号 `""`，避免字符串解析错误 |
