# Purpur 服务器配置文件中文手册

> Purpur 是基于 Paper 的高性能优化分支，主打**极致可配置性**。
> 继承关系：Vanilla → Spigot → Paper → Purpur
> 官方网站：https://purpurmc.org/
> 官方文档：https://purpurmc.org/docs/purpur/configuration/
> 源码仓库：https://github.com/PurpurMC/Purpur
> 数据来源：Purpur 官方文档 / GitHub 源码（PurpurConfig.java、PurpurWorldConfig.java）/ 社区优化指南
> 适用版本基准：Purpur 1.21.x（2025-2026 稳定版）

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置（生成上限、命令别名等） |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（实体激活范围、视距等） |
| paper-global.yml | YAML | Paper 继承 | Paper 全局配置（区块、网络、漏洞修复等） |
| paper-world-defaults.yml | YAML | Paper 继承 | Paper 世界默认配置（每世界可覆盖） |
| **purpur.yml** | YAML | **Purpur 专属** | **Purpur 独有配置（本文档重点）** |

> Purpur 还会为每个世界生成 `purpur.yml` 副本，覆盖 `world-settings.default.*` 的默认值。

## purpur.yml 整体结构

```yaml
settings:                              # 全局设置（影响整个服务器）
  verbose: false
  config-version: 31
  use-alternate-keepalive: false
  async-chunks: true
  lagging-threshold: 19
  seconds-to-stop-server-shutdown-after-cancel: 60
  command:                             # Purpur 内置命令配置
    uptime: { ... }
    gamemode: { ... }
    tpsbar: { ... }
    rambar: { ... }
    credits: { ... }
  velocity: { ... }                    # Velocity 代理支持
  bstats: { ... }                      # bStats 统计

world-settings:
  default:                             # 默认世界配置（每个世界可单独覆盖）
    gameplay-mechanics: { ... }        # 游戏机制（最多配置项集中地）
    blocks: { ... }                    # 方块配置（木桶、末影箱等）
    entities: { ... }                  # 实体通用配置
    mobs:                              # 每种生物独立配置（约 80+ 种生物）
      allay: { ... }
      axolotl: { ... }
      creeper: { ... }
      enderman: { ... }
      phantom: { ... }
      villager: { ... }
      zombie: { ... }
      # ... 共约 80 种生物
    ridables: { ... }                  # 全局可骑乘配置
    player: { ... }                    # 玩家配置
    fishing-time: { ... }              # 钓鱼时间
    misc: { ... }                      # 杂项
```

---

## settings（全局服务器设置）

> 影响整个服务器，修改后部分项需要重启。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.verbose` | 详细日志输出 | `false` | bool | true/false | 是 | 启动时把所有配置值打印到服务器日志，调试用 |
| `settings.config-version` | 配置版本号 | `31` | int | — | 是 | 内部使用，**不要手动修改**，Purpur 用它做配置自动升级 |
| `settings.use-alternate-keepalive` | 备用心跳检测 | `false` | bool | true/false | 是 | 启用 Purpur 的备用保活系统：每秒发一次心跳包，连续 30 秒无响应才算超时。网络差的玩家不会经常掉线。⚠️ 已知与 TCPShield 不兼容 |
| `settings.async-chunks` | 异步区块加载 | `true` | bool | true/false | 是 | 是否启用异步区块加载线程，建议开启 |
| `settings.lagging-threshold` | 卡顿 TPS 阈值 | `19` | int | 1-20 | 是 | TPS 低于此值时服务器判定为"卡顿"，触发相关降级逻辑 |
| `settings.seconds-to-stop-server-shutdown-after-cancel` | 取消关服等待时间 | `60` | int | 0+ | 否 | 执行 `/stop` 后玩家取消关服的宽限秒数 |
| `settings.bstats.server-id` | bStats 服务器 ID | `0` | int | — | 是 | bStats 统计平台分配的服务器唯一标识，0=未启用 |
| `settings.velocity.enabled` | 启用 Velocity 支持 | `false` | bool | true/false | 是 | 是否启用 Velocity 代理的现代转发功能 |
| `settings.velocity.secret` | Velocity 密钥 | `（空）` | string | — | 是 | Velocity 代理配置的转发密钥，须与代理端一致 |
| `settings.velocity.debug` | Velocity 调试日志 | `false` | bool | true/false | 是 | 是否打印 Velocity 转发调试信息 |

### settings.command.uptime（运行时长命令）

> `/uptime` 命令显示服务器已运行多长时间。可在格式串中用占位符组合输出。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.command.uptime.format` | 运行时长格式 | `<days><hours><minutes><seconds>` | string | — | 否 | 占位符组合，可用 `<days>` `<hours>` `<minutes>` `<seconds>` 及单个 `<day>` 等 |
| `settings.command.uptime.day` | 1 天文案 | `%02d day, ` | string | — | 否 | `<day>` 占位符的输出格式，`%02d` 为天数补零 |
| `settings.command.uptime.days` | 多天文案 | `%02d days, ` | string | — | 否 | 多天时使用，注意复数 s |
| `settings.command.uptime.hour` | 1 小时文案 | `%02d hour, ` | string | — | 否 | 单小时格式 |
| `settings.command.uptime.hours` | 多小时文案 | `%02d hours, ` | string | — | 否 | 多小时格式 |
| `settings.command.uptime.minute` | 1 分钟文案 | `%02d minute, ` | string | — | 否 | 单分钟格式 |
| `settings.command.uptime.minutes` | 多分钟文案 | `%02d minutes, ` | string | — | 否 | 多分钟格式 |
| `settings.command.uptime.second` | 1 秒文案 | `%02d second` | string | — | 否 | 单秒格式 |
| `settings.command.uptime.seconds` | 多秒文案 | `%02d seconds` | string | — | 否 | 多秒格式 |
| `settings.command.uptime.command-output` | 命令输出文本 | `<white>Server uptime is <uptime>` | string | — | 否 | `/uptime` 实际打印内容，`<uptime>` 会被上面格式替换 |

### settings.command.gamemode（游戏模式命令）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.command.gamemode.requires-specific-permission` | 需要细分权限 | `false` | bool | true/false | 否 | true 时 `/gamemode creative` 需要 `purpur.cmd.gamemode.creative` 等细分权限，否则只需 `purpur.cmd.gamemode` |

### settings.command.tpsbar（TPS 状态条命令）

> `/tpsbar` 在玩家屏幕上方显示一条 Boss 血条形式的 TPS 监控条。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.command.tpsbar.title` | TPS 条标题 | `<rgb(0,255,0)><text>` | string | — | 否 | 标题模板，`<text>` 会被替换为当前 TPS 数值 |
| `settings.command.tpsbar.overlay` | 进度条样式 | `NOTCHED_20` | enum | `PROGRESS`/`NOTCHED_6`/`NOTCHED_10`/`NOTCHED_12`/`NOTCHED_20` | 否 | PROGRESS=平滑条；NOTCHED_20=20 段刻度（对应 20 TPS） |
| `settings.command.tpsbar.fill-mode` | 填充依据 | `TPS` | enum | `TPS`/`MSPT` | 否 | TPS=按每秒刻数填充；MSPT=按每刻毫秒数填充 |
| `settings.command.tpsbar.progress-color.good` | 良好时进度色 | `GREEN` | enum | 颜色枚举（见下） | 否 | TPS 良好（≥18）时的进度条颜色 |
| `settings.command.tpsbar.progress-color.medium` | 中等时进度色 | `YELLOW` | enum | 颜色枚举 | 否 | TPS 中等（15-17）时的颜色 |
| `settings.command.tpsbar.progress-color.low` | 低劣时进度色 | `RED` | enum | 颜色枚举 | 否 | TPS 低下（<15）时的颜色 |
| `settings.command.tpsbar.text-color.good` | 良好时文本色 | `GREEN` | enum | 颜色枚举 | 否 | 同上，作用于文本 |
| `settings.command.tpsbar.text-color.medium` | 中等时文本色 | `YELLOW` | enum | 颜色枚举 | 否 | 同上 |
| `settings.command.tpsbar.text-color.low` | 低劣时文本色 | `RED` | enum | 颜色枚举 | 否 | 同上 |
| `settings.command.tpsbar.tick-interval` | 刷新间隔 | `20` | int | 1+ | 否 | 多少 tick 刷新一次 TPS 条，20=1 秒 |

> **颜色枚举可选值**：`BLACK` `DARK_BLUE` `DARK_GREEN` `DARK_AQUA` `DARK_RED` `DARK_PURPLE` `GOLD` `GRAY` `DARK_GRAY` `BLUE` `GREEN` `AQUA` `RED` `LIGHT_PURPLE` `YELLOW` `WHITE`

### settings.command.rambar（内存状态条命令）

> `/rambar` 在玩家屏幕上方显示内存使用率监控条。结构与 tpsbar 对称。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.command.rambar.title` | 内存条标题 | `<green><text>` | string | — | 否 | 标题模板，`<text>` 会被替换为当前内存使用率 |
| `settings.command.rambar.overlay` | 进度条样式 | `NOTCHED_20` | enum | `PROGRESS`/`NOTCHED_6`/`NOTCHED_10`/`NOTCHED_12`/`NOTCHED_20` | 否 | 同 tpsbar |
| `settings.command.rambar.fill-mode` | 填充依据 | `USED` | enum | `USED`/`FREE` | 否 | USED=按已用内存填充；FREE=按空闲内存填充 |
| `settings.command.rambar.progress-color.good` | 良好时进度色 | `GREEN` | enum | 颜色枚举 | 否 | 内存使用率低时的颜色 |
| `settings.command.rambar.progress-color.medium` | 中等时进度色 | `YELLOW` | enum | 颜色枚举 | 否 | 中等时的颜色 |
| `settings.command.rambar.progress-color.low` | 低劣时进度色 | `RED` | enum | 颜色枚举 | 否 | 内存吃紧时的颜色 |
| `settings.command.rambar.text-color.good` | 良好时文本色 | `GREEN` | enum | 颜色枚举 | 否 | 同上，作用于文本 |
| `settings.command.rambar.text-color.medium` | 中等时文本色 | `YELLOW` | enum | 颜色枚举 | 否 | 同上 |
| `settings.command.rambar.text-color.low` | 低劣时文本色 | `RED` | enum | 颜色枚举 | 否 | 同上 |
| `settings.command.rambar.tick-interval` | 刷新间隔 | `20` | int | 1+ | 否 | 多少 tick 刷新一次内存条 |

### settings.command.credits（成就播报命令）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.command.credits.format` | 成就播报格式 | `<yellow><player> has earned the <achievement> achievement!` | string | — | 否 | 玩家获得成就时的全服播报模板，`<player>` `<achievement>` 自动替换 |

---

## world-settings.default.gameplay-mechanics（游戏机制）

> Purpur 配置项最集中的章节，几乎涵盖所有游戏玩法的微调。位于 `world-settings.default.gameplay-mechanics.*`。

### 通用游戏机制

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `gameplay-mechanics.baby-zombie-movement-speed.modifier` | 小僵尸速度修正 | `-0.5` | double | -1.0~1.0 | 否 | 小僵尸相对成年僵尸的速度修正（-0.5 表示慢一半） |
| `gameplay-mechanics.baby-zombie-movement-speed.max` | 小僵尸速度上限 | `0.35` | double | 0+ | 否 | 小僵尸最大移动速度 |
| `gameplay-mechanics.disable-player-crits` | 禁用玩家暴击 | `false` | bool | true/false | 否 | true 时玩家跳跃攻击不再触发暴击 |
| `gameplay-mechanics.disable-sprint-interruption-on-attack` | 攻击不取消冲刺 | `false` | bool | true/false | 否 | true 时攻击不会让玩家停下冲刺（类似 1.8 战斗手感） |
| `gameplay-mechanics.disable-relative-projectile-velocity` | 禁用相对抛射物速度 | `false` | bool | true/false | 否 | true 时弓箭/雪球等不再继承玩家移动速度，调瞄准更简单 |
| `gameplay-mechanics.disable-piston-rewind` | 禁用活塞回退 | `false` | bool | true/false | 否 | 防止活塞在卡顿时回退到旧状态 |
| `gameplay-mechanics.disable-end-credits` | 禁用末地通关字幕 | `false` | bool | true/false | 否 | true 时玩家杀末影龙后不再显示通关诗 |
| `gameplay-mechanics.entities-can-use-portals` | 实体可用传送门 | `true` | bool | true/false | 否 | 实体（如矿车、怪物）是否能通过地狱传送门 |
| `gameplay-mechanics.milk-cures-bad-omen` | 牛奶清除不祥之兆 | `true` | bool | true/false | 否 | true 时喝牛奶可清除不祥之兆效果 |
| `gameplay-mechanics.boats-drop-on-destroy` | 船破坏时掉落 | `false` | bool | true/false | 否 | true 时船被击碎会掉落物品（原版不掉） |
| `gameplay-mechanics.boats-need-2-blocks-to-push` | 船需两格推动 | `false` | bool | true/false | 否 | 船在狭窄通道的移动规则 |
| `gameplay-mechanics.fire-crossbow-fires-fireworks` | 弩发射烟花 | `false` | bool | true/false | 否 | true 时装填烟花的弩可像发射器一样射出烟花 |
| `gameplay-mechanics.persistent-tile-entity-removal` | 持久方块实体移除 | `false` | bool | true/false | 否 | 是否移除持久化标记的方块实体 |
| `gameplay-mechanics.players-cannot-leave-area` | 禁止玩家离开区域 | `false` | bool | true/false | 否 | true 时玩家不能离开当前区域（需配合插件/边界） |
| `gameplay-mechanics.send-player-pos-when-riding` | 骑乘时发送位置 | `false` | bool | true/false | 否 | 玩家骑乘实体时是否持续向客户端发送位置更新 |
| `gameplay-mechanics.smooth-client-weather-change` | 平滑天气切换 | `false` | bool | true/false | 否 | true 时天气变化不会突兀地切换画面 |
| `gameplay-mechanics.spawner-nerfed-mobs-ai` | 削弱刷怪笼怪 AI | `false` | bool | true/false | 否 | true 时刷怪笼生成的怪不进行 AI 计算（性能优化） |
| `gameplay-mechanics.tnt-fuse-time-ticks` | TNT 引信时间 | `-1` | int | -1 / 0+ | 否 | TNT 引爆倒计时（tick），-1=原版默认（80） |
| `gameplay-mechanics.use-correct-player-count` | 使用正确玩家数 | `true` | bool | true/false | 否 | 影响 mob 上限计算，true 时按真实在线人数算 |
| `gameplay-mechanics.allow-undead-to-burn-in-sunlight` | 亡灵日光燃烧 | `true` | bool | true/false | 否 | 亡灵生物（僵尸/骷髅）是否在阳光下燃烧 |
| `gameplay-mechanics.zombies-target-turtle-eggs` | 僵尸瞄海龟蛋 | `true` | bool | true/false | 否 | 僵尸是否会主动踩碎海龟蛋 |
| `gameplay-mechanics.iron-golem-spawn-attempt-cooldown` | 铁傀儡生成冷却 | `2` | int | 1+ | 否 | 铁傀儡生成尝试之间的冷却 tick 数 |
| `gameplay-mechanics.mobs-can-spawn-from-villagers` | 僵尸感染村民 | `true` | bool | true/false | 否 | 僵尸是否能感染/转化村民 |
| `gameplay-mechanics.mobs-ignore-each-other` | 怪物互相忽略 | `false` | bool | true/false | 否 | true 时怪物不会互相攻击/碰撞 |
| `gameplay-mechanics.spawner-boss` | 刷怪笼生成 Boss | `false` | bool | true/false | 否 | 是否允许刷怪笼生成 Boss 类生物 |
| `gameplay-mechanics.skip-clone-entity-teleport` | 跳过实体克隆传送 | `false` | bool | true/false | 否 | 性能优化：传送实体时不做深拷贝 |
| `gameplay-mechanics.skip-entity-stopping-during-save` | 保存时跳过实体停止 | `false` | bool | true/false | 否 | 性能优化：世界保存时不停止实体 |
| `gameplay-mechanics.skip-tracking-entity-collisions` | 跳过实体碰撞追踪 | `false` | bool | true/false | 否 | 性能优化：不追踪实体间碰撞 |
| `gameplay-mechanics.skip-cloning-entity-in-tracking` | 跳过追踪时克隆 | `false` | bool | true/false | 否 | 性能优化：实体追踪时不克隆 |

### 村民婴儿相关

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `gameplay-mechanics.villager-babies` | 允许村民婴儿 | `true` | bool | true/false | 否 | 是否允许村民繁殖出小村民 |
| `gameplay-mechanics.villager-babies-min-adults` | 婴儿最少成年级 | `0` | int | 0+ | 否 | 至少多少成年村民在场才会繁殖 |
| `gameplay-mechanics.villager-babies-modifier` | 婴儿生成倍率 | `1.0` | double | 0+ | 否 | 繁殖概率倍率，2.0=翻倍 |

### 钓鱼时间

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `gameplay-mechanics.fishing-minimum-time` | 钓鱼最短时间 | `100` | int | 0+ | 否 | 鱼上钩前最少等待 tick，原版 100 |
| `gameplay-mechanics.fishing-maximum-time` | 钓鱼最长时间 | `600` | int | 0+ | 否 | 鱼上钩前最多等待 tick，原版 600 |
| `gameplay-mechanics.fishing-time-steps` | 钓鱼时间步进 | `20` | int | 1+ | 否 | 计算上钩时间的随机步进 tick |

### 鞘翅（Elytra）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `gameplay-mechanics.elytra.disable-boost` | 禁用鞘翅加速 | `false` | bool | true/false | 否 | true 时不能用烟花给鞘翅加速 |
| `gameplay-mechanics.elytra.sprint` | 冲刺起飞 | `false` | bool | true/false | 否 | true 时玩家冲刺跳跃即可起飞 |

### 刷怪笼统一参数

> 这些会作为所有刷怪笼的默认参数，单个刷怪笼可在 NBT 中覆盖。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `gameplay-mechanics.spawner-spawn-count` | 刷怪笼生成数量 | `4` | int | 0+ | 否 | 每次生成多少只怪 |
| `gameplay-mechanics.spawner-max-nearby-entities` | 附近实体上限 | `6` | int | 0+ | 否 | 附近同类实体超过此值则不生成 |
| `gameplay-mechanics.spawner-required-player-range` | 玩家激活距离 | `16` | int | 0+ | 否 | 玩家需在此距离内刷怪笼才工作 |
| `gameplay-mechanics.spawner-max-spawn-delay` | 最大生成延迟 | `800` | int | 0+ | 否 | 两次生成之间最长间隔 tick |
| `gameplay-mechanics.spawner-min-spawn-delay` | 最小生成延迟 | `200` | int | 0+ | 否 | 两次生成之间最短间隔 tick |

### 生物生成 Tick Rate

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `gameplay-mechanics.mob-spawning-modify-tick-rate` | 修改生成 Tick Rate | `false` | bool | true/false | 否 | 是否启用下方 tick rate 调整 |
| `gameplay-mechanics.mob-spawning-tick-rate` | 生成 Tick Rate | `1` | int | 1+ | 否 | 多少 tick 跑一次生成检测，调大可省 CPU |

---

## world-settings.default.blocks（方块配置）

> 每种方块可独立配置。最常用的是木桶/末影箱/潜影盒的行数扩展（Purpur 特色功能）。

### 木桶（Barrel）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `blocks.barrel.six-rows` | 木桶六行 | `false` | bool | true/false | 否 | true 时木桶容量扩展到 6 行（54 格） |
| `blocks.barrel.cache` | 木桶缓存 | `true` | bool | true/false | 否 | 是否缓存木桶打开状态以优化性能 |

### 末影箱（Ender Chest）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `blocks.ender_chest.six-rows` | 末影箱六行 | `false` | bool | true/false | 否 | true 时末影箱扩展到 6 行 |
| `blocks.ender_chest.use-per-player` | 每玩家独立 | `false` | bool | true/false | 否 | true 时每个玩家看到的末影箱内容独立 |

### 潜影盒（Shulker Box）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `blocks.shulker_box.six-rows` | 潜影盒六行 | `false` | bool | true/false | 否 | true 时潜影盒扩展到 6 行 |

### 堆肥桶（Composter）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `blocks.composter.drop-bone-meal` | 掉骨粉 | `true` | bool | true/false | 否 | 空手右键堆肥桶是否掉落骨粉 |

### 投掷器/发射器（Dispenser / Dropper）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `blocks.dispenser.fill-from-bucket` | 发射器倒水 | `true` | bool | true/false | 否 | 发射器能否用桶放置流体 |
| `blocks.dispenser.place-armor` | 发射器穿装备 | `true` | bool | true/false | 否 | 发射器能否给玩家/盔甲架穿装备 |

### 信标（Beacon）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `blocks.beacon.effect-range` | 信标效果范围 | `-1` | int | -1 / 1+ | 否 | 信标效果作用半径（方块），-1=原版按等级计算 |

---

## world-settings.default.entities（实体通用配置）

> 通用于所有实体的基础属性默认值，可被 `mobs.<类型>.attributes.*` 覆盖。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `entities.attributes.max-health` | 默认最大生命值 | `（实体类型相关）` | double | 0+ | 否 | 实体默认最大生命值 |
| `entities.attributes.attack-damage` | 默认攻击伤害 | `（实体类型相关）` | double | 0+ | 否 | 实体默认近战伤害 |
| `entities.attributes.movement-speed` | 默认移动速度 | `（实体类型相关）` | double | 0+ | 否 | 实体默认移动速度 |
| `entities.attributes.follow-range` | 默认追踪范围 | `（实体类型相关）` | double | 0+ | 否 | 实体默认追踪玩家的范围 |
| `entities.attributes.knockback-resistance` | 默认击退抗性 | `0.0` | double | 0.0~1.0 | 否 | 0=完全被击退，1=完全免疫击退 |
| `entities.attributes.flying-speed` | 默认飞行速度 | `0.4` | double | 0+ | 否 | 飞行类实体默认速度 |
| `entities.attributes.attack-knockback` | 默认攻击击退 | `0.0` | double | 0+ | 否 | 实体近战造成的击退力度 |
| `entities.armorstands.do-not-move` | 盔甲架不移动 | `false` | bool | true/false | 否 | true 时盔甲架被推动后不会回弹 |
| `entities.armorstands.use-vehicle` | 盔甲架骑乘 | `true` | bool | true/false | 否 | 盔甲架是否能骑乘其他实体 |
| `entities.armorstands.cant-be-removed` | 盔甲架不可移除 | `false` | bool | true/false | 否 | true 时玩家不能破坏盔甲架 |

---

## world-settings.default.mobs.*（生物配置）

> Purpur 配置的"重头戏"。约 80 种生物各自有独立子节，且共享一组通用选项。
> 本章先列出**通用模板**（每个生物子节都有的选项），再列出**重要生物的特有选项**。

### 通用生物配置模板

> 下列选项对**每一种**生物（`mobs.<生物名>.*`）都生效。生物名采用 snake_case，如 `mobs.creeper.*`、`mobs.ender_dragon.*`。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.<生物>.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家是否能骑乘该生物 |
| `mobs.<生物>.ridable.saddle-required` | 需要鞍 | `false` | bool | true/false | 否 | true 时必须给生物装鞍才能骑 |
| `mobs.<生物>.ridable.steering-enabled` | 启用转向 | `true` | bool | true/false | 否 | 玩家骑乘时能否用 WASD 转向 |
| `mobs.<生物>.ridable.movement` | 骑乘移动速度 | `0.0` | double | 0+ | 否 | 骑乘状态下的基础速度 |
| `mobs.<生物>.ridable.jump` | 骑乘跳跃力 | `0` | int | 0+ | 否 | 骑乘时按空格的跳跃力度 |
| `mobs.<生物>.ridable.max-yaw` | 最大水平转角 | `0` | float | 0+ | 否 | 骑乘时玩家视角水平限位 |
| `mobs.<生物>.ridable.min-yaw` | 最小水平转角 | `0` | float | 0+ | 否 | 同上，反向限位 |
| `mobs.<生物>.ridable.max-pitch` | 最大俯仰角 | `0` | float | 0+ | 否 | 骑乘时玩家视角上下限位 |
| `mobs.<生物>.ridable.min-pitch` | 最小俯仰角 | `0` | float | 0+ | 否 | 同上 |
| `mobs.<生物>.attributes.max-health` | 最大生命值 | `（生物默认）` | double | 1+ | 否 | 该生物的最大生命值 |
| `mobs.<生物>.attributes.attack-damage` | 攻击伤害 | `（生物默认）` | double | 0+ | 否 | 该生物的近战伤害 |
| `mobs.<生物>.attributes.movement-speed` | 移动速度 | `（生物默认）` | double | 0+ | 否 | 该生物的移动速度 |
| `mobs.<生物>.attributes.follow-range` | 追踪范围 | `（生物默认）` | double | 0+ | 否 | 该生物追踪玩家的范围 |
| `mobs.<生物>.attributes.knockback-resistance` | 击退抗性 | `0.0` | double | 0.0~1.0 | 否 | 0=完全被击退 |
| `mobs.<生物>.attributes.attack-knockback` | 攻击击退 | `0.0` | double | 0+ | 否 | 该生物近战造成的击退 |
| `mobs.<生物>.attributes.armor` | 护甲值 | `0.0` | double | 0+ | 否 | 该生物的护甲 |
| `mobs.<生物>.attributes.armor-toughness` | 盔甲韧性 | `0.0` | double | 0+ | 否 | 该生物的盔甲韧性 |
| `mobs.<生物>.takes-damage-from-water` | 水中受伤 | `false` | bool | true/false | 否 | true 时该生物在水中会持续受伤（类似末影人） |
| `mobs.<生物>.always-drop-exp` | 总是掉经验 | `false` | bool | true/false | 否 | true 时该生物死亡必掉经验球 |
| `mobs.<生物>.mob-griefing-override` | 覆盖破坏规则 | `（原版）` | bool | true/false | 否 | 覆盖 gamerule mobGriefing，控制该生物是否破坏方块 |

> ⚠️ 不同生物还可能有**特有**选项（如苦力怕的爆炸半径、末影人的瞬移、幻翼的燃烧等），下面按生物分类列出。

### 重要生物特有配置

#### 苦力怕（Creeper）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.creeper.charged-chance` | 闪电苦力怕概率 | `0.0` | double | 0.0~1.0 | 否 | 自然生成时即为闪电苦力怕的概率 |
| `mobs.creeper.max-fuse-ticks` | 最大引信 tick | `30` | int | 0+ | 否 | 苦力怕爆炸前的最大引信时间 |
| `mobs.creeper.explosion-radius` | 爆炸半径 | `3` | int | 0+ | 否 | 爆炸破坏半径（方块） |

#### 末影人（Enderman）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.enderman.teleport-on-rain` | 下雨瞬移 | `true` | bool | true/false | 否 | 末影人在雨天是否会瞬移躲避 |
| `mobs.enderman.ignore-players-with-pumpkin-head` | 忽略南瓜头玩家 | `false` | bool | true/false | 否 | true 时戴南瓜头的玩家不会被末影人攻击 |
| `mobs.enderman.drop-total-pearls` | 必掉末影珍珠 | `false` | bool | true/false | 否 | true 时死亡必掉 1 个末影珍珠 |

#### 幻翼（Phantom）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.phantom.spawn-attempts-per-minute` | 每分钟生成尝试 | `4` | int | 0+ | 否 | 幻翼生成检测频率 |
| `mobs.phantom.burn-in-daylight` | 日光燃烧 | `true` | bool | true/false | 否 | true 时幻翼在白天燃烧消失 |
| `mobs.phantom.burn-in-light` | 光照燃烧 | `0` | int | 0~15 | 否 | 光照等级 ≥ 此值时幻翼燃烧，0=禁用 |
| `mobs.phantom.ignore-players-with-torch` | 忽略火把玩家 | `false` | bool | true/false | 否 | true 时手持火把/灯笼的玩家不会被幻翼锁定 |

#### 村民（Villager）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.villager.follow-emerald-blocks` | 跟随绿宝石块 | `false` | bool | true/false | 否 | true 时村民会被手持绿宝石块的玩家吸引（⚠️ 1.21.3 已知 bug，建议关闭） |
| `mobs.villager.clerics-farm-wart` | 牧师种下界疣 | `true` | bool | true/false | 否 | true 时牧师村民会种植下界疣 |
| `mobs.villager.breeding-delay-ticks` | 繁殖冷却 | `6000` | int | 0+ | 否 | 两次繁殖之间的冷却 tick |
| `mobs.villager.can-breed` | 可繁殖 | `true` | bool | true/false | 否 | 村民是否可繁殖 |
| `mobs.villager.tobias-zombies` | 僵尸转化 | `true` | bool | true/false | 否 | 僵尸是否会转化村民 |

#### 僵尸（Zombie）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.zombie.jockey.chance` | 骑鸡概率 | `0.05` | double | 0.0~1.0 | 否 | 小僵尸骑鸡生成的概率 |
| `mobs.zombie.jockey.only-babies` | 仅小僵尸骑乘 | `false` | bool | true/false | 否 | true 时只有小僵尸会作骑乘者 |
| `mobs.zombie.jockey.try-existing-chicken` | 尝试现有鸡 | `true` | bool | true/false | 否 | true 时小僵尸会优先骑附近已有的鸡 |
| `mobs.zombie.aggressive-towards-villager` | 主动攻击村民 | `true` | bool | true/false | 否 | 僵尸是否会主动追村民 |

#### 末影龙（Ender Dragon）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.ender_dragon.ridable.flight` | 飞行可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑乘飞行中的末影龙 |
| `mobs.ender_dragon.no-wall-check` | 关闭撞墙检测 | `false` | bool | true/false | 否 | true 时末影龙不会被墙阻挡 |
| `mobs.ender_dragon.always-drops-elytra` | 总掉鞘翅 | `false` | bool | true/false | 否 | true 时击杀末影龙必掉鞘翅 |

#### 凋灵（Wither）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.wither.can-spawn-in-anywhere` | 任意位置生成 | `false` | bool | true/false | 否 | true 时凋灵生成不受方块限制 |
| `mobs.wither.bypass-armor` | 穿透护甲 | `false` | bool | true/false | 否 | true 时凋灵攻击无视护甲 |
| `mobs.wither.explosion-radius` | 爆炸半径 | `1` | int | 0+ | 否 | 凋灵初始生成的爆炸半径 |

#### 守卫者/远古守卫者（Guardian / Elder Guardian）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.guardian.spawn-rate` | 生成频率 | `（原版）` | int | 0+ | 否 | 守卫者生成速率 |
| `mobs.elder_guardian.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑远古守卫者 |

#### 雪傀儡（Snow Golem）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.snow_golem.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑雪傀儡 |
| `mobs.snow_golem.leave-snow-trail` | 留雪迹 | `true` | bool | true/false | 否 | 是否在脚下留雪层 |

#### 史莱姆/岩浆怪（Slime / Magma Cube）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.slime.max-size` | 史莱姆最大尺寸 | `4` | int | 1+ | 否 | 史莱姆可生成的最大尺寸（1=最小，4=最大） |
| `mobs.slime.ridable.slime` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑史莱姆 |
| `mobs.magma_cube.max-size` | 岩浆怪最大尺寸 | `4` | int | 1+ | 否 | 同上 |

#### 蜜蜂（Bee）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.bee.can-work-in-rain` | 雨天工作 | `false` | bool | true/false | 否 | true 时蜜蜂在雨天也会采蜜 |
| `mobs.bee.can-work-at-night` | 夜间工作 | `false` | bool | true/false | 否 | true 时蜜蜂夜间也会工作 |

#### 哞菇（Mooshroom）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.mooshroom.shear-turns-mushroom` | 剪蘑菇变异 | `true` | bool | true/false | 否 | 红色哞菇剪蘑菇后是否会变成棕色 |

#### 羊驼（Llama）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.llama.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑羊驼 |
| `mobs.llama.strength-modifier` | 力量修正 | `0` | int | 0+ | 否 | 羊驼驮箱容量修正 |

#### 烈焰人（Blaze）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.blaze.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑烈焰人飞行 |
| `mobs.blaze.takes-damage-from-water` | 水中受伤 | `true` | bool | true/false | 否 | 烈焰人接触水是否受伤（默认受伤） |

#### 巨人（Giant）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.giant.has-ai` | 启用 AI | `false` | bool | true/false | 否 | true 时巨人有完整 AI（原版巨人无 AI） |
| `mobs.giant.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑巨人 |

#### 唤魔者（Evoker）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.evoker.summon-vex` | 召唤恼鬼 | `true` | bool | true/false | 否 | 唤魔者能否召唤恼鬼 |

#### 猫/豹猫（Cat / Ocelot）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.cat.spawn-villager` | 村庄生成 | `true` | bool | true/false | 否 | 猫是否会在村庄自然生成 |
| `mobs.ocelot.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑豹猫 |

#### 羊（Sheep）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.sheep.eat-grass` | 吃草 | `true` | bool | true/false | 否 | 羊是否会吃草变羊毛 |
| `mobs.sheep.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑羊 |

#### 马/驴/骡（Horse / Donkey / Mule）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.horse.ridable` | 可骑乘 | `true` | bool | true/false | 否 | 玩家能否骑马（默认已可骑） |
| `mobs.horse.breed-with-villagers` | 村民繁殖 | `false` | bool | true/false | 否 | 实验性：马能否通过村民繁殖 |

#### 溺尸（Drowned）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.drowned.can-break-doors` | 破门 | `true` | bool | true/false | 否 | 溺尸是否会破门 |
| `mobs.drowned.jockey.chance` | 骑乘概率 | `0.0` | double | 0.0~1.0 | 否 | 小溺尸骑鸡/溺尸的概率 |

#### 猪灵/猪灵蛮兵（Piglin / Piglin Brute）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.piglin.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑猪灵 |
| `mobs.piglin_brute.always-aggressive` | 总是敌对 | `true` | bool | true/false | 否 | 猪灵蛮兵是否始终敌对 |

#### 疣猪兽（Hoglin）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.hoglin.ridable` | 可骑乘 | `false` | bool | true/false | 否 | 玩家能否骑疣猪兽 |

#### 羊驼兽（Trader Llama）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.trader_llama.despawn-delay` | 消失延迟 | `48000` | int | 0+ | 否 | 流浪商人的羊驼兽消失延迟 tick |

#### 流浪商人（Wandering Trader）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `mobs.wandering_trader.despawn-delay` | 消失延迟 | `24000` | int | 0+ | 否 | 流浪商人消失延迟 tick |
| `mobs.wandering_trader.trades` | 交易列表 | `（原版）` | list | — | 否 | 自定义流浪商人的交易内容 |

#### 行商僵尸/尸壳/卫道士/掠夺者等

> 这些生物主要继承通用模板，特有选项较少，此处不一一展开。完整列表见 Purpur 源码 `PurpurWorldConfig.java`。

### 全部支持 mobs.<生物>.* 的生物清单

`allay` `axolotl` `bat` `bee` `blaze` `camel` `cat` `cave_spider` `chicken` `cod` `cow` `creeper` `dolphin` `donkey` `dragon` `drowned` `elder_guardian` `ender_dragon` `enderman` `endermite` `evoker` `fox` `frog` `ghast` `giant` `glow_squid` `goat` `guardian` `hoglin` `horse` `husk` `illusioner` `iron_golem` `llama` `magma_cube` `mooshroom` `mule` `ocelot` `panda` `parrot` `phantom` `pig` `piglin` `piglin_brute` `pillager` `polar_bear` `pufferfish` `rabbit` `ravager` `salmon` `sheep` `shulker` `silverfish` `skeleton` `skeleton_horse` `slime` `sniffer` `snow_golem` `spider` `squid` `stray` `strider` `tadpole` `trader_llama` `tropical_fish` `turtle` `vex` `villager` `vindicator` `wandering_trader` `warden` `witch` `wither` `wither_skeleton` `wolf` `zoglin` `zombie` `zombie_horse` `zombie_villager` `zombified_piglin`

---

## world-settings.default.ridables（全局可骑乘配置）

> 控制骑乘系统的全局行为。单种生物是否可骑乘仍由 `mobs.<生物>.ridable` 决定。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `ridables.enabled` | 启用骑乘系统 | `false` | bool | true/false | 否 | 总开关，关闭后所有 `mobs.<生物>.ridable` 失效 |
| `ridables.wasd-controls` | WASD 控制 | `true` | bool | true/false | 否 | 玩家骑乘时是否可用 WASD 控制方向 |
| `ridables.spacebar-event` | 空格事件 | `true` | bool | true/false | 否 | 空格键是否触发跳跃/特殊动作 |
| `ridables.jump-event` | 跳跃事件 | `true` | bool | true/false | 否 | 是否允许骑乘跳跃 |
| `ridables.saddle-required` | 默认需鞍 | `false` | bool | true/false | 否 | 全局默认是否需要鞍才能骑 |
| `ridables.steering-enabled` | 默认启用转向 | `true` | bool | true/false | 否 | 全局默认是否启用转向 |

---

## world-settings.default.player（玩家配置）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `player.save-player-data` | 保存玩家数据 | `true` | bool | true/false | 否 | 是否将玩家数据写入磁盘 |
| `player.always-take-fall-damage` | 总是受跌落伤害 | `false` | bool | true/false | 否 | true 时无视任何抗性总是受跌落伤害 |
| `player.kick-on-dup-velocity-packet` | 重复速度包踢出 | `true` | bool | true/false | 否 | 收到重复速度包是否踢出玩家 |
| `player.fall-damage-modifier` | 跌落伤害修正 | `1.0` | double | 0+ | 否 | 跌落伤害倍率，2.0=双倍 |
| `player.creative-no-clip` | 创造穿墙 | `false` | bool | true/false | 否 | true 时创造模式玩家可穿墙 |
| `player.disable-death-message` | 禁用死亡消息 | `false` | bool | true/false | 否 | true 时玩家死亡不广播消息 |
| `player.disable-combat-cooldown` | 禁用战斗冷却 | `false` | bool | true/false | 否 | true 时取消攻击冷却（恢复 1.8 战斗节奏） |
| `player.too-many-packets-threshold` | 过多包阈值 | `（原版）` | int | 0+ | 否 | 单位时间内超过此包数判定异常 |

---

## world-settings.default.misc（杂项配置）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `misc.disable-player-crits` | 禁用暴击 | `false` | bool | true/false | 否 | 同 gameplay-mechanics.disable-player-crits 的别名 |
| `misc.farmland-trample-modifier` | 农田踩踏修正 | `（原版）` | double | 0+ | 否 | 跳跃踩农田的概率修正 |
| `misc.lightning-strike-fire-tick` | 闪电引燃 tick | `（原版）` | int | 0+ | 否 | 闪电击中地面后的引燃时间 |
| `misc.fireball-fire-tick` | 火球引燃 tick | `（原版）` | int | 0+ | 否 | 烈焰人火球的引燃时间 |

---

## 实用建议（小白快速上手）

### 🌐 网络优化（推荐立即开启）

```yaml
settings:
  use-alternate-keepalive: true   # 网络差的玩家不会老掉线
  async-chunks: true              # 异步加载区块
```

⚠️ 若使用了 TCPShield 代理，**不要**开启 `use-alternate-keepalive`，会冲突。

### ⚡ 性能优化（适合大型 SMP）

```yaml
settings:
  lagging-threshold: 18           # 把卡顿阈值调高一点更早触发降级
world-settings:
  default:
    gameplay-mechanics:
      spawner-nerfed-mobs-ai: false  # 刷怪笼怪不削弱 AI（如需生电）
      mob-spawning-modify-tick-rate: true
      mob-spawning-tick-rate: 2       # 每 2 tick 跑一次生成检测
```

### 🎮 玩法增强（特色功能）

```yaml
world-settings:
  default:
    blocks:
      barrel:
        six-rows: true           # 木桶 6 行（54 格）
      ender_chest:
        six-rows: true           # 末影箱 6 行
    mobs:
      ender_dragon:
        ridable:
          enabled: true          # 骑末影龙
      phantom:
        burn-in-daylight: true   # 幻翼白天燃烧
```

### 🔓 生电服务器必看

Purpur 默认关闭了一些"生电技巧"，相关开关其实在 **paper-global.yml** 的 `unsupported-settings`：

```yaml
# config/paper-global.yml
unsupported-settings:
  allow-headless-pistons: true                # 无头活塞
  allow-permanent-block-break-exploits: true  # 永久方块破坏
  allow-piston-duplication: true              # 活塞复制（TNT 复制等）
  allow-tripwire-disarming-exploits: true     # 绊线除警
  allow-unsafe-end-portal-teleportation: true # 不安全末地传送
  compression-format: ZLIB
  perform-username-validation: true
  skip-vanilla-damage-tick-when-shield-blocked: true
```

---

## 参考链接

- 官方配置文档：https://purpurmc.org/docs/purpur/configuration/
- 官方下载页：https://purpurmc.org/downloads
- GitHub 源码：https://github.com/PurpurMC/Purpur
- 配置源码（全局）：`purpur-server/src/main/java/org/purpurmc/purpur/PurpurConfig.java`
- 配置源码（世界）：`purpur-server/src/main/java/org/purpurmc/purpur/PurpurWorldConfig.java`
- DeepWiki 配置参考：https://deepwiki.com/PurpurMC/Purpur/3.3-configuration-options-reference
- 社区优化指南（中文）：https://mhy278.github.io/MinecraftServerHostGuideHtml/Optimization.html
- minebbs 优化指南：https://www.minebbs.com/threads/minecraft-server-optimization-guide.27098/

---

> ⚠️ **免责声明**：Purpur 配置项变更频繁，本文档基于 2025-2026 稳定版本整理。如遇本文未列出的配置项或行为差异，请以官方文档与服务器实际生成的 `purpur.yml` 注释为准。
