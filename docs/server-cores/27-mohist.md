# Mohist 服务器配置文件中文手册

> Mohist（墨石/墨端）是基于 Forge + Paper 的混合服务端，允许同时运行 Forge 模组与 Bukkit/Spigot/Paper 插件。
> 继承关系：Vanilla → Forge + Paper → Mohist
> 官方 GitHub：https://github.com/MohistMC/Mohist
> 官方网站：https://mohistmc.com/
> 官方 Wiki：https://wiki.mohistmc.com/

Mohist 由 MohistMC 团队开发，定位为「Thermos / Cauldron / MCPC+ 的现代继承者」。它在 Forge 模组加载器之上叠加了 Paper 系列插件的兼容层，让服主既能运行工业、神秘、冒险等大型 Forge 模组，又能使用 EssentialsX、LuckPerms、WorldEdit 等 Bukkit/Spigot 插件。Mohist 是当前主流混合端之一，更新活跃，对 1.7.10 ~ 1.20.2 都有维护版本。注意：混合端性能普遍低于纯插件服或纯模组服，且并非所有模组/插件都 100% 兼容，新手服主请逐个测试组合。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| paper.yml / paper-global.yml / paper-world-defaults.yml | YAML | Paper 继承 | Paper 全局/世界配置 |
| forge.cfg / fml.toml | TOML/CFG | Forge 继承 | Forge 模组加载器配置 |
| mohist-config/mohist.yml | YAML | Mohist 专属 | Mohist 独有核心配置（本文档重点） |
| mohist-config/world.yml | YAML | Mohist 专属 | Mohist 世界级配置（部分版本生成） |

> 说明：Mohist 完整继承 Forge 与 Paper 的全部配置体系，本文档仅聚焦 Mohist 独有的 `mohist-config/mohist.yml`（部分版本路径为根目录的 `mohist.yml`）。其余配置请参阅对应的 Forge / Paper / Spigot / Bukkit 手册。

## mohist-config/mohist.yml（Mohist 专属配置）

`mohist.yml` 位于 `mohist-config/` 目录下（1.20.1+ 版本），早期版本位于服务器根目录。由 `com.mohistmc.config.MohistConfig` 加载。配置文件顶层使用扁平的 `mohist:` 命名空间，启动时读取，多数项需重启生效，少数项支持通过 `/mohist reload` 热重载。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `mohist.lang`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `string[]` 字符串列表。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持 `/mohist reload` 热重载。

---

### 1. 通用设置（mohist）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `mohist.lang` | 控制台语言 | string | `en_US`（`en_US` / `zh_CN` / `fr_FR` / `es_ES` / `de_DE` / `ja_JP` / `ko_KR` / `ru_RU` / `pt_BR` / `zh_TW`） | ✅ | Mohist 启动日志与控制台提示所使用的语言。注意：仅影响 Mohist 自身日志，不影响 Minecraft 原版日志。修改后需重启。 |
| `mohist.check_update` | 检查 Mohist 更新 | bool | `true`（`true` / `false`） | ✅ | 启动时是否联网检查 Mohist 新版本。公网服务器可开启；离线服可关闭以避免启动卡顿。 |
| `mohist.check_update_bukkit` | 检查 Bukkit/Spigot 兼容性 | bool | `true`（`true` / `false`） | ✅ | 启动时是否联网检查当前 Mohist 与最新 Bukkit/Spigot API 的兼容性。 |
| `mohist.check_libraries_update` | 检查依赖库更新 | bool | `true`（`true` / `false`） | ✅ | 启动时是否检查并自动下载缺失的依赖库文件。首次启动务必开启。 |
| `mohist.metrics` | 启用 bStats 统计上报 | bool | `true`（`true` / `false`） | ✅ | 是否启用 bStats 匿名数据上报，帮助开发者了解使用情况。无隐私敏感信息，建议保持开启。 |
| `mohist.show_logo` | 启动时显示 Mohist Logo | bool | `true`（`true` / `false`） | ✅ | 控制台启动时是否打印 Mohist ASCII Logo。 |
| `mohist.console_name` | 控制台名称 | string | `Server` | ✅ | 控制台作为虚拟发送者执行命令时的显示名称。 |
| `mohist.only_english` | 强制仅英文日志 | bool | `false`（`true` / `false`） | ✅ | 是否强制所有日志输出为英文（即使 lang 设置为其他语言）。便于向 GitHub 提交 Issue。 |

---

### 2. 兼容性设置（兼容性 / 平台适配）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `mohist.bukkit_version` | Bukkit API 版本 | string | 自动检测（如 `1.20.1-R0.1-SNAPSHOT`） | ✅ | Mohist 内部使用的 Bukkit API 版本号，通常由 Mohist 自动写入，请勿手动修改。 |
| `mohist.support_non_paper_plugins` | 允许非 Paper 系插件 | bool | `true`（`true` / `false`） | ✅ | 是否允许加载仅声明支持 Spigot/CraftBukkit 的插件。关闭后只允许加载声明支持 Paper 的插件。 |
| `mohist.disable_plugins_blacklist` | 禁用插件黑名单检查 | bool | `false`（`true` / `false`） | ✅ | Mohist 维护了一份已知与混合端不兼容的插件黑名单。设为 `true` 跳过该检查（不推荐，可能导致崩溃）。 |
| `mohist.disable_mods_blacklist` | 禁用模组黑名单检查 | bool | `false`（`true` / `false`） | ✅ | 同上，跳过 Mohist 维护的已知不兼容 Forge 模组黑名单。 |
| `mohist.use_blacklist_extensions` | 启用黑名单扩展 | bool | `false`（`true` / `false`） | ✅ | 是否启用更严格的扩展黑名单（包含更多边缘案例）。开启可能阻止更多模组/插件加载。 |
| `mohist.plugins_hot_reload` | 插件热重载 | bool | `false`（`true` / `false`） | 🔄 | 是否启用插件热重载功能（如 `/plugin reload`）。实验性功能，部分插件热重载可能引发内存泄漏。 |
| `mohist.disable_warn` | 禁用兼容性警告 | bool | `false`（`true` / `false`） | 🔄 | 是否在启动日志中禁用 Mohist 对某些不兼容插件/模组的警告信息。生产环境为减少日志噪音可考虑开启。 |

---

### 3. 性能优化（实体/区块/异步）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `mohist.max_entities` | 实体数量上限 | int | `-1`（≥ -1，-1 = 禁用） | 🔄 | 单一世界内允许的最大实体数量。超出则阻止新实体生成。-1 表示不限制。注意：与 Forge 模组的实体（如机器内的物品）可能冲突。 |
| `mohist.entity_tick` | 实体 tick 优化 | int | `1`（≥ 1） | 🔄 | 实体 tick 优化级别。值越大越省 CPU 但实体 AI 越迟钝。1 = 原版。⚠️ 影响模组怪物 AI，建议保持默认。 |
| `mohist.entity_tick_skip` | 跳过远实体 tick | bool | `false`（`true` / `false`） | 🔄 | 是否跳过远离玩家实体的 tick 计算。开启可提升性能，但可能破坏部分模组刷怪塔/农场。 |
| `mohist.async_pathfinding` | 异步寻路 | bool | `false`（`true` / `false`） | ✅ | 将生物寻路计算转移到异步线程。⚠️ 部分模组（如自定义 AI 模组）可能与异步寻路冲突，开启前请测试。 |
| `mohist.async_mob_spawning` | 异步生物生成 | bool | `false`（`true` / `false`） | ✅ | 将生物生成计算转移到异步线程。⚠️ 与 Forge 模组的事件监听可能冲突，模组较多的服务器请谨慎开启。 |
| `mohist.enable_real_ticking` | 真实 tick 远实体 | bool | `false`（`true` / `false`） | ✅ | 是否对远离玩家的实体也保持「真实 tick」（原版行为）。关闭可省性能，但部分模组的机器/农场可能失效。 |
| `mohist.runtime_optimizations` | 启用运行时优化 | bool | `true`（`true` / `false`） | ✅ | 是否启用 Mohist 运行时性能优化补丁。包含若干异步处理与缓存优化。⚠️ 与高性能需求模组可能冲突。 |
| `mohist.tps_real_time` | 真实 TPS 显示 | bool | `true`（`true` / `false`） | 🔄 | `/tps` 命令显示真实 TPS（包含所有线程负载）还是仅主线程 TPS。 |
| `mohist.use_Spark_and_Sync_Timer` | 启用 Spark 计时器 | bool | `true`（`true` / `false`） | ✅ | 是否启用 Mohist 内置的同步计时器（用于性能分析）。Spark 插件依赖此功能。 |

---

### 4. 区块与世界设置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `mohist.unload_worlds` | 允许卸载世界 | bool | `true`（`true` / `false`） | 🔄 | 是否允许在无玩家时卸载非主世界（如下界、末地）以节省内存。多世界服建议开启。 |
| `mohist.disable_chunk_unload` | 禁用区块卸载 | bool | `false`（`true` / `false`） | 🔄 | 是否禁用区块自动卸载（所有加载过的区块常驻内存）。开启可减少卡顿但极大增加内存占用。 |
| `mohist.chunk_unload_delay` | 区块卸载延迟 | int | `15000`（≥ 0，单位：毫秒） | 🔄 | 玩家离开后多久才卸载对应区块。值越大越省 CPU 但内存占用越高。 |
| `mohist.max-tick-time` | 单 tick 最大耗时 | int | `60000`（≥ -1，单位：毫秒；-1 = 禁用） | 🔄 | 单个 tick 超过此时间则触发 watchdog 崩服报告。-1 禁用 watchdog（不推荐，模组卡死将无报警）。 |

---

### 5. 事件桥接（Forge ↔ Bukkit 事件转发）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `mohist.fire_MC_ExplosionEvent` | 转发爆炸事件 | bool | `true`（`true` / `false`） | 🔄 | 是否将 Forge 的爆炸事件转发到 Bukkit 的 `EntityExplodeEvent` / `BlockExplodeEvent`。关闭可省 CPU，但 WorldGuard 等保护插件将无法拦截模组爆炸。 |
| `mohist.fire_MC_BlockBreakEvent` | 转发破坏方块事件 | bool | `true`（`true` / `false`） | 🔄 | 是否将 Forge 的方块破坏事件转发到 Bukkit 的 `BlockBreakEvent`。关闭后保护插件将无法拦截模组方块破坏。 |
| `mohist.fire_MC_BlockPlaceEvent` | 转发放置方块事件 | bool | `true`（`true` / `false`） | 🔄 | 是否将 Forge 的方块放置事件转发到 Bukkit 的 `BlockPlaceEvent`。 |
| `mohist.implement_entity_collision_event` | 实体碰撞事件 | bool | `true`（`true` / `false`） | 🔄 | 是否实现 Bukkit 的实体碰撞事件（`EntityInteractEvent` 等）。关闭可提升性能，但部分反作弊/物理插件会失效。 |
| `mohist.implement_entity_damage_event` | 实体伤害事件 | bool | `true`（`true` / `false`） | 🔄 | 是否为 Forge 模组的实体伤害触发 Bukkit 的 `EntityDamageEvent`。关闭后 RPG/伤害修改类插件将无法作用于模组伤害。 |

---

### 6. 玩家与权限

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `mohist.hide_online_players` | 隐藏在线玩家列表 | bool | `false`（`true` / `false`） | 🔄 | 是否对其他服务器隐藏本服在线玩家列表（用于跨服防止 Tab 自动补全）。 |
| `mohist.disable_op_permissions` | 禁用 OP 权限 | bool | `false`（`true` / `false`） | 🔄 | 是否禁用原版 OP 权限系统，强制所有权限通过 LuckPerms 等插件管理。 |
| `mohist.no respawn_screen` | 禁用重生界面 | bool | `false`（`true` / `false`） | 🔄 | 玩家死亡时直接重生（不显示「你死了」界面），适合小游戏服。 |

---

### 7. 日志与调试

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `mohist.log_mods_deaths` | 记录模组实体死亡 | bool | `false`（`true` / `false`） | 🔄 | 是否在日志中记录所有 Forge 模组实体的死亡事件（用于排查刷怪问题）。开启会产生大量日志。 |
| `mohist.watchdog` | 启用看门狗 | bool | `true`（`true` / `false`） | 🔄 | 是否启用 watchdog 线程监控主线程卡顿。生产环境强烈建议开启。 |
| `mohist.use_java_Hoe` | 启用 Java 优化 | bool | `false`（`true` / `false`） | ✅ | 实验性：启用 Java 内部优化（如向量化运算）。需要 JDK 17+ 支持。⚠️ 实验功能，可能不稳定。 |

---

### 8. 黑名单与扩展（mohist-config/world.yml 部分项）

> 1.20.1+ 版本会生成 `mohist-config/world.yml`，包含与世界级相关的实体、刷怪、世界加载等设置。常见字段如下：

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.keep_spawn_loaded` | 出生点常驻内存 | bool | `true`（`true` / `false`） | 🔄 | 是否将世界出生点区块常驻内存（不卸载）。建议开启以避免新玩家进入时卡顿。 |
| `world-settings.default.max_entity_cramming` | 实体挤压上限 | int | `24`（≥ 0） | 🔄 | 单格内实体数量上限，超过即触发挤压伤害（原版 24）。模组刷怪塔可能需要调高。 |
| `world-settings.default.max_entity_ticks` | 单 tick 实体上限 | int | `-1`（≥ -1，-1 = 不限制） | 🔄 | 单次 tick 最多处理的实体数量。-1 不限制。模组较多的服务器可设置上限以防止实体爆炸卡服。 |

---

## 配置示例（mohist-config/mohist.yml 完整默认值）

```yaml
# Mohist Configuration
# Wiki: https://wiki.mohistmc.com/
mohist:
  # 控制台语言
  lang: en_US
  # 启动时检查 Mohist 更新
  check_update: true
  # 检查 Bukkit/Spigot 兼容性
  check_update_bukkit: true
  # 检查并下载依赖库
  check_libraries_update: true
  # bStats 匿名统计
  metrics: true
  # 启动 Logo
  show_logo: true
  # 控制台虚拟名称
  console_name: Server
  # 强制仅英文日志
  only_english: false
  # Bukkit API 版本（自动写入，请勿手动修改）
  bukkit_version: 1.20.1-R0.1-SNAPSHOT
  # 允许非 Paper 系插件
  support_non_paper_plugins: true
  # 禁用插件黑名单
  disable_plugins_blacklist: false
  # 禁用模组黑名单
  disable_mods_blacklist: false
  # 启用扩展黑名单
  use_blacklist_extensions: false
  # 插件热重载（实验性）
  plugins_hot_reload: false
  # 禁用兼容性警告
  disable_warn: false
  # 实体数量上限（-1 不限制）
  max_entities: -1
  # 实体 tick 优化级别
  entity_tick: 1
  # 跳过远实体 tick
  entity_tick_skip: false
  # 异步寻路
  async_pathfinding: false
  # 异步生物生成
  async_mob_spawning: false
  # 真实 tick 远实体
  enable_real_ticking: false
  # 运行时优化
  runtime_optimizations: true
  # 真实 TPS 显示
  tps_real_time: true
  # Spark 计时器
  use_Spark_and_Sync_Timer: true
  # 允许卸载世界
  unload_worlds: true
  # 禁用区块卸载
  disable_chunk_unload: false
  # 区块卸载延迟（毫秒）
  chunk_unload_delay: 15000
  # 单 tick 最大耗时（-1 禁用 watchdog）
  max-tick-time: 60000
  # 转发爆炸事件
  fire_MC_ExplosionEvent: true
  # 转发破坏方块事件
  fire_MC_BlockBreakEvent: true
  # 转发放置方块事件
  fire_MC_BlockPlaceEvent: true
  # 实体碰撞事件
  implement_entity_collision_event: true
  # 实体伤害事件
  implement_entity_damage_event: true
  # 隐藏在线玩家列表
  hide_online_players: false
  # 禁用 OP 权限
  disable_op_permissions: false
  # 禁用重生界面
  no respawn_screen: false
  # 记录模组实体死亡
  log_mods_deaths: false
  # 看门狗
  watchdog: true
  # Java 优化（实验性）
  use_java_Hoe: false
```

## 优化建议（针对 Forge 模组 + Bukkit 插件混合服）

1. **黑名单与兼容性**：保持 `disable_plugins_blacklist: false` 与 `disable_mods_blacklist: false`，让 Mohist 自动拦截已知不兼容项。若启动失败且确认是误判，再单独绕过。
2. **事件桥接**：保护插件（WorldGuard、GriefPrevention）需要 `fire_MC_BlockBreakEvent`、`fire_MC_BlockPlaceEvent`、`fire_MC_ExplosionEvent` 保持 `true`，否则模组方块操作将绕过保护。
3. **实体优化**：模组刷怪塔/农场较多的服务器**不要**开启 `entity_tick_skip` 或 `enable_real_ticking: false`，否则会破坏模组机制。优先调整 `max_entities` 与 `entity_tick`。
4. **异步功能**：`async_pathfinding` 与 `async_mob_spawning` 在模组较多时易冲突，建议**仅在纯原版 + 插件**的辅助子服开启，主模组服保持关闭。
5. **看门狗**：保持 `watchdog: true` 与 `max-tick-time: 60000`，模组卡死时能及时收到崩溃报告。
6. **JVM 优化**：Mohist 是混合端，对内存敏感，推荐 `-Xms4G -Xmx8G -XX:+UseG1GC -XX:+ParallelRefProcEnabled`（4G 起，按模组数量递增）。
7. **Java 版本**：1.18.2+ 必须使用 Java 17；1.16.5 推荐 Java 11；1.12.2 使用 Java 8。版本不匹配会启动失败。
8. **更新检查**：离线服可关闭 `check_update`、`check_update_bukkit`、`check_libraries_update` 以加速启动；公网服建议保持开启以及时获取安全修复。

> 参考来源：Mohist 官方源码 [`MohistConfig.java`](https://github.com/MohistMC/Mohist)、[Mohist Wiki](https://wiki.mohistmc.com/)。
