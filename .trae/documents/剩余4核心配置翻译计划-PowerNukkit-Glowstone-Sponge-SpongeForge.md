# 剩余 4 核心配置翻译计划（PowerNukkit / Glowstone / Sponge / SpongeForge）

## 摘要

为 MSMC 项目完成剩余 4 个 Minecraft 服务器核心（PowerNukkit、Glowstone、Sponge、SpongeForge）的配置文件中文翻译，产出双轨成果物：
1. **Markdown 文档**（4 份）：每核心一份，写入 `/workspace/docs/server-cores/`
2. **C# 注册方法代码片段**（4 份）：每核心一个 Register 方法，写入 `/workspace/docs/server-cores/_patches/`

严格遵循已完成的 Nukkit 文档（`34-nukkit.md` + `RegisterNukkitYml.cs`）所建立的模式与翻译规范。

## 现状分析

### 已完成（参考模板）
- `/workspace/docs/server-cores/34-nukkit.md` — Nukkit 完整文档（nukkit.yml + 基岩版 server.properties），约 60 个配置项
- `/workspace/docs/server-cores/_patches/RegisterNukkitYml.cs` — Nukkit C# 注册方法，含 `RegisterNukkitYml()` + `RegisterNukkitServerProperties()` 两个子方法
- `/workspace/docs/server-cores/07-pufferfish.md` — 另一参考模板（Paper 系）
- `/workspace/docs/server-cores/18-forge.md` + `RegisterForgeServerToml.cs` — Forge 参考（模组端模式）

### 待产出（8 个文件）
| 核心 | Markdown 文档 | C# 补丁文件 | 配置文件 | 格式 |
|---|---|---|---|---|
| PowerNukkit | `35-powernukkit.md` | `RegisterPowerNukkitYml.cs` | `powernukkit.yml` + 基岩版 server.properties | YAML + Properties |
| Glowstone | `36-glowstone.md` | `RegisterGlowstoneConfig.cs` | `glowstone.yml` | YAML |
| Sponge | `32-sponge.md` | `RegisterSpongeGlobalConf.cs` | `global.conf` | HOCON |
| SpongeForge | `33-spongeforge.md` | `RegisterSpongeForgeConf.cs` | `global.conf`（Forge 集成差异） | HOCON |

### 数据源现状（本地缓存）
- ✅ **Sponge `global.conf`**：完整 697 行已缓存于 `/tmp/cfgs/sponge-global.conf`，涵盖所有节：broken-mods、bungeecord、cause-tracker、commands、debug、entity、entity-activation-range、entity-collisions、exploits、general、ip-sets、logging、metrics、modules、movement-checks、optimizations、permission、player-block-tracker、spawner、sql、teleport-helper、tileentity-activation、timings、world
- ✅ **Glowstone `ServerConfig.java`**：Key 枚举完整定义已缓存于 `/tmp/cfgs/glowstone-ServerConfig.java`（第 392-578 行），包含所有配置键的 path、默认值、验证器。Glowstone 的 `glowstone.yml` 由该枚举在运行时生成，因此 Key 枚举是权威数据源
- ❌ **PowerNukkit**：本地缓存的 `pnx-*.yml` 均为 404（抓取失败），需执行时重新调研
- ✅ **SpongeForge**：基于 Sponge `global.conf` + 配置注释中标注的 "Only affects SpongeVanilla" / "Forge native" 差异点

### ServerConfigDescriptor 结构（已确认）
位于 `/workspace/src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs` 第 22-58 行：
- `Key`（required string）— 配置项键名，点号扁平化路径
- `ConfigFileName`（required string）— 所属配置文件名
- `DisplayName`（required string）— 中文显示名
- `Description`（required string）— 中文详细描述
- `Category`（required string）— 功能分类
- `DefaultValue`（string?）— 默认值字符串
- `MinValue` / `MaxValue`（int?）— 数值范围约束
- `AllowedValues`（string[]?）— 枚举允许值
- `RegexPattern`（string?）— 正则验证
- `ValueType`（string，默认 "string"）— bool/int/string/enum/list/路径
- `RequiresRestart`（bool）— 是否需重启

## 翻译规范（沿用 Nukkit 已建立的标准）

1. **小白友好**：让从未开过服的人也能看懂
2. **枚举值翻译**：在说明列标注每个枚举值的中文含义
3. **键名不翻译**：保持英文键名不变（代码依赖）
4. **值类型标注**：bool / int / string / enum / list / 路径
5. **取值范围明确**：数值标 min-max，枚举列出所有可选值
6. **重启标注**：✅ 需重启 / 🔄 可热重载
7. **说明详尽**：解释做什么、为什么、改了的影响、坑
8. **Markdown 表格列**：`| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |`
9. **C# Description 字段**：用 `\n` 换行，枚举值用 `值 = 中文` 格式列出
10. **文件名冲突处理**：用核心前缀区分（如 Nukkit 用 `nukkit-server.properties`，SpongeForge 用 `spongeforge-global.conf`）

---

## 实施方案

### 任务 1：PowerNukkit（35-powernukkit.md + RegisterPowerNukkitYml.cs）

#### 1.1 调研
- 通过 GitHub MCP（`get_file_contents`）获取 PowerNukkitX 仓库（`PowerNukkitX/PowerNukkitX`）的默认配置文件：
  - `src/main/resources/lang/eng/nukkit.yml`（继承自 Nukkit 的基础配置）
  - `powernukkit.yml` 或 `default-powernukkit.yml`（PowerNukkitX 独有扩展配置，可能在 `src/main/resources/` 或 `src/test/resources/` 下）
  - 基岩版 `server.properties`（与 Nukkit 基本一致，但需核实 PowerNukkitX 是否有额外字段）
- 用 `search_code` 搜索 `powernukkit.yml` 定位实际文件路径
- 核实 PowerNukkitX 相比 Nukkit 新增的配置项（如新的反作弊、性能优化、API 扩展等）

#### 1.2 文档结构（35-powernukkit.md）
```markdown
# PowerNukkit 服务器配置文件中文手册
> PowerNukkit 是 Nukkit 的活跃 fork（PowerNukkitX），修复大量 bug 并扩展功能。
> 协议：基岩版 RakNet（UDP）
> 官方 GitHub：https://github.com/PowerNukkitX/PowerNukkitX
## ⚠️ 重要：与 Nukkit 的关系
（说明 PowerNukkitX 继承 Nukkit 全部配置，并新增 powernukkit.yml 独有配置）
## 配置文件清单
| 文件名 | 格式 | 来源 | 说明 |
（列出 nukkit.yml / powernukkit.yml / server.properties）
## nukkit.yml（继承自 Nukkit）
> 与 Nukkit 完全一致，详见 [Nukkit 手册](34-nukkit.md)。此处仅列出 PowerNukkitX 修改的默认值或新增项。
## powernukkit.yml（PowerNukkitX 独有配置）
### 1. xxx 节
| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
## server.properties（基岩版基础设置）
> 与 Nukkit 一致，详见 [Nukkit 手册](34-nukkit.md)
## 配置示例
## 优化建议
## 参考链接
```

#### 1.3 C# 补丁（RegisterPowerNukkitYml.cs）
- 注册 `powernukkit.yml` 的所有独有配置项
- 对 `nukkit.yml` 中 PowerNukkitX 修改了默认值或新增的项，用文件名 `powernukkit.yml` 注册（避免与 Nukkit 的 `nukkit.yml` 描述符冲突）
- 基岩版 server.properties 用文件名 `powernukkit-server.properties`（区分 Nukkit 的 `nukkit-server.properties`）
- 仅注册 PowerNukkitX **独有或修改**的项，不重复注册与 Nukkit 完全相同的项（已在 RegisterNukkitYml.cs 中注册）

---

### 任务 2：Glowstone（36-glowstone.md + RegisterGlowstoneConfig.cs）

#### 2.1 数据源
- 权威数据：`/tmp/cfgs/glowstone-ServerConfig.java` 的 Key 枚举（第 392-578 行）
- 该枚举按分类定义了全部配置键：
  - **server**：SERVER_IP、SERVER_PORT、SERVER_NAME、LOG_FILE、ONLINE_MODE、MAX_PLAYERS、WHITELIST、MOTD、SHUTDOWN_MESSAGE、ALLOW_CLIENT_MODS、DNS_OVERRIDES
  - **console**：USE_JLINE、CONSOLE_PROMPT、CONSOLE_DATE、CONSOLE_LOG_DATE
  - **game**：GAMEMODE、FORCE_GAMEMODE、DIFFICULTY、HARDCORE、PVP_ENABLED、MAX_BUILD_HEIGHT、ALLOW_FLIGHT、ENABLE_COMMAND_BLOCK、RESOURCE_PACK、RESOURCE_PACK_HASH、SNOOPER_ENABLED、PREVENT_PROXY
  - **creatures**：SPAWN_MONSTERS/ANIMALS/NPCS、MONSTER_LIMIT/ANIMAL_LIMIT/WATER_ANIMAL_LIMIT/AMBIENT_LIMIT/WATER_AMBIENT_LIMIT、各 ticks
  - **folders**：PLUGIN_FOLDER、UPDATE_FOLDER、WORLD_FOLDER、LIBRARIES_FOLDER
  - **files**：PERMISSIONS_FILE、COMMANDS_FILE、HELP_FILE
  - **advanced**：CONNECTION_THROTTLE、PLAYER_IDLE_TIMEOUT、WARN_ON_OVERLOAD、EXACT_LOGIN_LOCATION、PLUGIN_PROFILING、WARNING_STATE、COMPRESSION_THRESHOLD、PROXY_SUPPORT、PLAYER_SAMPLE_COUNT、GRAPHICS_COMPUTE、REGION_CACHE_SIZE、REGION_COMPRESSION、PROFILE_LOOKUP_TIMEOUT、SUGGEST_PLAYER_NAMES、MAX_WORLD_SIZE
  - **extras**：QUERY_ENABLED、QUERY_PORT、QUERY_PLUGINS、RCON_ENABLED、RCON_PASSWORD、RCON_PORT、RCON_COLORS
  - **world**：LEVEL_NAME、LEVEL_SEED、LEVEL_TYPE、SPAWN_RADIUS、VIEW_DISTANCE、GENERATE_STRUCTURES、ALLOW_NETHER、ALLOW_END、PERSIST_SPAWN、POPULATE_ANCHORED_CHUNKS、WATER_CLASSIC、DISABLE_GENERATION
  - **libraries**：LIBRARY_CHECKSUM_VALIDATION、LIBRARY_REPOSITORY_URL、LIBRARY_DOWNLOAD_ATTEMPTS、COMPATIBILITY_BUNDLE、LIBRARIES_LIST

#### 2.2 文档结构（36-glowstone.md）
```markdown
# Glowstone 服务器配置文件中文手册
> Glowstone 是独立的 Bukkit API 实现，从零编写，不依赖 CraftBukkit 代码。
> 特点：将 server.properties + bukkit.yml 合并为单一 glowstone.yml
> 官方 GitHub：https://github.com/GlowstoneMC/Glowstone
## 配置文件清单
| 文件名 | 格式 | 来源 | 说明 |
（glowstone.yml 为主，另有 permissions.yml / commands.yml / help.yml）
## glowstone.yml（Glowstone 唯一主配置）
> 说明：Glowstone 将原版 server.properties 和 bukkit.yml 的所有设置合并到 glowstone.yml，
> 由 ServerConfig.java 的 Key 枚举定义，首次启动时自动生成。
### 阅读约定
### 1. server（服务器基础）
| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
### 2. console（控制台）
### 3. game（游戏设置）
### 4. creatures（生物生成）
### 5. folders（目录）
### 6. files（文件）
### 7. advanced（高级）
### 8. extras（Query / RCON）
### 9. world（世界）
### 10. libraries（库）
## 配置示例（glowstone.yml 完整默认值）
## 优化建议
## 参考链接
```

#### 2.3 C# 补丁（RegisterGlowstoneConfig.cs）
- 配置文件名：`glowstone.yml`
- 按 Key 枚举的 10 个分类注册所有配置项
- 枚举值处理：GAMEMODE（SURVIVAL/CREATIVE/ADVENTURE/SPECTATOR）、DIFFICULTY（PEACEFUL/EASY/NORMAL/HARD）、LEVEL_TYPE（DEFAULT/FLAT/LARGE_BIOMES/AMPLIFIED 等）、COMPATIBILITY_BUNDLE（CRAFTBUKKIT/SPIGOT/PAPER）
- 数值范围：PORT（1-65535）、MAX_BUILD_HEIGHT（≥1）、VIEW_DISTANCE（≥1）等
- 预计约 60-70 个描述符

---

### 任务 3：Sponge（32-sponge.md + RegisterSpongeGlobalConf.cs）

#### 3.1 数据源
- 权威数据：`/tmp/cfgs/sponge-global.conf`（完整 697 行 HOCON 配置）
- 文件位于 `config/sponge/global.conf`，HOCON 格式，顶级 `sponge { ... }` 节
- 涵盖以下子节（按顺序）：
  1. **broken-mods**：broken-network-handler-mods（损坏模组网络处理器列表）
  2. **bungeecord**：ip-forwarding（BungeeCord IP 转发）
  3. **cause-tracker**：8 项（capture-async-spawning-entities、generate-stacktrace-per-phase、max-block-processing-depth、maximum-printed-runaway-counts、report-null-source-blocks-on-neighbor-notifications、resync-commands-from-async、verbose、verbose-errors）
  4. **commands**：aliases、command-hiding（hide-on-discovery-attempt、hide-on-execution-attempt）、enforce-permission-checks-on-non-sponge-commands、multi-world-patches（8 个子项）
  5. **debug**：concurrent-chunk-map-checks、concurrent-entity-checks、thread-contention-monitoring
  6. **entity**：collision-warn-size、entity-painting-respawn-delay、human-player-list-remove-delay、item-despawn-rate、living-hard-despawn-range、living-soft-despawn-minimum-life、living-soft-despawn-range、max-bounding-box-size、max-speed
  7. **entity-activation-range**：auto-populate、defaults（ambient/aquatic/creature/misc/monster）
  8. **entity-collisions**：auto-populate、max-entities-within-aabb
  9. **exploits**：book-size-total-multiplier、filter-invalid-entities-on-chunk-save、limit-book-size、load-chunk-on-position-set、mark-chunks-as-dirty-on-entity-list-modification、max-book-page-size、prevent-creative-itemstack-name-exploit、sync-player-positions-for-vehicle-movement、update-tracked-chunk-on-entity-move
  10. **general**：check-file-when-saving-sponge-data-file、config-dir、file-io-thread-sleep、plugins-dir
  11. **ip-sets**：（空映射，按需添加）
  12. **logging**：14 项日志开关（block-break/modify/place/populate/tracking、chunk-gc-queue-unload/load/unload、entity-collision-checks/death/despawn/spawn/speed-removal、exploit-*、log-stacktraces、transaction-merge-fail、world-auto-save）
  13. **metrics**：global-state、plugin-states
  14. **modules**：11 项模块开关（broken-mod、bungeecord、entity-activation-range、entity-collisions、exploits、movement-checks、optimizations、realtime、tileentity-activation、timings、tracking）
  15. **movement-checks**：moved-wrongly、player-moved-too-quickly、player-vehicle-moved-too-quickly
  16. **optimizations**：async-lighting（enabled/num-threads）、cache-tameable-owners、disable-failing-deserialization-log-spam、disable-pathfinding-chunk-loads、disable-raytracing-chunk-loads、drops-pre-merge、eigen-redstone（enabled/vanilla-decrement/vanilla-search）、enchantment-helper-leak-fix、faster-thread-checks、map-optimization、optimize-hoppers、panda-redstone、structure-saving、use-active-chunks-for-collisions
  17. **permission**：forge-permissions-handler
  18. **player-block-tracker**：block-blacklist、enabled
  19. **spawner**：spawn-limit-*（4 项）、tick-rate-*（4 项）
  20. **sql**：aliases
  21. **teleport-helper**：force-blacklist、unsafe-body-block-ids、unsafe-floor-block-ids
  22. **tileentity-activation**：auto-populate、default-block-range、default-tick-rate
  23. **timings**：enabled、hidden-config-entries、history-interval、history-length、server-name-privacy、verbose
  24. **world**：auto-player-save-interval、auto-save-interval、chunk-gc-load-threshold、chunk-gc-tick-interval、chunk-unload-delay、deny-chunk-requests、deny-neighbor-notification-chunk-requests、gameprofile-lookup-task-interval、generate-spawn-on-load、invalid-lookup-uuids、item-merge-radius、keep-spawn-loaded、leaf-decay、load-on-startup、max-chunk-unloads-per-tick、mob-spawn-range、portal-agents、pvp-enabled、view-distance、weather-ice-and-snow、weather-thunder、world-enabled
  25. **world-generation-modifiers**：（空列表）

#### 3.2 文档结构（32-sponge.md）
```markdown
# Sponge 服务器配置文件中文手册
> Sponge 是独立的 Minecraft 服务端 API 平台，提供 SpongeAPI 插件体系。
> 配置格式：HOCON（人类优化的 JSON 超集）
> 官方文档：https://docs.spongepowered.org/
## ⚠️ HOCON 格式说明
（简介 HOCON 语法：点号路径、大括号嵌套、字符串引号等）
## 配置文件清单
| 文件名 | 格式 | 来源 | 说明 |
（global.conf 为主，另有 world.conf / trackers.conf 等）
## global.conf（Sponge 全局配置）
> 位于 config/sponge/global.conf，HOCON 格式，顶级 sponge { } 节
### 阅读约定
- 键名用点号路径表示层级，如 sponge.modules.timings
- HOCON 布尔：true/false
- HOCON 列表：[a, b, c]
### 1. broken-mods（损坏模组修复）
### 2. bungeecord（代理转发）
### 3. cause-tracker（原因追踪）
...（按上述 25 节逐一列表）
## 配置示例（global.conf 关键默认值）
## 优化建议
## 参考链接
```

#### 3.3 C# 补丁（RegisterSpongeGlobalConf.cs）
- 配置文件名：`sponge-global.conf`
- HOCON 键名用点号路径：如 `sponge.modules.timings`、`sponge.entity.item-despawn-rate`、`sponge.optimizations.async-lighting.enabled`
- 预计约 90-110 个描述符（覆盖所有有实际默认值的叶子配置项，跳过空映射如 `aliases {}`、`ip-sets {}`）

---

### 任务 4：SpongeForge（33-spongeforge.md + RegisterSpongeForgeConf.cs）

#### 4.1 数据源与差异点
- SpongeForge = Sponge API 运行在 Forge 之上，使用**相同的 `global.conf`** 配置文件
- 关键差异（从 global.conf 注释中提取）：
  1. **exploits 节**：多项标注 "Only affects SpongeVanilla"（limit-book-size、load-chunk-on-position-set、mark-chunks-as-dirty-on-entity-list-modification、sync-player-positions-for-vehicle-movement、update-tracked-chunk-on-entity-move）——这些在 SpongeForge 中**由 Forge 原生处理**，Sponge 的配置不生效
  2. **optimizations.enchantment-helper-leak-fix**：注释说明 "Forge native has a similar patch" —— SpongeForge 中 Forge 已有类似修复
  3. **permission.forge-permissions-handler**：仅 SpongeForge 相关（SpongeVanilla 无 Forge 权限）
  4. **cause-tracker**：主要用于处理模组兼容性问题，SpongeForge 场景更常见
  5. **broken-mods**：仅模组服（SpongeForge）会用到，SpongeVanilla 无模组
  6. **invalid-lookup-uuids**：注释明确 "If you are using SpongeForge, make sure to enter any mod fake player's UUID"
- 额外配置：SpongeForge 还涉及 Forge 的 `forge-server.toml`（已在 RegisterForgeServerToml.cs 中注册，本文档引用）

#### 4.2 文档结构（33-spongeforge.md）
```markdown
# SpongeForge 服务器配置文件中文手册
> SpongeForge 是 Sponge API 在 Forge 上的实现，兼容 Forge 模组与 Sponge 插件。
> 继承关系：Forge + SpongeAPI = SpongeForge
> 官方 GitHub：https://github.com/SpongePowered/SpongeForge
## ⚠️ SpongeForge 与 SpongeVanilla 的区别
（说明两者共用 global.conf，但部分配置项行为不同）
## 配置文件清单
| 文件名 | 格式 | 来源 | 说明 |
（global.conf + forge-server.toml + server.properties）
## global.conf（Sponge 全局配置）
> 与 Sponge 共用，详见 [Sponge 手册](32-sponge.md)。本文档仅标注 SpongeForge 特有差异。
### SpongeForge 配置差异一览
| 键名 | 差异说明 | 原因 |
（列出所有 "Only affects SpongeVanilla" 项 + Forge native 项 + 模组相关项）
### 1. broken-mods（模组兼容修复）—— SpongeForge 重点
### 2. cause-tracker（模组原因追踪）—— SpongeForge 重点
### 3. exploits（漏洞修复）—— 标注哪些仅 SpongeVanilla 生效
### 4. permission（Forge 权限）—— SpongeForge 独有
### 5. world.invalid-lookup-uuids（模组假玩家 UUID）
## forge-server.toml（Forge 服务端配置）
> 详见 [Forge 手册](18-forge.md)
## 配置示例
## 优化建议（针对模组服）
## 参考链接
```

#### 4.3 C# 补丁（RegisterSpongeForgeConf.cs）
- 配置文件名：`spongeforge-global.conf`（用前缀区分 Sponge 的 `sponge-global.conf`，避免描述符冲突）
- **仅注册 SpongeForge 有差异行为的配置项**，不重复注册与 Sponge 完全相同的项
- 重点注册：
  - `sponge.broken-mods.broken-network-handler-mods`（模组网络修复）
  - `sponge.permission.forge-permissions-handler`（Forge 权限处理器）
  - `sponge.world.invalid-lookup-uuids`（模组假玩家 UUID 列表）
  - `sponge.cause-tracker.*`（模组原因追踪，SpongeForge 场景关键）
  - exploits 节中标注 "Only affects SpongeVanilla" 的项（在 Description 中说明 SpongeForge 中由 Forge 原生处理）
- 预计约 20-30 个描述符（仅差异项）

---

## 实施顺序与 Todo

1. **PowerNukkit**（需联网调研，优先）
   - 调研 PowerNukkitX 仓库获取 powernukkit.yml
   - 编写 `35-powernukkit.md`
   - 编写 `RegisterPowerNukkitYml.cs`
2. **Glowstone**（数据已就绪）
   - 基于 ServerConfig.java Key 枚举编写 `36-glowstone.md`
   - 编写 `RegisterGlowstoneConfig.cs`
3. **Sponge**（数据已就绪）
   - 基于 global.conf 编写 `32-sponge.md`
   - 编写 `RegisterSpongeGlobalConf.cs`
4. **SpongeForge**（依赖 Sponge 完成）
   - 基于 Sponge 差异编写 `33-spongeforge.md`
   - 编写 `RegisterSpongeForgeConf.cs`

## 假设与决策

1. **文件名冲突处理**：
   - PowerNukkit 的基岩版 server.properties 用 `powernukkit-server.properties`（区分 `nukkit-server.properties`）
   - SpongeForge 的 global.conf 用 `spongeforge-global.conf`（区分 Sponge 的 `sponge-global.conf`）
   - 沿用 Nukkit 已建立的前缀区分模式

2. **避免重复注册**：
   - PowerNukkit 仅注册独有/修改项，不重复注册与 Nukkit 完全相同的 nukkit.yml 项
   - SpongeForge 仅注册差异项，不重复注册与 Sponge 完全相同的 global.conf 项
   - Glowstone 完整注册全部项（因为 glowstone.yml 是独立文件，不与 server.properties/bukkit.yml 共用文件名）

3. **HOCON 键名表示**：用点号扁平化路径，如 `sponge.modules.timings.enabled`，与 YAML 处理方式一致

4. **Glowstone 枚举值**：
   - GAMEMODE：SURVIVAL/CREATIVE/ADVENTURE/SPECTATOR（注意 Glowstone 支持旁观模式，与基岩版不同）
   - DIFFICULTY：PEACEFUL/EASY/NORMAL/HARD
   - LEVEL_TYPE：DEFAULT/FLAT/LARGE_BIOMES/AMPLIFIED（Java 版全集）
   - COMPATIBILITY_BUNDLE：CRAFTBUKKIT/SPIGOT/PAPER

5. **PowerNukkit 调研后备方案**：若 GitHub MCP 无法获取 powernukkit.yml，使用 WebSearch + WebFetch 从 PowerNukkitX 文档或 wiki 获取配置项列表

6. **空映射/列表处理**：跳过 global.conf 中无实际默认值的空映射（如 `aliases {}`、`ip-sets {}`、`auto-fix-null-source-block-providing-tile-entities {}`），不注册为描述符

## 验证步骤

1. **文件完整性**：确认 8 个文件均已创建：
   - `/workspace/docs/server-cores/35-powernukkit.md`
   - `/workspace/docs/server-cores/36-glowstone.md`
   - `/workspace/docs/server-cores/32-sponge.md`
   - `/workspace/docs/server-cores/33-spongeforge.md`
   - `/workspace/docs/server-cores/_patches/RegisterPowerNukkitYml.cs`
   - `/workspace/docs/server-cores/_patches/RegisterGlowstoneConfig.cs`
   - `/workspace/docs/server-cores/_patches/RegisterSpongeGlobalConf.cs`
   - `/workspace/docs/server-cores/_patches/RegisterSpongeForgeConf.cs`
2. **配置项统计**：统计每个核心的配置项数量，列在最终报告中
3. **格式一致性**：检查 Markdown 表格列名、C# 描述符字段与 Nukkit 模板一致
4. **键名真实性**：确认所有配置键名来自实际源码/配置文件，未凭空编造
5. **文件名无冲突**：确认 C# 补丁中 ConfigFileName 不与已有描述符冲突
