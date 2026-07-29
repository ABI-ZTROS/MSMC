# Pufferfish 服务器配置文件中文手册

> Pufferfish 是基于 Paper 的高性能优化分支，主打异步实体追踪和区块优化。
> 继承关系：Vanilla → Spigot → Paper → Pufferfish
> 官方 GitHub：https://github.com/pufferfish-gg/Pufferfish

Pufferfish 由 Pufferfish Host 团队开发，定位为「面向大型服务器的企业级 Paper 分支」。它在 Paper 基础上叠加了实体性能优化、部分异步处理、8 倍速地图渲染、漏斗提速（较 Paper 快约 30%）、更快的射线检测、内置性能分析器（Flare）、Sentry 错误集成等特性，并完整兼容所有 Paper 插件。其弟 Purpur 又在 Pufferfish 之上进一步扩展自定义选项。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| paper-global.yml | YAML | Paper 继承 | Paper 全局配置 |
| paper-world-defaults.yml | YAML | Paper 继承 | Paper 世界默认配置 |
| pufferfish.yml | YAML | Pufferfish 专属 | Pufferfish 独有配置（本文档重点） |

> 说明：Pufferfish 完整继承 Paper 的全部配置体系，本文档仅聚焦 Pufferfish 独有的 `pufferfish.yml`。其余配置文件请参阅对应的 Paper / Spigot / Bukkit 手册。

## pufferfish.yml（Pufferfish 专属配置）

`pufferfish.yml` 位于服务器根目录，由 `gg.pufferfish.pufferfish.PufferfishConfig` 加载。所有配置项在服务器启动时读取，并可通过 `/pufferfish reload` 命令热重载（少数标注「需重启」的项除外）。文件顶部为 `info` 元信息块（版本号自动维护，请勿手动修改）。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `dab.enabled`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `string[]` 字符串列表。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持 `/pufferfish reload` 热重载。

---

### 1. 信息块（info）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `info.version` | 配置版本号 | string | `1.0` | 🔄 | Pufferfish 配置文件的内部版本号，由程序自动维护，请勿手动修改。 |

---

### 2. 书籍设置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `enable-books` | 允许写入书本 | bool | `true`（`true`/`false`） | 🔄 | 是否允许玩家在成书上继续写入内容。容易成为复制漏洞（duping）目标的服务器可考虑关闭。可对单个玩家用权限节点 `pufferfish.usebooks` 覆盖此设置。 |

---

### 3. 性能优化

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `tps-catchup` | 卡顿后补帧追赶 | bool | `true`（`true`/`false`） | 🔄 | 开启后，服务器在经历一次卡顿后会加速运行以维持 20 TPS（Spigot/Paper 的默认行为）。副作用：卡顿后生物可能短暂「瞬移」或快速移动。若你不希望出现这种追赶行为可关闭。 |
| `enable-suffocation-optimization` | 窒息检测优化 | bool | `true`（`true`/`false`） | 🔄 | 通过有选择地跳过窒息检测来优化性能，跳过方式在玩家视角下几乎察觉不到与原版的差异。绝大多数服务器建议保持开启；若追求 100% 原版行为可关闭。 |
| `enable-async-mob-spawning` | 异步生物生成 | bool | `true`（`true`/`false`） | ✅ | 将生物生成所需的昂贵计算（并非真正生成生物，那样不安全）转移到异步线程。实体较多的服务器可提升约 15% 性能。**前置条件**：必须同时在 Paper 中开启 `per-player-mob-spawns`。⚠️ 此项仅在服务器启动时读取，热重载不会改变其值，修改后必须重启。 |
| `inactive-goal-selector-throttle` | 节流非激活实体 AI | bool | `true`（`true`/`false`） | 🔄 | 在实体处于「非激活（inactive）tick」时节流其 AI 目标选择器（goal selector）。可带来百分之几的性能提升，但对游戏玩法有轻微影响。 |

---

### 4. 弹射物优化（projectile）

> 当弹射物（如末影珍珠、箭矢、恶魂火球等）飞行穿过未加载区块时，会触发同步区块加载，容易造成卡顿。本节用于限制弹射物引发的同步加载量。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `projectile.max-loads-per-tick` | 每 tick 弹射物加载区块上限 | int | `10`（≥ 0） | 🔄 | 控制每个游戏 tick 内，所有弹射物合计允许同步加载多少个区块。降低此值可缓解弹射物密集时（如投射物农场、TNT 大炮）的卡顿，但可能导致弹射物飞行途中「穿模」或行为异常。 |
| `projectile.max-loads-per-projectile` | 单个弹射物加载区块上限 | int | `10`（≥ 0） | 🔄 | 控制单个弹射物在其整个生命周期内最多能加载多少个区块，超过即被自动移除。推荐值 8。可有效防止恶意玩家用大量投射物拖垮服务器。 |

---

### 5. 实体 AI 优化（dab —— 动态大脑激活 / Dynamic Activation of Brains）

> DEAR 是 Airplane/Pufferfish 引入的实体大脑优化机制：远离玩家的实体其 AI（寻路、行为）按距离衰减 tick 频率，越远 tick 得越慢，从而大幅降低 CPU 占用，同时近处玩家几乎察觉不到差异。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `dab.enabled` | 启用 DEAR 实体大脑优化 | bool | `true`（`true`/`false`） | 🔄 | 是否启用动态大脑激活。开启后远离玩家的实体会降低 AI tick 频率。（旧键名 `activation-range.enabled`） |
| `dab.start-distance` | DEAR 生效起始距离 | int | `12`（≥ 0，单位：方块） | 🔄 | 实体距玩家多远时开始受 DEAR 影响。距离小于此值的实体保持原版全速 tick。（旧键名 `activation-range.start-distance`） |
| `dab.max-tick-freq` | 最远实体最大 tick 间隔 | int | `20`（≥ 1，单位：tick；20 = 1 秒） | 🔄 | 距离最远的实体多久 tick 一次 AI（寻路器与行为）。值越大，远处实体 AI 越省 CPU，但行为越迟钝。20 表示最远实体每秒 tick 一次。（旧键名 `activation-range.max-tick-freq`） |
| `dab.activation-dist-mod` | 距离对频率的影响系数 | int | `8`（建议 7–9） | 🔄 | 距离对 tick 频率的影响强度。计算公式：`频率 = (到玩家距离^2) / (2^本值)`。想让远处实体 tick 更少（更省 CPU）用 `7`；想让远处实体 tick 更多（更接近原版）用 `9`。（旧键名 `activation-range.activation-dist-mod`） |
| `dab.blacklisted-entities` | DEAR 忽略的实体列表 | string[] | `[]`（实体命名空间 ID 列表） | 🔄 | 不受 DEAR 影响、始终保持全速 AI 的实体列表（填实体类型 ID，如 `minecraft:villager`、`minecraft:iron_golem`）。适合需要保持敏锐 AI 的关键实体。（旧键名 `activation-range.blacklisted-entities`） |

---

### 6. 末地设置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `allow-end-crystal-respawn` | 允许末影水晶复活末影龙 | bool | `true`（`true`/`false`） | 🔄 | 是否允许末影水晶复活末影龙。在预期会发生末影水晶战斗（PvP）的末地服务器上，关闭此项可避免玩家每次放置末影水晶时服务器执行昂贵的复活搜索，从而减少卡顿。 |

---

### 7. 性能分析器（flare）

> Flare 是 Pufferfish 内置的零开销性能分析器，配合在线服务生成可视化的火焰图。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `flare.url` | Flare 分析服务地址 | string | `https://flare.airplane.gg`（URL） | 🔄 | 生成性能分析报告时所使用的在线服务地址。一般无需修改，除非你部署了自托管的 Flare 服务。 |

---

### 8. 在线服务（web-services）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `web-services.token` | Pufferfish/Airplane 在线服务令牌 | string | ` `（空字符串 = 禁用） | ✅ | 连接 Pufferfish/Airplane 在线工具（如 Flare 远程分析）所需的访问令牌。留空则禁用。填写有效令牌后，服务器启动时会初始化 Flare 并注册 `/flare` 命令。修改后需重启以重新初始化。 |

---

### 9. 错误监控（sentry）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `sentry-dsn` | Sentry 错误上报 DSN | string | ` `（空字符串 = 禁用） | ✅ | Sentry 数据源名称（DSN），用于将服务器错误以详尽堆栈形式上报到 Sentry。留空则禁用。可从 https://sentry.io/ 获取。也可通过环境变量 `SENTRY_DSN` 设置（环境变量优先级高于本配置）。填写后服务器启动时初始化 Sentry，修改需重启。 |

---

### 10. 杂项（misc）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.disable-method-profiler` | 禁用方法性能分析器 | bool | `true`（`true`/`false`） | 🔄 | 是否禁用 Minecraft 原生的方法级性能分析器（method profiler）。该分析器在生产环境几乎无用且会带来额外开销，默认关闭以节省性能。仅在需要排查原版内部性能问题时才开启。 |

---

## SIMD 加速（启动参数，非配置项）

Pufferfish 还支持基于 Java 向量 API（JDK 孵化模块 `jdk.incubator.vector`）的 SIMD 加速，可替换部分运算为更快的向量化版本。该功能不通过 `pufferfish.yml` 控制，而是由启动参数决定：

- **启用方式**：在 `-jar` 之前添加启动参数 `--add-modules=jdk.incubator.vector`。
- **环境要求**：仅在 Java 17–25 上安全支持。
- 启动时控制台会输出 SIMD 是否生效的诊断信息；若提示「未配置」，请按上述方式添加启动参数。

## 配置示例（pufferfish.yml 完整默认值）

```yaml
# Pufferfish Configuration
# Check out Pufferfish Host for maximum performance server hosting: https://pufferfish.host
# Join our Discord for support: https://discord.gg/reZw4vQV9H
# Download new builds at https://ci.pufferfish.host/job/Pufferfish
info:
  version: '1.0'
enable-books: true
tps-catchup: true
enable-suffocation-optimization: true
enable-async-mob-spawning: true
projectile:
  # Optimizes projectile settings
  max-loads-per-tick: 10
  max-loads-per-projectile: 10
dab:
  # Optimizes entity brains when
  # they're far away from the player
  enabled: true
  start-distance: 12
  max-tick-freq: 20
  activation-dist-mod: 8
  blacklisted-entities: []
inactive-goal-selector-throttle: true
allow-end-crystal-respawn: true
flare:
  # Configures Flare, the built-in profiler
  url: https://flare.airplane.gg
web-services:
  # Options for connecting to Pufferfish/Airplane's online utilities
  token: ''
sentry-dsn: ''
misc:
  disable-method-profiler: true
```

## 优化建议（针对大型服务器）

1. **异步生物生成**：保持 `enable-async-mob-spawning: true`，并确保 Paper 的 `per-player-mob-spawns` 已开启，可获约 15% 实体性能提升。
2. **DEAR 实体大脑优化**：保持 `dab.enabled: true`。若服务器怪物/村民密集且 CPU 紧张，可把 `dab.activation-dist-mod` 降到 `7` 让远处实体更省 CPU；对村民交易所等关键 AI 实体加入 `dab.blacklisted-entities` 白名单。
3. **弹射物限流**：玩家用末影珍珠/箭矢较多或存在投射物农场时，将 `projectile.max-loads-per-projectile` 设为 `8`，`projectile.max-loads-per-tick` 设为 `8`–`10`。
4. **末地 PvP**：若有末影水晶 PvP 场景，关闭 `allow-end-crystal-respawn` 避免放置水晶时的昂贵搜索。
5. **错误监控**：生产环境填写 `sentry-dsn` 以便及时发现并定位服务端异常。
6. **SIMD 加速**：在启动脚本中加入 `--add-modules=jdk.incubator.vector`（需 Java 17–25）以启用额外向量化优化。

> 参考来源：Pufferfish 官方源码 [`PufferfishConfig.java`](https://github.com/pufferfish-gg/Pufferfish/blob/ver/1.21/pufferfish-server/src/main/java/gg/pufferfish/pufferfish/PufferfishConfig.java)（ver/1.21 分支）、[官方优化指南](https://docs.pufferfish.host/optimization/pufferfish-server-optimization-guide/)。
