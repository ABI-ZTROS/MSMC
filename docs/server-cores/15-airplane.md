# Airplane 服务器配置文件中文手册

> ⚠️ 此核心已停止维护（TECHNOVE 团队于 2022 年宣布 Airplane 进入维护停止状态，核心补丁已合并入 Paper / Pufferfish），官方推荐迁移至 [Pufferfish](./07-pufferfish.md) 或 [Purpur](./06-purpur.md)。本文档仍完整提供翻译供存量服参考。
>
> Airplane 是基于 **Paper** 的优化分支，专注于降低高负载服务器的 CPU 与内存占用，主要补丁来自 Tuinity 与社区贡献。
> 继承关系：Vanilla → Spigot → Paper → **Airplane**
> 官方 GitHub：https://github.com/TECHNOVE/Airplane
> 适用版本基准：Airplane 1.17.1 / 1.18.2（最终停更版本）

Airplane 完整继承 Paper / Spigot / Bukkit 的配置体系（paper.yml / paper-global.yml / paper-world.yml 等仍可用），并新增独立的 `airplane.yml` 配置文件。本文档仅聚焦 Airplane 独有的 `airplane.yml`。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（实体激活范围等） |
| paper.yml / paper-global.yml | YAML | Paper 继承 | Paper 配置 |
| **airplane.yml** | YAML | **Airplane 专属** | **Airplane 全部独有配置（本文档重点）** |

> 说明：Airplane 完整继承 Paper / Spigot / Bukkit 全部配置体系，本文档仅聚焦 Airplane 独有的 `airplane.yml`。

## airplane.yml 整体结构

```yaml
config-version: 1                # 配置版本号（内部用，勿手改
airplane:                         # 全局优化
  brand-name: "Airplane"
  allow-unsafe-commands: false
world-settings:                   # 每世界设置
  default:
    chunks: { ... }              # 区块优化
    entities: { ... }            # 实体优化
    fixes: { ... }               # 修复
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `airplane.brand-name`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载。
- 由于 Airplane 已停更，**新建服建议直接使用 [Pufferfish](./07-pufferfish.md) / [Purpur](./06-purpur.md)**，Airplane 仅供存量服过渡。

---

## 1. 信息块

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `config-version` | 配置版本号 | int | `1`（—） | ✅ | 内部使用，**不要手动修改**。Airplane 用它做配置自动升级与兼容性判断。 |

---

## 2. airplane（全局优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `airplane.brand-name` | 服务端品牌名 | string | `Airplane`（任意文本） | 🔄 | 发送给客户端的服务端品牌名（F3 界面 "Mod" 字段）。可用 § 颜色码，可隐藏真实核心类型 |
| `airplane.allow-unsafe-commands` | 允许不安全命令 | bool | `false`（`true`/`false`） | 🔄 | 是否允许执行可能引发性能问题或不安全的内置调试命令。true=允许（仅适合开发 / 测试服）；false=禁用（生产服保持） |

---

## 3. world-settings.default.chunks（每世界：区块优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.chunks.chunk-load-cooldown` | 区块加载冷却 | int | `0`（≥0，单位：tick） | 🔄 | 玩家触发区块加载后再次允许加载的间隔（tick）。0=无冷却；正值=降低区块加载频率，可缓解突发加载导致的卡顿 |
| `world-settings.default.chunks.autosave-period` | 自动保存周期 | int | `6000`（≥0，单位：tick） | 🔄 | 自动保存世界数据的间隔（tick）。6000 = 5 分钟。调大省 IO 但崩服丢数据更多；调小反之 |
| `world-settings.default.chunks.max-chunk-sends-per-tick` | 每 tick 最大区块发送数 | int | `0`（≥0） | 🔄 | 每 tick 向玩家发送的最大区块包数。0=不限制；正值=限速，可避免进服时网络尖峰 |

---

## 4. world-settings.default.entities（每世界：实体优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.entities.spawn-packet-queue` | 生成包排队 | bool | `true`（`true`/`false`） | 🔄 | 是否把实体生成数据包排队发送。true=平滑网络峰值，避免一次性发送大量实体导致客户端卡顿；false=原版行为 |
| `world-settings.default.entities.dab.enabled` | 启用 DAB 实体激活 | bool | `true`（`true`/`false`） | ✅ | 是否启用 Airplane 改进的「动态实体激活」（DAB）。true=远离玩家的实体降低 tick 频率以省 CPU；false=原版固定激活范围 |

---

## 5. world-settings.default.fixes（每世界：修复）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.fixes.fix-coordinate-exploit` | 修复坐标泄露漏洞 | bool | `true`（`true`/`false`） | 🔄 | 是否修复通过传送包反推远处坐标的漏洞。true=修复（推荐）；false=允许玩家通过特定客户端作弊获取远距离方块位置 |
| `world-settings.default.fixes.prevent-double-pistons` | 防止双活塞卡服 | bool | `true`（`true`/`false`） | 🔄 | 是否防止双活塞同时激活导致的卡服机器。true=防止（推荐）；false=原版行为，可能被用于恶意卡服 |

---

## 配置示例

```yaml
# Airplane 推荐配置（生存服，平衡性能与玩法）
config-version: 1

airplane:
  brand-name: "Airplane"
  allow-unsafe-commands: false

world-settings:
  default:
    chunks:
      chunk-load-cooldown: 0      # 默认即可
      autosave-period: 6000
      max-chunk-sends-per-tick: 0
    entities:
      spawn-packet-queue: true
      dab:
        enabled: true             # DAB 是 Airplane 亮点，保持开
    fixes:
      fix-coordinate-exploit: true
      prevent-double-pistons: true
```

## 优化建议

1. **优先迁移 Pufferfish**：Pufferfish 已吸收 Airplane 全部补丁并持续维护，配置兼容性极高。
2. **DAB 默认开**：动态实体激活是 Airplane 最大亮点，对大型生存服 / 大量实体场景效果显著，关闭等于自废武功。
3. **spawn-packet-queue 几乎无损**：仅平滑网络峰值，对玩法无影响，建议保持开启。
4. **max-chunk-sends-per-tick 慎调**：调小可缓解进服卡顿，但玩家进服速度会变慢；0 即不限制最稳妥。
5. **allow-unsafe-commands 仅测试服开**：生产服开启会让恶意 OP 命令更容易触发 OOM / 卡服。
6. **fix-coordinate-exploit 务必开**：可防止外挂玩家探测远处建筑基地，对 PvP / 防窥服特别重要。
7. **保留 airplane.yml 备份**：迁移到 Pufferfish 时配置键名基本兼容，但仍建议备份以便对比。
