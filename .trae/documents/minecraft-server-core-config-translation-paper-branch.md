# 计划：翻译 4 个 Paper 系 Minecraft 服务器核心配置文件

## 任务概要

为 MSMC（McServerGuard）项目编写 4 个核心（Folia、Kaiiju、NachoSpigot、USpigot）的中文配置文件手册 + C# ServerConfigDescriptor 注册方法代码片段，共 8 份产出物。所有文档须让从未开过服的小白也能看懂，遵守翻译规范（键名不翻译、值类型标注、取值范围明确、重启标注、详尽说明、枚举值翻译）。

---

## Phase 1 探索结论（已确认）

### 1.1 项目约定（已核实）

- **目录结构**：`/workspace/docs/server-cores/` 下已有 11 份 md 文档（06-pufferfish / 07-pufferfish / 08-leaves / 09-leaf / 10-luminol / 18-forge / 19-neoforge / 20-fabric / 21-quilt / 22-bungeecord / 23-velocity / 34-nukkit）。
- **`_patches/` 目录已存在**（无需 `mkdir -p`），已有 5 个 cs 片段：`RegisterFabricServerProperties.cs`、`RegisterForgeServerToml.cs`、`RegisterNeoForgeYml.cs`、`RegisterNukkitYml.cs`、`RegisterQuiltServerProperties.cs`。**任务清单中的"创建 _patches 目录"步骤可省略**。
- **格式参考**：`07-pufferfish.md` 是最贴近本任务的参考（Paper 系 + YAML + 全局专属配置）。
- **缺失/不存在配置的处理参考**：`08-leaves.md` 末尾"附录：配置文件不存在性核实"展示了如何在文档中显式说明不存在某文件。
- **TOML 参考**：`10-luminol.md` 展示了 TOML 节路径格式（`[misc.server_mod_name] name`）。

### 1.2 Markdown 文档约定

参考 `07-pufferfish.md` / `08-leaves.md` 的固定模板：

1. 标题：`# <核心名> 服务器配置文件中文手册`
2. 引用块：核心简介 + 继承关系链 + 官方 GitHub
3. 一段叙述性介绍
4. **配置文件清单**表格（文件名 / 格式 / 来源 / 说明）
5. 阅读约定说明（键名不翻译、值类型缩写、✅/🔄 重启标注）
6. 按节分组的配置项表格，列固定为：`键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明`
7. 配置示例（YAML 默认值代码块）
8. 优化建议（针对大型服务器）
9. 参考来源链接

### 1.3 C# 代码片段约定（来自 `RegisterNukkitYml.cs` / `RegisterNeoForgeYml.cs`）

- 文件头部多行注释：文件名、功能描述、配置文件、来源核心、适用版本、数据来源、集成位置
- 方法签名：`private void Register<Name>()`
- 方法体首行：`const string file = "<filename>";`
- 每个 Register 调用注册一个 `ServerConfigDescriptor`，包含字段：`Key` / `ConfigFileName` / `DisplayName` / `Description`（多行用 `\n`）/ `Category` / `DefaultValue` / `MinValue` / `MaxValue` / `AllowedValues`（数组）/ `RegexPattern` / `ValueType` / `RequiresRestart`
- 按 section 用注释分隔：`// ==================== section-name ====================`
- ValueType 取值：`bool` / `int` / `double` / `string` / `enum` / `string[]`
- 注：这些 cs 文件**只是粘贴片段**，不修改 `/workspace/src/McServerGuard/Services/ConfigManagement/ConfigDescriptorRegistry.cs`（与现有 5 个 _patches 文件一致）

### 1.4 ServerConfigDescriptor 结构（已从 `ConfigDescriptorRegistry.cs` 第 22-75 行核实）

- `Key` / `ConfigFileName` / `DisplayName` / `Description` / `Category` 为 `required` 字段
- `DefaultValue` / `MinValue` / `MaxValue` / `AllowedValues` / `RegexPattern` 可空
- `ValueType` 默认 `"string"`
- `RequiresRestart` 默认 `false`

### 1.5 各核心调研数据状况（已确认）

| 核心 | 调研结果 | 数据来源文件 |
|---|---|---|
| **Folia** | 已从 `/tmp/folia-base.patch` 第 429-441 行提取出 `ThreadedRegions` ConfigurationPart 的 3 个字段（`threads`/`gridExponent`/`scheduler`），并已核实这是 Folia 对 Paper 的 `GlobalConfiguration.java` 的扩展（写入 `paper-global.yml`，**不存在独立的 `folia-global.yml`**）。`scheduler` 是枚举 `TickRegionScheduler.SchedulerType`，默认 `EDF`。 | `/tmp/folia-base.patch`、`/tmp/folia-readme.md`、`/tmp/f-0002-Max-pending-logins.patch`、`/tmp/f-0007-Region-profiler.patch`、`/tmp/f-0008-Add-watchdog-thread.patch` |
| **Kaiiju** | 之前调研已确认 kaiiju.yml 含 5 节：`region-format` / `network` / `optimization` / `gameplay` / `world-settings`。需补一份基于 Kaiiju 公开 README/wiki 的配置项清单（Linear 区域格式 + 异步区块加载 + Folia 优化 + 玩法开关）。 | 之前 WebSearch 检索 Kaiiju GitHub 仓库 README 与 wiki |
| **NachoSpigot** | 已完整提取：`/tmp/NachoConfig.java` 共 35 项 settings.* + `/tmp/NachoWorldConfig.java` 共 13 项 world-settings.*，并已读到 `loadComments()` 中的官方英文注释作为说明依据。`config-version` 当前为 7。 | `/tmp/NachoConfig.java`、`/tmp/NachoWorldConfig.java` |
| **USpigot** | **PalladiumOS/USpigot 仓库在 GitHub 上不存在**，国内社区（MineBBS、CSDN）也无 uspigot.yml 内容。任务摘要已明确处置：文档中显式声明无法定位源码，转而文档化 USpigot 作为 1.8 Spigot 分支所继承的 spigot.yml 关键差异点 + 已知 USpigot 改动（品牌名等）。 | 无可用源文件 |

---

## Phase 3 实施计划

### 待产出文件清单（8 份）

| # | 文件路径 | 核心内容 |
|---|---|---|
| 1 | `/workspace/docs/server-cores/05-folia.md` | Folia 多线程区域化架构 + paper-global.yml 的 `threaded-regions` 节（3 项），并显式说明无 `folia-global.yml` |
| 2 | `/workspace/docs/server-cores/_patches/RegisterFoliaGlobalYml.cs` | `RegisterFoliaGlobalYml()` 方法，3 项 |
| 3 | `/workspace/docs/server-cores/11-kaiiju.md` | kaiiju.yml 5 节配置（region-format / network / optimization / gameplay / world-settings） |
| 4 | `/workspace/docs/server-cores/_patches/RegisterKaiijuYml.cs` | `RegisterKaiijuYml()` 方法 |
| 5 | `/workspace/docs/server-cores/12-nachospigot.md` | nacho.yml：settings.*（35 项）+ world-settings.default.*（13 项）+ config-version |
| 6 | `/workspace/docs/server-cores/_patches/RegisterNachoYml.cs` | `RegisterNachoYml()` 方法，~49 项 |
| 7 | `/workspace/docs/server-cores/13-uspigot.md` | 显式说明无法定位源码 + 1.8 Spigot 基础配置对照 + 已知 USpigot 改动点 |
| 8 | `/workspace/docs/server-cores/_patches/RegisterUSpigotYml.cs` | `RegisterUSpigotYml()` 方法，仅注册可推断项（品牌名 / config-version / 继承自 spigot.yml 的关键差异点） |

### 各核心产出物详细规格

#### A. Folia（`05-folia.md` + `RegisterFoliaGlobalYml.cs`）

**Markdown 文档结构**：
- 标题：`# Folia 服务器配置文件中文手册`
- 引用块：Folia 是 PaperMC 团队的 Paper 分支，引入区域化多线程（regionised multithreading）
- 继承关系：`Vanilla → Spigot → Paper → Folia`
- 官方 GitHub：https://github.com/PaperMC/Folia
- 介绍段：摘自 `/tmp/folia-readme.md`（独立 region / tick loop / 并行线程池 / 无主线程）
- **配置文件清单表格**（包含 server.properties / bukkit.yml / spigot.yml / paper-global.yml / paper-world-defaults.yml）
- **重要说明段**：参考 `08-leaves.md` 末尾"附录"模式，显式声明 Folia 不存在独立的 `folia-global.yml`，其多线程配置直接追加到 `paper-global.yml` 的 `threaded-regions` 节（基于 `GlobalConfiguration.java` 第 394 行后的 patch）
- 阅读约定：键名采用点号扁平化路径，✅ = 需重启（Folia 配置均仅启动时读取）
- 配置项表格（节名 `threaded-regions`）：

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `threaded-regions.threads` | 区域 tick 线程数 | int | `-1`（-1 = 自动 / ≥ 1） | ✅ | 区域化 tick 线程池大小。-1 表示自动按 CPU 核心数计算。 |
| `threaded-regions.gridExponent` | 区域网格指数 | int | `4`（0–31） | ✅ | 控制区域分组的网格大小，2^此值。值越大区域越大、并行度越低；值越小区域越小、并行度越高但跨区域同步开销增加。 |
| `threaded-regions.scheduler` | 区域调度器类型 | enum | `EDF`（`EDF` / `FAIR`） | ✅ | 区域 tick 调度算法：EDF=最早截止期优先（Earliest Deadline First）；FAIR=公平轮转。 |

- **FAQ 段**（移植自 README：哪些服务器能受益、硬件要求 16 核、最佳配置方法）
- **插件兼容性警告段**：必须 `folia-supported: true`、线程上下文 API 等
- **优化建议段**：基于 README 第 43-72 行的线程分配建议（netty IO、chunk IO、chunk worker、GC、剩余留给 tick threads < 80% CPU）
- 配置示例 YAML
- 参考来源：Folia README + GlobalConfiguration.java patch

**C# 注册方法**：3 项注册（与上表一致）

#### B. Kaiiju（`11-kaiiju.md` + `RegisterKaiijuYml.cs`）

**Markdown 文档结构**：
- 标题：`# Kaiiju 服务器配置文件中文手册`
- 引用块：Kaiiju 是基于 Folia 的优化分支，集成 Linear 区域文件格式 + 异步 IO + 多项性能与玩法优化
- 继承关系：`Vanilla → Spigot → Paper → Folia → Kaiiju`
- 官方 GitHub：https://github.com/KaiijuMC/Kaiiju
- 介绍段：Kaiiju 在 Folia 之上叠加 Linear 区块格式（来自 Krypton）、异步区块 IO、可配置玩法开关
- **配置文件清单表格**
- kaiiju.yml 5 节分组表格：

  1. **region-format 节**（区块文件格式，参考 Luminol 的 Linear 段）：
     - `region-format.format` (enum: ANVIL/LINEAR，默认 LINEAR)
     - `region-format.linear.compression-level` (int 0-9，默认 1)
     - `region-format.linear.flush-max-threads` (int，默认 6)
     - `region-format.linear.use-virtual-thread` (bool，默认 true)
     - 等
  2. **network 节**：TCP_NODELAY、TCP Fast Open 等
  3. **optimization 节**：实体、方块、tick 优化
  4. **gameplay 节**：玩法开关（fix-eat-while-running 等）
  5. **world-settings 节**：每世界继承默认值

- 配置示例 YAML
- 优化建议
- 参考来源

**C# 注册方法**：~25-30 项注册（视具体调研结果填充）

> ⚠️ **执行时的不确定项**：之前调研确认了 Kaiiju 的 5 节结构，但每节具体默认值清单可能不完整。执行时如遇某项无法核实默认值，将在说明中显式标注「默认值待官方文档确认」，仍保留该项注册（避免偷工减料）。

#### C. NachoSpigot（`12-nachospigot.md` + `RegisterNachoYml.cs`）

**Markdown 文档结构**：
- 标题：`# NachoSpigot 服务器配置文件中文手册`
- 引用块：NachoSpigot 是 CobbleSword 团队的 Paper/Spigot 1.8.8 分支，主打反作弊、性能优化与 cannon（TNT 大炮）支持
- 继承关系：`Vanilla → Spigot → Paper → NachoSpigot`
- 官方 GitHub：https://github.com/CobbleSword/NachoSpigot
- 配置文件清单
- nacho.yml 整体结构（移植自 `/tmp/NachoConfig.java` 的 HEADER 注释）
- **按 section 分组的表格**：

  1. **config-version**（1 项）
  2. **settings 节**：
     - `settings.save-empty-scoreboard-teams` (bool，默认 false)
     - `settings.fast-operators` (bool，默认 false)
     - `settings.stop-notify-bungee` (bool，默认 false)
     - `settings.anti-malware` (bool，默认 false)
     - `settings.kick-on-illegal-behavior` (bool，默认 true)
     - `settings.panda-wire` (bool，默认 true)
     - `settings.brand-name` (string，默认 "NachoSpigot")
     - `settings.stop-decoding-itemstack-on-place` (bool，默认 true)
     - `settings.anti-crash` (bool，默认 true)
     - `settings.use-tcp-nodelay` (bool，默认 true)
     - `settings.faster-cannon-tracker` (bool，默认 true)
     - `settings.fix-eat-while-running` (bool，默认 false)
     - `settings.hide-projectiles-from-hidden-players` (bool，默认 false)
     - `settings.anti-enderpearl-glitch` (bool，默认 false)
     - `settings.disabled-block-fall-animation` (bool，默认 false)
     - `settings.enable-protocol-shim` (bool，默认 true)
     - `settings.instant-interaction` (bool，默认 false)
     - `settings.disable-infinisleeper-thread-usage` (bool，默认 false)
     - `settings.enable-fastmath` (bool，默认 false)
     - `settings.tcp-fastopen-mode` (int，默认 1，0/1/2/3，需在说明中翻译 4 个枚举值)
     - `settings.tile-entity-ticking-time` (int，默认 1)
     - `settings.item-dirty-ticks` (int，默认 20)
     - `settings.use-tcp-fastopen` (bool，默认 true)
     - `settings.lag-compensated-potions` (bool，默认 false)
     - `settings.smooth-potting` (bool，默认 false)
     - `settings.use-improved-hitreg` (bool，默认 false)
     - `settings.disable-disconnect-spam` (bool，默认 false)
  3. **settings.commands 子节**：
     - `settings.commands.enable-version-command` (bool，默认 true)
     - `settings.commands.enable-plugins-command` (bool，默认 true)
     - `settings.commands.enable-reload-command` (bool，默认 true)
     - `settings.commands.enable-help-command` (bool，默认 true)
     - `settings.commands.permission.version` (bool，默认 true)
     - `settings.commands.permission.plugins` (bool，默认 true)
  4. **settings.event 子节**：
     - `settings.event.fire-entity-explode-event` (bool，默认 true)
     - `settings.event.fire-player-move-event` (bool，默认 true)
     - `settings.event.fire-leaf-decay-event` (bool，默认 true)
  5. **settings.chunk 子节**：
     - `settings.chunk.threads` (int，默认 2)
     - `settings.chunk.players-per-thread` (int，默认 50)
  6. **settings.fixed-pools 子节**：
     - `settings.fixed-pools.use-fixed-pools-for-explosions` (bool，默认 false)
     - `settings.fixed-pools.size` (int，默认 500)
  7. **world-settings.default 节**（来自 `/tmp/NachoWorldConfig.java`）：
     - `world-settings.default.disable-sponge-absorption` (bool，默认 false)
     - `world-settings.default.unload-chunks` (bool，默认 true)
     - `world-settings.default.block-operations` (bool，默认 true)
     - `world-settings.default.physics.disable-place` (bool，默认 false)
     - `world-settings.default.physics.disable-update` (bool，默认 false)
     - `world-settings.default.enable-lava-to-cobblestone` (bool，默认 true)
     - `world-settings.default.entity.mob-ai` (bool，默认 true)
     - `world-settings.default.entity.mob-sound` (bool，默认 true)
     - `world-settings.default.entity.entity-activation` (bool，默认 true)
     - `world-settings.default.entity.endermite-spawning` (bool，默认 true)
     - `world-settings.default.infinite-water-sources` (bool，默认 true)
     - `world-settings.default.explosions.constant-radius` (bool，默认 false)
     - `world-settings.default.explosions.reduced-density-rays` (bool，默认 true)
     - `world-settings.default.tick-enchantment-tables` (bool，默认 true)

  **合计：1 + 28 + 6 + 3 + 2 + 2 + 14 = 56 项**

- 配置示例 YAML
- 优化建议（cannon/TNT 服务器、反作弊、网络优化）
- 参考来源

**C# 注册方法**：56 项注册，按 section 注释分组

#### D. USpigot（`13-uspigot.md` + `RegisterUSpigotYml.cs`）

**Markdown 文档结构**：
- 标题：`# USpigot 服务器配置文件中文手册`
- 引用块：USpigot 是基于 Spigot 1.8.8 的国内分支（PalladiumOS 团队），主打 PVP 体验与中文环境优化
- 继承关系：`Vanilla → Spigot → USpigot`
- 官方仓库：未公开（PalladiumOS/USpigot 仓库不可访问）
- **重要说明段**（参考 `08-leaves.md` 附录模式）：明确声明
  - GitHub 上未找到 USpigot 公开仓库
  - 国内社区（MineBBS、CSDN）未公开 uspigot.yml 默认内容
  - 后续可联系 USpigot 团队或下载官方构建后从 jar 中提取默认配置补全
- 配置文件清单（与 Spigot 一致：server.properties / bukkit.yml / spigot.yml / uspigot.yml）
- **uspigot.yml 推断配置项表格**（仅列出可合理推断的项）：
  - `config-version` (int，用于配置版本管理)
  - `settings.brand-name` (string，默认 "USpigot"，与 NachoSpigot 同类设计)
  - 注：USpigot 作为 1.8 PVP 分支，可能继承 NachoSpigot / SportPaper 部分配置项（anti-enderpearl-glitch、fix-eat-while-running 等），但因无法核实，仅列出可推断项
- **1.8 Spigot 继承配置对照段**：列出 USpigot 必然继承的 spigot.yml 关键项（视距、tick 速率、合并半径等），并提示用户参照 spigot.yml 手册
- 参考来源：USpigot 相关国内社区讨论

**C# 注册方法**：仅注册 2-3 项可推断项，并在文件头注释中明确标注「数据源不完整，待补充」

---

## 假设与决策

### 假设
1. **Folia 无 `folia-global.yml`**：通过核实 `/tmp/folia-base.patch` 对 `GlobalConfiguration.java` 的修改，确认 Folia 把 `threaded-regions` 节直接追加到 Paper 的 `paper-global.yml`，无独立文件。文档将显式说明这一点（仿照 `08-leaves.md` 处理 `leaves-global.yml` 不存在的写法）。
2. **Kaiiju 配置数据可能不完整**：之前调研确认了 5 节结构但具体默认值清单可能不完整。执行时如遇无法核实的项，将在该项说明末尾标注「⚠️ 默认值待官方文档核实」并仍注册该项，避免遗漏。
3. **NachoSpigot 数据完整可信**：`/tmp/NachoConfig.java` 与 `/tmp/NachoWorldConfig.java` 是源码直读，且 `loadComments()` 提供了官方英文注释，所有 56 项默认值与说明均有依据。
4. **USpigot 数据源不可用**：GitHub 与国内社区均无可用源，按任务摘要既定方案处置（显式声明 + 推断少量项 + 引导用户参照 spigot.yml 手册）。
5. **不修改主仓库代码**：仅新增 8 份文档/代码片段文件到 `/workspace/docs/server-cores/` 及其 `_patches/` 子目录。`ConfigDescriptorRegistry.cs` 不修改（与现有 5 个 _patches 文件的处理方式一致——它们是粘贴片段，不是已被集成的代码）。
6. **`_patches/` 目录已存在**：第一阶段 LS 已确认，跳过任务清单中的"创建 _patches 目录"步骤。

### 决策
- **关于 Folia 文件名**：尽管任务要求产出 `RegisterFoliaGlobalYml.cs`，但因 Folia 实际无 `folia-global.yml` 文件，C# 片段中 `const string file = "paper-global.yml";`，并在文件头注释中说明这一点。Markdown 文档同样显式声明。
- **关于 Kaiiju 不确定项**：宁可保留项并标注「待核实」，也不删减（遵守"不要偷工减料"指令）。
- **关于 USpigot 不完整数据**：在文档顶部以 ⚠️ 醒目警告标注数据源不完整，但仍然产出最小可用的 2 份文件（避免完全跳过此核心）。
- **重启标注统一规则**：
  - YAML 在服务器启动时读取且无热重载命令的 → ✅
  - 有 `/reload` 类热重载命令且该项支持热重载 → 🔄
  - 无法核实热重载支持时 → 默认 ✅（保守）

---

## 实施顺序（建议使用 TodoWrite 跟踪）

1. ✅ 探索阶段已完成（本计划）
2. 编写 `05-folia.md` + `RegisterFoliaGlobalYml.cs`（数据最确定，先做）
3. 编写 `12-nachospigot.md` + `RegisterNachoYml.cs`（数据完整，再做）
4. 编写 `11-kaiiju.md` + `RegisterKaiijuYml.cs`（需要边写边补默认值，第三做）
5. 编写 `13-uspigot.md` + `RegisterUSpigotYml.cs`（数据缺失，最后做并显式声明）
6. 汇总产出文件路径与配置项数量统计，返回给父代理

---

## 验证步骤

完成后逐项核对：

- [ ] 8 份文件均已创建在正确路径
- [ ] 每份 md 文档都包含：标题 / 引用块 / 继承关系 / 配置清单表 / 阅读约定 / 配置项表格 / 配置示例 / 优化建议 / 参考来源
- [ ] 每份 cs 文件都包含：文件头注释 / `Register<Name>()` 方法 / 至少 1 个 `Register(new ServerConfigDescriptor{...})` 调用
- [ ] Folia 文档显式说明无 `folia-global.yml`
- [ ] USpigot 文档顶部有 ⚠️ 数据源不完整警告
- [ ] NachoSpigot 文档覆盖全部 56 项
- [ ] 所有枚举值（如 `tcp-fastopen-mode` 的 0/1/2/3、Folia `scheduler` 的 EDF/FAIR）在说明中已翻译为中文
- [ ] 所有数值项标注了取值范围
- [ ] 所有配置项标注了 ✅/🔄 重启要求
- [ ] 返回报告包含每个核心的配置项数量统计

---

## 备注

- 本计划已"决策完整"——执行者按本计划逐文件编写即可，无需再做架构选择。
- 计划中已为不确定项（Kaiiju 默认值、USpigot 数据源）预设了降级策略，避免执行时卡住。
- 8 份文件总规模预估：4 份 md 文档约 8000-12000 字，4 份 cs 文件约 600-1000 行。
