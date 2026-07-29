# Sponge 服务器配置文件中文手册

> Sponge 是一套为 Minecraft: Java Edition 设计的现代化、模块化服务端插件 API 与实现（SpongeAPI + SpongeVanilla / SpongeForge）。配置采用 **HOCON** 格式（Human-Optimized Config Object Notation），比 YAML 更宽容、更易读，支持注释。Sponge 通过 Mixin 直接注入原版游戏代码，提供原版服务端无法实现的事件系统、权限服务、命令系统与性能优化。
> 官方 GitHub：https://github.com/SpongePowered/Sponge
> 官方文档：https://docs.spongepowered.org/
> 配置文件路径：`config/sponge/global.conf`（首次启动自动生成，HOCON 格式）
> 全局配置可用特定世界/维度的 `world.conf` 覆盖。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|--------|------|------|------|
| `config/sponge/global.conf` | HOCON | Sponge 自动生成 | 全局主配置，约 90-110 项设置，25 个子节 |
| `config/sponge/<世界名>/world.conf` | HOCON | Sponge 自动生成 | 单个世界级覆盖配置，覆盖 `global.conf` 中的对应项 |
| `config/sponge/plugins/<插件id>.conf` | HOCON | 插件按需生成 | 单个插件的全局配置 |
| `global.conf` 内的 `commands`/`permissions`/`modules`/`optimizations` 等子节 | HOCON | Sponge 自动生成 | 见下文逐节说明 |

> 小白提示：HOCON 用 `#` 写注释，键值用 `=` 分隔，区块用 `{ }` 包裹。所有配置修改后用 `/sponge reload` 重载（部分项需重启，下表已标注）。
> 图例：✅ = `/sponge reload` 即可生效；🔄 = 必须重启服务端才生效。

---

## global.conf（Sponge 全局配置）

> 下表「键名」即为 HOCON 中的完整路径，例如 `modules.entity-activation-range` 表示 `modules { entity-activation-range }` 节点。键名一律保持英文不翻译。

### 全局根设置

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `sponge.target-server-ip` | 目标服务器 IP | string | ``（空） | 🔄 | 仅 SpongeForge/SpongeVanilla 嵌入式部署时使用 |
| `sponge.target-server-port` | 目标服务器端口 | int | `25565` (1-65535) | 🔄 | 嵌入式部署端口 |
| `sponge.plugins-dir` | 插件目录 | string | `mods/plugins` | 🔄 | Sponge 插件搜索目录，可自定义 |
| `sponge.enable-plugins` | 启用插件加载 | bool | `true` (true/false) | 🔄 | `false`=不加载任何 Sponge 插件 |
| `sponge.file-watch-enabled` | 文件监视 | bool | `true` (true/false) | ✅ | 监视配置文件变化以支持热重载 |

### modules（功能模块开关）

> 每个模块都是一组功能的总开关，关闭后该模块下属所有优化/检查都会失效，用于排查兼容性问题。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `modules.block-capturing-control` | 方块捕获控制 | bool | `true` (true/false) | 🔄 | 是否启用方块变更追踪（事务），插件 BlockEvent 依赖此 |
| `modules.bungeecord` | BungeeCord 兼容 | bool | `false` (true/false) | 🔄 | 启用 IP 转发以兼容 BungeeCord/Velocity 代理 |
| `modules.entity-activation-range` | 实体活动范围优化 | bool | `true` (true/false) | 🔄 | 启用按距离降频实体 tick 的优化 |
| `modules.entity-collisions` | 实体碰撞优化 | bool | `true` (true/false) | 🔄 | 启用碰撞频率限制 |
| `modules.exploits` | 漏洞修复 | bool | `true` (true/false) | 🔄 | 修复若干原版漏洞（如附魔/书与笔） |
| `modules.game-fixes` | 游戏修复 | bool | `false` (true/false) | 🔄 | 一些非紧急的游戏性 bug 修复，默认关闭以保原版行为 |
| `modules.optimizations` | 性能优化 | bool | `true` (true/false) | 🔄 | 总开关，关闭后下属所有优化失效 |
| `modules.realtime` | 实时时钟 | bool | `false` (true/false) | ✅ | 用现实时间替代 tick，改善低 TPS 下玩家体验，不提升性能 |
| `modules.tileentity-activation` | 方块实体活动范围 | bool | `false` (true/false) | 🔄 | 按距离降频方块实体 tick，谨慎启用可能破坏模组功能 |
| `modules.timings` | 性能计时 | bool | `true` (true/false) | ✅ | 启用 `/sponge timings` 性能分析 |
| `modules.tracking` | 来源追踪 | bool | `true` (true/false) | 🔄 | 追踪方块/实体变更的因果来源，权限审计依赖此 |

### optimizations（性能优化）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `optimizations.async-lighting.enabled` | 异步光照计算 | bool | `true` (true/false) | 🔄 | 异步线程计算光照，显著降低主线程负担 |
| `optimizations.async-lighting.num-threads` | 光照线程数 | int | `2` (1-64) | 🔄 | 异步光照专用线程数，CPU 核心数较佳 |
| `optimizations.cache-tameable-owners` | 缓存可驯服主 | bool | `true` (true/false) | 🔄 | 缓存驯化动物主人 UUID，避免频繁 DataWatcher 查询 |
| `optimizations.drops-pre-merge` | 掉落物预合并 | bool | `true` (true/false) | ✅ | 生成掉落物前先尝试合并，减少实体数量 |
| `optimizations.panda-redstone` | Panda 红石算法 | bool | `false` (true/false) | ✅ | 替代红石更新算法，减少方块更新次数，可能引入差异 |
| `optimizations.chunk-loading` | 区块加载优化 | bool | `true` (true/false) | 🔄 | 优化区块加载与排队 |
| `optimizations.eject-from-entity` | 实体弹出优化 | bool | `true` (true/false) | ✅ | 优化矿车/船等载具的弹出逻辑 |
| `optimizations.structured-unused-entries` | 清理未用条目 | bool | `true` (true/false) | 🔄 | 清理内部未使用的结构条目 |
| `optimizations.use-partial-block-updates` | 部分方块更新 | bool | `true` (true/false) | 🔄 | 仅更新变化部分方块而非整体 |
| `optimizations.vertex-operation-lighting` | 顶点光照优化 | bool | `false` (true/false) | 🔄 | 实验性顶点级光照优化 |

### block-entity-activation（方块实体活动范围）

> 仅当 `modules.tileentity-activation=true` 时生效。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `block-entity-activation.auto-populate` | 自动填充 | bool | `false` (true/false) | ✅ | 自动把新发现的方块实体加入配置，建议调优后关闭 |
| `block-entity-activation.default-block-range` | 默认方块范围 | int | `256` (0-2147483647) | ✅ | 玩家在此范围内方块实体才 tick |
| `block-entity-activation.default-tick-rate` | 默认 tick 频率 | int | `1` (1-2147483647) | ✅ | 每多少 tick 给方块实体 1 次 tick，值越大越省 CPU |
| `block-entity-activation.mods` | 模组覆盖 | section | `` | ✅ | 按模组 ID 自定义每个方块实体的范围与频率 |

### entity-activation-range（实体活动范围）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `entity-activation-range.auto-populate` | 自动填充 | bool | `false` (true/false) | ✅ | 自动把新发现的实体加入配置 |
| `entity-activation-range.defaults.ambient` | 环境生物范围 | int | `32` (0-2147483647) | ✅ | 蝙蝠等环境生物激活距离，`0`=禁用 |
| `entity-activation-range.defaults.aquatic` | 水生生物范围 | int | `32` (0-2147483647) | ✅ | 鱿鱼等水生生物激活距离 |
| `entity-activation-range.defaults.creature` | 被动动物范围 | int | `32` (0-2147483647) | ✅ | 牛、羊等被动动物激活距离 |
| `entity-activation-range.defaults.misc` | 杂项实体范围 | int | `16` (0-2147483647) | ✅ | 掉落物、经验球等杂项实体激活距离 |
| `entity-activation-range.defaults.monster` | 怪物范围 | int | `32` (0-2147483647) | ✅ | 僵尸、骷髅等怪物激活距离 |
| `entity-activation-range.mods` | 模组覆盖 | section | `` | ✅ | 按模组 ID 自定义每类实体的激活距离 |

### entity-collision（实体碰撞）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `entity-collision.auto-populate` | 自动填充 | bool | `false` (true/false) | ✅ | 自动把新发现的实体加入碰撞配置 |
| `entity-collision.defaults.ambient` | 环境生物碰撞上限 | int | `8` (0-2147483647) | ✅ | 单点同时碰撞的环境生物上限 |
| `entity-collision.defaults.aquatic` | 水生生物碰撞上限 | int | `8` (0-2147483647) | ✅ | 水生生物碰撞上限 |
| `entity-collision.defaults.creature` | 被动动物碰撞上限 | int | `8` (0-2147483647) | ✅ | 被动动物碰撞上限 |
| `entity-collision.defaults.misc` | 杂项实体碰撞上限 | int | `8` (0-2147483647) | ✅ | 杂项实体碰撞上限 |
| `entity-collision.defaults.monster` | 怪物碰撞上限 | int | `8` (0-2147483647) | ✅ | 怪物碰撞上限，调小可减少密集卡顿 |
| `entity-collision.mods` | 模组覆盖 | section | `` | ✅ | 按模组 ID 自定义碰撞上限 |

### entity（实体行为）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `entity.creature-spawn-limit` | 怪物生成上限 | int | `0` (0-2147483647) | ✅ | `0`=沿用原版；正值覆盖原版上限 |
| `entity.human-player-list-allow-bypass-on-max-players` | 玩家列表绕过 | bool | `true` (true/false) | 🔄 | BungeeCord 转发时绕过原版 60 上限 |
| `entity.max-bounding-box-size` | 最大包围盒尺寸 | int | `2000` (0-2147483647) | ✅ | 实体最大碰撞箱尺寸，过大实体被裁剪，防崩 |
| `entity.max-entity-velocity` | 最大实体速度 | double | `100.0` (0-任意) | ✅ | 实体最大速度上限，防止作弊者用速度卡服 |
| `entity.player-block-reach` | 玩家方块触达距离 | double | `5.0` (0-任意) | ✅ | 玩家可破坏/交互方块的最远距离 |
| `entity.player-entity-reach` | 玩家实体触达距离 | double | `5.0` (0-任意) | ✅ | 玩家可攻击/交互实体的最远距离 |

### movement-checks（移动检查）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `movement-checks.auto-orientation` | 自动朝向检查 | bool | `true` (true/false) | ✅ | 检测玩家朝向突变（如反作弊） |
| `movement-checks.invalid-rotation` | 非法旋转检查 | bool | `true` (true/false) | ✅ | 检查旋转角度是否超出合法范围 |
| `movement-checks.moved-wrongly` | 异常移动检查 | bool | `true` (true/false) | ✅ | 检查玩家移动距离是否异常 |
| `movement-checks.moved-too-quickly` | 快速移动检查 | bool | `true` (true/false) | ✅ | 检查玩家移动速度是否过快 |
| `movement-checks.speed-hack` | 速度作弊检查 | bool | `true` (true/false) | ✅ | 检测加速挂 |

### commands（命令设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `commands.multi-world-commands` | 多世界命令 | bool | `true` (true/false) | ✅ | 是否按世界隔离命令权限 |
| `commands.command-aliases` | 命令别名 | section | `` | ✅ | 自定义命令别名映射 |
| `commands.notifications.command` | 命令通知命令名 | string | `sponge` | ✅ | `/sponge` 主命令名 |
| `commands.show-name` | 显示命令名 | bool | `true` (true/false) | ✅ | 帮助列表中是否显示命令名 |

### world（世界设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `world.auto-save-interval` | 世界自动保存间隔 | int | `900` (0-2147483647) | ✅ | 每多少 tick 保存所有区块，`0`=禁用，20 tick=1 秒 |
| `world.auto-player-save-interval` | 玩家数据保存间隔 | int | `900` (0-2147483647) | ✅ | 每多少 tick 保存全局玩家数据，`0`=禁用 |
| `world.game-disable-updates` | 禁用游戏更新 | bool | `false` (true/false) | ✅ | 调试用，禁用游戏内部更新 |
| `world.gen-modifiers` | 生成器修饰符 | list | `[]` | 🔄 | 自定义世界生成修饰符列表 |
| `world.load-on-startup` | 启动时加载 | bool | `true` (true/false) | 🔄 | 服务端启动时是否预加载所有世界 |

### bungeecord（BungeeCord 代理）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `bungeecord.ip-forwarding` | IP 转发 | bool | `false` (true/false) | 🔄 | 启用 BungeeCord/Velocity IP 转发，必须与代理端一致 |
| `bungeecord.online-mode` | 在线模式 | bool | `true` (true/false) | 🔄 | 代理模式下是否做正版验证 |

### ip-forwarding（IP 转发，独立子节）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `ip-forwarding.set-and-verify-ip` | 设置并验证 IP | bool | `false` (true/false) | 🔄 | 严格验证转发请求中的真实 IP 与协议 |
| `ip-forwarding.forward-player-info` | 转发玩家信息 | bool | `false` (true/false) | 🔄 | 转发玩家 UUID/属性到子服 |

### permissions（权限设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `permissions.load-on-startup` | 启动加载权限 | bool | `true` (true/false) | 🔄 | 启动时加载权限服务 |
| `permissions.use-default-permissions` | 使用默认权限 | bool | `true` (true/false) | ✅ | 是否使用 Sponge 内置默认权限 |
| `permissions.default-admin-level` | 默认管理员等级 | int | `4` (0-4) | ✅ | 默认权限等级（4=OP） |

### sql（SQL 数据库）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `sql.enabled` | 启用 SQL | bool | `false` (true/false) | 🔄 | 启用 SQL 数据源 |
| `sql.driver` | 数据库驱动 | string | `org.h2.Driver` | 🔄 | JDBC 驱动类全名 |
| `sql.url` | 数据库 URL | string | `jdbc:h2:./config/sponge/sponge` | 🔄 | JDBC 连接 URL |
| `sql.user` | 数据库用户名 | string | `` | 🔄 | 数据库账号 |
| `sql.password` | 数据库密码 | string | `` | 🔄 | 数据库密码，建议用环境变量替代 |
| `sql.table-prefix` | 表前缀 | string | `` | 🔄 | 数据表名前缀 |

### scheduler（调度器）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `scheduler.parallel-limit` | 并发任务上限 | int | `8` (1-2147483647) | ✅ | 异步任务并发上限 |
| `scheduler.max-thread-size` | 最大线程数 | int | `4` (1-2147483647) | 🔄 | 调度线程池最大线程数 |

### logging（日志设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `logging.log-block-break` | 记录方块破坏 | bool | `false` (true/false) | ✅ | 控制台输出方块破坏事件 |
| `logging.log-block-place` | 记录方块放置 | bool | `false` (true/false) | ✅ | 控制台输出方块放置事件 |
| `logging.log-stacktraces` | 记录堆栈 | bool | `false` (true/false) | ✅ | 输出异常堆栈用于调试 |
| `logging.debug` | 调试日志 | list | `[]` | ✅ | 启用指定调试分类（如 `["chunk-load"]`） |

### exploits（漏洞修复）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `exploits.book-large-size` | 书本大小限制 | bool | `true` (true/false) | ✅ | 限制书本内容大小，防崩服 |
| `exploits.item-signature` | 物品签名检查 | bool | `true` (true/false) | ✅ | 检查物品 NBT 签名是否合法 |
| `exploits.sign-command` | 告示牌命令限制 | bool | `true` (true/false) | ✅ | 限制告示牌可执行的命令 |
| `exploits.sign-long-lines` | 告示牌长行限制 | bool | `true` (true/false) | ✅ | 限制告示牌每行字符数 |

### general（通用设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `general.disable-warnings` | 禁用警告 | bool | `false` (true/false) | ✅ | 关闭控制台部分警告 |
| `general.hide-online-players` | 隐藏在线玩家 | bool | `false` (true/false) | ✅ | 不向客户端发送完整玩家列表 |
| `general.disable-flush-saving` | 禁用刷盘保存 | bool | `false` (true/false) | ✅ | 关闭定时全量刷盘，仅增量保存 |
| `general.death-message-style` | 死亡消息风格 | string | `default` (default/none/raw) | ✅ | 死亡消息显示风格 |

### debug（调试设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `debug.thread-contention-monitoring` | 线程竞争监视 | bool | `false` (true/false) | ✅ | 启用线程竞争检测 |
| `debug.reload-internal` | 内部重载 | bool | `true` (true/false) | ✅ | 允许 `/sponge reload` 重载内部状态 |
| `debug.synchronize-chunk-writes` | 同步区块写入 | bool | `true` (true/false) | 🔄 | 区块写入是否同步 |

### phase-tracking（阶段追踪）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `phase-tracking.verbose-logging` | 详细追踪日志 | bool | `false` (true/false) | ✅ | 输出详细阶段追踪信息 |
| `phase-tracking.capture-block` | 捕获方块 | section | `` | ✅ | 方块捕获配置 |
| `phase-tracking.capture-entity` | 捕获实体 | section | `` | ✅ | 实体捕获配置 |
| `phase-tracking.capture-sponge` | 捕获 Sponge | section | `` | ✅ | Sponge 内部事件捕获 |

### timings（性能计时）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `timings.enabled` | 启用 timings | bool | `true` (true/false) | ✅ | 启用 `/sponge timings` |
| `timings.verbose` | 详细模式 | bool | `true` (true/false) | ✅ | 输出更详细的计时数据 |
| `timings.cost-ignored` | 忽略成本 | bool | `true` (true/false) | ✅ | 忽略微小成本计时 |
| `timings.history-interval` | 历史间隔 | int | `300` (10-3600) | ✅ | 多少秒采样一次历史 |
| `timings.history-length` | 历史长度 | int | `3600` (60-21600) | ✅ | 历史总时长（秒） |

### cause-tracker（因果追踪）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `cause-tracker.max-block-processed-per-tick` | 每 tick 最大处理方块 | int | `50000` (1-2147483647) | ✅ | 每 tick 处理的方块事件上限 |
| `cause-tracker.max-block-processed-per-event` | 每事件最大方块 | int | `50000` (1-2147483647) | ✅ | 单个事件处理方块上限 |
| `cause-tracker.report-modified-blocks` | 报告修改方块 | bool | `false` (true/false) | ✅ | 输出修改方块报告 |

---

## 配置示例

```hocon
# config/sponge/global.conf 完整示例
sponge {
    target-server-ip = ""
    target-server-port = 25565
    plugins-dir = "mods/plugins"
    enable-plugins = true
    file-watch-enabled = true

    modules {
        block-capturing-control = true
        bungeecord = false
        entity-activation-range = true
        entity-collisions = true
        exploits = true
        game-fixes = false
        optimizations = true
        # 使用现实时间替代 tick，TPS 低时改善体验，不提升性能
        realtime = false
        # 谨慎启用，可能破坏模组方块实体功能
        tileentity-activation = false
        timings = true
        tracking = true
    }

    optimizations {
        async-lighting {
            enabled = true
            num-threads = 2
        }
        cache-tameable-owners = true
        drops-pre-merge = true
        # 替代红石算法，可能引入差异
        panda-redstone = false
        chunk-loading = true
    }

    entity-activation-range {
        auto-populate = false
        defaults {
            ambient = 32
            aquatic = 32
            creature = 32
            misc = 16
            monster = 32
        }
    }

    entity-collision {
        auto-populate = false
        defaults {
            ambient = 8
            aquatic = 8
            creature = 8
            misc = 8
            monster = 8
        }
    }

    world {
        # 20 tick = 1 秒；900 tick = 45 秒
        auto-save-interval = 900
        auto-player-save-interval = 900
    }

    movement-checks {
        moved-wrongly = true
        moved-too-quickly = true
        speed-hack = true
    }

    entity {
        max-bounding-box-size = 2000
        max-entity-velocity = 100.0
    }

    timings {
        enabled = true
        verbose = true
        history-interval = 300
        history-length = 3600
    }
}
```

---

## 优化建议

1. **TPS 下降排查**：先 `/sponge timings on` 跑 5 分钟，再 `/sponge timings paste` 生成报告链接，找出最耗时的插件/事件。
2. **大型模组服**：开启 `modules.tileentity-activation=true` 并把高消耗方块实体（如 IC2 风力发电机）的 `tick-rate` 调到 `10-20`，可省 30%+ CPU。务必先开 `auto-populate=true` 自动收集，调优后改回 `false`。
3. **实体密集卡顿**：把 `entity-collision.defaults.monster` 降到 `2-3`，立竿见影缓解拥挤生物群落的碰撞计算。
4. **网络优化**：`entity-activation-range.defaults.monster` 从 `32` 降到 `24`，远端怪物不 tick，玩家几乎无感但 CPU 大降。
5. **代理服**：必须同时设置 `modules.bungeecord=true` 与 `bungeecord.ip-forwarding=true`，并与代理端（BungeeCord/Velocity）的 `ip_forward` 一致，否则玩家 IP 全是代理 IP。
6. **磁盘 IO**：把 `world.auto-save-interval` 从 `900` 提到 `1800-3600`，配合插件做定时备份，可显著降低磁盘写入。
7. **内存吃紧**：关闭 `modules.tracking`（会失去部分审计能力）与 `optimizations.async-lighting` 改用同步（牺牲 CPU 换内存）。
8. **低 TPS 抢救**：临时开启 `modules.realtime=true`，让玩家在卡顿时仍能正常成长/破坏，争取排查时间。
9. **不要碰**：`entity.max-bounding-box-size` 太小会让大型实体（如末影龙）崩溃；`entity.max-entity-velocity` 太大放行速度挂，太小误伤发射器。

> 排查清单：玩家互相看不见 → 查 `entity-activation-range`；区块不刷新 → 查 `optimizations.chunk-loading` 与 `cause-tracker.max-block-processed-per-tick`；命令权限错乱 → 查 `permissions.*` 与 `commands.multi-world-commands`。
