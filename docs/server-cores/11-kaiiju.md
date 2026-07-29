# Kaiiju 服务器配置文件中文手册

> Kaiiju 是基于 **Folia** 的 Minecraft 服务端分支，专为**原版 / 无政府（Anarchy）服务器**设计。它在 Folia 多线程架构上叠加了 Xymb 线性格式（节省约 50% 磁盘）、异步寻路、原版漏洞开关（刷沙、RNG 控制）等特性。
> 继承关系：Vanilla → Spigot → Paper → Folia → **Kaiiju**
> 官方 GitHub：https://github.com/KaiijuMC/Kaiiju
> 配置 Wiki：https://github.com/KaiijuMC/Kaiiju/wiki/Configuration
> 数据来源：KaiijuMC/Kaiiju 仓库 `README.md`（ver/1.20.1 分支，build #240）+ Configuration Wiki
> 适用版本基准：Kaiiju 1.20.1（2023 年最后构建，仓库已 Public archive）
> ⚠️ 项目已归档停更，建议仅用于研究或作为 Folia 配置参考。

Kaiiju 完整继承 Folia 的全部配置体系（包括 `paper-global.yml` 的 `threaded-regions` 节），并新增独立的 `kaiiju.yml` 配置文件。本文档仅聚焦 Kaiiju 独有的 `kaiiju.yml`，Folia 多线程配置请参阅 Folia 手册。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| config/paper-global.yml | YAML | Paper + Folia 继承 | Folia 多线程配置（`threaded-regions` 等） |
| config/paper-world-defaults.yml | YAML | Paper 继承 | Paper 世界默认配置 |
| **kaiiju.yml** | YAML | **Kaiiju 专属** | **Kaiiju 全部独有配置（本文档重点）** |
| kaiiju-entity-limits.yml | YAML | Kaiiju 专属 | 实体限制配置（配合 `enable-entity-throttling` 使用） |

> 说明：Kaiiju 完整继承 Folia / Paper 的全部配置体系，本文档仅聚焦 Kaiiju 独有的 `kaiiju.yml`。Folia 多线程配置请参阅 Folia 手册。

## kaiiju.yml 整体结构

```yaml
region-format:               # 全局：线性格式刷新
  linear:
    flush-frequency: 10
    flush-max-threads: 1
network:                     # 全局：网络
  send-null-entity-packets: true
  alternate-keepalive: false
  kick-player-on-bad-packet: true
optimization:                # 全局：优化
  disable-vanish-api: false
  disable-player-stats: false
  disable-arm-swing-event: false
  async-path-processing:
    enable: false
    max-threads: 0
    keepalive: 60
    queue-capacity: 4096
gameplay:                    # 全局：玩法
  server-mod-name: Kaiiju
  shared-random-for-players: true
unsupported:                 # 全局：不安全实验
  disable-ensure-tick-thread-checks: false
  global-event-synchronization: false
world-settings:              # 每世界
  default:
    region-format: { ... }
    optimization: { ... }
    gameplay: { ... }
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `network.alternate-keepalive`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载。
- **作用域**：全局（Global）配置对整个服务器生效；每世界（Per-World）配置在 `world-settings.default` 下，可按世界名覆盖。

---

## 1. region-format.linear（全局：线性格式刷新）

> Xymb 线性格式（Linear）可将主世界 / 下界的磁盘占用减少约 50%，末地减少约 95%。本节控制全局的刷新策略；每世界是否启用 Linear 由 `world-settings.default.region-format.format` 决定。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `region-format.linear.flush-frequency` | 线性文件刷新频率 | int | `10`（≥ 1，单位：秒） | ✅ | 多久将内存中的线性 Region 数据刷新到磁盘一次（秒）。值越小越频繁、崩服丢数据越少但 IO 越多；值越大越省 IO 但丢数据风险越高。 |
| `region-format.linear.flush-max-threads` | 刷新最大线程数 | int | `1`（≥ 1） | ✅ | 刷新线性 Region 文件时使用的最大线程数。1 = 单线程刷新（安全）；增大可加快刷新但增加磁盘 IO 争用。 |

---

## 2. network（全局：网络）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `network.send-null-entity-packets` | 发送空实体移动包 | bool | `true`（`true`/`false`） | 🔄 | 是否发送空移动实体数据包。设为 `false` 可减少网络流量，除非有插件依赖此行为，否则建议 `false`。 |
| `network.alternate-keepalive` | 备用心跳机制 | bool | `false`（`true`/`false`） | 🔄 | 沿用 Purpur 的备用心跳：每秒发送一个 keepalive 包，仅当 30 秒内无任何响应才踢出玩家。可避免因偶发丢包导致的误踢（玩家不会因为丢一个心跳包就被踢）。 |
| `network.kick-player-on-bad-packet` | 收到坏包踢出玩家 | bool | `true`（`true`/`false`） | 🔄 | 收到损坏 / 非法数据包时是否踢出玩家。设为 `false` 不踢（实验性，可能被恶意客户端利用）。无政府服可考虑 `false`，正常服保持 `true`。 |

---

## 3. optimization（全局：优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `optimization.disable-vanish-api` | 禁用隐身 API | bool | `false`（`true`/`false`） | 🔄 | 禁用 Bukkit 的 Player#hidePlayer / showPlayer 隐身 API。无隐身需求的服务器可设 `true` 以节省性能。 |
| `optimization.disable-player-stats` | 禁用玩家统计 | bool | `false`（`true`/`false`） | 🔄 | 禁用玩家统计信息（如走了多少格、挖了多少方块）的记录与持久化。无政府 / 战斗服通常不需要统计，可设 `true` 提速。 |
| `optimization.disable-arm-swing-event` | 禁用手臂挥动事件 | bool | `false`（`true`/`false`） | 🔄 | 不调用 `PlayerArmSwingEvent`。若没有插件监听此事件（绝大多数服都没有），可设 `true` 减少事件开销。 |

### optimization.async-path-processing（异步寻路处理）

> Kaiiju 修复并重构了 Petal 的异步寻路。启用后寻路计算移至独立线程池，主线程不阻塞。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `optimization.async-path-processing.enable` | 启用异步寻路 | bool | `false`（`true`/`false`） | ✅ | 是否启用异步寻路处理。⚠️ **修改必须重启**，热重载无效。开启后实体寻路移至异步线程池，可显著降低主线程负载。 |
| `optimization.async-path-processing.max-threads` | 异步寻路最大线程数 | int | `0`（0 = 自动；>0 = 手动；<0 = 核心数减去该值） | ✅ | 寻路线程池最大线程数。**0** = 自动（`max(核心数/4, 1)`）；**负数 -n** = `max(核心数 − n, 1)`；**正数** = 固定值。允许线程池在突发负载时临时扩张到该上限。 |
| `optimization.async-path-processing.keepalive` | 空闲线程存活时间 | int | `60`（≥ 0，单位：秒） | ✅ | 当线程数超过核心池大小时，多余空闲线程的存活秒数。短存活时间可快速回收多余线程，长存活时间可应对频繁突发。 |
| `optimization.async-path-processing.queue-capacity` | 任务队列容量 | int | `4096`（≥ 0） | ✅ | 寻路任务等待队列的最大长度。队列满后才会创建新线程（直到 `max-threads`）。大队列可吸收突发任务而不创建过多线程，但会增加延迟。 |

---

## 4. gameplay（全局：玩法）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `gameplay.server-mod-name` | 服务端名称 | string | `Kaiiju` | 🔄 | 发送给客户端的服务端品牌名（F3 界面显示的 "Mod" 字段）。可用于品牌定制或隐藏真实核心类型。 |
| `gameplay.shared-random-for-players` | 玩家共享随机源 | bool | `true`（`true`/`false`） | 🔄 | 玩家共用同一个随机数生成器，而非每个玩家独立 RNG。**这是原版 RNG 操纵（RNG manipulation）的关键**：开启时所有玩家共享 RNG，可被用于预测 / 操纵随机事件（如掉落、生物生成）。无政府服保持 `true` 以允许 RNG 控制。 |

---

## 5. unsupported（全局：不安全实验）

> ⚠️ 本节选项**极不安全**，仅在排查特定问题时使用，生产环境请全部保持默认。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `unsupported.disable-ensure-tick-thread-checks` | 禁用线程检查 | bool | `false`（`true`/`false`） | ✅ | 禁用 Folia 的「确保在正确 tick 线程」安全检查。**绝对不要开启**，会导致数据竞争与崩溃。仅用于调试。 |
| `unsupported.global-event-synchronization` | 全局事件同步 | bool | `false`（`true`/`false`） | ✅ | 启用全局事件同步锁。会显著降低多线程性能，仅用于排查事件竞态问题。 |

---

## 6. world-settings.default.region-format（每世界：区域文件格式）

> 决定每个世界使用何种 Region 文件格式。⚠️ Linear 与 ANVIL **不兼容**，切换前必须用 [LinearRegionFileFormatTools](https://github.com/xymb-endcrystalme/LinearRegionFileFormatTools) 转换。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.region-format.format` | 区域文件格式 | enum | `ANVIL`（`ANVIL` / `LINEAR`） | ✅ | 世界在磁盘上使用的 Region 文件格式。`ANVIL` = Minecraft 原生 .mca 格式（兼容性最好）；`LINEAR` = Xymb 线性格式（主世界/下界省 ~50% 磁盘，末地省 ~95%）。切换必须转换数据，否则世界会丢失。 |
| `world-settings.default.region-format.linear.compression-level` | Linear 压缩级别 | int | `1`（1–22） | ✅ | Linear 格式使用的 ZSTD 压缩级别。推荐 `1` / `3` / `6`。级别越高磁盘越省但 CPU 越高。实测：级别 1 总占用 7.88GB，级别 6 仅 6.59GB（节省约 16%）。 |
| `world-settings.default.region-format.linear.crash-on-broken-symlink` | 符号链接损坏时崩溃 | bool | `true`（`true`/`false`） | ✅ | 当 Region 文件的符号链接损坏时是否让服务器崩溃。`true`（推荐）= 崩溃以暴露问题；`false` = 静默跳过。通过 NFS 访问 Region 文件时建议 `true`。 |

---

## 7. world-settings.default.optimization（每世界：优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.optimization.shulker-box-drop-contents-when-destroyed` | 潜影盒被毁掉落内容 | bool | `true`（`true`/`false`） | 🔄 | 潜影盒被熔岩 / 仙人掌等摧毁时，是否掉落其内部物品。`true` = 原版行为；`false` = 内容物一并销毁。 |
| `world-settings.default.optimization.optimize-hoppers` | 漏斗优化 | bool | `true`（`true`/`false`） | 🔄 | 启用 Paper 的漏斗优化。`false` 可还原 100% 原版漏斗行为，但会破坏大量生电红石机器。生电服可考虑 `false`。 |
| `world-settings.default.optimization.tick-when-empty` | 空世界仍 tick | bool | `true`（`true`/`false`） | 🔄 | 世界无玩家时是否仍进行 tick（实体、红石等）。`false` = 无玩家时世界冻结，省 CPU 但红石机器会停。 |
| `world-settings.default.optimization.enable-entity-throttling` | 实体节流 | bool | `false`（`true`/`false`） | 🔄 | 启用实体数量节流。开启后超限的实体会被限制 / 移除。具体限制在 `kaiiju-entity-limits.yml` 中配置。 |
| `world-settings.default.optimization.disable-achievements` | 禁用成就 | bool | `false`（`true`/`false`） | 🔄 | 禁用成就 / 进度系统的触发与记录。无政府服可设 `true` 提速。 |
| `world-settings.default.optimization.disable-creatures-spawn-events` | 禁用生物生成事件 | bool | `false`（`true`/`false`） | 🔄 | 不触发 `CreatureSpawnEvent`。无插件监听此事件时可设 `true` 减少事件开销，但反作弊 / 限制类插件会失效。 |
| `world-settings.default.optimization.disable-dolphin-swim-to-treasure` | 禁用海豚寻宝 | bool | `false`（`true`/`false`） | 🔄 | 禁用海豚引导玩家寻找沉船 / 海底废墟的行为。可减少海豚寻路计算开销。 |

---

## 8. world-settings.default.gameplay（每世界：玩法 / 漏洞开关）

> ⚠️ 本节包含原版漏洞开关，专为无政府 / 生电服设计。开启后重新引入已被修复的漏洞，正常服请保持默认。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.gameplay.fix-void-trading` | 修复虚空交易 | bool | `true`（`true`/`false`） | 🔄 | 是否修复虚空交易漏洞。`true`（默认）= 修复；`false` = 允许虚空交易。若关闭，建议安装 [Kaiivoid](https://github.com/KaiijuMC/Kaiivoid) 插件替代。 |
| `world-settings.default.gameplay.break-redstone-on-top-of-trap-doors-early` | 提前破坏活板门上红石 | bool | `true`（`true`/`false`） | 🔄 | 始终提前破坏活板门上的红石。`false` 会允许「门切片（portal slicing）」与活板门卡服机器。生电服可设 `false` 还原漏洞。 |
| `world-settings.default.gameplay.fix-tripwire-state-inconsistency` | 修复绊线状态不一致 | bool | `true`（`true`/`false`） | 🔄 | 修复绊线状态不一致。`false` 会启用**线复制漏洞**，并允许末地黑曜石平台抑制。 |
| `world-settings.default.gameplay.safe-teleportation` | 安全传送 | bool | `true`（`true`/`false`） | 🔄 | `true` = 末地传送门只传送活着的实体（修复刷沙）；`false` = 允许末地传送门传送已移除的实体（**刷沙前置**）。要开启刷沙必须设为 `false`。 |
| `world-settings.default.gameplay.sand-duplication` | 沙子复制 | bool | `false`（`true`/`false`） | 🔄 | 允许沙子复制漏洞。⚠️ **前置条件**：必须同时将 `safe-teleportation` 设为 `false` 才能生效。无政府刷沙服开启。 |
| `world-settings.default.gameplay.teleport-async-on-high-velocity` | 高速时异步传送 | bool | `false`（`true`/`false`） | 🔄 | 玩家高速移动（高速度）时使用异步传送。实验性，可能改善高速场景下的传送稳定性。 |

---

## 配置示例（kaiiju.yml 完整默认值）

```yaml
# ===== 全局配置 =====
region-format:
  linear:
    flush-frequency: 10
    flush-max-threads: 1
network:
  send-null-entity-packets: true
  alternate-keepalive: false
  kick-player-on-bad-packet: true
optimization:
  disable-vanish-api: false
  disable-player-stats: false
  disable-arm-swing-event: false
  async-path-processing:
    enable: false          # ⚠️ 改后必须重启
    max-threads: 0          # 0 = 自动 (核心数/4)
    keepalive: 60
    queue-capacity: 4096
gameplay:
  server-mod-name: Kaiiju
  shared-random-for-players: true   # RNG 操纵关键
unsupported:
  disable-ensure-tick-thread-checks: false   # 绝对不要开
  global-event-synchronization: false

# ===== 每世界配置 =====
world-settings:
  default:
    region-format:
      format: ANVIL                    # 切 LINEAR 前必须转换数据
      linear:
        compression-level: 1
        crash-on-broken-symlink: true
    optimization:
      shulker-box-drop-contents-when-destroyed: true
      optimize-hoppers: true
      tick-when-empty: true
      enable-entity-throttling: false
      disable-achievements: false
      disable-creatures-spawn-events: false
      disable-dolphin-swim-to-treasure: false
    gameplay:
      fix-void-trading: true
      break-redstone-on-top-of-trap-doors-early: true
      fix-tripwire-state-inconsistency: true
      safe-teleportation: true
      sand-duplication: false          # 需配合 safe-teleportation: false
      teleport-async-on-high-velocity: false
```

---

## 优化建议（针对无政府 / 原版服）

1. **启用 Linear 格式**：将 `world-settings.default.region-format.format` 改为 `LINEAR` 并用工具转换数据，主世界 / 下界省 ~50% 磁盘，末地省 ~95%。生产环境务必先备份。
2. **异步寻路**：实体多的服开启 `optimization.async-path-processing.enable: true`，`max-threads` 设 `0`（自动）即可，必须重启生效。
3. **关闭不必要事件**：无隐身 / 统计 / 挥手事件需求时，将 `disable-vanish-api`、`disable-player-stats`、`disable-arm-swing-event` 设为 `true`。
4. **备用心跳**：玩家经常因网络抖动被误踢时，开启 `network.alternate-keepalive: true`。
5. **RNG 操纵**：保持 `gameplay.shared-random-for-players: true`，这是原版 RNG 控制的基石。
6. **刷沙**：要启用刷沙，必须**同时**将 `safe-teleportation: false` 与 `sand-duplication: true`，缺一不可。
7. **生电服漏斗**：若红石机器依赖原版漏斗行为，设 `optimize-hoppers: false` 还原原版（会损失性能）。
8. **绝不触碰 unsupported**：`unsupported` 节两项仅用于调试，生产环境务必保持 `false`，否则会数据损坏。
9. **配合 Folia 调优**：Kaiiju 继承 Folia，务必同时按 Folia 手册配置 `paper-global.yml` 的 `threaded-regions` 等多线程项。

> 参考来源：KaiijuMC/Kaiiju [README.md](https://github.com/KaiijuMC/Kaiiju/blob/ver/1.20.1/README.md)（ver/1.20.1 分支）、[Configuration Wiki](https://github.com/KaiijuMC/Kaiiju/wiki/Configuration)。
