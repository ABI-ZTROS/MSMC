# PowerNukkitX 服务器配置文件中文手册

> PowerNukkitX（简称 PNX）是 Nukkit 的现代化分支，专注于为 Minecraft 基岩版提供高性能、可扩展的服务端实现。
> 继承关系：Nukkit → PowerNukkit → PowerNukkitX
> 官方 GitHub：https://github.com/PowerNukkitX/PowerNukkitX
> 官方文档：https://docs.powernukkitx.org/

PowerNukkitX 由 PowerNukkitX 团队开发，定位为「富特性、高定制性的基岩版第三方服务端软件」。它采用现代化的 Gradle 模块化架构，原生支持自定义物品/方块/实体、Terra 世界生成器、完整原版命令系统、行为包、教育版特性等，同时保持与 Nukkit 插件生态的兼容。配置体系基于 Okaeri Configs 框架，分为 `powernukkit.yml`（高级设置）和 `server.properties`（基岩版基础设置）两个文件，加载入口位于 `org.powernukkitx.config.ServerSettings`。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | 基岩版 BDS 兼容 | 服务器基础设置（端口、世界、游戏模式等） |
| powernukkit.yml | YAML | PowerNukkitX 专属 | PNX 独有配置（性能、网络、调试等，本文档重点） |
| allowlist.json | JSON | 基岩版继承 | 白名单玩家列表 |
| ops.json | JSON | 基岩版继承 | OP 列表 |

> 说明：PNX 的 `server.properties` 与 Java 版的 `server.properties` 字段完全不同（端口为 UDP、无 spectator、online-mode 指 Xbox Live 验证）。本文档使用 `powernukkit-server.properties` 作为文件名以避免与 Java 版描述符冲突。本手册仅聚焦 PNX 独有的 `powernukkit.yml` 与基岩版 `server.properties`。

## powernukkit.yml（PowerNukkitX 专属配置）

`powernukkit.yml` 位于服务器根目录，由 `org.powernukkitx.config.ServerSettings` 类通过 Okaeri Configs 加载。所有配置项在服务器启动时读取，部分项需重启才能生效。文件由 `ServerSettings` 根节点及 `settings`、`player-settings`、`gameplay-settings`、`misc-settings`、`level-settings`、`chunk-settings`、`network-settings`、`debug-settings`、`performance-settings`、`config` 等子节组成。键名前缀 `pnx.settings.<category>.<field>` 为程序内部使用的国际化键。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `settings.ip`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `float` 浮点 / `string` 字符串 / `string[]` 字符串列表。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载（PNX 当前版本主要支持启动时加载，绝大多数项需重启）。

---

### 1. 基础设置（settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.ip` | 服务器监听 IP | string | `0.0.0.0` | ✅ | 服务器绑定的 IPv4 地址。`0.0.0.0` 表示监听所有网卡；多网卡环境下可指定具体 IP。 |
| `settings.port` | 服务器端口（UDP） | int | `19132`（1–65535） | ✅ | 服务器监听的 UDP 端口。⚠️ 基岩版使用 UDP，路由器端口转发必须选 UDP 协议。 |
| `settings.maxplayers` | 最大玩家数 | int | `20`（≥ 1） | ✅ | 服务器同时允许的最大玩家数。 |
| `settings.defaultlevel` | 默认世界名 | string | `world` | ✅ | 玩家首次进服默认进入的世界名称。 |
| `settings.allowlist` | 启用白名单 | bool | `false`（`true`/`false`） | ✅ | 是否启用白名单，启用后仅 `allowlist.json` 中的玩家可加入。 |
| `settings.allowlist.message` | 白名单拒绝消息 | string | `Server is white-listed` | ✅ | 玩家被白名单拒绝时显示的提示文本。 |
| `settings.motd` | 服务器 MOTD | string | `PowerNukkitX Server` | ✅ | 服务器在客户端服务器列表中显示的名称。可使用 `§` 颜色码。 |
| `settings.sub-motd` | 子 MOTD | string | `powernukkitx.org` | ✅ | 服务器副标题，部分客户端在 MOTD 下方显示。 |
| `settings.language` | 服务器语言 | string | `eng` | ✅ | 控制台与提示消息使用的语言代码（如 `eng` 英语、`chs` 简中、`cht` 繁中、`jpn` 日语、`rus` 俄语等）。 |
| `settings.forcetranslate` | 强制使用服务器语言 | bool | `false`（`true`/`false`） | ✅ | `true` 时所有字符串按服务器语言翻译后发送给客户端；`false` 时让客户端自行处理本地化。 |
| `settings.safespawn` | 安全出生 | bool | `true`（`true`/`false`） | ✅ | 是否在玩家首次进服时寻找安全位置出生（防止卡在方块中）。 |
| `settings.autosave` | 自动保存 | bool | `true`（`true`/`false`） | ✅ | 是否启用自动保存（间隔由 `autosaveDelay` 控制）。 |
| `settings.autosaveDelay` | 自动保存间隔 | int | `6000`（≥ 0，单位：tick） | ✅ | 自动保存的间隔（20 tick = 1 秒，6000 = 5 分钟）。`0` 禁用自动保存（不推荐）。 |
| `settings.saveunknownblock` | 保存未知方块 | bool | `true`（`true`/`false`） | ✅ | 是否在 NBT 中保存 PNX 无法识别的方块（用于行为包扩展兼容）。 |
| `settings.xboxauth` | Xbox Live 验证 | bool | `true`（`true`/`false`） | ✅ | 是否要求所有玩家通过 Xbox Live 认证。公网服务器强烈建议开启。 |

---

### 2. 玩家设置（player-settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `player-settings.saveplayerdata` | 保存玩家数据 | bool | `true`（`true`/`false`） | ✅ | `true` 时玩家数据保存为 `players/<UUID>.dat`。 |
| `player-settings.skinchangecooldown` | 皮肤更换冷却 | int | `30`（≥ 0，单位：秒） | ✅ | 玩家两次更换皮肤之间的冷却时间。`0` = 无冷却。 |
| `player-settings.forceskintrusted` | 强制可信皮肤 | bool | `false`（`true`/`false`） | ✅ | `true` 时仅使用可信（Xbox Live）的皮肤。 |
| `player-settings.checkmovement` | 校验玩家移动 | bool | `true`（`true`/`false`） | ✅ | 是否启用服务器端玩家移动校验（反作弊）。 |
| `player-settings.rotationupdatethreshold` | 旋转更新阈值 | float | `1`（≥ 0） | ✅ | 玩家旋转角度变化超过此值才发送更新，降低网络包频率。 |
| `player-settings.movementdistancethreshold` | 移动距离阈值 | float | `0.1`（≥ 0） | ✅ | 玩家位移超过此值才发送位置更新。 |
| `player-settings.spawnRadius` | 出生保护半径 | int | `16`（≥ 0，单位：方块） | ✅ | 出生点周围此半径内的方块受到保护，非 OP 玩家无法破坏。 |

---

### 3. 游戏玩法设置（gameplay-settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `gameplay-settings.enablecommandblocks` | 启用命令方块 | bool | `true`（`true`/`false`） | ✅ | 是否允许使用命令方块。 |
| `gameplay-settings.allowbeta` | 允许 Beta 客户端 | bool | `false`（`true`/`false`） | ✅ | 是否允许 Beta 版本客户端连接。 |
| `gameplay-settings.enableredstone` | 启用红石 | bool | `true`（`true`/`false`） | ✅ | 是否启用红石系统。 |
| `gameplay-settings.tickRedstone` | 红石每 tick 处理 | bool | `true`（`true`/`false`） | ✅ | 是否每 tick 都处理红石信号。关闭后红石仍工作但更新频率降低。 |
| `gameplay-settings.viewDistance` | 视野距离 | int | `8`（≥ 5，单位：区块） | ✅ | 玩家可见的区块半径。值越大带宽和内存占用越高。 |
| `gameplay-settings.achivements` | 启用成就 | bool | `true`（`true`/`false`） | ✅ | 是否启用成就/进度系统。 |
| `gameplay-settings.announceAchievements` | 广播成就 | bool | `true`（`true`/`false`） | ✅ | 玩家解锁成就时是否在聊天栏广播。 |
| `gameplay-settings.spawnProtection` | 出生保护半径 | int | `16`（≥ 0） | ✅ | 出生点保护半径（方块），非 OP 玩家无法在此范围内破坏。 |
| `gameplay-settings.gamemode` | 默认游戏模式 | int | `0`（`0`/`1`/`2`） | ✅ | 新玩家默认游戏模式。`0`=生存 / `1`=创造 / `2`=冒险。⚠️ 基岩版无 spectator！ |
| `gameplay-settings.forceGamemode` | 强制游戏模式 | bool | `false`（`true`/`false`） | ✅ | `true` 时玩家进服始终被强制设置为 `gamemode` 指定的模式。 |
| `gameplay-settings.hardcore` | 极限模式 | bool | `false`（`true`/`false`） | ✅ | 是否启用极限模式（玩家死亡后封禁）。 |
| `gameplay-settings.pvp` | 启用 PvP | bool | `true`（`true`/`false`） | ✅ | 是否允许玩家间伤害。 |
| `gameplay-settings.difficulty` | 难度 | int | `1`（`0`/`1`/`2`/`3`） | ✅ | 世界难度。`0`=和平 / `1`=简单 / `2`=普通 / `3`=困难。 |
| `gameplay-settings.allowNether` | 启用下界 | bool | `true`（`true`/`false`） | ✅ | 是否加载下界维度。 |
| `gameplay-settings.allowEnd` | 启用末地 | bool | `true`（`true`/`false`） | ✅ | 是否加载末地维度。 |
| `gameplay-settings.forceResources` | 强制资源包 | bool | `false`（`true`/`false`） | ✅ | `true` 时玩家必须接受服务器资源包才能进服。 |
| `gameplay-settings.allowClientPacks` | 允许客户端资源包 | bool | `true`（`true`/`false`） | ✅ | 是否允许玩家使用客户端自带资源包。 |
| `gameplay-settings.allowVibrantVisuals` | 允许 Vibrant Visuals | bool | `true`（`true`/`false`） | ✅ | 是否允许客户端使用「鲜明视觉」图形选项。 |
| `gameplay-settings.experiments` | 实验特性 | string[] | `["data_driven_vanilla_blocks_and_items"]` | ✅ | 启用的实验性特性 ID 列表（如自定义方块、实验性玩法等）。 |
| `gameplay-settings.cacheStructures` | 缓存结构 | bool | `false`（`true`/`false`） | ✅ | 是否缓存世界生成结构以加速加载（占用内存）。 |
| `gameplay-settings.enableEdu` | 教育版特性 | bool | `false`（`true`/`false`） | ✅ | 是否启用 Minecraft 教育版特性（化学、NPC 等）。 |
| `gameplay-settings.muteEmoteAnnouncements` | 静默表情广播 | bool | `false`（`true`/`false`） | ✅ | 是否屏蔽玩家使用表情时的聊天栏广播。 |
| `gameplay-settings.enablemobai` | 启用生物 AI | bool | `true`（`true`/`false`） | ✅ | 是否启用实体 AI（寻路、行为）。 |
| `gameplay-settings.enableRecipes` | 启用配方 | bool | `true`（`true`/`false`） | ✅ | 是否启用合成配方解锁。 |
| `gameplay-settings.enableCreativeInventory` | 启用创造物品栏 | bool | `true`（`true`/`false`） | ✅ | 是否启用创造模式物品栏。 |
| `gameplay-settings.enableDaylightCycle` | 启用日夜循环 | bool | `true`（`true`/`false`） | ✅ | 是否启用日夜循环。 |
| `gameplay-settings.enableWeather` | 启用天气 | bool | `true`（`true`/`false`） | ✅ | 是否启用天气变化（雨、雷暴）。 |
| `gameplay-settings.enableEntitySpawning` | 启用实体生成 | bool | `true`（`true`/`false`） | ✅ | 是否允许自然生成实体（怪物、动物）。 |
| `gameplay-settings.enableBlockRandomTicking` | 启用方块随机 tick | bool | `true`（`true`/`false`） | ✅ | 是否启用方块随机 tick（作物生长、草地蔓延等）。 |
| `gameplay-settings.enableLiquidFlow` | 启用液体流动 | bool | `true`（`true`/`false`） | ✅ | 是否启用液体（水、熔岩）流动。 |
| `gameplay-settings.enableItemDrops` | 启用物品掉落 | bool | `true`（`true`/`false`） | ✅ | 是否启用方块破坏后的物品掉落。 |
| `gameplay-settings.enableXpOrbs` | 启用经验球 | bool | `true`（`true`/`false`） | ✅ | 是否启用经验球实体。 |
| `gameplay-settings.enableExplosionBlockDamage` | 启用爆炸破坏 | bool | `true`（`true`/`false`） | ✅ | 爆炸是否对方块造成破坏。 |
| `gameplay-settings.enableBlockGravity` | 启用方块重力 | bool | `true`（`true`/`false`） | ✅ | 是否启用受重力影响的方块（沙子、砂砾）。 |
| `gameplay-settings.enableHunger` | 启用饥饿值 | bool | `true`（`true`/`false`） | ✅ | 是否启用玩家饥饿值系统。 |

---

### 4. 杂项设置（misc-settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc-settings.shutdownmessage` | 关服提示消息 | string | `Server closed` | ✅ | 服务器关闭时踢出玩家显示的提示文本。 |
| `misc-settings.installspark` | 安装 Spark | bool | `false`（`true`/`false`） | ✅ | 是否自动下载并加载 Spark 性能分析插件。 |
| `misc-settings.bypassapicheck` | 跳过 API 版本检查 | bool | `false`（`true`/`false`） | ✅ | `true` 时跳过插件对 PNX API 版本的兼容性检查（不推荐生产环境使用）。 |
| `misc-settings.overrideserverauthblockbreaking` | 覆盖服务器权威破坏 | bool | `false`（`true`/`false`） | ✅ | `true` 时覆盖基岩版 `server-authoritative-block-breaking` 字段，强制启用服务器权威校验。 |
| `misc-settings.enablemetrics` | 启用统计上报 | bool | `true`（`true`/`false`） | ✅ | 是否向 PNX bStats 上报匿名统计数据。 |

---

### 5. 世界设置（level-settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `level-settings.levelthread` | 每世界独立线程 | bool | `false`（`true`/`false`） | ✅ | `true` 时每个世界使用独立线程运行（PNX 多线程模型）。开启可提升多世界性能但可能引发同步问题。 |
| `level-settings.autotickrate` | 自动调节 tick 频率 | bool | `true`（`true`/`false`） | ✅ | 服务器卡顿时自动降低 tick 频率以维持稳定。 |
| `level-settings.autotickratelimit` | 自动降频上限 | int | `20`（≥ 1） | ✅ | 自动降频的最大倍率，避免 tick 速率被降到不可接受的程度。 |
| `level-settings.basetickrate` | 基础 tick 频率 | int | `1`（≥ 1） | ✅ | 基础 tick 倍率。`1` = 20 TPS（原版）；`2` = 10 TPS（半速）。 |
| `level-settings.alwaystickplayers` | 每 tick 都处理玩家 | bool | `false`（`true`/`false`） | ✅ | `true` 时无论其他设置如何，每个 tick 都处理玩家逻辑。 |
| `level-settings.loadalllevels` | 加载所有世界 | bool | `true`（`true`/`false`） | ✅ | 启动时是否加载所有已注册的世界。 |
| `level-settings.chunkunloaddelay` | 区块卸载延迟 | int | `15000`（≥ 0，单位：ms） | ✅ | 区块无人引用后多久才卸载（毫秒）。 |
| `level-settings.entityspawncap` | 实体生成上限 | int | `512`（≥ 0） | ✅ | 单个世界实体数量上限。 |
| `level-settings.fieldofview` | 视场角 | int | `100`（≥ 0） | ✅ | 服务器发送给客户端的视场角（FOV）值。 |
| `level-settings.levelworkerthreads` | 世界工作线程数 | int | `-1`（-1 = 自动） | ✅ | 每个世界的工作线程数。`-1` 表示自动根据 CPU 核心数决定。 |

---

### 6. 区块设置（chunk-settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunk-settings.spawnlimit` | 区块生成上限 | int | `3`（≥ 0） | ✅ | 每 tick 最多生成多少个区块。 |
| `chunk-settings.perticksend` | 每 tick 发送区块数 | int | `32`（≥ 1） | ✅ | 每个 tick 向单个玩家发送多少个区块。值越大玩家加载地形越快但带宽占用越高。 |
| `chunk-settings.spawnthreshold` | 出生前发送区块数 | int | `56`（≥ 1） | ✅ | 玩家进服前至少需要发送多少个区块才能让其出生。 |
| `chunk-settings.chunksperticks` | 每 tick 处理区块数 | int | `-1`（-1 = 自动） | ✅ | 每 tick 处理多少个区块的 tick（实体、红石、作物）。`-1` 表示自动。 |
| `chunk-settings.tickRadius` | 区块 tick 半径 | int | `4`（≥ 1） | ✅ | 玩家周围多少区块半径内会被 tick。值越大 CPU 占用越高。 |
| `chunk-settings.lightupdates` | 启用光照更新 | bool | `true`（`true`/`false`） | ✅ | 是否启用光照计算与更新。 |
| `chunk-settings.clearticklist` | 清空 tick 列表 | bool | `true`（`true`/`false`） | ✅ | 是否在每次 tick 后清空待处理列表。 |
| `chunk-settings.generationqueuesize` | 生成队列上限 | int | `8`（≥ 1） | ✅ | 等待生成的区块队列最大长度。 |
| `chunk-settings.saveGenerated` | 保存生成的区块 | bool | `true`（`true`/`false`） | ✅ | 是否将新生成的区块立即保存到磁盘。 |
| `chunk-settings.convertBDSChunks` | 转换 BDS 区块 | bool | `false`（`true`/`false`） | ✅ | 是否将官方 BDS 服务器生成的区块格式转换为 PNX 格式。 |
| `chunk-settings.disableblockticking` | 禁用方块 tick 列表 | string[] | `[]`（方块命名空间 ID 列表） | ✅ | 不进行随机 tick 的方块 ID 列表（如 `minecraft:grass`）。 |

---

### 7. 网络设置（network-settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `network-settings.queryplugins` | Query 暴露插件列表 | bool | `true`（`true`/`false`） | ✅ | 是否允许通过 GameSpy Query 协议列出已加载插件。公网服务器建议关闭。 |
| `network-settings.compressionlevel` | Zlib 压缩级别 | int | `4`（1–9） | ✅ | 数据包 Zlib 压缩级别。值越大 CPU 占用越高、带宽越省。基岩版推荐 4–6。 |
| `network-settings.zlibprovider` | Zlib 实现提供者 | int | `3`（0–4） | ✅ | Zlib 压缩库的提供者。`0`=Java / `1`=Native / `2`=JNI / `3`=Netty / `4`=System。 |
| `network-settings.snappy` | 启用 Snappy 压缩 | bool | `false`（`true`/`false`） | ✅ | 实验性：使用 Google Snappy 算法替代 Zlib。压缩比低但速度极快。⚠️ 实验功能。 |
| `network-settings.compressionbuffersize` | 压缩缓冲区大小 | int | `1048576`（≥ 0，单位：字节） | ✅ | Zlib 压缩缓冲区大小（默认 1 MB）。 |
| `network-settings.maxdecompresssize` | 最大解压大小 | int | `268435456`（≥ 0，单位：字节） | ✅ | 单个数据包最大解压大小（默认 256 MB），防止恶意超大包攻击。 |
| `network-settings.packetlimit` | 数据包大小上限 | int | `8000`（≥ 0，单位：字节） | ✅ | 单个数据包最大字节数。超过此值的包会被拒绝。 |
| `network-settings.query` | 启用 Query | bool | `true`（`true`/`false`） | ✅ | 是否启用 GameSpy Query 协议（用于服务器列表服务）。 |
| `network-settings.encryption` | 启用网络加密 | bool | `true`（`true`/`false`） | ✅ | 是否启用基岩版网络加密（基于 ECDH 握手）。强烈建议保持 `true`。 |
| `network-settings.logintime` | 检查登录时间 | bool | `false`（`true`/`false`） | ✅ | 是否校验玩家登录用时（防止登录洪水攻击）。 |
| `network-settings.autoflush` | 自动刷新发送缓冲 | bool | `true`（`true`/`false`） | ✅ | 是否自动刷新网络发送缓冲。关闭可省 CPU 但增加延迟。 |
| `network-settings.flushinterval` | 刷新间隔 | int | `10`（≥ 0，单位：tick） | ✅ | 自动刷新发送缓冲的间隔。 |
| `network-settings.maxqueuedbytes` | 最大排队字节数 | int | `67108864`（≥ 0，单位：字节） | ✅ | 单个玩家发送队列最大字节数（默认 64 MB），防止慢速客户端拖垮服务器。 |
| `network-settings.cookiemode` | Cookie 模式 | string | `ACTIVE`（`ACTIVE`/`IGNORE`） | ✅ | 处理基岩版 1.21+ Cookie 的模式。`ACTIVE`=接受并响应 / `IGNORE`=忽略。 |

#### 7.1 速率限制（network-settings.rate-limit）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `network-settings.rate-limit.enabled` | 启用速率限制 | bool | `true`（`true`/`false`） | ✅ | 是否启用网络包速率限制（防洪水攻击）。 |
| `network-settings.rate-limit.maxinboundpersecond` | 每秒入站包上限 | int | `1500`（≥ 0） | ✅ | 单个玩家每秒可发送的最大数据包数。 |
| `network-settings.rate-limit.maxpacketspertick` | 每 tick 包上限 | int | `500`（≥ 0） | ✅ | 单个玩家每 tick 可发送的最大数据包数。 |
| `network-settings.rate-limit.maxcommandsperplayer` | 每秒命令上限 | int | `10`（≥ 0） | ✅ | 单个玩家每秒可执行的命令数。 |
| `network-settings.rate-limit.maxchatperplayer` | 每秒聊天上限 | int | `2`（≥ 0） | ✅ | 单个玩家每秒可发送的聊天消息数。 |
| `network-settings.rate-limit.maxformresponsesperplayer` | 每秒表单响应上限 | int | `20`（≥ 0） | ✅ | 单个玩家每秒可发送的表单（UI）响应数。 |
| `network-settings.rate-limit.maxmovementperplayer` | 每秒移动包上限 | int | `40`（≥ 0） | ✅ | 单个玩家每秒可发送的移动数据包数。 |

#### 7.2 僵尸网络检测（network-settings.botnet）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `network-settings.botnet.enabled` | 启用僵尸网络检测 | bool | `false`（`true`/`false`） | ✅ | 是否启用基于行为分析的僵尸网络检测。 |
| `network-settings.botnet.suspiciousthreshold` | 可疑阈值 | int | `300`（≥ 0） | ✅ | IP 行为评分超过此值视为可疑。 |
| `network-settings.botnet.minsuspiciousips` | 最小可疑 IP 数 | int | `3`（≥ 0） | ✅ | 触发自动封禁所需的最小可疑 IP 数。 |
| `network-settings.botnet.autoblock` | 自动封禁 | bool | `true`（`true`/`false`） | ✅ | 是否在检测到僵尸网络时自动封禁可疑 IP。 |
| `network-settings.botnet.autoblockdurationseconds` | 自动封禁时长 | int | `60`（≥ 0，单位：秒） | ✅ | 自动封禁的持续时长。 |
| `network-settings.botnet.minscore` | 最小评分 | int | `2`（≥ 0） | ✅ | 单个 IP 触发评分的最小行为次数。 |

---

### 8. 调试设置（debug-settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `debug-settings.deprecatedverbose` | 弃用 API 警告 | bool | `true`（`true`/`false`） | ✅ | 插件使用已弃用 API 时是否在控制台打印警告。 |
| `debug-settings.level` | 调试日志级别 | string | `INFO`（`INFO`/`DEBUG`/`TRACE`） | ✅ | 控制台日志详细程度。`INFO`=正常 / `DEBUG`=调试 / `TRACE`=追踪（极大量日志）。 |
| `debug-settings.command` | 启用调试命令 | bool | `false`（`true`/`false`） | ✅ | 是否启用 `/debug` 调试命令。 |
| `debug-settings.packet.mode` | 数据包调试模式 | bool | `false`（`true`/`false`） | ✅ | `false`=忽略数据包日志 / `true`=记录 `packetList` 中指定的数据包。 |
| `debug-settings.packetList` | 数据包白名单 | string[] | `[]`（数据包 ID 列表） | ✅ | 启用 `packet.mode` 时要记录的数据包 ID 列表。 |
| `debug-settings.disableencodinglimits` | 禁用编码限制 | bool | `false`（`true`/`false`） | ✅ | 是否禁用 NBT 编码长度限制（仅调试用，会带来安全风险）。 |

---

### 9. 性能设置（performance-settings）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `performance-settings.asyncworkers` | 异步工作线程数 | string | `auto` | ✅ | AsyncTask 的工作线程数。`auto` 自动检测 CPU 核心数（至少 4）。 |
| `performance-settings.basetps` | 基础 TPS | int | `20`（≥ 1） | ✅ | 服务器目标 TPS（每秒 tick 数）。原版为 20。 |
| `performance-settings.registrycache.enable` | 启用注册表缓存 | bool | `false`（`true`/`false`） | ✅ | 是否在启动时将方块/物品注册表缓存到磁盘以加速下次启动。 |
| `performance-settings.registrycache.path` | 缓存文件路径 | string | `path/to/your/registry_cache.bin` | ✅ | 注册表缓存文件路径。 |
| `performance-settings.forcegcpercentage` | 强制 GC 阈值 | float | `1.0`（0–1） | ✅ | 内存使用率达到此比例时强制触发 GC（`1.0` = 100%，禁用强制 GC）。 |

#### 9.1 冻结数组优化（performance-settings.freeze-array）

> PNX 2.0 引入的「冻结数组」优化：将常量数组包装为不可变版本，便于 JVM 进行内联优化。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `performance-settings.freeze-array.enable` | 启用冻结数组 | bool | `true`（`true`/`false`） | ✅ | 是否启用冻结数组优化。 |
| `performance-settings.freeze-array.slots` | 插槽数 | int | `32` | ✅ | 冻结数组缓存插槽数。 |
| `performance-settings.freeze-array.defaultTemperature` | 默认温度 | int | `32` | ✅ | 冻结数组「温度」参数（用于 LRU 淘汰）。 |
| `performance-settings.freeze-array.freezingPoint` | 冰点 | int | `0` | ✅ | 数组「冻结」的温度阈值。 |
| `performance-settings.freeze-array.boilingPoint` | 沸点 | int | `1024` | ✅ | 数组「沸腾」（频繁使用）的温度阈值。 |
| `performance-settings.freeze-array.absoluteZero` | 绝对零度 | int | `-256` | ✅ | 数组温度下限。 |
| `performance-settings.freeze-array.melting` | 融化速率 | int | `16` | ✅ | 数组温度上升（融化）的速率。 |
| `performance-settings.freeze-array.singleOperation` | 单次操作 | int | `1` | ✅ | 单次访问增加的温度。 |
| `performance-settings.freeze-array.batchOperation` | 批量操作 | int | `32` | ✅ | 批量访问增加的温度。 |

---

## powernukkit-server.properties（基岩版基础设置）

`server.properties` 位于服务器根目录，由 `org.powernukkitx.config.legacy.LegacyServerProperties` 类加载。该文件兼容官方基岩版 BDS 的字段，但与 Java 版 `server.properties` 字段**完全不同**。本手册使用文件名 `powernukkit-server.properties` 以区分 Java 版同名文件。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `motd` | 服务器 MOTD | string | `PowerNukkitX Server` | ✅ | 服务器在客户端列表中显示的名称（与 `powernukkit.yml` 中的 `motd` 同步）。 |
| `sub-motd` | 子 MOTD | string | `powernukkitx.org` | ✅ | 服务器副标题。 |
| `server-port` | IPv4 端口（UDP） | int | `19132`（1–65535） | ✅ | 服务器监听的 IPv4 UDP 端口。⚠️ 必须开放 UDP！ |
| `server-ip` | 服务器 IP | string | `0.0.0.0` | ✅ | 服务器绑定的 IPv4 地址。 |
| `view-distance` | 视野距离 | int | `8`（≥ 5，单位：区块） | ✅ | 玩家可见的区块半径。 |
| `white-list` | 启用白名单 | bool | `false`（`true`/`false`） | ✅ | 是否启用白名单。 |
| `achievements` | 启用成就 | bool | `true`（`true`/`false`） | ✅ | 是否启用成就系统。 |
| `announce-player-achievements` | 广播成就 | bool | `true`（`true`/`false`） | ✅ | 玩家解锁成就时是否在聊天栏广播。 |
| `spawn-protection` | 出生保护半径 | int | `16`（≥ 0） | ✅ | 出生点保护半径（方块）。 |
| `max-players` | 最大玩家数 | int | `20`（≥ 1） | ✅ | 服务器同时允许的最大玩家数。 |
| `allow-flight` | 允许飞行 | bool | `false`（`true`/`false`） | ✅ | 是否允许玩家在生存模式飞行（用于反作弊豁免）。 |
| `spawn-animals` | 生成动物 | bool | `true`（`true`/`false`） | ✅ | 是否自然生成动物。 |
| `spawn-mobs` | 生成怪物 | bool | `true`（`true`/`false`） | ✅ | 是否自然生成怪物。 |
| `gamemode` | 默认游戏模式 | int | `0`（`0`/`1`/`2`） | ✅ | 新玩家默认游戏模式。`0`=生存 / `1`=创造 / `2`=冒险。 |
| `force-gamemode` | 强制游戏模式 | bool | `false`（`true`/`false`） | ✅ | 玩家进服是否被强制设置为 `gamemode` 模式。 |
| `hardcore` | 极限模式 | bool | `false`（`true`/`false`） | ✅ | 是否启用极限模式。 |
| `pvp` | 启用 PvP | bool | `true`（`true`/`false`） | ✅ | 是否允许玩家间伤害。 |
| `difficulty` | 难度 | int | `1`（`0`/`1`/`2`/`3`） | ✅ | 世界难度。`0`=和平 / `1`=简单 / `2`=普通 / `3`=困难。 |
| `level-name` | 世界名称 | string | `world` | ✅ | 主世界文件夹名称。 |
| `level-seed` | 世界种子 | string | （空 = 随机） | ✅ | 世界生成种子。相同种子生成相同地形。 |
| `allow-nether` | 启用下界 | bool | `true`（`true`/`false`） | ✅ | 是否加载下界维度。 |
| `allow-the_end` | 启用末地 | bool | `true`（`true`/`false`） | ✅ | 是否加载末地维度。 |
| `enable-query` | 启用 Query | bool | `true`（`true`/`false`） | ✅ | 是否启用 GameSpy Query 协议。 |
| `enable-rcon` | 启用 RCON | bool | `false`（`true`/`false`） | ✅ | 是否启用远程控制台协议（RCON）。启用务必设置强密码！ |
| `rcon.password` | RCON 密码 | string | （空） | ✅ | RCON 远程管理密码。启用 RCON 时必须设置。 |
| `auto-save` | 自动保存 | bool | `true`（`true`/`false`） | ✅ | 是否启用自动保存。 |
| `force-resources` | 强制资源包 | bool | `false`（`true`/`false`） | ✅ | 玩家必须接受服务器资源包才能进服。 |
| `force-resources-allow-client-packs` | 允许客户端资源包 | bool | `true`（`true`/`false`） | ✅ | 是否允许玩家使用客户端自带资源包。 |
| `xbox-auth` | Xbox Live 验证 | bool | `true`（`true`/`false`） | ✅ | 是否要求玩家通过 Xbox Live 认证。⚠️ 与 Java 版 `online-mode` 含义不同！ |
| `check-login-time` | 检查登录时间 | bool | `false`（`true`/`false`） | ✅ | 是否校验玩家登录用时。 |
| `network-encryption` | 网络加密 | bool | `true`（`true`/`false`） | ✅ | 是否启用基岩版网络加密（ECDH 握手）。 |

---

## 配置示例

### powernukkit.yml 默认配置

```yaml
# PowerNukkitX Configuration
config:
  version: '3.0.0'
settings:
  ip: 0.0.0.0
  port: 19132
  maxplayers: 20
  defaultlevel: world
  allowlist: false
  allowlist.message: Server is white-listed
  motd: PowerNukkitX Server
  sub-motd: powernukkitx.org
  language: eng
  forcetranslate: false
  safespawn: true
  autosave: true
  autosaveDelay: 6000
  saveunknownblock: true
  xboxauth: true
player-settings:
  saveplayerdata: true
  skinchangecooldown: 30
  forceskintrusted: false
  checkmovement: true
  rotationupdatethreshold: 1
  movementdistancethreshold: 0.1
  spawnRadius: 16
gameplay-settings:
  enablecommandblocks: true
  allowbeta: false
  enableredstone: true
  tickRedstone: true
  viewDistance: 8
  achivements: true
  announceAchievements: true
  spawnProtection: 16
  gamemode: 0
  forceGamemode: false
  hardcore: false
  pvp: true
  difficulty: 1
  allowNether: true
  allowEnd: true
  forceResources: false
  allowClientPacks: true
  allowVibrantVisuals: true
  experiments:
    - data_driven_vanilla_blocks_and_items
  cacheStructures: false
  enableEdu: false
  muteEmoteAnnouncements: false
  enablemobai: true
  enableRecipes: true
  enableCreativeInventory: true
  enableDaylightCycle: true
  enableWeather: true
  enableEntitySpawning: true
  enableBlockRandomTicking: true
  enableLiquidFlow: true
  enableItemDrops: true
  enableXpOrbs: true
  enableExplosionBlockDamage: true
  enableBlockGravity: true
  enableHunger: true
misc-settings:
  shutdownmessage: Server closed
  installspark: false
  bypassapicheck: false
  overrideserverauthblockbreaking: false
  enablemetrics: true
level-settings:
  levelthread: false
  autotickrate: true
  autotickratelimit: 20
  basetickrate: 1
  alwaystickplayers: false
  loadalllevels: true
  chunkunloaddelay: 15000
  entityspawncap: 512
  fieldofview: 100
  levelworkerthreads: -1
chunk-settings:
  spawnlimit: 3
  perticksend: 32
  spawnthreshold: 56
  chunksperticks: -1
  tickRadius: 4
  lightupdates: true
  clearticklist: true
  generationqueuesize: 8
  saveGenerated: true
  convertBDSChunks: false
  disableblockticking: []
network-settings:
  queryplugins: true
  compressionlevel: 4
  zlibprovider: 3
  snappy: false
  compressionbuffersize: 1048576
  maxdecompresssize: 268435456
  packetlimit: 8000
  query: true
  encryption: true
  logintime: false
  autoflush: true
  flushinterval: 10
  maxqueuedbytes: 67108864
  cookiemode: ACTIVE
  rate-limit:
    enabled: true
    maxinboundpersecond: 1500
    maxpacketspertick: 500
    maxcommandsperplayer: 10
    maxchatperplayer: 2
    maxformresponsesperplayer: 20
    maxmovementperplayer: 40
  botnet:
    enabled: false
    suspiciousthreshold: 300
    minsuspiciousips: 3
    autoblock: true
    autoblockdurationseconds: 60
    minscore: 2
debug-settings:
  deprecatedverbose: true
  level: INFO
  command: false
  packet:
    mode: false
  packetList: []
  disableencodinglimits: false
performance-settings:
  asyncworkers: auto
  basetps: 20
  registrycache:
    enable: false
    path: path/to/your/registry_cache.bin
  forcegcpercentage: 1.0
  freeze-array:
    enable: true
    slots: 32
    defaultTemperature: 32
    freezingPoint: 0
    boilingPoint: 1024
    absoluteZero: -256
    melting: 16
    singleOperation: 1
    batchOperation: 32
```

### powernukkit-server.properties 默认配置

```properties
motd=PowerNukkitX Server
sub-motd=powernukkitx.org
server-port=19132
server-ip=0.0.0.0
view-distance=8
white-list=false
achievements=true
announce-player-achievements=true
spawn-protection=16
max-players=20
allow-flight=false
spawn-animals=true
spawn-mobs=true
gamemode=0
force-gamemode=false
hardcore=false
pvp=true
difficulty=1
level-name=world
level-seed=
allow-nether=true
allow-the_end=true
enable-query=true
enable-rcon=false
rcon.password=
auto-save=true
force-resources=false
force-resources-allow-client-packs=true
xbox-auth=true
check-login-time=false
network-encryption=true
```

---

## 优化建议（针对生产服务器）

1. **网络加密与 Xbox 验证**：保持 `network-settings.encryption: true` 和 `settings.xboxauth: true`，公网服务器关闭这两项会带来严重安全风险。
2. **压缩级别**：带宽紧张的服务器可调高 `network-settings.compressionlevel` 到 `6`；CPU 紧张则保持 `4` 或调低到 `3`。
3. **速率限制**：生产环境保持 `rate-limit.enabled: true`，并根据玩家数量调整 `maxinboundpersecond`（小型服可降到 1000）。
4. **僵尸网络防护**：公开 PvP / 生存服建议开启 `botnet.enabled: true`，自动封禁可疑 IP。
5. **视野距离**：基岩版客户端默认视野较大，建议 `gameplay-settings.viewDistance: 8`–`12`，公网服不超过 `16`。
6. **冻结数组优化**：保持 `performance-settings.freeze-array.enable: true`，可显著降低常量数组的内存占用与访问开销。
7. **每世界独立线程**：多世界服（如大厅+小游戏）建议开启 `level-settings.levelthread: true`，但需测试插件兼容性。
8. **自动保存**：保持 `settings.autosave: true`，`autosaveDelay: 6000`（5 分钟），过短间隔会增加磁盘 IO。
9. **资源包**：使用自定义资源包时建议 `force-resources: false` + `allow-client-packs: true`，避免玩家因拒绝资源包被踢出。

> 参考来源：PowerNukkitX 官方源码 [`ServerSettings.java`](https://github.com/PowerNukkitX/PowerNukkitX/blob/master/src/main/java/org/powernukkitx/config/ServerSettings.java) 及 `org.powernukkitx.config.category` 包下各类、`LegacyServerPropertiesKeys.java` 枚举（master 分支）。
