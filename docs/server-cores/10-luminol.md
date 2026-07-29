# Luminol 服务器配置文件中文手册

> Luminol 是基于 Folia 的优化分支，由 LuminolMC 团队开发，提供可配置的原版特性、Tpsbar、线性区域文件格式等增强。
> 继承关系：Vanilla → Spigot → Paper → Folia → Luminol
> 官方 GitHub：https://github.com/LuminolMC/Luminol
> 官方网站：https://luminolmc.com

⚠️ **重要提示**：Luminol 仓库已归档（Public Archive），但仍在使用中。后续维护由社区分支 LightingLuminol / Lophine 接续。使用 Luminol 之前，**必须**先了解 Folia 服务端的多线程区域化特性：传统 Paper 插件大多不兼容 Folia，需要专门适配。

Luminol 在 Folia 基础上叠加了：可配置的原版特性（如允许刷沙）、Tpsbar 支持、线性区域文件格式（来自 Kaiiju）、对单区域性能优化、更多插件开发 API 支持。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置 |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置 |
| paper-global.yml | YAML | Paper 继承 | Paper 全局配置 |
| paper-world-defaults.yml | YAML | Paper 继承 | Paper 世界默认配置 |
| folia.yml | YAML | Folia 继承 | Folia 多线程区域配置 |
| **luminol_config/luminol_global_config.toml** | **TOML** | **Luminol 专属** | Luminol 全局独有配置（本文档重点） |

> 说明：Luminol 完整继承 Folia + Paper 的全部配置体系，本文档仅聚焦 Luminol 独有的 `luminol_global_config.toml`。其余配置请参阅对应手册。
> 注意：Luminol 使用 **TOML** 格式（不同于多数服务端的 YAML），文件位于 `luminol_config/` 目录下。

## luminol_config/luminol_global_config.toml（Luminol 全局专属配置）

文件位于服务器根目录的 `luminol_config/` 子目录下，使用 **TOML 格式**。所有配置在服务器启动时读取，部分支持热重载（标注 🔄 的项可通过 `/luminol reload` 重载）。

### 阅读约定

- **键名**：保持原样不翻译，采用 TOML 节路径格式（如 `[misc.server_mod_name] name`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `double` 浮点 / `string` 字符串 / `string[]` 字符串列表 / `enum` 枚举。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器；🔄 表示支持 `/luminol reload` 热重载。

---

### 1. 服务器品牌重写（[misc.server_mod_name]）

> 控制服务器在网络协议中自报的「Mod 名称」。原版服务器会发送 `vanilla`，使用本节可伪装成原版或自定义品牌。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[misc.server_mod_name] name` | 服务器 Mod 名称 | string | `Luminol`（任意字符串） | 🔄 | 玩家按 F3 看到的服务器 Mod 名称。设为 `vanilla` 可伪装成原版服务器（用于绕过部分客户端 Mod 的服务端检测）。 |
| `[misc.server_mod_name] vanilla_spoof` | 原版伪装 | bool | `false`（`true`/`false`） | 🔄 | 是否将服务器在网络协议中伪装成原版 `vanilla`。开启后部分客户端反作弊 Mod 会将服务器视为原版。⚠️ 可能与部分依赖服务端品牌识别的插件冲突。 |

---

### 2. 聊天校验（[misc.chat]）

> 控制聊天消息签名校验行为（1.19.1+ 聊天签名系统）。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[misc.chat] chat_check` | 聊天签名校验 | bool | `true`（`true`/`false`） | 🔄 | 是否校验玩家聊天消息的签名。关闭后服务器不再验证消息签名真伪，可改善离线模式或第三方客户端的聊天兼容性。⚠️ 关闭后无法检测伪造消息。 |
| `[misc.chat] only_aura_real_player` | 仅光环真实玩家 | bool | `false`（`true`/`false`） | 🔄 | 是否只对真实玩家应用光环效果。可用于过滤假人产生的光环。 |

---

### 3. TPS 状态条（[misc.tpsbar]）

> 在玩家屏幕上方显示一个 Boss 条，实时显示服务器 TPS、MSPT 和玩家延迟。这是 Luminol 的标志性功能，类似 Purpur 的 tpsbar。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[misc.tpsbar] enabled` | 启用 Tpsbar | bool | `false`（`true`/`false`） | 🔄 | 是否默认为所有玩家启用 Tpsbar。玩家可用 `/tpsbar` 命令切换个人状态。 |
| `[misc.tpsbar] color` | Boss 条颜色 | enum | `GREEN`（`PINK`/`BLUE`/`RED`/`GREEN`/`YELLOW`/`PURPLE`/`WHITE`） | 🔄 | Tpsbar Boss 条的基础颜色。部分实现会根据 TPS 高低自动切换颜色，此值作为默认 / 最佳状态颜色。 |
| `[misc.tpsbar] style` | Boss 条样式 | enum | `NOTCHED_20`（`PROGRESS`/`NOTCHED_6`/`NOTCHED_10`/`NOTCHED_12`/`NOTCHED_20`） | 🔄 | Boss 条的进度条样式。`NOTCHED_20` 表示分成 20 段（对应 20 TPS）。 |
| `[misc.tpsbar] progress` | 进度来源 | enum | `MSPT`（`TPS`/`MSPT`） | 🔄 | Boss 条进度依据的指标。`TPS`：按每秒 tick 数（0-20 映射到 0-100%）；`MSPT`：按每 tick 毫秒数（0-50ms 映射到 100%-0%）。 |
| `[misc.tpsbar] text` | 显示文本模板 | string | 默认含 TPS/MSPT/Ping 占位符 | 🔄 | Boss 条上显示的文本模板，支持占位符（如 `%tps%`、`%mspt%`、`%ping%`）。 |
| `[misc.tpsbar] ping_color_list` | 延迟颜色梯度 | string[] | 见说明 | 🔄 | 根据玩家延迟（ping）显示不同颜色的阈值列表。常见格式：`[{"color":"GREEN","threshold":100},{"color":"YELLOW","threshold":200},{"color":"RED","threshold":300}]`，表示 ping < 100ms 绿色、< 200ms 黄色、≥ 300ms 红色。 |
| `[misc.tpsbar] tps_color_list` | TPS 颜色梯度 | string[] | 见说明 | 🔄 | 根据服务器 TPS 显示不同颜色的阈值列表。常见格式：`[{"color":"GREEN","threshold":19.0},{"color":"YELLOW","threshold":15.0},{"color":"RED","threshold":0}]`。 |
| `[misc.tpsbar] mspt_color_list` | MSPT 颜色梯度 | string[] | 见说明 | 🔄 | 根据服务器 MSPT（每 tick 毫秒数）显示不同颜色的阈值列表。常见格式：`[{"color":"GREEN","threshold":40},{"color":"YELLOW","threshold":50},{"color":"RED","threshold":100}]`。 |

---

### 4. 原版特性修复（[fixes]）

> Luminol 通过补丁方式修复或恢复部分原版特性。本节控制各项修复的开启状态，**修改前请充分了解其影响**。

#### 4.1 虚空交易（[fixes.allow_void_trade]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[fixes.allow_void_trade] enabled` | 允许虚空交易 | bool | `false`（`true`/`false`） | 🔄 | 是否允许玩家在虚空（y < 0 或维度外）与村民交易。原版默认禁止，开启后可恢复早期版本的虚空交易行为。 |

#### 4.2 刷沙 / 刷沙砾（[fixes.sand_duplication]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[fixes.sand_duplication] enabled` | 允许刷沙 | bool | `false`（`true`/`false`） | 🔄 | 是否恢复原版的沙子 / 沙砾 duplication bug（沙子落入末地传送门时复制）。生电玩家常用。⚠️ 会破坏服务器经济平衡，谨慎开启。 |

#### 4.3 刷沙砾（[fixes.gravel_duplication]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[fixes.gravel_duplication] enabled` | 允许刷沙砾 | bool | `false`（`true`/`false`） | 🔄 | 同上，针对沙砾的 duplication bug。 |

#### 4.4 末影龙逃逸修复（[fixes.ender_dragon_escape_fix]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[fixes.ender_dragon_escape_fix] enabled` | 末影龙逃逸修复 | bool | `false`（`true`/`false`） | 🔄 | 是否修复末影龙飞出末地主岛边界的 bug。开启后末影龙将被限制在末地中心区域活动。 |

#### 4.5 实体挤压（[fixes.entity_collision]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[fixes.entity_collision] enabled` | 实体挤压修复 | bool | `false`（`true`/`false`） | 🔄 | 是否修复多个实体挤压进入同一方块导致的崩溃 / 推动异常。开启后实体的挤压行为更接近原版。 |

#### 4.6 TNT 实体复制（[fixes.tnt_duplication]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[fixes.tnt_duplication] enabled` | 允许 TNT 复制 | bool | `false`（`true`/`false`） | 🔄 | 是否恢复原版 TNT duplication bug（TNT 在传送门 / 活塞推动时复制）。生电玩家常用。⚠️ 易被滥用，谨慎开启。 |

---

### 5. 性能优化（[performance]）

#### 5.1 区域 Tick 优化（[performance.region_tick]）

> Folia 将世界划分为多个独立区域，每个区域由独立线程 tick。本节控制区域 tick 的优化策略。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[performance.region_tick] optimize_tick_occupancy` | 优化 Tick 占用率 | bool | `true`（`true`/`false`） | ✅ | 是否优化区域 tick 任务的线程分配，让空闲线程接管更多区域任务。开启后可提升多核 CPU 利用率。 |
| `[performance.region_tick] max_tick_time` | 单区域最大 tick 时长 | int | `50`（≥ 1，单位：毫秒） | ✅ | 单个区域单次 tick 允许的最大耗时。超出此值的区域将被记录警告。50ms 对应 20 TPS 上限。 |

#### 5.2 区块加载优化（[performance.chunk_load]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[performance.chunk_load] async_chunk_load` | 异步区块加载 | bool | `true`（`true`/`false`） | ✅ | 是否启用异步区块加载（Folia 默认已异步，此项为额外的优化开关）。 |
| `[performance.chunk_load] max_chunk_load_per_tick` | 每 tick 最大加载区块数 | int | `100`（≥ 0） | ✅ | 每个游戏 tick 内最多加载多少个区块，避免瞬间加载大量区块导致卡顿。 |

---

### 6. 区域文件格式（[regions]）

> Luminol 支持线性区域文件格式（Linear Region File Format），来自 Kaiiju。相比原版 MCA 格式，线性格式可将区块文件大小减少 50-70%，并提升 IO 性能。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[regions] use_linear_region` | 使用线性区域文件 | bool | `false`（`true`/`false`） | ✅ | 是否启用线性区域文件格式（.linear）。开启后新创建的区块将以线性格式存储。⚠️ 已有 MCA 文件需通过工具转换，或保持关闭以兼容。 |
| `[regions] linear_region_compression` | 线性区域压缩算法 | enum | `ZSTD`（`ZSTD`/`GZIP`/`NONE`） | ✅ | 线性区域文件使用的压缩算法。`ZSTD`（推荐）：压缩率高、解压快；`GZIP`：兼容性好；`NONE`：不压缩（最大文件，最快读写）。 |
| `[regions] linear_region_buffer_size` | 线性区域缓冲区大小 | int | `1048576`（≥ 0，单位：字节；1MB = 1048576） | ✅ | 线性区域文件读写缓冲区大小。较大的缓冲区可减少 IO 次数但占用更多内存。 |
| `[regions] mca_region_auto_convert` | 自动转换 MCA 区域 | bool | `false`（`true`/`false`） | ✅ | 是否在加载时自动将旧的 MCA 区域文件转换为线性格式。开启后服务器启动可能较慢。 |

---

### 7. 命令权限（[commands]）

> 控制部分 Luminol 专有命令的权限默认值。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[commands] luminol_reload_permission` | `/luminol reload` 权限 | string | `luminol.reload`（权限节点字符串） | 🔄 | 执行 `/luminol reload` 命令所需的权限节点。 |
| `[commands] tpsbar_permission` | `/tpsbar` 权限 | string | `luminol.tpsbar`（权限节点字符串） | 🔄 | 执行 `/tpsbar` 命令所需的权限节点。 |
| `[commands] region_permission` | 区域信息命令权限 | string | `luminol.region`（权限节点字符串） | 🔄 | 执行区域相关查询命令所需的权限节点。 |

---

### 8. 杂项（[misc]）

#### 8.1 区域消息（[misc.region_messages]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[misc.region_messages] show_region_info_on_join` | 进服显示区域信息 | bool | `false`（`true`/`false`） | 🔄 | 玩家进入服务器时是否在聊天框显示其当前所在区域的信息（区域 ID、tick 频率等）。适合调试。 |

#### 8.2 安全设置（[misc.security]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[misc.security] disable_book_exploit` | 禁用书本漏洞 | bool | `true`（`true`/`false`） | 🔄 | 是否禁用书本复制 / 注入漏洞。建议保持开启。 |
| `[misc.security] max_book_pages` | 书本最大页数 | int | `100`（≥ 1） | 🔄 | 单本书允许的最大页数，防止恶意玩家发送超大书本导致卡顿。 |
| `[misc.security] max_book_chars` | 书本最大字符数 | int | `50000`（≥ 1） | 🔄 | 单本书允许的最大字符总数。 |

#### 8.3 调试（[misc.debug]）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `[misc.debug] enable_region_debug` | 启用区域调试 | bool | `false`（`true`/`false`） | 🔄 | 是否输出区域 tick / 调度的详细调试日志。仅排查问题时开启，正常使用请关闭。 |
| `[misc.debug] enable_thread_debug` | 启用线程调试 | bool | `false`（`true`/`false`） | 🔄 | 是否输出线程池调度的详细调试日志。 |

---

## 配置示例

### 标准生存服（启用 Tpsbar + 性能优化）

```toml
# luminol_config/luminol_global_config.toml

[misc.server_mod_name]
name = "Luminol"
vanilla_spoof = false

[misc.chat]
chat_check = true
only_aura_real_player = false

[misc.tpsbar]
enabled = true
color = "GREEN"
style = "NOTCHED_20"
progress = "MSPT"
text = "&7TPS&8: &a%tps% &7MSPT&8: &a%mspt% &7Ping&8: &a%ping%&7ms"

[[misc.tpsbar.ping_color_list]]
color = "GREEN"
threshold = 100

[[misc.tpsbar.ping_color_list]]
color = "YELLOW"
threshold = 200

[[misc.tpsbar.ping_color_list]]
color = "RED"
threshold = 300

[[misc.tpsbar.tps_color_list]]
color = "GREEN"
threshold = 19.0

[[misc.tpsbar.tps_color_list]]
color = "YELLOW"
threshold = 15.0

[[misc.tpsbar.tps_color_list]]
color = "RED"
threshold = 0.0

[performance.region_tick]
optimize_tick_occupancy = true
max_tick_time = 50

[performance.chunk_load]
async_chunk_load = true
max_chunk_load_per_tick = 100

[regions]
use_linear_region = false  # 老服建议保持 false 避免转换开销
linear_region_compression = "ZSTD"

[misc.security]
disable_book_exploit = true
max_book_pages = 100
max_book_chars = 50000
```

### 生电服（启用刷沙 + TNT 复制）

```toml
# luminol_config/luminol_global_config.toml

[misc.server_mod_name]
name = "Luminol"
vanilla_spoof = false

[misc.tpsbar]
enabled = true

[fixes.sand_duplication]
enabled = true  # 启用刷沙

[fixes.gravel_duplication]
enabled = true  # 启用刷沙砾

[fixes.tnt_duplication]
enabled = true  # 启用 TNT 复制

[fixes.allow_void_trade]
enabled = false  # 虚空交易按需开启

[performance.region_tick]
optimize_tick_occupancy = true
```

### 高性能大服（启用线性区域 + 多线程优化）

```toml
# luminol_config/luminol_global_config.toml

[misc.server_mod_name]
name = "Luminol"
vanilla_spoof = false

[misc.tpsbar]
enabled = true
progress = "MSPT"

[performance.region_tick]
optimize_tick_occupancy = true
max_tick_time = 50

[performance.chunk_load]
async_chunk_load = true
max_chunk_load_per_tick = 200  # 高配可调大

[regions]
use_linear_region = true  # 启用线性区域，节省 50-70% 磁盘空间
linear_region_compression = "ZSTD"
linear_region_buffer_size = 2097152  # 2MB 缓冲区
mca_region_auto_convert = true  # 自动转换旧文件

[misc.debug]
enable_region_debug = false
enable_thread_debug = false
```

---

## 常见问题

### Q1：Luminol 仓库已归档，还能用吗？
A：可以。Luminol 本身仍可正常使用，只是不再接收新功能更新。安全更新和 bug 修复由社区分支 LightingLuminol 和 Lophine 接续。新开服建议直接使用 LightingLuminol 或 Lophine。

### Q2：Tpsbar 怎么单独给某个玩家关闭？
A：玩家可在游戏内执行 `/tpsbar` 命令切换个人 Tpsbar 显示状态。该命令默认权限为 `luminol.tpsbar`，所有玩家可执行。

### Q3：开启了线性区域文件后能切回 MCA 吗？
A：可以，但需要使用转换工具（如 Regionerator 或专门的 linear → MCA 转换器）。建议先在测试服验证。最安全做法是新建世界时启用线性区域，老服保持 MCA。

### Q4：Folia 不兼容我的插件怎么办？
A：Luminol 虽然是 Folia 分支，但**不解决插件兼容性问题**。需要：
   1. 检查插件是否声明支持 Folia；
   2. 联系插件作者适配；
   3. 使用替代插件；
   4. 改用 Paper / Purpur / Leaf 等单线程核心（如果插件生态更重要）。

### Q5：刷沙 / TNT 复制开启后会被反作弊检测吗？
A：服务器端不会，但部分客户端反作弊 Mod 可能根据服务器品牌识别并发出警告。设置 `vanilla_spoof = true` 可伪装成原版服务器规避部分检测。

### Q6：TOML 格式和 YAML 有什么区别？
A：TOML 用方括号 `[section]` 表示节，用 `key = value` 表示键值对，列表用 `[[section]]` 表示。比 YAML 更严格（不会因缩进产生歧义），但灵活性略低。修改时注意 TOML 语法。

---

## 参考资料

- Luminol 官方 GitHub：https://github.com/LuminolMC/Luminol
- Luminol 官方网站：https://luminolmc.com
- LightingLuminol（社区分支）：https://github.com/LuminolMC/LightingLuminol
- Lophine（社区分支，生电向）：https://github.com/LuminolMC/Lophine
- Folia 官方文档：https://docs.papermc.io/folia
- MineBBS 资源页：https://www.minebbs.com/resources/luminol-mc.7645/
- TOML 格式规范：https://toml.io/cn/
