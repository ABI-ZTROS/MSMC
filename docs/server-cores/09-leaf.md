# Leaf 服务器配置文件中文手册

> Leaf 是基于 Paper（更准确说是基于 Gale）的高性能优化分支，由 Winds Studio 维护。
> 继承关系：Vanilla → Spigot → Paper → Gale → Leaf
> 官方 GitHub：https://github.com/Winds-Studio/Leaf
> 官方文档站：https://www.leafmc.one/zh/

Leaf 集成了来自 Pufferfish、Purpur、Leaves、Mirai、Petal、Luminol、Kaiiju 等多个优秀分支的优化补丁，主打「异步路径查找」「多线程实体追踪」「线性区域文件」「协议支持」「安全种子」等特性。它完整兼容 Bukkit / Spigot / Paper 插件，并要求 Java 21+。配置上同时拥有 `leaf.yml`（根目录，世界级 / 玩法级配置）与 `config/leaf-global.yml`（全局级配置）。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| paper-global.yml | YAML | Paper 继承 | Paper 全局配置 |
| paper-world-defaults.yml | YAML | Paper 继承 | Paper 世界默认配置 |
| purpur.yml | YAML | Purpur 继承（Leaf 基于 Gale，间接含 Purpur 选项） | Purpur 独有配置 |
| gale-global.yml | YAML | Gale 继承 | Gale 全局配置 |
| gale-world-defaults.yml | YAML | Gale 继承 | Gale 世界默认配置 |
| **leaf.yml** | YAML | **Leaf 专属** | Leaf 世界级 / 玩法级独有配置（本文档重点） |
| **config/leaf-global.yml** | YAML | **Leaf 专属** | Leaf 全局级独有配置（本文档重点） |

> 说明：Leaf 完整继承 Paper + Gale + Purpur 的全部配置体系，本文档仅聚焦 Leaf 独有的 `leaf.yml` 与 `leaf-global.yml`。其余配置请参阅对应手册。

## leaf.yml（Leaf 世界级 / 玩法级专属配置）

`leaf.yml` 位于服务器根目录（首次启动自动生成）。世界级配置可为每个世界单独覆盖（在世界文件夹下的 `leaf.yml` 中重写同名键即可）。所有配置项在服务器启动时读取，标注「需重启」的项无法通过 `/leaf reload` 热重载。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `async.pathfinding.enabled`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `double` 浮点 / `string` 字符串 / `string[]` 字符串列表 / `enum` 枚举。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器；🔄 表示支持 `/leaf reload` 热重载。

---

### 1. 异步处理（async）

> Leaf 的核心卖点。将路径查找、实体追踪、玩家数据保存等 CPU 密集型任务从主线程剥离到独立线程池，大幅降低主线程阻塞，提升 TPS。

#### 1.1 异步路径查找（async-pathfinding）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `async.async-pathfinding.enabled` | 启用异步路径查找 | bool | `false`（`true`/`false`） | ✅ | 是否将实体寻路计算转移到异步线程池。开启后实体寻路不再阻塞主线程。⚠️ 仅在启动时读取，热重载不生效。 |
| `async.async-pathfinding.max-threads` | 路径查找最大线程数 | int | `0`（`0` = 自动 = CPU 核心数/4；`<0` = CPU 核心数 + 此值；`>0` = 指定线程数） | ✅ | 异步路径查找线程池的最大线程数。`0` 表示自动按 CPU 核心数 / 4 计算。8 核 CPU 推荐设 `4`。 |
| `async.async-pathfinding.keepalive` | 线程空闲保活时间 | int | `60`（≥ 0，单位：秒） | ✅ | 线程池中空闲线程的存活时间。超过此时间无任务的线程将被回收。 |
| `async.async-pathfinding.queue-size` | 任务队列大小 | int | `0`（`0` = 自动 = 线程数 × 256；`>0` = 指定大小） | ✅ | 等待执行的任务队列容量。队列满后将触发拒绝策略。`0` 自动按线程数 × 256 计算。 |
| `async.async-pathfinding.reject-policy` | 队列满拒绝策略 | enum | `FLUSH_ALL`（`FLUSH_ALL` / `CALLER_RUNS`） | ✅ | 队列满时的处理策略。`FLUSH_ALL`：清空队列并在主线程执行所有任务（适合 CPU ≥ 12 核的高配服务器）；`CALLER_RUNS`：仅在新任务提交时在主线程执行（适合低配或队列较小的服务器）。 |

#### 1.2 多线程实体追踪（async-entity-tracker）

> 实体追踪（Entity Tracking）是 Minecraft 服务器将实体位置 / 状态同步给附近玩家的过程。Leaf 将其拆分到多线程，可提升 40-60% 实体处理性能。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `async.async-entity-tracker.enabled` | 启用多线程实体追踪 | bool | `false`（`true`/`false`） | ✅ | 是否将实体追踪转移到异步线程池。⚠️ 仅在启动时读取。 |
| `async.async-entity-tracker.max-threads` | 实体追踪最大线程数 | int | `0`（`0` = 自动 = CPU 核心数 / 6；`>0` = 指定线程数） | ✅ | 实体追踪线程池的最大线程数。8 核 CPU 推荐设 `3`。 |
| `async.async-entity-tracker.compat-mode` | 兼容模式 | bool | `false`（`true`/`false`） | ✅ | 是否启用 NPC 插件兼容模式。若使用 Citizens 等基于实体的 NPC 插件建议开启（性能略降）；若使用基于数据包的 NPC 插件可关闭以获得更好性能。 |
| `async.async-entity-tracker.queue-size` | 任务队列大小 | int | `0`（`0` = 自动 = 线程数 × 384；`>0` = 指定大小） | ✅ | 实体追踪任务队列容量。 |

#### 1.3 异步生物生成（async-mob-spawning）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `async.async-mob-spawning.enabled` | 启用异步生物生成 | bool | `false`（`true`/`false`） | ✅ | 是否将生物生成所需计算转移到异步线程（仅计算，不真正生成实体）。前置条件：必须在 Paper 中开启 `per-player-mob-spawns`。⚠️ 仅在启动时读取。 |

#### 1.4 异步玩家数据保存（async-player-data-save）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `async.async-player-data-save.enabled` | 启用异步玩家数据保存 | bool | `true`（`true`/`false`） | ✅ | 是否将玩家数据（.dat 文件）的保存操作转移到异步线程，避免主线程 I/O 阻塞。 |

#### 1.5 快速随机数生成器（faster-random-generator）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `async.faster-random-generator.enabled` | 启用快速随机数生成器 | bool | `true`（`true`/`false`） | ✅ | 是否用更快的随机数生成算法替代原版 `java.util.Random`。可提升 15-25% 涉及随机的性能。⚠️ 修改后世界生成可能略有差异。 |
| `async.faster-random-generator.random-generator` | 随机数算法 | enum | `XOROSHIRO128_PLUS_PLUS`（`XOROSHIRO128_PLUS_PLUS` / `XOSHIRO256_PLUS_PLUS` / `JAVA_UTIL_RANDOM` / `SPLITABLE_RANDOM`） | ✅ | 使用的随机数算法。推荐 `XOROSHIRO128_PLUS_PLUS`（速度与质量平衡佳）。 |
| `async.faster-random-generator.enable-for-worldgen` | 用于世界生成 | bool | `false`（`true`/`false`） | ✅ | 是否将快速随机数生成器用于世界生成。**强烈建议保持 `false`**，否则会影响地形生成一致性，导致已有世界出现接缝。 |
| `async.faster-random-generator.warn-for-slime-chunk` | 史莱姆区块警告 | bool | `true`（`true`/`false`） | ✅ | 启用快速随机数生成器后，是否在控制台警告史莱姆区块判定可能变化。 |
| `async.faster-random-generator.use-legacy-for-slime-chunk` | 史莱姆区块使用旧算法 | bool | `true`（`true`/`false`） | ✅ | 是否对史莱姆区块判定继续使用原版随机算法，保证区块分布与原版一致。建议开启。 |

---

### 2. 性能优化（performance）

#### 2.1 动态 AI 激活（performance.dab —— Dynamic Activation of Brains）

> DEAR 优化：远离玩家的实体其 AI（寻路、行为）按距离衰减 tick 频率，越远 tick 越慢，大幅降低 CPU 占用。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `performance.dab.enabled` | 启用 DEAR 实体大脑优化 | bool | `true`（`true`/`false`） | 🔄 | 是否启用动态大脑激活。开启后远离玩家的实体会降低 AI tick 频率。 |
| `performance.dab.start-distance` | DEAR 生效起始距离 | int | `12`（≥ 0，单位：方块） | 🔄 | 实体距玩家多远时开始受 DEAR 影响。距离小于此值的实体保持原版全速 tick。 |
| `performance.dab.max-tick-freq` | 最远实体最大 tick 间隔 | int | `20`（≥ 1，单位：tick；20 = 1 秒） | 🔄 | 距离最远的实体多久 tick 一次 AI。值越大越省 CPU，但远处实体行为越迟钝。 |
| `performance.dab.activation-dist-mod` | 距离对频率的影响系数 | int | `8`（建议 7–9） | 🔄 | 距离对 tick 频率的影响强度。计算公式：`频率 = (到玩家距离^2) / (2^本值)`。想让远处实体 tick 更少（更省 CPU）用 `7`；想让远处实体 tick 更多（更接近原版）用 `9`。 |
| `performance.dab.dont-enable-if-in-water` | 水中实体不受 DEAR 影响 | bool | `false`（`true`/`false`） | 🔄 | 是否让水中的实体始终保持全速 tick。开启后水中生物（如鱼、鱿鱼）的 AI 不会被降频。 |
| `performance.dab.blacklisted-entities` | DEAR 忽略的实体列表 | string[] | `[]`（实体命名空间 ID 列表） | 🔄 | 不受 DEAR 影响、始终保持全速 AI 的实体列表（如 `minecraft:villager`）。 |

---

### 3. 实体激活范围（optimizations.entity.activation-range）

> 控制实体在距玩家多远时停止 tick（完全冻结）。这是 Paper/Spigot 已有功能，Leaf 在其基础上做了优化。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `optimizations.entity.activation-range.animals` | 动物激活范围 | int | `32`（≥ 0，单位：方块） | 🔄 | 距玩家多少方块内的动物会 tick。超出范围的动物会被冻结。 |
| `optimizations.entity.activation-range.monsters` | 怪物激活范围 | int | `32`（≥ 0，单位：方块） | 🔄 | 距玩家多少方块内的怪物会 tick。 |
| `optimizations.entity.activation-range.raiders` | 袭击者激活范围 | int | `48`（≥ 0，单位：方块） | 🔄 | 距玩家多少方块内的袭击者（掠夺者、唤魔者等）会 tick。 |
| `optimizations.entity.activation-range.misc` | 杂项实体激活范围 | int | `16`（≥ 0，单位：方块） | 🔄 | 距玩家多少方块内的杂项实体（掉落物、经验球、箭矢等）会 tick。 |
| `optimizations.entity.activation-range.water` | 水生实体激活范围 | int | `16`（≥ 0，单位：方块） | 🔄 | 距玩家多少方块内的水生实体（鱼、鱿鱼、海豚等）会 tick。 |
| `optimizations.entity.activation-range.villagers` | 村民激活范围 | int | `32`（≥ 0，单位：方块） | 🔄 | 距玩家多少方块内的村民会 tick。村民 AI 较重，建议保持适中。 |
| `optimizations.entity.activation-range.flying-monsters` | 飞行怪物激活范围 | int | `32`（≥ 0，单位：方块） | 🔄 | 距玩家多少方块内的飞行怪物（恶魂、幻翼等）会 tick。 |

---

### 4. 生物生成限制（optimizations.spawning）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `optimizations.spawning.per-player-mob-spawns` | 按玩家单独计算生物生成 | bool | `true`（`true`/`false`） | 🔄 | 是否按玩家单独计算生物生成上限，而非全局共享。可避免单个玩家刷怪场耗尽全服生成配额。**异步生物生成前置条件**。 |
| `optimizations.spawning.spawn-limits.monsters` | 怪物生成上限 | int | `70`（≥ 0） | 🔄 | 每个玩家周围可生成的怪物数量上限。 |
| `optimizations.spawning.spawn-limits.animals` | 动物生成上限 | int | `10`（≥ 0） | 🔄 | 每个玩家周围可生成的动物数量上限。 |
| `optimizations.spawning.spawn-limits.water-animals` | 水生动物生成上限 | int | `5`（≥ 0） | 🔄 | 每个玩家周围可生成的水生动物数量上限。 |
| `optimizations.spawning.spawn-limits.water-ambient` | 水环境生物生成上限 | int | `20`（≥ 0） | 🔄 | 每个玩家周围可生成的水环境生物（如热带鱼）数量上限。 |
| `optimizations.spawning.spawn-limits.ambient` | 环境生物生成上限 | int | `15`（≥ 0） | 🔄 | 每个玩家周围可生成的环境生物（蝙蝠）数量上限。 |

---

### 5. 网络设置（network）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `network.compression-threshold` | 网络压缩阈值 | int | `256`（≥ 0，单位：字节） | 🔄 | 数据包大小超过此值时才进行压缩。`0` = 全部压缩；`-1` = 禁用压缩。 |
| `network.compression-level` | 压缩级别 | int | `6`（0-9） | 🔄 | 网络压缩的级别。`0` = 不压缩（最快）；`9` = 最大压缩（最慢）。 |

---

### 6. 协议支持（network.protocol-support）

> Leaf 集成了 Leaves 的协议支持功能，可与多种客户端 Mod 直接通信，无需插件中转。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `network.protocol-support.jade-protocol` | Jade 协议支持 | bool | `false`（`true`/`false`） | 🔄 | 是否启用 Jade（看向方块/实体时显示其信息的客户端 Mod）的服务器端协议支持。开启后玩家无需 Jade 服务端插件即可看到信息。 |
| `network.protocol-support.appleskin-protocol` | AppleSkin 协议支持 | bool | `false`（`true`/`false`） | 🔄 | 是否启用 AppleSkin（显示饥饿值、饱和度、消耗度的客户端 Mod）的服务器端协议支持。 |
| `network.protocol-support.appleskin-protocol-sync-tick-interval` | AppleSkin 同步间隔 | int | `20`（≥ 1，单位：tick；20 = 1 秒） | 🔄 | AppleSkin 协议同步数据的频率。值越小越实时但网络开销越大。 |
| `network.protocol-support.xaero-map-protocol` | Xaero 地图协议支持 | bool | `false`（`true`/`false`） | 🔄 | 是否启用 Xaero's Minimap / World Map 客户端 Mod 的服务器端协议支持，向其发送世界边界等数据。 |
| `network.protocol-support.syncmatica-protocol` | Syncmatica 协议支持 | bool | `false`（`true`/`false`） | 🔄 | 是否启用 Syncmatica（允许客户端在服务器世界共享 schematica 模式的 LiteLoader Mod）协议支持。 |
| `network.protocol-support.syncmatica-quota-limit` | Syncmatica 配额上限 | int | `40000000`（≥ 0，单位：字节） | 🔄 | Syncmatica 协议单个玩家可共享的数据量上限。 |

---

### 7. 玩法设置（gameplay）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `gameplay.player.max-use-item-distance` | 使用物品最大距离 | double | `4.0`（≥ 0，单位：方块） | 🔄 | 玩家使用物品（如吃东西、扔药水）时允许的最大距离。无政府服务器常调高以支持远距离使用。 |

---

### 8. 杂项设置（misc）

#### 8.1 服务器品牌重写（misc.rebrand）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.rebrand.server-mod-name` | 服务器 Mod 名称 | string | `Leaf`（任意字符串） | 🔄 | 玩家按 F3 看到的服务器 Mod 名称。原版显示 `vanilla`，可改成你的服务器品牌。 |
| `misc.rebrand.server-gui-name` | 服务器 GUI 标题 | string | `Leaf Console`（任意字符串） | 🔄 | 服务器控制台窗口标题（仅在不使用 nogui 启动时生效）。 |

#### 8.2 安全种子（misc.secure-seed）

> Matter 安全种子技术：将世界种子从 64 位提升到 1024 位，使种子分析工具无法推算服务器世界种子。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.secure-seed.enabled` | 启用安全种子 | bool | `false`（`true`/`false`） | ✅ | 是否启用 1024 位安全种子。开启后所有矿物与结构生成使用加密种子，无法被分析。⚠️ **启用后无法关闭**，否则世界生成会不一致。 |

#### 8.3 Sentry 错误监控（misc.sentry）

> Sentry 是开源的错误追踪平台，可实时捕获服务器异常并上报。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.sentry.dsn` | Sentry DSN 地址 | string | `""`（Sentry DSN URL） | 🔄 | Sentry 项目的 Data Source Name。留空则禁用 Sentry 上报。可在 sentry.io 免费注册获取。 |
| `misc.sentry.log-level` | 上报日志级别 | enum | `WARN`（`INFO`/`WARN`/`ERROR`/`DEBUG`） | 🔄 | 上报到 Sentry 的最低日志级别。 |
| `misc.sentry.only-log-thrown` | 仅上报抛出异常 | bool | `true`（`true`/`false`） | 🔄 | 是否仅上报实际抛出的异常，过滤掉纯日志记录。 |

#### 8.4 玩家档案缓存（misc.cache）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.cache.cache-player-profile-result` | 缓存玩家档案 | bool | `true`（`true`/`false`） | 🔄 | 是否缓存玩家档案（皮肤、UUID 等）查询结果，减少 Mojang API 调用。 |
| `misc.cache.cache-player-profile-result-timeout` | 档案缓存时长 | int | `1440`（≥ 0，单位：分钟；1440 = 1 天） | 🔄 | 玩家档案缓存的有效时长。 |

#### 8.5 TPS 显示（misc）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.including-5s-in-get-tps` | TPS 包含最近 5 秒数据 | bool | `true`（`true`/`false`） | 🔄 | 计算 TPS 时是否包含最近 5 秒的数据，提供更平滑的性能视图。 |

---

### 9. 极简优化选项（opt）

> 一些零散的小优化，建议保持默认开启。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `opt.skip-map-item-data-updates` | 跳过地图物品数据更新 | bool | `true`（`true`/`false`） | 🔄 | 是否跳过不必要的地图物品数据更新，减少网络包发送。 |
| `opt.reduce-useless-packets` | 减少无用数据包 | bool | `true`（`true`/`false`） | 🔄 | 是否合并或跳过部分无用数据包，降低网络开销。 |
| `opt.throttle-hopper-when-full` | 满漏斗节流 | bool | `true`（`true`/`false`） | 🔄 | 当漏斗容器已满时是否限制其检查频率，减少 CPU 占用。 |

---

## config/leaf-global.yml（Leaf 全局级专属配置）

`config/leaf-global.yml` 位于 `config/` 目录下，存放**全局**生效的配置（不随世界变化）。结构与 `leaf.yml` 类似，但仅包含适合全局生效的子集。

### 阅读约定

与 `leaf.yml` 相同。

---

### 1. 全局异步设置（async）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `async.async-pathfinding.enabled` | 全局异步路径查找 | bool | `false`（`true`/`false`） | ✅ | 同 `leaf.yml` 中对应项，但作用于所有世界。 |
| `async.async-entity-tracker.enabled` | 全局多线程实体追踪 | bool | `false`（`true`/`false`） | ✅ | 同 `leaf.yml` 中对应项，但作用于所有世界。 |

---

### 2. 全局性能优化（performance）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `performance.dab.enabled` | 全局 DEAR 优化 | bool | `true`（`true`/`false`） | 🔄 | 同 `leaf.yml` 中对应项，但作用于所有世界。 |

---

### 3. 全局杂项（misc）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.sentry.dsn` | 全局 Sentry DSN | string | `""`（Sentry DSN URL） | 🔄 | 同 `leaf.yml` 中对应项，全局生效。 |
| `misc.including-5s-in-get-tps` | 全局 TPS 包含 5 秒数据 | bool | `true`（`true`/`false`） | 🔄 | 同 `leaf.yml` 中对应项，全局生效。 |

---

## 配置示例

### 高性能生存服（200 玩家 + 8 核 CPU）

```yaml
# leaf.yml
async:
  async-pathfinding:
    enabled: true
    max-threads: 4
    keepalive: 60
    queue-size: 1024
    reject-policy: "FLUSH_ALL"
  async-entity-tracker:
    enabled: true
    max-threads: 3
    compat-mode: false
    queue-size: 1152
  async-mob-spawning:
    enabled: true
  async-player-data-save:
    enabled: true
  faster-random-generator:
    enabled: true
    random-generator: "XOROSHIRO128_PLUS_PLUS"
    enable-for-worldgen: false
    warn-for-slime-chunk: true
    use-legacy-for-slime-chunk: true

performance:
  dab:
    enabled: true
    start-distance: 12
    max-tick-freq: 20
    activation-dist-mod: 8
    dont-enable-if-in-water: false
    blacklisted-entities: []

network:
  compression-threshold: 256
  compression-level: 6
  protocol-support:
    jade-protocol: true
    appleskin-protocol: true

misc:
  rebrand:
    server-mod-name: "我的奇幻世界"
    server-gui-name: "奇幻世界控制台"
  sentry:
    dsn: "https://your-sentry-dsn@sentry.io/123"
    log-level: "WARN"
    only-log-thrown: true
```

### 小型朋友服（4 玩家 + 4 核 CPU）

```yaml
# leaf.yml
async:
  async-pathfinding:
    enabled: false  # 玩家少时主线程压力不大，可关闭
  async-entity-tracker:
    enabled: false
  faster-random-generator:
    enabled: true
    random-generator: "XOROSHIRO128_PLUS_PLUS"
    enable-for-worldgen: false

performance:
  dab:
    enabled: true
    start-distance: 12
    max-tick-freq: 20
    activation-dist-mod: 8

misc:
  rebrand:
    server-mod-name: "朋友乐园"
```

---

## 常见问题

### Q1：异步路径查找开了反而卡顿？
A：检查 `reject-policy`。低配服务器（CPU < 12 核）应使用 `CALLER_RUNS` 而非 `FLUSH_ALL`。同时调小 `queue-size` 避免队列堆积。

### Q2：使用 Citizens NPC 插件后实体显示异常？
A：在 `async.async-entity-tracker.compat-mode` 设为 `true` 启用兼容模式。

### Q3：开启安全种子后能关闭吗？
A：**不能**。开启安全种子后世界生成已使用加密种子，关闭会导致新生成区域与已有区域不一致。建议新建世界时再决定。

### Q4：`faster-random-generator` 影响史莱姆农场吗？
A：默认 `use-legacy-for-slime-chunk: true` 时**不影响**史莱姆区块判定。但其他随机事件（如怪物掉落）可能略有差异。

### Q5：协议支持需要客户端安装对应 Mod 吗？
A：需要。`jade-protocol` 等仅向已安装对应客户端 Mod 的玩家发送数据，未安装的玩家不受影响。

---

## 参考资料

- Leaf 官方 GitHub：https://github.com/Winds-Studio/Leaf
- Leaf 官方文档站：https://www.leafmc.one/zh/
- Leaf 官方 QQ 群：619278377
- Leaf Discord：https://discord.com/invite/gfgAwdSEuM
- Gale 项目（Leaf 上游）：https://github.com/GaleMC/Gale
