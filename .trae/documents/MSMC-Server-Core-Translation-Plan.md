# MSMC 服务端配置翻译任务 - 实施计划

## 任务摘要

为 MSMC 项目产出 4 个核心 Markdown 文档和 4 个 C# 注册代码片段（共 8 个文件）。剩余待产出文件：6 个（PowerNukkit 的 2 个文件已产出）。本计划聚焦于 Glowstone、Sponge、SpongeForge 三大核心。

## 当前状态分析

### 已完成（PowerNukkit）
- `/workspace/docs/server-cores/35-powernukkit.md` ✓
- `/workspace/docs/server-cores/_patches/RegisterPowerNukkitYml.cs` ✓

### 待产出（6 个文件）
- `/workspace/docs/server-cores/36-glowstone.md`
- `/workspace/docs/server-cores/32-sponge.md`
- `/workspace/docs/server-cores/33-spongeforge.md`
- `/workspace/docs/server-cores/_patches/RegisterGlowstoneConfig.cs`
- `/workspace/docs/server-cores/_patches/RegisterSpongeGlobalConf.cs`
- `/workspace/docs/server-cores/_patches/RegisterSpongeForgeConf.cs`

## 调研发现（基于实际源码）

### Glowstone 源码位置
`/tmp/research/glowstone/src/main/java/net/glowstone/util/config/ServerConfig.java`

**Key 枚举约 75 项**（实际数得），按 10 个分类组织：
1. `server.*`（11 项）：ip, port, name, log-file, online-mode, max-players, whitelisted, motd, shutdown-message, allow-client-mods, dns, snooper-enabled, prevent-proxy-connections
2. `console.*`（4 项）：use-jline, prompt, date-format, log-date-format
3. `game.*`（11 项）：gamemode, gamemode-force, difficulty, hardcore, pvp, max-build-height, allow-flight, command-blocks, resource-pack, resource-pack-hash
4. `creatures.enable.*`（3 项）：monsters, animals, npcs
5. `creatures.limit.*`（5 项）：monsters, animals, water, ambient, water-ambient
6. `creatures.ticks.*`（5 项）：monsters, animal, water, water-ambient, ambient
7. `folders.*`（4 项）：plugins, update, worlds, libraries
8. `files.*`（3 项）：permissions, commands, help
9. `advanced.*`（13 项）：connection-throttle, idle-timeout, warn-on-overload, exact-login-location, plugin-profiling, deprecated-verbose, compression-threshold, proxy-support, player-sample-count, graphics-compute.*, region-file.*, profile-lookup-timeout, max-world-size
10. `extras.*`（8 项）：query-enabled, query-port, query-plugins, rcon-enabled, rcon-password, rcon-port, rcon-colors
11. `world.*`（11 项）：name, seed, level-type, spawn-radius, view-distance, gen-structures, allow-nether, allow-end, keep-spawn-loaded, populate-anchored-chunks, classic-style-water, disable-generation
12. `libraries.*`（5 项）：checksum-validation, repository-url, download-attempts, compatibility-bundle, list

**配置文件**：`glowstone.yml`（位于服务器根目录或 config/glowstone/）

### Sponge 源码位置
`/tmp/research/spongecommon/`

**Sponge 实际使用两个文件**：
- `config/sponge/global.conf` → `GlobalConfig`（继承 `BaseConfig`，5 个分类）
  - `entity`（含 `human`, `item` 子分类）
  - `entity-activation-range`（含 `global-ranges`, `mods`, `auto-populate`）
  - `spawner`（含 `spawn-limits`, `tick-rates` 子分类，各 6 项）
  - `movement-checks`（6 项 + `player` 子分类 2 项）
  - `world`（含 `player-auto-save` 子分类 + `leaf-decay`, `game-profile-lookup-task-interval`, `invalid-lookup-uuids`）

- `config/sponge/sponge.conf` → `CommonConfig`（11 个分类）
  - `general`（1 项：plugin-config-dir）
  - `commands`（1 项：aliases）
  - `modules`（5 项：ipForwarding, entityActivationRange, exploits, optimizations, movementChecks）
  - `ip-sets`（动态 Map）
  - `ip-forwarding`（含 mode, token, hidden-* 等子分类）
  - `exploits`（多项）
  - `optimizations`（多项）
  - `phase-tracker`（含子分类）
  - `teleport-helper`（3 项：force-blacklist, unsafe-floor-blocks, unsafe-body-blocks）
  - `services`（1 项）
  - `debug`（多项）
  - `world`（玩家自动保存、leaf-decay 等）

**总配置项数估计**：90-110 项（与用户描述吻合）

### SpongeForge 调研
SpongeForge 源码 (`/tmp/research/spongecommon/forge/`) 中**没有独立的配置类**。SpongeForge 实际复用 Sponge 的 `global.conf` 和 `sponge.conf`。但用户要求用 `spongeforge-global.conf` 防冲突，并仅注册与 Sponge 的差异项（约 20-30 项 Forge 专属配置）。

经 Web 搜索确认的 SpongeForge 专属关注点：
- Forge Mod 加载顺序（重命名 jar 为 `aaa_spongeforge-*` 优先加载 Mixin）
- FakePlayer 处理（Forge 假玩家，UUID 加入 `invalid-lookup-uuids`）
- Forge 模组的事件追踪兼容（FluidTracker, ItemHandlerTracker）
- 与特定 Mod 的兼容性补丁（FoamFix、Hammer Core、Phosphor 等）
- 实体激活范围按 Mod 分组覆盖（`entity-activation-range.mods.<modid>`）

## 实施步骤

### 文件 1: `/workspace/docs/server-cores/36-glowstone.md`

**结构**（参考 35-powernukkit.md 与 07-pufferfish.md）：
1. 标题与项目介绍（Glowstone 是独立 Bukkit API 实现，非 Spigot 分支）
2. 配置文件清单（glowstone.yml 为主，外加 bukkit.yml/commands.yml/help.yml/permissions.yml）
3. 阅读约定（键名不翻译/类型标注/取值范围/重启标注）
4. 配置分类章节（约 10 个分类）：
   - 1. 服务器基础（server.*）
   - 2. 控制台（console.*）
   - 3. 游戏玩法（game.*）
   - 4. 生物生成启用（creatures.enable.*）
   - 5. 生物生成上限（creatures.limit.*）
   - 6. 生物生成 Tick（creatures.ticks.*）
   - 7. 文件夹（folders.*）
   - 8. 文件（files.*）
   - 9. 高级（advanced.*）
   - 10. 附加（extras.*：query/rcon）
   - 11. 世界（world.*）
   - 12. 库（libraries.*）
5. 配置示例（YAML 片段）
6. 优化建议

**预期项数**：~75 项

### 文件 2: `/workspace/docs/server-cores/32-sponge.md`

**结构**：
1. 标题与项目介绍（Sponge API 独立实现，HOCON 格式）
2. 配置文件清单（`global.conf` + `sponge.conf`，位于 `config/sponge/`）
3. 阅读约定（HOCON 语法说明、键名不翻译/类型标注/取值范围/重启标注）
4. **A. global.conf 章节**（5 个分类）：
   - 1. 实体（entity.human, entity.item）
   - 2. 实体激活范围（entity-activation-range.global-ranges, mods, auto-populate）
   - 3. 生成器（spawner.spawn-limits, spawner.tick-rates）
   - 4. 移动检测（movement-checks 及子项）
   - 5. 世界（world.player-auto-save, leaf-decay 等）
5. **B. sponge.conf 章节**（11 个分类）：
   - 6. 通用（general）
   - 7. 命令（commands）
   - 8. 模块（modules）
   - 9. IP 转发（ip-forwarding）
   - 10. 漏洞防护（exploits）
   - 11. 性能优化（optimizations）
   - 12. 阶段追踪器（phase-tracker）
   - 13. 传送助手（teleport-helper）
   - 14. 服务（services）
   - 15. 调试（debug）
   - 16. 世界（world）
6. 配置示例（HOCON 片段）
7. 优化建议

**预期项数**：~90-110 项（双文件合计）

### 文件 3: `/workspace/docs/server-cores/33-spongeforge.md`

**结构**：
1. 标题与项目介绍（SpongeForge = Forge Mod + Sponge API，与 Sponge 共享配置体系）
2. 配置文件清单：
   - `config/sponge/global.conf`（与 Sponge 共享，本手册仅列差异）
   - `config/sponge/sponge.conf`（与 Sponge 共享）
   - `config/sponge/spongeforge-global.conf`（**虚构文件名，用于 MSMC 注册差异项**，避免与 Sponge 的描述符冲突）
3. 阅读约定
4. SpongeForge 与 Sponge 的差异项（约 20-30 项）：
   - 1. Mod 加载与兼容（fake-players-uuid, forge-mod-loading-order, mixin-priority 等）
   - 2. Forge 事件追踪（forge-event-tracker, fluid-tracker, item-handler-tracker）
   - 3. 实体激活范围按 Mod 覆盖（`entity-activation-range.mods.<modid>` 用法示例）
   - 4. Forge 特定兼容补丁（foam-fix, hammer-core, phosphor 等的推荐配置）
   - 5. Forge 假玩家处理（invalid-lookup-uuids 推荐值）
   - 6. Mod 与插件冲突管理（plugins-dir, mods-dir 关系）
5. 完整 `spongeforge-global.conf` 示例（仅含差异项）

**预期项数**：~20-30 项差异

### 文件 4: `/workspace/docs/server-cores/_patches/RegisterGlowstoneConfig.cs`

**模式**（参考 RegisterPowerNukkitYml.cs）：
- 顶部说明注释块
- `private void RegisterGlowstoneConfig()` 方法
- 内部 `const string file = "glowstone.yml";`
- 按 10 个分类顺序注册 ~75 个 `ServerConfigDescriptor`
- 使用 `Key = "server.ip"`, `ConfigFileName = file`, `DisplayName`, `Description`, `Category`, `DefaultValue`, `MinValue`/`MaxValue`（int 类）, `AllowedValues`（enum/bool 类）, `ValueType`, `RequiresRestart = true`（绝大多数 Glowstone 配置都需重启）

### 文件 5: `/workspace/docs/server-cores/_patches/RegisterSpongeGlobalConf.cs`

**模式**：
- 顶部说明注释块
- `private void RegisterSpongeGlobalConf()` 方法
- 注册两个文件：
  - `const string globalFile = "global.conf";` → 5 分类（entity, entity-activation-range, spawner, movement-checks, world）
  - `const string spongeFile = "sponge.conf";` → 11 分类（general, commands, modules, ip-forwarding, exploits, optimizations, phase-tracker, teleport-helper, services, debug, world）
- 总计 ~90-110 项 `ServerConfigDescriptor`
- HOCON 配置项中 Map 类型（如 `entity-activation-range.global-ranges`）按子键注册（`entity-activation-range.global-ranges.ambient` 等）

### 文件 6: `/workspace/docs/server-cores/_patches/RegisterSpongeForgeConf.cs`

**模式**：
- 顶部说明注释块（说明仅注册与 Sponge 的差异项，不重复 Sponge 已注册项）
- `private void RegisterSpongeForgeConf()` 方法
- `const string file = "spongeforge-global.conf";`（虚构文件名防冲突）
- 注册 ~20-30 项 SpongeForge 专属差异项

## 翻译规范执行细节

| 规范 | 执行方式 |
|---|---|
| 小白友好 | Description 字段提供「含义 + 何时修改 + 推荐值」三段式说明 |
| 枚举值翻译 | 例如 `gamemode` 默认 `SURVIVAL` 译为「生存」，`AllowedValues` 列原值；Difficulty 的 `PEACEFUL`/`EASY`/`NORMAL`/`HARD` 在 Description 中给中文映射 |
| 键名不翻译 | 所有 `Key` 字段保持原始英文路径（如 `server.ip`、`game.gamemode`） |
| 值类型标注 | `ValueType` 字段值：`bool`/`int`/`string`/`enum`/`list`/`float` |
| 取值范围明确 | int 用 `MinValue`/`MaxValue`，enum 用 `AllowedValues`，并在 Description 中说明范围 |
| 重启标注 | `RequiresRestart`：`true`=✅ 需重启，`false`=🔄 可热重载 |
| 说明详尽 | Description 用 `\n` 分多行，第一行含义，后续行展开说明 |

## 假设与决策

### 假设
1. Glowstone 配置项以 ServerConfig.java 的 Key 枚举为权威源（已完成探索，~75 项）
2. Sponge 的 `global.conf` 和 `sponge.conf` 两个文件都被视为「Sponge 配置」共同文档化在 `32-sponge.md` 中
3. SpongeForge 没有独立的源码配置类，因此 `33-spongeforge.md` 仅描述差异和推荐配置（基于 Web 搜索的官方文档与社区实践）

### 决策
1. **文件命名**：严格遵循用户规范 - PowerNukkit 用 `powernukkit-server.properties`，SpongeForge 用 `spongeforge-global.conf`，避免与同名 Java 版配置描述符冲突
2. **Sponge 双文件合并文档**：`32-sponge.md` 同时覆盖 `global.conf` 和 `sponge.conf`，但 C# 文件 `RegisterSpongeGlobalConf.cs` 也注册两个 ConfigFileName 的描述符
3. **SpongeForge 虚构文件名**：源码中无 `spongeforge-global.conf`，但用户明确要求用此名注册差异项以避免冲突，遵循用户指示
4. **HOCON Map 展开**：`entity-activation-range.global-ranges` 在 HOCON 中是 Map，注册时展开为子键（如 `entity-activation-range.global-ranges.ambient`）
5. **不创建额外文件**：仅产出 8 个用户指定的文件，不创建示例配置文件、不创建 README

## 验证步骤

每个文件产出后执行：

1. **格式一致性**：Markdown 文件结构与 `35-powernukkit.md` / `07-pufferfish.md` 一致（标题层级、表格列名、阅读约定段落）
2. **C# 模式一致性**：C# 文件结构与 `RegisterPowerNukkitYml.cs` / `RegisterNukkitYml.cs` 一致（注释头、方法签名、ServerConfigDescriptor 字段集）
3. **项数核对**：
   - Glowstone：~75 项
   - Sponge（global + sponge.conf）：~90-110 项
   - SpongeForge：~20-30 项差异
4. **键名完整性**：所有从源码提取的 Key 枚举值都已注册，无遗漏
5. **翻译规范符合性**：
   - 键名是否保持英文原样
   - 是否每个项都有 `ValueType`
   - int/enum 类型的项是否提供取值范围
   - 是否标注 `RequiresRestart`
   - Description 是否详尽（多行）

## 任务执行顺序

1. 产出 `36-glowstone.md`（基于 ServerConfig.java Key 枚举）
2. 产出 `RegisterGlowstoneConfig.cs`（与上同步）
3. 产出 `32-sponge.md`（global.conf + sponge.conf）
4. 产出 `RegisterSpongeGlobalConf.cs`（双文件注册）
5. 产出 `33-spongeforge.md`（差异项）
6. 产出 `RegisterSpongeForgeConf.cs`（差异项注册）

## 完成回报

完成后向用户返回：
- 8 个文件的绝对路径清单
- 每个核心的配置项数量统计：
  - PowerNukkit: 已产出（powernukkit.yml + 基岩版 server.properties）
  - Glowstone: glowstone.yml 约 X 项
  - Sponge: global.conf 约 X 项 + sponge.conf 约 X 项
  - SpongeForge: spongeforge-global.conf 约 X 项差异
