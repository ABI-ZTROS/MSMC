# Tuinity 服务器配置文件中文手册

> ⚠️ 此核心已停止维护（Tuinity 的官方补丁已全部合并入上游 Paper，独立分支由社区 fork 维护），官方推荐直接使用 [Paper](https://papermc.io/) 或 [Purpur](./06-purpur.md)。本文档仍完整提供翻译供存量服参考。
>
> Tuinity 是基于 **Paper** 的高性能优化分支，由 Spottedleaf 开发，专注单线程 TPS 优化与区块加载性能，是 Yatopia / Airplane / Pufferfish 等后续分支的祖源之一。
> 继承关系：Vanilla → Spigot → Paper → **Tuinity**
> 官方 GitHub（社区 fork）：https://github.com/StarWishsama/Tuinity
> 适用版本基准：Tuinity 1.17.1 / 1.18.2（最终上游版本，社区 fork 可达 1.20+）

Tuinity 完整继承 Paper / Spigot / Bukkit 的配置体系，并新增独立的 `tuinity.yml` 配置文件。本文档仅聚焦 Tuinity 独有的 `tuinity.yml`。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（实体激活范围等） |
| paper.yml / paper-global.yml | YAML | Paper 继承 | Paper 配置 |
| **tuinity.yml** | YAML | **Tuinity 专属** | **Tuinity 全部独有配置（本文档重点）** |

> 说明：Tuinity 完整继承 Paper / Spigot / Bukkit 全部配置体系，本文档仅聚焦 Tuinity 独有的 `tuinity.yml`。

## tuinity.yml 整体结构

```yaml
config-version: 1                # 配置版本号（内部用，勿手改
world-settings:                   # 每世界设置
  default:
    chunks: { ... }              # 区块加载
    tick-rates: { ... }          # tick 频率
    fixes: { ... }               # 修复
    misc: { ... }                # 杂项优化
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `world-settings.default.chunks.chunk-gc`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载。
- 由于 Tuinity 上游已停更，**新建服建议直接使用 [Purpur](./06-purpur.md) 或最新 Paper**，Tuinity 仅供存量服过渡。

---

## 1. 信息块

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `config-version` | 配置版本号 | int | `1`（—） | ✅ | 内部使用，**不要手动修改**。Tuinity 用它做配置自动升级与兼容性判断。 |

---

## 2. world-settings.default.chunks（每世界：区块加载）

Tuinity 的核心优化方向之一。控制区块的回收、加载与并发策略。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.chunks.chunk-gc` | 区块垃圾回收间隔 | int | `600`（≥0，单位：tick） | 🔄 | 多久回收一次无人观察的区块（tick）。600 = 30 秒。调小可更快释放内存；调大减少 IO 但内存占用高 |
| `world-settings.default.chunks.delay-chunk-unloads-by` | 延迟区块卸载 | int | `0`（≥0，单位：tick） | 🔄 | 玩家离开后多久才真正卸载区块（tick）。正值=延迟卸载，玩家短时间往返不重复加载；0=立即卸载 |
| `world-settings.default.chunks.entity-activation-range-strict-mode` | 实体激活严格模式 | bool | `false`（`true`/`false`） | 🔄 | 是否严格按 Spigot 的实体激活范围判定。true=原版行为；false=使用 Tuinity 优化后的更宽松判定，可省 CPU |

---

## 3. world-settings.default.tick-rates（每世界：tick 频率）

控制各类游戏元素的 tick 频率，是 Tuinity 的招牌功能。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.tick-rates.mob-spawner` | 刷怪笼 tick 频率 | int | `1`（≥1） | 🔄 | 刷怪笼每多少 tick 触发一次生成判定。1=原版；2=减半（适合大量刷怪笼的服，可大幅省 CPU） |
| `world-settings.default.tick-rates.sensors.behavior` | 行为传感器 tick 频率 | int | `1`（≥1） | 🔄 | 村民 / 生物 AI 行为传感器（如最近村民、最近玩家）的 tick 频率。调大可降低村民密集场景的 CPU |
| `world-settings.default.tick-rates.grass-tick` | 草生长 tick 频率 | int | `1`（≥1） | 🔄 | 草方块蔓延生长的 tick 频率。调大可省 CPU 但草生长变慢，影响自动农场产量 |

---

## 4. world-settings.default.fixes（每世界：修复）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.fixes.fix-item-merge` | 修复物品合并 | bool | `true`（`true`/`false`） | 🔄 | 是否修复多个相同物品无法合并的漏洞。true=修复（推荐）；false=原版行为，可能导致掉落物丢失或重复 |
| `world-settings.default.fixes.prevent-moving-into-unloaded-chunks` | 防止进入未加载区块 | bool | `true`（`true`/`false`） | 🔄 | 是否阻止玩家通过卡墙 / 加速进入未加载区块。true=阻止（防止穿墙与崩溃）；false=原版行为 |

---

## 5. world-settings.default.misc（每世界：杂项优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.misc.use-optimized-light` | 使用优化光照 | bool | `true`（`true`/`false`） | ✅ | 是否使用 Tuinity 优化的光照计算引擎。true=光照计算更快、内存更省；false=原版光照（仅排查光照 bug 时关） |
| `world-settings.default.misc.redstone-implementation` | 红石实现 | enum | `VANILLA`（`VANILLA`/`ALTERNATE`） | ✅ | 红石更新算法选择。VANILLA=原版（生电兼容）；ALTERNATE=Tuinity 替代实现（更快但可能与生电机器冲突） |

---

## 配置示例

```yaml
# Tuinity 推荐配置（生存服，平衡性能与玩法）
config-version: 1

world-settings:
  default:
    chunks:
      chunk-gc: 600
      delay-chunk-unloads-by: 200   # 10 秒延迟，玩家短往返不重复加载
      entity-activation-range-strict-mode: false
    tick-rates:
      mob-spawner: 1                # 默认即可，原版刷怪塔产量不变
      sensors.behavior: 2           # 村民密集场景省 CPU
      grass-tick: 1
    fixes:
      fix-item-merge: true
      prevent-moving-into-unloaded-chunks: true
    misc:
      use-optimized-light: true
      redstone-implementation: VANILLA  # 生电服务必 VANILLA
```

## 优化建议

1. **优先迁移 Purpur**：Purpur 已合并 Tuinity 全部补丁并持续维护，社区迁移路径成熟。
2. **use-optimized-light 默认开**：Tuinity 光照优化对大型建筑 / 复杂地形效果显著，对玩法无影响。
3. **redstone-implementation 生电服务必 VANILLA**：ALTERNATE 实现会破坏大量红石机器（如卡服机、刷物机），生电服绝不可改。
4. **delay-chunk-unloads-by 适度调大**：玩家短时间跨服往返时此项可避免反复加载卸载，但过大占用内存。200-600 tick 是常用值。
5. **mob-spawner 调整需慎重**：值 >1 会降低刷怪塔产量，需告知玩家；测试服可设 2-4 加速实验。
6. **sensors.behavior 影响村民**：调大可省 CPU 但村民交互（交易、繁殖）会变迟钝，纯装饰村民服可大胆调大。
7. **prevent-moving-into-unloaded-chunks 务必开**：可防止玩家通过卡墙进入未加载区块引发崩溃或穿墙。
8. **chunk-gc 别调太小**：<200 会频繁触发卸载，反而增加 IO；公开服 600-1200 是合理区间。
