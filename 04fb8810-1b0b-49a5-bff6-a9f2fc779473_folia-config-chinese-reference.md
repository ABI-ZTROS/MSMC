# Minecraft 服务端全核心配置文件中文参考手册

> **涵盖所有主流 Java 版 / 基岩版服务端核心**，含 Paper 系、Folia 系、Forge 系、Sponge、Fabric、国产核心、基岩版核心
> 适用版本：以 **Folia 26.1.2**（2026 年 7 月）为基准，其余核心以最新稳定版为准
> 数据来源：各核心官方文档 / GitHub 源码默认配置 / Minecraft Wiki

---

## 核心架构与配置文件关系图

```
原版 Vanilla（server.properties）
├── Bukkit 系（+ bukkit.yml）
│   ├── Spigot（+ spigot.yml）
│   │   ├── Paper（+ paper-global.yml, paper-world-defaults.yml）
│   │   │   ├── Folia（+ threaded-regions 配置，同 Paper 文件）
│   │   │   ├── Purpur（+ purpur.yml）
│   │   │   └── Leaves（+ leaves.yml）
│   │   └── CanvasMC（+ canvas-server.yml, canvas-worlds.yml）〔Folia 分支〕
│   │       └── DeerFolia（+ deer-folia.yml, kaiiju-entity-throttling.yml）
│   └── Cardboard〔已停更，Fabric+Paper 混合〕
├── Forge / NeoForge（+ 每模组独立 TOML 配置，无全局 Forge 配置文件）
│   ├── Sponge（+ config/sponge/global.conf，HOCON 格式）
│   └── Arclight（+ config/arclight.conf，Forge+Bukkit 混合）
├── Fabric（无额外配置，仅 server.properties + 模组独立配置）
├── Mohist（+ mohist-config/mohist.yml，Forge+Bukkit 混合）
└── CatServer（+ catserver.yml，Forge+Bukkit 混合，国产）

基岩版
├── Nukkit（仅 server.properties，ON/OFF 布尔值）
└── PocketMine-MP（+ server.properties, pocketmine.yml）
```

## 共享配置文件说明

以下文件被**多个核心共享**，已在本手册第一节至第五节完整翻译：

| 共享文件 | 适用核心 | 说明 |
|----------|----------|------|
| `server.properties` | **所有 Java 版核心** | 原版 Minecraft 配置，Fabric/Forge/Sponge 等均使用 |
| `bukkit.yml` | Bukkit 系全部（Spigot/Paper/Folia/Purpur/Leaves/CanvasMC/DeerFolia/Mohist/CatServer/Arclight） | Bukkit API 层 |
| `spigot.yml` | Spigot 及其下游全部 | Spigot 优化层 |
| `paper-global.yml` | Paper/Folia/Purpur/Leaves | Paper 全局配置，Folia 在此新增 `threaded-regions` |
| `paper-world-defaults.yml` | Paper/Folia/Purpur/Leaves | Paper 世界配置 |

> **查找方法**：如果你用的核心是 Purpur，你需要阅读「第一节 server.properties + 第二节 paper-global.yml + 第三节 paper-world-defaults.yml + 第四节 bukkit.yml + 第五节 spigot.yml + 第八节 purpur.yml」。其余核心以此类推。

---

## 第一部分：共享配置文件（Java 版通用）

### 一、server.properties（原版 Minecraft 配置）

> 文件路径：服务器根目录 `server.properties`
> 官方来源：[Minecraft Wiki - server.properties](https://minecraft.wiki/w/Server.properties)

| 中文含义 | 英文键名 | 类型 | 默认值 | 最小值 | 最大值 | 说明 |
|----------|----------|------|--------|--------|--------|------|
| 服务器端口 | `server-port` | int | `25565` | 1 | 65533 | 服务器监听端口 |
| 服务器 IP | `server-ip` | string | `（空）` | — | — | 留空则绑定所有地址 |
| 最大玩家数 | `max-players` | int | `20` | 0 | 2147483647 | 同时在线上限 |
| 世界名称 | `level-name` | string | `world` | — | — | 世界文件夹名 |
| 世界种子 | `level-seed` | string | `（空）` | — | — | 留空则随机生成 |
| 世界类型 | `level-type` | enum | `minecraft:normal` | — | — | 可选：`minecraft:normal` / `minecraft:flat` / `minecraft:large_biomes` / `minecraft:amplified` / `minecraft:single_biome_surface` |
| 生成结构 | `generate-structures` | bool | `true` | — | — | 是否生成村庄等结构 |
| 世界大小限制 | `max-world-size` | int | `29999984` | 1 | 29999984 | 单位：方块 |
| 视距 | `view-distance` | int | `10` | **3** | **32** | 区块半径，控制客户端可见范围 |
| **模拟距离** | `simulation-distance` | int | `10` | **3** | **32** | 区块半径，控制红石/实体/农作物计算范围，**红石相关最关键参数** |
| 游戏难度 | `difficulty` | enum | `easy` | — | — | 可选：`peaceful` / `easy` / `normal` / `hard` |
| 游戏模式 | `gamemode` | enum | `survival` | — | — | 可选：`survival` / `creative` / `adventure` / `spectator` |
| 强制游戏模式 | `force-gamemode` | bool | `false` | — | — | 所有玩家使用默认游戏模式 |
| 极限模式 | `hardcore` | bool | `false` | — | — | 死亡后 banspec |
| 正版验证 | `online-mode` | bool | `true` | — | — | false = 离线模式 |
| 允许飞行 | `allow-flight` | bool | `false` | — | — | 生存模式下允许飞行 |
| 白名单 | `white-list` | bool | `false` | — | — | — |
| 强制白名单 | `enforce-whitelist` | bool | `false` | — | — | — |
| 服务器描述 | `motd` | string | `A Minecraft Server` | — | <59字符 | 服务器列表显示的描述 |
| 在线状态显示 | `enable-status` | bool | `true` | — | — | 服务器在列表中显示为在线 |
| 隐藏在线玩家 | `hide-online-players` | bool | `false` | — | — | 隐藏玩家列表 |
| 出生保护范围 | `spawn-protection` | int | `16` | 0 | — | 边长 = 2×此值+1，0=禁用 |
| OP 权限等级 | `op-permission-level` | int | `4` | **0** | **4** | 4=最高权限 |
| 函数权限等级 | `function-permission-level` | int | `2` | **1** | **4** | — |
| 单 tick 最大时间 | `max-tick-time` | int | `60000` | -1 | 9223372036854775807 | 毫秒，-1=禁用看门狗超时 |
| 最大连锁邻居更新 | `max-chained-neighbor-updates` | int | `1000000` | — | — | 负数=禁用，**红石大规模更新时相关** |
| 实体广播范围百分比 | `entity-broadcast-range-percentage` | int | `100` | **10** | **1000** | 实体元数据发送范围 = 原始范围 × 此值% |
| 网络压缩阈值 | `network-compression-threshold` | int | `256` | -1 | — | 字节，-1=禁用压缩，0=压缩所有 |
| 同步区块写入 | `sync-chunk-writes` | bool | `true` | — | — | 防止崩溃导致数据丢失，SSD 可设 false 提速 |
| 区域文件压缩 | `region-file-compression` | enum | `deflate` | — | — | 可选：`deflate` / `lz4` / `none`。lz4 读写最快 |
| 玩家空闲踢出 | `player-idle-timeout` | int | `0` | 0 | — | 分钟，0=永不踢出 |
| 空服暂停延迟 | `pause-when-empty-seconds` | int | `60` | 0 | — | 秒，无玩家时暂停服务器 |
| 聊天刷屏阈值 | `chat-spam-threshold-seconds` | int | `10` | 0 | — | 秒，0=禁用踢出 |
| 命令刷屏阈值 | `command-spam-threshold-seconds` | int | `10` | 0 | — | 秒，0=禁用踢出 |
| 每玩家数据包速率 | `rate-limit` | int | `0` | 0 | — | 0=禁用 |
| 记录 IP | `log-ips` | bool | `true` | — | — | — |
| 接受玩家转移 | `accepts-transfers` | bool | `false` | — | — | 接收其他服务器转入的玩家 |
| 强制安全配置 | `enforce-secure-profile` | bool | `true` | — | — | Mojang 签名验证 |
| 启用 Query | `enable-query` | bool | `false` | — | — | GameSpy4 Query 协议 |
| Query 端口 | `query.port` | int | `25565` | 1 | 65533 | — |
| 启用 RCON | `enable-rcon` | bool | `false` | — | — | 远程控制台 |
| RCON 端口 | `rcon.port` | int | `25575` | 1 | 65533 | — |
| RCON 密码 | `rcon.password` | string | `（空）` | — | — | — |
| 启用 JMX 监控 | `enable-jmx-monitoring` | bool | `false` | — | — | — |
| 管理服务器启用 | `management-server-enabled` | bool | `false` | — | — | — |
| 管理服务器主机 | `management-server-host` | string | `localhost` | — | — | — |
| 管理服务器端口 | `management-server-port` | int | `0` | — | — | — |
| 资源包 URL | `resource-pack` | string | `（空）` | — | — | — |
| 资源包 SHA1 | `resource-pack-sha1` | string | `（空）` | — | — | — |
| 资源包 UUID | `resource-pack-id` | string | `（空）` | — | — | — |
| 强制资源包 | `require-resource-pack` | bool | `false` | — | — | — |
| 资源包提示 | `resource-pack-prompt` | string | `（空）` | — | — | — |
| 防止代理连接 | `prevent-proxy-connections` | bool | `false` | — | — | — |
| 使用原生传输 | `use-native-transport` | bool | `true` | — | — | Linux epoll 网络优化 |
| 初始启用数据包 | `initial-enabled-packs` | string | `vanilla` | — | — | 逗号分隔 |
| 初始禁用数据包 | `initial-disabled-packs` | string | `（空）` | — | — | 逗号分隔 |
| 世界生成设置 | `generator-settings` | string | `{}` | — | — | — |
| Bug 报告链接 | `bug-report-link` | string | `（空）` | — | — | — |
| 聊天过滤配置 | `text-filtering-config` | string | `（空）` | — | — | — |
| 聊天过滤版本 | `text-filtering-version` | int | `0` | 0 | 1 | — |
| 状态心跳间隔 | `status-heartbeat-interval` | int | `0` | 0 | — | 秒，0=禁用 |
| 启用行为准则 | `enable-code-of-conduct` | bool | `false` | — | — | — |
| 控制台广播到 OP | `broadcast-console-to-ops` | bool | `true` | — | — | — |
| RCON 广播到 OP | `broadcast-rcon-to-ops` | bool | `true` | — | — | — |

---

### 二、paper-global.yml（Paper/Folia/Purpur/Leaves 全局配置）

> 文件路径：`config/paper-global.yml`
> Folia 在此文件中新增 `threaded-regions` 节点，其余与 Paper 一致。
> 官方来源：[PaperMC 全局配置参考](https://docs.papermc.io/paper/reference/global-configuration)

### 2.1 Folia 专属：区域化多线程配置

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| **区域 tick 线程数** | `threaded-regions.threads` | int | `-1` | **-1**（自动）或 **≥1** | -1=根据 CPU 自动分配。分配完 Netty IO（~4线程/200-300玩家）、Chunk IO（~3线程）、Chunk Worker（~2线程）、GC 并发线程后，将剩余核心的 80% 以内分配给此项 |
| **区域大小指数** | `threaded-regions.gridExponent` | int | `4` | 通常 **2~7** | 每个区域 = 2^n × 2^n 区块。4=16×16区块(256×256格)；5=32×32(512×512)；6=64×64(1024×1024)；**红石机器多时应调大到 6** |
| **区域调度算法** | `threaded-regions.scheduler` | enum | `EDF` | `EDF` / `WORK_STEALING` | EDF=最早截止时间优先（最稳定）；WORK_STEALING=工作窃取（性能更好但已知有问题）。Canvas 分支额外支持 `AFFINITY` |

### 2.2 方块更新控制

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 禁用紫颂植物更新 | `block-updates.disable-chorus-plant-updates` | `false` | bool | — |
| 禁用蘑菇方块更新 | `block-updates.disable-mushroom-block-updates` | `false` | bool | — |
| 禁用音符盒更新 | `block-updates.disable-noteblock-updates` | `false` | bool | — |
| 禁用绊线更新 | `block-updates.disable-tripwire-updates` | `false` | bool | — |

### 2.3 区块系统

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 区块生成并行度 | `chunk-system.gen-parallelism` | `default` | `default` / `true` / `false` | default=自动 |
| 区块 IO 线程数 | `chunk-system.io-threads` | `-1` | int，-1=自动 | — |
| 区块工作线程数 | `chunk-system.worker-threads` | `-1` | int，-1=自动（物理核心数一半） | — |

### 2.4 区块加载（基础）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 自动配置发送距离 | `chunk-loading-advanced.auto-config-send-distance` | `true` | bool | 基于视距自动匹配 |
| 每玩家最大并发区块生成 | `chunk-loading-advanced.player-max-concurrent-chunk-generates` | `0` | int，0=无限 | — |
| 每玩家最大并发区块加载 | `chunk-loading-advanced.player-max-concurrent-chunk-loads` | `0` | int，0=无限 | — |
| 每玩家每秒区块生成速率 | `chunk-loading-basic.player-max-chunk-generate-rate` | `-1.0` | float，-1=无限 | — |
| 每玩家每秒区块加载速率 | `chunk-loading-basic.player-max-chunk-load-rate` | `100.0` | float，-1=无限 | — |
| 每玩家每秒区块发送速率 | `chunk-loading-basic.player-max-chunk-send-rate` | `75.0` | float | — |

### 2.5 碰撞（全局）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 启用玩家碰撞 | `collisions.enable-player-collisions` | `true` | bool | — |
| 硬碰撞实体完整坐标 | `collisions.send-full-pos-for-hard-colliding-entities` | `true` | bool | — |

### 2.6 命令

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| /ride 允许玩家作载具 | `commands.ride-command-allow-player-as-vehicle` | `false` | bool | — |
| Tab 补全建议玩家名 | `commands.suggest-player-names-when-null-tab-completions` | `true` | bool | — |
| /time 影响所有世界 | `commands.time-command-affects-all-worlds` | `false` | bool | — |

### 2.7 控制台

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| Brigadier 补全 | `console.enable-brigadier-completions` | `true` | bool | — |
| Brigadier 高亮 | `console.enable-brigadier-highlighting` | `true` | bool | — |
| 控制台拥有所有权限 | `console.has-all-permissions` | `false` | bool | — |

### 2.8 物品验证

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 显示名最大长度 | `item-validation.display-name` | `8192` | 正整数 | — |
| Lore 每行最大长度 | `item-validation.lore-line` | `8192` | 正整数 | — |
| 书作者名最大长度 | `item-validation.book.author` | `8192` | 正整数 | — |
| 书标题最大长度 | `item-validation.book.title` | `8192` | 正整数 | — |
| 书每页最大长度 | `item-validation.book.page` | `16384` | 正整数 | — |
| 书最大页数 | `item-validation.book-size.page-max` | `2560` | 正整数 | — |
| 书总大小乘数 | `item-validation.book-size.total-multiplier` | `0.98` | 0.0~1.0 | — |
| 书中解析选择器 | `item-validation.resolve-selectors-in-books` | `false` | bool | — |

### 2.9 杂项

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 修复实体位置不同步 | `misc.fix-entity-position-desync` | `true` | bool | — |
| 插件前加载权限 | `misc.load-permissions-yml-before-plugins` | `true` | bool | — |
| 每 tick 最大加入玩家数 | `misc.max-joins-per-tick` | `5` | 正整数 | — |
| 防止村民负需求 | `misc.prevent-negative-villager-demand` | `false` | bool | — |
| 区域文件缓存大小 | `misc.region-file-cache-size` | `256` | 正整数 | — |
| 掉落物完整坐标 | `misc.send-full-pos-for-item-entities` | `false` | bool | — |
| 严格进度维度检查 | `misc.strict-advancement-dimension-check` | `false` | bool | — |
| 替代幸运公式 | `misc.use-alternative-luck-formula` | `false` | bool | — |
| 自定义刷怪笼用维度类型 | `misc.use-dimension-type-for-custom-spawners` | `false` | bool | — |
| 每区域经验球分组数 | `misc.xp-orb-groups-per-area` | `default` | `default` / 正整数 | — |
| 客户端交互宽容距离 | `misc.client-interaction-leniency-distance` | `default` | `default` / 正数 | — |
| 网络压缩级别 | `misc.compression-level` | `default` | `default` / -1~9 | — |
| 聊天执行器核心线程数 | `misc.chat-threads.chat-executor-core-size` | `-1` | int，-1=自动 | — |
| 聊天执行器最大线程数 | `misc.chat-threads.chat-executor-max-size` | `-1` | int，-1=自动 | — |

### 2.10 数据包限制器

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 超限操作 | `packet-limiter.all-packets.action` | `KICK` | `KICK` / `DROP` | — |
| 检测间隔 | `packet-limiter.all-packets.interval` | `7.0` | 正浮点数（秒） | — |
| 最大数据包速率 | `packet-limiter.all-packets.max-packet-rate` | `500.0` | 正浮点数 | — |
| 合成配方超限操作 | `packet-limiter.overrides.ServerboundPlaceRecipePacket.action` | `DROP` | `KICK` / `DROP` | — |
| 合成配方检测间隔 | `packet-limiter.overrides.ServerboundPlaceRecipePacket.interval` | `4.0` | 正浮点数（秒） | — |
| 合成配方最大速率 | `packet-limiter.overrides.ServerboundPlaceRecipePacket.max-packet-rate` | `5.0` | 正浮点数 | — |

### 2.11 玩家自动保存

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 每 tick 最大保存玩家数 | `player-auto-save.max-per-tick` | `-1` | int，-1=无限 | — |
| 自动保存间隔 | `player-auto-save.rate` | `-1` | int，-1=禁用（tick） | — |

### 2.12 垃圾信息限制

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 入站包阈值 | `spam-limiter.incoming-packet-threshold` | `300` | 正整数 | — |
| 合成配方递增量 | `spam-limiter.recipe-spam-increment` | `1` | 正整数 | — |
| 合成配方限制 | `spam-limiter.recipe-spam-limit` | `20` | 正整数 | — |
| Tab 补全递增量 | `spam-limiter.tab-spam-increment` | `1` | 正整数 | — |
| Tab 补全限制 | `spam-limiter.tab-spam-limit` | `500` | 正整数 | — |

### 2.13 不支持设置（风险自担）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 允许无头活塞 | `unsupported-settings.allow-headless-pistons` | `false` | bool | — |
| 允许永久破坏方块 | `unsupported-settings.allow-permanent-block-break-exploits` | `false` | bool | — |
| **允许活塞复制** | `unsupported-settings.allow-piston-duplication` | `false` | bool | **TNT/地毯/铁轨复制** |
| 允许不安全末地传送 | `unsupported-settings.allow-unsafe-end-portal-teleportation` | `false` | bool | — |
| 压缩格式 | `unsupported-settings.compression-format` | `ZLIB` | `ZLIB` / `GZIP` / `NONE` | — |
| 用户名验证 | `unsupported-settings.perform-username-validation` | `true` | bool | — |
| 跳过绊线钩验证 | `unsupported-settings.skip-tripwire-hook-placement-validation` | `false` | bool | — |
| 盾牌跳过伤害 tick | `unsupported-settings.skip-vanilla-damage-tick-when-shield-blocked` | `false` | bool | — |
| 行动时更新装备 | `unsupported-settings.update-equipment-on-player-actions` | `true` | bool | — |

### 2.14 看门狗

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 早期警告延迟 | `watchdog.early-warning-delay` | `10000` | 正整数（毫秒） | — |
| 早期警告间隔 | `watchdog.early-warning-every` | `5000` | 正整数（毫秒） | — |

### 2.15 代理

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| PROXY 协议 | `proxies.proxy-protocol` | `false` | bool | — |
| BungeeCord | `proxies.bungee-cord.online-mode` | `true` | bool | — |
| Velocity 启用 | `proxies.velocity.enabled` | `false` | bool | — |
| Velocity 在线验证 | `proxies.velocity.online-mode` | `true` | bool | — |
| Velocity 密钥 | `proxies.velocity.secret` | `（空）` | 字符串 | — |

---

### 三、paper-world-defaults.yml（Paper/Folia/Purpur/Leaves 世界默认配置）

> 文件路径：`config/paper-world-defaults.yml`
> 可被 `config/worlds/<世界名>/paper-world.yml` 按世界覆盖
> 官方来源：[PaperMC 世界配置参考](https://docs.papermc.io/paper/reference/world-configuration)

### 3.1 环境（environment）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **最大方块 tick 数** | `environment.max-block-ticks` | `65536` | 正整数 | **每 tick 最大方块更新数（含红石），红石密集时可能需调低** |
| **最大流体 tick 数** | `environment.max-fluid-ticks` | `65536` | 正整数 | — |
| 优化爆炸计算 | `environment.optimize-explosions` | `false` | bool | 推荐开启，视觉效果不变 |
| 禁用冰和雪 | `environment.disable-ice-and-snow` | `false` | bool | — |
| 禁用雷暴 | `environment.disable-thunder` | `false` | bool | — |
| 禁用爆炸击退 | `environment.disable-explosion-knockback` | `false` | bool | — |
| 火焰蔓延延迟 | `environment.fire-tick-delay` | `30` | 正整数（tick） | 30=1.5秒 |
| 水覆熔岩流速 | `environment.water-over-lava-flow-speed` | `5` | 正整数 | — |
| 传送门搜索半径 | `environment.portal-search-radius` | `128` | 正整数 | — |
| 传送门创建半径 | `environment.portal-create-radius` | `16` | 正整数 | — |
| 传送门原版维度缩放 | `environment.portal-search-vanilla-dimension-scaling` | `true` | bool | — |
| 虚空伤害量 | `environment.void-damage-amount` | `4.0` | 浮点数 | — |
| 虚空伤害高度偏移 | `environment.void-damage-min-build-height-offset` | `-64.0` | 浮点数 | — |
| 下界天花板虚空伤害 | `environment.nether-ceiling-void-damage-height` | `disabled` | `disabled` / 整数 | — |
| 平坦基岩 | `environment.generate-flat-bedrock` | `false` | bool | — |
| 边界外定位结构 | `environment.locate-structures-outside-world-border` | `false` | bool | — |
| 霜冰启用 | `environment.frosted-ice.enabled` | `true` | bool | — |
| 霜冰最小延迟 | `environment.frosted-ice.delay.min` | `20` | 正整数（tick） | — |
| 霜冰最大延迟 | `environment.frosted-ice.delay.max` | `40` | 正整数（tick） | — |
| 藏宝图启用 | `environment.treasure-maps.enabled` | `true` | bool | — |
| 藏宝图指向已发现(战利品表) | `environment.treasure-maps.find-already-discovered.loot-tables` | `default` | `default` / bool | — |
| 藏宝图指向已发现(村民) | `environment.treasure-maps.find-already-discovered.villager-trade` | `false` | bool | — |

### 3.2 红石实现（misc，与红石直接相关）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **红石实现方式** | `misc.redstone-implementation` | `VANILLA` | `VANILLA` / `EIGENCRAFT` / `ALTERNATE_CURRENT` | **VANILLA=原版；ALTERNATE_CURRENT 性能最优（减少 90%+ 红石计算）** |
| **AC 更新顺序** | `misc.alternate-current-update-order` | `HORIZONTAL_FIRST_OUTWARD` | `HORIZONTAL_FIRST_OUTWARD` 等 | 仅 ALTERNATE_CURRENT 模式生效 |
| 方块更新时更新寻路 | `misc.update-pathfinding-on-block-update` | `true` | bool | 禁用可显著提升性能 |
| 盾牌格挡延迟 | `misc.shield-blocking-delay` | `5` | 正整数（tick） | — |
| 拴绳最大距离 | `misc.max-leash-distance` | `default` | `default` / 正浮点数 | — |
| 禁用末地终幕 | `misc.disable-end-credits` | `false` | bool | — |
| 禁用相对投射物速度 | `misc.disable-relative-projectile-velocity` | `false` | bool | — |
| 禁用攻击打断疾跑 | `misc.disable-sprint-interruption-on-attack` | `false` | bool | — |
| 旧版末影珍珠行为 | `misc.legacy-ender-pearl-behavior` | `false` | bool | — |
| 告示牌命令失败消息 | `misc.show-sign-click-command-failure-msgs-to-player` | `false` | bool | — |

### 3.3 Tick 速率（tick-rates，性能调控核心）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **容器更新速率** | `tick-rates.container-update` | `1` | 正整数（tick） | 箱子等容器内容刷新频率 |
| **草蔓延速率** | `tick-rates.grass-spread` | `1` | 正整数（tick） | 建议调到 4 降低开销 |
| **刷怪笼速率** | `tick-rates.mob-spawner` | `1` | 正整数（tick） | 设为 2 减半 CPU 开销 |
| **村民验证附近 POI** | `tick-rates.behavior.villager.validatenearbypoi` | `-1` | int，-1=原版 | 建议设为 60 |
| **村民次要 POI 传感器** | `tick-rates.sensor.villager.secondarypoisensor` | `40` | 正整数（tick） | 建议设为 80 |
| 干旱农田检测 | `tick-rates.dry-farmland` | `1` | 正整数（tick） | — |
| 湿润农田检测 | `tick-rates.wet-farmland` | `1` | 正整数（tick） | — |

### 3.4 区块（chunks）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 自动保存间隔 | `chunks.auto-save-interval` | `default` | `default` / 正整数（tick） | default = bukkit.yml 的值 |
| 延迟区块卸载 | `chunks.delay-chunk-unloads-by` | `10s` | 时间字符串（d/h/m/s） | — |
| 每 tick 最大保存区块数 | `chunks.max-auto-save-chunks-per-tick` | `24` | 正整数 | **HDD 建议降到 8~12** |
| 保存时刷新磁盘 | `chunks.flush-regions-on-save` | `false` | bool | true 更安全但更慢 |
| 阻止进入未加载区块 | `chunks.prevent-moving-into-unloaded-chunks` | `false` | bool | — |
| 固定区块居住时间 | `chunks.fixed-chunk-inhabited-time` | `-1` | int，-1=禁用 | 影响生物生成难度 |
| 每区块箭矢保存上限 | `chunks.entity-per-chunk-save-limit.arrow` | `-1` | int，-1=无限制 | — |
| 每区块末影珍珠保存上限 | `chunks.entity-per-chunk-save-limit.ender_pearl` | `-1` | int | — |
| 每区块经验球保存上限 | `chunks.entity-per-chunk-save-limit.experience_orb` | `-1` | int | — |
| 每区块火球保存上限 | `chunks.entity-per-chunk-save-limit.fireball` | `-1` | int | — |
| 每区块小火球保存上限 | `chunks.entity-per-chunk-save-limit.small_fireball` | `-1` | int | — |
| 每区块雪球保存上限 | `chunks.entity-per-chunk-save-limit.snowball` | `-1` | int | — |

### 3.5 碰撞（collisions，世界级）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **最大实体碰撞数** | `collisions.max-entity-collisions` | `8` | **≥0** | **0=完全禁用推挤，2~4 可大幅提升性能** |
| 允许挤压伤害 | `collisions.allow-player-cramming-damage` | `false` | bool | — |
| 允许载具碰撞 | `collisions.allow-vehicle-collisions` | `true` | bool | — |
| 修复攀爬绕过挤压 | `collisions.fix-climbing-bypassing-cramming-rule` | `false` | bool | — |
| 仅玩家碰撞 | `collisions.only-players-collide` | `false` | bool | — |

### 3.6 命令方块

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 强制满足权限等级 | `command-blocks.force-follow-perm-level` | `true` | bool | — |
| 默认权限等级 | `command-blocks.permissions-level` | `2` | **1 / 2 / 3 / 4** | — |

### 3.7 实体 - 盔甲架

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 盔甲架是否 tick | `entities.armor-stands.tick` | `true` | bool | 大量盔甲架时建议关 |
| 盔甲架碰撞查找 | `entities.armor-stands.do-collision-entity-lookups` | `true` | bool | — |

### 3.8 实体 - 标记

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 标记实体 tick | `entities.markers.tick` | `true` | bool | — |

### 3.9 实体 - 行为

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 幼年僵尸移速修正 | `entities.behavior.baby-zombie-movement-modifier` | `0.5` | 浮点数 | 0.5=快50% |
| 禁用箱子猫检测 | `entities.behavior.disable-chest-cat-detection` | `false` | bool | — |
| 禁用苦力怕残留 | `entities.behavior.disable-creeper-lingering-effect` | `false` | bool | — |
| 禁用玩家暴击 | `entities.behavior.disable-player-crits` | `false` | bool | — |
| 蜘蛛边界攀爬 | `entities.behavior.allow-spider-world-border-climbing` | `true` | bool | — |
| 末影龙必放龙蛋 | `entities.behavior.ender-dragons-death-always-places-dragon-egg` | `false` | bool | — |
| 经验球合并最大值 | `entities.behavior.experience-merge-max-value` | `-1` | int，-1=无限制 | — |
| 骷髅始终拾取 | `entities.behavior.mobs-can-always-pick-up-loot.skeletons` | `false` | bool | — |
| 僵尸始终拾取 | `entities.behavior.mobs-can-always-pick-up-loot.zombies` | `false` | bool | — |
| 削弱地狱门猪灵 | `entities.behavior.nerf-pigmen-from-nether-portals` | `false` | bool | — |
| 仅水平合并物品 | `entities.behavior.only-merge-items-horizontally` | `false` | bool | — |
| 幻翼不生成于创造玩家 | `entities.behavior.phantoms-do-not-spawn-on-creative-players` | `true` | bool | — |
| 幻翼仅攻击失眠 | `entities.behavior.phantoms-only-attack-insomniacs` | `true` | bool | — |
| 幻翼最大生成间隔(秒) | `entities.behavior.phantoms-spawn-attempt-max-seconds` | `119` | 正整数 | — |
| 幻翼最小生成间隔(秒) | `entities.behavior.phantoms-spawn-attempt-min-seconds` | `60` | 正整数 | — |
| 猪灵守卫箱子 | `entities.behavior.piglins-guard-chests` | `true` | bool | — |
| 掠夺者巡逻禁用 | `entities.behavior.pillager-patrols.disable` | `false` | bool | — |
| 掠夺者巡逻概率 | `entities.behavior.pillager-patrols.spawn-chance` | `0.2` | 0.0~1.0 | — |
| 掠夺者巡逻延迟(tick) | `entities.behavior.pillager-patrols.spawn-delay.ticks` | `12000` | 正整数 | — |
| 掠夺者巡逻按玩家 | `entities.behavior.pillager-patrols.spawn-delay.per-player` | `false` | bool | — |
| 掠夺者开始天数 | `entities.behavior.pillager-patrols.start.day` | `5` | 正整数 | — |
| 失眠开始 tick | `entities.behavior.player-insomnia-start-ticks` | `72000` | 正整数，-1=禁用幻翼 | 72000=3个游戏日 |
| 移除无传送门末影龙 | `entities.behavior.should-remove-dragon` | `false` | bool | — |
| 刷怪笼生物跳跃 | `entities.behavior.spawner-nerfed-mobs-should-jump` | `false` | bool | — |
| 僵尸感染概率 | `entities.behavior.zombie-villager-infection-chance` | `default` | `default` / 0.0~100.0 | — |
| 僵尸攻击海龟蛋 | `entities.behavior.zombies-target-turtle-eggs` | `true` | bool | 关闭可提升性能 |
| 鹦鹉不受移动影响 | `entities.behavior.parrots-are-unaffected-by-player-movement` | `false` | bool | — |
| 蜜蜂释放冷却 | `entities.behavior.cooldown-failed-beehive-releases` | `true` | bool | — |

### 3.10 实体 - 破门难度

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 尸壳破门 | `entities.behavior.door-breaking-difficulty.husk` | `[HARD]` | 难度列表 | — |
| 卫道士破门 | `entities.behavior.door-breaking-difficulty.vindicator` | `[NORMAL, HARD]` | 难度列表 | — |
| 僵尸破门 | `entities.behavior.door-breaking-difficulty.zombie` | `[HARD]` | 难度列表 | — |
| 僵尸村民破门 | `entities.behavior.door-breaking-difficulty.zombie_villager` | `[HARD]` | 难度列表 | — |
| 僵尸化猪灵破门 | `entities.behavior.door-breaking-difficulty.zombified_piglin` | `[HARD]` | 难度列表 | — |

### 3.11 实体 - 状态效果免疫

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 凋灵免疫凋零 | `entities.mob-effects.immune-to-wither-effect.wither` | `true` | bool | — |
| 凋灵骷髅免疫凋零 | `entities.mob-effects.immune-to-wither-effect.wither-skeleton` | `true` | bool | — |
| 蜘蛛免疫中毒 | `entities.mob-effects.spiders-immune-to-poison-effect` | `true` | bool | — |

### 3.12 实体 - 嗅探兽

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 加速孵化时间 | `entities.sniffer.boosted-hatch-time` | `default` | `default` / 正整数（tick） | — |
| 孵化时间 | `entities.sniffer.hatch-time` | `default` | `default` / 正整数（tick） | — |

### 3.13 实体 - 生成（spawning）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **每玩家独立生成** | `entities.spawning.per-player-mob-spawns` | `true` | bool | **最重要设置之一** |
| 全是史莱姆区块 | `entities.spawning.all-chunks-are-slime-chunks` | `false` | bool | — |
| 怪物最大光照等级 | `entities.spawning.monster-spawn-max-light-level` | `default` | `default` / 0~15 | — |
| 计算所有生物用于生成 | `entities.spawning.count-all-mobs-for-spawning` | `false` | bool | — |
| 消失范围形状 | `entities.spawning.despawn-range-shape` | `ELLIPSOID` | `ELLIPSOID` / `CYLINDER` | — |
| 强制/随机消失距离 | `entities.spawning.despawn-ranges.<类别>.hard/soft` | `default` | `default` / 正整数（方块） | 类别：ambient / creature / monster / misc / water_ambient / water_creature / underground_water_creature / axolotls |
| 启用替代物品消失 | `entities.spawning.alt-item-despawn-rate.enabled` | `false` | bool | — |
| 圆石消失速率 | `entities.spawning.alt-item-despawn-rate.items.cobblestone` | `300` | 正整数（tick） | 300=15秒 |
| 创造箭矢消失 | `entities.spawning.creative-arrow-despawn-rate` | `default` | `default` / 正整数 | — |
| 非玩家箭矢消失 | `entities.spawning.non-player-arrow-despawn-rate` | `default` | `default` / 正整数 | — |
| 禁用刷怪蛋变刷怪笼 | `entities.spawning.disable-mob-spawner-spawn-egg-transformation` | `false` | bool | — |
| 重复 UUID 模式 | `entities.spawning.duplicate-uuid.mode` | `SAFE_REGEN` | `SAFE_REGEN` / `DELETE` / `NOTHING` / `WARN` | — |
| 安全重生成删除范围 | `entities.spawning.duplicate-uuid.safe-regen-delete-range` | `32` | 正整数（方块） | — |
| 铁傀儡空中生成 | `entities.spawning.iron-golems-can-spawn-in-air` | `false` | bool | — |
| 扫描旧版末影龙 | `entities.spawning.scan-for-legacy-ender-dragon` | `true` | bool | — |
| 骷髅马雷暴概率 | `entities.spawning.skeleton-horse-thunder-spawn-chance` | `default` | `default` / 浮点数 | — |
| 史莱姆区块最大高度 | `entities.spawning.slime-spawn-height.slime-chunk.maximum` | `40.0` | 浮点数 | — |
| 表面史莱姆最大高度 | `entities.spawning.slime-spawn-height.surface-biome.maximum` | `70.0` | 浮点数 | — |
| 表面史莱姆最小高度 | `entities.spawning.slime-spawn-height.surface-biome.minimum` | `50.0` | 浮点数 | — |
| 生物生成上限(各分类) | `entities.spawning.spawn-limits.<类别>` | `-1` | int，-1=用 bukkit.yml | — |
| 生成间隔(各分类) | `entities.spawning.ticks-per-spawn.<类别>` | `-1` | int，-1=用 bukkit.yml | — |
| 流浪商人失败递增 | `entities.spawning.wandering-trader.spawn-chance-failure-increment` | `25` | 正整数 | — |
| 流浪商人最大概率 | `entities.spawning.wandering-trader.spawn-chance-max` | `75` | 正整数 | — |
| 流浪商人最小概率 | `entities.spawning.wandering-trader.spawn-chance-min` | `25` | 正整数 | — |
| 流浪商人生成日长度 | `entities.spawning.wandering-trader.spawn-day-length` | `24000` | 正整数（tick） | — |
| 流浪商人生成分钟长度 | `entities.spawning.wandering-trader.spawn-minute-length` | `1200` | 正整数（tick） | — |
| 水生生物最大高度 | `entities.spawning.wateranimal-spawn-height.maximum` | `default` | `default` / 浮点数 | — |
| 水生生物最小高度 | `entities.spawning.wateranimal-spawn-height.minimum` | `default` | `default` / 浮点数 | — |

### 3.14 实体 - Y 轴追踪

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 启用 Y 轴追踪 | `entities.tracking-range-y.enabled` | `false` | bool | — |
| 各类型 Y 轴范围 | `entities.tracking-range-y.<类型>` | `default` | `default` / 正整数 | 类型：animal / display / monster / player / misc / other |

### 3.15 漏斗

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 满时冷却 | `hopper.cooldown-when-full` | `true` | bool | — |
| 禁用移动事件 | `hopper.disable-move-event` | `false` | bool | 禁用可提升漏斗性能 |
| 忽略遮挡方块 | `hopper.ignore-occluding-blocks` | `false` | bool | — |

### 3.16 战利品刷新

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 自动补充 | `lootables.auto-replenish` | `false` | bool | — |
| 最大补充次数 | `lootables.max-refills` | `-1` | int，-1=无限 | — |
| 最大刷新间隔 | `lootables.refresh-max` | `2d` | 时间字符串 | — |
| 最小刷新间隔 | `lootables.refresh-min` | `12h` | 时间字符串 | — |
| 填充时重置种子 | `lootables.reset-seed-on-fill` | `true` | bool | — |
| 限制重复拾取 | `lootables.restrict-player-reloot` | `true` | bool | — |
| 重复拾取限制时间 | `lootables.restrict-player-reloot-time` | `disabled` | `disabled` / 时间字符串 | — |
| 保留潜影盒战利品表 | `lootables.retain-unlooted-shulker-box-loot-table-on-non-player-break` | `true` | bool | — |

### 3.17 地图

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 物品展示框光标限制 | `maps.item-frame-cursor-limit` | `128` | 正整数 | — |
| 物品展示框更新间隔 | `maps.item-frame-cursor-update-interval` | `10` | 正整数（tick） | — |

### 3.18 最大生长高度

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 竹子最大 | `max-growth-height.bamboo.max` | `16` | 正整数 | — |
| 竹子最小 | `max-growth-height.bamboo.min` | `11` | 正整数 | — |
| 仙人掌 | `max-growth-height.cactus` | `3` | 正整数 | — |
| 甘蔗 | `max-growth-height.reeds` | `3` | 正整数 | — |

### 3.19 钓鱼

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 最小时间 | `fishing-time-range.minimum` | `100` | 正整数（tick） | — |
| 最大时间 | `fishing-time-range.maximum` | `600` | 正整数（tick） | — |

### 3.20 修复项

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 禁用未加载区块末影珍珠 | `fixes.disable-unloaded-chunk-enderpearl-exploit` | `false` | bool | — |
| 掉落方块高度削弱 | `fixes.falling-block-height-nerf` | `disabled` | `disabled` / 浮点数 | — |
| 修复物品穿墙合并 | `fixes.fix-items-merging-through-walls` | `false` | bool | — |
| 阻止 TNT 水中移动 | `fixes.prevent-tnt-from-moving-in-water` | `false` | bool | — |
| 拆分超堆叠 | `fixes.split-overstacked-loot` | `true` | bool | — |
| TNT 高度削弱 | `fixes.tnt-entity-height-nerf` | `disabled` | `disabled` / 浮点数 | — |

### 3.21 计分板

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 非玩家在计分板 | `scoreboards.allow-non-player-entities-on-scoreboards` | `true` | bool | — |
| 原版名称着色 | `scoreboards.use-vanilla-world-scoreboard-name-coloring` | `false` | bool | — |

### 3.22 出生点

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 保护区内用告示牌 | `spawn.allow-using-signs-inside-spawn-protection` | `false` | bool | — |

### 3.23 不支持设置（世界级）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 空世界停止 tick | `unsupported-settings.disable-world-ticking-when-empty` | `false` | bool | — |
| 修复无敌末影水晶 | `unsupported-settings.fix-invulnerable-end-crystal-exploit` | `true` | bool | — |

---

### 四、bukkit.yml（Bukkit 系通用）

> 文件路径：根目录 `bukkit.yml`
> 官方来源：[PaperMC Bukkit 配置参考](https://docs.papermc.io/paper/reference/bukkit-configuration)

### 4.1 全局设置

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 允许末地 | `settings.allow-end` | `true` | bool | — |
| 过载警告 | `settings.warn-on-overload` | `true` | bool | — |
| 权限文件 | `settings.permissions-file` | `permissions.yml` | 文件路径 | — |
| 更新文件夹 | `settings.update-folder` | `update` | 文件夹名 | — |
| 插件性能分析 | `settings.plugin-profiling` | `false` | bool | — |
| 连接节流间隔 | `settings.connection-throttle` | `4000` | 正整数（毫秒），-1=禁用 | 同 IP 重连间隔 |
| 查询显示插件 | `settings.query-plugins` | `true` | bool | — |
| 过时 API 警告 | `settings.deprecated-verbose` | `default` | `default` / `true` / `false` | — |
| 关服消息 | `settings.shutdown-message` | `Server closed` | 字符串 | — |
| 最低 API 版本 | `settings.minimum-api` | `none` | 字符串 | — |
| 地图颜色缓存 | `settings.use-map-color-cache` | `true` | bool | — |
| 世界容器目录 | `settings.world-container` | `N/A` | 路径 | — |

### 4.2 生物生成上限

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 怪物上限 | `spawn-limits.monsters` | `70` | 正整数 | — |
| 动物上限 | `spawn-limits.animals` | `10` | 正整数 | — |
| 水生动物上限 | `spawn-limits.water-animals` | `5` | 正整数 | — |
| 水生环境上限 | `spawn-limits.water-ambient` | `20` | 正整数 | — |
| 地下水生上限 | `spawn-limits.water-underground-creature` | `5` | 正整数 | — |
| 美西螈上限 | `spawn-limits.axolotls` | `5` | 正整数 | — |
| 环境生物上限 | `spawn-limits.ambient` | `15` | 正整数 | 蝙蝠等 |

### 4.3 区块 GC 与 Tick 间隔

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 区块 GC 周期 | `chunk-gc.period-in-ticks` | `600` | 正整数（tick） | — |
| 动物生成间隔 | `ticks-per.animal-spawns` | `400` | 正整数（tick） | — |
| 怪物生成间隔 | `ticks-per.monster-spawns` | `1` | 正整数（tick） | — |
| 水生生成间隔 | `ticks-per.water-spawns` | `1` | 正整数（tick） | — |
| 水生环境生成间隔 | `ticks-per.water-ambient-spawns` | `1` | 正整数（tick） | — |
| 地下水生生成间隔 | `ticks-per.water-underground-creature-spawns` | `1` | 正整数（tick） | — |
| 美西螈生成间隔 | `ticks-per.axolotl-spawns` | `1` | 正整数（tick） | — |
| 环境生物生成间隔 | `ticks-per.ambient-spawns` | `1` | 正整数（tick） | — |
| 自动保存间隔 | `ticks-per.autosave` | `6000` | 正整数（tick） | 6000=5分钟 |

---

### 五、spigot.yml（Spigot 及下游通用）

> 文件路径：根目录 `spigot.yml`
> 世界设置在 `world-settings.default:` 下，可按世界名覆盖
> 官方来源：[PaperMC Spigot 配置参考](https://docs.papermc.io/paper/reference/spigot-configuration)

### 5.1 全局设置

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 禁用进度保存 | `advancements.disable-saving` | `false` | bool | — |
| 禁用的进度 | `advancements.disabled` | `[minecraft:story/disabled]` | 字符串列表 | — |
| 记录命令 | `commands.log` | `true` | bool | — |
| 替换的命令 | `commands.replace-commands` | `[setblock, summon]` | 字符串列表 | — |
| 发送命名空间命令 | `commands.send-namespaced` | `true` | bool | — |
| 命令方块静默 | `commands.silent-commandblock-console` | `false` | bool | — |
| 命令垃圾排除 | `commands.spam-exclusions` | `[/skill]` | 字符串列表 | — |
| Tab 补全模式 | `commands.tab-complete` | `0` | int | 0=在线玩家，1=所有，-1=禁用 |
| 禁用玩家保存 | `players.disable-saving` | `false` | bool | — |
| 禁用统计保存 | `stats.disable-saving` | `false` | bool | — |
| 攻击伤害上限 | `settings.attribute.attackDamage.max` | `2048.0` | 浮点数 | — |
| 最大吸收上限 | `settings.attribute.maxAbsorption.max` | `2048.0` | 浮点数 | — |
| 最大生命上限 | `settings.attribute.maxHealth.max` | `2048.0` | 浮点数 | — |
| 移动速度上限 | `settings.attribute.movementSpeed.max` | `2048.0` | 浮点数 | — |
| BungeeCord | `settings.bungeecord` | `false` | bool | — |
| 调试模式 | `settings.debug` | `false` | bool | — |
| 记录命名实体死亡 | `settings.log-named-deaths` | `true` | bool | — |
| 记录村民死亡 | `settings.log-villager-deaths` | `true` | bool | — |
| 移动过快倍数 | `settings.moved-too-quickly-multiplier` | `10.0` | 正浮点数 | — |
| 错误移动阈值 | `settings.moved-wrongly-threshold` | `0.0625` | 正浮点数 | — |
| **Netty 线程数** | `settings.netty-threads` | `4` | 正整数 | **每 200-300 玩家约需 4 线程** |
| 玩家顺序打乱间隔 | `settings.player-shuffle` | `0` | 正整数（tick），0=禁用 | — |
| 崩溃自动重启 | `settings.restart-on-crash` | `true` | bool | — |
| 重启脚本 | `settings.restart-script` | `./start.sh` | 文件路径 | — |
| Ping 采样数 | `settings.sample-count` | `12` | 正整数 | — |
| 仅关服保存缓存 | `settings.save-user-cache-on-stop-only` | `false` | bool | — |
| 登录超时 | `settings.timeout-time` | `60` | 正整数（秒） | — |
| 用户缓存大小 | `settings.user-cache-size` | `1000` | 正整数 | — |

### 5.2 世界设置（world-settings.default:）

#### 距离与范围

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **生物生成范围** | `mob-spawn-range` | `8` | 正整数（区块） | 以区块为单位的生成距离 |
| 动物生成范围 | `animal-spawn-range` | `8` | 正整数（区块） | — |
| 模拟距离 | `simulation-distance` | `default` | `default` / 正整数 | 覆盖 server.properties |
| 视距 | `view-distance` | `default` | `default` / 正整数 | 覆盖 server.properties |

#### 实体激活范围（entity-activation-range）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 动物激活范围 | `entity-activation-range.animals` | `32` | 正整数（方块） | — |
| 飞行怪物 | `entity-activation-range.flying-monsters` | `32` | 正整数 | — |
| 杂项 | `entity-activation-range.misc` | `16` | 正整数 | — |
| 怪物 | `entity-activation-range.monsters` | `32` | 正整数 | — |
| 掠夺者 | `entity-activation-range.raiders` | `64` | 正整数 | — |
| 村民 | `entity-activation-range.villagers` | `32` | 正整数 | — |
| 水生 | `entity-activation-range.water` | `16` | 正整数 | — |
| 忽略旁观者 | `entity-activation-range.ignore-spectators` | `false` | bool | — |
| 非激活村民仍 tick | `entity-activation-range.tick-inactive-villagers` | `true` | bool | 用于补货 |
| 村民恐慌时激活 | `entity-activation-range.villagers-active-for-panic` | `true` | bool | — |
| 村民工作免疫后 | `entity-activation-range.villagers-work-immunity-after` | `100` | 正整数（tick） | — |
| 村民工作免疫持续 | `entity-activation-range.villagers-work-immunity-for` | `20` | 正整数（tick） | — |

#### 唤醒间隔（wake-up-inactive）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 动物唤醒间隔 | `wake-up-inactive.animals-every` | `1200` | 正整数（tick） | — |
| 动物唤醒持续 | `wake-up-inactive.animals-for` | `100` | 正整数（tick） | — |
| 动物唤醒最大数 | `wake-up-inactive.animals-max-per-tick` | `4` | 正整数 | — |
| 飞行怪物唤醒间隔 | `wake-up-inactive.flying-monsters-every` | `200` | 正整数 | — |
| 飞行怪物唤醒持续 | `wake-up-inactive.flying-monsters-for` | `100` | 正整数 | — |
| 飞行怪物唤醒最大数 | `wake-up-inactive.flying-monsters-max-per-tick` | `8` | 正整数 | — |
| 怪物唤醒间隔 | `wake-up-inactive.monsters-every` | `400` | 正整数 | — |
| 怪物唤醒持续 | `wake-up-inactive.monsters-for` | `100` | 正整数 | — |
| 怪物唤醒最大数 | `wake-up-inactive.monsters-max-per-tick` | `8` | 正整数 | — |
| 村民唤醒间隔 | `wake-up-inactive.villagers-every` | `600` | 正整数 | — |
| 村民唤醒持续 | `wake-up-inactive.villagers-for` | `100` | 正整数 | — |
| 村民唤醒最大数 | `wake-up-inactive.villagers-max-per-tick` | `4` | 正整数 | — |

#### 实体追踪范围（entity-tracking-range）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 动物 | `entity-tracking-range.animals` | `96` | 正整数（方块） | — |
| 展示实体 | `entity-tracking-range.display` | `128` | 正整数 | — |
| 杂项 | `entity-tracking-range.misc` | `96` | 正整数 | — |
| 怪物 | `entity-tracking-range.monsters` | `96` | 正整数 | — |
| 其他 | `entity-tracking-range.other` | `64` | 正整数 | — |
| 玩家 | `entity-tracking-range.players` | `128` | 正整数 | — |

#### 生长修正（growth，百分比）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 竹子 | `growth.bamboo-modifier` | `100` | 正整数 | 100=原版速度 |
| 甜菜根 | `growth.beetroot-modifier` | `100` | 正整数 | — |
| 仙人掌 | `growth.cactus-modifier` | `100` | 正整数 | — |
| 甘蔗 | `growth.cane-modifier` | `100` | 正整数 | — |
| 胡萝卜 | `growth.carrot-modifier` | `100` | 正整数 | — |
| 洞穴藤蔓 | `growth.cavevines-modifier` | `100` | 正整数 | — |
| 可可豆 | `growth.cocoa-modifier` | `100` | 正整数 | — |
| 发光浆果 | `growth.glowberry-modifier` | `100` | 正整数 | — |
| 海带 | `growth.kelp-modifier` | `100` | 正整数 | — |
| 西瓜 | `growth.melon-modifier` | `100` | 正整数 | — |
| 蘑菇 | `growth.mushroom-modifier` | `100` | 正整数 | — |
| 地狱疣 | `growth.netherwart-modifier` | `100` | 正整数 | — |
| 瓶子草 | `growth.pitcherplant-modifier` | `100` | 正整数 | — |
| 马铃薯 | `growth.potato-modifier` | `100` | 正整数 | — |
| 南瓜 | `growth.pumpkin-modifier` | `100` | 正整数 | — |
| 树苗 | `growth.sapling-modifier` | `100` | 正整数 | — |
| 甜浆果 | `growth.sweetberry-modifier` | `100` | 正整数 | — |
| 火炬花 | `growth.torchflower-modifier` | `100` | 正整数 | — |
| 缠怨藤 | `growth.twistingvines-modifier` | `100` | 正整数 | — |
| 藤蔓 | `growth.vine-modifier` | `100` | 正整数 | — |
| 垂泪藤 | `growth.weepingvines-modifier` | `100` | 正整数 | — |
| 小麦 | `growth.wheat-modifier` | `100` | 正整数 | — |

#### 其他世界设置

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 卸载冻结区块 | `unload-frozen-chunks` | `false` | bool | — |
| 箭矢消失速率 | `arrow-despawn-rate` | `1200` | 正整数（tick） | — |
| 已有区块生成 y<0 | `below-zero-generation-in-existing-chunks` | `true` | bool | — |
| 末影龙死亡声音半径 | `dragon-death-sound-radius` | `0` | 正整数，0=无限制 | — |
| 僵尸猪灵传送门生成 | `enable-zombie-pigmen-portal-spawns` | `true` | bool | — |
| 末地门声音半径 | `end-portal-sound-radius` | `0` | 正整数，0=无限制 | — |
| 悬挂实体 tick 频率 | `hanging-tick-frequency` | `100` | 正整数（tick） | — |
| **漏斗每次传输数** | `hopper-amount` | `1` | 正整数 | — |
| 漏斗可加载区块 | `hopper-can-load-chunks` | `false` | bool | — |
| 物品消失速率 | `item-despawn-rate` | `6000` | 正整数（tick） | — |
| 实体最大 tick 时间 | `max-tick-time.entity` | `50` | 正整数（秒） | — |
| 方块实体最大 tick | `max-tick-time.tile` | `50` | 正整数（秒） | — |
| 每 tick 最大 TNT | `max-tnt-per-tick` | `100` | 正整数 | — |
| 经验球合并半径 | `merge-radius.exp` | `-1` | 浮点数，-1=原版 | — |
| 物品合并半径 | `merge-radius.item` | `0.5` | 浮点数 | — |
| 削弱刷怪笼生物 | `nerf-spawner-mobs` | `false` | bool | 不保留 AI |
| 雷暴概率 | `thunder-chance` | `100000` | 正整数 | 1/N 每 tick |
| 漏斗检查间隔 | `ticks-per.hopper-check` | `1` | 正整数（tick） | — |
| **漏斗传输间隔** | `ticks-per.hopper-transfer` | `8` | 正整数（tick） | **红石漏斗机关核心参数** |
| 三叉戟消失速率 | `trident-despawn-rate` | `1200` | 正整数（tick） | — |
| 详细日志 | `verbose` | `false` | bool | — |
| 凋灵声音半径 | `wither-spawn-sound-radius` | `0` | 正整数，0=无限制 | — |
| 僵尸攻击村民 | `zombie-aggressive-towards-villager` | `true` | bool | — |

#### 饥饿

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| 战斗消耗 | `hunger.combat-exhaustion` | `0.1` | 浮点数 | — |
| 疾跑跳跃消耗 | `hunger.jump-sprint-exhaustion` | `0.2` | 浮点数 | — |
| 行走跳跃消耗 | `hunger.jump-walk-exhaustion` | `0.05` | 浮点数 | — |
| 其他饥饿倍数 | `hunger.other-multiplier` | `0.0` | 浮点数 | — |
| 生命恢复消耗 | `hunger.regen-exhaustion` | `6.0` | 浮点数 | — |
| 疾跑倍数 | `hunger.sprint-multiplier` | `0.1` | 浮点数 | — |
| 游泳倍数 | `hunger.swim-multiplier` | `0.01` | 浮点数 | — |

---

### 六、红石与大型机器配置速查

> 针对你的"恐怖级别红石"场景，以下配置最关键

| 优先级 | 文件 | 配置键 | 默认值 | 建议值 | 说明 |
|--------|------|--------|--------|--------|------|
| **最高** | paper-global.yml | `threaded-regions.gridExponent` | `4` | **6** | 区域从 256×256 格扩大到 1024×1024 格，红石信号在区域内完全正常 |
| **最高** | paper-global.yml | `threaded-regions.threads` | `-1` | **根据核心数手动设** | 减去 Netty/IO/GC 后的 80% |
| **高** | paper-world-defaults.yml | `misc.redstone-implementation` | `VANILLA` | `ALTERNATE_CURRENT` | 红石计算减少 90%+ |
| **高** | paper-world-defaults.yml | `environment.max-block-ticks` | `65536` | 按需 | 限制每 tick 方块更新数 |
| **高** | server.properties | `simulation-distance` | `10` | **≤32**（建议 10~12） | 模拟距离不超过区域半径 |
| **中** | paper-world-defaults.yml | `environment.max-fluid-ticks` | `65536` | 按需 | 限制每 tick 流体计算 |
| **中** | paper-world-defaults.yml | `tick-rates.mob-spawner` | `1` | `2` | 刷怪笼开销减半 |
| **中** | paper-world-defaults.yml | `tick-rates.grass-spread` | `1` | `4` | 草蔓延频率降低 |
| **中** | paper-world-defaults.yml | `misc.update-pathfinding-on-block-update` | `true` | `false` | 大幅减少寻路开销 |
| **中** | spigot.yml | `ticks-per.hopper-transfer` | `8` | 按需 | 漏斗传输间隔 |
| **中** | spigot.yml | `hopper-amount` | `1` | 按需 | 漏斗每次传输数 |
| **低** | paper-world-defaults.yml | `collisions.max-entity-collisions` | `8` | `2~4` | 减少碰撞计算 |
| **低** | paper-world-defaults.yml | `chunks.max-auto-save-chunks-per-tick` | `24` | `8~12` | HDD 减少卡顿 |
| **低** | server.properties | `region-file-compression` | `deflate` | `lz4` | 区块读写更快 |
| **低** | server.properties | `sync-chunk-writes` | `true` | `false`（SSD） | 异步写入提速 |

---

### 七、26.1.2 版本新增与变更配置项

> Mojang 从 26.x 起改用年份命名体系（26 = 2026 年）。以下为 26.1.2 相比 1.21.x 的**所有配置变更**。
> 来源：[PaperMC 26.1 更新公告](https://papermc.io/news/26-1/)、[PaperMC 26.1.2 官方配置文档](https://docs.papermc.io/paper/reference/global-configuration/)、[PaperMC 世界配置文档](https://docs.papermc.io/paper/reference/world-configuration/)

### 7.1 重大结构性变更

#### 世界存储结构重组（影响所有服务端）

26.1 对世界存储方式做了根本性变更。维度数据不再放在服务端根目录的独立文件夹中，而是统一放在 `world/dimensions/` 下：

```
world/
├── data/minecraft/...
├── datapacks/
├── dimensions/
│   └── minecraft/
│       ├── overworld/
│       │   ├── data/minecraft/...
│       │   ├── data/paper/          ← Paper 专有数据新位置
│       │   │   ├── level_overrides.dat
│       │   │   ├── metadata.dat
│       │   │   └── persistent_data_container.dat
│       │   ├── entities/
│       │   ├── poi/
│       │   ├── region/
│       │   └── paper-world.yml      ← 每维度独立配置文件（原在 config/worlds/ 下）
│       ├── the_nether/
│       │   └── ...
│       └── the_end/
│           └── ...
├── players/
└── level.dat
```

**关键变化：**
- `paper-world.yml` 从 `config/worlds/<世界名>/paper-world.yml` 移到 `world/dimensions/minecraft/<维度名>/paper-world.yml`
- Paper 专有数据（`level_overrides.dat` 等）现在存在维度目录的 `data/paper/` 下
- **升级后不可降级**——从 26.1 升级后无法回到 1.21.x

#### 压缩格式合并

Paper 的 `unsupported-settings.compression-format` 已合并进原版 `server.properties` 的 `region-file-compression`，且原版现在额外支持 `gzip` 选项。

| 中文含义 | 英文键名 | 变更说明 |
|----------|----------|----------|
| 区域文件压缩 | `server.properties` → `region-file-compression` | 可选值新增 `gzip`（原仅 `deflate`/`lz4`/`none`）。Paper 的 `unsupported-settings.compression-format` 已**移除** |

#### API 版本号格式变更

Mojang 不再提供混淆 jar，Paper 版本号格式改为 `26.1.2.build.<build>-<status>`。插件开发者在 build.gradle 中使用 `26.1.2.build.+` 自动获取最新构建。

---

### 7.2 paper-global.yml 新增配置项

#### 时间系统（time 节点，全新）

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **时间影响所有世界** | `time.affects-all-worlds` | `false` | bool | 26.1 新增。`false`=每个世界独立时钟（Paper 默认行为）；`true`=按维度类型共享时钟（原版行为）。影响 `/time` 命令和 World time API |

#### 杂项新增/变更

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **最大战斗追踪条目数** | `misc.max-tracking-combat-entries` | `10240` | 正整数 | **26.1 新增**。服务器记录的最大战斗条目数，超出后丢弃旧条目。修复了内存泄漏问题 |
| **启用下界** | `misc.enable-nether` | `true` | bool | **26.1 新增**。是否启用并加载下界维度 |
| **修复远末地地形生成** | `misc.fix-far-end-terrain-generation` | `true` | bool | **26.1 新增**。修复 MC-159283，极远距离末地外岛的环形地形异常 |

#### 数据包限制器变更

| 中文含义 | 英文键名 | 变更说明 |
|----------|----------|----------|
| 入站包阈值 | `spam-limiter.incoming-packet-threshold` | 现在可设为 **`-1`** 来完全禁用（之前无禁用选项） |

#### 合成配方包重命名

| 旧键名（1.21.x） | 新键名（26.1.x） |
|-------------------|-------------------|
| `packet-limiter.overrides.ServerboundPlaceRecipePacket.*` | `packet-limiter.overrides.minecraft:place_recipe.*` |

#### 不支持设置变更

| 中文含义 | 英文键名 | 变更说明 |
|----------|----------|----------|
| 活塞复制 | `unsupported-settings.allow-piston-duplication` | 不再控制沙子复制（仅控制 TNT/地毯/铁轨） |
| 压缩格式 | `unsupported-settings.compression-format` | **已移除**，改用 `server.properties` 的 `region-file-compression` |
| **禁用区块 tick** | `unsupported-settings.ticking.chunks` | **26.1 新增**。设为 false 可完全禁用区块 tick（刷怪、随机方块 tick、雷暴、冰和雪）。大厅服务器适用 |
| **禁用方块实体 tick** | `unsupported-settings.ticking.blockEntities` | **26.1 新增**。设为 false 可禁用方块实体 tick（漏斗、熔炉等功能停止）。大厅服务器适用 |

#### 启动参数新增

| 中文含义 | 参数 | 说明 |
|----------|------|------|
| 附加插件目录 | `--add-plugin-dir=<路径>` | 启动时加载额外插件目录 |

---

### 7.3 paper-world-defaults.yml 新增配置项

#### 实体生成新增

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **箭矢无敌消失延迟** | `entities.spawning.max-arrow-despawn-invulnerability` | `200` | 正整数（tick） | **26.1 新增**（从 paper-global.yml 移入）。MC-125757 修复，箭矢在落地后多少 tick 开始计算消失时间 |

#### 实体行为新增

| 中文含义 | 英文键名 | 默认值 | 取值范围 | 说明 |
|----------|----------|--------|----------|------|
| **卡住实体 POI 重试延迟** | `entities.behavior.stuck-entity-poi-retry-delay` | `200` | 正整数（tick） | **26.1 新增**。实体导航卡住时重试获取 POI 的延迟，降低寻路性能影响 |
| **实体用跟随范围锁定目标** | `entities.entities-target-with-follow-range` | `false` | bool | **26.1 新增**。服务器是否使用跟随范围来锁定目标实体 |

#### 碰撞变更

| 中文含义 | 英文键名 | 变更说明 |
|----------|----------|----------|
| 破门难度键名变更 | `entities.behavior.door-breaking-difficulty.<entity>` | 26.1 中统一为嵌套在 `door-breaking-difficulty` 下的子键，结构不变 |

#### 区块保存限制扩展

| 中文含义 | 英文键名 | 变更说明 |
|----------|----------|----------|
| 每区块实体保存上限 | `chunks.entity-per-chunk-save-limit.<entity-type>` | **26.1 扩展**：现在可以指定**任意实体类型**，不再局限于预设的箭矢/末影珍珠/经验球/火球/雪球 |

---

### 7.4 server.properties 变更

| 中文含义 | 英文键名 | 变更说明 |
|----------|----------|----------|
| 区域文件压缩 | `region-file-compression` | 可选值新增 **`gzip`**（原 `deflate`/`lz4`/`none`） |
| 版本号体系 | — | Mojang 改用年份命名：26.1.2 = 2026 年第 1 次 dropping，patch 2 |

---

### 7.5 其他重要变更

#### Folia 专有：线程检查修复

26.1.2 修复了实体交互包处理的缺失线程检查（`Fix missing thread check for entity interact packet handling`），这直接影响红石装置与玩家交互时的线程安全性。

#### 红石速查表更新（26.1.2 版）

| 优先级 | 文件 | 配置键 | 默认值 | 建议值 | 说明 |
|--------|------|--------|--------|--------|------|
| **最高** | paper-global.yml | `threaded-regions.gridExponent` | `4` | **6** | 区域 1024×1024 格，红石在区域内完全正常 |
| **最高** | paper-global.yml | `threaded-regions.threads` | `-1` | **手动设** | 减去 Netty/IO/GC 后的 80% |
| **高** | paper-world-defaults.yml | `misc.redstone-implementation` | `VANILLA` | `ALTERNATE_CURRENT` | 红石计算减少 90%+ |
| **高** | paper-world-defaults.yml | `environment.max-block-ticks` | `65536` | 按需 | 每 tick 最大方块更新数 |
| **高** | server.properties | `simulation-distance` | `10` | **10~12** | 不超过区域半径 |
| **高** | server.properties | `region-file-compression` | `deflate` | **`lz4`** | 26.1 新增 gzip 选项，但 lz4 仍是最快 |
| **中** | paper-world-defaults.yml | `environment.max-fluid-ticks` | `65536` | 按需 | 每 tick 最大流体计算 |
| **中** | paper-world-defaults.yml | `tick-rates.mob-spawner` | `1` | `2` | 刷怪笼开销减半 |
| **中** | paper-world-defaults.yml | `tick-rates.grass-spread` | `1` | `4` | 草蔓延频率降低 |
| **中** | paper-world-defaults.yml | `misc.update-pathfinding-on-block-update` | `true` | `false` | 大幅减少寻路开销 |
| **中** | paper-global.yml | `misc.max-tracking-combat-entries` | `10240` | 按需 | **26.1 新增**，防止内存泄漏 |
| **中** | spigot.yml | `ticks-per.hopper-transfer` | `8` | 按需 | 漏斗传输间隔 |
| **低** | paper-world-defaults.yml | `collisions.max-entity-collisions` | `8` | `2~4` | 减少碰撞计算 |
| **低** | paper-world-defaults.yml | `chunks.max-auto-save-chunks-per-tick` | `24` | `8~12` | 减少卡顿 |
| **低** | server.properties | `sync-chunk-writes` | `true` | `false`（SSD） | 异步写入提速 |

---

## 第二部分：Paper 分支核心专属配置

> 以下核心**继承 Paper 全部共享配置**（第一部分一至五节），额外拥有专属配置文件。

---

### 八、purpur.yml（Purpur 核心专属）

> 文件路径：根目录 `purpur.yml`
> 适用核心：**Purpur**（基于 Paper）
> 官方来源：[Purpur GitHub](https://github.com/PurpurMC/Purpur)

#### 8.1 全局设置（settings）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 日志玩家 IP 地址 | `log-player-ip-addresses` | bool | `true` | bool | — |
| 日志玩家连接 | `log-player-connections` | bool | `true` | bool | — |
| 降级玩家名称验证 | `use-display-name-in-quit-message` | bool | `false` | bool | — |
| 禁用 MC 勘误 | `bStats-collect-enabled` | bool | `true` | bool | — |
| 服务器品牌名 | `server-mod-name` | string | `Purpur` | 字符串 | 在 /version 中显示 |
| 无法保存玩家数据 | `cannot-save-player-data` | bool | `false` | bool | — |
| 使用替代幸运公式 | `use-alternate-luck-formula` | bool | `false` | bool | — |

#### 8.2 信息与提示（messages）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 无权限消息 | `no-permission` | string | `§cI'm sorry, but you do not have permission to perform this command. Please contact the server administrators if you believe that this is in error.` | 字符串 | — |
| 使用 ACF 消息 | `use-aco-format` | bool | `false` | bool | 使用 AdventureChat 格式化 |

#### 8.3 TPSbar 命令

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用 TPSbar | `commands.tpsbar.enabled` | bool | `true` | bool | — |
| 每秒显示 TPS | `commands.tpsbar.tps-interval` | int | `1` | 正整数 | — |
| 每秒显示 MSPT | `commands.tpsbar.mspt-interval` | int | `1` | 正整数 | — |
| 时钟式 HUD | `commands.tpsbar.clock` | bool | `false` | bool | 显示为时钟格式 |
| 颜色-高 TPS | `commands.tpsbar.color-high` | string | `GREEN` | 颜色名/十六进制 | TPS ≥ 18 |
| 颜色-中 TPS | `commands.tpsbar.color-med` | string | `YELLOW` | 颜色名/十六进制 | TPS 15~18 |
| 颜色-低 TPS | `commands.tpsbar.color-low` | string | `RED` | 颜色名/十六进制 | TPS < 15 |

#### 8.4 RAMbar 命令

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用 RAMbar | `commands.rambar.enabled` | bool | `true` | bool | — |
| 每秒显示 RAM | `commands.rambar.ram-interval` | int | `1` | 正整数 | — |
| 显示最大内存 | `commands.rambar.show-max` | bool | `true` | bool | — |
| 显示总内存 | `commands.rambar.show-total` | bool | `true` | bool | — |
| 显示已用内存 | `commands.rambar.show-used` | bool | `true` | bool | — |
| 显示空闲内存 | `commands.rambar.show-free` | bool | `true` | bool | — |
| 颜色-高空闲 | `commands.rambar.color-high` | string | `GREEN` | 颜色名/十六进制 | 空闲 ≥ 50% |
| 颜色-中空闲 | `commands.rambar.color-med` | string | `YELLOW` | 颜色名/十六进制 | 空闲 20%~50% |
| 颜色-低空闲 | `commands.rambar.color-low` | string | `RED` | 颜色名/十六进制 | 空闲 < 20% |

#### 8.5 世界设置 - 玩法（world-settings.default.gameplay-mechanics）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 禁用 undead 伤害免疫 | `disable-undead-universal-damage-immunity` | bool | `false` | bool | 允许亡灵生物受到所有伤害 |
| 禁用玩家受伤无敌帧 | `disable-player-crits` | bool | `false` | bool | — |
| 修复遮挡物品拾取 | `fix-items-merging-through-walls` | bool | `false` | bool | — |
| 铁砧颜色支持 | `anvil-color-in-hopper` | bool | `false` | bool | — |
| 传送骑乘实体 | `teleport-vehicle-with-player` | bool | `false` | bool | — |
| 床不重置重生点 | `bed-does-not-reset-spawn` | bool | `false` | bool | — |
| 旁观者使用容器 | `allow-spectator-use-containers` | bool | `false` | bool | — |
| 末影人放下方块 | `enderman-pickup-blocks` | bool | `true` | bool | — |
| 末影人放置方块 | `enderman-place-blocks` | bool | `true` | bool | — |
| 铁傀儡传送门生成 | `iron-golems-can-spawn-in-air` | bool | `false` | bool | — |
| 返回重生点延迟 | `bed-respawn-delay` | int | `0` | 正整数（tick） | — |
| 村民死亡不损失声望 | `villager-death-does-not-affect-reputation` | bool | `false` | bool | — |
| 禁用 TNT 水中漂移 | `prevent-tnt-from-moving-in-water` | bool | `false` | bool | — |
| 挖掘药水隐藏效果 | `mining-fatigue-affects-break-speed` | bool | `true` | bool | — |
| 创造模式攻击实体 | `creative-mode-attack-damage` | bool | `true` | bool | — |

#### 8.6 世界设置 - 生物（world-settings.default.mobs）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 基岩版下界总动员 | `toggle-nether-portals-on-crafting-table` | bool | `true` | bool | — |
| 蜜蜂不蜇创造 | `bees-can-work-in-rain` | bool | `false` | bool | — |
| 恶魂火球爆炸 | `ghast-fireball-explosion-damage` | bool | `true` | bool | — |
| 幻翼忽略创造 | `phantoms-only-attack-insomniacs` | `true` | bool | — |
| 禁用僵尸攻击村民 | `zombie-aggressive-towards-villager` | `true` | bool | — |
| 僵尸感染概率 | `zombie-villager-infection-chance` | `default` | `default` / 0.0~100.0 | — |
| 铁傀儡弹出概率 | `iron-golem-pop-delay` | `default` | `default` / 正整数（tick） | — |
| 凋灵骷髅远程概率 | `wither-skeleton-rider-chance` | `default` | `default` / 0.0~1.0 | — |
| 每玩家刷怪上限 | `per-player-mob-spawns` | `true` | bool | — |
| 水下生物最大光照 | `water-creature-max-light-level` | `default` | `default` / 0~15 | — |

#### 8.7 世界设置 - 方块（world-settings.default.blocks）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 铁砧损坏概率 | `anvil-damage-chance.level-1` | `0.12` | 0.0~1.0 | — |
| 铁砧损坏概率 | `anvil-damage-chance.level-2` | `0.3` | 0.0~1.0 | — |
| 铁砧损坏概率 | `anvil-damage-chance.level-3` | `0.5` | 0.0~1.0 | — |
| 铁砧损坏概率 | `anvil-damage-chance.level-4` | `0.7` | 0.0~1.0 | — |
| 铁砧损坏概率 | `anvil-damage-chance.level-5` | `0.8` | 0.0~1.0 | — |
| 指向 J 附加随机刻 | `composter-delay` | `default` | `default` / 正整数（tick） | — |
| 刷怪蛋变刷怪笼 | `mob-spawner-spawn-egg-transformation` | `false` | bool | — |
| 龙蛋可放活塞上 | `dragon-egg-can-land-on-piston` | `true` | bool | — |
| 脚手架距离限制 | `scaffolding-distance-limit` | `7` | 正整数（方块） | — |
| 传送门搜索半径 | `portal-search-radius` | `128` | 正整数 | — |
| 传送门创建半径 | `portal-create-radius` | `16` | 正整数 | — |

#### 8.8 世界设置 - 附魔（world-settings.default.enchantments）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 禁用附魔 | `disabled.<enchantment>` | bool | `false` | bool | 对每个附魔单独设置 |
| 修改附魔最大等级 | `limit.<enchantment>` | int | `-1` | int，-1=不限制 | — |

#### 8.9 世界设置 - 物品（world-settings.default.items）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 书本大小限制 | `book-size.page-max` | `2560` | 正整数 | — |
| 书本总大小乘数 | `book-size.total-multiplier` | `0.98` | 0.0~1.0 | — |
| 禁用书页选择器 | `book-page-selector-enabled` | `true` | bool | — |
| 雪球伤害 | `snowball-damage` | `-1.0` | 浮点数，-1=原版 | — |
| 鸡蛋伤害 | `egg-damage` | `-1.0` | 浮点数，-1=原版 | — |
| 经验瓶修复 | `experience-bottle-repair` | `false` | bool | 修复已损物品 |
| 铁砧物品合并成本 | `anvil-item-merge-cost` | `0` | 非负整数 | — |
| 右键盔甲架穿戴 | `armor-stand-right-click-equip` | `true` | bool | — |
| 食物恢复饱和度 | `food.level-float` | `true` | bool | 非整数饥饿值 |
| 标记不可破坏 | `item-durability-attributes-work-on-unbreakable` | `false` | bool | — |

#### 8.10 世界设置 - 工程（world-settings.default.projectiles）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 恶魂火球受重力 | `ghost-fireball-explode-when-shot-with-arrow` | `false` | bool | — |
| 投射物受重力 | `projectile-collide-with-villager-golems` | `true` | bool | — |
| 箭矢传送门 | `arrows-can-portal-teleport` | `false` | bool | — |
| 末影珍珠返回 | `enderpearl-return-to-thrower` | `false` | bool | — |
| 末影珍珠伤害来源 | `enderpearl-damage-source` | `PROJECTILE` | `PROJECTILE` / `FALL` | — |
| 末影珍珠保留载具 | `enderpearl-keep-vehicle` | `false` | bool | — |

#### 8.11 世界设置 - 网络（world-settings.default.network）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 负值属性发送 | `negative-attributes-in-packets` | `false` | bool | — |

---

### 九、leaves.yml（Leaves 核心专属）

> 文件路径：根目录 `leaves.yml`
> 适用核心：**Leaves**（基于 Paper，非 Folia 分支）
> 官方来源：[Leaves GitHub](https://github.com/LeavesMC/Leaves)

#### 9.1 全局设置（global-settings）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 自动更新 | `auto-update` | bool | `false` | bool | — |
| 服务器品牌名 | `server-mod-name` | string | `Leaves` | 字符串 | — |
| 日志玩家 IP | `log-player-ip-addresses` | bool | `true` | bool | — |
| 阻止代理连接 | `prevent-proxy-connections` | bool | `false` | bool | — |

#### 9.2 假玩家系统（fakeplayer）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用假玩家 | `fakeplayer.enabled` | bool | `true` | bool | — |
| 假玩家列表上限 | `fakeplayer.limit` | `-1` | int，-1=无限 | — |
| 假玩家默认游戏模式 | `fakeplayer.default-gamemode` | `SURVIVAL` | 游戏模式枚举 | — |
| 假玩家可被踢出 | `fakeplayer.can-allow-kick` | bool | `true` | bool | — |
| 假玩家忽略睡眠 | `fakeplayer.ignore-sleep` | bool | `false` | bool | — |
| 假玩家显示在列表 | `fakeplayer.show-in-tab-list` | bool | `false` | bool | — |

#### 9.3 旧版机制还原（minecraft-old）

> Leaves 的核心功能之一：将旧版 Minecraft 机制作为可选配置还原。

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 红石粉行为 | `minecraft-old.redstone-wire-dont-connect-if-not-possible` | bool | `false` | bool | 旧版红石连接逻辑 |
| 活塞推出限制 | `minecraft-old.piston-push-limit` | int | `12` | 正整数 | 1.8 以前为 12 |
| 旧版船碰撞 | `minecraft-old.old-boat-collision` | bool | `false` | bool | — |
| 旧版物品拾取 | `minecraft-old.old-item-merge-check` | bool | `false` | bool | — |
| 旧版 TNT 行为 | `minecraft-old.old-tnt-entity-height-nerf` | bool | `false` | bool | — |
| 旧版末影人 | `minecraft-old.enderman-keep-blocks-on-death` | bool | `false` | bool | — |
| 旧版末影珍珠 | `minecraft-old.old-ender-pearl-behavior` | bool | `false` | bool | — |
| 箭矢不穿实体 | `minecraft-old.arrows-cant-pierce-entities` | bool | `false` | bool | — |
| 铁砧旧版损坏 | `minecraft-old.old-anvil-damage` | bool | `false` | bool | — |
| 旧版 sheep 吃草 | `minecraft-old.old-sheep-eat-grass` | bool | `false` | bool | — |
| 村民旧版交易 | `minecraft-old.old-villager-hero-of-the-village` | bool | `false` | bool | — |
| 旧版僵尸门破 | `minecraft-old.old-zombie-door-breaking` | bool | `false` | bool | — |
| 旧版刷怪蛋 | `minecraft-old.old-spawner-egg-transformation` | bool | `false` | bool | — |
| 旧版猫惊吓 | `minecraft-old.old-cat-chest-detection` | bool | `false` | bool | — |
| 旧版村民补货 | `minecraft-old.old-villager-replenish-trade` | bool | `false` | bool | — |
| 旧版床重生 | `minecraft-old.old-bed-respawn` | bool | `false` | bool | — |
| 旧版刷怪笼 AI | `minecraft-old.old-nerfed-spawner-mobs` | bool | `false` | bool | — |
| 旧版末影龙 | `minecraft-old.old-dragon-fight` | bool | `false` | bool | — |
| 旧版村民工作 | `minecraft-old.old-villager-work-station-detection` | bool | `false` | bool | — |
| 旧版地图扫描 | `minecraft-old.old-map-scanning` | bool | `false` | bool | — |
| 旧版实体验证 | `minecraft-old.old-entity-validation` | bool | `false` | bool | — |
| 旧版竹子生长 | `minecraft-old.old-bamboo-growth` | bool | `false` | bool | — |
| 旧版洞穴藤蔓 | `minecraft-old.old-cave-vines-growth` | bool | `false` | bool | — |
| 旧级跳板机制 | `minecraft-old.cauldron-cause-stalactite-drip` | bool | `false` | bool | — |
| 旧版末影龙蛋 | `minecraft-old.old-ender-dragon-egg` | bool | `false` | bool | — |
| 旧版村民繁殖 | `minecraft-old.old-villager-breeding` | bool | `false` | bool | — |
| 旧版床交互 | `minecraft-old.old-bed-interaction` | bool | `false` | bool | — |
| 旧版重生锚 | `minecraft-old.old-respawn-anchor-interaction` | bool | `false` | bool | — |
| 旧版药水持续时间 | `minecraft-old.old-potion-duration` | bool | `false` | bool | — |
| 旧版食物消耗 | `minecraft-old.old-food-eating` | bool | `false` | bool | — |
| 旧版重生无敌 | `minecraft-old.old-respawn-invulnerability` | bool | `false` | bool | — |
| 旧版经验修复 | `minecraft-old.old-experience-merge` | bool | `false` | bool | — |
| 旧版僵尸猪灵 | `minecraft-old.old-zombified-piglin-group-spawn` | bool | `false` | bool | — |
| 旧版信标范围 | `minecraft-old.old-beacon-range` | bool | `false` | bool | — |
| 旧版根须 | `minecraft-old.old-nether-portal-fix` | bool | `false` | bool | — |
| 旧版方块随机刻 | `minecraft-old.old-block-random-tick-order` | bool | `false` | bool | — |
| 旧版凋灵生成 | `minecraft-old.old-wither-target-selection` | bool | `false` | bool | — |

#### 9.4 性能优化（performance）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用 CA 红石 | `performance.alternate-current-redstone` | bool | `false` | bool | 同 Paper 的 ALTERNATE_CURRENT |
| 强制降延迟发包 | `performance.force-transaction-packet-delay` | bool | `false` | bool | — |
| 启用无延迟区块发送 | `performance.zero-tick-chunk-sends` | bool | `false` | bool | — |
| 优化爆炸计算 | `performance.explosion-optimization` | bool | `false` | bool | — |
| NPC 路径寻找优化 | `performance.npc-pathfinding-optimize` | bool | `false` | bool | — |
| 活性实体分桶 | `performance.entity-lookup-bucket` | bool | `false` | bool | — |
| 禁用方法配置器 | `performance.disable-method-profiler` | bool | `true` | bool | — |
| 每区块实体上限 | `performance.entity-per-chunk-save-limit` | int | `-1` | int，-1=无限 | — |

#### 9.5 协议支持（protocol）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用 Syncmatica | `protocol.syncmatica.enabled` | bool | `false` | bool | 与客户端 mod 联动 |
| 启用 PCA | `protocol.pca.enabled` | bool | `false` | bool | PlayerAnimationAPI |
| 启用 AppleSkin | `protocol.appleskin.enabled` | bool | `true` | bool | 饥饿条同步 |
| 启用 Servux | `protocol.servux.enabled` | bool | `false` | bool | — |
| 启用 Xaero 小地图 | `protocol.xaero-map.enabled` | bool | `false` | bool | — |
| 启用 Xaero 世界地图 | `protocol.xaero-world-map.enabled` | bool | `false` | bool | — |
| 启用 BladeRen | `protocol.bladeren.enabled` | bool | `false` | bool | 自定义服务端包 |
| 启用 JITPACK | `protocol.jepb.enabled` | bool | `false` | bool | — |

#### 9.6 修复与杂项（fixes & misc）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 修复未加载区块末影珍珠 | `fixes.unloaded-chunk-ender-pearl` | bool | `true` | bool | — |
| 修复玩家骑乘碰撞 | `fixes.vehicle-collision` | bool | `false` | bool | — |
| 修复物品穿墙合并 | `fixes.item-merge-through-walls` | bool | `true` | bool | — |
| 修复漏斗断线 | `fixes.hopper-ghost-items` | bool | `true` | bool | — |
| 修复重复包踢出 | `fixes.duplicate-map-uuid` | bool | `true` | bool | — |
| 修复刷怪笼随机刻 | `fixes.spawner-random-delay` | bool | `true` | bool | — |
| 修复远末地地形 | `fixes.far-end-terrain-generation` | bool | `true` | bool | — |

#### 9.7 区域文件格式（region-format）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用 Linear 区域格式 | `region-format.linear.enabled` | bool | `false` | bool | 实验性，替代 MCA |
| Linear 压缩级别 | `region-format.linear.compression-level` | `1` | -1~9 | — |
| Linear 线程数 | `region-format.linear.thread-num` | `2` | 正整数 | — |

---

### 十、canvas-server.yml + canvas-worlds.yml（CanvasMC 核心专属）

> 文件路径：根目录 `canvas-server.yml`、`config/canvas-worlds.yml`
> 适用核心：**CanvasMC**（Folia 分支，由 StellarRPC 维护）
> 官方来源：[CanvasMC GitHub](https://github.com/StellarRPC/CanvasMC)

#### 10.1 canvas-server.yml - 调度器（scheduler）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| **调度算法** | `scheduler.type` | enum | `EDF` | `EDF` / `WORK_STEALING` / **`AFFINITY`** | Canvas 独有 AFFINITY 模式：每个区域绑定固定 CPU 核心，减少上下文切换 |
| 工作窃取阈值 | `scheduler.work-stealing.threshold-ms` | float | `5.0` | 正浮点数（毫秒） | 区域空闲超过此时间后开始窃取任务 |
| Mid-tick 任务启用 | `scheduler.mid-tick-tasks.enabled` | bool | `true` | bool | — |
| Mid-tick 任务超时 | `scheduler.mid-tick-tasks.timeout-ms` | int | `5` | 正整数（毫秒） | — |
| CPU 亲和性核心列表 | `scheduler.affinity.cores` | string | `（空）` | 逗号分隔核心 ID | 如 `0,1,2,3`，留空=自动 |

#### 10.2 canvas-server.yml - 网络（networking）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 全局发送速率限制 | `networking.global-send-rate-limit` | int | `0` | 正整数，0=禁用 | 每 tick 每玩家最大发送包数 |
| 压缩阈值覆盖 | `networking.compression-threshold` | int | `-1` | int，-1=使用 server.properties | — |
| 禁用批量区块发送 | `networking.disable-bulk-chunk-sending` | bool | `false` | bool | — |

#### 10.3 canvas-server.yml - 航点（waypoints）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用航点系统 | `waypoints.enabled` | bool | `false` | bool | 允许客户端 mod 获取服务器坐标点 |
| 最大航点数 | `waypoints.max-per-player` | int | `64` | 正整数 | — |

#### 10.4 canvas-server.yml - 粒子（particles）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 粒子发送距离倍率 | `particles.send-distance-multiplier` | float | `1.0` | 正浮点数 | — |
| 禁用特定粒子 | `particles.disabled` | `[]` | 字符串列表 | — |

#### 10.5 canvas-worlds.yml - 世界专属设置

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 每区域最大区块数 | `region.max-chunks` | int | `-1` | int，-1=无限制 | — |
| 区域自动扩展 | `region.auto-expand` | bool | `false` | bool | — |
| 实体 tick 阈值 | `entity.tick-threshold-ms` | int | `50` | 正整数（毫秒） | 超过此时间发出警告 |

---

### 十一、deer-folia.yml（DeerFolia 核心专属）

> 文件路径：根目录 `deer-folia.yml`
> 适用核心：**DeerFolia**（CanvasMC 的进一步优化分支）
> 官方来源：[DeerFolia GitHub](https://github.com/Enderman-Deer/DeerFolia)

#### 11.1 DAB 动态激活大脑

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| **启用 DAB** | `dab.enabled` | bool | `true` | bool | 动态激活大脑（Dynamic Activation Brain），仅当实体附近有玩家时才激活 AI |
| DAB 激活距离 | `dab.activation-distance` | int | `32` | 正整数（方块） | — |
| DAB 激活距离-飞行 | `dab.activation-distance.flying-monsters` | int | `32` | 正整数 | — |
| DAB 激活距离-掠夺者 | `dab.activation-distance.raiders` | int | `64` | 正整数 | — |
| DAB 激活距离-村民 | `dab.activation-distance.villagers` | int | `32` | 正整数 | — |
| DAB 激活距离-水生 | `dab.activation-distance.water` | int | `16` | 正整数 | — |
| DAB 脱离重新激活 | `dab.start-frozen` | bool | `true` | bool | 新生成的实体初始为冻结状态 |
| DAB 忽略旁观者 | `dab.ignore-spectators` | bool | `false` | bool | — |

#### 11.2 异步寻路

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| **启用异步寻路** | `async-pathfinding.enabled` | bool | `true` | bool | 将寻路计算移出主线程，大幅减少 TPS 波动 |
| 异步寻路线程数 | `async-pathfinding.threads` | int | `-1` | int，-1=自动 | — |
| 异步寻路最大队列 | `async-pathfinding.max-queue-size` | int | `1024` | 正整数 | — |

#### 11.3 网络优化

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| AFK 网络优化 | `network.afk-optimization.enabled` | bool | `true` | bool | AFK 玩家降低发包频率 |
| AFK 检测时间 | `network.afk-optimization.idle-time-seconds` | int | `60` | 正整数（秒） | — |
| AFK 发包间隔倍率 | `network.afk-optimization.packet-interval-multiplier` | float | `4.0` | 正浮点数 | — |
| 实体元数据批量发送 | `network.entity-metadata-batching` | bool | `true` | bool | — |
| 延迟区块卸载 | `network.delayed-chunk-unload-seconds` | int | `10` | 正整数（秒） | — |

#### 11.4 POI 优化

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| POI 记录间隔 | `poi.record-interval` | int | `20` | 正整数（tick） | — |
| POI 最大缓存 | `poi.max-cache-size` | int | `4096` | 正整数 | — |

#### 11.5 kaiiju-entity-throttling.yml（实体节流）

> 文件路径：根目录 `kaiiju-entity-throttling.yml`
> DeerFolia 内置的实体数量限制系统

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用实体节流 | `enabled` | bool | `true` | bool | — |
| 每区块最大实体数 | `max-entities-per-chunk` | int | `-1` | int，-1=无限 | — |
| 每区块最大掉落物 | `max-items-per-chunk` | int | `-1` | int，-1=无限 | — |
| 每区块最大经验球 | `max-xp-orbs-per-chunk` | int | `-1` | int，-1=无限 | — |
| 超限处理方式 | `overflow-action` | enum | `REMOVE_OLDEST` | `REMOVE_OLDEST` / `PREVENT_SPAWN` / `LOG_ONLY` | — |
| 检测间隔 | `check-interval-ticks` | int | `20` | 正整数（tick） | — |

---

## 第三部分：国产混合核心专属配置

> 以下核心同时支持 Bukkit 插件和 Forge/NeoForge 模组。

---

### 十二、mohist.yml（Mohist 核心专属）

> 文件路径：`mohist-config/mohist.yml`
> 适用核心：**Mohist**（Forge + Bukkit 混合，国产）
> 官方来源：[Mohist GitHub](https://github.com/MohistMC/Mohist)

#### 12.1 全局设置

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 调试模式 | `debug` | bool | `false` | bool | — |
| 检查更新 | `check-update` | bool | `true` | bool | — |
| 服务器品牌名 | `server-brand-name` | string | `Mohist` | 字符串 | — |
| 语言 | `language` | string | `zh_CN` | 语言代码 | 支持 zh_CN / en_US 等 |

#### 12.2 修复与兼容

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 修复铁砧崩溃 | `fix-anvil-crash` | bool | `true` | bool | — |
| 修复潜影盒崩溃 | `fix-shulker-box-crash` | bool | `true` | bool | — |
| 修复创造模式掉落 | `fix-creative-mode-drop` | bool | `true` | bool | — |
| 修复末地传送门 | `fix-end-portal-teleport` | bool | `true` | bool | — |
| 修复实体骑乘 | `fix-entity-mount-crash` | bool | `true` | bool | — |

#### 12.3 模组管理

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 禁用模组列表 | `mods-blacklist` | list | `[]` | 字符串列表 | 模组 ID 列表，启动时跳过 |
| 模组依赖检查 | `check-mod-dependencies` | bool | `true` | bool | — |
| 显示模组信息 | `show-mod-info-on-start` | bool | `true` | bool | — |

#### 12.4 玩家管理

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用 TPA | `tpa.enable` | bool | `true` | bool | — |
| TPA 冷却 | `tpa.cooldown` | int | `30` | 正整数（秒） | — |
| TPA 超时 | `tpa.timeout` | int | `60` | 正整数（秒） | — |
| 启用 /back | `back.enable` | bool | `true` | bool | — |
| 死亡保留物品 | `keep-inventory-on-death` | bool | `false` | bool | — |
| 死亡保留经验 | `keep-experience-on-death` | bool | `false` | bool | — |

#### 12.5 实体清理

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用自动清理 | `entity-cleaner.enable` | bool | `false` | bool | — |
| 清理间隔 | `entity-cleaner.interval` | int | `600` | 正整数（秒） | — |
| 清理掉落物 | `entity-cleaner.clean-items` | bool | `true` | bool | — |
| 清理箭矢 | `entity-cleaner.clean-arrows` | bool | `true` | bool | — |
| 清理经验球 | `entity-cleaner.clean-xp-orbs` | bool | `true` | bool | — |
| 清理 TNT | `entity-cleaner.clean-tnt` | bool | `false` | bool | — |

#### 12.6 封禁系统

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 封禁 IP 列表 | `banned-ips` | list | `[]` | 字符串列表 | — |
| 封禁玩家列表 | `banned-players` | list | `[]` | 字符串列表 | — |
| 封禁消息 | `ban-message` | string | `You have been banned from this server.` | 字符串 | — |

#### 12.7 代理与网络

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| Velocity 启用 | `velocity.enabled` | bool | `false` | bool | — |
| Velocity 密钥 | `velocity.secret` | string | `（空）` | 字符串 | — |
| BungeeCord 模式 | `bungeecord` | bool | `false` | bool | — |

---

### 十三、catserver.yml（CatServer 核心专属）

> 文件路径：根目录 `catserver.yml`
> 适用核心：**CatServer**（Forge + Bukkit 混合，国产）
> 官方来源：[CatServer 官网](https://catserver.moe)

#### 13.1 全局设置

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 调试模式 | `debug` | bool | `false` | bool | — |
| 语言 | `language` | string | `zh_CN` | 语言代码 | — |
| 服务器品牌名 | `server-name` | string | `CatServer` | 字符串 | — |
| 检查更新 | `auto-update` | bool | `false` | bool | — |
| 日志级别 | `log-level` | string | `INFO` | `INFO` / `WARN` / `ERROR` / `DEBUG` | — |

#### 13.2 假玩家（fakeplayer）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 启用假玩家 | `fakeplayer.enable` | bool | `true` | bool | — |
| 假玩家权限 | `fakeplayer.permissions` | list | `[]` | 权限节点列表 | — |
| 假玩家游戏模式 | `fakeplayer.gamemode` | string | `SURVIVAL` | 游戏模式 | — |

#### 13.3 插件兼容性补丁（patcher）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| Dynmap 兼容 | `patcher.dynmap` | bool | `true` | bool | 修复 Dynmap 与模组的冲突 |
| WorldEdit 兼容 | `patcher.worldedit` | bool | `true` | bool | — |
| Essentials 兼容 | `patcher.essentials` | bool | `true` | bool | — |
| MythicMobs 兼容 | `patcher.mythicmobs` | bool | `true` | bool | — |
| PlaceholderAPI 兼容 | `patcher.placeholderapi` | bool | `true` | bool | — |
| Vault 兼容 | `patcher.vault` | bool | `true` | bool | — |
| 禁用的插件 | `disabled-plugins` | list | `[]` | 字符串列表 | 启动时跳过 |

#### 13.4 性能优化

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 异步生物生成 | `async-mob-spawning` | bool | `true` | bool | — |
| 优化实体碰撞 | `optimize-entity-collision` | bool | `false` | bool | — |
| 优化容器更新 | `optimize-container-update` | bool | `true` | bool | — |
| 减少 NBT 复制 | `reduce-nbt-copy` | bool | `false` | bool | — |
| 漏斗异步传输 | `async-hopper-transfer` | bool | `false` | bool | — |

#### 13.5 生物与生成

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 禁用自然生成 | `disable-natural-spawning` | bool | `false` | bool | — |
| 每玩家独立生成 | `per-player-mob-spawns` | bool | `true` | bool | — |
| 生物生成上限 | `spawn-limits.monsters` | int | `70` | 正整数 | — |
| 生物生成上限 | `spawn-limits.animals` | int | `10` | 正整数 | — |
| 生物生成上限 | `spawn-limits.water-animals` | int | `5` | 正整数 | — |
| 清理掉落物间隔 | `item-despawn-rate` | int | `6000` | 正整数（tick） | — |

---

### 十四、arclight.conf（Arclight 核心专属）

> 文件路径：`config/arclight.conf`
> 适用核心：**Arclight**（Forge + Bukkit 混合）
> 配置格式：**HOCON**（花括号嵌套，非 YAML）
> 官方来源：[Arclight GitHub](https://github.com/IzzelAliz/Arclight)

#### 14.1 优化（optimization）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 启用异步生物生成 | `optimization.async-mob-spawning` | bool | `true` | bool | — |
| 启用异步区块加载 | `optimization.async-chunk-loading` | bool | `false` | bool | — |
| 优化实体追踪 | `optimization.entity-tracking-optimization` | bool | `true` | bool | — |
| 优化方块状态 | `optimization.block-state-optimization` | bool | `true` | bool | — |

#### 14.2 兼容性（compatibility）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 材质映射覆盖 | `compatibility.material-overrides` | list | `[]` | 字符串列表 | Forge 材质 → Bukkit 材质映射 |
| 实体类型覆盖 | `compatibility.entity-type-overrides` | list | `[]` | 字符串列表 | — |
| 符号链接世界目录 | `compatibility.symlink-worlds` | bool | `false` | bool | 允许世界目录为符号链接 |
| 转发权限到模组 | `compatibility.forward-permissions-to-mods` | bool | `true` | bool | 将 Bukkit 权限转发给 Forge 模组 |
| 修复 NBT 标签冲突 | `compatibility.fix-nbt-tag-conflicts` | bool | `true` | bool | — |
| 修复物品 Lore 冲突 | `compatibility.fix-item-lore-conflicts` | bool | `true` | bool | — |

#### 14.3 异步捕获器（async-catcher）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 启用异步检测 | `async-catcher.enabled` | bool | `true` | bool | 检测从非主线程调用 Bukkit API |
| 检测时抛异常 | `async-catcher.throw-exception` | bool | `true` | bool | — |
| 日志级别 | `async-catcher.log-level` | string | `WARN` | `WARN` / `ERROR` / `SEVERE` | — |
| 忽略的插件 | `async-catcher.ignored-plugins` | list | `[]` | 字符串列表 | — |

---

## 第四部分：模组加载器核心配置

> Forge / NeoForge / Sponge 使用模组独立配置，通常无全局配置文件。
> Fabric 完全无额外配置。

---

### 十五、Sponge - global.conf（Sponge 核心专属）

> 文件路径：`config/sponge/global.conf`
> 适用核心：**Sponge**（基于 Forge/NeoForge，独立 API）
> 配置格式：**HOCON**（`.conf` 文件，花括号嵌套，支持引用和合并）
> 官方来源：[Sponge 官方文档](https://docs.spongepowered.org/stable/en/server/getting-started/configuration/)

#### 15.1 方块与方块实体激活（block-entity-activation）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 每区块最大方块实体 tick | `block-entity-activation.max-tick-time` | int | `1` | 正整数（tick/区块） | 控制熔炉、漏斗等方块实体的计算频率 |
| 方块实体激活范围 | `block-entity-activation.tick-range` | int | `2` | 正整数（区块） | 在此范围内玩家触发方块实体 tick |
| 启用方块实体激活范围 | `block-entity-activation.use-block-entity-activation-range` | bool | `true` | bool | — |

#### 15.2 实体激活（entity-activation）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 怪物激活范围 | `entity-activation-range.monsters` | int | `32` | 正整数（方块） | — |
| 动物激活范围 | `entity-activation-range.animals` | int | `32` | 正整数 | — |
| 水生激活范围 | `entity-activation-range.water` | int | `16` | 正整数 | — |
| 杂项激活范围 | `entity-activation-range.misc` | int | `16` | 正整数 | — |
| 飞行怪物激活范围 | `entity-activation-range.flying-monsters` | int | `32` | 正整数 | — |
| 唤醒不活跃实体间隔 | `entity-activation-range.wake-up-inactive.monsters-every` | int | `400` | 正整数（tick） | — |
| 唤醒不活跃实体持续 | `entity-activation-range.wake-up-inactive.monsters-for` | int | `100` | 正整数（tick） | — |
| 唤醒不活跃动物间隔 | `entity-activation-range.wake-up-inactive.animals-every` | int | `1200` | 正整数（tick） | — |
| 唤醒不活跃动物持续 | `entity-activation-range.wake-up-inactive.animals-for` | int | `100` | 正整数（tick） | — |

#### 15.3 实体碰撞（entity-collision）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 启用玩家碰撞 | `entity-collision.enable-player-collisions` | bool | `true` | bool | — |
| 最大实体碰撞数 | `entity-collision.max-entity-collisions` | int | `8` | 正整数 | 0=完全禁用推挤 |

#### 15.4 移动检查（movement-checks）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 启用移动检查 | `movement-checks.enable-player-collision-checks` | bool | `true` | bool | — |
| 过快移动倍数 | `movement-checks.moved-too-quickly-multiplier` | float | `10.0` | 正浮点数 | — |
| 错误移动阈值 | `movement-checks.moved-wrongly-threshold` | float | `0.0625` | 正浮点数 | — |
| 禁用载具飞行检查 | `movement-checks.disable-vehicle-flight-check` | bool | `false` | bool | — |

#### 15.5 刷怪笼（spawner）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 刷怪笼最大附近实体 | `spawner.max-nearby-entities` | int | `-1` | int，-1=原版 | — |
| 刷怪笼生成间隔 | `spawner.spawn-tick-rate` | int | `1` | 正整数（tick） | — |

#### 15.6 世界设置（world）

| 中文含义 | HOCON 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|-----------|------|--------|----------|------|
| 禁用雷暴 | `world.weather.thunder` | bool | `true` | bool | — |
| 禁用冰和雪 | `world.weather.ice-and-snow` | bool | `true` | bool | — |
| 禁用方块 tick | `world.ticking.disable-block-ticking` | bool | `false` | bool | — |
| 禁用实体 tick | `world.ticking.disable-entity-ticking` | bool | `false` | bool | — |
| 禁用随机 tick | `world.ticking.disable-random-ticking` | bool | `false` | bool | — |
| 禁用方块实体 tick | `world.ticking.disable-block-entity-ticking` | bool | `false` | bool | — |
| 区块 GC 周期 | `world.chunk-gc.tick-interval` | int | `600` | 正整数（tick） | — |
| 区块 GC 负载阈值 | `world.chunk-gc.load-threshold` | int | `300` | 正整数 | — |

---

### 十六、Forge / NeoForge（模组核心）

> 适用核心：**Forge** / **NeoForge**
> 这两个核心**没有全局配置文件**。所有配置由各模组自行管理。

#### 配置文件结构

| 路径 | 说明 |
|------|------|
| `config/` | 所有模组配置存放目录 |
| `config/forge-common.toml`（Forge）或 `config/neoforge-common.toml`（NeoForge） | 少量核心设置 |
| `config/<模组ID>.toml` | 每个模组的独立配置 |
| `config/<模组ID>-client.toml` | 客户端配置（服务端可忽略） |

#### forge-common.toml 核心配置

| 中文含义 | TOML 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 移除错误方块 | `removeErroringBlocks` | bool | `false` | bool | 加载出错时移除方块而非崩溃 |
| 移除错误实体 | `removeErroringEntities` | bool | `false` | bool | — |
| 全部日志输出到聊天 | `fullBoundingBoxLadders` | bool | `false` | bool | 梯子碰撞箱修复 |
| 云高度 | `cloudHeight` | int | `128` | 正整数 | — |
| 最大加载距离 | `maxTicketLength` | int | `11` | 正整数 | 区块加载 ticket 生命周期 |
| 修复可见度检查 | `forge.lightningEnabled` | bool | `true` | bool | 闪电效果 |
| 控制台日志级别 | `logLevel` | string | `INFO` | 日志级别 | — |

#### 模组配置通用说明

每个模组的 `.toml` 文件结构通常如下：

```toml
[category]
    # 中文说明
    key = value  # 类型: 默认值  范围: min~max

[category.subcategory]
    another-key = value
```

> **注意**：模组配置无统一标准，具体键名和结构取决于模组作者。查看各模组的 `config/<模组ID>.toml` 文件内的注释。

---

### 十七、Fabric（模组核心）

> 适用核心：**Fabric**
> Fabric **没有任何额外的全局配置文件**。它只使用原版 `server.properties`。
> 每个模组可以有自己的配置文件，通常存放在 `config/` 目录下（JSON 或 TOML 格式）。

#### Fabric 配置说明

| 项目 | 说明 |
|------|------|
| 全局配置 | **无**——仅使用 `server.properties` |
| 模组配置 | 各模组自行管理，通常为 `config/<模组ID>.json` 或 `config/<模群ID>.toml` |
| Fabric Loader 配置 | `.fabric/` 目录下（通常无需手动修改） |

---

## 第五部分：基岩版服务端核心配置

---

### 十八、Nukkit（基岩版 - 简单配置）

> 适用核心：**Nukkit** / **NukkitX**（基于 Java 的基岩版服务端）
> 官方来源：[Nukkit GitHub](https://github.com/NukkitX/Nukkit)
> **Nukkit 仅有一个配置文件 `server.properties`**，格式与 Java 版类似但部分键名不同。

#### 18.1 Nukkit server.properties

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 服务器端口 | `server-port` | int | `19132` | 1~65535 | 基岩版默认端口 |
| 服务器 IP | `server-ip` | string | `0.0.0.0` | — | — |
| MOTD | `motd` | string | `Minecraft Server` | 字符串 | — |
| 子游戏版本 | `sub-motd` | string | `Powered by Nukkit` | 字符串 | 在 MOTD 下方显示 |
| 最大玩家数 | `max-players` | int | `20` | 正整数 | — |
| 视距 | `view-distance` | int | `8` | 正整数 | — |
| 世界名称 | `level-name` | string | `world` | 字符串 | — |
| 世界种子 | `level-seed` | string | `（空）` | 字符串 | — |
| 世界生成器 | `level-type` | string | `DEFAULT` | `DEFAULT` / `FLAT` / `NETHER` / `ENDS` / `VOID` / — | — |
| 生成结构 | `generate-structures` | string | `true` | `ON` / `on` / `true` / `FALSE` / `off` / `false` | **注意：使用 ON/OFF 字符串而非布尔值** |
| 游戏难度 | `difficulty` | int | `1` | `0`（和平）/ `1`（简单）/ `2`（普通）/ `3`（困难） | **注意：使用数字而非字符串** |
| 游戏模式 | `gamemode` | int | `0` | `0`（生存）/ `1`（创造）/ `2`（冒险）/ `3`（旁观） | **注意：使用数字** |
| 强制游戏模式 | `force-resources` | string | `false` | `ON` / `off` | — |
| 正版验证 | `xbox-auth` | string | `true` | `ON` / `off` | Xbox Live 验证 |
| 允许飞行 | `allow-flight` | string | `false` | `ON` / `off` | — |
| 白名单 | `white-list` | string | `false` | `ON` / `off` | — |
| 启用查询 | `enable-query` | string | `true` | `ON` / `off` | — |
| Query 端口 | `query-port` | int | `19132` | 1~65535 | — |
| 启用 RCON | `enable-rcon` | string | `false` | `ON` / `off` | — |
| RCON 端口 | `rcon.port` | int | `25575` | 1~65535 | — |
| RCON 密码 | `rcon.password` | string | `（空）` | 字符串 | — |
| 自动保存 | `auto-save` | string | `true` | `ON` / `off` | — |
| 自动保存间隔 | `auto-save-interval` | int | `60` | 正整数（秒） | — |
| 禁用动物生成 | `spawn-animals` | string | `true` | `ON` / `off` | — |
| 禁用怪物生成 | `spawn-mobs` | string | `true` | `ON` / `off` | — |
| 禁用 PvP | `pvp` | string | `true` | `ON` / `off` | — |
| 基岩版网络协议 | `network-protocol-version` | int | `最新` | 整数 | — |
| 启用日志 | `enable-log` | string | `true` | `ON` / `off` | — |
| 调试命令 | `debug` | string | `1` | `1` / `0` | — |
| 玩家空闲超时 | `player-idle-timeout` | int | `0` | 正整数（分钟），0=永不 | — |
| 语言 | `language` | string | `eng` | 语言代码 | — |
| 记录 IP | `log-ips` | string | `true` | `ON` / `off` | — |
| 基岩版编码阈值 | `upnp-forwarding` | string | `false` | `ON` / `off` | UPnP 自动端口转发 |

---

### 十九、pocketmine.yml（PocketMine-MP 核心专属）

> 文件路径：根目录 `pocketmine.yml` + `server.properties`
> 适用核心：**PocketMine-MP**（基于 PHP 的基岩版服务端）
> 官方来源：[PocketMine-MP 官网](https://pmmp.io)
> 注意：`server.properties` 仅包含少量基础设置，大量配置在 `pocketmine.yml` 中。

#### 19.1 pocketmine.yml - 内存（memory）

| 中文含义 | YAML 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 主线程内存限制 | `memory.main-limit` | int | `1024` | 正整数（MB） | — |
| 全局内存限制 | `memory.global-limit` | int | `0` | 正整数（MB），0=无限制 | — |
| 内存步长 | `memory.memory-step` | int | `16` | 正整数（MB） | 内存不足时每次扩展量 |
| 连续垃圾回收阈值 | `memory.continuous-trigger` | bool | `true` | bool | 持续触发 GC |
| 垃圾回收周期 | `memory.garbage-collection.period` | int | `36000` | 正整数（tick） | — |
| 垃圾收集阈值 | `memory.garbage-collection.threshold` | int | `16384` | 正整数 | — |
| 缓存阈值 | `memory.cache-chunk-threshold` | int | `256` | 正整数 | — |

#### 19.2 pocketmine.yml - 网络（network）

| 中文含义 | YAML 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 批量发送 | `network.batch-threshold` | int | `256` | 正整数（字节） | — |
| 压缩级别 | `network.compression-level` | int | `7` | 1~9 | — |
| 异步压缩 | `network.async-compression` | string | `true` | `ON` / `off` | — |
| 上行带宽 | `network.upstream` | int | `0` | 正整数（字节/秒），0=无限 | — |
| 连接超时 | `network.connection-timeout` | int | `5` | 正整数（秒） | — |

#### 19.3 pocketmine.yml - 区块（chunks）

| 中文含义 | YAML 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 区块生成队列大小 | `chunk-generation.queue-size` | int | `8` | 正整数 | — |
| 区块生成 populace | `chunk-generation.population-queue-size` | int | `8` | 正整数 | — |
| 每玩家每 tick 区块发送 | `chunk-sending.per-tick` | int | `4` | 正整数 | — |
| 区块发送最大缓存 | `chunk-sending.max-chunks` | int | `192` | 正整数 | — |
| 区块发送激活范围 | `chunk-sending.spawn-threshold` | int | `56` | 正整数 | — |
| 区块发送缓存大小 | `chunk-sending.cache-chunks` | int | `512` | 正整数 | — |
| 区块 Tick 数量 | `chunk-ticking.per-tick` | int | `40` | 正整数 | 每 tick 处理的区块数 |
| 区块 Tick 半径 | `chunk-ticking.tick-radius` | int | `3` | 正整数（区块） | — |
| 区块 GC 周期 | `chunk-ticking.clear-tick-list` | bool | `true` | bool | — |
| 实体 Tick 半径 | `chunk-ticking.entity-tick-radius` | int | `2` | 正整数（区块） | — |

#### 19.4 pocketmine.yml - 调试（debug）

| 中文含义 | YAML 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 调试级别 | `debug.level` | int | `1` | 0~2 | 0=无，1=错误，2=全部 |
| 命令权限 | `debug.commands` | bool | `false` | bool | — |

#### 19.5 pocketmine.yml - 玩家设置（player）

| 中文含义 | YAML 键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 保存玩家数据 | `player.save-player-data` | bool | `true` | bool | — |
| 反作弊 | `player.anti-cheat.allow-movement-cheats` | bool | `false` | bool | — |
| 预缓存皮肤 | `player.pre-cache-skin` | bool | `true` | bool | — |

#### 19.6 PocketMine-MP server.properties（精简版）

| 中文含义 | 英文键名 | 类型 | 默认值 | 取值范围 | 说明 |
|----------|----------|------|--------|----------|------|
| 服务器端口 | `server-port` | int | `19132` | 1~65535 | — |
| 服务器 IP | `server-ip` | string | `0.0.0.0` | — | — |
| MOTD | `motd` | string | `Minecraft: PE Server` | 字符串 | — |
| 子 MOTD | `sub-motd` | string | `Powered by PocketMine-MP` | 字符串 | — |
| 最大玩家数 | `max-players` | int | `20` | 正整数 | — |
| 视距 | `view-distance` | int | `8` | 正整数 | — |
| 白名单 | `white-list` | bool | `false` | bool | — |
| 正版验证 | `xbox-auth` | bool | `true` | bool | Xbox Live 验证 |
| 语言 | `language` | string | `eng` | 语言代码 | — |
| 启用查询 | `enable-query` | bool | `true` | bool | — |
| Query 端口 | `query-port` | int | `19132` | 1~65535 | — |
| 启用 RCON | `enable-rcon` | bool | `false` | bool | — |
| RCON 端口 | `rcon.port` | int | `25575` | 1~65535 | — |
| RCON 密码 | `rcon.password` | string | `（空）` | 字符串 | — |
| 自动保存 | `auto-save` | bool | `true` | bool | — |
| 禁用动物 | `spawn-animals` | bool | `true` | bool | — |
| 禁用怪物 | `spawn-mobs` | bool | `true` | bool | — |
| PvP | `pvp` | bool | `true` | bool | — |
| 调试 | `debug` | bool | `false` | bool | — |

---

## 第六部分：快速索引

> 按你使用的核心，快速找到需要阅读的章节。

| 核心名称 | 类型 | 需要阅读的章节 |
|----------|------|----------------|
| **Folia** | 多线程 Paper 分支 | 一、二（含 Folia 专属 2.1）、三、四、五、六、七 |
| **Paper** | 标准 Paper | 一、二、三、四、五、七 |
| **Purpur** | Paper + 扩展 | 一、二、三、四、五、八 |
| **Leaves** | Paper + 旧版还原 | 一、二、三、四、五、九 |
| **CanvasMC** | Folia 分支 | 一、二、三、四、五、十 |
| **DeerFolia** | CanvasMC 优化分支 | 一、二、三、四、五、十、十一 |
| **Mohist** | Forge + Bukkit（国产） | 一、四、五、十二 |
| **CatServer** | Forge + Bukkit（国产） | 一、四、五、十三 |
| **Arclight** | Forge + Bukkit | 一、四、五、十四 |
| **Sponge** | 独立 API（Forge 上） | 一、十五 |
| **Forge / NeoForge** | 模组加载器 | 一、十六 |
| **Fabric** | 轻量模组加载器 | 一、十七 |
| **Nukkit** | 基岩版（Java） | 十八 |
| **PocketMine-MP** | 基岩版（PHP） | 十九 |

---

> **文档版本**：v2.0 | 最后更新：2026-07-15 | 覆盖核心数量：**14 个**