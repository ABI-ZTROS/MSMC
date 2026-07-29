# FlameCord 代理配置文件中文手册

> FlameCord 是基于 **BungeeCord** 的高性能反机器人（AntiBot）安全分支，由 4drian3d（AdrianTodt）开发。
> 它内置了连接限流、IP 防火墙、账户爆破防御、防快速重连等反机器人模块，能在不依赖外部 AntiBot 插件的情况下抵御大量假人攻击。
> 继承关系：BungeeCord → **FlameCord**
> 官方 GitHub：https://github.com/4drian3d/FlameCord
> 适用版本基准：FlameCord（基于 BungeeCord 1.19+ 分支）

FlameCord 完整继承 BungeeCord 的 `config.yml` / `messages.yml` 体系（详见 [22-bungeecord.md](./22-bungeecord.md)），并新增独立的 `flamecord.yml` 配置文件。本文档仅聚焦 FlameCord 独有的反机器人与防火墙配置。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| config.yml | YAML | BungeeCord 继承 | BungeeCord 主配置（监听器、子服、权限等），详见 [22-bungeecord.md](./22-bungeecord.md) |
| messages.yml | YAML | BungeeCord 继承 | BungeeCord 提示消息多语言文件 |
| **flamecord.yml** | YAML | **FlameCord 专属** | **FlameCord 全部独有配置（本文档重点）：反机器人、防火墙、防重连** |

> 说明：FlameCord 完整继承 BungeeCord 全部配置体系，本文档仅聚焦 FlameCord 独有的 `flamecord.yml`。

## flamecord.yml 整体结构

```yaml
config-version: 1                # 配置版本号（内部用，勿手改
antibot:                          # 反机器人模块
  enabled: true
  check-accounts: true
  max-accounts-per-ip: 3
  accounts-per-second: 2
  max-connections-per-ip: 5
  connections-per-second: 4
firewall:                         # 防火墙模块
  enabled: true
  max-rate: 10
  timeout: 5000
reconnect-handler:                # 防快速重连模块
  enabled: true
  time: 600
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `antibot.max-connections-per-ip`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启代理才能生效；🔄 表示支持热重载（`/greload`）。
- 代理端配置通常影响所有跨服入口的安全策略，**建议改动后先压测再上生产**。

---

## 1. 信息块

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `config-version` | 配置版本号 | int | `1`（—） | ✅ | 内部使用，**不要手动修改**。FlameCord 用它做配置自动升级与兼容性判断。 |

---

## 2. antibot（反机器人模块）

FlameCord 的核心模块。通过限制单 IP 的并发连接数与账户请求频率，在握手与登录阶段拦截假人攻击。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `antibot.enabled` | 启用反机器人 | bool | `true`（`true`/`false`） | ✅ | 总开关。true=启用 FlameCord 内置 AntiBot；false=完全关闭，退化为普通 BungeeCord。被攻击时务必 true |
| `antibot.check-accounts` | 检查账户爆破 | bool | `true`（`true`/`false`） | ✅ | 是否启用账户频率检测。true=限制单 IP 在窗口内尝试登录不同账号的次数，可防撞库；false=不检测 |
| `antibot.max-accounts-per-ip` | 单 IP 最大账号数 | int | `3`（≥1） | ✅ | 同一 IP 在窗口时间内最多尝试登录多少个不同账号。超过此值会被视为机器人并踢出 / 封禁 |
| `antibot.accounts-per-second` | 账号请求频率 | int | `2`（≥1，单位：次/秒） | ✅ | 单 IP 每秒最多尝试登录的账号次数。值越小越严格，但可能误杀家庭网络共享 IP 的玩家 |
| `antibot.max-connections-per-ip` | 单 IP 最大连接数 | int | `5`（≥1） | ✅ | 同一 IP 同时允许的未完成握手连接数。超过此值的连接会被直接丢弃，防止 TCP 连接洪水 |
| `antibot.connections-per-second` | 连接请求频率 | int | `4`（≥1，单位：次/秒） | ✅ | 单 IP 每秒最多发起新连接的次数。建议与正常玩家进入频率匹配，过低会误杀玩家 |

---

## 3. firewall（防火墙模块）

基于 Netty 流量速率的 L4 层防护，可在反机器人触发前提前丢弃恶意流量，减轻代理 CPU 负载。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `firewall.enabled` | 启用防火墙 | bool | `true`（`true`/`false`） | ✅ | 总开关。true=启用 Netty 层流量限速；false=关闭，所有连接直通代理主线程 |
| `firewall.max-rate` | 最大速率 | int | `10`（≥1，单位：包/秒） | ✅ | 单 IP 每秒允许通过的最大数据包数。超过此速率的包会被丢弃，可有效缓解坏包攻击（BadPacket） |
| `firewall.timeout` | 超时时间 | int | `5000`（≥0，单位：毫秒） | ✅ | 单连接无数据传输的超时时间。超过此值无响应的连接会被关闭，可释放僵尸连接占用 |

---

## 4. reconnect-handler（防快速重连模块）

限制同一 IP / 玩家被踢出后的重连间隔，防止机器人反复刷连接绕过 AntiBot。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `reconnect-handler.enabled` | 启用防重连 | bool | `true`（`true`/`false`） | ✅ | 总开关。true=被踢出后短时间内禁止重连；false=允许立即重连，会被机器人利用绕过 AntiBot |
| `reconnect-handler.time` | 重连冷却时间 | int | `600`（≥0，单位：秒） | ✅ | 被踢出 / 封禁后再次允许连接的间隔（秒）。值越大越安全，但正常玩家被误杀后等待越久 |

---

## 配置示例

```yaml
# FlameCord 推荐配置（中等规模公开服，应对常规假人攻击）
config-version: 1

antibot:
  enabled: true
  check-accounts: true
  max-accounts-per-ip: 2        # 收紧账号尝试，防撞库
  accounts-per-second: 1
  max-connections-per-ip: 3     # 家庭网络一般够用
  connections-per-second: 2

firewall:
  enabled: true
  max-rate: 8                   # 略低于默认，更早丢包
  timeout: 4000

reconnect-handler:
  enabled: true
  time: 900                     # 15 分钟冷却，反复试错成本高
```

## 优化建议

1. **先压测再上线**：FlameCord 的限流参数对玩家体验影响较大，建议先用 `mcstress` / 假人压测工具模拟攻击，确认无误杀后再放生产。
2. **配合 IP 白名单**：若有固定 IP 管理员或老玩家，可配合 BungeeCord 插件（如 FastLogin）放行已知 IP，避免误杀。
3. **不要把限流调到极端**：`max-connections-per-ip=1` 看似安全，但玩家断线重连、网络抖动会反复触发，导致正常玩家进不来。
4. **离线服需重点防撞库**：离线模式（`online-mode=false`）下账号可任意伪造，务必启用 `check-accounts` 并调小 `max-accounts-per-ip`。
5. **配合外部 WAF / 高防**：FlameCord 是 L7 层防护，无法抵御大规模 L3/L4 DDoS，仍需配合 TCPShield / 高防 IP / HAProxy PROXY protocol 使用。
6. **定期查看日志**：FlameCord 会把拦截记录写入 `logs/latest.log`，定期 grep `FlameCord` 关键词可观察攻击趋势与误杀情况。
