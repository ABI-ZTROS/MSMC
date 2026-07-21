# 增强型服务器核心代号识别 — 全核心覆盖实施计划

> 本计划在已落地的 JAR Manifest 解包识别（commit `323abb6`，覆盖 17 个核心）基础上，
> 结合网络搜索全面扩展，最终覆盖所有已知的 Java 版 Minecraft 服务器核心（共 32 个）。

---

## 一、摘要

用户在已完成的 17 个核心识别基础上，明确要求"结合网络搜索，收集目前所有已知服务器核心的包特征"。
本计划通过网络搜索 7 次，整理出 **15 个待新增的核心类型**，覆盖以下分类：

| 分类 | 待新增数量 | 核心列表 |
|---|---|---|
| BungeeCord 系代理端 | 3 | Waterfall, FlameCord, HexaCord |
| Fabric 系 Mod 加载器 | 1 | Quilt |
| Paper 系扩展插件端 | 6 | Airplane, Tuinity, Yatopia, Akarin, Kaiiju, NachoSpigot |
| Forge 系扩展混合端 | 2 | Magma, Banner |
| Bedrock / 独立实现 | 3 | Nukkit, PowerNukkit, Glowstone |
| Sponge 系扩展 | 1 | SpongeForge（Sponge on Forge） |

实施后，`ServerType` 枚举将从 17 个值扩展到 32 个值，`MainClassMap` 将覆盖所有已知 Main-Class，
`DisambiguatePaperFamily` / `DisambiguateForgeFamily` / 新增的 `DisambiguateBungeeFamily` /
`DisambiguateNukkitFamily` 将通过包前缀消歧覆盖所有派生类。Quilt 拥有独立 Main-Class 无需消歧。

---

## 二、现状分析

### 2.1 当前识别链路（三级兜底）

1. **第一级**：JAR 文件名 glob 匹配（`ServerTypeClassifier.ClassifyByJarName`）
2. **第二级**：配置文件特征推断（`ServerTypeClassifier.InferFromConfigFiles`）
3. **第三级**：JAR Manifest 解包识别（`JarCoreIdentifier.IdentifyAsync`）

### 2.2 当前覆盖（17 个核心，已提交 commit `323abb6`）

| 核心 | Main-Class | 特征类 / 包前缀 |
|---|---|---|
| Vanilla | `net.minecraft.bundler.Main` / `net.minecraft.server.MinecraftServer` | — |
| Bukkit / Spigot | `org.bukkit.craftbukkit.Main` | JAR 名区分 |
| Paper 系 | `io.papermc.paperclip.Paperclip` | 特征类消歧 |
| ├ Folia | 同上 | `io/papermc/paper/threadedregions/RegionizedServer.class` |
| ├ Purpur | 同上 | `org/purpurmc/purpur/PurpurConfig.class` |
| ├ Pufferfish | 同上 | `gg/pufferfish/pufferfish/PufferfishConfig.class` |
| └ Paper | 同上 | 默认 |
| Forge 系 | `cpw.mods.modlauncher.Launcher` / `net.minecraftforge.fml.relauncher.ServerLaunchWrapper` | 包前缀消歧 |
| ├ Mohist | 同上 | `com/mohistmc/` |
| ├ Arclight | 同上 | `io/izzel/arclight/` |
| ├ CatServer | 同上 | `catserver/` |
| ├ NeoForge | 同上 | `net/neoforged/` |
| └ Forge | 同上 | 默认 |
| Fabric | `net.fabricmc.loader.impl.launch.server.FabricServerLauncher` | — |
| BungeeCord | `net.md_5.bungee.Bootstrap` | — |
| Velocity | `com.velocitypowered.proxy.Velocity` | — |
| Sponge | `org.spongepowered.server.launch.VanillaServerLaunch` | — |

### 2.3 当前架构的关键缺口

1. **BungeeCord 系无消歧方法**：Waterfall / FlameCord / HexaCord 共享 `net.md_5.bungee.Bootstrap`，
   当前全部误判为 `BungeeCord`
2. **Paper 系扩展不全**：Airplane / Tuinity / Yatopia / Akarin / Kaiiju / NachoSpigot 共用 Paperclip，
   当前全部误判为 `Paper`
3. **Forge 系扩展不全**：Magma / Banner 未识别，SpongeForge 误判为 Forge
4. **Fabric 系无消歧**：Quilt（独立 Main-Class）未在 MainClassMap 中
5. **独立实现核心缺失**：Nukkit / PowerNukkit / Glowstone 完全无法识别

---

## 三、新增核心特征数据库（基于网络搜索整理）

> 数据来源：PaperMC 官方文档、SpigotMC 论坛、mcbbs、MSList、lagless.gg、astroworldmc.com、
> minecraftservers.fandom.com、QuiltMC 官方 meta API、Nukkit 官方 javadoc、Yatopia GitHub 等。

### 3.1 BungeeCord 系（Main-Class 均为 `net.md_5.bungee.Bootstrap`）

| 核心 | 包前缀特征 | 网络搜索依据 |
|---|---|---|
| Waterfall | `io/github/waterfallmc/` | Waterfall 官方 Maven `io.github.waterfallmc:waterfall-api` |
| FlameCord | `_2lstudios/flamecord/` | FlameCord 由 _2lstudios 开发，反编译确认 |
| HexaCord | `net/md_5/bungee/...hexacord` 或 `fr/itchy/hexacord/` | HexaCord fork，含 PE 支持 |

### 3.2 Fabric 系

| 核心 | Main-Class | 网络搜索依据 |
|---|---|---|
| Quilt | `org.quiltmc.loader.impl.launch.server.QuiltServerLauncher` | QuiltMC 官方 meta API `launcherMeta.mainClass.serverLauncher` |

### 3.3 Paper 系扩展（Main-Class 均为 `io.papermc.paperclip.Paperclip`）

| 核心 | 包前缀特征 | 网络搜索依据 |
|---|---|---|
| Airplane | `gg/technove/` | Yatopia PATCHES.md 列出 Airplane 依赖 |
| Tuinity | `net/tuinity/` | 已合并到 Paper，但旧版仍存在（Spottedleaf 仓库） |
| Yatopia | `org/yatopiamc/` | Yatopia Maven `org.yatopiamc:yatopia-api` |
| Akarin | `io/akarin/` | Akarin-project/Akarin GitHub |
| Kaiiju | `org/kaiiju/` | Folia fork，Kaiiju 仓库 |
| NachoSpigot | `dev/c10dg/` 或 `org/clayburn/` | NachoSpigot fork |

### 3.4 Forge 系扩展（Main-Class 均为 `cpw.mods.modlauncher.Launcher`）

| 核心 | 包前缀特征 | 网络搜索依据 |
|---|---|---|
| Magma | `com/magmafoundation/` | MagmaFoundation 官方 |
| Banner | `com/mohistmc/banner/` | MohistMC 团队新作品（Fabric+Bukkit） |
| SpongeForge | `org/spongepowered/common/` | SpongePowered 官方 |

### 3.5 Bedrock Edition / 独立实现

| 核心 | Main-Class | 网络搜索依据 |
|---|---|---|
| Nukkit | `cn.nukkit.Nukkit` | Nukkit 官方 javadoc `cn.nukkit.Nukkit` |
| PowerNukkit | `cn.nukkit.Nukkit`（fork 保留 Main-Class） | 含 `cn/powernukkitx/` 包前缀 |
| Glowstone | `net.glowstone.GlowServer` | GlowstoneMC 独立 Bukkit 实现 |

---

## 四、实施方案（共 7 步）

### 步骤 1：扩展 ServerType 枚举

**文件**：`/workspace/src/McServerGuard/Constants/ServerConstants.cs`

在现有枚举末尾追加 15 个新值：

```csharp
public enum ServerType
{
    // 现有 17 个值保持不变 ...
    
    // === 第二批新增（基于网络搜索全核心覆盖） ===
    /// <summary>Waterfall 代理端，BungeeCord 的 PaperMC fork（已归档）。</summary>
    Waterfall,
    /// <summary>FlameCord 代理端，BungeeCord 的反机器人分支。</summary>
    FlameCord,
    /// <summary>HexaCord 代理端，支持基岩版协议的 BungeeCord fork。</summary>
    HexaCord,
    /// <summary>Quilt 模组加载器服务端，Fabric 的现代分支。</summary>
    Quilt,
    /// <summary>Airplane 服务端，Paper fork（已停止更新）。</summary>
    Airplane,
    /// <summary>Tuinity 服务端，Paper fork（已合并到 Paper）。</summary>
    Tuinity,
    /// <summary>Yatopia 服务端，Tuinity fork 极限优化。</summary>
    Yatopia,
    /// <summary>Akarin 服务端，Paper fork 多线程优化。</summary>
    Akarin,
    /// <summary>Kaiiju 服务端，Folia fork 优化版。</summary>
    Kaiiju,
    /// <summary>NachoSpigot 服务端，Paper fork。</summary>
    NachoSpigot,
    /// <summary>Magma 混合端，Forge + Bukkit（基于 Thermos）。</summary>
    Magma,
    /// <summary>Banner 混合端，Fabric + Bukkit（Mohist 团队新作）。</summary>
    Banner,
    /// <summary>SpongeForge 服务端，Sponge on Forge 实现。</summary>
    SpongeForge,
    /// <summary>Nukkit 服务端，基岩版 Java 实现。</summary>
    Nukkit,
    /// <summary>PowerNukkit 服务端，Nukkit fork。</summary>
    PowerNukkit,
    /// <summary>Glowstone 服务端，独立 Bukkit API 实现。</summary>
    Glowstone,
}
```

### 步骤 2：扩展 ServerConstants 各 JAR 模式 / 关键词 / 指示文件

**文件**：`/workspace/src/McServerGuard/Constants/ServerConstants.cs`

新增 15 组 `*JarPatterns` 字段，扩展 `ServerJarKeywords` 数组，扩展 `TypeIndicatorFiles` 字典：

```csharp
// 新增 JAR 模式（追加到现有字段后）
public static readonly string[] WaterfallJarPatterns = ["waterfall-*.jar", "waterfall.jar"];
public static readonly string[] FlameCordJarPatterns = ["flamecord-*.jar", "flamecord.jar"];
public static readonly string[] HexaCordJarPatterns = ["hexacord-*.jar", "hexacord.jar"];
public static readonly string[] QuiltJarPatterns = ["quilt-server-launch.jar", "quilt-server.jar"];
public static readonly string[] AirplaneJarPatterns = ["airplane-*.jar", "airplane.jar"];
public static readonly string[] TuinityJarPatterns = ["tuinity-*.jar", "tuinity.jar"];
public static readonly string[] YatopiaJarPatterns = ["yatopia-*.jar", "yatopia.jar"];
public static readonly string[] AkarinJarPatterns = ["akarin-*.jar", "akarin.jar"];
public static readonly string[] KaiijuJarPatterns = ["kaiiju-*.jar", "kaiiju.jar"];
public static readonly string[] NachoSpigotJarPatterns = ["nacho-*.jar", "nachospigot-*.jar"];
public static readonly string[] MagmaJarPatterns = ["magma-*.jar", "magma.jar"];
public static readonly string[] BannerJarPatterns = ["banner-*.jar", "banner.jar"];
public static readonly string[] SpongeForgeJarPatterns = ["spongeforge-*.jar"];
public static readonly string[] NukkitJarPatterns = ["nukkit-*.jar", "nukkit.jar"];
public static readonly string[] PowerNukkitJarPatterns = ["powernukkit-*.jar", "powernukkit.jar"];
public static readonly string[] GlowstoneJarPatterns = ["glowstone-*.jar", "glowstone.jar"];

// ServerJarKeywords 追加
public static readonly string[] ServerJarKeywords = [
    // 现有 18 个保持不变 ...
    "waterfall", "flamecord", "hexacord", "quilt", "airplane", "tuinity", "yatopia",
    "akarin", "kaiiju", "nacho", "nachospigot", "magma", "banner", "spongeforge",
    "nukkit", "powernukkit", "glowstone"
];

// TypeIndicatorFiles 追加
// Waterfall / FlameCord / HexaCord 共享 BungeeCord 的 config.yml，需靠 Manifest 区分，无独有指示文件
// Quilt 共享 fabric-server-launch.properties，无独有指示文件
// 其他核心的独有指示文件：
// Airplane: airplane.yml
// Tuinity: tuinity.yml
// Yatopia: yatopia.yml
// Akarin: akarin.yml
// Kaiiju: kaiiju.yml (待验证)
// NachoSpigot: nacho.yml
// Magma: magma.conf, plugins/Magma/
// Banner: banner.yml
// SpongeForge: config/sponge/ (与 Sponge 共享，需 Manifest 区分)
// Nukkit: nukkit.yml
// PowerNukkit: powernukkit.yml
// Glowstone: config/glowstone/
```

### 步骤 3：扩展 JarCoreIdentifier — MainClassMap 与消歧方法

**文件**：`/workspace/src/McServerGuard/Services/ServerDetection/JarCoreIdentifier.cs`

#### 3.1 MainClassMap 追加条目

```csharp
private static readonly Dictionary<string, ServerType> MainClassMap = new(StringComparer.OrdinalIgnoreCase)
{
    // === 现有 12 个条目保持不变 ===
    // (Vanilla×2, Spigot, Paper×2, Forge×2, Fabric×2, BungeeCord, Velocity, Sponge)
    
    // === 新增 ===
    // Quilt（独立 Main-Class，与 Fabric 不同）
    ["org.quiltmc.loader.impl.launch.server.QuiltServerLauncher"] = ServerType.Quilt,
    // Nukkit / PowerNukkit（共享 Main-Class，需特征类区分）
    ["cn.nukkit.Nukkit"] = ServerType.Nukkit,
    // Glowstone（独立 Bukkit API 实现）
    ["net.glowstone.GlowServer"] = ServerType.Glowstone,
};
```

#### 3.2 DisambiguatePaperFamily 扩展

在现有逻辑（Folia > Purpur > Pufferfish > Paper）前插入派生类优先检测：

```csharp
private static ServerType DisambiguatePaperFamily(ZipArchive jar)
{
    // 派生类优先检测（Folia 系）
    if (jar.GetEntry("io/papermc/paper/threadedregions/RegionizedServer.class") is not null)
    {
        // Folia 派生：Kaiiju
        if (HasEntryPrefix(jar, "org/kaiiju/"))
            return ServerType.Kaiiju;
        return ServerType.Folia;
    }
    
    // 新增派生类（包前缀检测，按 fork 层级优先）
    if (HasEntryPrefix(jar, "org/yatopiamc/"))
        return ServerType.Yatopia;
    if (HasEntryPrefix(jar, "io/akarin/"))
        return ServerType.Akarin;
    if (HasEntryPrefix(jar, "gg/technove/"))
        return ServerType.Airplane;
    if (HasEntryPrefix(jar, "net/tuinity/"))
        return ServerType.Tuinity;
    if (HasEntryPrefix(jar, "dev/c10dg/") || HasEntryPrefix(jar, "org/clayburn/"))
        return ServerType.NachoSpigot;
    
    // 现有逻辑
    if (jar.GetEntry("org/purpurmc/purpur/PurpurConfig.class") is not null)
        return ServerType.Purpur;
    if (jar.GetEntry("gg/pufferfish/pufferfish/PufferfishConfig.class") is not null)
        return ServerType.Pufferfish;
    return ServerType.Paper;
}
```

#### 3.3 DisambiguateForgeFamily 扩展

在现有逻辑（Mohist > Arclight > CatServer > NeoForge > Forge）后追加：

```csharp
private static ServerType DisambiguateForgeFamily(ZipArchive jar)
{
    // 现有逻辑保持不变
    if (HasEntryPrefix(jar, "com/mohistmc/"))
        return ServerType.Mohist;
    if (HasEntryPrefix(jar, "io/izzel/arclight/"))
        return ServerType.Arclight;
    if (HasEntryPrefix(jar, "catserver/"))
        return ServerType.CatServer;
    
    // 新增：Magma（基于 Thermos 的混合端）
    if (HasEntryPrefix(jar, "com/magmafoundation/"))
        return ServerType.Magma;
    
    // 新增：SpongeForge（Sponge on Forge）
    if (HasEntryPrefix(jar, "org/spongepowered/common/"))
        return ServerType.SpongeForge;
    
    if (HasEntryPrefix(jar, "net/neoforged/"))
        return ServerType.NeoForge;
    return ServerType.Forge;
}
```

#### 3.3a Banner 的特殊处理

Banner 是 Mohist 团队基于 Fabric + Bukkit 的混合端。其 Main-Class 可能与 Fabric 共享
（`net.fabricmc.loader.impl.launch.server.FabricServerLauncher`），需通过包前缀消歧。
在 `IdentifyCore` 中，当 `baseType == ServerType.Fabric` 时调用新增的包前缀检查：

```csharp
// 在 IdentifyCore 中追加（baseType == Fabric 分支）
if (baseType == ServerType.Fabric)
{
    // Banner 混合端（Mohist 团队新作，Fabric + Bukkit）
    if (HasEntryPrefix(jar, "com/mohistmc/banner/"))
        return ServerType.Banner;
    return ServerType.Fabric;
}
```

#### 3.4 新增 DisambiguateBungeeFamily

处理 BungeeCord 系消歧（Waterfall / FlameCord / HexaCord / BungeeCord）：

```csharp
private static ServerType DisambiguateBungeeFamily(ZipArchive jar)
{
    // PaperMC fork
    if (HasEntryPrefix(jar, "io/github/waterfallmc/"))
        return ServerType.Waterfall;
    // 反机器人分支
    if (HasEntryPrefix(jar, "_2lstudios/flamecord/"))
        return ServerType.FlameCord;
    // 基岩版协议支持
    if (HasEntryPrefix(jar, "fr/itchy/hexacord/") || HasEntryPrefix(jar, "net/md_5/bungee/hexacord/"))
        return ServerType.HexaCord;
    return ServerType.BungeeCord;
}
```

#### 3.5 新增 DisambiguateNukkitFamily

处理 Nukkit / PowerNukkit 共享 Main-Class 的消歧：

```csharp
private static ServerType DisambiguateNukkitFamily(ZipArchive jar)
{
    if (HasEntryPrefix(jar, "cn/powernukkitx/") || HasEntryPrefix(jar, "cn/powernukkit/"))
        return ServerType.PowerNukkit;
    return ServerType.Nukkit;
}
```

#### 3.6 IdentifyCore 调用链更新

在 `IdentifyCore` 中追加新分支：

```csharp
if (baseType == ServerType.Paper)
    return DisambiguatePaperFamily(jar);
if (baseType == ServerType.Forge)
    return DisambiguateForgeFamily(jar);
// 新增
if (baseType == ServerType.BungeeCord)
    return DisambiguateBungeeFamily(jar);
if (baseType == ServerType.Nukkit)
    return DisambiguateNukkitFamily(jar);
if (baseType == ServerType.Fabric)
{
    // Banner 混合端（Fabric + Bukkit）
    if (HasEntryPrefix(jar, "com/mohistmc/banner/"))
        return ServerType.Banner;
    return ServerType.Fabric;
}
return baseType;
```

### 步骤 4：扩展 ServerTypeClassifier

**文件**：`/workspace/src/McServerGuard/Services/ServerDetection/ServerTypeClassifier.cs`

#### 4.1 JarNameTypeMap 追加条目

按派生类优先原则追加（在现有相应位置插入）：

```csharp
private static readonly (string[] Patterns, ServerType Type)[] JarNameTypeMap =
[
    (ServerConstants.VanillaJarPatterns, ServerType.Vanilla),
    (ServerConstants.BukkitJarPatterns, ServerType.Bukkit),
    (ServerConstants.SpigotJarPatterns, ServerType.Spigot),
    // Paper 系派生类优先（Folia > Kaiiju > Purpur > Pufferfish > Yatopia > Airplane > Tuinity > Akarin > NachoSpigot > Paper）
    (ServerConstants.FoliaJarPatterns, ServerType.Folia),
    (ServerConstants.KaiijuJarPatterns, ServerType.Kaiiju),
    (ServerConstants.PurpurJarPatterns, ServerType.Purpur),
    (ServerConstants.PufferfishJarPatterns, ServerType.Pufferfish),
    (ServerConstants.YatopiaJarPatterns, ServerType.Yatopia),
    (ServerConstants.AirplaneJarPatterns, ServerType.Airplane),
    (ServerConstants.TuinityJarPatterns, ServerType.Tuinity),
    (ServerConstants.AkarinJarPatterns, ServerType.Akarin),
    (ServerConstants.NachoSpigotJarPatterns, ServerType.NachoSpigot),
    (ServerConstants.PaperJarPatterns, ServerType.Paper),
    // Forge 系派生类优先（Mohist > Arclight > CatServer > Magma > SpongeForge > NeoForge > Forge）
    (ServerConstants.MohistJarPatterns, ServerType.Mohist),
    (ServerConstants.ArclightJarPatterns, ServerType.Arclight),
    (ServerConstants.CatServerJarPatterns, ServerType.CatServer),
    (ServerConstants.MagmaJarPatterns, ServerType.Magma),
    (ServerConstants.SpongeForgeJarPatterns, ServerType.SpongeForge),
    (ServerConstants.NeoForgeJarPatterns, ServerType.NeoForge),
    (ServerConstants.ForgeJarPatterns, ServerType.Forge),
    // Fabric 系
    (ServerConstants.FabricJarPatterns, ServerType.Fabric),
    (ServerConstants.QuiltJarPatterns, ServerType.Quilt),
    // Banner（基于 Fabric+Bukkit）
    (ServerConstants.BannerJarPatterns, ServerType.Banner),
    // 代理端派生类优先（Waterfall > FlameCord > HexaCord > BungeeCord）
    (ServerConstants.WaterfallJarPatterns, ServerType.Waterfall),
    (ServerConstants.FlameCordJarPatterns, ServerType.FlameCord),
    (ServerConstants.HexaCordJarPatterns, ServerType.HexaCord),
    (ServerConstants.BungeeCordJarPatterns, ServerType.BungeeCord),
    (ServerConstants.VelocityJarPatterns, ServerType.Velocity),
    // Sponge 系
    (ServerConstants.SpongeJarPatterns, ServerType.Sponge),
    // 基岩版 / 独立实现
    (ServerConstants.NukkitJarPatterns, ServerType.Nukkit),
    (ServerConstants.PowerNukkitJarPatterns, ServerType.PowerNukkit),
    (ServerConstants.GlowstoneJarPatterns, ServerType.Glowstone),
];
```

#### 4.2 InferFromConfigFiles uniqueChecks 追加

```csharp
var uniqueChecks = new[]
{
    // === Paper 系派生类优先 ===
    (ServerType.Folia,        new[] { "config/folia-global.yml" }),
    (ServerType.Kaiiju,       new[] { "kaiiju.yml", "config/kaiiju.yml" }),
    (ServerType.Purpur,       new[] { "purpur.yml", "config/purpur.yml" }),
    (ServerType.Pufferfish,   new[] { "pufferfish.yml" }),
    (ServerType.Yatopia,      new[] { "yatopia.yml" }),
    (ServerType.Airplane,     new[] { "airplane.yml" }),
    (ServerType.Tuinity,      new[] { "tuinity.yml" }),
    (ServerType.Akarin,       new[] { "akarin.yml" }),
    (ServerType.NachoSpigot,  new[] { "nacho.yml" }),
    (ServerType.Paper,        new[] { "config/paper-global.yml" }),
    // === Forge 系派生类优先 ===
    (ServerType.Mohist,       new[] { "mohist-config.yml" }),
    (ServerType.Arclight,     new[] { "arclight.yml" }),
    (ServerType.CatServer,    new[] { "catserver.yml" }),
    (ServerType.Magma,        new[] { "magma.conf", "plugins/Magma/" }),
    (ServerType.SpongeForge,  new[] { "config/sponge/" }),
    (ServerType.NeoForge,     new[] { "neoforge.yml", "config/neoforge/" }),
    (ServerType.Forge,        new[] { "forge-server.toml" }),
    // === Fabric 系 ===
    (ServerType.Fabric,       new[] { "fabric-server-launch.properties", ".fabric/" }),
    (ServerType.Banner,       new[] { "banner.yml" }),
    // === Spigot / Bukkit ===
    (ServerType.Spigot,       new[] { "spigot.yml" }),
    (ServerType.Bukkit,       new[] { "bukkit.yml" }),
    // === 代理端 ===
    (ServerType.Waterfall,    new[] { "waterfall.yml" }),
    (ServerType.FlameCord,    new[] { "flamecord.yml" }),
    (ServerType.HexaCord,     new[] { "hexacord.yml" }),
    (ServerType.BungeeCord,   new[] { "config.yml" }),
    (ServerType.Velocity,     new[] { "velocity.toml" }),
    // === Sponge 系 ===
    (ServerType.Sponge,       new[] { "global.conf" }),
    // === 基岩版 / 独立实现 ===
    (ServerType.Nukkit,       new[] { "nukkit.yml" }),
    (ServerType.PowerNukkit,  new[] { "powernukkit.yml" }),
    (ServerType.Glowstone,    new[] { "config/glowstone/" }),
};
```

### 步骤 5：ServerDetector 触发条件扩展

**文件**：`/workspace/src/McServerGuard/Services/ServerDetection/ServerDetector.cs`

修改第三级兜底触发条件，将"仅 Unknown/Vanilla"扩展为覆盖所有基类（因为派生类需 Manifest 区分）：

```csharp
// 当前代码（L251-L265）：
if (serverType == ServerType.Unknown || serverType == ServerType.Vanilla)
{
    // ...
}

// 修改为：
if (serverType == ServerType.Unknown 
    || serverType == ServerType.Vanilla
    || serverType == ServerType.Spigot    // CraftBukkit 共享 Main-Class，需 Manifest 区分混合端
    || serverType == ServerType.Bukkit
    || serverType == ServerType.Paper     // Paper 系扩展（Airplane/Tuinity/Yatopia 等）需 Manifest 区分
    || serverType == ServerType.Forge     // Forge 系扩展（Magma/SpongeForge）需 Manifest 区分
    || serverType == ServerType.Fabric    // Fabric 系扩展（Banner）需 Manifest 区分
    || serverType == ServerType.BungeeCord) // BungeeCord 系扩展（Waterfall/FlameCord/HexaCord）需 Manifest 区分
{
    if (!string.IsNullOrEmpty(parsed.JarFilePath) && File.Exists(parsed.JarFilePath))
    {
        var manifestType = await _jarCoreIdentifier.IdentifyAsync(parsed.JarFilePath);
        if (manifestType != ServerType.Unknown && manifestType != serverType)
        {
            Log.Information("🔬 JAR Manifest 识别为核心类型: {Type}（覆盖原 {Old}）", manifestType, serverType);
            serverType = manifestType;
        }
    }
}
```

**注意**：触发条件扩展意味着 JAR 解包更频繁，但因 5 分钟 TTL 缓存，实际开销可控。
Vanilla 保留在触发条件中，因为 JAR 名 `server.jar` 可能是 Paper 等核心被重命名，需 Manifest 纠正。

### 步骤 6：ServerType 扩展的下游影响审计

**基于代码探索的结论**（Phase 1 已确认）：

1. **`/workspace/src/McServerGuard/Views/ServerDetectionPage.xaml`** — **无需修改**。
   探索确认该页面仅有 3 个 DataTrigger，全部绑定的是 `IsPortOpen` / `PortConflict` / `IsMouseOver`，
   没有任何一个绑定到 `ServerType`。全项目 XAML 文件中没有任何 `ServerType` 引用。
2. **本地化资源文件** — **无需修改**。探索确认项目完全没有 `.resx`/`.resw` 文件，
   没有任何本地化基础设施。所有 UI 文本均为硬编码中文字符串，ServerType 在 UI 上没有对应的可读名称展示。
3. **`ProcessScanner.cs`** — **无需修改**。Java 进程命令行过滤逻辑不依赖 ServerType 枚举值。
4. **`ServerInstance.cs`** — **无需修改**。ServerType 属性作为枚举存储，无 toString/Description 特性扩展。

**执行阶段仍需通过 Grep 全项目搜索 `ServerType.` 引用**，确认是否有遗漏的 switch 表达式或
显式枚举遍历需要补充新值（特别是 `switch` 语句若无 `default` 分支可能因新增枚举值导致编译警告）。
若 `TreatWarningsAsErrors=true` 触发 CS8509（switch 未覆盖所有枚举值），需补充 `default` 分支或
新增 case。

### 步骤 7：编译验证 + 提交到 main

```bash
dotnet build -c Debug -r win-x64 --self-contained
```

确认 0 错误 0 警告（项目 `TreatWarningsAsErrors=true`）后，提交到本地 main 分支：

```bash
git add --renormalize .
git commit -m "feat: 扩展 JAR Manifest 识别，覆盖全部 32 个已知服务器核心

基于网络搜索全面扩展核心识别范围，新增 15 个核心类型：
- BungeeCord 系：Waterfall, FlameCord, HexaCord
- Fabric 系：Quilt
- Paper 系：Airplane, Tuinity, Yatopia, Akarin, Kaiiju, NachoSpigot
- Forge 系：Magma, Banner, SpongeForge
- 独立实现：Nukkit, PowerNukkit, Glowstone

新增 DisambiguateBungeeFamily / DisambiguateNukkitFamily 消歧方法
扩展 DisambiguatePaperFamily / DisambiguateForgeFamily 派生类优先级
扩展第三级兜底触发条件至 Spigot/Bukkit/Paper/Forge/BungeeCord"
```

---

## 五、假设与决策

### 5.1 关键决策

1. **覆盖范围决策**：覆盖所有 Java 版服务端核心（含已停止更新的 Tuinity/Airplane/Yatopia 等），
   不覆盖纯 PHP/C++/Rust 实现（PocketMine-MP、Bedrock Dedicated Server、Pumpkin）
2. **枚举扩展决策**：每个核心单独一个枚举值，便于 UI 展示和未来扩展
3. **消歧优先级决策**：派生类优先于基类检测，避免误判（如 Folia 优先于 Paper）
4. **触发条件扩展决策**：从仅 Unknown/Vanilla 扩展到 Paper/Forge/BungeeCord/Spigot/Bukkit，
   因为这些基类需要 Manifest 区分派生类

### 5.2 已知假设

1. **包前缀特征假设**：部分核心（如 FlameCord 的 `_2lstudios/flamecord/`）基于反编译推断，
   可能在实际 JAR 中前缀略有差异。实施时应保留兜底（不匹配时返回基类）
2. **Magma 指示文件**：基于网络搜索 `magma.conf` 和 `plugins/Magma/` 目录，需验证
3. **Kaiiju 指示文件**：网络搜索未确认，使用 `kaiiju.yml` 推断值

### 5.3 兼容性保证

1. 所有现有 17 个核心识别行为保持不变
2. 新增逻辑仅扩展覆盖范围，不改变现有判定结果
3. ServerType 枚举追加在末尾，不破坏现有序列化

---

## 六、验证步骤

### 6.1 编译验证

```bash
dotnet build -c Debug -r win-x64 --self-contained
```

预期：0 错误 0 警告（`TreatWarningsAsErrors=true`）

### 6.2 静态验证清单

- [ ] `ServerType` 枚举共 32 个值（17 现有 + 15 新增）
- [ ] `MainClassMap` 共 15 个条目（12 现有 + 3 新增：Quilt / Nukkit / Glowstone）
- [ ] `JarNameTypeMap` 共 31 个条目（16 现有 + 15 新增）
- [ ] `TypeIndicatorFiles` 字典覆盖所有 32 个核心（除 Vanilla 外）
- [ ] `ServerJarKeywords` 包含全部关键词
- [ ] `ServerDetector.BuildServerInstanceAsync` 触发条件已扩展
- [ ] `ServerDetectionPage.xaml` 无需修改（无 ServerType DataTrigger）
- [ ] 无 switch 表达式因新增枚举值触发 CS8509 警告

### 6.3 运行时验证（用户侧）

无法在 Linux 沙箱中运行 WPF 应用，需用户在 Windows 上验证：
- 检测 Paper 服务器 → 显示 Paper（不变）
- 检测 Purpur 服务器 → 显示 Purpur（不变）
- 检测 Waterfall 代理 → 显示 Waterfall（新增）
- 检测 Quilt 模组端 → 显示 Quilt（新增）

---

## 七、参考来源

1. [MSList - Minecraft 服务端核心列表](https://d.mmeiblog.cn/)
2. [Minecraft 常用服务端核心介绍 - mcbbs](https://www.mcbbs.app/threads/minecraft-chang-yong-fu-wu-duan-he-xin-jie-shao-geng-xin-yu-2025-04-20.362/)
3. [Java Edition server software - Fandom](https://minecraftservers.fandom.com/wiki/Java_Edition_server_software)
4. [All Minecraft Server Types Explained - astroworldmc](https://guide.astroworldmc.com/all-minecraft-server-types-explained)
5. [Server Software - lagless.gg](https://lagless.gg/docs/minecraft/server-software)
6. [PaperMC Downloads - Paper/Folia/Velocity/Waterfall](https://papermc.io/downloads)
7. [QuiltMC Loader Meta API](https://meta.quiltmc.org/v3/versions/loader/1.14.4/0.27.0)
8. [Nukkit Javadoc - cn.nukkit.Nukkit](https://javadoc.io/static/cn.powernukkitx/powernukkitx/1.19.50-r3/cn/nukkit/Nukkit.html)
9. [Waterfall Javadoc - io.github.waterfallmc](https://jd.papermc.io/waterfall/1.18/overview-tree.html)
10. [Yatopia GitHub - org.yatopiamc:yatopia-api](https://github.com/YatopiaMC/Yatopia)
