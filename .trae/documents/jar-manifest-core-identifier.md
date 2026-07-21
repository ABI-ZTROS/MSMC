# 增强型服务器核心代号识别：JAR Manifest 解包识别

## 摘要

参考 DeepSeek 分享的思路并通过网络搜索收集所有已知服务器核心的包特征，新增基于 **JAR 解包** 的服务器核心代号识别方法。通过 `System.IO.Compression.ZipArchive` 读取 JAR 内部 `META-INF/MANIFEST.MF` 的 `Main-Class` 字段，辅以特征类存在性检查，解决现有识别方法的痛点：
1. **JAR 被重命名后无法识别**（如 `myserver.jar`、`core.jar`）
2. **Paper/Purpur/Pufferfish/Folia 互相混淆**（Main-Class 相同，需特征类区分）
3. **混合端（Mohist/Arclight/CatServer）完全无法识别**
4. **代理端（BungeeCord/Velocity）落入 Unknown**

该方法作为现有识别链路的**第三级兜底**：JAR 名 → 配置文件 → JAR Manifest。带基于 JAR 路径的长 TTL 缓存（JAR 内容不变则结果不变），避免每轮 3 秒检测循环都解包。

---

## 现状分析

基于代码探索（Phase 1）确认的关键事实：

### 现有识别链路
| 层级 | 实现位置 | 机制 | 局限 |
|---|---|---|---|
| 第一级：JAR 名 | [ServerTypeClassifier.ClassifyByJarName](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs#L45) | glob 通配符匹配（`paper-*.jar` 等） | 重命名后失效；`server.jar` 过于通用 |
| 第二级：配置文件 | [ServerTypeClassifier.InferFromConfigFiles](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs#L122) | 检查 `paper-global.yml`/`folia-global.yml` 等存在性 | Purpur/Pufferfish 也产生 `paper-global.yml` → 误判为 Paper |
| 兜底 | [ServerDetector.DiscoverByPortScanAsync](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerDetector.cs) | 端口扫描发现新实例 | 类型一律设为 `Unknown` |

### 现有 ServerType 枚举
[ServerConstants.cs:14-55](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs#L14-L55) 定义 8 个值：`Unknown, Vanilla, Spigot, Paper, Forge, Fabric, Bukkit, Folia`。**缺少大量核心**：Purpur、Pufferfish、NeoForge、BungeeCord、Velocity、Mohist、Arclight、CatServer、Sponge。

### 现有缓存模式
[ServerDetector.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerDetector.cs) 已有两套缓存：PID 生命周期缓存（15s TTL）+ 端口扫描缓存（10s TTL）。新组件应遵循同样的缓存-aside 模式。

### 技术选型
| 需求 | 方案 | 理由 |
|---|---|---|
| 读取 JAR（ZIP）内部 | `System.IO.Compression.ZipArchive`（.NET 内置） | JAR 即 ZIP 格式；无需额外 NuGet 包 |
| 解析 MANIFEST.MF | 逐行读取 + `string.StartsWith("Main-Class:")` | 格式简单，无需引入 manifest 解析库 |
| 特征类检查 | `ZipArchive.GetEntry("io/papermc/...")` 非 null 判定 | 直接查 ZIP 条目，不解压到磁盘 |

---

## 服务器核心包特征数据库（网络搜索汇总）

通过搜索官方文档、社区博客、Jenkins 构建日志等确认的特征：

### Main-Class 映射表

| 核心 | Main-Class | 来源/备注 |
|---|---|---|
| **Vanilla** | `net.minecraft.bundler.Main` | 1.17+ 使用 bundler 打包 |
| **Vanilla** | `net.minecraft.server.MinecraftServer` | 1.16 及以下旧版 |
| **Spigot/CraftBukkit** | `org.bukkit.craftbukkit.Main` | Spigot 和 CraftBukkit 共用（CraftBukkit 是 Spigot 的底层） |
| **Paper 系** | `io.papermc.paperclip.Paperclip` | Paperclip 包装器，Paper/Folia/Purpur/Pufferfish 共用，需特征类区分 |
| **Forge** | `cpw.mods.modlauncher.Launcher` | 1.13+ 使用 modlauncher |
| **Forge** | `net.minecraftforge.fml.relauncher.ServerLaunchWrapper` | 1.12 及以下旧版 |
| **NeoForge** | `cpw.mods.modlauncher.Launcher` | 与 Forge 共用 modlauncher，需特征类区分 |
| **Fabric** | `net.fabricmc.loader.impl.launch.server.FabricServerLauncher` | fabric-loader 0.12+，MANIFEST.MF 明确声明 |
| **Fabric** | `net.fabricmc.loader.launch.server.FabricServerLauncher` | 旧版 fabric-loader（无 impl 子包） |
| **BungeeCord** | `net.md_5.bungee.Bootstrap` | 代理端，Jenkins 构建确认 bootstrap 模块 |
| **Velocity** | `com.velocitypowered.proxy.Velocity` | 代理端 |
| **SpongeVanilla** | `org.spongepowered.server.launch.VanillaServerLaunch` | Sponge 原版实现 |

### 特征类检查表（区分同 Main-Class 的核心）

**Paper 系**（Main-Class = `io.papermc.paperclip.Paperclip`）：
| 核心 | 特征类路径 | 优先级 |
|---|---|---|
| **Folia** | `io/papermc/paper/threadedregions/RegionizedServer.class` | 1（派生类优先） |
| **Purpur** | `org/purpurmc/purpur/PurpurConfig.class` | 2 |
| **Pufferfish** | `gg/pufferfish/pufferfish/PufferfishConfig.class` | 3 |
| **Paper** | （排除以上后默认） | 4 |

**Forge/NeoForge 系**（Main-Class = `cpw.mods.modlauncher.Launcher`）：
| 核心 | 特征类/包路径 | 优先级 |
|---|---|---|
| **Mohist** | `com/mohistmc/` 包存在 | 1（混合端优先） |
| **Arclight** | `io/izzel/arclight/` 包存在 | 2 |
| **CatServer** | `catserver/` 包存在 | 3 |
| **NeoForge** | `net/neoforged/` 包存在 | 4 |
| **Forge** | （排除以上后默认） | 5 |

### 特征配置文件表（辅助识别，JAR 外部）

| 核心 | 特征配置文件 |
|---|---|
| Folia | `config/folia-global.yml` |
| Purpur | `purpur.yml`, `config/purpur.yml` |
| Pufferfish | `pufferfish.yml` |
| Mohist | `mohist-config.yml` |
| Arclight | `arclight.yml` |
| NeoForge | `neoforge.yml`（新版本生成） |

---

## 变更方案

### 步骤 1：大幅扩展 ServerType 枚举

**文件**：[ServerConstants.cs:14-55](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs#L14-L55)

**操作**：在现有枚举后追加 9 个新值

```csharp
public enum ServerType
{
    Unknown,
    Vanilla,
    Spigot,
    Paper,
    Forge,
    Fabric,
    Bukkit,
    Folia,
    // === 新增核心类型 ===
    /// <summary>Purpur 服务端，基于 Paper 的高性能优化分支</summary>
    Purpur,
    /// <summary>Pufferfish 服务端，基于 Paper 的异步优化分支</summary>
    Pufferfish,
    /// <summary>NeoForge 服务端，Forge 的现代分支</summary>
    NeoForge,
    /// <summary>BungeeCord 代理端，经典 Minecraft 代理</summary>
    BungeeCord,
    /// <summary>Velocity 代理端，现代高性能代理</summary>
    Velocity,
    /// <summary>Mohist 混合端，Forge + Bukkit 混合</summary>
    Mohist,
    /// <summary>Arclight 混合端，可配置 Forge/NeoForge/Fabric + Bukkit 混合</summary>
    Arclight,
    /// <summary>CatServer 混合端，Forge + Bukkit 混合</summary>
    CatServer,
    /// <summary>Sponge 服务端，独立插件 API（SpongeVanilla）</summary>
    Sponge
}
```

---

### 步骤 2：扩展 ServerConstants 的 JAR 模式与配置指示

**文件**：[ServerConstants.cs](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs)

追加新核心的 JAR 名模式：
```csharp
public static readonly string[] PurpurJarPatterns = ["purpur-*.jar", "purpur.jar"];
public static readonly string[] PufferfishJarPatterns = ["pufferfish-*.jar", "pufferfish.jar"];
public static readonly string[] NeoForgeJarPatterns = ["neoforge-*.jar", "neoforge.jar"];
public static readonly string[] BungeeCordJarPatterns = ["bungeecord-*.jar", "bungeecord.jar"];
public static readonly string[] VelocityJarPatterns = ["velocity-*.jar", "velocity.jar"];
public static readonly string[] MohistJarPatterns = ["mohist-*.jar", "mohist.jar"];
public static readonly string[] ArclightJarPatterns = ["arclight-*.jar", "arclight.jar"];
public static readonly string[] CatServerJarPatterns = ["catserver-*.jar", "catserver.jar"];
public static readonly string[] SpongeJarPatterns = ["sponge-*.jar", "spongevanilla-*.jar", "spongeforge-*.jar"];
```

扩展 `TypeIndicatorFiles` 字典：
```csharp
[ServerType.Purpur] = ["purpur.yml", "config/purpur.yml"],
[ServerType.Pufferfish] = ["pufferfish.yml"],
[ServerType.NeoForge] = ["neoforge.yml", "config/neoforge/"],
[ServerType.BungeeCord] = ["config.yml"],
[ServerType.Velocity] = ["velocity.toml"],
[ServerType.Mohist] = ["mohist-config.yml"],
[ServerType.Arclight] = ["arclight.yml"],
[ServerType.CatServer] = ["catserver.yml"],
[ServerType.Sponge] = ["config/sponge/", "global.conf"],
```

---

### 步骤 3：新增 JarCoreIdentifier 服务

**文件**：`/workspace/src/McServerGuard/Services/ServerDetection/JarCoreIdentifier.cs`（新建）

**职责**：解包 JAR 文件，读取 `META-INF/MANIFEST.MF` 的 `Main-Class`，辅以特征类检查识别核心类型。带基于 JAR 路径的长 TTL 缓存。

**核心 API**：
```csharp
public sealed class JarCoreIdentifier
{
    public async Task<ServerType> IdentifyAsync(string jarPath)
    {
        // 1. 查缓存（5 分钟 TTL，JAR 内容不变）
        // 2. FileStream + FileShare.ReadWrite 打开 JAR
        // 3. ZipArchive 读取 META-INF/MANIFEST.MF
        // 4. 解析 Main-Class 字段
        // 5. 按 MainClassMap 查基础类型
        // 6. Paper 系/Forge 系用特征类检查进一步区分
        // 7. 写缓存并返回
    }
}
```

**Main-Class 映射表**（完整版，基于网络搜索确认）：
```csharp
private static readonly Dictionary<string, ServerType> MainClassMap = new(StringComparer.OrdinalIgnoreCase)
{
    // Vanilla
    ["net.minecraft.bundler.Main"] = ServerType.Vanilla,
    ["net.minecraft.server.MinecraftServer"] = ServerType.Vanilla,
    // Spigot / CraftBukkit
    ["org.bukkit.craftbukkit.Main"] = ServerType.Spigot,
    // Paper 系（需特征类进一步区分 Paper/Folia/Purpur/Pufferfish）
    ["io.papermc.paperclip.Paperclip"] = ServerType.Paper, // 占位
    // Forge / NeoForge 系（需特征类进一步区分 Forge/NeoForge/Mohist/Arclight/CatServer）
    ["cpw.mods.modlauncher.Launcher"] = ServerType.Forge, // 占位
    ["net.minecraftforge.fml.relauncher.ServerLaunchWrapper"] = ServerType.Forge, // 旧版 Forge
    // Fabric
    ["net.fabricmc.loader.impl.launch.server.FabricServerLauncher"] = ServerType.Fabric,
    ["net.fabricmc.loader.launch.server.FabricServerLauncher"] = ServerType.Fabric, // 旧版
    // BungeeCord
    ["net.md_5.bungee.Bootstrap"] = ServerType.BungeeCord,
    // Velocity
    ["com.velocitypowered.proxy.Velocity"] = ServerType.Velocity,
    // SpongeVanilla
    ["org.spongepowered.server.launch.VanillaServerLaunch"] = ServerType.Sponge,
};
```

**特征类消歧方法**：
```csharp
/// <summary>
/// 消歧 Paper 系核心（Folia/Purpur/Pufferfish/Paper）
/// </summary>
private static ServerType DisambiguatePaperFamily(ZipArchive jar)
{
    if (jar.GetEntry("io/papermc/paper/threadedregions/RegionizedServer.class") != null)
        return ServerType.Folia;
    if (jar.GetEntry("org/purpurmc/purpur/PurpurConfig.class") != null)
        return ServerType.Purpur;
    if (jar.GetEntry("gg/pufferfish/pufferfish/PufferfishConfig.class") != null)
        return ServerType.Pufferfish;
    return ServerType.Paper;
}

/// <summary>
/// 消歧 Forge 系核心（Mohist/Arclight/CatServer/NeoForge/Forge）
/// </summary>
private static ServerType DisambiguateForgeFamily(ZipArchive jar)
{
    if (HasEntryPrefix(jar, "com/mohistmc/"))
        return ServerType.Mohist;
    if (HasEntryPrefix(jar, "io/izzel/arclight/"))
        return ServerType.Arclight;
    if (HasEntryPrefix(jar, "catserver/"))
        return ServerType.CatServer;
    if (HasEntryPrefix(jar, "net/neoforged/"))
        return ServerType.NeoForge;
    return ServerType.Forge;
}

/// <summary>
/// 检查 JAR 是否存在指定前缀的条目（用于包级别特征检测）
/// </summary>
private static bool HasEntryPrefix(ZipArchive jar, string prefix)
{
    // ZipArchive 的 Entries 是全量枚举，包前缀检查需遍历
    // 优化：只检查前几个条目，命中即返回
    foreach (var entry in jar.Entries)
    {
        if (entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}
```

**实现要点**：
- `FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)` + `ZipArchive`，using 包裹
- 缓存：`Dictionary<string, (ServerType Type, DateTime Timestamp)>`，TTL 5 分钟
- 容错：任何异常返回 `ServerType.Unknown`，不抛异常
- 特征类路径用正斜杠（ZIP 条目路径规范）
- `HasEntryPrefix` 遍历 Entries 检查包前缀（Mohist/Arclight 等混合端没有单一特征类，用包前缀检测）

---

### 步骤 4：ServerTypeClassifier 扩展新核心

**文件**：[ServerTypeClassifier.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs)

在 `JarNameTypeMap` 数组（L25-34）追加新核心（派生类优先排序）：
```csharp
private static readonly (string[] Patterns, ServerType Type)[] JarNameTypeMap =
[
    (ServerConstants.VanillaJarPatterns, ServerType.Vanilla),
    (ServerConstants.BukkitJarPatterns, ServerType.Bukkit),
    (ServerConstants.SpigotJarPatterns, ServerType.Spigot),
    // Paper 系派生类优先（Folia > Purpur > Pufferfish > Paper）
    (ServerConstants.FoliaJarPatterns, ServerType.Folia),
    (ServerConstants.PurpurJarPatterns, ServerType.Purpur),
    (ServerConstants.PufferfishJarPatterns, ServerType.Pufferfish),
    (ServerConstants.PaperJarPatterns, ServerType.Paper),
    // Forge 系派生类优先（Mohist > Arclight > CatServer > NeoForge > Forge）
    (ServerConstants.MohistJarPatterns, ServerType.Mohist),
    (ServerConstants.ArclightJarPatterns, ServerType.Arclight),
    (ServerConstants.CatServerJarPatterns, ServerType.CatServer),
    (ServerConstants.NeoForgeJarPatterns, ServerType.NeoForge),
    (ServerConstants.ForgeJarPatterns, ServerType.Forge),
    (ServerConstants.FabricJarPatterns, ServerType.Fabric),
    // 代理端
    (ServerConstants.BungeeCordJarPatterns, ServerType.BungeeCord),
    (ServerConstants.VelocityJarPatterns, ServerType.Velocity),
    // Sponge
    (ServerConstants.SpongeJarPatterns, ServerType.Sponge),
];
```

在 `InferFromConfigFiles` 的 `uniqueChecks` 数组追加新核心（派生类优先）：
```csharp
var uniqueChecks = new[]
{
    // Paper 系派生类优先
    (ServerType.Folia,      new[] { "config/folia-global.yml" }),
    (ServerType.Purpur,     new[] { "purpur.yml", "config/purpur.yml" }),
    (ServerType.Pufferfish, new[] { "pufferfish.yml" }),
    (ServerType.Paper,      new[] { "config/paper-global.yml" }),
    // Forge 系派生类优先
    (ServerType.Mohist,     new[] { "mohist-config.yml" }),
    (ServerType.Arclight,   new[] { "arclight.yml" }),
    (ServerType.CatServer,  new[] { "catserver.yml" }),
    (ServerType.NeoForge,   new[] { "neoforge.yml", "config/neoforge/" }),
    (ServerType.Forge,      new[] { "forge-server.toml" }),
    // Fabric
    (ServerType.Fabric,     new[] { "fabric-server-launch.properties", ".fabric/" }),
    // Spigot / Bukkit
    (ServerType.Spigot,     new[] { "spigot.yml" }),
    (ServerType.Bukkit,     new[] { "bukkit.yml" }),
    // 代理端
    (ServerType.BungeeCord, new[] { "config.yml" }),
    (ServerType.Velocity,   new[] { "velocity.toml" }),
    // Sponge
    (ServerType.Sponge,     new[] { "config/sponge/", "global.conf" }),
};
```

---

### 步骤 5：ServerDetector 集成 Manifest 兜底识别

**文件**：[ServerDetector.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerDetector.cs)

#### 5.1 构造函数注入新依赖

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

#### 5.2 BuildServerInstanceAsync 追加第三级判定

在 `ServerTypeClassifier.ClassifyByJarNameAndConfigFiles` 之后插入 Manifest 兜底：
```csharp
var serverType = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles(jarName, workingDir);

// === 第三级兜底：JAR Manifest 解包识别 ===
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

---

### 步骤 6：DI 注册新服务

**文件**：[App.xaml.cs](file:///workspace/src/McServerGuard/App.xaml.cs) L88 后

```csharp
services.AddSingleton<JarCoreIdentifier>();
```

---

### 步骤 7：扩展 ProcessScanner 的 ServerJarKeywords

**文件**：[ServerConstants.cs](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs) L81

在 `ServerJarKeywords` 数组追加新核心关键词，确保进程扫描能捕获这些核心：
```csharp
public static readonly string[] ServerJarKeywords = [
    "minecraft_server", "server", "spigot", "paper", "forge", "fabric-server-launch",
    "craftbukkit", "folia", "purpur", "pufferfish", "neoforge", "bungeecord", "velocity",
    "mohist", "arclight", "catserver", "sponge", "spongevanilla"
];
```

---

### 步骤 8：编译验证 + 提交

```bash
cd /workspace
dotnet build src/McServerGuard/McServerGuard.csproj -c Debug -r win-x64 --self-contained
```

验证点：
- `System.IO.Compression` 引用可用（.NET 9 内置）
- ServerType 枚举扩展后无 switch 表达式遗漏
- ServerDetector 构造函数 7 参数依赖注入链完整
- ServerTypeClassifier 的 JarNameTypeMap 和 uniqueChecks 数组无类型遗漏

编译通过后提交到 main：
```bash
git add src/McServerGuard/Constants/ServerConstants.cs \
        src/McServerGuard/Services/ServerDetection/JarCoreIdentifier.cs \
        src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs \
        src/McServerGuard/Services/ServerDetection/ServerDetector.cs \
        src/McServerGuard/App.xaml.cs

git commit -m "feat: 增强 JAR Manifest 解包识别，覆盖全部已知服务器核心

- ServerType 枚举扩展 9 个核心：Purpur/Pufferfish/NeoForge/BungeeCord/Velocity/Mohist/Arclight/CatServer/Sponge
- 新增 JarCoreIdentifier：解包 JAR 读取 MANIFEST.MF Main-Class + 特征类检查
- Paper 系消歧：Folia(RegionizedServer)/Purpur(PurpurConfig)/Pufferfish(PufferfishConfig)/Paper
- Forge 系消歧：Mohist/Arclight/CatServer/NeoForge/Forge（包前缀检测）
- Main-Class 映射覆盖 13 个核心（Vanilla/Spigot/Paper/Forge/Fabric/BungeeCord/Velocity/Sponge 等）
- ServerDetector 集成第三级兜底，带 5 分钟 TTL 缓存"
```

---

## 假设与决策

### 假设
1. **JAR 文件可读**：服务器运行时 JAR 文件可通过 `FileShare.ReadWrite` 共享读取
2. **MANIFEST.MF 路径固定**：始终位于 `META-INF/MANIFEST.MF`（JAR 规范标准）
3. **Main-Class 跨版本稳定**：主流核心的 Main-Class 模式跨版本稳定
4. **特征类路径稳定**：`RegionizedServer`/`PurpurConfig`/`PufferfishConfig` 等在各版本中路径不变
5. **混合端用包前缀可识别**：Mohist 的 `com/mohistmc/`、Arclight 的 `io/izzel/arclight/` 等包前缀稳定

### 决策
| 决策点 | 选择 | 理由 |
|---|---|---|
| 解包库 | `System.IO.Compression.ZipArchive`（.NET 内置） | 无需额外 NuGet；JAR 即 ZIP |
| 触发时机 | 仅 Unknown/Vanilla 时兜底 | JAR 名已识别的不重复解包 |
| 缓存 TTL | 5 分钟 | JAR 内容不变 |
| 枚举扩展 | 9 个新核心 | 覆盖网络搜索到的所有已知核心 |
| 消歧策略 | 特征类优先级 + 包前缀检测 | 派生类优先；混合端无单一特征类用包前缀 |
| 代理端支持 | BungeeCord/Velocity | 代理端也是 Java 进程，应识别 |

### 不做的事
- 不读取 `Implementation-Version` 提取版本号（暂无需求）
- 不计算 JAR 哈希比对（需维护庞大哈希库）
- 不解析 `plugin.yml`（那是插件识别）
- 不修改 ServerTypeClassifier 为实例类（保持静态）

---

## 验证步骤

1. **编译验证**：`dotnet build` 无错误
2. **识别准确性验证**（手动）：
   - 重命名的 Paper JAR → Paper（排除 Folia/Purpur/Pufferfish 特征类后）
   - Folia JAR → Folia（`RegionizedServer` 命中）
   - Purpur JAR → Purpur（`PurpurConfig` 命中）
   - Mohist JAR → Mohist（`com/mohistmc/` 包前缀命中）
   - Velocity JAR → Velocity（Main-Class `com.velocitypowered.proxy.Velocity`）
   - 原版 server.jar 重命名为 core.jar → Vanilla（Main-Class `net.minecraft.bundler.Main`）
3. **缓存验证**：首次解包后 5 分钟内同一 JAR 路径命中缓存
4. **兜底链路验证**：JAR 名匹配成功时不触发解包；Unknown 时触发解包
5. **容错验证**：JAR 删除/损坏/MANIFEST 缺失均返回 Unknown 不抛异常
