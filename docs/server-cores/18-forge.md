# Forge 服务器配置文件中文手册

> Forge 是 Minecraft 最经典、生态最庞大的模组加载器，支持通过 TOML 配置文件管理自身与服务端行为。
> 继承关系：Vanilla → Forge（在原版服务端基础上加载 mods/ 下的模组 JAR）
> 官方 GitHub：https://github.com/MinecraftForge/MinecraftForge
> 官方文档：https://docs.minecraftforge.net/
> 数据来源：Forge 1.21.x 源码 `ForgeConfig.java` / 官方文档 / 社区故障排查指南
> 适用版本基准：Forge 1.18 ~ 1.21.x（自 1.14 起配置体系基本一致）

## 核心简介

Minecraft Forge 自 1.13 起将其原版服务端配置文件从 `config/forge.cfg`（Properties 风格）切换为 TOML 格式，文件名为 `forge-server.toml`，并迁移到**每个世界目录下**的 `serverconfig/` 子目录（即 `<世界名>/serverconfig/forge-server.toml`）。

> ⚠️ 这个位置非常关键：很多新手会去服务器根目录的 `config/` 下找，结果找不到。Forge 故意把服务端配置放到**世界目录里**，这样不同世界可以拥有不同的服务端配置；同样地，单人存档也会在 `saves/<存档名>/serverconfig/` 下生成对应文件。

除了 `forge-server.toml`，Forge 还会生成两个相关配置文件：
- `config/forge-common.toml`（客户端 + 服务端共有的通用配置）
- `config/forge-client.toml`（仅客户端使用的配置，开服不需要关注）

由于本手册面向**开服**场景，重点讲解 `forge-server.toml`，并简要带过 `forge-common.toml` 中开服可能用到的部分。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| eula.txt | 文本 | Vanilla 继承 | 必须 `eula=true` 才能启动 |
| **forge-server.toml** | TOML | **Forge 专属** | **Forge 服务端核心配置（本文档重点）** |
| config/forge-common.toml | TOML | Forge 专属 | Forge 通用配置（开发调试为主） |
| config/forge-client.toml | TOML | Forge 专属 | Forge 客户端配置（开服无需关注） |
| mods/ | 目录 | Forge 专属 | 存放模组 JAR 文件 |

> 说明：Forge 是模组加载器，并非完整的服务端实现。除上述 Forge 自身配置外，其余运行规则（端口、视距、白名单等）均沿用原版 `server.properties`，请参阅 Vanilla 手册。

## forge-server.toml（Forge 服务端核心配置）

`forge-server.toml` 由 Forge 在世界首次加载时自动生成。所有配置项位于 `[server]` 节（顶级 `[server]` 表）。配置项采用 TOML 语法：键名在 `[server]` 节下，等号右侧为值。

### 阅读约定

- **键名**：保持原样不翻译，TOML 路径以 `server.键名` 形式表达（实际文件中等价于 `[server]` 节下的 `键名 = 值`）。
- **值类型**：`bool` 布尔 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示必须重启世界（关闭并重新加载存档）才能生效；🔄 表示支持运行时热重载。
- **worldRestart**：Forge 在源码中标记为 `worldRestart()` 的选项，意味着修改后需要**重启世界**（不是整个服务端，而是 unload → load 该世界）才能生效。对于单一主世界的模组服，等同于重启服务端。

---

### [server] 节 —— 服务端配置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `server.removeErroringBlockEntities` | 删除报错方块实体 | bool | `false`（`true`/`false`） | ✅ | 设为 `true` 时，当某个**方块实体**（BlockEntity，即俗称的 TileEntity，如箱子/熔炉/模组机器）在其更新方法（update tick）中抛出异常时，Forge 会**直接删除该方块实体**，而不是关闭服务器并打印崩溃日志。**⚠️ 危险选项**：可能导致机器内的物品丢失、方块状态错乱。仅在排查「Ticking Block Entity」崩溃时作为应急手段临时开启，处理完务必改回 `false`！Forge 官方明确声明对此造成的损失不负责。 |
| `server.removeErroringEntities` | 删除报错实体 | bool | `false`（`true`/`false`） | ✅ | 设为 `true` 时，当某个**实体**（Entity，如僵尸、掉落物、矿车等，注意**不包括**方块实体）在其 tick 方法中抛出异常时，Forge 会**直接删除该实体**，而不是关闭服务器并打印崩溃日志。**⚠️ 危险选项**：可能导致玩家丢失骑乘的坐骑、农场中的关键生物等。仅在排查「Ticking Entity」崩溃时作为应急手段临时开启，处理完务必改回 `false`！ |
| `server.fullBoundingBoxLadders` | 完整碰撞盒爬梯检测 | bool | `false`（`true`/`false`） | ✅ | 设为 `true` 时，检测实体是否在爬梯子时会检查**整个实体的碰撞盒**所覆盖的方块，而不仅限于实体当前所在的那个方块。这会带来**明显的机制差异**（例如更高的爬梯判定范围），因此默认保持原版行为。仅在你确知某些模组需要此特性时才开启。 |
| `server.permissionHandler` | 权限处理器 | string | `forge:default_handler`（任意已注册的权限处理器 ID） | ✅ | 服务器使用的权限处理器 ID。默认为 `forge:default_handler`（Forge 内置的默认权限处理器）。仅当服务器中安装了提供自定义权限系统的模组（如某些权限管理 API）时才需要修改。普通开服玩家保持默认即可。 |
| `server.advertiseDedicatedServerToLan` | 向局域网广播服务器 | bool | `true`（`true`/`false`） | 🔄 | 设为 `true` 时，专用服务端会向**本地局域网**广播自身存在，使同局域网下的客户端能在「多人游戏」界面自动看到这台服务器。在公网/VPS 部署时此项无实际意义；如果你在本地测试时不希望他人自动看到服务器，可以关闭。 |

---

## config/forge-common.toml（Forge 通用配置）

`forge-common.toml` 同时影响客户端和服务器。开服者通常只需关注其中的标签迁移警告相关选项。

### [general] 节 —— 通用配置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `general.logLegacyTagWarnings` | 旧标签迁移助手模式 | enum | `OFF`（`OFF`/`ONLY_IN_DEV_ENV`/`ALWAYS`） | 🔄 | 帮助模组开发者查找使用旧命名空间（如 `forge:` 前缀）的模组标签，并提示其对应的新约定标签的可选值。`OFF`（关闭，默认）= 不提示 / `ONLY_IN_DEV_ENV`（仅开发环境）= 仅在开发环境中提示 / `ALWAYS`（始终）= 在任何环境（包括生产服务器）下都提示。普通开服者保持 `OFF` 即可。**注意**：此键名虽为 `logLegacyTagWarnings`，但对应的 Java 变量名是 `migrationHelperMode`，属于历史命名遗留。 |

---

## 配置示例（forge-server.toml 完整默认值）

```toml
#Server configuration settings
[server]
    #Set this to true to remove any BlockEntity that throws an error in its update method instead of closing the server and reporting a crash log. BE WARNED THIS COULD SCREW UP EVERYTHING USE SPARINGLY WE ARE NOT RESPONSIBLE FOR DAMAGES.
    removeErroringBlockEntities = false
    #Set this to true to remove any Entity (Note: Does not include BlockEntities) that throws an error in its tick method instead of closing the server and reporting a crash log. BE WARNED THIS COULD SCREW UP EVERYTHING USE SPARINGLY WE ARE NOT RESPONSIBLE FOR DAMAGES.
    removeErroringEntities = false
    #Set this to true to check the entire entity's collision bounding box for ladders instead of just the block they are in. Causes noticeable differences in mechanics so default is vanilla behavior. Default: false.
    fullBoundingBoxLadders = false
    #The permission handler used by the server. Defaults to forge:default_handler if no such handler with that name is registered.
    permissionHandler = "forge:default_handler"
    #Set this to true to enable advertising the dedicated server to local LAN clients so that it shows up in the Multiplayer screen automatically.
    advertiseDedicatedServerToLan = true
```

### config/forge-common.toml 完整默认值

```toml
#General configuration settings
[general]
    #A config option to help developers find known legacy modded tags that have common convention equivalents when running on integrated server. Defaults to OFF.
    logLegacyTagWarnings = "OFF"
```

---

## 优化建议（针对模组服管理员）

### 🚨 应急修复「Ticking Entity / Ticking Block Entity」崩溃

模组服最常见的崩溃类型之一，是某个被模组修改过的实体或方块实体在更新时报错，导致服务端反复崩溃。处理流程：

1. **先备份世界**：`cp -r world world_backup`（操作前务必备份）。
2. **关闭服务器**。
3. **打开** `<世界名>/serverconfig/forge-server.toml`（注意是**世界目录下**的 serverconfig，不是根目录 config）。
4. **根据崩溃类型**临时开启对应选项：
   - 崩溃日志中看到 `Ticking entity` / `-- Entity being ticked --` → 把 `removeErroringEntities = false` 改为 `true`。
   - 崩溃日志中看到 `Ticking block entity` / `-- Block entity being ticked --` → 把 `removeErroringBlockEntities = false` 改为 `true`。
   - 不确定时可同时开启两者。
5. **保存文件，启动服务器**。Forge 会自动删除引发崩溃的实体，服务器得以正常加载。
6. **⚠️ 关键收尾**：修复成功后**必须**停服并把上述两个值改回 `false`！否则日后任何实体报错都会被静默删除，可能悄悄破坏你的机器物品或坐骑。

### 🌐 局域网服务器可见性

- 如果你在家里和舍友联机，但舍友在「多人游戏」界面看不到你的服务器，检查 `advertiseDedicatedServerToLan = true`（默认就是 true）。
- 公网部署时此项无影响，可保留默认。

### 🔒 权限处理器

- 99% 的服主无需修改 `permissionHandler`。仅当安装了提供 `custom:permission_handler` 类权限 API 的模组，并按其文档要求切换处理器时才需修改。
- 错误的 `permissionHandler` 值会导致服务器启动失败，提示找不到对应处理器。

### 📁 模组管理小贴士

- Forge 自身配置非常精简，真正的「调参」工作分散在 `config/` 目录下各模组自己的配置文件（如 `config/<modid>-common.toml`、`config/<modid>-server.toml` 等）。每个模组的配置文件命名与内容由模组作者决定。
- 升级 Forge 或模组后，建议删除 `serverconfig/` 目录让其按新规则重新生成（先备份！）。

---

## 参考链接

- 官方文档（配置系统）：https://docs.minecraftforge.net/en/latest/misc/config/
- GitHub 源码（ForgeConfig.java）：https://github.com/MinecraftForge/MinecraftForge/blob/1.21.x/src/main/java/net/minecraftforge/common/ForgeConfig.java
- Forge 文件下载：https://files.minecraftforge.net/
- 社区故障排查指南（Ticking Entities）：https://docs.feed-the-beast.com/docs/support/Troubleshooting/ticking-entities/
- ATGuides NeoForge/Forge 设置指南：https://guide.astroworldmc.com/how-to-set-up-neoforge-server

---

> ⚠️ **免责声明**：Forge 配置项在版本迭代中偶有调整，本文档基于 Forge 1.21.x 源码整理。如遇键名差异或新选项，请以服务器实际生成的 `forge-server.toml` 注释为准。
