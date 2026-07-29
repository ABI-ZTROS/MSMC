# HexaCord 代理配置文件中文手册

> HexaCord 是基于 **BungeeCord** 的多协议代理分支，最大特性是原生支持基岩版（Bedrock Edition）协议接入。
> 它允许 Java 版与基岩版玩家通过同一代理入口进入后端子服，常配合 Geyser / Floodgate 使用，或单独作为基岩版兼容代理。
> 继承关系：BungeeCord → **HexaCord**
> 官方 GitHub：https://github.com/Hexacord/HexaCord
> 适用版本基准：HexaCord（基于 BungeeCord 1.19+ 分支，含基岩协议适配层）

HexaCord 完整继承 BungeeCord 的 `config.yml` / `messages.yml` 体系（详见 [22-bungeecord.md](./22-bungeecord.md)），并新增独立的 `hexacord.yml` 配置文件。本文档仅聚焦 HexaCord 独有的基岩协议与跨版本配置。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| config.yml | YAML | BungeeCord 继承 | BungeeCord 主配置（监听器、子服、权限等），详见 [22-bungeecord.md](./22-bungeecord.md) |
| messages.yml | YAML | BungeeCord 继承 | BungeeCord 提示消息多语言文件 |
| **hexacord.yml** | YAML | **HexaCord 专属** | **HexaCord 全部独有配置（本文档重点）：基岩协议、跨版本、网络层** |

> 说明：HexaCord 完整继承 BungeeCord 全部配置体系，本文档仅聚焦 HexaCord 独有的 `hexacord.yml`。

## hexacord.yml 整体结构

```yaml
config-version: 1                # 配置版本号（内部用，勿手改
bedrock:                          # 基岩版协议适配
  enabled: false
  listen-port: 19132
  max-players: 100
  broadcast-port: 19132
  motd: "HexaCord Proxy"
protocol:                         # 跨版本协议
  allow-old-clients: true
  min-version: "1.7.2"
  max-version: "1.21.x"
network:                          # 网络层
  packet-compression-level: 6
  use-direct-memory: true
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `bedrock.listen-port`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启代理才能生效；🔄 表示支持热重载（`/greload`）。
- 基岩版协议与 Java 版差异较大，**首次开启 bedrock.enabled 后必须重启代理**，热重载不生效。

---

## 1. 信息块

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `config-version` | 配置版本号 | int | `1`（—） | ✅ | 内部使用，**不要手动修改**。HexaCord 用它做配置自动升级与兼容性判断。 |

---

## 2. bedrock（基岩版协议适配）

HexaCord 的核心模块。开启后会额外监听一个 UDP 端口接收基岩版客户端连接，并把它们桥接到 Java 后端子服。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `bedrock.enabled` | 启用基岩版 | bool | `false`（`true`/`false`） | ✅ | 总开关。true=在 `listen-port` 上额外监听 UDP 基岩版流量；false=只接受 Java 版连接。开启后必须重启 |
| `bedrock.listen-port` | 基岩版监听端口 | int | `19132`（1-65535） | ✅ | 基岩版客户端连接的 UDP 端口。⚠️ 必须与 `config.yml` 中 Java 版 `host` 端口不同，且防火墙需放行 UDP |
| `bedrock.max-players` | 基岩版玩家上限 | int | `100`（≥0） | ✅ | 同时允许的基岩版连接数上限。0=不限制；正数=达上限后拒绝新连接。建议略小于后端实际承载 |
| `bedrock.broadcast-port` | 广播端口 | int | `19132`（1-65535） | ✅ | 基岩版 LAN 广播与 MOTD 查询使用的端口，通常与 `listen-port` 一致。仅在内网穿透 / 多代理时需调整 |
| `bedrock.motd` | 基岩版 MOTD | string | `HexaCord Proxy`（任意文本） | 🔄 | 基岩版客户端在服务器列表中看到的 MOTD 文本。支持 § 颜色码与两行显示（用 `\n` 分隔） |

---

## 3. protocol（跨版本协议）

控制允许进入代理的客户端版本范围。HexaCord 自带协议转换层，可让旧版客户端连接新版后端子服。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `protocol.allow-old-clients` | 允许旧版客户端 | bool | `true`（`true`/`false`） | ✅ | 是否允许低于后端子服版本的 Java 客户端通过协议转换进入。true=开启跨版本；false=严格匹配版本 |
| `protocol.min-version` | 最低客户端版本 | string | `1.7.2`（版本号字符串） | ✅ | 允许进入代理的最低 Java 客户端版本。低于此版本会被直接踢出。调高可减少协议转换开销 |
| `protocol.max-version` | 最高客户端版本 | string | `1.21.x`（版本号字符串） | ✅ | 允许进入代理的最高 Java 客户端版本。高于此版本的客户端会被踢出。用于在 MC 新版本发布后等待适配 |

---

## 4. network（网络层）

控制代理的网络数据传输参数，影响带宽与 CPU 占用。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `network.packet-compression-level` | 数据包压缩级别 | int | `6`（0-9） | ✅ | Netty Zlib 压缩级别。0=不压缩（最快、最费带宽）；9=最高压缩（最省带宽、最费 CPU）。推荐 6 平衡 |
| `network.use-direct-memory` | 使用堆外内存 | bool | `true`（`true`/`false`） | ✅ | 是否使用 Netty 堆外内存（Direct Buffer）。true=减少 GC 压力，提升吞吐；false=堆内存，便于调试内存泄漏 |

---

## 配置示例

```yaml
# HexaCord 推荐配置（Java + 基岩双端混合网络）
config-version: 1

bedrock:
  enabled: true
  listen-port: 19132              # 基岩版 UDP 端口，需放行防火墙
  max-players: 200
  broadcast-port: 19132
  motd: "§a跨版本网络 §7| §bJava + Bedrock"

protocol:
  allow-old-clients: true
  min-version: "1.12.2"           # 太旧的 1.7/1.8 客户端转换开销大，建议收紧
  max-version: "1.21.x"

network:
  packet-compression-level: 6
  use-direct-memory: true
```

## 优化建议

1. **端口务必分离**：Java 版 `host`（TCP）与基岩版 `listen-port`（UDP）必须不同，否则会冲突导致代理无法启动。
2. **基岩版需配合 Floodgate**：仅靠 HexaCord 桥接基岩版会导致基岩玩家显示为 Java 占位账号；要正确显示基岩版玩家名 / 皮肤，需在代理上额外安装 Floodgate。
3. **跨版本有性能成本**：`allow-old-clients=true` 时每条数据包都要做协议转换，CPU 占用显著上升。若后端只有单一版本玩家，建议关闭跨版本。
4. **min-version 不要设太低**：1.7.x 协议与新版差异极大，转换层 bug 较多，建议最低设为 1.12.2 或 1.16.5。
5. **direct-memory 默认开**：堆外内存可显著降低大流量下的 GC 停顿，**除非排查内存泄漏**否则不要关闭。
6. **基岩版 LAN 发现**：`broadcast-port` 仅影响 LAN 局域网发现，公网玩家直接填 IP 即可，无需调整。
7. **优先考虑 Geyser**：若仅是「让基岩版进 Java 服」需求，独立 Geyser + Velocity/Paper 通常比 HexaCord 更稳定且持续维护；HexaCord 更适合需要 BungeeCord 兼容性的老网络。
