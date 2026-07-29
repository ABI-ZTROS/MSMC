# Magma 服务器配置文件中文手册

> Magma 是基于 Forge + Paper 的开源混合服务端，兼容 CraftBukkit/Spigot/Paper 插件与 Forge 模组。
> 继承关系：Vanilla → Forge + Paper → Magma
> 官方 GitHub：https://github.com/magmamaintainers/Magma（活跃维护版）
> 原始仓库：https://github.com/magmafoundation/Magma
> 官方网站：https://magmafoundation.org/

Magma 由 Magma Foundation 开发，定位与 Mohist 类似，是 Forge + Paper 混合端。Magma 历史上有 1.12.2 和 1.18.2 两个长期支持版本。其配置文件 `magma.yml` **实际采用 Properties 格式**（虽然扩展名为 `.yml`），由 `org.magmafoundation.magma.config.MagmaConfig` 加载。Magma 的配置项数量较多（约 40 项），覆盖了性能优化、兼容性、刷怪、区块、网络等多个方面。注意：由于 Properties 格式是扁平的，所有键使用点号分隔的扁平路径（如 `magma.entity-activation-range.animals`），不存在真正的嵌套结构。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| paper.yml / paper-global.yml | YAML | Paper 继承 | Paper 全局/世界配置 |
| forge.cfg / fml.toml | TOML/CFG | Forge 继承 | Forge 模组加载器配置 |
| magma.yml | Properties | Magma 专属 | Magma 独有核心配置（本文档重点，注意是 Properties 格式） |

> 说明：Magma 完整继承 Forge 与 Paper 的全部配置体系，本文档仅聚焦 Magma 独有的 `magma.yml`。其余配置请参阅对应的 Forge / Paper / Spigot / Bukkit 手册。

## magma.yml（Magma 专属配置）

`magma.yml` 位于服务器根目录，**虽然扩展名是 `.yml`，但实际是 Properties 格式（key=value）**，由 `org.magmafoundation.magma.config.MagmaConfig` 加载。所有键使用点号分隔的扁平路径，不存在真正的 YAML 嵌套。所有配置在服务器启动时读取，多数项需重启生效。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `magma.check-update`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `string[]` 字符串列表。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示部分支持热重载。
- **格式注意**：Properties 文件中布尔值写作 `true` / `false`，字符串列表通常用逗号分隔。

---

### 1. 通用设置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `magma.check-update` | 检查 Magma 更新 | bool | `true`（`true` / `false`） | ✅ | 启动时是否联网检查 Magma 新版本。 |
| `magma.bukkit-version` | Bukkit API 版本 | string | 自动检测（如 `1.18.2-R0.1-SNAPSHOT`） | ✅ | Magma 内部使用的 Bukkit API 版本号，由 Magma 自动写入，请勿手动修改。 |
| `magma.disable-logger` | 禁用部分日志 | bool | `false`（`true` / `false`） | ✅ | 是否禁用 Magma 自身的部分调试日志（如启动日志）。减少日志噪音。 |
| `magma.disable-sentry` | 禁用 Sentry 错误上报 | bool | `false`（`true` / `false`） | ✅ | 是否禁用 Sentry 错误自动上报。Magma 默认会上报崩溃信息到 Sentry 帮助开发。 |
| `magma.remove-blank-line` | 移除日志空行 | bool | `true`（`true` / `false`） | ✅ | 是否移除日志中的多余空行，让日志更紧凑。 |
| `magma.remove-errormods` | 移除报错模组日志 | bool | `false`（`true` / `false`） | ✅ | 是否在启动失败时移除报错模组的详细日志（仅显示摘要）。 |

---

### 2. 性能优化（实体）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `magma.use-multi-thread-entity-tick` | 多线程实体 tick | bool | `false`（`true` / `false`） | ✅ | 实验性：是否使用多线程处理实体 tick。⚠️ 与绝大多数 Forge 模组冲突，**强烈不建议开启**。 |
| `magma.max-entity-ticks-per-tick` | 单 tick 实体上限 | int | `-1`（≥ -1，-1 = 不限制） | 🔄 | 单次 tick 最多处理的实体数量。-1 不限制。模组较多的服务器可设上限防止实体爆炸卡服。 |
| `magma.entity-tick-limit` | 实体 tick 限制 | int | `-1`（≥ -1） | 🔄 | 类似 `max-entity-ticks-per-tick`，限制实体 tick 总数。 |
| `magma.enable-real-ticking-entities` | 真实 tick 实体 | bool | `false`（`true` / `false`） | ✅ | 是否对所有实体保持真实 tick（原版行为）。关闭可省性能，但部分模组机器/农场可能失效。 |
| `magma.tick-skip` | 跳过远实体 tick | bool | `false`（`true` / `false`） | ✅ | 是否跳过远离玩家实体的 tick。开启可省 CPU 但破坏部分模组刷怪塔。 |
| `magma.entity-activation-range` | 实体激活范围总开关 | bool | `true`（`true` / `false`） | ✅ | 是否启用实体激活范围机制（远离玩家的实体降低 tick 频率）。 |

---

### 3. 性能优化（区块与异步）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `magma.enable-asynchronous-chunk` | 异步区块加载 | bool | `true`（`true` / `false`） | ✅ | 是否启用异步区块加载/生成。开启可显著减少主线程卡顿，提升玩家飞行/传送流畅度。 |
| `magma.async-pathfinding` | 异步寻路 | bool | `false`（`true` / `false`） | ✅ | 将生物寻路计算转移到异步线程。⚠️ 部分模组可能与异步寻路冲突。 |
| `magma.async-mob-spawning` | 异步生物生成 | bool | `false`（`true` / `false`） | ✅ | 将生物生成计算转移到异步线程。⚠️ 与 Forge 模组的事件监听可能冲突。 |
| `magma.use-async-thread` | 启用异步线程 | bool | `true`（`true` / `false`） | ✅ | 是否启用 Magma 的异步工作线程（用于区块、寻路等）。建议保持 `true`。 |
| `magma.print-chunk` | 打印区块加载信息 | bool | `false`（`true` / `false`） | 🔄 | 是否在日志中打印区块加载/卸载的详细信息。排查区块问题时开启。 |

---

### 4. 性能优化（综合）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `magma.target-tps` | 目标 TPS | int | `20`（≥ 1） | 🔄 | 服务器目标 TPS。一般保持 20（原版）。降低可省 CPU 但游戏变卡。 |
| `magma.max-tick-time` | 单 tick 最大耗时 | int | `60000`（≥ -1，单位：毫秒；-1 = 禁用） | 🔄 | 单个 tick 超过此时间触发 watchdog。-1 禁用看门狗（不推荐）。 |
| `magma.disable-watchdog` | 禁用看门狗 | bool | `false`（`true` / `false`） | ✅ | 是否禁用 watchdog 主线程监控。⚠️ 不推荐，模组卡死将无报警。 |
| `magma.disable-watcher` | 禁用监视器 | bool | `false`（`true` / `false`） | ✅ | 是否禁用文件监视器（监视 mods/、plugins/ 等目录变化）。关闭后无法热检测文件变更。 |
| `magma.optimized-crafting` | 优化合成 | bool | `true`（`true` / `false`） | ✅ | 是否启用合成台合成优化（缓存合成结果）。可提升合成性能。 |
| `magma.fast-rain` | 快速降雨 | bool | `false`（`true` / `false`） | ✅ | 是否优化天气变化（降雨/降雪）的处理逻辑，减少天气切换时的卡顿。 |
| `magma.use-spark` | 启用 Spark 集成 | bool | `true`（`true` / `false`） | ✅ | 是否启用与 Spark 性能分析插件的集成。建议保持 `true`。 |

---

### 5. 兼容性与事件

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `magma.allow-fluid-flow` | 允许流体流动事件 | bool | `true`（`true` / `false`） | 🔄 | 是否允许 Forge 模组的流体流动触发 Bukkit 事件。关闭可省 CPU，但部分物理/红石插件会失效。 |
| `magma.disable-super-vanilla-fallable-block` | 禁用原版下落方块优化 | bool | `false`（`true` / `false`） | 🔄 | 是否禁用 Magma 对原版下落方块（沙子、砂砾）的优化。模组下落方块异常时可尝试开启。 |
| `magma.fix-tile-entity` | 修复方块实体 | bool | `true`（`true` / `false`） | 🔄 | 修复部分 Forge 模组方块实体（TileEntity）与 Bukkit 事件的兼容性。建议保持 `true`。 |
| `magma.disable-flush` | 禁用批量刷新 | bool | `false`（`true` / `false`） | ✅ | 是否禁用网络数据包批量刷新。开启可能减少延迟但增加带宽。 |
| `magma.disable-book-ban` | 禁用书本封禁 | bool | `true`（`true` / `false`） | 🔄 | 是否启用书本封禁保护（防止玩家通过恶意 NBT 书本导致客户端/服务器崩溃）。建议保持 `true`。 |
| `magma.enable-bungee` | 启用 BungeeCord 支持 | bool | `false`（`true` / `false`） | ✅ | 是否启用 BungeeCord/Velocity 跨服代理支持。使用代理服时必须开启，并设置 `bungeecord` 相关项。 |

---

## 配置示例（magma.yml 完整默认值）

```properties
# Magma Configuration
# https://github.com/magmamaintainers/Magma
# 注意：本文件实际是 Properties 格式（key=value），不是真正的 YAML

# ========== 通用设置 ==========
magma.check-update=true
magma.bukkit-version=1.18.2-R0.1-SNAPSHOT
magma.disable-logger=false
magma.disable-sentry=false
magma.remove-blank-line=true
magma.remove-errormods=false

# ========== 性能优化（实体） ==========
magma.use-multi-thread-entity-tick=false
magma.max-entity-ticks-per-tick=-1
magma.entity-tick-limit=-1
magma.enable-real-ticking-entities=false
magma.tick-skip=false
magma.entity-activation-range=true

# ========== 性能优化（区块与异步） ==========
magma.enable-asynchronous-chunk=true
magma.async-pathfinding=false
magma.async-mob-spawning=false
magma.use-async-thread=true
magma.print-chunk=false

# ========== 性能优化（综合） ==========
magma.target-tps=20
magma.max-tick-time=60000
magma.disable-watchdog=false
magma.disable-watcher=false
magma.optimized-crafting=true
magma.fast-rain=false
magma.use-spark=true

# ========== 兼容性与事件 ==========
magma.allow-fluid-flow=true
magma.disable-super-vanilla-fallable-block=false
magma.fix-tile-entity=true
magma.disable-flush=false
magma.disable-book-ban=true
magma.enable-bungee=false
```

## 优化建议（针对 Forge 模组 + Bukkit 插件混合服）

1. **多线程实体 tick**：**绝对不要**开启 `magma.use-multi-thread-entity-tick`，目前与绝大多数 Forge 模组冲突，会引发严重崩溃。
2. **异步区块**：保持 `magma.enable-asynchronous-chunk: true`，可显著提升玩家飞行/传送流畅度。
3. **异步寻路与生成**：`magma.async-pathfinding` 与 `magma.async-mob-spawning` 在模组较多时易冲突，主模组服保持 `false`。
4. **看门狗**：保持 `magma.disable-watchdog: false` 与 `magma.max-tick-time: 60000`，模组卡死时能及时报警。
5. **合成优化**：保持 `magma.optimized-crafting: true`，可提升合成性能。模组合成异常时再关闭。
6. **书本封禁**：保持 `magma.disable-book-ban: true`，防止恶意 NBT 书本攻击。
7. **BungeeCord**：使用代理服时开启 `magma.enable-bungee: true`，并在 `spigot.yml` 中配置 `bungeecord: true`。
8. **JVM 优化**：Magma 推荐 `-Xms4G -Xmx8G -XX:+UseG1GC -XX:+AlwaysPreTouch -XX:+ParallelRefProcEnabled`。
9. **Java 版本**：1.12.2 使用 Java 8；1.18.2 需要 Java 17。
10. **格式注意**：编辑 `magma.yml` 时请使用 `key=value` 的 Properties 语法，不要用 YAML 的 `key: value`，否则配置会失效。

> 参考来源：Magma 官方源码 [`MagmaConfig.java`](https://github.com/magmamaintainers/Magma)、[Magma Foundation 官网](https://magmafoundation.org/)。
