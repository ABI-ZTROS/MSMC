# Paper 服务器配置文件中文手册

> Paper（PaperMC）是 Spigot 的高性能优化分支，业界最流行的 Java 版服务端核心，被 Purpur、Pufferfish、Folia 等下游分支作为基线。
> 继承关系：**Vanilla → Bukkit → Spigot → Paper**
> 官方网站：https://papermc.io/
> 官方文档：https://docs.papermc.io/
> 数据来源：PaperMC 源码 `io.papermc.paper.configuration`（PaperConfiguration / GlobalConfiguration / WorldConfiguration）/ 官方文档
> 适用版本基准：Paper 1.21.x（2025–2026 稳定版，新配置体系）

Paper 完整继承 Vanilla 的 `server.properties`、Bukkit 的 `bukkit.yml` / `permissions.yml` / `commands.yml` / `help.yml`、Spigot 的 `spigot.yml`，并新增**两个** Paper 专属配置文件：`paper-global.yml`（全局配置）与 `paper-world-defaults.yml`（世界默认配置）。⚠️ Paper 1.19.4+ 使用 **新配置体系**（`io.papermc.paper.configuration`），文件位于 `config/` 目录下，每个世界可单独生成 `paper-world.yml` 覆盖默认值。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（请参阅 Vanilla 手册） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置（请参阅 Bukkit 手册） |
| permissions.yml | YAML | Bukkit 继承 | 默认权限组（请参阅 Bukkit 手册） |
| commands.yml | YAML | Bukkit 继承 | 命令别名（请参阅 Bukkit 手册） |
| help.yml | YAML | Bukkit 继承 | 帮助页配置（请参阅 Bukkit 手册） |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（请参阅 Spigot 手册） |
| **config/paper-global.yml** | YAML | **Paper 专属** | **Paper 全局配置（本文档重点）** |
| **config/paper-world-defaults.yml** | YAML | **Paper 专属** | **Paper 世界默认配置（本文档重点）** |

> 本文仅翻译 Paper **专属**的两个 YAML 文件。其余配置请参阅对应手册（Vanilla / Bukkit / Spigot）。

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `chunk-loading.basic-maximizer-chunk-limit`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举 / `double` 浮点 / `list` 列表 / `duration` 时长。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示必须重启服务器才能生效；🔄 表示支持 `/paper reload` 热重载（部分项）。
- Paper 新配置使用 `Duration` 类型（如 `5s`、`1ms`、`200ms`、`1d`），下文标注为 `duration`。

---

## config/paper-global.yml（Paper 全局配置）

> Paper 全局配置由 `GlobalConfiguration` 类加载，影响整个服务器（非每世界）。位于 `config/paper-global.yml`。

### 1. chunk-loading（区块加载）

> Paper 的区块加载系统是性能优化核心，比 Spigot 的 `entity-activation-range` 更精细。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunk-loading.basic-maximizer-chunk-limit` | 基础加载器区块上限 | int | `4`（≥ 0） | ✅ | 单个玩家每 tick 最多生成 / 加载多少区块。降低可缓解玩家快速移动（鞘翅）时的卡顿。 |
| `chunk-loading.player-max-chunk-load-rate` | 玩家区块加载速率 | double | `-1.0`（-1 = 无限制；≥ 0） | ✅ | 单个玩家每秒最多加载多少区块。`-1` 无限制。降低可保护 CPU。 |
| `chunk-loading.player-max-chunk-generate-rate` | 玩家区块生成速率 | double | `-1.0`（-1 = 无限制；≥ 0） | ✅ | 单个玩家每秒最多生成多少区块。`-1` 无限制。 |
| `chunk-loading.global-max-chunk-load-rate` | 全局区块加载速率 | double | `-1.0`（-1 = 无限制；≥ 0） | ✅ | 全服每秒最多加载多少区块。`-1` 无限制。 |
| `chunk-loading.global-max-chunk-generate-rate` | 全局区块生成速率 | double | `-1.0`（-1 = 无限制；≥ 0） | ✅ | 全服每秒最多生成多少区块。`-1` 无限制。降低可保护 CPU。 |

### 2. chunk-system（区块系统线程池）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunk-system.io-threads` | 区块 IO 线程数 | int | `-1`（-1 = 自动；≥ 1） | ✅ | 负责从磁盘读写区块文件的线程数。`-1` 自动（基于 CPU 核心数）。大型服可手动设为 `2`–`4`。 |
| `chunk-system.worker-threads` | 区块工作线程数 | int | `-1`（-1 = 自动；≥ 1） | ✅ | 负责区块生成 / 装饰计算的线程数。`-1` 自动。未预生成时需大幅增加。 |
| `chunk-system.gen.parallelism` | 生成并行度 | int | `-1`（-1 = 自动；≥ 1） | ✅ | 区块生成任务的并行度。 |

### 3. collisions（碰撞）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `collisions.enable-player-collisions` | 启用玩家碰撞 | bool | `true`（`true`/`false`） | ✅ | 是否启用玩家间的物理碰撞。关闭后玩家可互相穿过。 |
| `collisions.send-player-pos-when-teleporting` | 传送时发送位置 | bool | `true`（`true`/`false`） | ✅ | 玩家传送时是否立即同步位置，避免位置不同步。 |
| `collisions.send-player-pos-when-colliding-with` | 碰撞时发送位置 | bool | `true`（`true`/`false`） | ✅ | 玩家发生碰撞时是否发送位置同步。 |

### 4. commands（命令）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `commands.time-command-affects-all-worlds` | time 命令影响所有世界 | bool | `true`（`true`/`false`） | 🔄 | `/time` 命令是否影响所有世界。`false` 仅影响当前世界。 |
| `commands.fix-target-selector-tag-completion` | 修复选择器补全 | bool | `true`（`true`/`false`） | 🔄 | 修复目标选择器中标签补全的 bug。 |

### 5. console（控制台）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `console.enable-brigadier-highlighting` | 启用 Brigadier 高亮 | bool | `true`（`true`/`false`） | ✅ | 控制台是否启用 Brigadier 命令高亮显示。 |
| `console.enable-brigadier-completions` | 启用 Brigadier 补全 | bool | `true`（`true`/`false`） | ✅ | 控制台是否启用 Brigadier 命令 Tab 补全。 |
| `console.has-all-permissions` | 拥有所有权限 | bool | `false`（`true`/`false`） | ✅ | 控制台是否拥有所有权限（绕过权限检查）。 |

### 6. item-validation（物品验证）

> 防止恶意玩家利用超长 NBT、过大显示名等物品卡服。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `item-validation.book-size.page-max` | 书本单页最大字节 | int | `2560`（≥ 0） | 🔄 | 成书单页最大字节数。降低可防止恶意长 NBT 卡服。 |
| `item-validation.book-size.total-multiplier` | 书本总大小倍率 | double | `0.98`（≥ 0） | 🔄 | 成书总大小 = 页数 × 此倍率。 |
| `item-validation.display-name` | 物品显示名最大字节 | int | `8192`（≥ 0） | 🔄 | 物品自定义显示名最大字节数。 |
| `item-validation.resolve-selectors-in-books` | 解析书中选择器 | bool | `false`（`true`/`false`） | 🔄 | 是否在成书中解析目标选择器。 |

### 7. logging（日志）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `logging.log-ping-packet-length-mismatch` | 记录 ping 包长度不匹配 | bool | `false`（`true`/`false`） | 🔄 | 是否记录 ping 包长度不匹配事件（异常客户端）。 |
| `logging.deobfuscate-stacktraces` | 反混淆堆栈 | bool | `true`（`true`/`false`） | 🔄 | 是否将堆栈跟踪中的混淆方法名反混淆为可读名。 |

### 8. misc（杂项）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.chat-threads.core-size` | 聊天线程核心数 | int | `-1`（-1 = 自动；≥ 1） | ✅ | 处理玩家聊天的核心线程数。 |
| `misc.chat-threads.max-size` | 聊天线程最大数 | int | `-1`（-1 = 自动；≥ 1） | ✅ | 处理玩家聊天的最大线程数。 |
| `misc.server-activity.timeunit` | 服务器活跃统计单位 | enum | `SECONDS`（`SECONDS`/`MINUTES`/`HOURS`/`DAYS`） | 🔄 | 服务器活跃统计的时间单位。 |
| `misc.server-activity.timeout` | 服务器活跃超时 | int | `60`（≥ 0） | 🔄 | 服务器活跃统计超时值。 |
| `misc.max-joins-per-tick` | 每 tick 加入上限 | int | `5`（≥ 0） | 🔄 | 每 tick 允许多少玩家加入服务器。降低可防止登录冲击。 |
| `misc.player-auto-save-rate` | 玩家自动保存频率 | int | `-1`（-1 = 自动；≥ 0） | 🔄 | 玩家数据自动保存频率（每多少玩家 / tick）。 |
| `misc.max-player-auto-save-per-tick` | 每 tick 最多保存玩家数 | int | `-1`（-1 = 自动；≥ 0） | 🔄 | 每 tick 最多保存多少玩家的数据，避免一次性保存卡顿。 |
| `misc.fix-wrong-rotations` | 修复错误旋转 | bool | `false`（`true`/`false`） | 🔄 | 修复某些实体的旋转 bug。 |

### 9. packet-limiter（数据包限流）

> 防止恶意玩家通过发送大量数据包攻击服务器（DDoS）。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `packet-limiter.kick-message` | 踢出消息 | string | `<red><lang:disconnect.exceeded_packet_rate></red>` | 🔄 | 玩家被限流踢出时显示的消息。 |
| `packet-limiter.packet-limit.settings` | 限流设置 | map | `{}` | 🔄 | 各数据包的限流规则。键为数据包名，值为 `action`/`interval`/`max-packet-rate`。 |
| `packet-limiter.packet-limit.overrides` | 限流覆盖 | map | `{}` | 🔄 | 对特定数据包的限流覆盖规则。 |
| `packet-limiter.kick-message` | 限流踢出消息 | string | `<red>...</red>` | 🔄 | 玩家触发限流被踢时显示的提示。 |

### 10. player-auto-save（玩家自动保存）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `player-auto-save.rate` | 自动保存频率 | int | `-1`（-1 = 自动；≥ 0，tick） | 🔄 | 玩家数据自动保存的间隔 tick。`-1` 自动（基于 `ticks-per.autosave`）。 |
| `player-auto-save.max-per-tick` | 每 tick 保存上限 | int | `-1`（-1 = 自动；≥ 0） | 🔄 | 每 tick 最多保存多少玩家的数据，避免一次性卡顿。 |

### 11. proxies（代理）

> 当 Paper 前置 BungeeCord / Velocity 代理时配置。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `proxies.bungee-cord.online-mode` | BungeeCord 在线模式 | bool | `false`（`true`/`false`） | ✅ | BungeeCord 是否已做 Mojang 正版验证。设为 `true` 时 Paper 信任 BungeeCord 转发的正版身份。 |
| `proxies.velocity.enabled` | 启用 Velocity 转发 | bool | `false`（`true`/`false`） | ✅ | 是否启用 Velocity 现代转发。启用后 `server.properties` 的 `online-mode` 应设为 `false`。 |
| `proxies.velocity.online-mode` | Velocity 在线模式 | bool | `false`（`true`/`false`） | ✅ | Velocity 是否已做 Mojang 正版验证。设为 `true` 时 Paper 信任 Velocity 转发的正版身份。 |
| `proxies.velocity.secret` | Velocity 共享密钥 | string | ` `（空 = 禁用） | ✅ | 与 Velocity `forwarding.secret` 一致的密钥。**生产环境必须设置强密钥**，留空则任何人都可伪造玩家身份。 |
| `proxies.proxy-protocol` | 启用 Proxy Protocol | bool | `false`（`true`/`false`） | ✅ | 是否启用 HAProxy Proxy Protocol v2。仅在使用 HAProxy 等 TCP 代理时启用。 |

### 12. spam-limiter（刷屏限制）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `spam-limiter.tab-spam-increment` | Tab 补全刷屏增量 | double | `1.0`（≥ 0） | 🔄 | 玩家每次 Tab 补全增加的刷屏分值。 |
| `spam-limiter.tab-spam-limit` | Tab 补全刷屏上限 | double | `500.0`（≥ 0） | 🔄 | 玩家 Tab 补全刷屏分值上限，超过即禁止补全。 |
| `spam-limiter.recipe-spam-increment` | 配方刷屏增量 | double | `1.0`（≥ 0） | 🔄 | 玩家每次打开配方书增加的刷屏分值。 |
| `spam-limiter.recipe-spam-limit` | 配方刷屏上限 | double | `20.0`（≥ 0） | 🔄 | 玩家配方刷屏分值上限。 |
| `spam-limiter.ignored-packets` | 忽略数据包列表 | list | `[]` | 🔄 | 不计入刷屏检测的数据包列表。 |

### 13. timings（性能分析）

> Timings 是 Paper 内置的性能分析工具，比 Spigot 的更详细。⚠️ Paper 1.21+ 已废弃 Timings，推荐使用 Spark。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `timings.enabled` | 启用 Timings | bool | `false`（`true`/`false`） | 🔄 | 是否启用 Timings 性能分析。⚠️ Paper 1.21+ 已弃用，推荐用 Spark。 |
| `timings.verbose` | 详细 Timings | bool | `true`（`true`/`false`） | 🔄 | 是否记录详细的 Timings 数据。 |
| `timings.url` | Timings 上传地址 | string | `https://timings.aikar.co/` | 🔄 | `/timings paste` 上传报告的地址。 |
| `timings.history-interval` | 历史记录间隔 | duration | `60s` | 🔄 | Timings 历史记录的间隔时长。 |
| `timings.history-length` | 历史记录长度 | duration | `3600s` | 🔄 | Timings 历史记录的总时长。 |
| `timings.hidden-config-entries` | 隐藏配置项 | list | `[]` | 🔄 | 上传报告时隐藏的配置项列表（防止泄露敏感信息）。 |
| `timings.server-name` | 服务器名称 | string | `Unknown Server` | 🔄 | Timings 报告中显示的服务器名称。 |
| `timings.server-name-privacy` | 隐藏服务器名 | bool | `false`（`true`/`false`） | 🔄 | 上传报告时是否隐藏服务器名。 |

### 14. unsupported-settings（不支持的设置）

> 这些设置启用后会破坏 Vanilla 行为或导致插件不兼容，仅在明确知道后果时启用。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `unsupported-settings.allow-headless-pistons` | 允许无头活塞 | bool | `false`（`true`/`false`） | 🔄 | 允许利用 bug 创建无头活塞（用于某些红石机器）。⚠️ 破坏 Vanilla 行为。 |
| `unsupported-settings.allow-permanent-block-break-exploits` | 允许永久破坏 bug | bool | `false`（`true`/`false`） | 🔄 | 允许利用 bug 破坏「不可破坏」方块（如基岩、末地传送门）。⚠️ 严重破坏游戏。 |
| `unsupported-settings.allow-piston-duplication` | 允许活塞复制 | bool | `false`（`true`/`false`） | 🔄 | 允许利用活塞 bug 复制物品（如 TNT 复制机、地毯复制）。⚠️ Vanilla 视为作弊。 |
| `unsupported-settings.perform-username-validation` | 用户名验证 | bool | `true`（`true`/`false`） | 🔄 | 是否对用户名进行严格验证。关闭可让非标准用户名进入（不推荐）。 |

---

## config/paper-world-defaults.yml（Paper 世界默认配置）

> Paper 世界默认配置由 `WorldConfiguration` 类加载。每个世界可单独生成 `config/<世界名>/paper-world.yml` 覆盖默认值。本节列出 `default` 节。

### 1. chunks（区块设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunks.auto-save-interval` | 自动保存间隔 | duration | `6000t`（≥ 0） | 🔄 | 世界自动保存的间隔（`t` = tick）。覆盖 `bukkit.yml` 的 `ticks-per.autosave`。 |
| `chunks.delay-chunk-unloads-by` | 延迟区块卸载 | duration | `10s`（≥ 0） | 🔄 | 玩家离开后多久才卸载其加载的区块。增大可减少玩家来回移动时的重复加载。 |
| `chunks.entity-activation-range.ignore-spectators` | 忽略旁观者 | bool | `true`（`true`/`false`） | 🔄 | 旁观者是否触发实体激活。`true` 时旁观者不激活实体。 |
| `chunks.fixed-chunk-inhabited-time` | 固定区块居住时间 | int | `-1`（-1 = 不固定；≥ 0） | 🔄 | 强制设置区块的「居住时间」（影响怪物生成难度）。`-1` 不修改。 |
| `chunks.max-auto-save-chunks-per-tick` | 每 tick 自动保存上限 | int | `24`（≥ 0） | 🔄 | 每 tick 最多自动保存多少区块，避免一次性保存卡顿。 |
| `chunks.prevent-moving-into-unloaded-chunks` | 阻止进入未加载区块 | bool | `false`（`true`/`false`） | 🔄 | 是否阻止玩家移动进入未加载区块（避免掉入虚空）。 |

### 2. collisions（碰撞）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `collisions.max-entity-collisions` | 实体碰撞上限 | int | `8`（≥ 0） | 🔄 | 单个实体最多同时与多少实体碰撞。覆盖 `spigot.yml` 同名键。 |
| `collisions.enable-player-collisions` | 启用玩家碰撞 | bool | `true`（`true`/`false`） | 🔄 | 是否启用玩家间物理碰撞。 |
| `collisions.allow-player-cramming-damage` | 允许挤压伤害 | bool | `false`（`true`/`false`） | 🔄 | 是否启用玩家挤压伤害（实体堆叠过多时受伤）。 |

### 3. entities（实体设置）

#### 3.1 实体行为

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `entities.mark-experimental-as-different` | 标记实验实体 | bool | `true`（`true`/`false`） | 🔄 | 是否将实验性实体标记为不同（用于统计）。 |
| `entities.sniffer-paced-hatching` | 嗅探兽缓慢孵化 | bool | `true`（`true`/`false`） | 🔄 | 嗅探兽蛋是否缓慢孵化。 |

#### 3.2 箭矢与三叉戟

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `entities.behavior.pufferfish-height-limit` | 河豚高度上限 | double | `14.0`（≥ 0） | 🔄 | 河豚膨胀的高度差上限。 |
| `entities.spawning.counted-all-chunks-for-spawn` | 计算所有区块生成 | bool | `false`（`true`/`false`） | 🔄 | 是否计算所有区块用于生物生成（而非仅玩家周围）。 |
| `entities.spawning.immediate-despawn-impatience-factor` | 立即消失不耐心因子 | int | `0`（≥ 0） | 🔄 | 怪物在远离玩家时立即消失的不耐心因子。 |
| `entities.spawning.despawn-ranges` | 消失范围 | map | `{ambient: [32, 128], axolotls: [32, 128], creature: [32, 128], misc: [32, 128], monster: [32, 128], underground_water_creature: [32, 128], water_ambient: [32, 64], water_creature: [32, 128]}` | 🔄 | 各类生物的消失距离范围（近 / 远，单位方块）。超出远距离立即消失，介于近远之间概率消失。 |
| `entities.spawning.per-player-mob-spawns` | 每玩家生物生成 | bool | `true`（`true`/`false`） | ✅ | 是否启用每玩家生物生成上限（替代全服上限）。**Paper 性能核心**，更精确控制生物分布。 |
| `entities.spawning.scan-for-named-mobs` | 扫描命名生物 | bool | `true`（`true`/`false`） | 🔄 | 是否扫描命名生物（命名过的生物不消失）。关闭可省 CPU 但命名生物可能消失。 |
| `entities.spawning.despawn-tick-rates` | 消失 tick 频率 | map | `{...}` | 🔄 | 各类生物消失检查的 tick 频率。 |
| `entities.armor-stands.do-collision-entity-lookups` | 盔甲架碰撞查找 | bool | `false`（`true`/`false`） | 🔄 | 盔甲架是否进行实体碰撞查找。关闭可省 CPU。 |
| `entities.armor-stands.tick` | 盔甲架 tick | bool | `true`（`true`/`false`） | 🔄 | 盔甲架是否 tick。关闭可大幅省 CPU，但盔甲架不动画。 |
| `entities.armor-stands.disable-when-hidden` | 隐藏时禁用 | bool | `true`（`true`/`false`） | 🔄 | 盔甲架被 `Invisible` 标记时是否禁用 tick。 |
| `entities.spawning.all-chunks-are-slime-chunks` | 所有区块都是史莱姆区块 | bool | `false`（`true`/`false`） | 🔄 | 是否所有区块都视为史莱姆区块。 |
| `entities.mark-experimental-as-different` | 标记实验实体 | bool | `true`（`true`/`false`） | 🔄 | 实验性实体是否标记为不同。 |

#### 3.3 实体优化

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `entities.armor-stands.tick` | 盔甲架 tick | bool | `true`（`true`/`false`） | 🔄 | 盔甲架是否 tick。关闭可省 CPU。 |
| `entities.behavior.baby-zombie-movement-modifier` | 小僵尸移动修正 | double | `0.5`（≥ 0） | 🔄 | 小僵尸移动速度修正倍率。`0.5` = +50% 速度。 |
| `entities.behavior.spider-world-wrap` | 蜘蛛世界环绕 | bool | `true`（`true`/`false`） | 🔄 | 修复蜘蛛世界环绕 bug。 |
| `entities.behavior.disable-zombie-aggression-toward-villager` | 禁用僵尸攻击村民 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用僵尸主动攻击村民。 |
| `entities.spawning.filtered-entity-spawning-tick-rates` | 实体生成 tick 频率 | map | `{}` | 🔄 | 特定实体的生成 tick 频率。 |
| `entities.spawning.bypass-spawn-ranges` | 绕过生成范围 | list | `[]` | 🔄 | 不受生成范围限制的实体列表。 |

#### 3.4 清理

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `entities.cleanup.cluttered-slider-max` | 杂乱滑块最大 | int | `4`（≥ 0） | 🔄 | 实体清理杂乱滑块最大值。 |
| `entities.cluttered-category-size` | 杂乱分类大小 | int | `16`（≥ 0） | 🔄 | 实体清理杂乱分类大小。 |

### 4. environment（环境）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `environment.disable-thunder` | 禁用雷暴 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用雷暴天气。 |
| `environment.disable-ice-and-snow` | 禁用冰雪 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用冰雪生成。 |
| `environment.disable-vehicle-phantom-phantom` | 禁用载具幻影 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用载具移动产生的幻影。 |
| `environment.fixed-time` | 固定时间 | string | ` `（空 = 不固定；`0`–`24000`） | 🔄 | 固定世界时间。空 = 不固定。`6000` = 中午。 |
| `environment.void-teleport-height` | 虚空传送高度 | int | `-100000000`（任意整数） | 🔄 | 玩家落到此 Y 坐标时传送到出生点。 |
| `environment.historical-tracking-enabled` | 启用历史追踪 | bool | `false`（`true`/`false`） | 🔄 | 是否启用历史追踪。 |

### 5. fixes（漏洞修复）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `fixes.disable-unloaded-chunk-entities` | 禁用未加载区块实体 | bool | `false`（`true`/`false`） | 🔄 | 修复未加载区块实体 bug。 |
| `fixes.disable-void-teleporting` | 禁用虚空传送 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用虚空传送。 |
| `fixes.disable-void-teleporting` | 禁用虚空传送 | bool | `false`（`true`/`false`） | 🔄 | 关闭后玩家掉入虚空不会被传送回出生点。 |
| `fixes.falling-block-teleport-vehicles` | 下落方块传送载具 | bool | `false`（`true`/`false`） | 🔄 | 修复下落方块传送载具的 bug。 |
| `fixes.fix-curing-zombie-villager-exploit` | 修复治愈村民漏洞 | bool | `false`（`true`/`false`） | 🔄 | 修复治愈僵尸村民时反复刷价格的漏洞。 |
| `fixes.parked-tick-list-entities` | 停靠 tick 列表实体 | bool | `false`（`true`/`false`） | 🔄 | 修复停靠 tick 列表实体 bug。 |
| `fixes.split-overstacked-loot` | 拆分超叠战利品 | bool | `true`（`true`/`false`） | 🔄 | 是否将超叠战利品拆分为多个物品。 |
| `fixes.pearl-exploit` | 末影珍珠漏洞修复 | bool | `true`（`true`/`false`） | 🔄 | 修复末影珍珠跨世界复制漏洞。 |
| `fixes.tnt-entity-merging-not-bouncing` | TNT 实体合并不反弹 | bool | `false`（`true`/`false`） | 🔄 | 修复 TNT 实体合并时不反弹的 bug。 |
| `fixes.disable-relative-teleport-velocity-exploit` | 禁用相对传送速度漏洞 | bool | `true`（`true`/`false`） | 🔄 | 修复相对传送时的速度漏洞。 |
| `fixes.fix-curing-zombie-villager-exploit` | 修复治愈村民漏洞 | bool | `false`（`true`/`false`） | 🔄 | 修复治愈僵尸村民刷价格漏洞。 |

### 6. gameplay-mechanics（游戏机制）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `gameplay-mechanics.chorus-fruit-teleport-rideable-entities` | 紫颂果传送可骑乘 | bool | `true`（`true`/`false`） | 🔄 | 紫颂果是否能传送骑乘中的实体。 |
| `gameplay-mechanics.chorus-fruit-teleport-riders` | 紫颂果传送骑手 | bool | `true`（`true`/`false`） | 🔄 | 紫颂果是否能传送骑手。 |
| `gameplay-mechanics.disable-chest-cat-detection` | 禁用箱子猫检测 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用打开箱子时检测猫坐其上（可防止刷怪塔）。 |
| `gameplay-mechanics.disable-player-crits` | 禁用玩家暴击 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用玩家的暴击伤害。 |
| `gameplay-mechanics.disable-sprint-interruption-on-attack` | 禁用攻击打断冲刺 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用攻击时打断冲刺（1.8 PVP 手感）。 |
| `gameplay-mechanics.disable-relative-velocity-on-teleport` | 传送时禁用相对速度 | bool | `false`（`true`/`false`） | 🔄 | 玩家传送时是否禁用相对速度（防止甩出）。 |
| `gameplay-mechanics.ink-block-black-list` | 墨囊方块黑名单 | list | `[]` | 🔄 | 墨囊不能染色的方块列表。 |
| `gameplay-mechanics.mobs-disable-block-follow-range-rewrites` | 禁用方块跟随范围重写 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用生物方块跟随范围重写。 |
| `gameplay-mechanics.obbstruction-pickup` | 障碍拾取 | bool | `false`（`true`/`false`） | 🔄 | 是否启用障碍拾取。 |
| `gameplay-mechanics.player-collisions` | 玩家碰撞 | bool | `true`（`true`/`false`） | 🔄 | 是否启用玩家间碰撞。 |
| `gameplay-mechanics.player-cramming-damage` | 玩家挤压伤害 | bool | `false`（`true`/`false`） | 🔄 | 是否启用玩家挤压伤害。 |
| `gameplay-mechanics.shield-blocking-delay` | 盾牌格挡延迟 | int | `5`（≥ 0，毫秒） | 🔄 | 玩家举起盾牌到实际格挡的延迟（毫秒）。降低可让 PvP 更灵敏。 |
| `gameplay-mechanics.spawner-recover-air` | 刷怪笼恢复空气 | bool | `false`（`true`/`false`） | 🔄 | 刷怪笼生成的实体是否恢复空气。 |
| `gameplay-mechanics.tick-rates.block-update` | 方块更新频率 | int | `1`（≥ 0） | 🔄 | 方块更新的 tick 频率。 |
| `gameplay-mechanics.tick-rates.mob-spawner` | 刷怪笼 tick 频率 | int | `1`（≥ 0） | 🔄 | 刷怪笼的 tick 频率。 |
| `gameplay-mechanics.tnt-entity-merging-not-bouncing` | TNT 实体合并不反弹 | bool | `false`（`true`/`false`） | 🔄 | TNT 实体合并时不反弹。 |
| `gameplay-mechanics.water-bottle-empty-on-drink` | 水瓶饮尽清空 | bool | `true`（`true`/`false`） | 🔄 | 玩家饮用水瓶后是否变为空瓶。 |
| `gameplay-mechanics.piston-block-bounding-box` | 活塞方块边界 | bool | `true`（`true`/`false`） | 🔄 | 是否启用活塞方块边界修复。 |
| `gameplay-mechanics.arrow movement` | 箭矢移动修正 | bool | `false`（`true`/`false`） | 🔄 | 是否启用箭矢移动修正。 |

### 7. spawn-limits（生成上限）

> Paper 提供更精细的生成上限控制，按实体类型分类。覆盖 `bukkit.yml` 的 `spawn-limits`。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `spawn-limits.monsters` | 怪物上限 | int | `70`（≥ 0） | 🔄 | 每玩家怪物生成上限。覆盖 `bukkit.yml`。 |
| `spawn-limits.animals` | 动物上限 | int | `10`（≥ 0） | 🔄 | 每玩家动物生成上限。 |
| `spawn-limits.water-animals` | 水生动物上限 | int | `5`（≥ 0） | 🔄 | 每玩家水生动物上限。 |
| `spawn-limits.water-ambient` | 水生环境生物上限 | int | `20`（≥ 0） | 🔄 | 每玩家水生环境生物上限。 |
| `spawn-limits.ambient` | 环境生物上限 | int | `15`（≥ 0） | 🔄 | 每玩家环境生物（蝙蝠）上限。 |
| `spawn-limits.axolotls` | 美西螈上限 | int | `5`（≥ 0） | 🔄 | 每玩家美西螈上限。 |
| `spawn-limits.underground-water-creature` | 地下水生生物上限 | int | `5`（≥ 0） | 🔄 | 每玩家地下水生生物（发光鱿鱼）上限。 |

#### ticks-per（生成 tick 间隔）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `ticks-per.monster-spawn` | 怪物生成间隔 | int | `1`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次怪物生成。 |
| `ticks-per.animal-spawn` | 动物生成间隔 | int | `400`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次动物生成。 |
| `ticks-per.water-ambient-spawn` | 水生环境生物生成间隔 | int | `1`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次水生环境生物生成。 |
| `ticks-per.water-animal-spawn` | 水生动物生成间隔 | int | `1`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次水生动物生成。 |
| `ticks-per.axolotl-spawn` | 美西螈生成间隔 | int | `1`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次美西螈生成。 |
| `ticks-per.ambient-spawn` | 环境生物生成间隔 | int | `1`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次环境生物生成。 |
| `ticks-per.underground-water-spawn` | 地下水生生物生成间隔 | int | `1`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次地下水生生物生成。 |

### 8. hopper（漏斗）

> Paper 优化漏斗为「延迟加载」机制，可大幅省 CPU。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `hopper.cooldown-when-full` | 满时冷却 | bool | `true`（`true`/`false`） | 🔄 | 漏斗满时是否进入冷却（不再尝试传输），可省 CPU。 |
| `hopper.disable-move-event` | 禁用移动事件 | bool | `false`（`true`/`false`） | 🔄 | ⚠️ 启用后漏斗不再触发 `InventoryMoveItemEvent`，大幅省 CPU 但插件无法监听漏斗传输。**仅在确认无插件依赖此事件时启用**。 |
| `hopper.ignore-occluding-blocks` | 忽略遮挡方块 | bool | `false`（`true`/`false`） | 🔄 | 漏斗是否忽略上方遮挡方块的检测。 |

### 9. fixes（漏洞修复 - 漏斗相关）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `fixes.fix-cannons` | 修复大炮 | bool | `false`（`true`/`false`） | 🔄 | 修复 TNT 大炮的 bug。 |

### 10. max-growth-height（最大生长高度）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `max-growth-height.cactus` | 仙人掌最大高度 | int | `3`（≥ 1） | 🔄 | 仙人掌最大生长高度（方块）。 |
| `max-growth-height.reeds` | 甘蔗最大高度 | int | `3`（≥ 1） | 🔄 | 甘蔗最大生长高度（方块）。 |
| `max-growth-height.bamboo` | 竹子最大高度 | int | `16`（≥ 1） | 🔄 | 竹子最大生长高度（方块）。 |
| `max-growth-height.bamboo.max` | 竹子最大高度 | int | `16`（≥ 1） | 🔄 | 竹子最大生长高度上限。 |
| `max-growth-height.bamboo.min` | 竹子最小高度 | int | `11`（≥ 1） | 🔄 | 竹子最小生长高度下限。 |

### 11. mob-settings（生物设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `entities.armor-stands.disable-tick` | 禁用盔甲架 tick | bool | `false`（`true`/`false`） | 🔄 | 是否完全禁用盔甲架 tick，省 CPU 但盔甲架无动画。 |

### 12. growth-modifiers（生长修正）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `growth.cactus-modifier` | 仙人掌生长修正 | int | `100`（≥ 0） | 🔄 | 仙人掌生长速度修正百分比。`100` = Vanilla。 |
| `growth.cane-modifier` | 甘蔗生长修正 | int | `100`（≥ 0） | 🔄 | 甘蔗生长速度修正百分比。 |
| `growth.melon-modifier` | 西瓜生长修正 | int | `100`（≥ 0） | 🔄 | 西瓜生长速度修正百分比。 |
| `growth.pumpkin-modifier` | 南瓜生长修正 | int | `100`（≥ 0） | 🔄 | 南瓜生长速度修正百分比。 |
| `growth.sapling-modifier` | 树苗生长修正 | int | `100`（≥ 0） | 🔄 | 树苗生长速度修正百分比。 |
| `growth.wheat-modifier` | 小麦生长修正 | int | `100`（≥ 0） | 🔄 | 小麦生长速度修正百分比。 |
| `growth.netherwart-modifier` | 地狱疣生长修正 | int | `100`（≥ 0） | 🔄 | 地狱疣生长速度修正百分比。 |
| `growth.vine-modifier` | 藤蔓生长修正 | int | `100`（≥ 0） | 🔄 | 藤蔓生长速度修正百分比。 |
| `growth.cocoa-modifier` | 可可豆生长修正 | int | `100`（≥ 0） | 🔄 | 可可豆生长速度修正百分比。 |
| `growth.bamboo-modifier` | 竹子生长修正 | int | `100`（≥ 0） | 🔄 | 竹子生长速度修正百分比。 |
| `growth.sweetberry-modifier` | 甜浆果生长修正 | int | `100`（≥ 0） | 🔄 | 甜浆果生长速度修正百分比。 |
| `growth.kelp-modifier` | 海带生长修正 | int | `100`（≥ 0） | 🔄 | 海带生长速度修正百分比。 |
| `growth.twistingvines-modifier` | 缠怨藤生长修正 | int | `100`（≥ 0） | 🔄 | 缠怨藤生长速度修正百分比。 |
| `growth.weepingvines-modifier` | 垂泪藤生长修正 | int | `100`（≥ 0） | 🔄 | 垂泪藤生长速度修正百分比。 |
| `growth.cavevines-modifier` | 洞穴藤生长修正 | int | `100`（≥ 0） | 🔄 | 洞穴藤生长速度修正百分比。 |
| `growth.glowberry-modifier` | 发光浆果生长修正 | int | `100`（≥ 0） | 🔄 | 发光浆果生长速度修正百分比。 |
| `growth.rooted-grass-modifier` | 根系草生长修正 | int | `100`（≥ 0） | 🔄 | 根系草生长速度修正百分比。 |
| `growth.mangrove-propagule-modifier` | 红树胎生苗生长修正 | int | `100`（≥ 0） | 🔄 | 红树胎生苗生长速度修正百分比。 |
| `growth.torchflower-modifier` | 火把花生长修正 | int | `100`（≥ 0） | 🔄 | 火把花生长速度修正百分比。 |
| `growth.crop-fruit-plant-modifier` | 作物果实修正 | int | `100`（≥ 0） | 🔄 | 作物果实生长修正百分比。 |
| `growth.pitcher-plant-modifier` | 瓶子草生长修正 | int | `100`（≥ 0） | 🔄 | 瓶子草生长速度修正百分比。 |

### 13. maps（地图）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `maps.enabled` | 启用地图 | bool | `true`（`true`/`false`） | 🔄 | 是否启用地图绘制。关闭后地图不更新。 |
| `maps.item-frame` | 物品展示框地图 | bool | `true`（`true`/`false`） | 🔄 | 物品展示框中的地图是否更新。 |
| `maps.auto-update` | 自动更新 | bool | `true`（`true`/`false`） | 🔄 | 地图是否自动更新。 |

### 14. behavior（行为）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `behavior.baby-zombie-movement-modifier` | 小僵尸移动修正 | double | `0.5`（≥ 0） | 🔄 | 小僵尸移动速度修正倍率。`0.5` = +50% 速度。 |
| `entities.behavior.zombies-target-turtle-eggs` | 僵尸目标海龟蛋 | bool | `true`（`true`/`false`） | 🔄 | 僵尸是否主动踩踏海龟蛋。关闭可省 CPU。 |
| `entities.behavior.polar-bears-target-players-on-attack` | 北极熊攻击玩家 | bool | `true`（`true`/`false`） | 🔄 | 北极熊被攻击后是否反击玩家。 |

### 15. visibility（可见性）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `entities.tracking.range.animals` | 动物追踪范围 | int | `48`（≥ 0） | 🔄 | 动物对玩家的可见距离。 |
| `entities.tracking.range.display` | 展示实体追踪范围 | int | `128`（≥ 0） | 🔄 | 展示实体的可见距离。 |
| `entities.tracking.range.misc` | 其他实体追踪范围 | int | `32`（≥ 0） | 🔄 | 其他实体的可见距离。 |
| `entities.tracking.range.monsters` | 怪物追踪范围 | int | `48`（≥ 0） | 🔄 | 怪物的可见距离。 |
| `entities.tracking.range.other` | 其他实体追踪范围 | int | `64`（≥ 0） | 🔄 | 其他实体的可见距离。 |
| `entities.tracking.range.players` | 玩家追踪范围 | int | `48`（≥ 0） | 🔄 | 其他玩家可见你的距离。 |
| `entities.tracking.range.tick-frequency` | tick 频率 | int | `1`（≥ 1） | 🔄 | 实体追踪的 tick 频率。 |

### 16. entity-activation-range（实体激活范围）

> Paper 在 Spigot 基础上增加了 `flying-monsters` 与 `villagers` 类型。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunks.entity-activation-range.animals` | 动物激活范围 | int | `32`（≥ 0） | 🔄 | 玩家距动物多少方块内时动物 AI 才 tick。 |
| `chunks.entity-activation-range.monsters` | 怪物激活范围 | int | `32`（≥ 0） | 🔄 | 玩家距怪物多少方块内时怪物 AI 才 tick。 |
| `chunks.entity-activation-range.raiders` | 袭击者激活范围 | int | `48`（≥ 0） | 🔄 | 袭击者的激活范围。 |
| `chunks.entity-activation-range.misc` | 其他实体激活范围 | int | `16`（≥ 0） | 🔄 | 其他实体的激活范围。 |
| `chunks.entity-activation-range.water` | 水生生物激活范围 | int | `16`（≥ 0） | 🔄 | 水生生物的激活范围。 |
| `chunks.entity-activation-range.flying-monsters` | 飞行怪物激活范围 | int | `32`（≥ 0） | 🔄 | 飞行怪物（恶魂、幻翼）的激活范围。 |
| `chunks.entity-activation-range.villagers` | 村民激活范围 | int | `32`（≥ 0） | 🔄 | 村民的激活范围。 |
| `chunks.entity-activation-range.villagers-work-immune-after` | 村民工作免疫后 | int | `100`（≥ 0，tick） | 🔄 | 村民在多少 tick 后工作免疫。 |
| `chunks.entity-activation-range.villagers-work-immune-for` | 村民工作免疫持续 | int | `20`（≥ 0，tick） | 🔄 | 村民工作免疫持续多少 tick。 |
| `chunks.entity-activation-range.villagers-active-for-panic` | 村民恐慌激活 | bool | `true`（`true`/`false`） | 🔄 | 村民恐慌时是否激活。 |
| `chunks.entity-activation-range.tick-inactive-villagers` | 休眠村民仍 tick | bool | `true`（`true`/`false`） | 🔄 | 休眠村民是否仍 tick（保证农场工作）。 |

### 17. snapshots（快照）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunks.delay-chunk-unloads-by` | 延迟卸载 | duration | `10s` | 🔄 | 玩家离开后多久卸载其加载的区块。 |
| `chunks.fix-curing-zombie-villager-exploit` | 修复治愈村民漏洞 | bool | `false`（`true`/`false`） | 🔄 | 修复治愈村民刷价格漏洞。 |
| `chunks.max-chunk-sends-per-tick` | 每 tick 区块发送上限 | int | `-1`（-1 = 自动；≥ 0） | 🔄 | 每 tick 给单玩家发送多少区块。 |
| `chunks.max-chunk-gens-per-tick` | 每 tick 区块生成上限 | int | `-1`（-1 = 自动；≥ 0） | 🔄 | 每 tick 为单玩家生成多少区块。 |

### 18. tick-rates（tick 频率）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `tick-rates.sensor.villager-secondary-poi` | 村民次级 POI 传感器 | int | `40`（≥ 0） | 🔄 | 村民次级兴趣点传感器 tick 频率。调大可省 CPU。 |
| `tick-rates.behavior.villager-baby-make-love` | 小村民相爱行为 | int | `5`（≥ 0） | 🔄 | 小村民相爱行为 tick 频率。 |
| `tick-rates.behavior.villager-acquire-poi` | 村民获取 POI 行为 | int | `120`（≥ 0） | 🔄 | 村民获取兴趣点行为 tick 频率。 |

---

## 配置示例（paper-global.yml 完整默认值节选）

```yaml
chunk-loading:
  basic-maximizer-chunk-limit: 4
  player-max-chunk-load-rate: -1.0
  player-max-chunk-generate-rate: -1.0
  global-max-chunk-load-rate: -1.0
  global-max-chunk-generate-rate: -1.0
chunk-system:
  io-threads: -1
  worker-threads: -1
  gen:
    parallelism: -1
collisions:
  enable-player-collisions: true
  send-player-pos-when-teleporting: true
  send-player-pos-when-colliding-with: true
commands:
  time-command-affects-all-worlds: true
  fix-target-selector-tag-completion: true
console:
  enable-brigadier-highlighting: true
  enable-brigadier-completions: true
  has-all-permissions: false
item-validation:
  book-size:
    page-max: 2560
    total-multiplier: 0.98
  display-name: 8192
  resolve-selectors-in-books: false
logging:
  use-rgb-for-name-component: true
  log-ping-packet-length-mismatch: false
  deobfuscate-stacktraces: true
misc:
  chat-threads:
    core-size: -1
    max-size: -1
  server-activity:
    timeunit: SECONDS
    timeout: 60
  max-joins-per-tick: 5
  player-auto-save-rate: -1
  max-player-auto-save-per-tick: -1
  fix-wrong-rotations: false
packet-limiter:
  kick-message: "<red><lang:disconnect.exceeded_packet_rate></red>"
  packet-limit:
    settings:
      all: {action: KICK, interval: 7.0s, max-packet-rate: 500.0}
    overrides:
      ServerboundPlaceRecipePacket: {action: DROP, interval: 4.0s, max-packet-rate: 5.0}
player-auto-save:
  rate: -1
  max-per-tick: -1
proxies:
  bungee-cord:
    online-mode: false
  velocity:
    enabled: false
    online-mode: false
    secret: ""
  proxy-protocol: false
spam-limiter:
  tab-spam-increment: 1.0
  tab-spam-limit: 500.0
  recipe-spam-increment: 1.0
  recipe-spam-limit: 20.0
  ignored-packets: []
timings:
  enabled: false
  verbose: true
  url: https://timings.aikar.co/
  history-interval: 60s
  history-length: 3600s
  hidden-config-entries:
    - database
    - proxies.velocity.secret
  server-name: Unknown Server
  server-name-privacy: false
unsupported-settings:
  allow-headless-pistons: false
  allow-permanent-block-break-exploits: false
  allow-piston-duplication: false
  perform-username-validation: true
```

## 配置示例（paper-world-defaults.yml 完整默认值节选）

```yaml
chunks:
  auto-save-interval: 6000t
  delay-chunk-unloads-by: 10s
  entity-activation-range:
    ignore-spectators: true
  fixed-chunk-inhabited-time: -1
  max-auto-save-chunks-per-tick: 24
  prevent-moving-into-unloaded-chunks: false
  max-chunk-sends-per-tick: -1
  max-chunk-gens-per-tick: -1
  fix-curing-zombie-villager-exploit: false
collisions:
  max-entity-collisions: 8
  enable-player-collisions: true
  allow-player-cramming-damage: false
entities:
  mark-experimental-as-different: true
  behavior:
    baby-zombie-movement-modifier: 0.5
    disable-zombie-aggression-toward-villager: false
    zombies-target-turtle-eggs: true
    polar-bears-target-players-on-attack: true
    pufferfish-height-limit: 14.0
    spider-world-wrap: true
  spawning:
    counted-all-chunks-for-spawn: false
    immediate-despawn-impatience-factor: 0
    despawn-ranges:
      ambient: [32, 128]
      axolotls: [32, 128]
      creature: [32, 128]
      misc: [32, 128]
      monster: [32, 128]
      underground_water_creature: [32, 128]
      water_ambient: [32, 64]
      water_creature: [32, 128]
    per-player-mob-spawns: true
    scan-for-named-mobs: true
    despawn-tick-rates: {...}
    all-chunks-are-slime-chunks: false
    filtered-entity-spawning-tick-rates: {}
    bypass-spawn-ranges: []
  armor-stands:
    do-collision-entity-lookups: false
    tick: true
    disable-when-hidden: true
  sniffer-paced-hatching: true
  cluttered-category-size: 16
environment:
  disable-thunder: false
  disable-ice-and-snow: false
  disable-vehicle-phantom-phantom: false
  fixed-time: ""
  void-teleport-height: -100000000
  historical-tracking-enabled: false
fixes:
  disable-unloaded-chunk-entities: false
  falling-block-teleport-vehicles: false
  fix-curing-zombie-villager-exploit: false
  disable-relative-teleport-velocity-exploit: true
  pear-exploit: true
  tnt-entity-merging-not-bouncing: false
  split-overstacked-loot: true
  parked-tick-list-entities: false
gameplay-mechanics:
  chorus-fruit-teleport-rideable-entities: true
  chorus-fruit-teleport-riders: true
  disable-chest-cat-detection: false
  disable-player-crits: false
  disable-sprint-interruption-on-attack: false
  disable-relative-velocity-on-teleport: false
  ink-block-black-list: []
  mobs-disable-block-follow-range-rewrites: false
  obstruction-pickup: false
  shield-blocking-delay: 5
  spawner-recover-air: false
  water-bottle-empty-on-drink: true
  player-collisions: true
  player-cramming-damage: false
  tnt-entity-merging-not-bouncing: false
  piston-block-bounding-box: true
  tick-rates:
    block-update: 1
    mob-spawner: 1
hopper:
  cooldown-when-full: true
  disable-move-event: false
  ignore-occluding-blocks: false
max-growth-height:
  cactus: 3
  reeds: 3
  bamboo:
    max: 16
    min: 11
spawn-limits:
  monsters: 70
  animals: 10
  water-animals: 5
  water-ambient: 20
  ambient: 15
  axolotls: 5
  underground-water-creature: 5
ticks-per:
  monster-spawn: 1
  animal-spawn: 400
  water-ambient-spawn: 1
  water-animal-spawn: 1
  axolotl-spawn: 1
  ambient-spawn: 1
  underground-water-spawn: 1
growth:
  cactus-modifier: 100
  cane-modifier: 100
  melon-modifier: 100
  pumpkin-modifier: 100
  sapling-modifier: 100
  wheat-modifier: 100
  netherwart-modifier: 100
  vine-modifier: 100
  cocoa-modifier: 100
  bamboo-modifier: 100
  sweetberry-modifier: 100
  kelp-modifier: 100
  twistingvines-modifier: 100
  weepingvines-modifier: 100
  cavevines-modifier: 100
  glowberry-modifier: 100
  rooted-grass-modifier: 100
  mangrove-propagule-modifier: 100
  torchflower-modifier: 100
  pitcher-plant-modifier: 100
maps:
  enabled: true
  item-frame: true
  auto-update: true
entities:
  tracking:
    range:
      animals: 48
      display: 128
      misc: 32
      monsters: 48
      other: 64
      players: 48
    tick-frequency: 1
  armor-stands:
    disable-tick: false
tick-rates:
  sensor:
    villager-secondary-poi: 40
  behavior:
    villager-baby-make-love: 5
    villager-acquire-poi: 120
```

---

## 优化建议（针对大型服务器）

1. **`per-player-mob-spawns=true`**：Paper 性能核心，按玩家计算生物上限，更精确且防「生物额度被盗」。
2. **`hopper.disable-move-event=true`**：⚠️ 仅在确认无插件依赖 `InventoryMoveItemEvent` 时启用，可大幅省 CPU（漏斗农场服提升明显）。
3. **`hopper.cooldown-when-full=true`**：保持开启，漏斗满时不再尝试传输，省 CPU。
4. **`chunks.delay-chunk-unloads-by=30s`**：玩家来回移动多的大厅服可调到 `30s`，减少重复加载。
5. **`chunk-system.io-threads` 与 `worker-threads`**：玩家 > 200 时手动设置（IO 线程 4、工作线程 8）。
6. **`entities.armor-stands.tick=false`**：盔甲架装饰多的大厅服可关闭，省 CPU 但盔甲架无动画。
7. **`entities.behavior.zombies-target-turtle-eggs=false`**：无海龟蛋农场的服务器可关闭，省 CPU。
8. **`gameplay-mechanics.shield-blocking-delay=0`**：PvP 服调到 `0` 让盾牌更灵敏（接近 1.8 手感）。
9. **`spawn-limits.monsters=50`**：低配服从 `70` 降到 `50`，配合 `ticks-per.monster-spawn=5` 可显著省 CPU。
10. **`proxies.velocity.enabled=true`**：前置 Velocity 时启用并设置 `secret`，`server.properties` 的 `online-mode` 必须设为 `false`。
11. **`timings.enabled=false`**：Paper 1.21+ 已弃用 Timings，推荐安装 [Spark](https://spark.lucko.me/) 替代。
12. **`packet-limiter`**：默认配置已可防大多数 DDoS，生产环境保持默认即可。
13. **`unsupported-settings.allow-piston-duplication=true`**：⚠️ 仅在玩家强烈要求保留 TNT 复制机等技术机器时启用，否则保持 `false`。
14. **`max-joins-per-tick=5`**：防止开服时大量玩家同时登录导致卡顿，可调到 `3`。

> 参考来源：[PaperMC 官方文档](https://docs.papermc.io/)、PaperMC 源码 `paper-api/src/main/java/io/papermc/paper/configuration`（GlobalConfiguration.java / WorldConfiguration.java，1.21.x 分支）。
