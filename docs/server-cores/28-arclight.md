# Arclight 服务器配置文件中文手册

> Arclight 是基于 Forge（含 NeoForge）+ Mixin 的 Bukkit 服务端实现，让 Forge 模组与 Bukkit 插件共存。
> 继承关系：Vanilla → Forge/NeoForge → Arclight
> 官方 GitHub：https://github.com/IzzelAliz/Arclight

Arclight 由 IzzelAliz 开发，定位为「现代 Bukkit on Forge 实现」。与 Mohist 不同，Arclight 使用 SpongePowered Mixin 技术直接修改 Minecraft 字节码，而非依赖反射桥接，理论上更轻量、兼容性更好。Arclight 同时维护 Forge、Fabric、NeoForge 三个版本分支（fabric、forge、neoforge），是少数能在 1.20/1.21 高版本上稳定运行的混合端。其配置文件采用 **HOCON 格式**（`.conf` 后缀，而非 `.yml`），由 `io.izzel.arclight.config.ArclightConfig` 加载。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| paper.yml / paper-global.yml | YAML | Paper 兼容层 | Arclight 提供的 Paper 兼容配置 |
| forge.cfg / fml.toml / neoforge.toml | TOML/CFG | Forge/NeoForge 继承 | 模组加载器配置 |
| arclight.conf | HOCON | Arclight 专属 | Arclight 独有核心配置（本文档重点） |

> 说明：Arclight 完整继承 Forge/NeoForge 与 Bukkit 的全部配置体系，本文档仅聚焦 Arclight 独有的 `arclight.conf`。其余配置请参阅对应的 Forge / Spigot / Bukkit 手册。

## arclight.conf（Arclight 专属配置）

`arclight.conf` 位于服务器根目录，**采用 HOCON 格式（不是 YAML！）**。由 `io.izzel.arclight.config.ArclightConfig` 加载。HOCON 语法特点：使用大括号嵌套、`key = value`（等号两侧空格可选）、`#` 注释、字符串可不加引号。所有配置在服务器启动时读取，多数项需重启生效。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `arclight.setdefaultlocale`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示部分支持热重载（Arclight 多数项需重启）。
- **格式注意**：HOCON 文件中的布尔值写作 `true` / `false`，字符串可加或不加双引号。

---

### 1. 通用设置（arclight）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `arclight.setdefaultlocale` | 设置默认区域语言 | bool | `false`（`true` / `false`） | ✅ | 是否强制将服务器的默认区域设置为系统区域（而非 en_US）。影响部分插件的本地化文本。 |
| `arclight.bukkit-version` | Bukkit API 版本 | string | 自动检测（如 `1.20.1-R0.1-SNAPSHOT`） | ✅ | Arclight 内部使用的 Bukkit API 版本号，由 Arclight 自动写入，请勿手动修改。 |
| `arclight.bukkit-version-override` | 强制覆盖 Bukkit 版本 | string | 空（任意版本字符串） | ✅ | 强制覆盖对插件声明的 Bukkit 版本号。仅在插件因版本检查拒绝加载时使用。 |
| `arclight.api-version-check` | API 版本检查 | bool | `true`（`true` / `false`） | ✅ | 是否对插件进行 Bukkit API 版本兼容性检查。关闭后所有插件无视版本声明强制加载（可能导致崩溃）。 |
| `arclight.verbose` | 详细日志输出 | bool | `false`（`true` / `false`） | ✅ | 是否启用 Arclight 详细日志（包含 Mixin 注入、事件桥接等调试信息）。排查兼容性问题时开启。 |

---

### 2. 性能与并发

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `arclight.async-tick.enabled` | 异步 tick 模式 | bool | `false`（`true` / `false`） | ✅ | 实验性：是否启用异步 tick 模式（部分世界逻辑异步执行）。⚠️ 极不稳定，与绝大多数 Forge 模组冲突，**强烈不建议开启**。 |
| `arclight.disable-flush` | 禁用批量刷新 | bool | `false`（`true` / `false`） | ✅ | 是否禁用网络数据包批量刷新。开启可能减少延迟但增加带宽。一般保持 false。 |
| `arclight.disable-watchdog` | 禁用看门狗 | bool | `false`（`true` / `false`） | ✅ | 是否禁用 watchdog 主线程监控。⚠️ 不推荐，模组卡死将无报警。 |
| `arclight.optimize-entity-portal` | 优化实体传送门 | bool | `true`（`true` / `false`） | ✅ | 是否优化实体穿越传送门（下界/末地）的处理逻辑。开启可减少传送门附近的卡顿。 |

---

### 3. 兼容性与事件桥接

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `arclight.capture-compound` | 捕获 NBT 复合事件 | bool | `true`（`true` / `false`） | ✅ | 是否捕获模组方块的 NBT 复合数据用于 Bukkit 事件。开启可让 ChestShop 等插件识别模组方块，但增加少量开销。 |
| `arclight.event-transformation` | 事件类型转换 | bool | `true`（`true` / `false`） | ✅ | 是否启用 Forge ↔ Bukkit 事件类型自动转换。关闭后大量 Bukkit 插件将无法响应模组事件。**务必保持 true**。 |
| `arclight.entity-spawn.unique-id` | 实体生成唯一 ID | bool | `true`（`true` / `false`） | ✅ | 是否为模组生成的实体分配 Bukkit 兼容的唯一 UUID。开启可让 RPG/统计类插件识别模组实体。 |

---

### 4. 命令与权限

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `arclight.command.no-permission-message` | 无权限提示消息 | string | `You do not have permission to use this command.` | ✅ | 玩家无权限执行 Arclight 内置命令时显示的提示文本。支持 `&` 颜色代码。 |

---

## 配置示例（arclight.conf 完整默认值）

```hocon
# Arclight Configuration
# https://github.com/IzzelAliz/Arclight
arclight {
    # 强制设置默认区域语言
    setdefaultlocale = false
    # Bukkit API 版本（自动写入，请勿手动修改）
    bukkit-version = 1.20.1-R0.1-SNAPSHOT
    # 强制覆盖 Bukkit 版本（仅在插件版本检查失败时使用）
    bukkit-version-override = ""
    # API 版本兼容性检查
    api-version-check = true
    # 详细日志输出
    verbose = false

    # 异步 tick 设置
    async-tick {
        # ⚠️ 实验性，强烈不建议开启
        enabled = false
    }

    # 禁用网络批量刷新
    disable-flush = false
    # 禁用看门狗（不推荐）
    disable-watchdog = false
    # 优化实体传送门
    optimize-entity-portal = true

    # 捕获 NBT 复合事件
    capture-compound = true
    # 事件类型转换（务必保持 true）
    event-transformation = true

    # 实体生成设置
    entity-spawn {
        # 为模组实体分配 Bukkit UUID
        unique-id = true
    }

    # 命令设置
    command {
        # 无权限提示消息
        no-permission-message = "You do not have permission to use this command."
    }
}
```

## 优化建议（针对 Forge 模组 + Bukkit 插件混合服）

1. **事件桥接**：保持 `event-transformation: true` 与 `capture-compound: true`，否则 Bukkit 插件将无法识别模组方块与实体。
2. **异步 tick**：**绝对不要**开启 `async-tick.enabled`，目前与绝大多数 Forge 模组冲突，会引发严重崩溃。
3. **看门狗**：保持 `disable-watchdog: false`，模组卡死时能及时收到报警。
4. **版本检查**：插件因版本检查拒绝加载时，可临时设置 `api-version-check: false` 或 `bukkit-version-override` 绕过，但需自行承担风险。
5. **详细日志**：排查兼容性问题时开启 `verbose: true`，生产环境关闭以减少日志量。
6. **JVM 优化**：Arclight 推荐 `-Xms4G -Xmx8G -XX:+UseG1GC`，模组多时按需增加。
7. **Java 版本**：1.20+ 需要 Java 17；1.21+ 可能需要 Java 21（取决于 NeoForge 分支）。
8. **平台选择**：Arclight 同时维护 Forge / Fabric / NeoForge 三分支，根据模组依赖选择对应版本，**不可混用**。

> 参考来源：Arclight 官方源码 [`ArclightConfig.java`](https://github.com/IzzelAliz/Arclight)、[Arclight README](https://github.com/IzzelAliz/Arclight/blob/master/README.md)。
