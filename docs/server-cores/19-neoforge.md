# NeoForge 服务器配置文件中文手册

> NeoForge 是 Minecraft Forge 的社区驱动分支，自 1.20.2 起作为现代模组加载器延续 Forge 生态。
> 继承关系：Vanilla → Forge → NeoForge（与 Forge 平行，不互相兼容 JAR）
> 官方 GitHub：https://github.com/neoforged/NeoForge
> 官方文档：https://docs.neoforged.net/
> 数据来源：NeoForge 1.21.x 源码 `NeoForgeConfig.java` / 官方文档 / 社区故障排查指南
> 适用版本基准：NeoForge 1.20.2 ~ 1.21.x

## 核心简介

NeoForge 自 1.20.2 从 Forge 分叉而来，沿用了 Forge 的 TOML 配置体系，但做了几项重要变更：

1. **配置文件位置变更**：自 NeoForge 1.20.4 起，`neoforge-server.toml` 不再位于世界目录下的 `serverconfig/`，而是统一放在**服务器根目录**的 `config/` 下，即 `config/neoforge-server.toml`。这一点与 Forge 不同，新手尤其要注意。
2. **默认权限处理器命名**：从 `forge:default_handler` 改为 `neoforge:default_handler`。
3. **新增通用配置项**：`neoforge-common.toml` 增加了多项开发者调试选项（标签翻译警告、属性高级提示等），开服者通常保持默认即可。
4. **ModID 命名空间**：从 `forge` 改为 `neoforge`，但标签命名空间暂时仍保留 `forge` 以兼容旧模组。

> ⚠️ NeoForge 与 Forge 不兼容，无法混用对方的模组 JAR。请确认你的模组明确标注支持 NeoForge 后再放入 `mods/` 目录。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| eula.txt | 文本 | Vanilla 继承 | 必须 `eula=true` 才能启动 |
| user_jvm_args.txt | 文本 | NeoForge 安装器生成 | JVM 参数（内存分配、GC 等） |
| run.sh / run.bat | 脚本 | NeoForge 安装器生成 | 启动脚本 |
| **config/neoforge-server.toml** | TOML | **NeoForge 专属** | **NeoForge 服务端核心配置（本文档重点）** |
| config/neoforge-common.toml | TOML | NeoForge 专属 | NeoForge 通用配置（开发调试为主） |
| config/neoforge-client.toml | TOML | NeoForge 专属 | NeoForge 客户端配置（开服无需关注） |
| mods/ | 目录 | NeoForge 专属 | 存放模组 JAR 文件 |

> 说明：NeoForge 与 Forge 一样是模组加载器，不是完整的服务端实现。除上述 NeoForge 自身配置外，其余运行规则沿用原版 `server.properties`，请参阅 Vanilla 手册。

## config/neoforge-server.toml（NeoForge 服务端核心配置）

`neoforge-server.toml` 由 NeoForge 在首次启动时自动生成于 `config/` 目录下。所有配置项位于 `[server]` 节。配置项采用 TOML 语法：键名在 `[server]` 节下，等号右侧为值。

> 📍 **位置提醒**：NeoForge 1.20.4+ 的服务端配置文件路径是 `config/neoforge-server.toml`（**服务器根目录的 config 文件夹下**）。这与 Forge 1.20.x 及更早版本将文件放在 `<世界>/serverconfig/` 不同。如果你刚从 Forge 迁移过来，特别注意此差异。

### 阅读约定

- **键名**：保持原样不翻译，TOML 路径以 `server.键名` 形式表达（实际文件中等价于 `[server]` 节下的 `键名 = 值`）。
- **值类型**：`bool` 布尔 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示必须重启世界（关闭并重新加载存档）才能生效；🔄 表示支持运行时热重载。
- **worldRestart**：标记为 `worldRestart()` 的选项，修改后需要重启世界（对单一主世界服即重启服务端）才能生效。

---

### [server] 节 —— 服务端配置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `server.removeErroringBlockEntities` | 删除报错方块实体 | bool | `false`（`true`/`false`） | ✅ | 设为 `true` 时，当某个**方块实体**（BlockEntity，即俗称的 TileEntity，如箱子/熔炉/模组机器）在其更新方法中抛出异常时，NeoForge 会**直接删除该方块实体**，而不是关闭服务器并打印崩溃日志。**⚠️ 危险选项**：可能导致机器内的物品丢失、方块状态错乱。仅在排查「Ticking Block Entity」崩溃时作为应急手段临时开启，处理完务必改回 `false`！NeoForge 官方明确声明对此造成的损失不负责。 |
| `server.removeErroringEntities` | 删除报错实体 | bool | `false`（`true`/`false`） | ✅ | 设为 `true` 时，当某个**实体**（Entity，如僵尸、掉落物、矿车等，**不包括**方块实体）在其 tick 方法中抛出异常时，NeoForge 会**直接删除该实体**，而不是关闭服务器并打印崩溃日志。**⚠️ 危险选项**：可能导致玩家丢失骑乘的坐骑、农场中的关键生物等。仅在排查「Ticking Entity」崩溃时作为应急手段临时开启，处理完务必改回 `false`！ |
| `server.fullBoundingBoxLadders` | 完整碰撞盒爬梯检测 | bool | `false`（`true`/`false`） | ✅ | 设为 `true` 时，检测实体是否在爬梯子时会检查**整个实体的碰撞盒**所覆盖的方块，而不仅限于实体当前所在的那个方块。会带来**明显的机制差异**（例如更高的爬梯判定范围），因此默认保持原版行为。仅在你确知某些模组需要此特性时才开启。 |
| `server.permissionHandler` | 权限处理器 | string | `neoforge:default_handler`（任意已注册的权限处理器 ID） | ✅ | 服务器使用的权限处理器 ID。默认为 `neoforge:default_handler`（NeoForge 内置的默认权限处理器）。仅当服务器中安装了提供自定义权限系统的模组时才需要修改。普通开服玩家保持默认即可。 |
| `server.advertiseDedicatedServerToLan` | 向局域网广播服务器 | bool | `true`（`true`/`false`） | 🔄 | 设为 `true` 时，专用服务端会向**本地局域网**广播自身存在，使同局域网下的客户端能在「多人游戏」界面自动看到这台服务器。公网部署时无实际意义；本地测试时不希望他人自动看到可关闭。 |

---

## config/neoforge-common.toml（NeoForge 通用配置）

`neoforge-common.toml` 同时影响客户端和服务器，包含开发者调试相关的配置。开服者通常保持默认。

### [common] 节（实际 TOML 顶级节） —— 通用配置

> ⚠️ 注意：NeoForge 的 `neoforge-common.toml` 实际上**没有外层 `[common]` 表头**，配置项直接位于文件顶级（即不写 `[common]`）。下列键名直接以顶级形式存在。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `logUntranslatedItemTagWarnings` | 未翻译物品标签警告模式 | enum | `SILENCED`（`SILENCED` / `DEV_SHORT` / `DEV_LONG` / `ENABLED`） | 🔄 | 主要面向开发者：在内置服务器（单人游戏）运行时，记录那些缺少翻译键（`tag.item.<命名空间>.<路径>`）的模组物品标签。`SILENCED`（静默，默认）= 不记录 / `DEV_SHORT` / `DEV_LONG` = 在开发环境中以短/长格式记录 / `ENABLED` = 任何环境都记录。普通开服者保持 `SILENCED`。 |
| `logLegacyTagWarnings` | 旧命名空间标签警告模式 | enum | `DEV_SHORT`（`SILENCED` / `DEV_SHORT` / `DEV_LONG` / `ENABLED`） | 🔄 | 主要面向开发者：在内置服务器运行时，记录那些仍在使用旧的 `forge:` 命名空间的模组标签。`DEV_SHORT`（默认）= 仅在开发环境中以短格式记录 / `SILENCED` = 不记录 / `DEV_LONG` = 长格式 / `ENABLED` = 任何环境都记录。普通开服者可改为 `SILENCED` 减少日志噪音。 |
| `attributeAdvancedTooltipDebugInfo` | 属性高级工具提示调试 | bool | `true`（`true`/`false`） | 🔄 | 设为 `true` 时，开启「高级工具提示」（按 F3+H）后会在物品上额外显示其属性的调试信息。开服端一般不显示 tooltip，此项对服务端运行无影响，保持默认即可。 |

---

## config/neoforge-client.toml（NeoForge 客户端配置，仅作了解）

> 以下文件**仅在客户端**生效，开服时无需修改，此处仅作简要介绍以便完整性。

| 键名 | 中文含义 | 类型 | 默认值 | 说明 |
|---|---|---|---|---|
| `experimentalForgeLightPipelineEnabled` | 实验性光照管线 | bool | `false` | 启用 NeoForge 实验性方块渲染管线，修复自定义模型的光照问题。 |
| `showLoadWarnings` | 显示加载警告 | bool | `true` | 加载时是否弹出警告窗口。 |
| `logUntranslatedConfigurationWarnings` | 未翻译配置警告 | bool | `true` | 开发环境记录未翻译的配置值，开发者选项。 |
| `reducedDepthStencilFormat` | 简化深度模板格式 | bool | `false` | 模组启用模板测试时，深度缓冲位数。`true`=24+8 位（省显存，可能引入伪影），`false`=32+8 位。 |

---

## 配置示例（neoforge-server.toml 完整默认值）

```toml
#Set this to true to remove any BlockEntity that throws an error in its update method instead of closing the server and reporting a crash log. BE WARNED THIS COULD SCREW UP EVERYTHING USE SPARINGLY WE ARE NOT RESPONSIBLE FOR DAMAGES.
removeErroringBlockEntities = false
#Set this to true to remove any Entity (Note: Does not include BlockEntities) that throws an error in its tick method instead of closing the server and reporting a crash log. BE WARNED THIS COULD SCREW UP EVERYTHING USE SPARINGLY WE ARE NOT RESPONSIBLE FOR DAMAGES.
removeErroringEntities = false
#Set this to true to check the entire entity's collision bounding box for ladders instead of just the block they are in. Causes noticeable differences in mechanics so default is vanilla behavior. Default: false.
fullBoundingBoxLadders = false
#The permission handler used by the server. Defaults to neoforge:default_handler if no such handler with that name is registered.
permissionHandler = "neoforge:default_handler"
#Set this to true to enable advertising the dedicated server to local LAN clients so that it shows up in the Multiplayer screen automatically.
advertiseDedicatedServerToLan = true
```

> **注意**：NeoForge 1.20.4+ 的 `neoforge-server.toml` 文件中，配置项**直接位于文件顶级**（没有 `[server]` 表头），但语义上仍属于服务端配置。这与你看到的源码 `Server` 内部类对应。

### config/neoforge-common.toml 完整默认值

```toml
#A config option mainly for developers. Logs out modded item tags that do not have translations when running on integrated server. Format desired is tag.item.<namespace>.<path> for the translation key. Defaults to SILENCED.
logUntranslatedItemTagWarnings = "SILENCED"
#A config option mainly for developers. Logs out modded tags that are using the 'forge' namespace when running on integrated server. Defaults to DEV_SHORT.
logLegacyTagWarnings = "DEV_SHORT"
#Set this to true to enable showing debug information about attributes on an item when advanced tooltips is on.
attributeAdvancedTooltipDebugInfo = true
```

---

## 优化建议（针对模组服管理员）

### 🚨 应急修复「Ticking Entity / Ticking Block Entity」崩溃

NeoForge 处理流程与 Forge 一致，**但文件位置不同**：

1. **先备份世界**：`cp -r world world_backup`。
2. **关闭服务器**。
3. **打开** `config/neoforge-server.toml`（注意是**根目录 config 文件夹**下，不是世界目录）。
4. **根据崩溃类型**临时开启对应选项：
   - 看到 `-- Entity being ticked --` → `removeErroringEntities = false` 改为 `true`。
   - 看到 `-- Block entity being ticked --` → `removeErroringBlockEntities = false` 改为 `true`。
5. **保存，启动服务器**。NeoForge 会自动删除引发崩溃的实体。
6. **⚠️ 关键收尾**：修复成功后**必须**改回 `false`！

### 🌐 Java 版本要求

NeoForge 1.21+ 强制要求 **Java 21**（推荐 Microsoft OpenJDK 21 / Eclipse Temurin 21）。低版本 Java 会直接启动失败。验证：`java -version` 必须显示 `21.x.x`。

### 💾 内存与启动参数

- 修改 `user_jvm_args.txt` 调整内存分配，例如：
  ```
  -Xms4G
  -Xmx8G
  ```
- 推荐使用 Aikar's Flags（G1GC）以获得更好 GC 性能。
- 大型整合包建议至少分配 6-8 GB 内存。

### 🌱 性能优化通用建议

- 使用 Chunky 预生成区块，避免玩家探索时实时生成卡顿。
- 视距 `view-distance` 设为 8~10 即可（在 `server.properties` 中设置）。
- 安装 Spark 模组用于性能分析，定位卡顿源。
- Java 21 的 G1GC 已大幅改进，无需切换到 ZGC/Shenandoah 除非你真的需要超低延迟。

### 🔄 从 Forge 迁移

- 世界数据可直接复用，无需转换。
- 模组必须使用 NeoForge 兼容版本，**不可混用 Forge 与 NeoForge 模组 JAR**。
- 配置文件位置发生变化：Forge 的 `<世界>/serverconfig/forge-server.toml` → NeoForge 的 `config/neoforge-server.toml`。
- 各模组的配置文件命名可能因模组作者在 Forge 与 NeoForge 之间改键而略有不同，迁移后建议删除 `config/` 让其重新生成（备份！）。

---

## 参考链接

- 官方文档（服务端安装）：https://docs.neoforged.net/user/docs/server/
- GitHub 源码（NeoForgeConfig.java）：https://github.com/neoforged/NeoForge/blob/1.21.x/src/main/java/net/neoforged/neoforge/common/NeoForgeConfig.java
- 官方网站：https://neoforged.net/
- 社区故障排查指南（Ticking Entities）：https://docs.feed-the-beast.com/docs/support/Troubleshooting/ticking-entities/
- ATGuides NeoForge 设置指南：https://guide.astroworldmc.com/how-to-set-up-neoforge-server

---

> ⚠️ **免责声明**：NeoForge 处于活跃开发中，配置项在版本迭代中可能调整。本文档基于 NeoForge 1.21.x 源码整理。如遇键名差异或新选项，请以服务器实际生成的 `neoforge-server.toml` 注释为准。
