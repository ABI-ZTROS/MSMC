# Waterfall 代理配置文件中文手册

> ⚠️ 此核心已停止维护 / 归档（PaperMC 团队已将 Waterfall 标记为 Archived，官方推荐迁移至 [Velocity](./23-velocity.md)），但本文档仍完整提供翻译供存量网络参考。
>
> Waterfall 是 **PaperMC 团队**维护的 BungeeCord 分支，目标是更友好的控制台输出、更完善的日志、更可定制化的配置与更快的 bug 修复。
> 继承关系：BungeeCord → **Waterfall**
> 官方 GitHub：https://github.com/PaperMC/Waterfall
> 适用版本基准：Waterfall 1.20.x（最终归档版本，对应 BungeeCord 1.20 协议）

Waterfall 完整继承 BungeeCord 的 `config.yml` / `messages.yml` 体系（详见 [22-bungeecord.md](./22-bungeecord.md)），并新增独立的 `waterfall.yml` 配置文件。本文档仅聚焦 Waterfall 独有的日志、性能与网络配置。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| config.yml | YAML | BungeeCord 继承 | BungeeCord 主配置（监听器、子服、权限等），详见 [22-bungeecord.md](./22-bungeecord.md) |
| messages.yml | YAML | BungeeCord 继承 | BungeeCord 提示消息多语言文件 |
| **waterfall.yml** | YAML | **Waterfall 专属** | **Waterfall 全部独有配置（本文档重点）：日志、性能、网络** |

> 说明：Waterfall 完整继承 BungeeCord 全部配置体系，本文档仅聚焦 Waterfall 独有的 `waterfall.yml`。

## waterfall.yml 整体结构

```yaml
config-version: 1                # 配置版本号（内部用，勿手改
log_initial_handler_logs: true   # 初始连接日志
log_pings: true                  # Ping 日志
force_empty_motd: false          # 强制空 MOTD
force_empty_player_sample: false # 强制空玩家样本
sample_count: 12                 # 玩家样本数量
disable_tab_list_rewrite: false  # 禁用 Tab 重写
use_netty_dns_resolver: true     # 使用 Netty DNS
throttling:                      # 限流
  tabcomplete: 1000
```

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `throttling.tabcomplete`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启代理才能生效；🔄 表示支持热重载（`/greload`）。
- 由于 Waterfall 已归档，**新建网络建议直接使用 [Velocity](./23-velocity.md)**；存量 BungeeCord 网络可在过渡期继续使用 Waterfall。

---

## 1. 信息块

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `config-version` | 配置版本号 | int | `1`（—） | ✅ | 内部使用，**不要手动修改**。Waterfall 用它做配置自动升级与兼容性判断。 |

---

## 2. log（日志设置）

控制 Waterfall 控制台与日志文件的输出内容，可减少噪音、便于审计。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `log_initial_handler_logs` | 初始连接日志 | bool | `true`（`true`/`false`） | 🔄 | 是否记录玩家建立连接时的初始 Netty Handler 日志。true=记录（便于排查握手问题）；false=关闭以减少日志噪音 |
| `log_pings` | Ping 请求日志 | bool | `true`（`true`/`false`） | 🔄 | 是否记录客户端对代理的 ping 请求（即服务器列表刷新触发的 ping）。关闭可大幅减少日志量 |

---

## 3. motd-sample（MOTD 与玩家样本）

控制代理在服务器列表中显示的内容，比 BungeeCord 原生更灵活。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `force_empty_motd` | 强制空 MOTD | bool | `false`（`true`/`false`） | 🔄 | true=忽略 `config.yml` 中 listeners.motd，服务器列表始终显示空 MOTD。适合子服列表不希望被外部探测的场景 |
| `force_empty_player_sample` | 强制空玩家样本 | bool | `false`（`true`/`false`） | 🔄 | true=服务器列表不再显示在线玩家头像与名字。可隐藏玩家身份，避免被外挂工具批量探测 |
| `sample_count` | 玩家样本数量 | int | `12`（≥0） | 🔄 | 服务器列表显示的在线玩家头像 / 名字数量。调小可减少数据包大小；0=不显示任何玩家 |

---

## 4. network（网络设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `disable_tab_list_rewrite` | 禁用 Tab 重写 | bool | `false`（`true`/`false`） | ✅ | 是否禁用代理对 Tab 列表的强制重写。true=把 Tab 列表交还给后端子服控制（适合 GLOBAL 模式异常的服）；false=由代理统一管理 |
| `use_netty_dns_resolver` | 使用 Netty DNS 解析器 | bool | `true`（`true`/`false`） | ✅ | 是否使用 Netty 自带的异步 DNS 解析器（而非 JDK 同步解析）。true=解析更快、不阻塞主线程；false=退回 JDK 解析，便于排查 DNS 问题 |

---

## 5. throttling（限流）

针对特定操作的客户端请求频率限制。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `throttling.tabcomplete` | Tab 补全限流 | int | `1000`（≥0，单位：毫秒） | 🔄 | 同一玩家两次 Tab 补全请求之间的最小间隔（毫秒）。防止恶意客户端通过疯狂 Tab 补全窃取命令列表或刷 CPU |

---

## 配置示例

```yaml
# Waterfall 推荐配置（中等规模公开网络，已归档版本）
config-version: 1

log_initial_handler_logs: false   # 减少日志噪音
log_pings: false                  # 服务器列表刷新太频繁，关闭 ping 日志

force_empty_motd: false
force_empty_player_sample: false
sample_count: 8                   # 略减样本，缩小响应包

disable_tab_list_rewrite: false
use_netty_dns_resolver: true

throttling:
  tabcomplete: 1000
```

## 优化建议

1. **正式上生产请迁移 Velocity**：Waterfall 已归档，不再接收安全更新与协议适配。新协议（如 1.21+ 新特性）只能等社区 fork，建议尽快规划迁移。
2. **关闭 ping 日志可显著减噪**：公开服每天数千次服务器列表刷新会产生大量 `log_pings` 噪音，建议设为 `false`。
3. **谨慎使用 force_empty_player_sample**：开启后服务器列表不显示玩家头像，部分玩家会误以为服务器无人。仅在明确需要隐藏玩家列表时开启。
4. **use_netty_dns_resolver 保持 true**：JDK 同步 DNS 解析在高并发下会阻塞代理主线程，导致假死。除非排查 DNS 问题，否则不要关闭。
5. **tabcomplete 限流别调太低**：低于 500ms 仍可能被刷，但过低会让玩家 Tab 补全延迟明显。1000ms 是平衡值。
6. **disable_tab_list_rewrite 慎用**：开启后跨服 Tab 列表统一性会丢失，仅在后端插件强烈冲突时尝试。
7. **保留配置备份**：归档后无新版本，迁移到 Velocity 时配置格式完全不同（YAML → TOML），迁移前务必备份 BungeeCord `config.yml` 与 `waterfall.yml`。
