# 5 个混合端核心配置文件中文翻译 —— 实施计划

## 任务概要

为 5 个 Minecraft 混合端核心（Mohist、Arclight、CatServer、Magma、Banner）的**原生专属**配置文件编写中文翻译手册，每个核心产出两份成果物：
- 一份 Markdown 文档（位于 `/workspace/docs/server-cores/`）
- 一份 C# `Register*` 方法代码片段（位于 `/workspace/docs/server-cores/_patches/`）

## 当前状态分析

### 已完成
- `/workspace/docs/server-cores/_patches/` 目录已存在（含 Forge/NeoForge/Fabric/Quilt/Nukkit 5 份示例）
- 已下载 Mohist 完整源码（`/tmp/MohistConfig.java`，含全部 ~50 个配置项的 `getXxx(path, default)` 调用）
- 已下载 Magma 完整源码（`/tmp/MagmaConfig.java`，含 ~40 个 `BooleanValue/IntValue/StringValue/StringArrayValue` 声明）
- 已确认 CatServer 配置文件名为 `catserver.yml`，基于 `CatServerConfig.java`
- 已确认 Arclight 配置文件为 `arclight.conf`（HOCON 格式），含 optimization / compatibility / async-catcher / messages 等节点
- 已确认 Banner 项目仍位于 `github.com/MohistMC/Banner`（Fabric + Bukkit 混合端，区别于 Mohist 的 Forge+Bukkit）

### 格式参考（已读取确认）
- Markdown 格式参考：`/workspace/docs/server-cores/07-pufferfish.md`（Pufferfish 手册）
  - 结构：标题 + 继承关系引言 + 配置文件清单表 + 阅读约定 + 分节表格（键名/中文含义/类型/默认值/需重启/说明）+ 配置示例 + 优化建议
  - 翻译规范：键名不翻译、值类型标注（bool/int/string/string[]/enum）、取值范围标在默认值括号内、需重启用 ✅/🔄 标记
- C# 格式参考：`/workspace/docs/server-cores/_patches/RegisterNeoForgeYml.cs`、`RegisterForgeServerToml.cs`、`RegisterNukkitYml.cs`
  - 结构：文件头注释块（文件名/功能描述/配置文件/来源核心/适用版本/数据来源/集成位置）+ `private void Register*()` 方法 + 多个 `Register(new ServerConfigDescriptor { ... })` 调用
  - ServerConfigDescriptor 字段：Key、ConfigFileName、DisplayName、Description、Category、DefaultValue、AllowedValues（可选）、MinValue/MaxValue（可选）、ValueType、RequiresRestart

## 待创建的 10 个文件

### 1. Mohist（Forge + Bukkit 混合端，1.20.1）
- `/workspace/docs/server-cores/27-mohist.md`
- `/workspace/docs/server-cores/_patches/RegisterMohistConfigYml.cs`
- **配置文件**：`mohist-config/mohist.yml`（YAML，~50 项）
- **数据来源**：`/tmp/MohistConfig.java`（已完整读取，1.20.1 版本）
- **主要配置分组**：
  - `mohist.*`（show_logo, lang, check_update, ping_status_version, watchdog_spigot, watchdog_mohist）
  - `anvilfix.*`（maximumrepaircost, enchantment_fix, max_enchantment_level）
  - `player_modlist_blacklist.*`（enable, list）
  - `server_modlist_whitelist.*`（enable, list）
  - `custom.*`（max-bees-in-hive, no_villager, entity_tp_end, entity_tp_nether, raid_no_emerald, lava_speed.normal, lava_speed.nether）
  - `enchantment-table-book-animation-tick`
  - `networkmanager.*`（debug, intercept）
  - `keepinventory.*`（global.enable, global.inventory, permission.enable, permission.inventory, global.exp, permission.exp）
  - `threadpriority.server_thread`
  - `entity.clear.*`（enable, time, countdown.msg, item.enable, item.whitelist, item.msg, noitem.enable, noitem.whitelist, noitem.msg）
  - `ban.*`（item.enable, item.list, entity.enable, entity.list, enchantment.enable, enchantment.list）
  - `motd.*`（enable, firstline, secondline）
  - `settings.messages.ping-command-output`
  - `events.*`（fire_tick, explosion）
  - `forge.bukkitpermissionshandler`
  - `worldmanage`
  - `velocity.*`（enabled, onlineMode, secret）
  - `recipe.warn`
  - `tpa.*`、`back.*`、`permissions.debug.*`
  - `world.async_save`
  - `message.require_forge`、`server_mod_name`
  - `deepseek.*`（enable, apikey, model, system, command, chatfromat）

### 2. Arclight（Forge + Bukkit 混合端，1.20.1）
- `/workspace/docs/server-cores/28-arclight.md`
- `/workspace/docs/server-cores/_patches/RegisterArclightYml.cs`（实际配置文件为 `arclight.conf`，HOCON 格式，但保持任务要求的命名）
- **配置文件**：`arclight.conf`（HOCON，~20 项）
- **数据来源**：ArclightConfig.java + ConfigSpec/OptimizationSpec/CompatSpec/AsyncCatcherSpec（GitHub: IzzelAliz/Arclight）
- **主要配置分组**：
  - `optimization.*`（fast-radius-search, skip-entity-activation-check, skip-tick-events, cache-ChunkPos, disabled-flow-control 等）
  - `compatibility.*`（enchantment-level, fake-player-permission 等）
  - `async-catcher.*`（enabled, world, player, entity, block, chunk 等）
  - `messages.*`（log-level, color 等）
  - 顶级项：`disable-ansi-color`, `mixin.color` 等
- **⚠️ 待执行**：执行期需重新从 GitHub 拉取 1.20.1 分支的 ArclightConfig.java 及其引用的 Spec 类，确认完整配置项清单与默认值

### 3. CatServer（Forge + Bukkit + Spigot，1.12.2/1.16.5/1.18.2）
- `/workspace/docs/server-cores/29-catserver.md`
- `/workspace/docs/server-cores/_patches/RegisterCatServerYml.cs`
- **配置文件**：`catserver.yml`（YAML，~15 项）
- **数据来源**：CatServerConfig.java（GitHub: CatServer/CatServer）
- **主要配置项**：
  - `world.keepSpawnInMemory`（bool, true）
  - `world.forceSaveOnWatchdog`（bool, true）
  - `fakePlayer.permissions`（string[], ["essentials.build"]）
  - `fakePlayer.eventPass`（bool, false）
  - `plugin.patcher.enableDynmapCompatible`（bool, true）
  - `plugin.patcher.enableWorldEditCompatible`（bool, true）
  - `plugin.patcher.enableEssentialsNewVersionCompatible`（bool, true）
  - `versionCheck`（bool, true）
  - `disableAsyncCatchWarn`（bool, false）
  - `worldGenMaxTickTime`（int, 15）
  - `enable-mod-player-permission-bypass`（bool, false）
  - `disable-custom-packet-flood-protection`（bool, true）
- **⚠️ 待执行**：执行期需重新拉取 CatServerConfig.java 核实最新字段，因为不同分支（1.12.2/1.16.5/1.18.2）字段略有差异，本计划以 1.16.5 为主，并在文档中说明各版本差异

### 4. Magma（Forge + Bukkit，1.18.2/1.20.1）
- `/workspace/docs/server-cores/30-magma.md`
- `/workspace/docs/server-cores/_patches/RegisterMagmaConf.cs`（实际配置文件为 `magma.yml`，但保持任务要求的命名）
- **配置文件**：`magma.yml`（YAML，~40 项）
- **数据来源**：`/tmp/MagmaConfig.java`（已完整读取）
- **主要配置分组**：
  - `debug.*`（debugPrintBukkitMatterials, debugPrintBukkitBannerPatterns, debugPrintCommandNode, debugPrintBiomes, debugPrintSounds）
  - `console.colour.level.*`、`console.colour.message.*`、`console.colour.time.*`（每个 5 项：error/warning/info/fatal/trace，共 15 项）
  - `forge.blacklistedmods.*`（enabled, list, kickmessage）
  - `experience-merge-max-value`（int, -1）
  - `auto-unload-dimensions`（bool, true）
  - `hide-dimension-load-unload`（bool, false）
  - `respawn-in-other-dim`（bool, true）
  - `fakeplayer.permissions`（string[], []）
  - `forge.bukkitPermissionHandler.enable`（bool, true）
  - `magma.auto-update`（bool, true）
  - `magma.advanced.*`（override-name, override-name-string, override-brand, override-brand-string, tooltip-priority, server-type, forge-bukkit-access, fastbench-fix）
  - `bukkit.*`（max-potion-effect-amount, enable-reload）
  - `magma.messages.fml.*`（fml-required, missing-mods, server-still-starting）
  - `forge.autoUnloadDimensionsWhitelist`（int[], [0]）

### 5. Banner（Fabric + Bukkit 混合端，1.20.1）
- `/workspace/docs/server-cores/31-banner.md`
- `/workspace/docs/server-cores/_patches/RegisterBannerYml.cs`
- **配置文件**：`banner.yml`（YAML，~10-15 项）
- **数据来源**：GitHub `MohistMC/Banner`（注意：Banner 是 **Fabric + Bukkit** 混合端，与 Mohist 的 Forge 体系完全不同）
- **⚠️ 待执行**：执行期需克隆 `MohistMC/Banner` 仓库的 1.20.1 分支，定位 Banner 主配置类（如 `BannerConfig.java` 或 `BannerModConfig.java`），下载完整配置文件后再翻译
- **已知大致配置项**（需执行期核实）：banner 配置项通常较少，可能涉及 fabric mod 兼容性、bukkit api 行为开关等

## 翻译规范（严格遵守，参考 07-pufferfish.md）

1. **小白友好**：每项都从「这是什么、改了会怎样、什么时候要改」三个角度写说明
2. **枚举值也翻译**：如 `SILENCED`（静默）、`DEV_SHORT`（开发短格式）
3. **键名不翻译**：保持点号扁平化路径（如 `mohist.show_logo`）
4. **值类型标注**：`bool` / `int` / `string` / `string[]` / `enum` / `int[]`
5. **取值范围明确**：标在默认值括号内（如 `40`（≥ 0））
6. **重启标注**：✅ 必须重启 / 🔄 支持热重载（用 `/mohist reload` 等命令）
7. **说明要详尽**：包含游戏机制影响、性能权衡、模组兼容性注意事项

## C# 代码片段规范（参考 RegisterNeoForgeYml.cs）

- 文件头注释块：文件名、功能描述、配置文件路径、来源核心 GitHub、适用版本、数据来源、集成位置
- 私有方法 `private void Register*Yml()`
- 每个配置项一个 `Register(new ServerConfigDescriptor { ... })` 调用
- ServerConfigDescriptor 字段：
  - `Key`：完整路径键名（如 `"mohist.show_logo"`）
  - `ConfigFileName`：配置文件名常量（如 `"mohist.yml"`）
  - `DisplayName`：中文显示名
  - `Description`：中文详细说明（含 \n 换行）
  - `Category`：中文分类（如 `"基础设置"`）
  - `DefaultValue`：默认值字符串
  - `AllowedValues`：枚举/布尔可选值（数组）
  - `MinValue`/`MaxValue`：int 类型范围（可选）
  - `ValueType`：`"bool"` / `"int"` / `"string"` / `"string[]"` / `"enum"`
  - `RequiresRestart`：bool

## 实施步骤（按依赖顺序）

### 步骤 1：补全 Arclight / CatServer / Banner 的源码核实
- WebFetch Arclight 1.20.1 分支 `arclight-server/src/main/java/io/izzel/arclight/.../ArclightConfig.java` 及关联 Spec 类
- WebFetch CatServer 1.16.5 分支 `src/main/java/catserver/server/CatServerConfig.java`
- WebFetch Banner `MohistMC/Banner` 仓库的 Banner 配置类
- 如 WebFetch 受限，则用 `RunCommand` 的 `curl` 下载 raw 文件（带 User-Agent header）

### 步骤 2：编写 Mohist 文档与 C# 代码（数据已齐备）
- 写 `/workspace/docs/server-cores/27-mohist.md`（按 07-pufferfish.md 格式，~50 项配置分 10+ 个分组表）
- 写 `/workspace/docs/server-cores/_patches/RegisterMohistConfigYml.cs`（~50 个 Register 调用）

### 步骤 3：编写 Magma 文档与 C# 代码（数据已齐备）
- 写 `/workspace/docs/server-cores/30-magma.md`（~40 项配置，console.colour 颜色项可合并说明）
- 写 `/workspace/docs/server-cores/_patches/RegisterMagmaConf.cs`（~40 个 Register 调用）

### 步骤 4：编写 Arclight 文档与 C# 代码（步骤 1 完成后）
- 写 `/workspace/docs/server-cores/28-arclight.md`
- 写 `/workspace/docs/server-cores/_patches/RegisterArclightYml.cs`

### 步骤 5：编写 CatServer 文档与 C# 代码（步骤 1 完成后）
- 写 `/workspace/docs/server-cores/29-catserver.md`
- 写 `/workspace/docs/server-cores/_patches/RegisterCatServerYml.cs`

### 步骤 6：编写 Banner 文档与 C# 代码（步骤 1 完成后）
- 写 `/workspace/docs/server-cores/31-banner.md`
- 写 `/workspace/docs/server-cores/_patches/RegisterBannerYml.cs`
- 在文档开头注明 Banner 是 Fabric+Bukkit 混合端（区别于 Mohist/Magma/CatServer 的 Forge 系）

### 步骤 7：最终验证与统计
- 用 LS 确认 10 个文件全部创建
- 用 Read 抽查每个 Markdown 文档的格式一致性
- 统计每个核心的配置项数量，输出最终报告

## 假设与决策

1. **版本选择**：
   - Mohist：1.20.1（最新稳定主分支）
   - Arclight：1.20.1（最新主分支）
   - CatServer：1.16.5（最广泛使用版本，文档中说明 1.12.2/1.18.2 差异）
   - Magma：1.20.1（最新主分支，源码已下载）
   - Banner：1.20.1（唯一稳定版本）

2. **配置文件命名**：C# 文件名严格按任务要求（RegisterMohistConfigYml.cs / RegisterArclightYml.cs / RegisterCatServerYml.cs / RegisterMagmaConf.cs / RegisterBannerYml.cs），即使实际配置文件格式不同（如 Arclight 是 .conf，Magma 是 .yml 而非 .conf）也保持任务要求的命名。但在 C# 文件内部的 `ConfigFileName` 字段中使用真实文件名（如 `"arclight.conf"`、`"magma.yml"`）。

3. **配置范围**：仅翻译各核心**原生独有**的配置文件（如 `mohist.yml`、`arclight.conf`、`catserver.yml`、`magma.yml`、`banner.yml`），不翻译继承自 Vanilla/Spigot/Paper/Forge 的通用配置（如 server.properties、bukkit.yml、spigot.yml、paper-world.yml、forge-common.toml 等）。

4. **重启标记约定**：混合端核心的配置大多在启动时一次性读取，不支持热重载，因此绝大多数项标记为 ✅（需重启）。仅少数核心支持 `/mohist reload` 类热重载命令的项才标 🔄。

5. **混合端兼容性说明**：每个 Markdown 文档的开头引言部分需说明该核心支持的 mod 加载器（Forge 或 Fabric）以及与 Bukkit/Spigot 插件的兼容性注意事项。

## 验证步骤

1. `ls -la /workspace/docs/server-cores/2[789]-*.md /workspace/docs/server-cores/3[01]-*.md` 应列出 5 个 md 文件
2. `ls -la /workspace/docs/server-cores/_patches/Register*Mohist* /workspace/docs/server-cores/_patches/Register*Arclight* /workspace/docs/server-cores/_patches/Register*CatServer* /workspace/docs/server-cores/_patches/Register*Magma* /workspace/docs/server-cores/_patches/Register*Banner*` 应列出 5 个 cs 文件
3. 每个 Markdown 文件应包含：标题、引言、配置文件清单表、阅读约定、至少 1 个配置项分组表格、配置示例、参考来源
4. 每个 C# 文件应包含：文件头注释、`private void Register*()` 方法签名、与配置项数量一致的 `Register(new ServerConfigDescriptor{...})` 调用
5. 配置项数量统计（预期）：
   - Mohist：~50 项
   - Arclight：~20 项（待核实）
   - CatServer：~12 项（待核实）
   - Magma：~40 项
   - Banner：~10-15 项（待核实）

## 风险与缓解

1. **GitHub API 限流**：使用 `curl` + `User-Agent` header 访问 raw.githubusercontent.com，避免 API 限流
2. **配置项版本漂移**：在文档中明确标注版本号，并在参考来源处给出具体 commit/分支链接
3. **Banner 项目状态不确定**：执行期第一步优先核实 Banner 仓库是否存在、最新分支、配置类位置；若仓库已迁移或重命名，在文档中如实说明
4. **Arclight HOCON 格式**：Markdown 文档示例代码块用 HOCON 语法（不是 YAML），C# 的 `ConfigFileName` 用 `arclight.conf`
