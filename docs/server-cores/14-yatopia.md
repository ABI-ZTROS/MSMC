# Yatopia 服务器配置文件中文手册

> ⚠️ 此核心已停止维护（YatopiaMC 团队于 2022 年宣布停止开发），官方推荐迁移至 [Purpur](./06-purpur.md) 或 [Folia](./05-folia.md)。本文档仍完整提供翻译供存量服参考。
>
> Yatopia 是基于 **Tuinity** 的极限优化分支，曾以「合并所有知名优化补丁」闻名，目标是把 Paper / Tuinity / Airplane / Purpur 等分支的优化全部聚合到一个核心。
> 继承关系：Vanilla → Spigot → Paper → Tuinity → **Yatopia**
> 官方 GitHub：https://github.com/YatopiaMC/Yatopia
> 适用版本基准：Yatopia 1.17.1 / 1.18.2（最终停更版本）

Yatopia 完整继承 Tuinity / Paper / Spigot / Bukkit 的配置体系（paper-global.yml / paper-world.yml / tuinity.yml 等仍可用），并新增独立的 `yatopia.yml` 配置文件。本文档仅聚焦 Yatopia 独有的 `yatopia.yml`。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（实体激活范围等） |
| paper.yml | YAML | Paper 继承 | Paper 配置（旧版为单一 paper.yml） |
| tuinity.yml | YAML | Tuinity 继承 | Tuinity 配置（区块加载优化等） |
| **yatopia.yml** | YAML | **Yatopia 专属** | **Yatopia 全部独有配置（本文档重点）** |

> 说明：Yatopia 完整继承 Tuinity / Paper / Spigot / Bukkit 全部配置体系，本文档仅聚焦 Yatopia 独有的 `yatopia.yml`。

## yatopia.yml 整体结构

```yaml
config-version: 1                # 配置版本号（内部用，勿手改
settings:                         # 全局设置
  brand-name: "Yatopia"
  disable-connection-messages: false
  use-player-luck-perms: false
  fix-bridging: true
world-settings:                   # 每世界设置
  default:
    entities: { ... }            # 实体优化
    ticks: { ... }               # tick 优化
    fixes: { ... }               # 漏洞修复
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `settings.brand-name`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载（`/restart` 或重载插件）。
- 由于 Yatopia 已停更，**新建服建议直接使用 [Purpur](./06-purpur.md) / [Folia](./05-folia.md)**，Yatopia 仅供存量服过渡使用。

---

## 1. 信息块

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `config-version` | 配置版本号 | int | `1`（—） | ✅ | 内部使用，**不要手动修改**。Yatopia 用它做配置自动升级与兼容性判断。 |

---

## 2. settings（全局设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.brand-name` | 服务端品牌名 | string | `Yatopia`（任意文本） | 🔄 | 发送给客户端的服务端品牌名（F3 界面 "Mod" 字段）。可用 § 颜色码，可隐藏真实核心类型 |
| `settings.disable-connection-messages` | 禁用连接消息 | bool | `false`（`true`/`false`） | 🔄 | 是否关闭玩家加入 / 退出的全服广播。true=不再显示「XXX joined the game」类消息；false=原版行为 |
| `settings.use-player-luck-perms` | 使用 LuckPerms 玩家缓存 | bool | `false`（`true`/`false`） | ✅ | 是否直接读取 LuckPerms 玩家对象缓存（绕过 Bukkit API）。true=权限查询更快；false=走标准 API，兼容性更好 |
| `settings.fix-bridging` | 修复速桥 | bool | `true`（`true`/`false`） | 🔄 | 是否修复速桥（Bridging）时方块放置位置异常。true=修复；false=还原原版时序，部分玩家可能更顺手 |

---

## 3. world-settings.default.entities（每世界：实体优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.entities.disable-skeleton-ai` | 禁用骷髅 AI | bool | `false`（`true`/`false`） | 🔄 | true=骷髅不再主动寻路 / 射箭，只保持原地待机。可显著降低骷髅密集场景的 CPU 占用，但破坏玩法 |
| `world-settings.default.entities.disable-zombie-ai` | 禁用僵尸 AI | bool | `false`（`true`/`false`） | 🔄 | true=僵尸不再主动追击玩家 / 拆门。同上，仅适合刷怪塔或测试服 |
| `world-settings.default.entities.fast-velocity-calc` | 快速速度计算 | bool | `true`（`true`/`false`） | 🔄 | 是否使用更快的实体速度计算算法。true=省 CPU，可能与原版物理略有差异；false=原版精确计算 |

---

## 4. world-settings.default.ticks（每世界：tick 优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.ticks.disable-tick-scheduler` | 禁用 tick 调度器 | bool | `false`（`true`/`false`） | ✅ | 是否禁用原版 tick 调度器改用简化实现。true=省 CPU 但部分依赖调度的红石机器可能失效；false=原版调度 |
| `world-settings.default.ticks.optimize-hopper` | 漏斗优化 | bool | `true`（`true`/`false`） | 🔄 | 启用 Paper 的漏斗优化。false 可还原 100% 原版漏斗行为，但会破坏大量生电红石机器。生电服可考虑 false |

---

## 5. world-settings.default.fixes（每世界：漏洞修复）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world-settings.default.fixes.fix-player-z-fighting` | 修复玩家 Z 闪烁 | bool | `true`（`true`/`false`） | 🔄 | 是否修复玩家在低 Y 高速移动时的 Z 轴闪烁问题。true=修复（推荐）；false=原版行为 |
| `world-settings.default.fixes.disable-void-fishing` | 禁用虚空钓鱼 | bool | `false`（`true`/`false`） | 🔄 | 是否禁用虚空钓鱼漏洞。true=禁用（钓鱼浮标在虚空时不再生效）；false=原版行为 |

---

## 配置示例

```yaml
# Yatopia 推荐配置（生存服，平衡性能与玩法）
config-version: 1

settings:
  brand-name: "Yatopia"           # 或自定义隐藏核心
  disable-connection-messages: false
  use-player-luck-perms: false    # 装了 LuckPerms 才开
  fix-bridging: true

world-settings:
  default:
    entities:
      disable-skeleton-ai: false  # 不要破坏玩法
      disable-zombie-ai: false
      fast-velocity-calc: true    # 安全优化
    ticks:
      disable-tick-scheduler: false
      optimize-hopper: true
    fixes:
      fix-player-z-fighting: true
      disable-void-fishing: true  # 防滥用
```

## 优化建议

1. **优先迁移 Purpur**：Yatopia 已停更，1.18+ 协议适配缺失，Purpur 已合并 Yatopia 大部分补丁且持续维护。
2. **disable-*-ai 慎用**：禁用怪物 AI 虽然省 CPU，但刷怪塔、防僵尸拆门等玩法会全部失效，仅适合测试服或纯方块服。
3. **use-player-luck-perms 仅在装了 LP 时开启**：未安装 LuckPerms 时开启会报错或导致权限查询失败。
4. **fix-bridging 默认开**：修复速桥问题对绝大多数玩家更友好，只有特定 PvP 服可能需要关闭。
5. **fast-velocity-calc 影响极小**：实测对玩家几乎无感知，可放心开启省 CPU。
6. **保留 yatopia.yml 备份**：迁移到 Purpur 时，Purpur 已吸收这些补丁，但配置键名格式可能不同，迁移前务必备份。
7. **不要混用多个分支补丁**：Yatopia 已经聚合了大量补丁，再额外安装优化插件（如 ClearLag）容易冲突。
