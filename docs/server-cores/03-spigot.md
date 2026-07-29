# Spigot 服务器配置文件中文手册

> Spigot 是 CraftBukkit 的继任者，在 Bukkit API 基础上增加了大量性能优化、配置选项和反作弊机制。是现代 Java 版服务端的事实基线（Paper/Purpur/Pufferfish 等均基于 Spigot）。
> 继承关系：**Vanilla → Bukkit → Spigot**
> 官方网站：https://www.spigotmc.org/
> 官方 Wiki：https://www.spigotmc.org/wiki/spigot-configuration/
> 数据来源：SpigotMC Wiki / Spigot 源码 `org.spigotmc.SpigotConfig` / `SpigotWorldConfig`
> 适用版本基准：Spigot 1.21.x（2025–2026 稳定版）

Spigot 完整继承 Vanilla 的 `server.properties` 与 Bukkit 的 `bukkit.yml` / `permissions.yml` / `commands.yml` / `help.yml`，并新增自己的 `spigot.yml`。本文档**仅翻译 Spigot 独有的 `spigot.yml`**，其余配置请参阅对应手册（Vanilla / Bukkit）。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（请参阅 Vanilla 手册） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置（请参阅 Bukkit 手册） |
| permissions.yml | YAML | Bukkit 继承 | 默认权限组（请参阅 Bukkit 手册） |
| commands.yml | YAML | Bukkit 继承 | 命令别名（请参阅 Bukkit 手册） |
| help.yml | YAML | Bukkit 继承 | 帮助页配置（请参阅 Bukkit 手册） |
| **spigot.yml** | YAML | **Spigot 专属** | **Spigot 配置（本文档重点）** |

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `settings.bungeecord`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举 / `double` 浮点 / `list` 列表 / `map` 映射。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示必须重启服务器才能生效；🔄 表示支持 `/reload` 热重载（Spigot 多数项需重启）。
- `spigot.yml` 由 `org.spigotmc.SpigotConfig` 加载，启动时读取。

---

## spigot.yml（Spigot 专属配置）

### 1. settings（全局设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.bungeecord` | 启用 BungeeCord 支持 | bool | `false`（`true`/`false`） | ✅ | 是否前置 BungeeCord 代理。⚠️ 启用后 `server.properties` 的 `online-mode` 必须设为 `false`，由 BungeeCord 做正版验证。否则玩家可绕过验证伪造身份。 |
| `settings.timeout-time` | 卡死超时阈值 | int | `60`（≥ 0，秒） | 🔄 | 主线程连续无响应多少秒后判定为「卡死」并触发 watchdog。`0` 禁用 watchdog（不推荐）。 |
| `settings.restart-on-crash` | 崩溃自动重启 | bool | `true`（`true`/`false`） | ✅ | 服务器崩溃时是否自动执行 `restart-script`。 |
| `settings.restart-script` | 重启脚本路径 | string | `./start.sh`（脚本路径） | ✅ | 崩溃自动重启时执行的脚本路径。Linux 用 `./start.sh`，Windows 用 `start.bat`。 |
| `settings.sample-count` | 状态采样人数 | int | `12`（≥ 0） | 🔄 | 服务器列表 ping 时显示的「在线玩家预览」人数。`0` 不显示预览。降低可减少网络包。 |
| `settings.player-shuffle` | 玩家洗牌 | bool | `false`（`true`/`false`） | 🔄 | 是否在每 tick 随机打乱玩家处理顺序。可避免某些玩家始终先被处理的优势，但略增 CPU 开销。 |
| `settings.moved-wrongly-threshold` | 移动错误阈值 | double | `0.0625`（≥ 0） | 🔄 | 玩家移动距离与服务器预期差距超过此值时判定为「移动错误」并回滚。值越小越严格。 |
| `settings.moved-too-quickly-multiplier` | 移动过快倍率 | double | `10.0`（≥ 0） | 🔄 | 玩家单 tick 移动距离超过此倍率的「预期距离」时判定为「移动过快」并回滚。降低可更严格防飞行。 |
| `settings.netty-threads` | Netty IO 线程数 | int | `4`（≥ 1） | ✅ | 处理玩家网络数据包的 Netty 线程数。大型服可调到 `8`–`16`。一般保持默认。 |
| `settings.log-villager-deaths` | 记录村民死亡 | bool | `true`（`true`/`false`） | 🔄 | 是否在日志中记录村民死亡事件。村民农场较多的服务器可关闭以减少日志。 |
| `settings.log-named-deaths` | 记录命名实体死亡 | bool | `true`（`true`/`false`） | 🔄 | 是否在日志中记录用命名牌命名过的实体死亡事件。 |
| `settings.save-user-cache` | 保存用户缓存 | bool | `true`（`true`/`false`） | 🔄 | 是否将 `usercache.json` 写入磁盘。关闭后仅内存缓存，重启丢失玩家信息。 |
| `settings.user-cache-size` | 用户缓存大小 | int | `1000`（≥ 0） | 🔄 | `usercache.json` 缓存的最大玩家数。超过时淘汰最旧记录。 |
| `settings.plugin-profiling` | 插件性能分析 | bool | `false`（`true`/`false`） | 🔄 | 是否启用 `/timings` 命令记录插件性能数据。开启有少量开销，仅在排查性能时启用。 |
| `settings.connection-throttle` | 连接节流 | int | `4000`（≥ 0，毫秒） | 🔄 | 同一玩家两次连接之间的最小间隔。`0` 无限制。降低可让玩家快速重连，但易被刷连接攻击。 |
| `settings.internal-ping` | 内部 ping 响应 | bool | `false`（`true`/`false`） | 🔄 | 是否响应服务器内部的 ping 请求。一般保持 `false`。 |
| `settings.hidden-method` | 隐藏方法 | string | ` `（空） | 🔄 | 隐藏特定的方法调用，调试用。一般留空。 |
| `settings.attributes` | 自定义属性 | map | `{}`（YAML map） | 🔄 | 自定义实体属性上限。键为属性名，值为 `max`/`min`。 |
| `settings.attribute.maxHealth.max` | 最大生命值上限 | double | `2048.0`（≥ 0） | 🔄 | 实体最大生命值属性的上限。降低可防止插件设置变态血量。 |
| `settings.attribute.maxHealth.min` | 最大生命值下限 | double | `1.0`（≥ 0） | 🔄 | 实体最大生命值属性的下限。 |
| `settings.attribute.attackDamage.max` | 攻击伤害上限 | double | `2048.0`（≥ 0） | 🔄 | 实体攻击伤害属性的上限。 |
| `settings.attribute.attackDamage.min` | 攻击伤害下限 | double | `0.0`（≥ 0） | 🔄 | 实体攻击伤害属性的下限。 |
| `settings.attribute.movementSpeed.max` | 移动速度上限 | double | `2048.0`（≥ 0） | 🔄 | 实体移动速度属性的上限。降低可防止插件设置变态速度。 |
| `settings.attribute.movementSpeed.min` | 移动速度下限 | double | `0.0`（≥ 0） | 🔄 | 实体移动速度属性的下限。 |

### 2. commands（命令设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `commands.replace-commands` | 命令替换列表 | list | `[setblock, summon, testforblock, tellraw]` | 🔄 | 用 Spigot 实现替换 Vanilla 命令的列表。替换后命令由 Spigot 处理，支持插件 Hook。 |
| `commands.spam-exclusions` | 命令刷屏排除 | list | `[]` | 🔄 | 不计入命令刷屏检测的命令列表。如 `[]` 或 `[say, me]`。 |
| `commands.log` | 记录命令日志 | bool | `true`（`true`/`false`） | 🔄 | 是否在日志中记录玩家执行的命令。关闭可减少日志量，但失去审计能力。 |
| `commands.silent-commandblock-console` | 静默命令方块日志 | bool | `false`（`true`/`false`） | 🔄 | 是否禁止命令方块执行命令时在控制台输出日志。开启可大幅减少日志噪音。 |
| `commands.silent-commandblock-console` | 静默命令方块输出 | bool | `false`（`true`/`false`） | 🔄 | 命令方块执行命令时是否在控制台静默。⚠️ 开启后命令方块日志完全不输出。 |

### 3. messages（消息定制）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `messages.whitelist` | 白名单提示 | string | `You are not whitelisted on this server!` | 🔄 | 非白名单玩家加入时显示的提示。支持 `§` 颜色码。 |
| `messages.unknown-command` | 未知命令提示 | string | `Unknown command. Type "/help" for help.` | 🔄 | 覆盖 `bukkit.yml` 的未知命令提示。 |
| `messages.server-full` | 服务器满员提示 | string | `The server is full!` | 🔄 | 服务器满员时新玩家加入显示的提示。 |
| `messages.outdated-client` | 客户端版本过低提示 | string | `Outdated client! Please use {0}` | 🔄 | 客户端版本过低时显示。`{0}` = 服务器版本号。 |
| `messages.outdated-server` | 服务器版本过低提示 | string | `Outdated server! I'm still on {0}` | 🔄 | 客户端版本过高时显示。`{0}` = 服务器版本号。 |
| `messages.restart` | 重启提示 | string | `Server is restarting` | 🔄 | 服务器重启时踢出玩家显示的提示。 |

### 4. stats（统计）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `stats.disable-saving` | 禁用统计保存 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用玩家自定义统计（成就、距离等）的保存。⚠️ 关闭后玩家统计不持久化。 |
| `stats.forced-stats` | 强制统计值 | map | `{}`（YAML map） | 🔄 | 强制设置的统计数据值。键为统计名，值为数值。如 `stats.forced-stats.minecraft.custom:minecraft.jump: 100`。 |

### 5. advancements（成就）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `advancements.disable-saving` | 禁用成就保存 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用玩家成就进度保存。⚠️ 关闭后玩家成就不持久化。 |
| `advancements.disabled` | 禁用成就列表 | list | `[]` | 🔄 | 禁用的成就命名空间列表。如 `[minecraft:story/minecraft, minecraft:end/dragon_egg]`。 |

### 6. world-settings.default（世界默认设置）

> `world-settings.default` 是所有世界的默认配置，每个世界可单独覆盖（`world-settings.<世界名>.<键>`）。本文仅列 `default` 节。

#### 6.1 基础世界设置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.spawn-radius` | 出生保护半径 | int | `16`（≥ 0） | 🔄 | 出生点周围多少方块半径内仅 OP 可破坏。⚠️ 与 `server.properties` 的 `spawn-protection` 二选一，Spigot 用此键覆盖。`0` 禁用。 |
| `world-settings.default.view-distance` | 视野距离 | int | `-1`（-1 = 使用 server.properties；3–32 = 覆盖） | 🔄 | 每个世界的视野距离。`-1` 继承 `server.properties`。覆盖可让末地视距更小。 |
| `world-settings.default.simulation-distance` | 模拟距离 | int | `-1`（-1 = 使用 server.properties；3–32 = 覆盖） | 🔄 | 每个世界的模拟距离。`-1` 继承 `server.properties`。 |
| `world-settings.default.verbose` | 详细日志 | bool | `true`（`true`/`false`） | 🔄 | 是否在启动时打印世界配置详情。生产环境可关闭。 |

#### 6.2 实体激活范围（entity-activation-range）

> **Spigot 性能优化核心**：远离玩家的实体其 AI 会被「休眠」，仅当玩家进入激活范围时才 tick AI。可大幅降低 CPU 占用。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.entity-activation-range.animals` | 动物激活范围 | int | `32`（≥ 0，方块） | 🔄 | 玩家距动物多少方块内时动物 AI 才 tick。值越小越省 CPU，但动物反应迟钝。 |
| `world-settings.default.entity-activation-range.monsters` | 怪物激活范围 | int | `32`（≥ 0，方块） | 🔄 | 玩家距怪物多少方块内时怪物 AI 才 tick。值越小越省 CPU，但怪物不会主动攻击远处玩家。 |
| `world-settings.default.entity-activation-range.raiders` | 袭击者激活范围 | int | `48`（≥ 0，方块） | 🔄 | 玩家距袭击者（掠夺者等）多少方块内时其 AI 才 tick。 |
| `world-settings.default.entity-activation-range.misc` | 其他实体激活范围 | int | `16`（≥ 0，方块） | 🔄 | 玩家距其他实体（掉落物、矿车等）多少方块内时其 AI 才 tick。 |
| `world-settings.default.entity-activation-range.water` | 水生生物激活范围 | int | `16`（≥ 0，方块） | 🔄 | 玩家距水生生物多少方块内时其 AI 才 tick。 |
| `world-settings.default.entity-activation-range.tick-inactive-villagers` | 休眠村民仍 tick | bool | `true`（`true`/`false`） | 🔄 | `true` 时村民即使在激活范围外仍 tick（保证农场工作）。`false` 严格按范围休眠，更省 CPU 但村民农场不工作。 |
| `world-settings.default.entity-activation-range.wake-up-inactive.animals-max-per-tick` | 唤醒动物上限 | int | `4`（≥ 0） | 🔄 | 每 tick 最多唤醒多少个休眠动物。 |
| `world-settings.default.entity-activation-range.wake-up-inactive.animals-every` | 唤醒动物间隔 | int | `1200`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次唤醒休眠动物。 |
| `world-settings.default.entity-activation-range.wake-up-inactive.monsters-max-per-tick` | 唤醒怪物上限 | int | `8`（≥ 0） | 🔄 | 每 tick 最多唤醒多少个休眠怪物。 |
| `world-settings.default.entity-activation-range.wake-up-inactive.monsters-every` | 唤醒怪物间隔 | int | `400`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次唤醒休眠怪物。 |

#### 6.3 实体追踪范围（entity-tracking-range）

> 控制实体对玩家的可见距离（客户端能「看到」实体的距离）。超出范围实体对客户端不可见，会突然消失 / 出现。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.entity-tracking-range.players` | 玩家追踪范围 | int | `48`（≥ 0，方块） | 🔄 | 其他玩家可见你的距离。 |
| `world-settings.default.entity-tracking-range.animals` | 动物追踪范围 | int | `48`（≥ 0，方块） | 🔄 | 动物对玩家的可见距离。 |
| `world-settings.default.entity-tracking-range.monsters` | 怪物追踪范围 | int | `48`（≥ 0，方块） | 🔄 | 怪物对玩家的可见距离。 |
| `world-settings.default.entity-tracking-range.misc` | 其他实体追踪范围 | int | `32`（≥ 0，方块） | 🔄 | 其他实体（掉落物、矿车、经验球等）的可见距离。 |
| `world-settings.default.entity-tracking-range.other` | 其他实体追踪范围 | int | `64`（≥ 0，方块） | 🔄 | 其他类型实体（如展示框）的可见距离。 |
| `world-settings.default.entity-tracking-range.display` | 展示实体追踪范围 | int | `128`（≥ 0，方块） | 🔄 | 1.19.4+ 展示实体（物品展示、文字展示、方块展示）的可见距离。 |

#### 6.4 生物生成频率（ticks-per-spawns）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.ticks-per-spawns.animal-spawns` | 动物生成间隔 | int | `-1`（-1 = 使用 bukkit.yml；≥ 0） | 🔄 | 多少 tick 尝试一次动物生成。`-1` 继承 `bukkit.yml`。 |
| `world-settings.default.ticks-per-spawns.monster-spawns` | 怪物生成间隔 | int | `-1`（-1 = 使用 bukkit.yml；≥ 0） | 🔄 | 多少 tick 尝试一次怪物生成。`-1` 继承 `bukkit.yml`。 |

#### 6.5 区块与加载

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.chunk-gc-period-in-ticks` | 区块 GC 间隔 | int | `600`（≥ 0，tick） | 🔄 | 多少 tick 执行一次区块 GC。⚠️ Paper 已废弃此机制。 |
| `world-settings.default.entity-activation-range.flying-monsters` | 飞行怪物激活范围 | int | `32`（≥ 0，方块） | 🔄 | 飞行怪物（恶魂、幻翼等）的激活范围。 |
| `world-settings.default.random-light-updates` | 随机光照更新 | bool | `false`（`true`/`false`） | 🔄 | 是否启用随机光照更新。⚠️ 已弃用，保持 `false`。 |
| `world-settings.default.mob-spawn-range` | 生物生成范围 | int | `4`（≥ 0，区块） | 🔄 | 玩家周围多少区块半径内尝试生成生物。降低可减少怪物生成密度。 |
| `world-settings.default.hopper-transfer` | 漏斗传输间隔 | int | `8`（≥ 0，tick） | 🔄 | 漏斗传输物品的间隔 tick。`8` = 每 0.4 秒传输一次。调大可省 CPU 但漏斗变慢。 |
| `world-settings.default.hopper-check` | 漏斗检测间隔 | int | `1`（≥ 0，tick） | 🔄 | 漏斗检测上方物品的间隔 tick。调大可省 CPU 但漏斗响应慢。 |
| `world-settings.default.hopper-amount` | 漏斗每次传输数 | int | `1`（≥ 1） | 🔄 | 漏斗每次传输多少个物品。调大可让漏斗更快（同时改变 Vanilla 平衡）。 |
| `world-settings.default.max-entity-collisions` | 实体碰撞上限 | int | `8`（≥ 0） | 🔄 | 单个实体最多同时与多少个实体发生碰撞。`0` 禁用碰撞。降低可缓解实体密集卡顿（如鸡农场）。 |
| `world-settings.default.max-tick-time.entity` | 实体 tick 上限 | int | `50`（≥ 0，毫秒） | 🔄 | 每 tick 实体处理最大耗时。超过即放弃剩余实体 tick。 |
| `world-settings.default.max-tick-time.tile` | 方块实体 tick 上限 | int | `50`（≥ 0，毫秒） | 🔄 | 每 tick 方块实体处理最大耗时。 |
| `world-settings.default.dragon-death-sound-radius` | 末影龙死亡声音半径 | int | `0`（≥ 0，方块） | 🔄 | 末影龙死亡声音可听范围。`0` = 全服可听。 |

#### 6.6 成长与生成限制

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.seed-village` | 村庄种子 | int | `10387312` | 🔄 | 村庄生成种子。修改可让村庄重新分布（仅影响新生成区块）。 |
| `world-settings.default.seed-desert` | 沙漠神殿种子 | int | `14357617` | 🔄 | 沙漠神殿生成种子。 |
| `world-settings.default.seed-monument` | 海底神殿种子 | int | `10387313` | 🔄 | 海底神殿生成种子。 |
| `world-settings.default.seed-slime` | 史莱姆种子 | int | `10387318` | 🔄 | 史莱姆区块生成种子。修改可改变史莱姆区块分布。 |
| `world-settings.default.seed-stronghold` | 末地要塞种子 | int | `10387312` | 🔄 | 末地要塞生成种子。 |
| `world-settings.default.seed-outpost` | 掠夺者前哨站种子 | int | `10387317` | 🔄 | 掠夺者前哨站生成种子。 |
| `world-settings.default.seed-endcity` | 末地城种子 | int | `10387313` | 🔄 | 末地城生成种子。 |
| `world-settings.default.seed-nether` | 下界结构种子 | int | `30084232` | 🔄 | 下界堡垒等结构生成种子。 |
| `world-settings.default.seed-mansion` | 林地府邸种子 | int | `10387319` | 🔄 | 林地府邸生成种子。 |
| `world-settings.default.seed-fossil` | 化石种子 | int | `14357921` | 🔄 | 化石生成种子。 |
| `world-settings.default.seed-portal` | 传送门种子 | int | `34222645` | 🔄 | 传送门生成种子。 |
| `world-settings.default.hanging-tick-frequency` | 悬挂实体 tick 频率 | int | `100`（≥ 0，tick） | 🔄 | 画、展示框等悬挂实体的 tick 频率。 |
| `world-settings.default.zombie-aggressive-towards-villager` | 僵尸主动攻击村民 | bool | `true`（`true`/`false`） | 🔄 | 僵尸是否主动攻击村民。关闭可降低村民死亡（但失去 Vanilla 玩法）。 |
| `world-settings.default nerf-spawner-mobs` | 弱化刷怪笼怪物 | bool | `false`（`true`/`false`） | 🔄 | 刷怪笼生成的怪物是否被弱化（无 AI、不攻击、不移动）。开启可大幅省 CPU，但破坏刷怪塔。 |
| `world-settings.default.enable-zombie-pigmen-portal-spawns` | 传送门生成僵尸猪灵 | bool | `true`（`true`/`false`） | 🔄 | 下界传送门是否生成僵尸猪灵。关闭可防止刷怪塔与意外刷怪。 |

#### 6.7 成长与天气

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.item-merge-radius` | 物品合并半径 | double | `2.5`（≥ 0，方块） | 🔄 | 掉落物合并的距离。调大可减少实体数（省 CPU），但农场掉落物合并可能影响收集。 |
| `world-settings.default.exp-merge-radius` | 经验球合并半径 | double | `3.0`（≥ 0，方块） | 🔄 | 经验球合并的距离。调大可减少实体数。 |
| `world-settings.default.max-growth-height` | 最大生长高度 | map | `{cactus: 3, reeds: 3}` | 🔄 | 仙人掌、甘蔗的最大生长高度（方块）。超过即停止生长。 |
| `world-settings.default.growth-modifier` | 作物生长修正 | map | `{cactus: 100, cane: 100, melon: 100, pumpkin: 100, sapling: 100, wheat: 100, netherwart: 100, vine: 100, cocoa: 100, bamboo: 100, sweetberry: 100, kelp: 100}` | 🔄 | 各种作物的生长速度修正百分比。`100` = Vanilla 速度；`200` = 两倍速；`50` = 半速。 |

#### 6.8 其他世界设置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.hopper-can-load-chunks` | 漏斗可加载区块 | bool | `false`（`true`/`false`） | 🔄 | 漏斗上方无方块时是否触发区块加载。关闭可防止漏斗农场卡服。 |
| `world-settings.default.arrow-despawn-rate` | 箭矢消失速率 | int | `1200`（≥ 0，tick） | 🔄 | 箭矢多少 tick 后消失。`1200` = 60 秒。降低可减少箭矢积累。 |
| `world-settings.default.trident-despawn-rate` | 三叉戟消失速率 | int | `1200`（≥ 0，tick） | 🔄 | 三叉戟多少 tick 后消失。 |
| `world-settings.default.entity-activation-range.water-animals` | 水生动物激活范围 | int | `16`（≥ 0，方块） | 🔄 | 水生动物（鱿鱼等）的激活范围。 |
| `world-settings.default.merge-radius.item` | 物品合并半径 | double | `2.5`（≥ 0，方块） | 🔄 | 同 `item-merge-radius`。 |
| `world-settings.default.merge-radius.exp` | 经验合并半径 | double | `3.0`（≥ 0，方块） | 🔄 | 同 `exp-merge-radius`。 |

### 7. players（玩家设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `players.disable-saving` | 禁用玩家数据保存 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用玩家数据（背包、坐标、生命）保存。⚠️ 关闭后玩家进度不持久化。 |
| `players.disable-advancement-saving` | 禁用成就保存 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用玩家成就进度保存。 |

---

## 配置示例（spigot.yml 完整默认值）

```yaml
settings:
  bungeecord: false
  timeout-time: 60
  restart-on-crash: true
  restart-script: ./start.sh
  sample-count: 12
  player-shuffle: false
  moved-wrongly-threshold: 0.0625
  moved-too-quickly-multiplier: 10.0
  netty-threads: 4
  log-villager-deaths: true
  log-named-deaths: true
  save-user-cache: true
  user-cache-size: 1000
  plugin-profiling: false
  connection-throttle: 4000
  internal-ping: false
  hidden-method: []
  attributes:
    maxHealth:
      max: 2048.0
      min: 1.0
    attackDamage:
      max: 2048.0
      min: 0.0
    movementSpeed:
      max: 2048.0
      min: 0.0
commands:
  replace-commands:
  - setblock
  - summon
  - testforblock
  - tellraw
  spam-exclusions:
  - /skill
  log: true
  silent-commandblock-console: false
messages:
  whitelist: You are not whitelisted on this server!
  unknown-command: Unknown command. Type "/help" for help.
  server-full: The server is full!
  outdated-client: Outdated client! Please use {0}
  outdated-server: Outdated server! I'm still on {0}
  restart: Server is restarting
stats:
  disable-saving: false
  forced-stats: {}
advancements:
  disable-saving: false
  disabled: []
world-settings:
  default:
    verbose: true
    spawn-radius: 16
    view-distance: -1
    simulation-distance: -1
    entity-activation-range:
      animals: 32
      monsters: 32
      raiders: 48
      misc: 16
      water: 16
      villagers: 32
      flying-monsters: 32
      wake-up-inactive:
        animals-max-per-tick: 4
        animals-every: 1200
        animals-for: 100
        monsters-max-per-tick: 8
        monsters-every: 400
        monsters-for: 100
        villagers-max-per-tick: 4
        villagers-every: 600
        villagers-for: 100
        flying-monsters-max-per-tick: 8
        flying-monsters-every: 200
        flying-monsters-for: 100
      villagers-work-immune-after: 100
      villagers-work-immune-for: 20
      villagers-active-for-panic: true
      tick-inactive-villagers: true
      creature-activation-range-override: {}
    entity-tracking-range:
      players: 48
      animals: 48
      monsters: 48
      misc: 32
      other: 64
      display: 128
    ticks-per-spawns:
      animal-spawns: -1
      monster-spawns: -1
    mob-spawn-range: 4
    item-merge-radius: 2.5
    exp-merge-radius: 3.0
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
    max-growth-height:
      cactus: 3
      reeds: 3
    random-light-updates: false
    nerf-spawner-mobs: false
    enable-zombie-pigmen-portal-spawns: true
    max-entity-collisions: 8
    max-tick-time:
      tile: 50
      entity: 50
    dragon-death-sound-radius: 0
    seed-village: 10387312
    seed-desert: 14357617
    seed-monument: 10387313
    seed-slime: 10387318
    seed-stronghold: 10387312
    seed-outpost: 10387317
    seed-endcity: 10387313
    seed-nether: 30084232
    seed-mansion: 10387319
    seed-fossil: 14357921
    seed-portal: 34222645
    arrow-despawn-rate: 1200
    trident-despawn-rate: 1200
    hopper-transfer: 8
    hopper-check: 1
    hopper-amount: 1
    hopper-can-load-chunks: false
    zombie-aggressive-towards-villager: true
    hanging-tick-frequency: 100
    chunk-gc-period-in-ticks: 600
    fix-curing-zombie-villager-exploit: true
    merge-radius:
      item: 2.5
      exp: 3.0
players:
  disable-saving: false
  disable-advancement-saving: false
```

---

## 优化建议（针对大型服务器）

1. **调整 `entity-activation-range`**：低配服可将 `monsters` 从 `32` 降到 `24`，`misc` 从 `16` 降到 `8`，可显著省 CPU。但不要过低否则怪物不会攻击。
2. **`max-entity-collisions`**：从 `8` 降到 `2`–`4` 可缓解实体农场（鸡、牛）卡顿，几乎无副作用。
3. **`item-merge-radius` 调大**：从 `2.5` 调到 `4.0`–`6.0`，让掉落物更易合并，减少实体数。
4. **`hopper-transfer` 调大**：从 `8` 调到 `16`–`24`，省 CPU 但漏斗变慢。可同时调大 `hopper-amount` 补偿速度。
5. **`hopper-check` 调大**：从 `1` 调到 `4`–`8`，可大幅省 CPU 但漏斗响应略慢。
6. **`settings.netty-threads`**：玩家 > 200 时调到 `8`，> 500 时调到 `16`。
7. **`arrow-despawn-rate`**：从 `1200` 调到 `300`，避免箭矢积累卡服。
8. **`commands.silent-commandblock-console=true`**：大量命令方块的服务器开启可减少日志噪音。
9. **`settings.bungeecord=true`**：前置 BungeeCord 时**必须**启用，否则玩家可伪造身份绕过验证。同时 `server.properties` 的 `online-mode` 必须设为 `false`。
10. **`ticks-per.monster-spawns` 调大**：在 `bukkit.yml` 中从 `1` 调到 `5`–`10`，怪物生成尝试变慢但每次更稳定。

> 参考来源：[SpigotMC Wiki - Spigot Configuration](https://www.spigotmc.org/wiki/spigot-configuration/)、Spigot 源码 `SpigotConfig.java` / `SpigotWorldConfig.java`。
