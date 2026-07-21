# 增强型服务器核心代号识别：JAR Manifest 解包识别

## 摘要

参考 DeepSeek 分享的思路，新增基于 **JAR 解包** 的服务器核心代号识别方法。通过 `System.IO.Compression.ZipArchive` 读取 JAR 内部 `META-INF/MANIFEST.MF` 的 `Main-Class` 字段，辅以特征类存在性检查，解决现有识别方法的三大痛点：
1. **JAR 被重命名后无法识别**（如 `myserver.jar`、`core.jar`）
2. **Paper/Purpur 混淆**（Purpur 产生 `paper-global.yml`，被误判为 Paper）
3. **Folia 误判为 Paper**（两者 Main-Class 相同，需特征类区分）

该方法作为现有识别链路的**第三级兜底**：JAR 名 → 配置文件 → JAR Manifest。带基于 JAR 路径的长 TTL 缓存（JAR 内容不变则结果不变），避免每轮 3 秒检测循环都解包。

---

## 现状分析

基于代码探索（Phase 1）确认的关键事实：

### 现有识别链路
| 层级 | 实现位置 | 机制 | 局限 |
|---|---|---|---|
| 第一级：JAR 名 | [ServerTypeClassifier.ClassifyByJarName](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs#L45) | glob 通配符匹配（`paper-*.jar` 等） | 重命名后失效；`server.jar` 过于通用 |
| 第二级：配置文件 | [ServerTypeClassifier.InferFromConfigFiles](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs#L122) | 检查 `paper-global.yml`/`folia-global.yml` 等存在性 | Purpur 也产生 `paper-global.yml` → 误判为 Paper |
| 兜底 | [ServerDetector.DiscoverByPortScanAsync](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerDetector.cs) | 端口扫描发现新实例 | 类型一律设为 `Unknown` |

### 现有 ServerType 枚举
[ServerConstants.cs:14-55](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs#L14-L55) 定义 8 个值：`Unknown, Vanilla, Spigot, Paper, Forge, Fabric, Bukkit, Folia`。**缺少 Purpur**——Paper 生态的重要分支，与 Paper 共享配置文件，仅靠现有方法无法区分。

### 现有缓存模式
[ServerDetector.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerDetector.cs) 已有两套缓存：PID 生命周期缓存（15s TTL）+ 端口扫描缓存（10s TTL）。新组件应遵循同样的缓存-aside 模式。

### JAR 文件访问注意
服务器运行时 Java 进程会持有 JAR 文件句柄。解包读取时必须用 `FileStream + FileShare.ReadWrite` 打开，避免文件锁冲突（与 [ServerPortResolver.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerPortResolver.cs) 读 server.properties 的模式一致）。

### 技术选型
| 需求 | 方案 | 理由 |
|---|---|---|
| 读取 JAR（ZIP）内部 | `System.IO.Compression.ZipArchive`（.NET 内置） | JAR 即 ZIP 格式；无需额外 NuGet 包，符合"减少手搓"原则 |
| 解析 MANIFEST.MF | 逐行读取 + `string.StartsWith("Main-Class:")` | 格式简单，无需引入 manifest 解析库 |
| 特征类检查 | `ZipArchive.GetEntry("io/papermc/...")` 非 null 判定 | 直接查 ZIP 条目，不解压到磁盘 |

---

## 变更方案

### 步骤 1：扩展 ServerType 枚举，新增 Purpur

**文件**：[ServerConstants.cs:14-55](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs#L14-L55)

**操作**：在 `Folia` 后追加 `Purpur` 枚举值

```csharp
/// <summary>
/// Folia 服务端，基于 Paper 的多线程区域化服务端。
/// </summary>
Folia,

/// <summary>
/// Purpur 服务端，基于 Paper 的高性能优化分支，支持丰富的自定义配置。
/// </summary>
Purpur
```

**理由**：Purpur 与 Paper 共享 `config/paper-global.yml`，现有配置文件判定会误判为 Paper。需通过 JAR Manifest 特征类区分，因此需要独立枚举值承载识别结果。

**同步更新**：在 [ServerConstants.cs](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs) 的 `TypeIndicatorFiles` 字典追加 Purpur 条目（`config/purpur.yml`、`purpur.yml`），并在 `JarNameTypeMap`（[ServerTypeClassifier.cs:25-34](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs#L25-L34)）追加 `PurpurJarPatterns`（`purpur-*.jar`、`purpur.jar`）。

---

### 步骤 2：新增 JarCoreIdentifier 服务

**文件**：`/workspace/src/McServerGuard/Services/ServerDetection/JarCoreIdentifier.cs`（新建）

**职责**：解包 JAR 文件，读取 `META-INF/MANIFEST.MF` 的 `Main-Class`，辅以特征类检查识别核心类型。带基于 JAR 路径的长 TTL 缓存。

**核心 API**：
```csharp
public sealed class JarCoreIdentifier
{
    /// <summary>
    /// 通过解包 JAR 读取 Manifest 识别服务器核心类型
    /// </summary>
    /// <param name="jarPath">JAR 文件完整路径</param>
    /// <returns>识别出的服务器类型；无法识别或读取失败返回 Unknown</returns>
    public async Task<ServerType> IdentifyAsync(string jarPath)
    {
        // 1. 查缓存（JAR 内容不变，长 TTL 5 分钟）
        // 2. 用 FileStream + FileShare.ReadWrite 打开 JAR
        // 3. 用 ZipArchive 读取 META-INF/MANIFEST.MF
        // 4. 解析 Main-Class 字段
        // 5. 按 Main-Class 映射表查基础类型
        // 6. 若 Main-Class 指向 Paper 系，用特征类检查区分 Paper/Folia/Purpur
        // 7. 写缓存并返回
    }
}
```

**Main-Class 映射表**（参考 DeepSeek 思路 + 社区文档）：
```csharp
private static readonly Dictionary<string, ServerType> MainClassMap = new(StringComparer.OrdinalIgnoreCase)
{
    // Vanilla（1.17+ 用 bundler，旧版用 MinecraftServer）
    ["net.minecraft.bundler.Main"] = ServerType.Vanilla,
    ["net.minecraft.server.MinecraftServer"] = ServerType.Vanilla,
    // Spigot / Bukkit
    ["org.bukkit.craftbukkit.Main"] = ServerType.Spigot,
    ["org.spigotmc.launch.Main"] = ServerType.Spigot,
    // Paper 系（Paperclip 包装器，需特征类进一步区分 Paper/Folia/Purpur）
    ["io.papermc.paperclip.Paperclip"] = ServerType.Paper, // 占位，需进一步判定
    // Forge
    ["cpw.mods.modlauncher.Launcher"] = ServerType.Forge,
    ["net.minecraftforge.fml.relauncher.ServerLaunchWrapper"] = ServerType.Forge,
    // Fabric
    ["net.fabricmc.loader.impl.launch.server.FabricServerLauncher"] = ServerType.Fabric,
    ["net.fabricmc.loader.launch.server.FabricServerLauncher"] = ServerType.Fabric, // 旧版
};
```

**特征类检查**（区分 Paper 系的三个分支）：
```csharp
private static ServerType DisambiguatePaperFamily(ZipArchive jar)
{
    // 优先级：Folia > Purpur > Paper（派生类优先）
    if (jar.GetEntry("io/papermc/paper/threadedregions/RegionizedServer.class") != null)
        return ServerType.Folia;
    if (jar.GetEntry("org/purpurmc/purpur/PurpurConfig.class") != null)
        return ServerType.Purpur;
    return ServerType.Paper;
}
```

**实现要点**：
- `IdentifyAsync` 内部：`new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)` + `new ZipArchive(fs, ZipArchiveMode.Read)`，using 包裹确保释放
- 缓存：`Dictionary<string, (ServerType Type, DateTime Timestamp)>`，TTL 5 分钟（JAR 内容稳定，不需要像端口那样频繁刷新）
- 容错：JAR 不存在、无法打开、MANIFEST.MF 缺失、Main-Class 为空等任何异常都返回 `ServerType.Unknown`，不抛异常
- 特征类路径用正斜杠（ZIP 条目路径规范），不依赖操作系统路径分隔符
- 代码风格参考 [PortScanner.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/PortScanner.cs) / [ServerPortResolver.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerPortResolver.cs) 的注释规范

---

### 步骤 3：ServerDetector 集成 Manifest 兜底识别

**文件**：[ServerDetector.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerDetector.cs)

#### 3.1 构造函数注入新依赖

在现有 6 个构造参数后追加 `JarCoreIdentifier`：
```csharp
public ServerDetector(
    ProcessScanner processScanner,
    WorkingDirectoryResolver workingDirResolver,
    ConfigFileScanner configScanner,
    PortScanner portScanner,
    PortToProcessMapper portToProcessMapper,
    ServerPortResolver portResolver,
    JarCoreIdentifier jarCoreIdentifier)  // 新增
```

#### 3.2 BuildServerInstanceAsync 追加第三级判定

在 `ServerTypeClassifier.ClassifyByJarNameAndConfigFiles`（约 L241）之后、网络套件探测之前，插入 Manifest 兜底：

```csharp
// 服务器类型推断阶段（策略模式：JAR 名匹配 + 配置文件辅助）
var serverType = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles(jarName, workingDir);

// === 第三级兜底：JAR Manifest 解包识别 ===
// 当 JAR 名 + 配置文件均无法确定类型时，解包 JAR 读取 Main-Class
if (serverType == ServerType.Unknown || serverType == ServerType.Vanilla)
{
    if (!string.IsNullOrEmpty(parsed.JarFilePath) && File.Exists(parsed.JarFilePath))
    {
        var manifestType = await _jarCoreIdentifier.IdentifyAsync(parsed.JarFilePath);
        if (manifestType != ServerType.Unknown)
        {
            Log.Information("🔬 JAR Manifest 识别为核心类型: {Type}（覆盖原 {Old}）", manifestType, serverType);
            serverType = manifestType;
        }
    }
}
```

**理由**：
- 仅在 `Unknown`/`Vanilla` 时触发解包（JAR 名已识别为 Paper/Spigot 等时不重复解包，性能优先）
- 使用 `parsed.JarFilePath`（CommandLineParser 解析的完整 JAR 路径），非 `jarName`（仅文件名）
- Manifest 结果**优先级高于** JAR 名 + 配置文件（因为它是 JAR 内部元数据，比外部文件名/配置更具确定性）

---

### 步骤 4：DI 注册新服务

**文件**：[App.xaml.cs](file:///workspace/src/McServerGuard/App.xaml.cs) L88 后（`ServerPortResolver` 注册后）

```csharp
services.AddSingleton<JarCoreIdentifier>();
```

---

### 步骤 5：补充 Purpur 的 JAR 名模式与配置文件指示

**文件**：[ServerConstants.cs](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs)

追加 Purpur 相关常量：
```csharp
public static readonly string[] PurpurJarPatterns = ["purpur-*.jar", "purpur.jar"];
```

在 `TypeIndicatorFiles` 字典追加：
```csharp
[ServerType.Purpur] = ["purpur.yml", "config/purpur.yml"],
```

**文件**：[ServerTypeClassifier.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs)

在 `JarNameTypeMap` 数组（L25-34）中，`Folia` 之后、`Forge` 之前插入 Purpur（派生类优先）：
```csharp
(ServerConstants.FoliaJarPatterns, ServerType.Folia),
(ServerConstants.PurpurJarPatterns, ServerType.Purpur),  // 新增
(ServerConstants.ForgeJarPatterns, ServerType.Forge),
```

在 `InferFromConfigFiles` 的 `uniqueChecks` 数组（L126-134）追加 Purpur（在 Paper 之前，派生类优先）：
```csharp
(ServerType.Folia,  new[] { "config/folia-global.yml" }),
(ServerType.Purpur, new[] { "purpur.yml", "config/purpur.yml" }),  // 新增
(ServerType.Fabric, new[] { "fabric-server-launch.properties", ".fabric/" }),
(ServerType.Paper,  new[] { "config/paper-global.yml" }),
// ...
```

---

### 步骤 6：编译验证 + 提交

```bash
cd /workspace
dotnet build src/McServerGuard/McServerGuard.csproj -c Debug -r win-x64 --self-contained
```

验证点：
- `System.IO.Compression` 引用可用（.NET 9 内置，无需额外 NuGet）
- `ZipArchive` / `ZipArchiveEntry` API 调用正确
- ServerType 枚举扩展后无 switch 表达式遗漏警告
- ServerDetector 构造函数 7 参数依赖注入链完整

编译通过后提交到 main：
```bash
git add src/McServerGuard/Constants/ServerConstants.cs \
        src/McServerGuard/Services/ServerDetection/JarCoreIdentifier.cs \
        src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs \
        src/McServerGuard/Services/ServerDetection/ServerDetector.cs \
        src/McServerGuard/App.xaml.cs

git commit -m "feat: 增强 JAR Manifest 解包识别服务器核心代号

- 新增 JarCoreIdentifier：解包 JAR 读取 MANIFEST.MF Main-Class + 特征类检查
- ServerType 枚举新增 Purpur，区分 Paper/Purpur/Folia
- ServerDetector 集成第三级兜底（JAR 名→配置文件→Manifest）
- 特征类检查：RegionizedServer→Folia，PurpurConfig→Purpur
- 带 5 分钟 TTL 缓存，避免每轮检测都解包"
```

---

## 假设与决策

### 假设
1. **JAR 文件可读**：服务器运行时 JAR 文件可通过 `FileShare.ReadWrite` 共享读取（Java 进程不独占写锁）
2. **MANIFEST.MF 路径固定**：始终位于 `META-INF/MANIFEST.MF`（JAR 规范标准）
3. **Main-Class 稳定性**：主流核心的 Main-Class 跨版本稳定（Paperclip 模式、Fabric Loader 模式等）
4. **特征类路径稳定**：`io.papermc.paper.threadedregions.RegionizedServer` 和 `org.purpurmc.purpur.PurpurConfig` 在各版本中路径不变

### 决策
| 决策点 | 选择 | 理由 |
|---|---|---|
| 解包库 | `System.IO.Compression.ZipArchive`（.NET 内置） | 无需额外 NuGet；JAR 即 ZIP；符合"减少手搓"原则 |
| 触发时机 | 仅 Unknown/Vanilla 时兜底 | JAR 名已识别的不重复解包，性能优先 |
| 缓存 TTL | 5 分钟 | JAR 内容不变，比端口缓存（10s）长得多 |
| 枚举扩展 | 仅加 Purpur | DeepSeek 提到且现有逻辑有 Purpur 误判 bug；代理端（Velocity/BungeeCord）不在 Java 进程检测范围 |
| 特征类优先级 | Folia > Purpur > Paper | 派生类优先（Folia 是 Paper 分支，Purpur 也是，但两者特征类互斥） |
| Manifest 优先级 | 高于 JAR 名 + 配置文件 | JAR 内部元数据比外部文件名/配置更具确定性 |

### 不做的事（避免过度工程）
- 不读取 `Implementation-Version` 提取核心版本号（暂无 UI 展示需求，后续可扩展）
- 不计算 JAR 哈希比对（需维护庞大哈希库，DeepSeek 也指出此法不实用）
- 不解析 `plugin.yml`（那是插件识别，非核心识别）
- 不新增代理端枚举（Velocity/BungeeCord，超出当前 Java 进程检测范围）
- 不修改 `ServerTypeClassifier` 为实例类（保持静态，新逻辑放独立服务）

---

## 验证步骤

1. **编译验证**：`dotnet build` 无错误
2. **识别准确性验证**（手动）：
   - 准备一个重命名的 Paper JAR（如 `myserver.jar`）→ 应识别为 Paper
   - 准备一个 Folia JAR → 应识别为 Folia（特征类 `RegionizedServer` 命中）
   - 准备一个 Purpur JAR → 应识别为 Purpur（特征类 `PurpurConfig` 命中）
   - 准备一个原版 `server.jar`（重命名为 `core.jar`）→ 应识别为 Vanilla（Main-Class `net.minecraft.bundler.Main`）
3. **缓存验证**：
   - 日志确认首次解包后，5 分钟内同一 JAR 路径命中缓存无重复解包
4. **兜底链路验证**：
   - JAR 名匹配成功（如 `paper-1.20.4.jar`）→ 不触发解包（日志无 Manifest 识别记录）
   - JAR 名为 `Unknown`（如 `core.jar`）→ 触发解包兜底
5. **容错验证**：
   - JAR 文件被删除 → 返回 Unknown，不抛异常
   - JAR 文件损坏（非 ZIP）→ 返回 Unknown，不抛异常
   - MANIFEST.MF 缺失 → 返回 Unknown，不抛异常
