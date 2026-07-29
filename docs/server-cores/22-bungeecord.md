# BungeeCord 代理配置文件中文手册

> BungeeCord 是经典的 Minecraft 代理服务器，由 SpigotMC 团队（md_5 等）开发。
> 它可以把多个 Minecraft 服务器连成一个网络，玩家无需断线即可在子服之间切换。
> 官方网站：https://www.spigotmc.org/wiki/bungeecord/
> 官方配置指南：https://www.spigotmc.org/wiki/bungeecord-configuration-guide/
> 下载地址：https://ci.md-5.net/job/BungeeCord/
>
> ⚠️ 使用 BungeeCord 时，**所有后端子服必须关闭正版验证**（`online-mode=false`），
> 并在子服的 `spigot.yml` 中将 `bungeecord` 设为 `true`。否则会出现 IP 显示为 127.0.0.1、
> 跨服异常或玩家可冒充他人登录等安全问题。

## 配置文件清单

| 文件名 | 格式 | 说明 |
|---|---|---|
| config.yml | YAML | BungeeCord 主配置文件（本文档重点），包含监听器、子服列表、权限、网络等全部设置 |
| messages.yml | YAML | BungeeCord 自带提示消息的多语言文件，由首次启动生成 |

## config.yml（BungeeCord 主配置）

BungeeCord 的 `config.yml` 位于代理主目录下，首次启动时自动生成。下面按区块逐一翻译。
默认值取自 BungeeCord 官方默认配置（含 1.19+ 新增的 `enforce_secure_profile` 与 1.20.5+ 新增的 `reject_transfers`）。

### 全局设置（顶层配置项）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| server_connect_timeout | 后端服务器连接超时 | 5000 | 数值 | ≥0（毫秒） | 是 | BungeeCord 连接后端子服时等待的毫秒数，超过此值视为连接失败 |
| enforce_secure_profile | 强制安全配置文件 | false | 布尔 | true/false | 是 | 1.19+ 聊天签名相关。true=踢出没有 Mojang 聊天签名密钥的玩家；false=允许所有玩家。离线/盗版服必须设为 false |
| remote_ping_cache | 远程 Ping 缓存 | -1 | 数值 | -1 或 ≥0（毫秒） | 是 | 缓存对后端子服 ping 结果的时长（毫秒）。-1=禁用缓存，每次实时查询；正值=缓存指定时长，可减轻子服压力 |
| forge_support | Forge 模组支持 | false | 布尔 | true/false | 是 | 是否启用对 Forge 模组客户端的支持。true=处理 Forge 特有的握手协议；纯原版服保持 false 即可 |
| player_limit | 玩家数量上限 | -1 | 数值 | -1 或 ≥0 | 是 | 整个代理网络同时允许的最大玩家数。-1=不限制（仅受后端服与硬件限制）；正数=达到上限后拒绝新连接 |
| timeout | 玩家连接超时 | 30000 | 数值 | ≥0（毫秒） | 是 | 玩家与 BungeeCord 之间无响应超过此毫秒数后，BungeeCord 会将其踢出。网络差可适当调大 |
| log_commands | 记录命令日志 | false | 布尔 | true/false | 否 | 是否在日志中记录玩家执行的命令。true=记录（便于审计）；false=不记录 |
| network_compression_threshold | 网络压缩阈值 | 256 | 数值 | -1/0/正整数（字节） | 是 | 数据包压缩的大小阈值（字节）。-1=禁用压缩；0=压缩所有包；正数=仅压缩大于此值的数据包。调小可省流量、调大可省 CPU |
| online_mode | 正版验证 | true | 布尔 | true/false | 是 | BungeeCord 自身是否对玩家做 Mojang 正版验证。true=只允许正版玩家进入代理；false=允许离线/盗版玩家。⚠️ 后端子服的 online-mode 必须始终为 false |
| ip_forward | IP 转发 | false | 布尔 | true/false | 是 | 是否把玩家真实 IP 与 UUID 转发给后端子服。true=子服能看到玩家真实 IP（需配合子服 spigot.yml 的 bungeecord: true）；false=子服看到的 IP 是 127.0.0.1 |
| remote_ping_timeout | 远程 Ping 超时 | 5000 | 数值 | ≥0（毫秒） | 是 | BungeeCord 对后端子服发起 ping 时等待响应的毫秒数，超过即视为子服无响应 |
| reject_transfers | 拒绝转移连接 | false | 布尔 | true/false | 是 | 1.20.5+ 新增。是否拒绝通过原服 `/transfer` 命令转入的玩家。true=拒绝；false=允许其他服通过转移机制把玩家送到本代理 |
| prevent_proxy_connections | 阻止代理连接 | false | 布尔 | true/false | 是 | 是否阻止使用 VPN/代理的玩家连接（依赖 Mojang 的 IP 风控数据）。true=阻止；需要 online_mode 为 true 才生效 |
| connection_throttle | 连接限流间隔 | 4000 | 数值 | -1 或 ≥0（毫秒） | 是 | 同一 IP 两次连接之间的最小间隔（毫秒）。-1=禁用限流；正值=在此间隔内重复连接会被拒绝，用于防机器人刷连接 |
| connection_throttle_limit | 连接限流次数上限 | 3 | 数值 | ≥0 | 是 | 在 connection_throttle 限流窗口内允许的最大重试次数。超过此次数后连接会被强制拒绝一段时间 |
| log_pings | 记录 Ping 请求 | true | 布尔 | true/false | 否 | 是否在日志中记录客户端对服务器的 ping 请求（服务器列表刷新会触发 ping）。关闭可减少日志噪音 |
| stats | 统计标识 ID | （随机 UUID） | 字符串 | UUID 格式 | 否 | BungeeCord 匿名统计用的唯一标识符，无需手动修改，每个实例自动生成 |

### servers（后端服务器列表）

`servers` 区块定义 BungeeCord 可以转发的所有后端子服。每个子服以「别名」作为键，下含三个字段。
示例中别名 `lobby` 即玩家用 `/server lobby` 切服时使用的名字。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| servers.`<别名>`.motd | 子服 MOTD 文本 | '&1Just another BungeeCord - Forced Host' | 字符串 | 任意文本（支持颜色代码 `&`） | 否 | 该子服的 MOTD 文本。仅在 `forced_hosts` 强制域名命中或 `ping_passthrough` 关闭时显示给客户端 |
| servers.`<别名>`.address | 子服地址 | localhost:25565 | 字符串 | IP:端口 | 是 | 后端子服的真实 IP 与端口。同一台机器可用 localhost/127.0.0.1，多机部署填内网 IP。⚠️ 各子服端口不可冲突 |
| servers.`<别名>`.restricted | 受限访问 | false | 布尔 | true/false | 否 | 是否限制普通玩家访问该子服。true=只有拥有对应权限（`bungeecord.server.<别名>`）的玩家才能进入；false=所有玩家可进入 |

### listeners（监听器设置）

`listeners` 是一个列表，每个元素是一个监听器（即 BungeeCord 对玩家开放的入口）。
通常只有一个监听器；若要在多端口/多 IP 上开放代理，可添加多个监听器。
默认配置中以下字段位于同一列表项下。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| listeners.host | 监听地址 | 0.0.0.0:25577 | 字符串 | IP:端口 | 是 | BungeeCord 监听玩家连接的地址与端口。0.0.0.0 表示监听所有网卡；端口即玩家客户端填写的端口，需在防火墙放行 |
| listeners.query_port | Query 查询端口 | 25577 | 数值 | 1-65535 | 是 | 用于 MC 协议的 UDP Query 查询端口（供第三方工具查询在线人数等）。仅在 `query_enabled` 为 true 时生效，通常与 host 端口一致 |
| listeners.query_enabled | 启用 Query 查询 | false | 布尔 | true/false | 是 | 是否开启 UDP Query 协议。true=允许通过 Query 协议查询服务器信息；一般无需开启，保持 false |
| listeners.motd | 监听器 MOTD | '&1Another Bungee server' | 字符串 | 任意文本（支持颜色代码 `&`） | 否 | 玩家在多人游戏服务器列表中看到的本代理 MOTD 文本。可使用 `|` 多行写法显示两行 |
| listeners.max_players | 列表显示最大玩家数 | 1 | 数值 | ≥0 | 否 | 服务器列表中显示的「最大玩家数」。仅作展示用，实际限制由 `player_limit` 决定 |
| listeners.tab_list | Tab 列表显示模式 | GLOBAL_PING | 枚举 | GLOBAL_PING/GLOBAL/SERVER | 是 | Tab 玩家列表的显示方式。GLOBAL_PING=全局显示所有玩家及其延迟；GLOBAL=全局显示所有玩家但不更新延迟；SERVER=只显示玩家当前所在子服的玩家列表 |
| listeners.tab_size | Tab 列表大小 | 60 | 数值 | ≥0 | 否 | Tab 列表可显示的最大玩家数量。每 20 格增加一列，默认 60 即三列 |
| listeners.ping_passthrough | Ping 透传 | false | 布尔 | true/false | 否 | 是否把对代理的 ping 请求透传给默认子服。true=服务器列表显示默认子服的 MOTD/在线人数/图标；false=显示本监听器自身的 motd 与 max_players |
| listeners.force_default_server | 强制默认服务器 | false | 布尔 | true/false | 否 | 玩家每次进入代理时是否强制送入 `priorities` 列表中的第一个服务器。true=每次都进默认服（适合带登录服的网络）；false=玩家回到上次所在子服 |
| listeners.priorities | 服务器优先级列表 | [lobby] | 列表 | 已定义的子服别名 | 否 | 玩家进入时尝试连接的服务器顺序。第一个连不上会自动尝试下一个；第一个同时也作为默认服务器 |
| listeners.forced_hosts | 强制域名映射 | pvp.md-5.net: pvp | 映射 | 域名 → 子服别名 | 否 | 把特定域名直接绑定到指定子服。玩家用该域名连接时，即使 force_default_server 为 false 也会进入对应子服，而非默认服 |
| listeners.bind_local_address | 绑定本地地址 | true | 布尔 | true/false | 是 | 是否把到后端子服的连接绑定到本监听器监听的本地地址。一般保持默认 true，多网卡时影响子服看到的源 IP |
| listeners.proxy_protocol | Proxy 协议 | false | 布尔 | true/false | 是 | 是否启用 HAProxy PROXY protocol。true=当 BungeeCord 前面有支持 PROXY protocol 的反向代理（如 HAProxy、TCPShield）时，正确解析真实玩家 IP；无前置代理时必须为 false |

### permissions（权限组）

`permissions` 区块定义权限组及其包含的权限节点。BungeeCord 自带命令的权限节点形如 `bungeecord.command.<命令>`。
默认有 `default`（普通玩家）和 `admin`（管理员）两个组。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| permissions.default | 默认组权限 | [bungeecord.command.server, bungeecord.command.list] | 列表 | 权限节点字符串 | 否 | 普通玩家默认拥有的权限节点列表。默认可用 `/server` 切服与 `/glist` 查看在线人数 |
| permissions.admin | 管理员组权限 | [bungeecord.command.alert, bungeecord.command.end, bungeecord.command.ip, bungeecord.command.reload, bungeecord.command.kick] | 列表 | 权限节点字符串 | 否 | 管理员拥有的权限节点列表。默认包含全服公告、关闭代理、查 IP、重载配置、踢人等命令权限 |

常用 BungeeCord 命令权限节点：

| 权限节点 | 对应命令 | 说明 |
|---|---|---|
| bungeecord.command.server | /server | 切换到指定子服 |
| bungeecord.command.list | /glist | 查看各子服在线人数 |
| bungeecord.command.alert | /alert、/alertraw | 发送全服公告（文本/JSON） |
| bungeecord.command.end | /end | 关闭 BungeeCord 代理 |
| bungeecord.command.ip | /ip | 查询玩家真实 IP |
| bungeecord.command.reload | /greload | 重载 BungeeCord 配置（不重载插件） |
| bungeecord.command.kick | /kick | 踢出指定玩家 |
| bungeecord.command.find | /find | 查询玩家所在子服 |
| bungeecord.command.send | /send | 强制把玩家传送到指定子服 |
| bungeecord.command.perms | /perms | 查看自己所属权限组与权限 |

### groups（玩家分组）

`groups` 区块把玩家（以游戏名标识）分配到上面定义的权限组。一个玩家可属于多个组。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| groups.`<玩家名>` | 玩家所属权限组 | md_5: [admin] | 列表 | 已定义的权限组名 | 否 | 把指定玩家加入哪些权限组。默认把 BungeeCord 作者 md_5 放入 admin 组；建议改为自己的游戏名，避免他人冒充 |

### disabled_commands（禁用命令）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| disabled_commands | 禁用命令列表 | [disabledcommandhere] | 列表 | 命令名（不含 `/`） | 否 | 在全代理范围禁用的命令列表。被禁用的命令对所有玩家不可用，示例值仅为占位符，可填如 `server`、`glist` 等 |

## 配置示例

一个典型的双子服（大厅 + 生存）正版网络配置：

```yaml
server_connect_timeout: 5000
enforce_secure_profile: false
remote_ping_cache: -1
forge_support: false
player_limit: -1

permissions:
  default:
  - bungeecord.command.server
  - bungeecord.command.list
  admin:
  - bungeecord.command.alert
  - bungeecord.command.end
  - bungeecord.command.ip
  - bungeecord.command.reload
  - bungeecord.command.kick

timeout: 30000
log_commands: false
network_compression_threshold: 256
online_mode: true
disabled_commands:
- disabledcommandhere

servers:
  lobby:
    motd: '&1Just another BungeeCord - Forced Host'
    address: localhost:25565
    restricted: false
  survival:
    motd: '&2Survival World'
    address: localhost:25566
    restricted: false

listeners:
- query_port: 25577
  motd: '&1Another Bungee server'
  tab_list: GLOBAL_PING
  query_enabled: false
  proxy_protocol: false
  forced_hosts:
    pvp.md-5.net: pvp
  ping_passthrough: false
  priorities:
  - lobby
  bind_local_address: true
  host: 0.0.0.0:25577
  max_players: 1
  tab_size: 60
  force_default_server: false

ip_forward: true
remote_ping_timeout: 5000
reject_transfers: false
prevent_proxy_connections: false
groups:
  md_5:
  - admin
connection_throttle: 4000
stats: c4b9cabb-93e9-4bce-93ab-4b18642e6f3e
connection_throttle_limit: 3
log_pings: true
```

## 安全注意事项

1. **后端子服必须关闭正版验证**：所有连接到 BungeeCord 的子服 `server.properties` 中 `online-mode` 必须为 `false`，否则 BungeeCord 无法转发。
2. **开启 ip_forward 后务必配置防火墙**：开启 IP 转发后子服会信任代理传来的 IP/UUID，若子服端口暴露在公网，任何人都能冒充他人或绕过 BungeeCord 直连。请用防火墙把子服端口限制为只允许代理访问（参考 SpigotMC 防火墙指南 https://www.spigotmc.org/wiki/firewall-guide/）。
3. **子服 spigot.yml 需开启 bungeecord**：在后端子服的 `spigot.yml` 中将 `settings.bungeecord` 设为 `true`，子服才能正确识别 BungeeCord 转发的 IP 与数据。
4. **默认 groups 建议修改**：默认配置把作者 `md_5` 放入 admin 组，建议改为自己的游戏名，并移除不必要的管理员权限，防止权限滥用。
5. **离线服需关闭 enforce_secure_profile**：盗版/离线服必须把 `enforce_secure_profile` 设为 `false`，否则玩家因没有 Mojang 聊天签名密钥无法正常聊天或进服。

## 数据来源

- SpigotMC 官方配置指南：https://www.spigotmc.org/wiki/bungeecord-configuration-guide/
- SpigotMC BungeeCord 官方主页：https://www.spigotmc.org/wiki/bungeecord/
- BungeeCord GitHub 仓库：https://github.com/SpigotMC/BungeeCord
- SpigotMC 防火墙指南：https://www.spigotmc.org/wiki/firewall-guide/
