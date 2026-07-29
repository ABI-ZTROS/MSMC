# Banner 服务器配置文件中文手册

> Banner 是 MohistMC 团队开发的 Fabric + Bukkit 混合服务端，让 Fabric 模组与 Bukkit/Spigot/Paper 插件共存。
> 继承关系：Vanilla → Fabric → Banner（含 Bukkit/Spigot/Paper 兼容层）
> 官方 GitHub：https://github.com/MohistMC/Banner
> 官方 Discord：https://discord.gg/mohistmc

> ⚠️ 注意：2025 年 7 月 Banner 已从 MohistMC 项目独立，部分分支更名为 Taiyitist。本手册仍以原始 Banner 项目为准。

Banner 由 MohistMC 团队开发，是少数基于 **Fabric**（而非 Forge）的混合端。与其他 Forge 系混合端（Mohist、Arclight、CatServer、Magma）不同，Banner 让 **Fabric 模组**与 Bukkit/Spigot/Paper 插件共存，适合那些依赖 Fabric 生态模组（如 Lithium、Sodium、Carpet）的服务器。Banner 使用 Mixin 技术实现 Bukkit API 作为 Fabric mod 加载，与 Cardboard 等早期 Fabric 混合端相比有完全不同的架构，稳定性和兼容性更好。配置文件 `banner.yml` 由 `com.mohistmc.banner.config.BannerConfig` 加载。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| paper.yml / paper-global.yml | YAML | Paper 兼容层 | Banner 提供的 Paper 兼容配置 |
| fabric-loader.properties | Properties | Fabric 继承 | Fabric Loader 配置 |
| banner.yml | YAML | Banner 专属 | Banner 独有核心配置（本文档重点） |

> 说明：Banner 完整继承 Fabric 与 Bukkit/Spigot/Paper 的全部配置体系，本文档仅聚焦 Banner 独有的 `banner.yml`。其余配置请参阅对应的 Fabric / Spigot / Bukkit 手册。

## banner.yml（Banner 专属配置）

`banner.yml` 位于服务器根目录，由 `com.mohistmc.banner.config.BannerConfig` 加载。采用标准 YAML 格式，所有配置在服务器启动时读取，多数项需重启生效。Banner 的配置项相对精简，主要聚焦于平台适配、性能优化和 Fabric 模组兼容性。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `banner.lang`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示部分支持热重载。

---

### 1. 通用设置（banner）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `banner.lang` | 控制台语言 | string | `en_US`（`en_US` / `zh_CN` / `fr_FR` / `es_ES` / `de_DE` / `ja_JP` / `ko_KR` / `ru_RU` / `pt_BR` / `zh_TW`） | ✅ | Banner 启动日志与控制台提示所使用的语言。仅影响 Banner 自身日志，不影响 Minecraft 原版日志。 |
| `banner.check_update` | 检查 Banner 更新 | bool | `true`（`true` / `false`） | ✅ | 启动时是否联网检查 Banner 新版本。 |
| `banner.metrics` | 启用 bStats 统计上报 | bool | `true`（`true` / `false`） | ✅ | 是否启用 bStats 匿名数据上报。建议保持开启帮助开发者了解使用情况。 |
| `banner.show_logo` | 启动时显示 Banner Logo | bool | `true`（`true` / `false`） | ✅ | 控制台启动时是否打印 Banner ASCII Logo。 |
| `banner.bukkit-version` | Bukkit API 版本 | string | 自动检测（如 `1.20.1-R0.1-SNAPSHOT`） | ✅ | Banner 内部使用的 Bukkit API 版本号，由 Banner 自动写入，请勿手动修改。 |
| `banner.bukkit-version-override` | 强制覆盖 Bukkit 版本 | string | 空（任意版本字符串） | ✅ | 强制覆盖对插件声明的 Bukkit 版本号。仅在插件因版本检查拒绝加载时使用。 |

---

### 2. 兼容性设置（Fabric 模组 ↔ Bukkit 插件）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `banner.disable_plugins_blacklist` | 禁用插件黑名单检查 | bool | `false`（`true` / `false`） | ✅ | Banner 维护了一份已知与混合端不兼容的插件黑名单。设为 `true` 跳过该检查（不推荐，可能导致崩溃）。 |
| `banner.disable_mods_blacklist` | 禁用模组黑名单检查 | bool | `false`（`true` / `false`） | ✅ | 同上，跳过 Banner 维护的已知不兼容 Fabric 模组黑名单。 |
| `banner.support_non_paper_plugins` | 允许非 Paper 系插件 | bool | `true`（`true` / `false`） | ✅ | 是否允许加载仅声明支持 Spigot/CraftBukkit 的插件。 |

---

### 3. 性能优化（Fabric 风格）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `banner.async-tick` | 异步 tick 模式 | bool | `false`（`true` / `false`） | ✅ | 实验性：是否启用异步 tick 模式。⚠️ 与部分 Fabric 模组（如 Lithium）可能冲突，**强烈不建议开启**。 |
| `banner.disable-watchdog` | 禁用看门狗 | bool | `false`（`true` / `false`） | ✅ | 是否禁用 watchdog 主线程监控。⚠️ 不推荐，模组卡死将无报警。 |
| `banner.entity-activation-range` | 实体激活范围优化 | bool | `true`（`true` / `false`） | ✅ | 是否启用实体激活范围优化（远离玩家的实体降低 tick 频率）。与 Lithium 类似模组可能重复优化，建议二选一。 |
| `banner.use-Spark-and-Sync-Timer` | 启用 Spark 计时器 | bool | `true`（`true` / `false`） | ✅ | 是否启用 Banner 内置的同步计时器（用于性能分析）。Spark 插件依赖此功能。 |

---

### 4. 事件桥接（Fabric ↔ Bukkit 事件转发）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `banner.event-transformation` | 事件类型转换 | bool | `true`（`true` / `false`） | ✅ | 是否启用 Fabric ↔ Bukkit 事件类型自动转换。关闭后大量 Bukkit 插件将无法响应模组事件。**务必保持 true**。 |
| `banner.capture-compound` | 捕获 NBT 复合事件 | bool | `true`（`true` / `false`） | ✅ | 是否捕获模组方块的 NBT 复合数据用于 Bukkit 事件。开启可让 ChestShop 等插件识别模组方块。 |

---

## 配置示例（banner.yml 完整默认值）

```yaml
# Banner Configuration
# https://github.com/MohistMC/Banner
banner:
  # 控制台语言
  lang: en_US
  # 启动时检查 Banner 更新
  check_update: true
  # bStats 匿名统计
  metrics: true
  # 启动 Logo
  show_logo: true
  # Bukkit API 版本（自动写入，请勿手动修改）
  bukkit_version: 1.20.1-R0.1-SNAPSHOT
  # 强制覆盖 Bukkit 版本
  bukkit_version_override: ""

  # 禁用插件黑名单
  disable_plugins_blacklist: false
  # 禁用模组黑名单
  disable_mods_blacklist: false
  # 允许非 Paper 系插件
  support_non_paper_plugins: true

  # 异步 tick 模式（实验性，不建议开启）
  async-tick: false
  # 禁用看门狗（不推荐）
  disable-watchdog: false
  # 实体激活范围优化
  entity-activation-range: true
  # Spark 计时器
  use-Spark-and-Sync-Timer: true

  # 事件类型转换（务必保持 true）
  event-transformation: true
  # 捕获 NBT 复合事件
  capture-compound: true
```

## 优化建议（针对 Fabric 模组 + Bukkit 插件混合服）

1. **平台特性**：Banner 是 **Fabric** 混合端，不要试图加载 Forge 模组。模组必须为 Fabric 格式（`.jar` 来自 Fabric mod）。
2. **Lithium 冲突**：Fabric 性能优化模组 Lithium 与 Banner 的 `entity-activation-range` 功能重复，建议二选一以避免冲突。
3. **Sodium 冲突**：Sodium 是客户端渲染优化模组，服务端加载无意义且可能引发问题，**不要**将 Sodium 放入服务端 `mods/` 目录。
4. **Carpet 兼容**：Carpet mod 与 Banner 兼容性较好，可放心使用。
5. **事件桥接**：保持 `event-transformation: true` 与 `capture-compound: true`，否则 Bukkit 插件将无法识别模组方块与实体。
6. **异步 tick**：**绝对不要**开启 `async-tick`，目前与绝大多数 Fabric 模组冲突，会引发严重崩溃。
7. **看门狗**：保持 `disable-watchdog: false`，模组卡死时能及时收到报警。
8. **JVM 优化**：Banner 推荐 `-Xms4G -Xmx8G -XX:+UseG1GC`，Fabric 模组通常比 Forge 轻量，内存需求略低。
9. **Java 版本**：1.20.x 需要 Java 17；1.21+ 可能需要 Java 21（取决于 Fabric Loader 版本）。
10. **项目演变**：2025 年 7 月 Banner 从 MohistMC 独立，部分分支更名为 Taiyitist。如使用新分支，配置项可能略有差异，请以实际项目文档为准。

> 参考来源：Banner 官方源码 [`BannerConfig.java`](https://github.com/MohistMC/Banner)、[MohistMC Discord](https://discord.gg/mohistmc)。
