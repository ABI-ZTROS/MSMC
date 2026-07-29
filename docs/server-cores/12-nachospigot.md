# NachoSpigot 服务器配置文件中文手册

> NachoSpigot 是基于 **TacoSpigot 1.8.9** 的高性能优化分支，提供大量性能增强与漏洞修复，适合 1.8.9 PvP 服 / 大量玩家场景。
> 继承关系：Vanilla → Spigot → Paper → TacoSpigot → **NachoSpigot**
> 官方 GitHub：https://github.com/CobbleSword/NachoSpigot
> 数据来源：CobbleSword/NachoSpigot 仓库 `README.md`（master 分支，commit `5655b72`）+ 社区 nacho.yml 默认值
> 适用版本基准：NachoSpigot 1.8.9（master 分支，2022 年最后构建）
> ⚠️ **项目已停止维护**（README 顶部声明）。官方推荐迁移到 [WindSpigot](https://github.com/Wind-Development/WindSpigot) 或 [PandaSpigot](https://github.com/hpfxd/PandaSpigot)，或升级到 1.18+ 使用 Purpur。

NachoSpigot 完整继承 TacoSpigot / Paper / Spigot / Bukkit 的配置体系，并新增独立的 `nacho.yml` 配置文件（含全局 `settings` 与每世界 `world-settings` 两大部分）。本文档仅聚焦 NachoSpigot 独有的 `nacho.yml`。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（实体激活范围等） |
| paper.yml | YAML | Paper 继承 | Paper 配置（1.8.x 时代为单一 paper.yml） |
| taco.yml | YAML | TacoSpigot 继承 | TacoSpigot 配置 |
| **nacho.yml** | YAML | **NachoSpigot 专属** | **NachoSpigot 全部独有配置（本文档重点）** |
| knockback.yml | YAML | NachoSpigot 专属 | 自定义击退配置（配合 `Nacho-0050` 补丁） |

> 说明：NachoSpigot 完整继承 TacoSpigot / Paper / Spigot / Bukkit 的全部配置体系，本文档仅聚焦 NachoSpigot 独有的 `nacho.yml`。

## nacho.yml 整体结构

```yaml
config-version: 6                # 配置版本号（内部用，勿手改）
settings:                        # 全局设置
  chunk: { ... }                 # 区块线程
  commands: { ... }              # 命令开关
  event: { ... }                 # 事件开关
  fixed-pools: { ... }           # 固定对象池
  # ... 约 35 个全局项
world-settings:                  # 每世界设置
  default:
    verbose: false
    physics: { ... }             # 物理开关
    explosions: { ... }          # 爆炸
    entity: { ... }              # 实体
    # ... 约 16 个每世界项
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `settings.commands.enable-reload-command`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载。
- **作用域**：全局（`settings.*`）对整个服务器生效；每世界（`world-settings.default.*`）可按世界名覆盖。

---

## 1. 信息块

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `config-version` | 配置版本号 | int | `6`（—） | ✅ | 内部使用，**不要手动修改**。NachoSpigot 用它做配置自动升级与兼容性判断。 |

---

## 2. settings.chunk（区块线程）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.chunk.threads` | 区块线程数 | int | `2`（≥ 0） | ✅ | 用于区块加载 / 生成的线程数。0 = 禁用多线程区块；建议 2–4。值越大区块加载越快但 CPU 越高。 |
| `settings.chunk.players-per-thread` | 每线程玩家数 | int | `50`（≥ 1） | ✅ | 每多少名玩家分配 1 个区块线程（与 `threads` 配合的负载估算参数）。 |

---

## 3. settings（全局杂项）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.player-time-statistics-interval` | 玩家统计间隔 | int | `90`（≥ 0，单位：tick） | 🔄 | 多久统计一次玩家在线时间等数据（tick）。20 tick = 1 秒，90 = 4.5 秒。值越大越省 CPU 但统计精度越低。 |
| `settings.panda-wire` | Panda 红石线优化 | bool | `true`（`true`/`false`） | 🔄 | 启用 PandaSpigot 的红石线优化。可显著降低红石密集场景的 CPU 占用。生电服可能需要 `false` 还原原版时序。 |
| `settings.brand-name` | 服务端品牌名 | string | `NachoSpigot` | 🔄 | 发送给客户端的服务端品牌名（F3 界面 "Mod" 字段）。可用 § 颜色码。可隐藏真实核心类型。 |
| `settings.anti-malware` | 反恶意软件扫描 | bool | `false`（`true`/`false`） | 🔄 | 启动时扫描插件 jar 是否包含已知恶意代码特征。开发 / 测试服可开启；生产服按需。 |
| `settings.disabled-block-fall-animation` | 禁用方块下落动画 | bool | `false`（`true`/`false`） | 🔄 | 禁用方块（如沙子、砂砾）下落时的客户端动画。`true` 可减少网络包但视觉体验下降。 |
| `settings.patch-protocollib` | 修补 ProtocolLib | bool | `true`（`true`/`false`） | 🔄 | 应用 ProtocolLib 兼容性补丁。使用 ProtocolLib 的服建议保持 `true`。 |
| `settings.stop-notify-bungee` | 停止 Bungee 通知 | bool | `true`（`true`/`false`） | 🔄 | 不向 BungeeCord 发送服务器状态通知。可减少跨服通信开销。 |
| `settings.anti-crash` | 反崩溃保护 | bool | `true`（`true`/`false`） | 🔄 | 启用反崩溃机制，捕获并阻止可能导致服务器崩溃的异常操作。生产服保持 `true`。 |
| `settings.fast-operators` | 快速 OP 操作 | bool | `false`（`true`/`false`） | 🔄 | 优化 OP 权限检查的性能。OP 较多的服可开启以加速权限判定。 |
| `settings.save-empty-scoreboard-teams` | 保存空记分板队伍 | bool | `false`（`true`/`false`） | 🔄 | 是否保存空的记分板队伍到磁盘。`false` 可减少无意义的队伍数据写入。 |
| `settings.kick-on-illegal-behavior` | 非法行为踢出 | bool | `true`（`true`/`false`） | 🔄 | 玩家执行非法操作（如发包作弊）时是否踢出。反作弊相关，生产服保持 `true`。 |
| `settings.stop-decoding-itemstack-on-place` | 放置时不解码物品 | bool | `true`（`true`/`false`） | 🔄 | 放置方块时跳过 ItemStack 的重复解码。可减少 CPU 开销，正常服保持 `true`。 |
| `settings.use-tcp-nodelay` | 启用 TCP_NODELAY | bool | `true`（`true`/`false`） | ✅ | 启用 TCP_NODELAY 禁用 Nagle 算法，降低网络延迟。PvP 服强烈建议 `true`。修改需重启。 |
| `settings.faster-cannon-tracker` | 快速炮弹追踪 | bool | `true`（`true`/`false`） | 🔄 | 优化 TNT / 炮弹实体的追踪性能。TNT 大炮服保持 `true`。 |
| `settings.fix-eat-while-running` | 修复跑动进食 | bool | `true`（`true`/`false`） | 🔄 | 修复玩家跑动时进食的漏洞。PvP 服保持 `true`。 |
| `settings.hide-projectiles-from-hidden-players` | 隐藏玩家对隐藏玩家发射弹射物 | bool | `false`（`true`/`false`） | 🔄 | 被隐藏的玩家发射的弹射物对其他玩家也不可见。隐身插件相关。 |
| `settings.lag-compensated-potions` | 卡顿补偿药水 | bool | `false`（`true`/`false`） | 🔄 | 启用卡顿补偿的药水效果计算。实验性，可能影响 PvP 平衡。 |
| `settings.smooth-potting` | 平滑投掷药水 | bool | `true`（`true`/`false`） | 🔄 | 平滑投掷药水的动画 / 时机。PvP 服保持 `true`。 |
| `settings.anti-enderpearl-glitch` | 防末影珍珠漏洞 | bool | `true`（`true`/`false`） | 🔄 | 防止末影珍珠传送漏洞。PvP 服保持 `true`。 |
| `settings.disable-infinisleeper-thread-usage` | 禁用 Infinisleeper 线程 | bool | `false`（`true`/`false`） | 🔄 | 禁用 Infinisleeper 后台线程。一般保持 `false`。 |
| `settings.enable-fastmath` | 启用 FastMath | bool | `false`（`true`/`false`） | 🔄 | 使用更快的数学运算库替代原版。实验性，可能影响某些计算精度。 |
| `settings.tile-entity-ticking-time` | 方块实体 tick 时间 | int | `20`（≥ 0，单位：tick） | 🔄 | 方块实体（如熔炉、漏斗）的 tick 间隔。20 = 每 20 tick（1 秒）处理一次。值越大越省 CPU 但方块实体变慢。 |
| `settings.item-dirty-ticks` | 物品脏标记 tick | int | `20`（≥ 0） | 🔄 | 多久标记一次物品栏为「脏」以同步给客户端。值越大网络包越少但物品栏更新越慢。 |
| `settings.use-tcp-fastopen` | 启用 TCP Fast Open | bool | `true`（`true`/`false`） | ✅ | 启用 TCP Fast Open（TFO）减少握手延迟。需操作系统与内核支持。修改需重启。 |
| `settings.tcp-fastopen-mode` | TCP Fast Open 模式 | int | `1`（0–3） | ✅ | TFO 模式：0 = 禁用；1 = 仅客户端模式；2 = 仅服务端模式；3 = 双向启用。修改需重启。 |
| `settings.enable-protocollib-shim` | 启用 ProtocolLib 垫片 | bool | `true`（`true`/`false`） | 🔄 | 启用 ProtocolLib 兼容垫片。使用 ProtocolLib 的服保持 `true`。 |
| `settings.instant-interaction` | 瞬时交互 | bool | `false`（`true`/`false`） | 🔄 | 跳过交互延迟检查。`true` 可能影响反作弊。一般保持 `false`。 |
| `settings.instant-use-entity` | 瞬时实体使用 | bool | `false`（`true`/`false`） | 🔄 | 跳过实体使用延迟检查。`true` 可能影响反作弊。一般保持 `false`。 |

---

## 4. settings.commands（命令开关）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.commands.enable-version-command` | 启用 /version 命令 | bool | `false`（`true`/`false`） | 🔄 | 是否允许玩家使用 `/version`（`/ver`）查看服务端版本信息。关闭可隐藏核心类型，防信息泄露。 |
| `settings.commands.enable-plugins-command` | 启用 /plugins 命令 | bool | `false`（`true`/`false`） | 🔄 | 是否允许玩家使用 `/plugins`（`/pl`）查看已加载插件列表。公网服建议关闭以防泄露插件信息。 |
| `settings.commands.enable-reload-command` | 启用 /reload 命令 | bool | `false`（`true`/`false`） | 🔄 | 是否允许使用 `/reload` 命令。`/reload` 易导致插件状态异常，**强烈建议关闭**。 |

---

## 5. settings.event（事件开关）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.event.fire-entity-explode-event` | 触发实体爆炸事件 | bool | `true`（`true`/`false`） | 🔄 | 是否触发 `EntityExplodeEvent`。无插件监听时可设 `false` 减少开销，但爆炸保护插件会失效。 |
| `settings.event.fire-player-move-event` | 触发玩家移动事件 | bool | `false`（`true`/`false`） | 🔄 | 是否触发 `PlayerMoveEvent`。⚠️ 设为 `false` 会破坏大量插件（区域保护、反作弊等）。仅极度追求性能且无移动相关插件时才可关。 |
| `settings.event.fire-leaf-decay-event` | 触发树叶凋落事件 | bool | `true`（`true`/`false`） | 🔄 | 是否触发 `LeavesDecayEvent`。无插件监听时可设 `false` 减少开销。 |

---

## 6. settings.fixed-pools（固定对象池）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.fixed-pools.use-fixed-pools-for-explosions` | 爆炸用固定池 | bool | `false`（`true`/`false`） | 🔄 | 爆炸计算使用固定大小的对象池，避免频繁 GC。TNT 密集服（如 TNT 大炮）可设 `true` 减少卡顿。 |
| `settings.fixed-pools.size` | 固定池大小 | int | `500`（≥ 1） | 🔄 | 固定对象池的容量。需大于同时进行的爆炸计算数，过小会回退到普通分配。 |

---

## 7. world-settings.default（每世界杂项）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.verbose` | 详细日志 | bool | `false`（`true`/`false`） | 🔄 | 是否在世界启动时输出该世界配置的详细信息。排查问题可临时开启。 |
| `world-settings.default.enable-lava-to-cobblestone` | 岩浆变圆石 | bool | `false`（`true`/`false`） | 🔄 | 允许水流接触岩浆生成圆石（原版行为）。`false` 可禁用以减少圆石农场卡服。 |
| `world-settings.default.infinite-water-sources` | 无限水源 | bool | `true`（`true`/`false`） | 🔄 | 允许 2×2 水池形成无限水源（原版行为）。`false` 可禁用以限制水农场。 |
| `world-settings.default.disable-sponge-absorption` | 禁用海绵吸水 | bool | `false`（`true`/`false`） | 🔄 | 禁用海绵吸水行为。`true` 可减少大量吸水计算的开销。 |
| `world-settings.default.tick-enchantment-tables` | 附魔台 tick | bool | `false`（`true`/`false`） | 🔄 | 是否 tick 附魔台（周围书架的浮动书页动画）。`false` 跳过此 tick 以省 CPU，对应补丁 `Nacho-0049`。 |
| `world-settings.default.block-operations` | 方块操作 | bool | `true`（`true`/`false`） | 🔄 | 启用方块操作批处理优化。一般保持 `true`。 |
| `world-settings.default.unload-chunks` | 卸载区块 | bool | `true`（`true`/`false`） | 🔄 | 允许自动卸载无玩家附近的区块以释放内存。内存紧张服保持 `true`。 |

---

## 8. world-settings.default.physics（每世界物理）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.physics.disable-place` | 禁用放置物理 | bool | `true`（`true`/`false`） | 🔄 | 放置方块时不触发物理更新（如沙子下落、红石更新）。⚠️ 会影响大量生电机制，仅极限性能服使用。 |
| `world-settings.default.physics.disable-update` | 禁用更新物理 | bool | `true`（`true`/`false`） | 🔄 | 方块变化时不触发周边物理更新。⚠️ 与 `disable-place` 类似，会破坏红石与生电。 |

---

## 9. world-settings.default.explosions（每世界爆炸）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.explosions.constant-radius` | 恒定爆炸半径 | bool | `false`（`true`/`false`） | 🔄 | 爆炸使用恒定半径而非随机半径。`true` 使爆炸范围可预测，便于 PvP 平衡。 |
| `world-settings.default.explosions.explode-protected-regions` | 受保护区域爆炸 | bool | `true`（`true`/`false`） | 🔄 | 是否在受保护区域（如 spawn保护区）仍计算爆炸。`false` 可跳过保护区爆炸以省 CPU。 |
| `world-settings.default.explosions.reduced-density-rays` | 减少密度射线 | bool | `true`（`true`/`false`） | 🔄 | 减少爆炸密度射线计算量。`true` 可显著降低 TNT 大量爆炸时的 CPU 占用，但爆炸破坏精度略降。 |

---

## 10. world-settings.default.entity（每世界实体）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.entity.mob-ai` | 生物 AI | bool | `false`（`true`/`false`） | 🔄 | ⚠️ 字段名易误解：`false` = 启用原版生物 AI；`true` = 禁用生物 AI（生物静止不动）。极限性能服才设 `true`。 |
| `world-settings.default.entity.mob-sound` | 生物声音 | bool | `false`（`true`/`false`） | 🔄 | ⚠️ 同上语义反转：`false` = 启用生物声音；`true` = 禁用生物声音以省 CPU。 |
| `world-settings.default.entity.entity-activation` | 实体激活 | bool | `false`（`true`/`false`） | 🔄 | ⚠️ `false` = 启用原版实体激活范围；`true` = 禁用激活范围（所有实体全 tick）。一般保持 `false`。 |
| `world-settings.default.entity.endermite-spawning` | 末影螨生成 | bool | `false`（`true`/`false`） | 🔄 | 是否允许末影螨生成。`false` 禁用以减少末影珍珠农场产生的实体。 |

---

## 配置示例（nacho.yml 完整默认值）

```yaml
config-version: 6
settings:
  chunk:
    threads: 2
    players-per-thread: 50
  player-time-statistics-interval: 90
  panda-wire: true
  brand-name: NachoSpigot
  commands:
    enable-version-command: false
    enable-plugins-command: false
    enable-reload-command: false
  anti-malware: false
  fixed-pools:
    use-fixed-pools-for-explosions: false
    size: 500
  disabled-block-fall-animation: false
  patch-protocollib: true
  stop-notify-bungee: true
  anti-crash: true
  fast-operators: false
  save-empty-scoreboard-teams: false
  kick-on-illegal-behavior: true
  event:
    fire-entity-explode-event: true
    fire-player-move-event: false      # ⚠️ 默认关闭，开启会破坏大量插件
    fire-leaf-decay-event: true
  stop-decoding-itemstack-on-place: true
  use-tcp-nodelay: true                # PvP 服保持 true
  faster-cannon-tracker: true
  fix-eat-while-running: true
  hide-projectiles-from-hidden-players: false
  lag-compensated-potions: false
  smooth-potting: true
  anti-enderpearl-glitch: true
  disable-infinisleeper-thread-usage: false
  enable-fastmath: false
  tile-entity-ticking-time: 20
  item-dirty-ticks: 20
  use-tcp-fastopen: true
  tcp-fastopen-mode: 1
  enable-protocollib-shim: true
  instant-interaction: false
  instant-use-entity: false
world-settings:
  default:
    verbose: false
    physics:
      disable-place: true              # ⚠️ 影响生电
      disable-update: true             # ⚠️ 影响生电
    enable-lava-to-cobblestone: false
    explosions:
      constant-radius: false
      explode-protected-regions: true
      reduced-density-rays: true
    entity:
      mob-ai: false                    # false = 启用 AI
      mob-sound: false                 # false = 启用声音
      entity-activation: false         # false = 启用激活范围
      endermite-spawning: false
    infinite-water-sources: true
    disable-sponge-absorption: false
    tick-enchantment-tables: false
    block-operations: true
    unload-chunks: true
```

---

## 优化建议（针对 1.8.9 PvP 服）

1. **网络延迟**：保持 `use-tcp-nodelay: true` 与 `use-tcp-fastopen: true`，对 PvP 手感提升明显。`tcp-fastopen-mode` 设 `1` 或 `3`。
2. **TNT 大炮服**：开启 `fixed-pools.use-fixed-pools-for-explosions: true`，`fixed-pools.size` 设 1000+，并保持 `faster-cannon-tracker: true`、`explosions.reduced-density-rays: true`。
3. **PvP 公平性**：保持 `fix-eat-while-running: true`、`anti-enderpearl-glitch: true`、`kick-on-illegal-behavior: true`，并关闭 `lag-compensated-potions`。
4. **信息泄露**：关闭 `commands.enable-version-command`、`commands.enable-plugins-command`、`commands.enable-reload-command`，并将 `brand-name` 改为通用名。
5. **生电服慎用物理开关**：`physics.disable-place` 与 `physics.disable-update` 默认 `true` 会破坏红石 / 生电，生电服请改为 `false`。
6. **ProtocolLib 用户**：保持 `patch-protocollib: true` 与 `enable-protocollib-shim: true`，否则 ProtocolLib 插件可能失效。
7. **区块线程**：`chunk.threads` 建议 2–4，过大会与其他系统争抢 CPU。
8. **迁移建议**：项目已停更，新服建议直接用 Purpur（1.18+）或 PandaSpigot（1.8.9 维护中）。

> 参考来源：CobbleSword/NachoSpigot [README.md](https://github.com/CobbleSword/NachoSpigot/blob/master/README.md)（补丁列表与功能说明）、社区默认 `nacho.yml`。
