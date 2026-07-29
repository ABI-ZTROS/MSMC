# Bukkit 服务器配置文件中文手册

> Bukkit 是 Minecraft Java 版最早的插件 API 框架，定义了插件开发标准接口。CraftBukkit 是其官方实现（已停止维护，由 Spigot 接替）。
> 继承关系：**Vanilla → Bukkit**（Bukkit 在 Vanilla 之上添加插件 API 层）
> 官方网站：https://bukkit.org/
> 官方文档：https://bukkit.fandom.com/wiki/Bukkit.yml
> 数据来源：Bukkit Wiki / `org.bukkit.craftbukkit.CraftServer` / BukkitAPI 源码
> 适用版本基准：Bukkit 1.20.x / 1.21.x（API 兼容 1.21+）

Bukkit 提供四个 YAML 配置文件：`bukkit.yml`（核心运行配置）、`permissions.yml`（默认权限组）、`commands.yml`（命令别名与替换）、`help.yml`（帮助页显示）。这些文件位于服务器根目录，由 Bukkit 在启动时加载，**部分项可通过 `/reload` 热重载，但建议重启以保安全**。所有键名不翻译，扁平化路径用点号分隔（如 `settings.allow-end`）。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（请参阅 Vanilla 手册） |
| **bukkit.yml** | YAML | **Bukkit 专属** | **核心运行配置（生成上限、tick 间隔、命令别名、自动更新等）** |
| **permissions.yml** | YAML | **Bukkit 专属** | **默认权限组定义（权限元数据，非具体权限）** |
| **commands.yml** | YAML | **Bukkit 专属** | **命令别名与命令替换** |
| **help.yml** | YAML | **Bukkit 专属** | **帮助页主题与显示控制** |
| help.yml | YAML | Bukkit 专属 | 帮助页配置（本文档重点） |

> 本文重点翻译 Bukkit **专属**的 4 个 YAML 文件。`server.properties` 请参阅 Vanilla 手册。

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `settings.allow-end`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举 / `list` 列表。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持 `/reload` 热重载。
- YAML 格式注意：缩进必须用空格（非 Tab），布尔值用 `true`/`false`，字符串无须引号。

---

## bukkit.yml（核心运行配置）

`bukkit.yml` 由 Bukkit 主类加载，控制 Bukkit API 层的所有运行时行为。⚠️ Spigot / Paper / Purpur 等下游核心完全继承此文件，并新增自己的配置文件（`spigot.yml` / `paper-global.yml` 等）。本文档同样适用于下游核心的 `bukkit.yml`。

### 1. settings（基础设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.allow-end` | 允许进入末地 | bool | `true`（`true`/`false`） | ✅ | 是否允许玩家进入末地。关闭后末地传送门不工作。⚠️ 与 `server.properties` 的 `allow-end` 是两套独立机制（Vanilla 用 properties，Bukkit 用 bukkit.yml），Spigot/Paper 建议用此键。 |
| `settings.shutdown-message` | 关服提示消息 | string | `Server closed`（任意文本） | 🔄 | 服务器关闭时踢出玩家显示的提示文本。支持 `§` 颜色码。 |
| `settings.deprecated-verbose` | 弃用 API 警告 | bool | `false`（`true`/`false`） | 🔄 | 插件使用已弃用的 Bukkit API 时是否在控制台打印警告。开发环境建议 `true`，生产环境 `false` 减少日志噪音。 |
| `settings.default-plugin-language` | 默认插件语言 | string | `en`（语言代码） | ✅ | 插件未指定语言时使用的默认语言。影响部分支持多语言的插件。 |
| `settings.use-map-converting` | 自动转换旧地图 | bool | `true`（`true`/`false`） | ✅ | 启动时自动将旧版本地图转换为新版本格式。关闭后旧地图可能无法加载。 |
| `settings.query-plugins` | Query 暴露插件列表 | bool | `true`（`true`/`false`） | ✅ | 通过 GameSpy Query 协议列出已加载插件。公网服建议 `false`，避免泄露插件信息。 |
| `settings.unknown-command` | 未知命令提示 | string | `Unknown command. Type "/help" for help.`（任意文本） | 🔄 | 玩家执行未注册命令时显示的提示。设为空字符串则不显示。支持 `§` 颜色码。 |
| `settings.permissions` | 默认权限提示 | string | `You do not have permission to use this command.`（任意文本） | 🔄 | 玩家无权限执行命令时显示的提示。 |
| `settings.timeout-time` | 卡死超时阈值 | int | `60`（≥ 0，秒） | 🔄 | 主线程连续无响应多少秒后判定为「卡死」并强制重启。`0` 禁用 watchdog。生产环境建议保持 `60`。 |
| `settings.restart-on-crash` | 崩溃自动重启 | bool | `false`（`true`/`false`） | ✅ | 服务器崩溃时是否自动执行 `restart-script`。⚠️ Vanilla 不支持此键，仅 CraftBukkit/Spigot/Paper 有。 |
| `settings.restart-script` | 重启脚本路径 | string | `./start.sh`（脚本路径） | ✅ | 崩溃自动重启时执行的脚本路径。Linux 用 `./start.sh`，Windows 用 `start.bat`。脚本需有可执行权限。 |

### 2. spawn-limits（生物生成上限）

> 控制每个玩家周围可生成的生物数量上限。**值越高性能压力越大**。这些是「每玩家」上限，不是「全服」上限。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `spawn-limits.monsters` | 怪物生成上限 | int | `70`（≥ 0） | 🔄 | 每个玩家周围可生成的敌对怪物数量上限。降低可缓解怪物密集时的卡顿，但夜晚怪物变少。 |
| `spawn-limits.animals` | 动物生成上限 | int | `10`（≥ 0） | 🔄 | 每个玩家周围可生成的被动动物数量上限。 |
| `spawn-limits.water-animals` | 水生动物生成上限 | int | `5`（≥ 0） | 🔄 | 每个玩家周围可生成的水生动物（鱿鱼、海豚等）数量上限。 |
| `spawn-limits.water-ambient` | 水生环境生物上限 | int | `20`（≥ 0） | 🔄 | 每个玩家周围可生成的水生环境生物（热带鱼等）数量上限。 |
| `spawn-limits.ambient` | 环境生物生成上限 | int | `15`（≥ 0） | 🔄 | 每个玩家周围可生成的环境生物（蝙蝠）数量上限。 |

### 3. chunk-gc（区块垃圾回收）

> 控制 Bukkit 主动回收未使用区块的频率。⚠️ Paper 已废弃此机制，使用自己的区块管理。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunk-gc.period-in-ticks` | 区块 GC 间隔 | int | `600`（≥ 0，tick） | 🔄 | 多少 tick 执行一次区块 GC。`600` = 30 秒。`0` 禁用主动 GC（不推荐）。 |
| `chunk-gc.load-threshold` | 区块 GC 触发阈值 | int | `0`（≥ 0） | 🔄 | 加载多少个新区块后触发一次区块 GC。`0` 仅按 `period-in-ticks` 周期触发。 |

### 4. ticks-per（生成 tick 间隔）

> 控制生物生成尝试的间隔。**值越大生成越慢但性能越好**。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `ticks-per.animal-spawns` | 动物生成间隔 | int | `400`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次动物生成。`400` = 20 秒。`-1` 禁用动物生成。 |
| `ticks-per.monster-spawns` | 怪物生成间隔 | int | `1`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次怪物生成。`1` = 每 tick 尝试（原版默认）。调大可减少怪物数量并提升性能。 |
| `ticks-per.water-ambient-spawns` | 水生环境生物生成间隔 | int | `1`（≥ 0，tick） | 🔄 | 多少 tick 尝试一次水生环境生物生成。 |
| `ticks-per.autosave` | 自动保存间隔 | int | `6000`（≥ 0，tick） | 🔄 | 多少 tick 自动保存一次世界与玩家数据。`6000` = 5 分钟。`0` 禁用自动保存（不推荐，崩服会丢进度）。 |

### 5. auto-updater（自动更新器）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `auto-updater.enabled` | 启用自动更新检查 | bool | `true`（`true`/`false`） | ✅ | 启动时是否检查 CraftBukkit 新版本。⚠️ 仅 CraftBukkit 有效，Spigot/Paper 使用各自的更新机制。 |
| `auto-updater.on-broken` | 检测到破坏性更新时 | enum | `warn-console`（`warn-console`/`warn-ops`/`console-and-ops`） | ✅ | 检测到当前版本已知有严重 bug 时的通知方式。 |
| `auto-updater.on-update` | 检测到新版本时 | enum | `warn-console`（`warn-console`/`warn-ops`/`console-and-ops`） | ✅ | 检测到新版本可用时的通知方式。 |
| `auto-updater.prefer-beta` | 偏好测试版 | bool | `false`（`true`/`false`） | ✅ | 是否优先检查 beta / dev 版本。生产环境保持 `false`。 |
| `auto-updater.host` | 更新服务器地址 | string | `dl.bukkit.org`（域名） | ✅ | 检查更新的服务器地址。一般无需修改。 |

### 6. aliases（命令别名）

> `aliases` 是顶层键，**不是 `settings` 的子键**！用于定义命令别名。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `aliases.<别名>` | 命令别名 | string/list | ` `（无默认别名） | 🔄 | 将 `<别名>` 重定向到指定命令。值可以是字符串（单个命令）或列表（按顺序执行多命令）。例：`aliases.gamemode: "minecraft:gamemode"`。⚠️ 1.13+ 推荐用 `commands.yml` 替代。 |

---

## permissions.yml（默认权限组配置）

> ⚠️ **重要**：`permissions.yml` **不**定义具体权限！它定义**权限组（permission groups）**，供插件通过 `Permission` API 引用。普通权限由各插件自行注册，本文件仅提供「权限组」聚合机制。
>
> 例如：插件 A 注册 `pluginA.basic`，插件 B 注册 `pluginB.basic`，你可以在 `permissions.yml` 中定义组 `default-basic` 同时包含这两者，方便赋给玩家。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `default` | 内置默认组 | map | `{}`（YAML map） | 🔄 | Bukkit 内置的默认权限组名。可直接在下方列出权限节点。⚠️ 不建议修改 `default` 组，应自定义新组。 |
| `<自定义组名>` | 自定义权限组 | map | `{}`（YAML map） | 🔄 | 自定义权限组。组名任意（如 `vip`、`admin`），值为包含 `default` 与 `children` 的 map。 |
| `<组名>.default` | 组默认权限 | enum | `op`（`true`/`false`/`op`/`not-op`） | 🔄 | 此组的默认赋权策略。`true` 所有人都有；`false` 所有人都没有；`op` 仅 OP 有；`not-op` 仅非 OP 有。 |
| `<组名>.children` | 子权限列表 | map | `{}`（YAML map） | 🔄 | 此组包含的子权限节点（键为权限名，值为 `true`/`false` 表示是否赋予）。可嵌套其他权限组。 |
| `<组名>.description` | 组描述 | string | ` `（空） | 🔄 | 权限组的文字描述，便于管理员理解用途。 |

### permissions.yml 配置示例

```yaml
# permissions.yml 默认内容
server.basics:
    description: 基础服务器命令权限
    default: true
    children:
        bukkit.command.help: true
        bukkit.command.tell: true
        bukkit.command.list: true

# 自定义 VIP 组示例
server.vip:
    description: VIP 玩家权限组
    default: false
    children:
        server.basics: true
        bukkit.command.teleport: true
        bukkit.command.gamemode: true
```

---

## commands.yml（命令别名与替换）

> 1.13+ 引入，用于**运行时**定义命令别名，无需修改 `plugin.yml`。与 `bukkit.yml` 的 `aliases` 相比，更灵活、支持参数转发。⚠️ 别名不能与现有命令同名，否则不生效。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `command-block-overrides` | 命令方块覆盖 | map | `{}`（YAML map） | 🔄 | 命令方块执行命令时使用的别名映射。键为原命令，值为别名命令。一般留空。 |
| `aliases` | 命令别名 | map | `{}`（YAML map） | 🔄 | 全局命令别名映射。键为别名命令名，值可以是字符串（单个目标命令）或 map（含 `i`/`k`/`p` 等 flags）。 |
| `aliases.<别名>` | 单个别名 | string/map | ` `（无默认别名） | 🔄 | 将 `<别名>` 重定向到目标命令。字符串形式：`gamemode: "minecraft:gamemode $1-"`（`$1-` 转发所有参数）。 |
| `aliases.<别名>.i` | 忽略大小写 | bool | `false`（`true`/`false`） | 🔄 | 别名匹配时是否忽略大小写。 |
| `aliases.<别名>.k` | 保留原命令 | bool | `false`（`true`/`false`） | 🔄 | `true` 时除执行别名命令外，仍保留原命令可用。 |
| `aliases.<别名>.p` | 参数转发模板 | string | ` `（空） | 🔄 | 参数转发模板。`$1` = 第一个参数；`$1-` = 第一个及之后所有参数；`$@` = 所有参数。例：`p: "minecraft:gamemode creative $1"`。 |

### commands.yml 配置示例

```yaml
# commands.yml 默认内容（无别名）
command-block-overrides: []
aliases:
  # 示例：将 /gamemode 别名到 /minecraft:gamemode
  # gamemode:
  #   p: "minecraft:gamemode $1-"
  # 示例：将 /gmc 设为创造模式快捷命令
  # gmc:
  #   p: "minecraft:gamemode creative $1"
  # 示例：将 /i 别名到 /give，忽略大小写
  # i:
  #   p: "minecraft:give $1 $2 $3"
  #   i: true
```

---

## help.yml（帮助页配置）

> 控制玩家执行 `/help` 时显示的帮助内容。可隐藏某些命令、自定义帮助主题、调整分页显示。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `general` | 通用设置 | map | `{}`（YAML map） | 🔄 | 帮助页通用设置节。 |
| `general.default-topic-format` | 默认主题格式 | string | ` <description>\n\n<usage>\n\n<aliases>\n`（格式串） | 🔄 | 默认帮助主题的输出格式模板。可用占位符 `<description>`、`<usage>`、`<aliases>`、`<permission>`。 |
| `general.command-prefix` | 命令前缀 | string | `/`（任意字符串） | 🔄 | 帮助页中命令的前缀字符。一般保持 `/`。 |
| `general.console-command-prefix` | 控制台命令前缀 | string | ` `（空 = 自动） | 🔄 | 控制台中命令的前缀。留空则与 `command-prefix` 相同。 |
| `general.search-index-listed` | 索引列出搜索结果 | bool | `true`（`true`/`false`） | 🔄 | `/help <关键词>` 搜索时是否列出索引。 |
| `general.max-help-page-size` | 每页最大帮助数 | int | `7`（≥ 1） | 🔄 | `/help` 每页显示多少条命令。值越大单页内容越多。 |
| `general.list-of-headers` | 帮助页标题列表 | list | `[Help - Index, Help - Search, Help - <topic>, Help - Topics, Help - Previous, Help - Next]` | 🔄 | 各类帮助页的标题文本列表，按顺序对应：索引页、搜索页、主题页、主题列表页、上一页、下一页。 |
| `general.default-topic-permission` | 默认主题权限 | string | ` `（空 = 无限制） | 🔄 | 查看默认帮助主题所需的权限节点。留空则所有人可见。 |
| `general.topics-on-first-page` | 首页显示主题列表 | bool | `true`（`true`/`false`） | 🔄 | `/help` 第一页是否显示主题列表。 |
| `general.amendments` | 命令修改 | map | `{}`（YAML map） | 🔄 | 对已注册命令的描述进行补充修改。键为命令名，值为包含 `description`/`usage`/`permission`/`aliases` 的 map。 |
| `general.amendments.<命令>.short-description` | 命令短描述 | string | ` `（空） | 🔄 | 覆盖命令在帮助列表中的短描述。 |
| `general.amendments.<命令>.full-description` | 命令完整描述 | string | ` `（空） | 🔄 | 覆盖命令的完整描述。 |
| `general.amendments.<命令>.usage` | 命令用法 | string | ` `（空） | 🔄 | 覆盖命令的用法说明。 |
| `general.amendments.<命令>.permission` | 命令权限 | string | ` `（空） | 🔄 | 覆盖命令所需权限节点。 |
| `general.amendments.<命令>.aliases` | 命令别名 | list | `[]` | 🔄 | 覆盖命令的别名列表。 |
| `topics` | 自定义主题 | map | `{}`（YAML map） | 🔄 | 自定义帮助主题。键为 `/<主题名>`，值为包含 `shortDescription`/`fullDescription`/`permission` 的 map。 |
| `topics.<主题名>.short-description` | 主题短描述 | string | ` `（空） | 🔄 | 自定义主题的短描述。 |
| `topics.<主题名>.full-description` | 主题完整描述 | string | ` `（空） | 🔄 | 自定义主题的完整描述（支持多行，用 `\n` 分隔）。 |
| `topics.<主题名>.permission` | 主题查看权限 | string | ` `（空 = 无限制） | 🔄 | 查看此主题所需的权限节点。 |
| `index` | 索引页内容 | map | `{}`（YAML map） | 🔄 | `/help` 索引页的额外说明内容。 |
| `index.<名称>.short-description` | 索引项短描述 | string | ` `（空） | 🔄 | 索引页中某个分类项的短描述。 |
| `index.<名称>.full-description` | 索引项完整描述 | string | ` `（空） | 🔄 | 索引页中某个分类项的完整描述。 |

### help.yml 配置示例

```yaml
# help.yml 默认内容
general:
    command-prefix: '/'
    console-command-prefix: ''
    default-topic-format: ' <description>\n\n<usage>\n\n<aliases>\n'
    search-index-listed: true
    max-help-page-size: 7
    list-of-headers:
        - Help - Index
        - Help - Search
        - Help - <topic>
        - Help - Topics
        - Help - Previous
        - Help - Next
    default-topic-permission: ''
    topics-on-first-page: true
    amendments:
        # 示例：修改 /stop 命令的描述
        # stop:
        #     short-description: 关闭服务器
        #     full-description: 关闭服务器并踢出所有玩家
        #     permission: bukkit.command.stop
        #     aliases: []
topics:
    # 示例：自定义主题
    # /rules:
    #     short-description: 服务器规则
    #     full-description: |
    #         1. 禁止作弊
    #         2. 禁止恶意破坏
    #         3. 禁止骚扰他人
    #     permission: ''
index:
    # 示例：索引页额外项
    # basics:
    #     short-description: 基础命令
    #     full-description: 查看基础服务器命令
```

---

## 配置示例（bukkit.yml 完整默认值）

```yaml
settings:
    allow-end: true
    shutdown-message: Server closed
    deprecated-verbose: false
    default-plugin-language: en
    use-map-converting: true
    query-plugins: true
    unknown-command: 'Unknown command. Type "/help" for help.'
    permissions: 'You do not have permission to use this command.'
    timeout-time: 60
    restart-on-crash: false
    restart-script: ./start.sh
spawn-limits:
    monsters: 70
    animals: 10
    water-animals: 5
    water-ambient: 20
    ambient: 15
chunk-gc:
    period-in-ticks: 600
    load-threshold: 0
ticks-per:
    animal-spawns: 400
    monster-spawns: 1
    water-ambient-spawns: 1
    autosave: 6000
auto-updater:
    enabled: true
    on-broken: warn-console
    on-update: warn-console
    prefer-beta: false
    host: dl.bukkit.org
aliases:
    # 留空，使用 commands.yml 替代
```

---

## 优化建议

1. **降低 `spawn-limits.monsters`**：低配服从 `70` 降到 `40`–`50` 可显著缓解夜晚卡顿，但怪物变少。
2. **调大 `ticks-per.monster-spawns`**：从 `1` 调到 `5`–`10`，怪物生成尝试变慢但每次生成更稳定。
3. **`chunk-gc.period-in-ticks`**：Paper 已弃用此机制，可忽略；纯 Bukkit/CraftBukkit 保持 `600` 即可。
4. **`settings.timeout-time`**：调到 `30` 可更快检测卡死，但可能误伤短暂卡顿。生产环境保持 `60`。
5. **`auto-updater.enabled=false`**：CraftBukkit 已停止维护，关闭自动更新检查避免无用网络请求。Spigot/Paper 各有更新机制。
6. **`settings.query-plugins=false`**：公网服关闭可避免通过 Query 协议泄露插件列表。
7. **使用 `commands.yml` 而非 `bukkit.yml` 的 `aliases`**：1.13+ 推荐用 `commands.yml`，支持参数转发和大小写忽略，更灵活。
8. **`help.yml` 隐藏管理命令**：通过 `amendments` 给 `/stop`、`/restart` 等敏感命令设置权限，避免普通玩家看到。
9. **`permissions.yml` 仅做聚合**：不要在此文件具体赋权给玩家，用权限插件（LuckPerms）管理。本文件仅定义可复用的权限组。
10. **`settings.restart-on-crash=true`**：无人值守的服务器可开启，并配合 `restart-script` 实现崩溃自动重启。

> 参考来源：[Bukkit Wiki - bukkit.yml](https://bukkit.fandom.com/wiki/Bukkit.yml)、[Bukkit Wiki - permissions.yml](https://bukkit.fandom.com/wiki/Permissions.yml)、[Bukkit Wiki - commands.yml](https://bukkit.fandom.com/wiki/Commands.yml)、[Bukkit Wiki - help.yml](https://bukkit.fandom.com/wiki/Help.yml)。
