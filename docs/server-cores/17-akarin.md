# Akarin 服务器配置文件中文手册

> ⚠️ 此核心已停止维护（Akarin-project 团队于 2021 年停止开发，仓库处于 Public archive 状态），官方推荐迁移至 [Purpur](./06-purpur.md) 或 [Folia](./05-folia.md)。本文档仍完整提供翻译供存量服参考。
>
> Akarin 是基于 **Paper** 的多线程优化分支，最大特色是引入「物理多线程」（Multi-threaded physics）能力，把区块 ticking 与实体 ticking 分摊到多个核心，可显著提升单世界大量实体场景的 TPS。
> 继承关系：Vanilla → Spigot → Paper → **Akarin**
> 官方 GitHub：https://github.com/Akarin-project/Akarin
> 适用版本基准：Akarin 1.12.2 / 1.15.2（最终归档版本）

Akarin 完整继承 Paper / Spigot / Bukkit 的配置体系，并新增独立的 `akarin.yml` 配置文件。本文档仅聚焦 Akarin 独有的 `akarin.yml`。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（实体激活范围等） |
| paper.yml | YAML | Paper 继承 | Paper 配置（1.12/1.15 时代为单一 paper.yml） |
| **akarin.yml** | YAML | **Akarin 专属** | **Akarin 全部独有配置（本文档重点）：多线程与杂项优化** |

> 说明：Akarin 完整继承 Paper / Spigot / Bukkit 全部配置体系，本文档仅聚焦 Akarin 独有的 `akarin.yml`。

## akarin.yml 整体结构

```yaml
config-version: 1                # 配置版本号（内部用，勿手改
settings:                         # 全局设置
  brand-name: "Akarin"
  enable-multi-thread: true
  threads: 0
world-settings:                   # 每世界设置
  default:
    physics: { ... }            # 物理多线程
    optimizations: { ... }      # 优化
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `settings.enable-multi-thread`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载。
- 由于 Akarin 已归档且仅支持到 1.15.2，**新建服建议直接使用 [Folia](./05-folia.md)（真正的多线程 Paper fork）**，Akarin 仅供存量 1.12/1.15 服过渡。

---

## 1. 信息块

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `config-version` | 配置版本号 | int | `1`（—） | ✅ | 内部使用，**不要手动修改**。Akarin 用它做配置自动升级与兼容性判断。 |

---

## 2. settings（全局设置）

Akarin 的核心：物理多线程开关与线程数。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.brand-name` | 服务端品牌名 | string | `Akarin`（任意文本） | 🔄 | 发送给客户端的服务端品牌名（F3 界面 "Mod" 字段）。可用 § 颜色码，可隐藏真实核心类型 |
| `settings.enable-multi-thread` | 启用多线程物理 | bool | `true`（`true`/`false`） | ✅ | Akarin 招牌开关。true=启用物理多线程，把区块 ticking 分摊到多核；false=退化为单线程 Paper。⚠️ 关闭后 Akarin 与普通 Paper 无差异，建议保持 true |
| `settings.threads` | 物理线程数 | int | `0`（≥0） | ✅ | 物理多线程使用的线程数。0=自动（按 CPU 核心数估算）；正值=固定值。建议 ≤ 物理核心数，避免线程切换开销 |

---

## 3. world-settings.default.physics（每世界：物理多线程）

控制每个世界的物理多线程具体行为。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.physics.async-block-physics` | 异步方块物理 | bool | `true`（`true`/`false`） | ✅ | 是否异步处理方块物理（沙子掉落、水流等）。true=移出主线程，可省 TPS；false=原版同步。⚠️ 异步可能与某些依赖物理事件的插件冲突 |
| `world-settings.default.physics.async-entity-physics` | 异步实体物理 | bool | `true`（`true`/`false`） | ✅ | 是否异步处理实体物理（实体移动、碰撞等）。true=多线程处理大量实体；false=原版同步。⚠️ 异步实体可能影响反作弊判定 |
| `world-settings.default.physics.max-async-tasks` | 最大异步任务数 | int | `4`（≥1） | ✅ | 异步物理任务队列的最大长度。值越大吞吐越高但延迟上升；值小延迟低但可能堆积任务。建议 2-8 |

---

## 4. world-settings.default.optimizations（每世界：优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.optimizations.disable-piston-physics` | 禁用活塞物理 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用活塞推拉方块时的物理计算。true=活塞推方块不再触发物理（极省 CPU 但破坏红石机器）；false=原版行为 |
| `world-settings.default.optimizations.fast-leaf-decay` | 快速叶子衰减 | bool | `false`（`true`/`false`） | 🔄 | 是否使用更快的叶子衰减算法。true=省 CPU 但可能与原版叶子农场产量略有差异；false=原版精确计算 |

---

## 配置示例

```yaml
# Akarin 推荐配置（生存服，平衡多线程与兼容性）
config-version: 1

settings:
  brand-name: "Akarin"
  enable-multi-thread: true       # Akarin 灵魂，保持开
  threads: 0                      # 自动按核心数估算

world-settings:
  default:
    physics:
      async-block-physics: true   # 异步方块物理，省 TPS
      async-entity-physics: true  # 大量实体场景效果显著
      max-async-tasks: 4          # 8 核服务器可调到 6-8
    optimizations:
      disable-piston-physics: false  # 不要破坏红石
      fast-leaf-decay: false
```

## 优化建议

1. **优先迁移 Folia**：Akarin 仅支持 1.15.2 及更早，Folia 是真正的多线程 Paper fork 且持续维护，是 Akarin 的精神继承者。
2. **enable-multi-thread 必须开**：关闭等于直接退化为普通 Paper，浪费 Akarin 的存在意义。
3. **threads 设 0 让自动估算**：手动设大于物理核心数反而因线程切换损失性能。8 核服务器建议 6（留 2 给系统与 Netty）。
4. **async-block-physics 与红石机器冲突**：依赖 BlockPhysicsEvent 的红石插件（如部分反作弊、生电辅助）可能失效，建议先在测试服验证。
5. **async-entity-physics 影响反作弊**：异步实体可能让反作弊无法实时判定实体异常移动，PvP 服慎开。
6. **max-async-tasks 与核心数匹配**：通常物理核心数 / 2 是合理值。过低任务堆积 TPS 下掉；过高增加调度延迟。
7. **disable-piston-physics 仅纯生存服**：开启后所有活塞红石机器（自动农场、刷物机）失效，仅适合不依赖红石的服务器。
8. **保留 akarin.yml 备份**：迁移到 Folia 时多线程模型完全不同（Folia 是按区域分线程，Akarin 是按物理类型分线程），配置不可直接平移，需重新设计。
9. **1.12.2 PvP 服推荐保留**：Akarin 1.12.2 在老 PvP 服生态成熟，迁移到 Folia 反而风险更高，可继续使用至服务器停运。
