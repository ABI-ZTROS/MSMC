# Nukkit 服务器配置文件中文手册

> Nukkit 是一款由 Java 编写的 Minecraft: Bedrock Edition（基岩版）开源服务端软件，主打高性能与可扩展性。
> 协议：基岩版 RakNet（UDP），与 Java 版服务端**完全不通用**
> 官方 GitHub：https://github.com/CloudburstMC/Nukkit
> 数据来源：CloudburstMC/Nukkit 源码（`src/main/resources/lang/eng/nukkit.yml` 模板）+ 基岩版 BDS 官方文档
> 适用版本基准：Nukkit 1.0（master 分支，commit `dbbb7ca`）

## ⚠️ 重要：基岩版与 Java 版的差异

Nukkit 是**基岩版**服务端，玩家通过手机、Windows 10/11 版、主机版 Minecraft 加入。其配置文件与 Java 版服务端有几点关键不同：

1. **server.properties 用 UDP 端口**：默认 `19132`，不是 Java 版的 `25565`（TCP）。
2. **MOTD 不支持 § 颜色码**：基岩版使用 `§` 但部分客户端不渲染，建议用纯文本。
3. **`online-mode` 指 Xbox Live 验证**：不是 Java 版的「正版验证」。基岩版玩家走 Xbox Live 账户体系。
4. **`gamemode` 没有 spectator**：基岩版仅支持 `survival` / `creative` / `adventure` 三种模式。
5. **`level-type` 选项不同**：基岩版为 `DEFAULT` / `FLAT` / `LEGACY` 等（无 ` amplified` / `largeBiomes`）。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| **nukkit.yml** | YAML | Nukkit 专属 | 服务器高级配置（网络、区块、调试等） |
| **server.properties** | Properties | 基岩版继承 | 基础服务器设置（端口、游戏模式、难度等，**与 Java 版字段不同**！） |
| permissions.yml | YAML | Nukkit 专属 | 权限配置 |
| commands.yml | YAML | Nukkit 专属 | 命令配置 |

> 本文重点翻译 **nukkit.yml** 与 **基岩版 server.properties**。其余文件请参阅 Nukkit 官方文档。

---

## nukkit.yml（Nukkit 高级配置）

`nukkit.yml` 位于服务器根目录，首次启动时由 `cn.nukkit.Server` 类从 `lang/<lang>/nukkit.yml` 模板生成。文件顶部注释明确说明：「Some of these settings are safe, others can break your server if modified incorrectly」（部分设置安全，部分设置改错会搞坏服务器）。修改后**通常需要重启服务器**才能生效（Nukkit 没有 Paper 那种细粒度的热重载命令）。

### 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `network.batch-threshold`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举 / `list` 列表。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器；🔄 表示可在不重启情况下生效（罕见，Nukkit 多数配置均需重启）。

---

### 1. settings（基础设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.language` | 服务器语言 | enum | `eng`（`eng`/`chs`/`cht`/`jpn`/`rus`/`spa`/`pol`/`bra`/`kor`/`ukr`/`deu`/`ltu`/`idn`/`cze`/`tur`/`fin`） | ✅ | 服务器控制台与提示消息使用的语言。`eng`=英语，`chs`=简体中文，`cht`=繁体中文，`jpn`=日语 等。修改后需重启。 |
| `settings.force-language` | 强制使用服务器语言 | bool | `false`（`true`/`false`） | ✅ | `true` 时所有字符串按服务器语言翻译后发送给客户端；`false` 时让客户端设备自行处理本地化（推荐）。 |
| `settings.shutdown-message` | 关服提示消息 | string | `Server closed` | ✅ | 服务器关闭时踢出玩家显示的提示文本。 |
| `settings.query-plugins` | Query 暴露插件列表 | bool | `true`（`true`/`false`） | ✅ | `true` 时允许通过 GameSpy Query 协议列出已加载插件。出于安全考虑，公网服务器建议关闭。 |
| `settings.deprecated-verbose` | 弃用 API 警告 | bool | `true`（`true`/`false`） | ✅ | 插件使用已弃用的 API 方法时是否在控制台打印警告。开发环境建议开启，生产环境可关闭以减少日志噪音。 |
| `settings.async-workers` | 异步工作线程数 | enum/int | `auto`（`auto` 或 ≥ 1） | ✅ | AsyncTask 的工作线程数。`auto` 自动检测 CPU 核心数（至少 4）。手动设置时建议不超过 CPU 核心数。 |

---

### 2. network（网络设置）

> 基岩版使用 RakNet 协议（UDP）。本节控制数据包批处理与压缩。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `network.batch-threshold` | 批处理字节阈值 | int | `256`（-1 ~ 65535，单位：字节） | ✅ | 数据包累积到此字节数才进行批处理压缩。`0` = 压缩所有包；`-1` = 完全禁用压缩。降低此值减少延迟但增加 CPU 负担；提高此值节省带宽但增加延迟。 |
| `network.compression-level` | Zlib 压缩级别 | int | `5`（1-9） | ✅ | 批处理包的 Zlib 压缩级别。**值越大 CPU 占用越高、带宽越省**。1=最快压缩比最低，9=最慢压缩比最高。基岩版推荐 5-7。 |
| `network.compression-use-snappy` | 启用 Snappy 压缩 | bool | `false`（`true`/`false`） | ✅ | 实验性：使用 Google Snappy 算法替代 Zlib。压缩比低但速度极快，CPU 紧张的服务器可尝试。⚠️ 实验功能，可能不兼容旧客户端。 |
| `network.encryption` | 启用网络加密 | bool | `true`（`true`/`false`） | ✅ | 是否启用基岩版网络加密（基于 ECDH 握手）。**强烈建议保持 `true`**，关闭后所有数据明文传输，存在严重安全风险。 |

---

### 3. debug（调试设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `debug.level` | 调试日志级别 | int | `1`（1-3） | ✅ | 控制台调试信息详细程度。`1` = 仅正常日志；`2` = 显示调试信息；`3` = 显示所有数据包详情（极大量日志，仅排查问题时使用）。 |

---

### 4. level-settings（世界设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `level-settings.default-format` | 默认世界存储格式 | enum | `leveldb`（`leveldb`/`mcbeta`/`anvil`） | ✅ | 新建世界使用的存储格式。基岩版原生为 `leveldb`，`mcbeta` 为旧版兼容，`anvil` 为 Java 版格式（实验性，不推荐）。**强烈建议保持 `leveldb`**。 |
| `level-settings.auto-tick-rate` | 自动调节 tick 频率 | bool | `true`（`true`/`false`） | ✅ | 服务器卡顿时自动降低 tick 频率以维持稳定。开启后服务器会动态调整以维持 20 TPS。 |
| `level-settings.auto-tick-rate-limit` | 自动降频上限 | int | `20`（≥ 1） | ✅ | 自动降频的最大倍率，避免服务器 tick 速率被降到不可接受的程度。 |
| `level-settings.base-tick-rate` | 基础 tick 频率 | int | `1`（≥ 1） | ✅ | 基础 tick 倍率。`1` = 20 TPS（原版）；`2` = 10 TPS（半速）；`3` = 约 6.7 TPS。**调大可省 CPU 但游戏变卡**。 |
| `level-settings.always-tick-players` | 每 tick 都处理玩家 | bool | `false`（`true`/`false`） | ✅ | `true` 时无论其他设置如何，每个 tick 都处理玩家逻辑。一般保持 `false`。 |

---

### 5. chunk-sending（区块发送）

> 控制服务器向客户端发送区块的节奏，影响玩家登录和移动时的「地形加载」速度。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunk-sending.per-tick` | 每 tick 发送区块数 | int | `4`（≥ 1） | ✅ | 每个 tick（1/20 秒）向单个玩家发送多少个区块。**值越大玩家加载地形越快，但带宽和 CPU 占用越高**。低配服务器建议保持 4，高配可调到 8-16。 |
| `chunk-sending.spawn-threshold` | 出生前发送区块数 | int | `56`（≥ 1） | ✅ | 玩家进服前至少需要发送多少个区块才能让其出生。值过低会导致玩家「悬空」或掉入未加载地形；过高会增加登录等待时间。 |
| `chunk-sending.cache-chunks` | 缓存区块序列化数据 | bool | `false`（`true`/`false`） | ✅ | `true` 时在内存中保存区块的序列化副本，加快向多个玩家发送同一区块的速度。**适合玩家密集的静态世界**（如大厅服），动态生存服建议关闭以省内存。 |

---

### 6. chunk-ticking（区块 tick 处理）

> 控制「玩家周围」哪些区块会被服务器实际 tick（处理实体、红石、作物生长等）。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunk-ticking.per-tick` | 每 tick 处理区块上限 | int | `40`（≥ 1） | ✅ | 每 tick 最多处理多少个区块（实体的 AI、红石、作物生长等）。**降低此值可缓解实体密集时的卡顿**，但作物生长和红石会变慢。 |
| `chunk-ticking.tick-radius` | 区块 tick 半径 | int | `3`（≥ 1，单位：区块） | ✅ | 玩家周围多少区块半径内会被 tick。`3` = 3 个区块半径（即 7×7 范围）。值越大玩家附近活动越流畅，但 CPU 占用越高。 |
| `chunk-ticking.clear-tick-list` | 清空 tick 列表 | bool | `false`（`true`/`false`） | ✅ | 是否在每次 tick 后清空待处理列表。开启可防止列表累积但可能影响连续的红石/作物逻辑。一般保持 `false`。 |

---

### 7. chunk-generation（区块生成）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunk-generation.queue-size` | 生成队列上限 | int | `8`（≥ 1） | ✅ | 等待生成的区块队列最大长度。队列满时新请求会被丢弃。玩家快速移动（如鞘翅飞行）时可适当调大。 |
| `chunk-generation.population-queue-size` | 装饰队列上限 | int | `8`（≥ 1） | ✅ | 等待「装饰」（放置花草、矿物、结构等）的区块队列最大长度。值过小会导致地形装饰滞后。 |

---

### 8. leveldb（LevelDB 存储）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `leveldb.use-native` | 使用原生 LevelDB | bool | `false`（`true`/`false`） | ✅ | `true` 时使用 C++ 原生 LevelDB 实现以获得更高性能。需服务器安装对应 native 库，否则回退到 Java 实现。 |
| `leveldb.cache-size-mb` | LevelDB 缓存大小 | int | `80`（≥ 1，单位：MB） | ✅ | LevelDB 内存缓存大小。**值越大读取越快但占用内存越多**。大型世界建议 128-256 MB。 |

---

### 9. ticks-per（每多少 tick 触发一次）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `ticks-per.autosave` | 自动保存间隔 | int | `6000`（≥ 0，单位：tick；20 tick = 1 秒） | ✅ | 服务器自动保存世界与玩家数据的间隔。`6000` = 每 5 分钟保存一次。`0` = 禁用自动保存（**不推荐**，崩服会丢失进度）。 |

---

### 10. player（玩家设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `player.save-player-data` | 保存玩家数据 | bool | `true`（`true`/`false`） | ✅ | `true` 时玩家数据保存为 `players/<玩家名>.dat`。`false` 时不保存，便于插件完全接管玩家数据。一般保持 `true`。 |
| `player.skin-change-cooldown` | 皮肤更换冷却 | int | `15`（≥ 0，单位：秒） | ✅ | 玩家两次更换皮肤之间的冷却时间。`0` = 无冷却。防止玩家通过频繁换皮肤刷屏或攻击服务器。 |
| `player.attack-stop-sprint` | 攻击停止冲刺 | bool | `true`（`true`/`false`） | ✅ | `true` 时玩家攻击实体后会停止冲刺（原版行为）。`false` 时攻击不会打断冲刺（类似 1.8 PVP 手感）。 |

---

### 11. aliases（命令别名）

> 用户可在此自定义命令别名。例如：
> ```yaml
> aliases:
>   showtheversion: version
>   savestop: [save-all, stop]
> ```

无固定键，按需添加。

---

### 12. worlds（多世界配置）

> 配置服务器加载哪些世界。每个世界可指定生成器和参数。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `worlds.world.generator` | 主世界生成器 | enum | `normal`（`normal`/`nether`/`flat`/`void` 等） | ✅ | 主世界（`world`）使用的地形生成器。`normal` = 标准地形，`flat` = 超平坦，`nether` = 下界地形。 |
| `worlds.nether.generator` | 下界生成器 | enum | `nether` | ✅ | 下界世界（`nether`）的生成器。默认 `nether`。 |

> 可通过 `seed:` 自定义种子，`options:` 传递生成器特定参数。

---

## server.properties（基岩版基础设置）

> ⚠️ **关键提醒**：Nukkit 的 `server.properties` 是**基岩版**格式，**不能复用 Java 版的字段描述**！
> - 端口默认 `19132`（UDP），不是 Java 版的 `25565`（TCP）
> - `gamemode` 没有 `spectator` 选项
> - `online-mode` 指 Xbox Live 验证，不是 Mojang 正版验证

为避免与 Java 版描述符冲突，本表注册时使用文件名 `nukkit-server.properties`。

### 阅读约定

- **键名**：保持原样不翻译。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：所有 `server.properties` 项修改后均需**重启服务器**才能生效（✅）。

---

### 网络与端口

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `server-name` | 服务器名称（MOTD） | string | `Dedicated Server`（任意不含分号的字符串） | ✅ | 服务器在客户端服务器列表中显示的名称。基岩版对 `§` 颜色码支持有限，建议使用纯文本或简单颜色。 |
| `server-port` | IPv4 端口（UDP） | int | `19132`（1-65535） | ✅ | 服务器监听的 **IPv4 UDP** 端口。⚠️ **必须开放 UDP 协议**，不是 TCP！路由器端口转发也需选 UDP。1024 以下端口通常需要管理员权限。 |
| `server-portv6` | IPv6 端口（UDP） | int | `19133`（1-65535） | ✅ | 服务器监听的 **IPv6 UDP** 端口。不需要 IPv6 时可设为 0 禁用。 |
| `enable-lan-visibility` | 局域网可见性 | bool | `true`（`true`/`false`） | ✅ | `true` 时监听并响应局域网服务器发现请求。同一台机器跑多个 Nukkit 时建议关闭以避免端口冲突。 |

### 玩家与权限

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `max-players` | 最大玩家数 | int | `10`（≥ 1） | ✅ | 服务器同时允许的最大玩家数。**值越高对性能影响越大**，小型服建议 20，大型服按硬件配置调整。 |
| `online-mode` | Xbox Live 验证 | bool | `true`（`true`/`false`） | ✅ | **基岩版的关键差异**：`true` 时所有玩家必须通过 Xbox Live 认证。公网服务器**强烈建议开启**，关闭会导致玩家可伪装身份。注意：远程（非 LAN）连接无论此设置如何，**始终需要 Xbox Live 认证**。 |
| `white-list` | 启用白名单 | bool | `false`（`true`/`false`） | ✅ | `true` 时仅 `allowlist.json` 中的玩家可加入。 |
| `default-player-permission-level` | 新玩家权限等级 | enum | `member`（`visitor`/`member`/`operator`） | ✅ | 首次加入的玩家默认权限等级。`visitor`=访客（仅参观，不能交互），`member`=成员（正常游玩），`operator`=管理员（OP 权限）。**生产环境务必用 `member`**，否则新玩家可能有 OP 权限！ |

### 游戏模式与难度

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `gamemode` | 默认游戏模式 | enum | `survival`（`survival`/`creative`/`adventure`） | ✅ | 新玩家加入时的默认游戏模式。⚠️ **基岩版无 `spectator` 选项**！`survival`=生存，`creative`=创造，`adventure`=冒险。 |
| `force-gamemode` | 强制游戏模式 | bool | `false`（`true`/`false`） | ✅ | `true` 时玩家进服始终被强制设置为 `gamemode` 指定的模式，忽略其上次退出时的模式。 |
| `difficulty` | 难度 | enum | `easy`（`peaceful`/`easy`/`normal`/`hard`） | ✅ | 世界难度。`peaceful`=和平（不刷怪），`easy`=简单，`normal`=普通，`hard`=困难（僵尸破门等）。 |
| `allow-cheats` | 允许作弊 | bool | `false`（`true`/`false`） | ✅ | `true` 时允许使用 `/gamemode`、`/give` 等作弊命令。生存服建议 `false`，创造/测试服可设 `true`。 |
| `texturepack-required` | 强制资源包 | bool | `false`（`true`/`false`） | ✅ | `true` 时玩家必须接受服务器资源包才能进服。拒绝资源包的玩家会被踢出。 |

### 世界生成

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `level-name` | 世界名称 | string | `Bedrock level`（文件名合法字符串） | ✅ | 世界文件夹的名称。每个世界在 `worlds/` 下有独立文件夹。改名为新世界，原世界保留但不再加载。 |
| `level-seed` | 世界种子 | string | `（空）` | ✅ | 世界生成种子。留空则随机生成。相同种子生成相同地形。 |
| `level-type` | 世界类型 | enum | `DEFAULT`（`DEFAULT`/`FLAT`/`LEGACY`/`DEFAULT_BIOMES_ *`） | ✅ | 地形类型。`DEFAULT`=标准地形，`FLAT`=超平坦，`LEGACY`=旧版地形。⚠️ **与 Java 版选项不同**（无 `amplified`、`largeBiomes`）。 |
| `view-distance` | 视野距离 | int | `32`（≥ 5，单位：区块） | ✅ | 玩家可见的区块半径。⚠️ **基岩版默认 32，比 Java 版的 10 大很多**！值越大带宽和内存占用越高，公网服建议 10-16。 |
| `tick-distance` | tick 距离 | int | `4`（4-12，单位：区块） | ✅ | 玩家周围多少区块半径内会被服务器 tick（处理实体、红石等）。**基岩版独有字段**，Java 版无此项。值越大 CPU 占用越高。 |
| `generate-structures` | 生成结构 | bool | `true`（`true`/`false`） | ✅ | 是否生成村庄、神殿、废弃矿井等结构。 |

### 安全与反作弊（基岩版独有）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `server-authoritative-movement` | 服务器权威移动 | enum | `server-auth`（`client-auth`/`server-auth`/`server-auth-with-rewind`） | ✅ | **基岩版反作弊关键字段**！`server-auth` = 服务器校验玩家移动，发现异常回滚；`server-auth-with-rewind` = 同上但允许客户端预测；`client-auth` = 客户端权威（不推荐，易被作弊）。 |
| `server-authoritative-block-breaking` | 服务器权威破坏方块 | bool | `true`（`true`/`false`） | ✅ | `true` 时服务器校验玩家破坏方块的合法性（如是否在范围内、是否用了正确工具）。**防加速挖矿作弊**。 |
| `player-movement-action-direction-threshold` | 移动方向阈值 | float | `0.65`（0.0-1.0） | ✅ | 玩家移动方向与视线方向的偏差阈值，超过此值视为可疑移动。 |
| `player-movement-distance-threshold` | 移动距离阈值 | float | `0.5`（≥ 0.0） | ✅ | 单 tick 内玩家移动距离超过此值视为可疑（可能在使用加速/飞行作弊）。 |
| `player-movement-duration-threshold-in-ms` | 异常持续时间阈值 | int | `500`（≥ 0，单位：毫秒） | ✅ | 玩家移动异常持续多久才视为作弊并触发回滚。 |
| `correct-player-movement` | 纠正玩家移动 | bool | `true`（`true`/`false`） | ✅ | `true` 时服务器主动纠正玩家可疑的移动（强制回滚到合法位置）。 |

### 性能与维护

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `max-threads` | 最大线程数 | int | `8`（≥ 0） | ✅ | 服务器最大使用的线程数。`0` = 自动检测使用尽可能多的线程。 |
| `player-idle-timeout` | 玩家挂机踢出 | int | `30`（≥ 0，单位：分钟） | ✅ | 玩家挂机多少分钟后被踢出。`0` = 永不踢出。 |
| `content-log-file-enabled` | 内容日志写文件 | bool | `false`（`true`/`false`） | ✅ | `true` 时将内容错误（如资源包解析失败）写入日志文件，便于排查问题。 |
| `compression-threshold` | 压缩阈值 | int | `1`（0-65535，单位：字节） | ✅ | 网络数据包压缩的最小原始载荷大小。**值越大 CPU 越省但带宽越费**。基岩版默认 1（几乎全压缩）。 |
| `compression-algorithm` | 压缩算法 | enum | `zlib`（`zlib`/`snappy`） | ✅ | 网络压缩算法。`zlib` = 标准压缩（兼容性好），`snappy` = Google Snappy（速度更快但压缩比低）。 |

### 远程管理

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `enable-rcon` | 启用 RCON | bool | `false`（`true`/`false`） | ✅ | 是否启用远程控制台协议（RCON）。允许通过 TCP 发送命令到服务器。**启用务必设置强密码**！ |
| `rcon.password` | RCON 密码 | string | `（空）` | ✅ | RCON 远程管理密码。**启用 RCON 时必须设置**，否则任何人都能远程控制服务器。 |
| `rcon.port` | RCON 端口 | int | `19132`（1-65535，TCP） | ✅ | RCON 监听的 TCP 端口。⚠️ 注意不要与 `server-port`（UDP）冲突。 |

---

## 配置示例

### nukkit.yml（完整默认值）

```yaml
# Advanced configuration file for Nukkit
# Some of these settings are safe, others can break your server if modified incorrectly
# New settings/defaults won't appear automatically on this file when upgrading

settings:
 language: "eng"
 force-language: false
 shutdown-message: "Server closed"
 query-plugins: true
 deprecated-verbose: true
 async-workers: auto

network:
 batch-threshold: 256
 compression-level: 5
 compression-use-snappy: false
 encryption: true

debug:
 level: 1

level-settings:
 default-format: leveldb
 auto-tick-rate: true
 auto-tick-rate-limit: 20
 base-tick-rate: 1
 always-tick-players: false

chunk-sending:
 per-tick: 4
 spawn-threshold: 56
 cache-chunks: false

chunk-ticking:
 per-tick: 40
 tick-radius: 3
 clear-tick-list: false

chunk-generation:
 queue-size: 8
 population-queue-size: 8

leveldb:
 use-native: false
 cache-size-mb: 80

ticks-per:
 autosave: 6000

player:
 save-player-data: true
 skin-change-cooldown: 15
 attack-stop-sprint: true

aliases:

worlds:
 world:
  generator: normal
 nether:
  generator: nether
```

### server.properties（基岩版默认值）

```properties
server-name=Dedicated Server
# 基岩版默认端口 19132（UDP），不是 Java 版的 25565（TCP）！
server-port=19132
server-portv6=19133
enable-lan-visibility=true
max-players=10
gamemode=survival
force-gamemode=false
difficulty=easy
allow-cheats=false
white-list=false
online-mode=true
default-player-permission-level=member
level-name=Bedrock level
level-seed=
level-type=DEFAULT
view-distance=32
tick-distance=4
generate-structures=true
texturepack-required=false
server-authoritative-movement=server-auth
server-authoritative-block-breaking=true
player-movement-action-direction-threshold=0.65
player-movement-distance-threshold=0.5
player-movement-duration-threshold-in-ms=500
correct-player-movement=true
max-threads=8
player-idle-timeout=30
content-log-file-enabled=false
compression-threshold=1
compression-algorithm=zlib
enable-rcon=false
rcon.password=
rcon.port=19132
```

---

## 优化建议（针对公网服务器）

### 🌐 网络优化（必看）

1. **端口转发用 UDP**：基岩版用 **UDP 协议**，路由器端口转发时务必选 UDP，不是 TCP。这是新手最常踩的坑！
2. **压缩级别调优**：带宽紧张的服务器把 `network.compression-level` 调到 `7-9`；CPU 紧张的调到 `3-4`。
3. **关闭 Query 暴露**：公网服建议 `settings.query-plugins: false`，避免泄露插件列表。
4. **视距调小**：默认 `view-distance: 32` 对公网服过大，建议 `10-16`，可大幅降低带宽和内存。

### 🛡️ 安全加固

1. **开启 Xbox Live 验证**：`online-mode=true`，防止玩家伪装身份。
2. **服务器权威移动**：保持 `server-authoritative-movement=server-auth`，防止飞行/加速作弊。
3. **RCON 密码**：如启用 RCON，务必设置 16 位以上强密码，且端口不要与游戏端口冲突。
4. **新玩家权限**：`default-player-permission-level` 必须为 `member`，绝不能用 `operator`！

### ⚡ 性能优化

1. **自动 tick 调节**：保持 `level-settings.auto-tick-rate: true`，让服务器卡顿时自动降速。
2. **LevelDB 缓存**：内存充裕时把 `leveldb.cache-size-mb` 调到 `128-256`，提升世界读取速度。
3. **区块 tick 限制**：实体密集（如刷怪塔）卡顿时，降低 `chunk-ticking.per-tick` 到 `20-30`。
4. **异步线程数**：CPU 核心多时，把 `settings.async-workers` 设为核心数（如 `8`），不要用 `auto`（有时检测不准）。

### 🎮 玩法适配

1. **PVP 服务器**：`player.attack-stop-sprint: false` 可获得类似 1.8 的战斗手感。
2. **大厅服**：开启 `chunk-sending.cache-chunks: true`，玩家频繁进出大厅时显著降低 CPU。
3. **多世界**：在 `worlds` 节自定义多个世界及其生成器，可创建主世界、下界、末地、小游戏地图等。

---

## 参考链接

- 官方 GitHub：https://github.com/CloudburstMC/Nukkit
- 配置模板源码（英文）：`src/main/resources/lang/eng/nukkit.yml`
- 基岩版 BDS server.properties 文档（Microsoft Learn）：https://learn.microsoft.com/minecraft/creator/documents/bedrockserver/server-properties
- 基岩版 server.properties 中文 Wiki：https://zh.minecraft.wiki/w/服务器配置文件格式
- Nukkit 社区文档：https://www.nukkit-mot.com/zh/docs/
- CloudburstMC Discord：https://discord.gg/5PzMkyK

---

> ⚠️ **免责声明**：Nukkit 已较少更新（推荐使用其活跃 fork PowerNukkitX）。本文档基于 CloudburstMC/Nukkit master 分支（commit `dbbb7ca`）整理。基岩版协议与字段会随 Minecraft 版本演进，如遇本文未列出的字段或行为差异，请以服务器实际生成的 `nukkit.yml` / `server.properties` 注释与官方 BDS 文档为准。
