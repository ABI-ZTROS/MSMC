# Velocity 代理配置文件中文手册

> Velocity 是 PaperMC 团队开发的现代高性能 Minecraft 代理（Proxy），用于把多台 Minecraft 服务器
> 串联成一个群组网络，让玩家在不同子服之间无缝切换。它取代了老旧的 BungeeCord / Waterfall。
>
> - 官方网站：<https://papermc.io/software/velocity>
> - 官方文档：<https://docs.papermc.io/velocity/>
> - 配置文件参考：<https://docs.papermc.io/velocity/configuration>
> - 默认配置（GitHub）：<https://github.com/PaperMC/Velocity/blob/dev/3.0.0/proxy/src/main/resources/default-velocity.toml>
>
> 本文以 `config-version = "2.7"`（Velocity 3.3+ / 3.4）的默认 `velocity.toml` 为基准，逐项翻译并解释。

---

## 配置文件清单

| 文件名 | 格式 | 说明 |
|---|---|---|
| `velocity.toml` | TOML | Velocity 主配置文件（本文档重点），首次启动代理时自动生成 |
| `forwarding.secret` | 纯文本 | Modern / BungeeGuard 转发模式使用的密钥文件，UTF-8 编码且非空，需与后端 Paper 的 `paper-global.yml` 中 secret 完全一致 |
| `server-icon.png` | PNG | 服务器列表显示的图标（可选），建议 64×64 |

> 说明：Velocity 的所有核心配置都集中在 `velocity.toml` 一个文件里，不存在独立的“高级配置文件”。
> 默认配置可由 Velocity 启动时自动生成，无需手写。

---

## 数据类型约定

| 类型 | 说明 |
|---|---|
| **布尔 (bool)** | `true` / `false` |
| **数值 (int)** | 整数，单位见说明（毫秒 / 字节等） |
| **字符串 (string)** | 普通文本，需用双引号包裹，如 `"0.0.0.0:25577"` |
| **地址 (address)** | 字符串，格式 `IP:端口` 或 `域名:端口`，如 `127.0.0.1:25565` |
| **聊天 (chat)** | 字符串，使用 MiniMessage 格式，1.16+ 支持 RGB 颜色 |
| **枚举 (enum)** | 只能取固定几个值（本文已逐项翻译） |
| **列表 (array)** | 用方括号包裹的字符串数组，如 `["lobby"]` |

---

## velocity.toml（Velocity 主配置）

配置文件按 TOML 节（section）分为五大部分：

1. **根节（基础设置）** —— 不属于任何 `[xxx]` 节的顶层配置项
2. **`[servers]`** —— 后端服务器列表与登录顺序
3. **`[forced-hosts]`** —— 按域名强制路由
4. **`[advanced]`** —— 高级网络 / 日志 / 协议调优
5. **`[query]`** —— GameSpy 4 查询协议响应

---

### 基础设置（根节）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `config-version` | 配置版本号 | `"2.7"` | 字符串 | 固定值，勿改 | 是 | 当前配置文件格式版本，Velocity 用它识别兼容性。**请勿手动修改**，升级新版后 Velocity 会自动迁移。 |
| `bind` | 绑定地址 | `"0.0.0.0:25577"` | 地址 | `IP:端口` | 是 | 代理监听玩家连接的地址和端口。`0.0.0.0` 表示监听所有网卡，`25577` 是 Velocity 默认端口（区别于单服的 25565）。玩家就连接这个端口。 |
| `motd` | 服务器信息（MOTD） | `"<#09add3>A Velocity Server"` | 聊天 | MiniMessage 文本 | 否 | 玩家把你的服务器加进服务器列表时显示的简介。支持 MiniMessage 颜色格式（如 `<#09add3>` 是十六进制颜色），1.16+ 支持 RGB。中文建议用 Unicode 转义。 |
| `show-max-players` | 显示最大玩家数 | `500` | 数值 | ≥0 | 否 | 服务器列表里显示的“最大玩家数”。**仅用于显示**，Velocity 并不真正限制在线人数。 |
| `online-mode` | 在线模式（正版验证） | `true` | 布尔 | `true` / `false` | 是 | 是否通过 Mojang 验证玩家身份。开正版服务必 `true`；离线/盗版服设 `false`（后端子服也要相应配置）。 |
| `force-key-authentication` | 强制公钥认证 | `true` | 布尔 | `true` / `false` | 是 | 是否强制执行 Mojang 新的公钥安全标准（1.19+ 引入的签名聊天/玩家报告机制）。建议保持 `true`。 |
| `prevent-client-proxy-connections` | 阻止客户端代理连接 | `false` | 布尔 | `true` / `false` | 是 | 若玩家客户端的 ISP/AS 与 Mojang 认证服务器返回的不一致，则踢出该玩家。能挡掉一部分 VPN / 代理，但防护较弱。 |
| `player-info-forwarding-mode` | 玩家信息转发模式 | `"NONE"` | 枚举 | 见下 | 是 | 把玩家真实 IP、UUID 等信息转发给后端服务器的方式。详见下方“枚举值翻译”。 |
| `forwarding-secret-file` | 转发密钥文件 | `"forwarding.secret"` | 字符串 | 文件名 | 是 | 存放 Modern / BungeeGuard 转发密钥的文件名。该文件需 UTF-8 编码且非空，密钥必须与所有后端 Paper 子服的 `paper-global.yml` 中的 `secret` 完全一致，否则子服会拒绝连接。 |
| `announce-forge` | 宣布支持 Forge | `false` | 布尔 | `true` / `false` | 是 | 是否向客户端声明本服支持 Forge / FML。模组服建议 `true`；若网络长期跑同一个整合包，可改用 `ping-passthrough = "mods"` 让列表显示更准。 |
| `kick-existing-players` | 踢出已在线玩家 | `false` | 布尔 | `true` / `false` | 是 | 在线模式下，当同名玩家重复连接时，是否踢掉原来在线的那个。`false` = 拒绝新连接（默认）；`true` = 踢旧留新（恢复 Vanilla 行为，用于断线重连）。 |
| `ping-passthrough` | Ping 透传模式 | `"DISABLED"` | 枚举 | 见下 | 否 | 服务器列表 ping 请求是否透传给后端服务器。详见下方“枚举值翻译”。 |
| `enable-player-address-logging` | 记录玩家 IP 地址 | `true` | 布尔 | `true` / `false` | 否 | 是否在日志中记录玩家真实 IP。设为 `false` 后，日志里玩家 IP 会被替换成 `<ip address withheld>`，保护隐私。 |

#### 枚举值翻译：`player-info-forwarding-mode`

| 英文值 | 中文 | 适用版本 | 说明 |
|---|---|---|---|
| `NONE` | 不转发 | 全部 | 不转发任何信息。所有玩家在子服看来都像是从代理本机连接的，UUID 为离线模式。**仅单服或纯离线服时使用**。 |
| `LEGACY` | 传统转发 | 1.12 及以下 | 以 BungeeCord 兼容格式转发玩家 IP 和 UUID。老版本子服（1.12-）用这个。 |
| `BUNGEEGUARD` | BungeeGuard 转发 | 1.12 及以下 | 以 BungeeGuard 插件兼容格式转发，带密钥校验。用于 1.12- 子服且无法做网络层防火墙的共享主机场景。 |
| `MODERN` | 现代转发（推荐） | 1.13 及以上 | Velocity 原生转发，在登录阶段用二进制格式 + HMAC 签名转发玩家 IP/UUID，最安全。**1.13+ 子服首选**。 |

#### 枚举值翻译：`ping-passthrough`

| 英文值 | 中文 | 说明 |
|---|---|---|
| `DISABLED` | 不透传 | 不透传，由 `velocity.toml` 的 `motd` 和 `server-icon.png` 决定列表响应。 |
| `MODS` | 仅透传模组列表 | 仅把后端服务器的模组列表透传到响应。取 `try` 列表（或强制主机）中第一个有模组列表的服务器。 |
| `DESCRIPTION` | 透传描述与模组 | 用后端服务器的描述（MOTD）和模组列表。取第一个能响应的服务器。 |
| `ALL` | 全部透传 | 直接把后端服务器的整个 ping 响应作为代理响应；联系不到任何后端时才回退到 Velocity 配置。 |

---

### `[servers]` 后端服务器列表

本节用于注册 Velocity 可以连接到的后端 Minecraft 服务器。**键名是服务器别名（自定义），值是对应的 IP:端口。** 别名会用在 `/server <别名>` 命令和 `try` / `forced-hosts` 中。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `<服务器别名>` | 后端服务器（自定义别名） | 如 `lobby = "127.0.0.1:30066"` | 地址 | `别名 = "IP:端口"` | 是 | 注册一个后端服务器。键名（如 `lobby`）是你在 Velocity 内部用的别名，值是该子服的地址。可添加任意多个。默认生成 `lobby` / `factions` / `minigames` 三个示例。 |
| `try` | 尝试连接顺序 | `["lobby"]` | 列表 | 服务器别名数组 | 是 | 玩家**首次登录**或**被某子服踢出**时，Velocity 按此列表顺序依次尝试连接，直到找到一个可用的。通常把主城 / 大厅放第一个。 |

**示例：**

```toml
[servers]
lobby = "127.0.0.1:30066"
survival = "127.0.0.1:30067"
creative = "127.0.0.1:30068"

try = ["lobby"]
```

---

### `[forced-hosts]` 强制主机（按域名路由）

本节根据**玩家连接时使用的域名**把玩家直接送到指定子服，而不是走 `try` 默认顺序。需要配合 DNS 把多个域名解析到同一个 Velocity IP。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `"<域名>"` | 强制主机（域名路由） | 如 `"lobby.example.com" = ["lobby"]` | 列表 | `"域名" = [别名...]` | 是 | 当玩家用该域名连接 Velocity 时，直接路由到右侧别名列表中的服务器（按顺序尝试）。键名是带引号的域名。 |

**示例：**

```toml
[forced-hosts]
"lobby.example.com" = ["lobby"]
"factions.example.com" = ["factions"]
"minigames.example.com" = ["minigames"]
```

> 没有匹配到任何强制主机的玩家，会走 `[servers]` 节的 `try` 列表。

---

### `[advanced]` 高级设置

本节是网络、压缩、日志、协议等进阶调优项。**不确定的项请保持默认。**

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `compression-threshold` | 压缩阈值 | `256` | 数值 | `-1` ~ 任意（字节） | 是 | 数据包多大（字节）才开始压缩。`0` = 压缩所有数据包；`-1` = 完全禁用压缩。Minecraft 默认 256 字节。 |
| `compression-level` | 压缩级别 | `-1` | 数值 | `-1` ~ `9` | 是 | zlib 压缩级别，0=最快/压缩率最低，9=最慢/压缩率最高。`-1` = 使用 zlib 默认级别（6）。一般保持 `-1`。 |
| `login-ratelimit` | 登录速率限制 | `3000` | 数值 | ≥0（毫秒） | 是 | 同一 IP 两次连接之间必须间隔的最小毫秒数。`0` = 禁用限速。默认 3000ms（3 秒），防爆破登录。 |
| `connection-timeout` | 连接超时 | `5000` | 数值 | >0（毫秒） | 是 | 代理连接后端服务器时的超时时间。超时则判定连不上。默认 5 秒。 |
| `read-timeout` | 读取超时 | `30000` | 数值 | >0（毫秒） | 是 | 代理等待后端服务器返回数据的超时时间。超时则断开。默认 30 秒。 |
| `haproxy-protocol` | HAProxy 协议 | `false` | 布尔 | `true` / `false` | 是 | 是否接收 HAProxy 的 PROXY 协议消息（用于在 HAProxy 后获取真实玩家 IP）。**不用 HAProxy 就别开**。 |
| `tcp-fast-open` | TCP Fast Open | `false` | 布尔 | `true` / `false` | 是 | 启用 TCP Fast Open，减少握手延迟。**需 Linux 内核 ≥ 4.14**，且系统已开启 `tcp_fastopen`。 |
| `bungee-plugin-message-channel` | BungeeCord 插件消息通道 | `true` | 布尔 | `true` / `false` | 是 | 启用 BungeeCord 插件消息通道（plugin messaging channel）兼容，让部分从 BungeeCord 移植的插件能正常通信。一般保持 `true`。 |
| `show-ping-requests` | 显示 Ping 请求 | `false` | 布尔 | `true` / `false` | 否 | 是否在日志中打印客户端发来的服务器列表 ping 请求。调试用，平时关闭以免刷屏。 |
| `failover-on-unexpected-server-disconnect` | 意外断连故障转移 | `true` | 布尔 | `true` / `false` | 否 | 玩家与子服意外断开（非正常踢出）时，是否尝试把玩家转移到 `try` 列表中的其他服务器，而不是直接踢下线。`false` = 直接断开（BungeeCord 行为）。 |
| `announce-proxy-commands` | 宣布代理命令 | `true` | 布尔 | `true` / `false` | 否 | 是否向 1.13+ 客户端声明 Velocity 自带的代理命令（如 `/server`、`/glist`），用于 Tab 补全显示。 |
| `log-command-executions` | 记录命令执行 | `false` | 布尔 | `true` / `false` | 否 | 是否记录玩家执行的所有命令到日志。审计 / 排查用。 |
| `log-player-connections` | 记录玩家连接 | `true` | 布尔 | `true` / `false` | 否 | 是否记录玩家连接代理、切换子服、断开连接等事件到日志。 |
| `accepts-transfers` | 接受转移连接 | `false` | 布尔 | `true` / `false` | 是 | 是否接受来自其他服务器的“玩家转移”连接（Minecraft 1.20.5+ 的 transfer 功能）。`false` 时代理会拒绝被转移过来的客户端。**2.7 版新增**。 |

---

### `[query]` 查询协议

GameSpy 4 / Minecraft query 协议（UDP）允许外部工具（如服务器列表网站、监控面板）查询服务器在线人数等信息。一般用不到，可保持关闭。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `enabled` | 启用查询协议 | `false` | 布尔 | `true` / `false` | 是 | 是否响应 GameSpy 4 查询请求。通常保持 `false`。 |
| `port` | 查询端口 | `25577` | 数值 | `1` ~ `65535` | 是 | 查询协议监听的 UDP 端口。一般和 `bind` 端口一致即可。 |
| `map` | 地图名称 | `"Velocity"` | 字符串 | 任意文本 | 否 | 报告给查询服务的地图名（显示用）。 |
| `show-plugins` | 显示插件 | `false` | 布尔 | `true` / `false` | 否 | 是否在查询响应中包含已安装的 Velocity 插件列表。 |

---

## 完整默认配置示例（带中文注释）

```toml
# 配置版本号，请勿修改
config-version = "2.7"

# 代理监听地址：所有网卡的 25577 端口
bind = "0.0.0.0:25577"

# 服务器列表显示的 MOTD（MiniMessage 格式）
motd = "<#09add3>A Velocity Server"

# 列表显示的最大玩家数（仅显示，不限人数）
show-max-players = 500

# 正版验证
online-mode = true

# 强制公钥认证
force-key-authentication = true

# 阻止客户端 VPN/代理连接（较弱防护）
prevent-client-proxy-connections = false

# 玩家信息转发模式：NONE / LEGACY / BUNGEEGUARD / MODERN
player-info-forwarding-mode = "NONE"

# 转发密钥文件
forwarding-secret-file = "forwarding.secret"

# 是否声明支持 Forge
announce-forge = false

# 重复连接时是否踢出已在线玩家
kick-existing-players = false

# Ping 透传：DISABLED / MODS / DESCRIPTION / ALL
ping-passthrough = "DISABLED"

# 是否在日志记录玩家 IP
enable-player-address-logging = true

[servers]
# 后端服务器列表：别名 = "IP:端口"
lobby = "127.0.0.1:30066"
factions = "127.0.0.1:30067"
minigames = "127.0.0.1:30068"

# 登录/被踢时依次尝试的服务器
try = ["lobby"]

[forced-hosts]
# 按域名强制路由
"lobby.example.com" = ["lobby"]
"factions.example.com" = ["factions"]
"minigames.example.com" = ["minigames"]

[advanced]
compression-threshold = 256          # 压缩阈值（字节），-1 禁用，0 全压
compression-level = -1               # zlib 压缩级别 -1~9，-1 用默认 6
login-ratelimit = 3000              # 同 IP 登录间隔（毫秒），0 禁用
connection-timeout = 5000           # 连接后端超时（毫秒）
read-timeout = 30000                # 读取后端超时（毫秒）
haproxy-protocol = false            # HAProxy PROXY 协议
tcp-fast-open = false               # TCP Fast Open（需 Linux ≥4.14）
bungee-plugin-message-channel = true # BungeeCord 插件消息通道兼容
show-ping-requests = false          # 日志打印 ping 请求
failover-on-unexpected-server-disconnect = true  # 意外断连故障转移
announce-proxy-commands = true      # 向 1.13+ 客户端声明代理命令
log-command-executions = false      # 记录命令执行
log-player-connections = true       # 记录玩家连接
accepts-transfers = false           # 接受 1.20.5+ 转移连接

[query]
enabled = false                     # 启用 GameSpy 4 查询
port = 25577                        # 查询端口
map = "Velocity"                    # 地图名
show-plugins = false                # 显示插件
```

---

## 常见搭配速查

### 与 Paper 子服搭配（Modern 转发，推荐）

**Velocity 端（velocity.toml）：**

```toml
online-mode = true
player-info-forwarding-mode = "MODERN"
forwarding-secret-file = "forwarding.secret"
```

**每个 Paper 子服：**

1. `server.properties` 设 `online-mode=false`（让 Velocity 负责验证）。
2. `config/paper-global.yml`（Paper 1.18.3+）：

```yaml
proxies:
  velocity:
    enabled: true
    online-mode: true
    secret: "粘贴 forwarding.secret 里的内容"
```

> Paper 1.18.2 及更低版本：该配置位于 `paper.yml` 的 `settings.velocity-support.online-mode`。

### 老版本子服（1.12 及以下）

```toml
player-info-forwarding-mode = "LEGACY"        # 或 "BUNGEEGUARD"（共享主机无防火墙时）
```

---

## 参考来源

- 官方配置文档：<https://docs.papermc.io/velocity/configuration>
- 玩家信息转发文档：<https://docs.papermc.io/velocity/player-information-forwarding>
- 默认配置源文件（GitHub，dev/3.0.0 分支）：<https://github.com/PaperMC/Velocity/blob/dev/3.0.0/proxy/src/main/resources/default-velocity.toml>
- Velocity 下载页：<https://papermc.io/downloads/velocity>
