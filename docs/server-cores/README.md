# Minecraft 服务器核心配置文件中文手册

> 本手册为 MSMC (McServerGuard) 项目收录的全部 35 种 Minecraft 服务器核心的原生配置文件提供全面的中文翻译。
> 目标：让从未开过服的小白也能看懂并熟练使用这些配置。
> 翻译规范：小白友好、枚举值翻译、键名不翻译、值类型标注、取值范围明确、重启标注、说明详尽。

## 核心总览

共收录 **35 种**服务器核心，覆盖原版、插件端、模组端、代理端、混合端、基岩版实现等全部主流类型。
注册表共注册 **1161 个配置描述符**，覆盖 **37 个配置文件**。

---

## 一、原版与基础插件端（4 种）

| # | 核心 | 配置文件 | 文档 |
|---|---|---|---|
| 01 | **Vanilla（原版）** | `server.properties` | [01-vanilla.md](01-vanilla.md) |
| 02 | **Bukkit** | `bukkit.yml` + `server.properties` + `permissions.yml` + `commands.yml` + `help.yml` | [02-bukkit.md](02-bukkit.md) |
| 03 | **Spigot** | `spigot.yml` + `bukkit.yml` + `server.properties` | [03-spigot.md](03-spigot.md) |
| 04 | **Paper** | `config/paper-global.yml` + `config/paper-world-defaults.yml` + `spigot.yml` + `bukkit.yml` + `server.properties` | [04-paper.md](04-paper.md) |

---

## 二、Paper 系派生核心（13 种）

### 活跃核心（9 种）

| # | 核心 | 配置文件 | 简介 | 文档 |
|---|---|---|---|---|
| 05 | **Folia** | `config/paper-global.yml`（ThreadedRegions 节） | Paper 多线程区域化分支 | [05-folia.md](05-folia.md) |
| 06 | **Purpur** | `purpur.yml` | 极致可配置性，最大的单文件配置 | [06-purpur.md](06-purpur.md) |
| 07 | **Pufferfish** | `pufferfish.yml` | 异步实体追踪、DEAR 优化 | [07-pufferfish.md](07-pufferfish.md) |
| 08 | **Leaves** | `leaves.yml` | 协议支持、原版特性修复 | [08-leaves.md](08-leaves.md) |
| 09 | **Leaf** | `leaf.yml` + `config/leaf-global.yml` | 异步路径查找、多线程实体追踪 | [09-leaf.md](09-leaf.md) |
| 10 | **Luminol** | `luminol_global_config.toml` | 极速优化、Tpsbar、原版特性修复 | [10-luminol.md](10-luminol.md) |
| 11 | **Kaiiju** | `kaiiju.yml` | Folia fork 优化版 | [11-kaiiju.md](11-kaiiju.md) |
| 12 | **NachoSpigot** | `nacho.yml` | Paper fork，性能优化 | [12-nachospigot.md](12-nachospigot.md) |
| 13 | **USpigot** | `uspigot.yml` | 国内衍生 Spigot 分支 | [13-uspigot.md](13-uspigot.md) |

### 已停更核心（4 种，仍完整提供翻译）

| # | 核心 | 配置文件 | 简介 | 文档 |
|---|---|---|---|---|
| 14 | ⚠️ **Yatopia** | `yatopia.yml` | Tuinity fork 极限优化（已停更） | [14-yatopia.md](14-yatopia.md) |
| 15 | ⚠️ **Airplane** | `airplane.yml` | Paper fork（已停更，Pufferfish 前身） | [15-airplane.md](15-airplane.md) |
| 16 | ⚠️ **Tuinity** | `tuinity.yml` | Paper fork（已合并到 Paper） | [16-tuinity.md](16-tuinity.md) |
| 17 | ⚠️ **Akarin** | `akarin.yml` | Paper fork 多线程优化（已停更） | [17-akarin.md](17-akarin.md) |

---

## 三、模组端（4 种）

| # | 核心 | 配置文件 | 简介 | 文档 |
|---|---|---|---|---|
| 18 | **Forge** | `forge-server.toml` | 经典 Mod 加载器 | [18-forge.md](18-forge.md) |
| 19 | **NeoForge** | `neoforge-server.toml` + `neoforge-common.toml` | Forge 现代分支 | [19-neoforge.md](19-neoforge.md) |
| 20 | **Fabric** | `fabric-server-launcher.properties` | 轻量级 Mod 加载器 | [20-fabric.md](20-fabric.md) |
| 21 | **Quilt** | `quilt-server-launcher.properties` | Fabric 现代分支 | [21-quilt.md](21-quilt.md) |

---

## 四、代理端（5 种）

| # | 核心 | 配置文件 | 简介 | 文档 |
|---|---|---|---|---|
| 22 | **BungeeCord** | `config.yml` | 经典 Minecraft 代理 | [22-bungeecord.md](22-bungeecord.md) |
| 23 | **Velocity** | `velocity.toml` | 现代高性能代理 | [23-velocity.md](23-velocity.md) |
| 24 | ⚠️ **Waterfall** | `waterfall.yml` | BungeeCord 的 PaperMC fork（已归档） | [24-waterfall.md](24-waterfall.md) |
| 25 | **FlameCord** | `flamecord.yml` | 反机器人 BungeeCord fork | [25-flamecord.md](25-flamecord.md) |
| 26 | **HexaCord** | `hexacord.yml` | 支持基岩版协议的 BungeeCord fork | [26-hexacord.md](26-hexacord.md) |

---

## 五、混合端（5 种）

| # | 核心 | 配置文件 | 简介 | 文档 |
|---|---|---|---|---|
| 27 | **Mohist** | `mohist-config.yml` | Forge + Bukkit 混合 | [27-mohist.md](27-mohist.md) |
| 28 | **Arclight** | `arclight.conf` | Forge/NeoForge/Fabric + Bukkit | [28-arclight.md](28-arclight.md) |
| 29 | **CatServer** | `catserver.yml` | Forge + Bukkit | [29-catserver.md](29-catserver.md) |
| 30 | **Magma** | `magma.yml` | 基于 Thermos，Forge + Bukkit | [30-magma.md](30-magma.md) |
| 31 | **Banner** | `banner.yml` | Fabric + Bukkit（Mohist 团队新作） | [31-banner.md](31-banner.md) |

---

## 六、基岩版 / 独立实现 / Sponge（5 种）

| # | 核心 | 配置文件 | 简介 | 文档 |
|---|---|---|---|---|
| 32 | **Sponge** | `config/sponge/global.conf` | 独立插件 API（HOCON 格式） | [32-sponge.md](32-sponge.md) |
| 33 | **SpongeForge** | `config/sponge/spongeforge-global.conf` | Sponge on Forge 实现 | [33-spongeforge.md](33-spongeforge.md) |
| 34 | **Nukkit** | `nukkit.yml` + `nukkit-server.properties` | 基岩版 Java 实现 | [34-nukkit.md](34-nukkit.md) |
| 35 | **PowerNukkit** | `powernukkit.yml` + `powernukkit-server.properties` | Nukkit fork | [35-powernukkit.md](35-powernukkit.md) |
| 36 | **Glowstone** | `config/glowstone/glowstone.yml` | 独立 Bukkit API 实现 | [36-glowstone.md](36-glowstone.md) |

---

## 继承关系图

```
Vanilla (server.properties)
├── Bukkit (bukkit.yml)
│   └── Spigot (spigot.yml)
│       └── Paper (paper-global.yml, paper-world-defaults.yml)
│           ├── Purpur (purpur.yml)
│           ├── Pufferfish (pufferfish.yml)
│           │   └── Purpur
│           ├── Leaves (leaves.yml)
│           │   ├── Leaf (leaf.yml)
│           │   └── Luminol (luminol_global_config.toml)
│           ├── Folia (paper-global.yml ThreadedRegions 节)
│           │   └── Kaiiju (kaiiju.yml)
│           ├── NachoSpigot (nacho.yml)
│           ├── USpigot (uspigot.yml)
│           ├── Airplane (airplane.yml) ⚠️ 已停更
│           ├── Tuinity (tuinity.yml) ⚠️ 已合并
│           ├── Yatopia (yatopia.yml) ⚠️ 已停更
│           └── Akarin (akarin.yml) ⚠️ 已停更

BungeeCord (config.yml)
├── Waterfall (waterfall.yml) ⚠️ 已归档
├── FlameCord (flamecord.yml)
└── HexaCord (hexacord.yml)

Velocity (velocity.toml) — 独立实现

Forge (forge-server.toml)
└── NeoForge (neoforge-server.toml)

Fabric (fabric-server-launcher.properties)
└── Quilt (quilt-server-launcher.properties)

Mohist (mohist-config.yml) — Forge + Bukkit
├── Arclight (arclight.conf) — Forge/NeoForge/Fabric + Bukkit
├── CatServer (catserver.yml) — Forge + Bukkit
├── Magma (magma.yml) — Forge + Bukkit
└── Banner (banner.yml) — Fabric + Bukkit

Sponge (global.conf) — HOCON 格式
└── SpongeForge (spongeforge-global.conf)

Nukkit (nukkit.yml) — 基岩版
└── PowerNukkit (powernukkit.yml)

Glowstone (glowstone.yml) — 独立 Bukkit API
```

---

## 翻译规范

1. **小白友好**：翻译要让从未开过服的人也能看懂熟用
2. **枚举值翻译**：如 `gamemode` 的值 `survival` → `生存`、`creative` → `创造`
3. **键名不翻译**：保持英文键名不变（代码需要），只翻译显示名和说明
4. **值类型标注**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举 / `list` 列表 / 路径
5. **取值范围明确**：数值标 min-max，枚举列出所有可选值并翻译
6. **重启标注**：✅ 修改后需重启 / 🔄 支持热重载
7. **说明详尽**：解释配置做什么、为什么这么设、改了会有什么影响、有什么坑

## 代码集成

所有翻译已集成到 [ConfigDescriptorRegistry.cs](../../src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs)，
共注册 **1161 个配置描述符**，覆盖 **37 个配置文件**。
配置编辑器会根据配置文件名自动匹配并显示中文翻译和校验规则。

## 数据来源

- 各核心官方 GitHub 仓库源码与默认配置文件
- 官方文档（PaperMC、LeavesMC、Purpur、Velocity、Sponge、NeoForge、Fabric 等）
- 社区资源（Minecraft Wiki、SpigotMC 论坛、MineBBS、CSDN 等）
- 对于文档不全的核心，从源码中提取配置项定义并合理推断
