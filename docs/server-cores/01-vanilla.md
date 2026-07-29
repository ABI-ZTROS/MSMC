# Vanilla（原版）服务器配置文件中文手册

> Vanilla 指 Mojang 官方发布的 Minecraft Java 版原版专用服务端（`minecraft_server.jar` / `server.jar`），不含任何插件 API 与第三方优化。
> 继承关系：**Vanilla**（最底层基线，所有 Java 版核心均继承自此）
> 官方文档：https://minecraft.wiki/w/Server.properties
> 官方下载：https://www.minecraft.net/download/server
> 数据来源：Minecraft Wiki / Mojang 官方源码 `net.minecraft.server.dedicated.Settings` / DedicatedServerProperties
> 适用版本基准：Minecraft Java 1.21.x（2025–2026 稳定版）

Vanilla 服务端是 Mojang 官方实现，配置文件 `server.properties` 采用 Java Properties 格式（`键=值`），位于服务器根目录，**仅服务器启动时读取**，所有项修改后均需重启服务器才能生效。文件若不存在，服务器首次启动时会自动生成默认值。Mojang 还会通过 `--worldEdit` / 启动参数等方式补充少量配置（不在本文件中）。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| **server.properties** | Properties | **Vanilla 专属** | **基础服务器设置（本文档重点）** |
| ops.json | JSON | Vanilla 继承 | 管理员列表（由 `/op` 命令维护，请勿手动编辑） |
| whitelist.json | JSON | Vanilla 继承 | 白名单（由 `/whitelist` 命令维护） |
| banned-players.json / banned-ips.json | JSON | Vanilla 继承 | 封禁列表（由 `/ban` 命令维护） |
| server-icon.png | PNG | Vanilla 继承 | 服务器列表图标（64×64） |

> 本文仅翻译 `server.properties`。其余 JSON 文件由命令动态维护，不应手动编辑。

## 阅读约定

- **键名**：保持原样不翻译，扁平化（如 `server-port`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举 / `double` 浮点。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示必须重启服务器才能生效（Vanilla 几乎所有项均为 ✅）；少数项（如 `white-list`、`enforce-whitelist`）通过命令热切换。
- Properties 格式注意：布尔值写 `true`/`false`，字符串无须引号，注释以 `#` 开头。

---

## server.properties（基础服务器设置）

### 1. 网络与连接

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `server-port` | 服务器端口 | int | `25565`（1–65535） | ✅ | 服务器监听的 IPv4 TCP 端口。⚠️ 若设为 `-1` 则使用默认值 25565。端口冲突时换用其他端口（如 `25566`）。 |
| `query.port` | Query 端口 | int | `25565`（1–65535） | ✅ | GameSpy Query 协议（用于服务器列表网站查询在线人数）监听端口。仅在 `enable-query=true` 时生效。可设为与 `server-port` 不同的端口。 |
| `enable-query` | 启用 Query | bool | `false`（`true`/`false`） | ✅ | 是否开启 GameSpy Query 协议，允许第三方网站查询服务器信息（在线人数、版本、插件等）。公网服建议关闭，避免泄露信息。 |
| `enable-rcon` | 启用 RCON | bool | `false`（`true`/`false`） | ✅ | 是否启用远程控制台协议（RCON），允许通过 TCP 远程执行命令。启用**必须**设置 `rcon.password`！ |
| `rcon.port` | RCON 端口 | int | `25575`（1–65535） | ✅ | RCON 监听的 TCP 端口。请勿与 `server-port` 冲突。 |
| `rcon.password` | RCON 密码 | string | ` `（空 = 不安全） | ✅ | RCON 远程控制密码。启用 RCON 时**必须**设置强密码，否则任何人都可远程控制服务器。修改需重启。 |
| `server-ip` | 服务器绑定 IP | string | ` `（空 = 所有网卡） | ✅ | 服务器绑定的本机 IP。留空则监听所有网卡（`0.0.0.0`）。单机多服时填对应网卡 IP。 |
| `network-compression-threshold` | 网络压缩阈值 | int | `256`（-1–65535） | ✅ | 数据包大于此字节数才压缩发送。`256` 表示 ≤256 字节的包不压缩。`-1` 完全禁用压缩（带宽紧张禁用可省 CPU 但占用网速）。`0` 压缩所有包。 |
| `use-native-transport` | 使用原生网络传输 | bool | `true`（`true`/`false`） | ✅ | Linux 上是否启用 epoll 原生网络（性能更高）。Windows / macOS 自动回退。 |
| `prevent-proxy-connections` | 阻止代理连接 | bool | `false`（`true`/`false`） | ✅ | 若为 `true`，服务器会向 Mojang 询问玩家 IP 是否经过代理 / VPN，是则拒绝。可能误伤合法玩家，慎用。 |

---

### 2. 在线模式与认证

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `online-mode` | 在线（正版）验证 | bool | `true`（`true`/`false`） | ✅ | **关键安全项**！`true` 启用 Mojang 正版验证，玩家必须用正版账号登录，UUID 由 Mojang 提供。`false` 关闭验证（俗称「离线模式」），玩家可用任意用户名登录，UUID 由用户名哈希生成，存在身份冒充风险。⚠️ 前置 BungeeCord/Velocity 时**必须**设为 `false`，由代理层做验证。 |
| `prevent-proxies` | 阻止代理 | bool | `false`（`true`/`false`） | ✅ | 旧名 `prevent-proxy-connections` 的别名（1.20+），是否阻止玩家使用代理 / VPN。 |

---

### 3. 玩家管理

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `max-players` | 最大玩家数 | int | `20`（0–2147483647） | ✅ | 服务器同时允许的最大在线玩家数。`0` 视为「无上限」（不推荐）。值越大对性能与内存影响越大。 |
| `white-list` | 启用白名单 | bool | `false`（`true`/`false`） | ✅ | 是否启用白名单，启用后仅 `whitelist.json` 中的玩家可加入。可由 `/whitelist on/off` 热切换。 |
| `enforce-whitelist` | 强制白名单 | bool | `false`（`true`/`false`） | ✅ | 若为 `true`，服务器在运行期间定期检查白名单，被移出白名单的在线玩家会被立即踢出。`false` 则仅在新玩家加入时检查。 |
| `enforce-secure-profile` | 强制安全档案 | bool | `true`（`true`/`false`） | ✅ | 1.19+：是否要求玩家使用 Mojang 签名的聊天档案。`true` 时未签名玩家无法加入（防聊天伪造）。离线模式自动忽略。 |
| `log-ips` | 记录玩家 IP | bool | `true`（`true`/`false`） | ✅ | 1.20.6+：是否在玩家加入时将其 IP 写入日志。关闭可满足 GDPR 等隐私合规要求。 |
| `player-idle-timeout` | 玩家挂机踢出 | int | `0` 分钟（0–2147483647） | ✅ | 玩家挂机多少分钟后被踢出。`0` = 永不踢出。 |
| `op-permission-level` | OP 默认权限等级 | int | `4`（0–4） | ✅ | 新增 OP（`/op` 命令）默认赋予的权限等级。`0` 无权限；`1` 可绕过 spawn 保护；`2` 可用命令方块 / 编辑器；`3` 可用 `/ban`、`/whitelist`；`4` 拥有所有权限（含 `/stop`）。 |
| `function-permission-level` | 函数权限等级 | int | `2`（0–4） | ✅ | 数据包中函数（`/function`）执行时的权限等级。建议保持 `2`，过高可能导致数据包函数执行危险命令。 |

---

### 4. 世界与生成

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `level-name` | 世界名称 | string | `world`（文件夹名） | ✅ | 主世界存档文件夹名。⚠️ **修改不会重命名旧文件夹**，会创建新世界。改名前请手动重命名文件夹。 |
| `level-seed` | 世界种子 | string | ` `（空 = 随机） | ✅ | 世界生成种子。留空则随机生成。相同种子生成相同地形。可填数字或字符串（字符串会被哈希为数字）。**仅影响新生成的区块**，已有区块不变。 |
| `level-type` | 世界类型 | enum | `minecraft\:normal`（`minecraft\:normal` / `minecraft\:flat` / `minecraft\:large_biomes` / `minecraft\:amplified` / `minecraft\:single_biome`） | ✅ | 地形类型。`normal` = 标准地形；`flat` = 超平坦；`large_biomes` = 大生物群系；`amplified` = 极限高度（耗资源）；`single_biome` = 单一群系（需配合 `generator-settings`）。⚠️ 1.19+ 必须加 `minecraft\:` 命名空间前缀。 |
| `generator-settings` | 生成器参数 | string | ` `（空 = 默认） | ✅ | 自定义世界生成参数（JSON 格式）。超平坦用 `{"layers":[{"block":"stone","height":1}],"biome":"plains"}`。详细格式见 Minecraft Wiki。 |
| `generate-structures` | 生成结构 | bool | `true`（`true`/`false`） | ✅ | 是否生成村庄、神殿、废弃矿井等结构。关闭后仍会生成传送门废墟。**仅影响新生成的区块**。 |
| `max-world-size` | 最大世界半径 | int | `29999984`（1–29999984） | ✅ | 世界边界半径（方块）。默认值接近原版最大值。缩小可限制玩家活动范围。 |
| `spawn-protection` | 出生保护半径 | int | `16`（0–256） | ✅ | 出生点周围多少方块半径内仅 OP 可破坏（半径 16 = 33×33 区域）。`0` 完全禁用出生保护。 |
| `allow-nether` | 允许进入下界 | bool | `true`（`true`/`false`） | ✅ | 是否生成并允许进入下界。关闭后下界不生成，传送门不工作。 |
| `allow-end` | 允许进入末地 | bool | `true`（`true`/`false`） | ✅ | 是否生成并允许进入末地。Vanilla 1.21+ 通过此键控制（旧版仅 Bukkit 控制）。 |
| `hardcore` | 极限模式 | bool | `false`（`true`/`false`） | ✅ | 极限模式：玩家死亡后**永久封禁**（踢出 + 加入封禁）。难度自动锁定为 `hard`。⚠️ 已有玩家不受影响。 |
| `max-tnt-radius` | TNT 爆炸上限 | int | `100`（0–2147483647） | ✅ | 1.21.5+：单次 TNT 链式爆炸的最大影响半径（方块）。`100` 是原版上限。降低可防止 TNT 大炮卡服。 |

---

### 5. 游戏机制

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `gamemode` | 默认游戏模式 | enum | `survival`（`survival`/`creative`/`adventure`/`spectator`） | ✅ | 新玩家加入时的默认游戏模式。`survival` = 生存；`creative` = 创造；`adventure` = 冒险；`spectator` = 旁观（仅旁观，无碰撞）。 |
| `force-gamemode` | 强制游戏模式 | bool | `false`（`true`/`false`） | ✅ | `true` 时玩家进服始终被强制设置为 `gamemode` 指定的模式，忽略其上次退出时的模式。 |
| `difficulty` | 难度 | enum | `easy`（`peaceful`/`easy`/`normal`/`hard`） | ✅ | 世界难度。`peaceful` = 和平（不刷怪，回血快）；`easy` = 简单（饥饿不致死）；`normal` = 普通（标准）；`hard` = 困难（饥饿致死，僵尸破门）。极限模式自动锁定为 `hard`。 |
| `pvp` | 允许 PvP | bool | `true`（`true`/`false`） | ✅ | 是否允许玩家间互相伤害。关闭后玩家无法直接攻击其他玩家（间接伤害如岩浆仍有效）。 |
| `allow-flight` | 允许飞行 | bool | `false`（`true`/`false`） | ✅ | 是否允许玩家在生存模式下飞行。⚠️ 不是「开启飞行」，而是「反飞行作弊是否豁免创造/旁观模式之外的情况」。生存模式插件飞行需设为 `true`，否则会被踢出。 |
| `allow-cheats` | 允许作弊 | bool | `false`（`true`/`false`） | ✅ | 是否允许使用 `/gamemode`、`/give`、`/tp` 等作弊命令（非 OP 也可用）。生产环境**必须** `false`。 |
| `spawn-animals` | 生成动物 | bool | `true`（`true`/`false`） | ✅ | 是否生成被动动物（牛、羊等）。关闭后已存在动物保留，不再新生。 |
| `spawn-npcs` | 生成 NPC | bool | `true`（`true`/`false`） | ✅ | 是否生成村民等 NPC。 |
| `spawn-monsters` | 生成怪物 | bool | `true`（`true`/`false`） | ✅ | 是否生成敌对怪物。关闭后已存在怪物保留，不再新生。和平难度下自动禁用。 |
| `view-distance` | 视野距离 | int | `10`（3–32） | ✅ | 玩家可见的区块半径。`10` = 21×21 区块。值越大带宽和内存占用越高，公网服建议 `8`–`12`。 |
| `simulation-distance` | 模拟距离 | int | `10`（3–32） | ✅ | 玩家周围多少区块半径内会被 tick（实体 AI、作物生长、红石）。值越大 CPU 占用越高。通常 ≥ `view-distance`。`0` 等于 `view-distance`。 |
| `entity-broadcast-range-percentage` | 实体广播范围百分比 | int | `100`（10–1000） | ✅ | 实体（怪物、掉落物等）对玩家的可见距离百分比。`100` = 原版默认（基于实体类型）。降低可减少网络包但玩家会看到实体「突然出现」。 |

---

### 6. 资源包

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `resource-pack` | 资源包 URL | string | ` `（空 = 不发送） | ✅ | 服务器向客户端推送的资源包下载 URL（须为直链）。留空则不发送。⚠️ 必须 HTTPS 且公网可访问。 |
| `resource-pack-sha1` | 资源包 SHA-1 | string | ` `（空 = 不校验） | ✅ | 资源包文件的 SHA-1 哈希值。强烈建议填写，可让客户端校验完整性，避免下载损坏或被篡改。 |
| `resource-pack-prompt` | 资源包提示文本 | string | ` `（空 = 无提示） | ✅ | 1.17+：玩家首次被推送资源包时显示的提示文本（JSON 文本格式）。仅在 `require-resource-pack=false` 时显示。 |
| `require-resource-pack` | 强制资源包 | bool | `false`（`true`/`false`） | ✅ | `true` 时玩家**必须**接受资源包才能进服，拒绝会被踢出。⚠️ 资源包 URL 必须可用，否则玩家无法进服。 |
| `max-chained-neighbor-updates` | 链式邻居更新上限 | int | `1000000`（0–2147483647） | ✅ | 1.19.3+：单次方块变更引发的链式邻居更新最大深度。超过即停止传播。降低可防止红石回路卡服。 |

---

### 7. 服务器信息与状态

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `motd` | 服务器 MOTD | string | `A Minecraft Server`（任意文本） | ✅ | 服务器在客户端列表中显示的简介文本。支持 `§` 颜色码与 `\n` 换行（最多两行，每行 60 字符）。 |
| `enable-status` | 启用状态响应 | bool | `true`（`true`/`false`） | ✅ | 是否响应客户端的服务器列表 ping 请求。关闭后客户端列表显示「无法连接」，但仍可直连。可隐藏服务器存在性。 |
| `enable-jmx-monitoring` | 启用 JMX 监控 | bool | `false`（`true`/`false`） | ✅ | 是否启用 JMX（Java Management Extensions）监控，允许通过 JConsole / VisualVM 远程查看服务器指标。 |
| `snooper-enabled` | 启用 Snooper 数据上报 | bool | `false`（`true`/`false`） | ✅ | 是否向 Mojang 上报匿名统计数据（硬件、版本、插件等）。1.15 后默认 `false`，1.19+ 已移除（保留键仅兼容旧配置）。 |

---

### 8. 性能与同步

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `sync-chunk-writes` | 同步区块写入 | bool | `true`（`true`/`false`） | ✅ | 是否同步写入区块到磁盘。`true` 数据安全但写入稍慢；`false` 异步写入更快但崩溃时可能丢数据。NVMe SSD 可设 `false`。 |
| `use-native-transport` | 原生网络（Linux） | bool | `true`（`true`/`false`） | ✅ | Linux 上启用 epoll 原生网络 IO，性能更高。非 Linux 自动忽略。 |
| `rate-limit` | 数据包速率限制 | int | `0`（0–2147483647） | ✅ | 单个玩家每秒最大数据包数。`0` = 不限制。降低可防部分 DDoS，但过低影响正常游玩。 |
| `max-tick-time` | 单 tick 最大时长 | int | `60000`（0–2147483647，毫秒） | ✅ | 单个 tick 超过此毫秒数时服务器判定为「卡死」并触发 `watchdog` 崩溃报告。`-1`/`0` 禁用 watchdog。崩溃时会输出线程转储。 |

---

### 9. 文本过滤（1.19+）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `text-filtering` | 启用文本过滤 | bool | `false`（`true`/`false`） | ✅ | 1.19+：是否启用 Mojang 文本过滤服务（需正版验证）。中国大陆服务器一般关闭。 |

---

## 配置示例（server.properties 完整默认值）

```properties
#Minecraft server properties
#Generated 2026-07-29
accepts-transfers=true
allow-flight=false
allow-nether=true
broadcast-console-to-ops=true
broadcast-rcon-to-ops=true
difficulty=easy
enable-command-block=false
enable-jmx-monitoring=false
enable-query=false
enable-rcon=false
enable-status=true
enforce-secure-profile=true
enforce-whitelist=false
entity-broadcast-range-percentage=100
force-gamemode=false
function-permission-level=2
gamemode=survival
generate-structures=true
hardcore=false
hide-online-players=false
initial-disabled-packs=
initial-enabled-packs=vanilla
level-name=world
level-seed=
level-type=minecraft\:normal
log-ips=true
max-chained-neighbor-updates=1000000
max-players=20
max-tick-time=60000
max-world-size=29999984
motd=A Minecraft Server
network-compression-threshold=256
online-mode=true
op-permission-level=4
player-idle-timeout=0
prevent-proxy-connections=false
pvp=true
query.port=25565
rate-limit=0
rcon.password=
rcon.port=25575
require-resource-pack=false
resource-pack=
resource-pack-prompt=
resource-pack-sha1=
server-ip=
server-port=25565
simulation-distance=10
spawn-animals=true
spawn-monsters=true
spawn-npcs=true
spawn-protection=16
sync-chunk-writes=true
text-filtering=false
use-native-transport=true
view-distance=10
white-list=false
```

## 优化建议

1. **公网服务器务必 `online-mode=true`**：除非前置 BungeeCord / Velocity（由代理做验证），否则关闭后任何人可冒充 OP 登录。
2. **调整 `view-distance` 与 `simulation-distance`**：低配服建议 `view-distance=8`、`simulation-distance=6`，可显著降低 CPU 与带宽占用。
3. **RCON 必设强密码**：`enable-rcon=true` 时务必填写 `rcon.password`，并尽量将 `rcon.port` 限制在内网。
4. **`network-compression-threshold`**：CPU 富余带宽紧张设 `256`；CPU 紧张带宽富余设 `-1`；默认 `256` 适合大多数场景。
5. **`max-tick-time`**：崩溃排查期可设为 `-1` 禁用 watchdog，避免误判；生产环境保留 `60000` 以便崩溃时输出线程转储。
6. **`sync-chunk-writes=false`**：使用 NVMe SSD 的服务器可关闭以提升性能，崩溃风险可接受（仅丢失最近区块）。
7. **`spawn-protection=0`**：与 Bukkit/Spigot 的 `spawn-radius` 二选一即可，避免双重保护造成混淆。Vanilla 走此键。
8. **`level-seed` 与 `level-type` 修改无效**：仅影响新生成区块，已有世界改这两项需删除 `world` 文件夹重新生成（**会丢失进度**）。
9. **资源包 `require-resource-pack=true`**：启用前务必测试 URL 可用性，否则玩家无法进服。建议同时填 `resource-pack-sha1` 防篡改。
10. **`enforce-secure-profile=false`**：混合服（含离线玩家）或不需要聊天签名验证的服务器可关闭，避免部分玩家无法加入。

> 参考来源：[Minecraft Wiki - Server.properties](https://minecraft.wiki/w/Server.properties)、Mojang 官方源码 `DedicatedServerProperties.java`（1.21.x 分支）。
