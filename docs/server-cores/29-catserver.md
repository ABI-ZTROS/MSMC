# CatServer 服务器配置文件中文手册

> CatServer（猫端）是国内开发者 Luohuayu 制作的 Forge + Bukkit + Spigot 三合一混合服务端。
> 继承关系：Vanilla → Forge → CatServer（含 Bukkit/Spigot/Paper 兼容层）
> 官方 GitHub：https://github.com/Luohuayu/CatServer
> 官方网站：https://catmc.org/

CatServer 由国人开发，是国内最早的高版本混合端之一，长期支持 1.12.2 / 1.16.5 / 1.18.2 三个版本。其核心创新是 **Remap 重映射系统**，能在运行时智能处理模组与插件之间的 API 调用转换，让 Forge 模组与 Bukkit/Spigot 插件能在同一运行时中共存。CatServer 集成了 PaperMC 的部分优化技术，并提供对 FakePlayer（虚拟玩家）的完整支持，使模组的机器/农场能正常触发 Bukkit 事件。配置文件 `catserver.yml` 相较其他混合端**配置项更精简**，主要聚焦于世界设置、假人支持和插件兼容性补丁。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| paper.yml | YAML | Paper 兼容层 | CatServer 提供的 Paper 兼容配置 |
| forge.cfg / fml.toml | TOML/CFG | Forge 继承 | Forge 模组加载器配置 |
| catserver.yml | YAML | CatServer 专属 | CatServer 独有核心配置（本文档重点） |

> 说明：CatServer 完整继承 Forge 与 Bukkit/Spigot/Paper 的全部配置体系，本文档仅聚焦 CatServer 独有的 `catserver.yml`。其余配置请参阅对应的 Forge / Spigot / Bukkit 手册。

## catserver.yml（CatServer 专属配置）

`catserver.yml` 位于服务器根目录，首次启动时由 `catserver.server.CatServerConfig` 自动生成。采用标准 YAML 格式，所有配置在服务器启动时读取，多数项需重启生效。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `world.keepSpawnInMemory`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `string[]` 字符串列表。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示部分支持热重载。

---

### 1. 世界设置（world）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `world.keepSpawnInMemory` | 出生点常驻内存 | bool | `true`（`true` / `false`） | 🔄 | 是否始终将出生点区域区块加载到内存中。开启可避免新玩家进入时卡顿，但占用内存。小型服建议 `true`，内存紧张的大型服可考虑 `false`。 |
| `world.forceSaveOnWatchdog` | 看门狗触发时强制保存 | bool | `true`（`true` / `false`） | 🔄 | 当服务器因 watchdog 超时崩溃时是否强制保存世界数据。强烈建议 `true` 防止数据丢失。注意：可能延长崩溃恢复时间。 |
| `world.worldGenMaxTickTime` | 世界生成最大 tick 时间 | int | `15`（≥ 1，单位：毫秒） | 🔄 | 单次 tick 内世界生成的最大耗时。降低此值可减少世界生成卡顿，但会延长生成完成时间。玩家频繁飞行（鞘翅）时建议调高。 |

---

### 2. 假人设置（fakePlayer）

> FakePlayer（假人/虚拟玩家）是模组机器（如工业模组的采矿机）触发 Bukkit 事件时使用的虚拟玩家身份。CatServer 对其有完整支持。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `fakePlayer.permissions` | 假人默认权限列表 | string[] | `["essentials.build"]`（权限节点列表） | 🔄 | 为服务器假人添加的默认权限节点列表。配合 Essentials 等插件实现假人自动建造、交互等功能。每行一个权限节点。 |
| `fakePlayer.eventPass` | 假人事件传递 | bool | `false`（`true` / `false`） | 🔄 | 是否让假人触发玩家事件（如方块破坏、实体交互）。设为 `false` 减少服务器负载（推荐）；设为 `true` 可实现更真实的假人行为（部分插件可能误判为真人玩家）。 |

---

### 3. 插件兼容性补丁（plugin.patcher）

> CatServer 内置多个插件兼容补丁，自动修复已知与 Forge 模组冲突的插件行为。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `plugin.patcher.enableDynmapCompatible` | Dynmap 兼容补丁 | bool | `true`（`true` / `false`） | 🔄 | 修复 Dynmap 地图插件与 Forge 模组的兼容性问题。使用 Dynmap 生成 3D 地图时必须开启。 |
| `plugin.patcher.enableWorldEditCompatible` | WorldEdit 兼容补丁 | bool | `true`（`true` / `false`） | 🔄 | 解决 WorldEdit 与部分 Forge 模组的方块操作冲突（如模组自定义方块无法被编辑）。建议始终开启，除非确认不使用 WorldEdit。 |
| `plugin.patcher.enableEssentialsNewVersionCompatible` | Essentials 新版兼容补丁 | bool | `true`（`true` / `false`） | 🔄 | 支持 EssentialsX 等新版本 Essentials 插件，修复指令冲突、权限管理等兼容性问题。使用 EssentialsX 时必须开启。 |

---

### 4. 性能优化（optimization）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `optimization.async-chunk-loading` | 异步区块加载 | bool | `true`（`true` / `false`） | ✅ | 是否启用异步区块加载。开启可减少主线程阻塞，提升玩家飞行/传送时的流畅度。⚠️ 与部分老式模组可能冲突。 |
| `optimization.reduce-lag` | 启用防卡顿优化 | bool | `true`（`true` / `false`） | ✅ | 启用 CatServer 的综合防卡顿优化（实体激活范围、AI 节流等）。建议保持 `true`。 |
| `optimization.fast-operations` | 快速操作优化 | bool | `true`（`true` / `false`） | ✅ | 启用快速方块/实体操作优化。可提升约 10% TPS。⚠️ 与依赖精确事件触发的红石插件可能冲突。 |

---

### 5. 村民与红石

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `villager.atFix` | 村民 AI 修复 | bool | `true`（`true` / `false`） | 🔄 | 修复部分 Forge 模组导致的村民 AI 异常（村民不工作/卡住）。建议保持 `true`。 |

---

### 6. 通用设置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `versionCheck` | 版本检查 | bool | `true`（`true` / `false`） | 🔄 | 启动时自动检查 CatServer 更新。建议 `true` 以及时获取安全更新。 |
| `disableAsyncCatchWarn` | 禁用异步捕获警告 | bool | `false`（`true` / `false`） | 🔄 | 是否禁用插件异步操作警告。插件调试时可设 `true`，生产环境建议 `false` 以便发现插件异步调用主线程 API 的问题。 |

---

## 配置示例（catserver.yml 完整默认值）

```yaml
# CatServer Configuration
# https://github.com/Luohuayu/CatServer
# 世界设置
world:
  # 出生点区块常驻内存
  keepSpawnInMemory: true
  # 看门狗崩溃时强制保存
  forceSaveOnWatchdog: true
  # 世界生成最大 tick 时间（毫秒）
  worldGenMaxTickTime: 15

# 假人设置
fakePlayer:
  # 假人默认权限
  permissions:
    - "essentials.build"
  # 是否传递假人事件
  eventPass: false

# 插件兼容补丁
plugin:
  patcher:
    # Dynmap 兼容
    enableDynmapCompatible: true
    # WorldEdit 兼容
    enableWorldEditCompatible: true
    # Essentials 新版兼容
    enableEssentialsNewVersionCompatible: true

# 性能优化
optimization:
  # 异步区块加载
  async-chunk-loading: true
  # 防卡顿优化
  reduce-lag: true
  # 快速操作
  fast-operations: true

# 村民 AI 修复
villager:
  atFix: true

# 通用设置
# 版本检查
versionCheck: true
# 禁用异步捕获警告
disableAsyncCatchWarn: false
```

## 优化建议（针对 Forge 模组 + Bukkit 插件混合服）

1. **出生点常驻**：小型服保持 `world.keepSpawnInMemory: true`，大型多世界服可考虑关闭以节省内存，但需配合插件预加载出生点。
2. **看门狗保存**：保持 `world.forceSaveOnWatchdog: true`，防止卡死时丢档。
3. **假人设置**：模组机器较多的服（工业/科技服）保持 `fakePlayer.eventPass: false` 并配置合适的 `permissions`，避免假人触发过多事件影响性能。
4. **兼容补丁**：使用 Dynmap / WorldEdit / EssentialsX 时务必保持对应补丁开启，否则可能引发模组方块操作异常。
5. **异步区块**：`optimization.async-chunk-loading` 对老式模组（如 1.12.2 部分模组）可能冲突，开启前请测试。
6. **村民修复**：模组较多的生存服保持 `villager.atFix: true`，避免村民 AI 异常影响交易系统。
7. **JVM 优化**：CatServer 推荐 `-Xms4G -Xmx8G -XX:+UseG1GC -XX:+AlwaysPreTouch`，模组多时按需增加。
8. **Java 版本**：1.12.2 使用 Java 8；1.16.5 推荐 Java 11；1.18.2 需要 Java 17。

> 参考来源：CatServer 官方源码 [`CatServerConfig.java`](https://github.com/Luohuayu/CatServer)、[CatServer 官方网站](https://catmc.org/)。
