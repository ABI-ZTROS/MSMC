# Glowstone 服务器配置文件中文手册

> Glowstone 是一个完全用 Java 从零开始编写的轻量级 Minecraft: Java Edition 服务器，100% 原创代码，无任何 Mojang 反编译产物。它原生兼容 Bukkit / Spigot / Paper 插件 API，启动速度快、内存占用低（比官方服务端省约 40% 内存），适合中小型服务器与开发测试环境。
> 官方 GitHub：https://github.com/GlowstoneMC/Glowstone
> 配置文件路径：`config/glowstone/glowstone.yml`（首次启动自动生成，YAML 格式）
> 参考：`net.glowstone.util.config.ServerConfig` 的 `Key` 枚举（约 60-70 项，10-12 个分类）

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|--------|------|------|------|
| `config/glowstone/glowstone.yml` | YAML | Glowstone 主程序自动生成 | 服务器主配置文件，整合 `server.properties` 与 `bukkit.yml` 的全部功能 |
| `config/glowstone/permissions.yml` | YAML | Glowstone 自动生成 | 权限配置 |
| `config/glowstone/commands.yml` | YAML | Glowstone 自动生成 | 命令别名配置 |
| `config/glowstone/help.yml` | YAML | Glowstone 自动生成 | 帮助主题配置 |
| `server.log` | 文本 | Glowstone 运行时 | 主日志（路径可由 `log-file` 修改） |

> 小白提示：所有配置修改后建议**重启服务端**使其生效；仅极少数项可通过 `reload` 命令热加载（下表已标注）。
> 图例：✅ = 无需重启即可生效（重载即可）；🔄 = 必须重启服务端才生效。

---

## glowstone.yml（Glowstone 主配置）

> 下表的「键名」列即为 YAML 中的完整路径，例如 `server.port` 表示 `server:` 节点下的 `port:` 子项。键名一律保持英文不翻译，便于直接对照文件修改。

### server（服务器基础设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `server.name` | 服务器名称 | string | `Glowstone Server` | 🔄 | 仅用于日志与部分插件识别，不影响客户端显示 |
| `server.port` | 服务器端口 | int | `25565` (1-65535) | 🔄 | 客户端连接端口，0 表示随机端口 |
| `server.ip` | 监听 IP | string | ``（空，监听全部） | 🔄 | 留空监听所有网卡；填入具体 IP 仅监听该网卡 |
| `server.max-players` | 最大玩家数 | int | `20` (1-2147483647) | ✅ | 同时在线上限，超出的玩家进入排队或被踢 |
| `server.motd` | 服务器描述 | string | `A Glowstone Server` | ✅ | 客户端服务器列表显示的文字，支持 `§` 颜色码 |
| `server.online-mode` | 正版验证 | bool | `true` (true/false) | 🔄 | `true`=只允许正版玩家；`false`=允许离线/盗版账号，注意皮肤与 UUID 会变 |
| `server.white-list` | 启用白名单 | bool | `false` (true/false) | ✅ | 开启后只有 `whitelist.json` 中的玩家可进入 |
| `server.log-file` | 日志文件路径 | string | `server.log` | 🔄 | 主日志输出文件 |
| `server.snooper-enabled` | 启用信息收集 | bool | `false` (true/false) | ✅ | 上报匿名数据到 Mojang，强烈建议保持 `false` |
| `server.prevent-proxy` | 拒绝代理连接 | bool | `false` (true/false) | ✅ | 启用后逐个反向解析玩家 IP 防止代理，可能误伤，建议关闭 |
| `server.network-compression-threshold` | 网络压缩阈值 | int | `256` (-1 到 65535) | ✅ | 数据包字节数大于该值才压缩；`-1`=禁用压缩；`0`=全部压缩 |
| `server.resource-pack` | 资源包 URL | string | ``（空） | ✅ | 玩家进服时强制推送的资源包下载地址 |
| `server.resource-pack-hash` | 资源包哈希 | string | ``（空） | ✅ | 资源包 SHA-1 哈希，用于校验完整性 |
| `server.resource-pack-prompt` | 资源包提示文本 | string | ``（空） | ✅ | 推送资源包时弹窗显示的提示文字 |
| `server.require-resource-pack` | 强制资源包 | bool | `false` (true/false) | ✅ | `true`=拒绝加载资源包的玩家会被踢出 |

### console（控制台设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `console.history` | 启用命令历史 | bool | `true` (true/false) | 🔄 | 控制台支持上下方向键翻阅历史命令 |
| `console.prompts` | 显示提示符 | bool | `true` (true/false) | ✅ | 是否显示 `>` 提示符 |
| `console.colors` | 控制台彩色输出 | bool | `true` (true/false) | ✅ | 日志按级别上色，Windows 旧 cmd 可能显示乱码 |
| `console.date-format` | 日期格式 | string | `HH:mm:ss` | ✅ | 日志时间戳格式，遵循 Java `SimpleDateFormat` 语法 |

### game（游戏规则设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `game.gamemode` | 默认游戏模式 | string | `SURVIVAL` (SURVIVAL/CREATIVE/ADVENTURE/SPECTATOR) | ✅ | 新玩家首次进入的模式 |
| `game.difficulty` | 难度 | string | `NORMAL` (PEACEFUL/EASY/NORMAL/HARD) | ✅ | `PEACEFUL`=和平；`HARD`=困难，影响刷怪与饥饿 |
| `game.hardcore` | 极限模式 | bool | `false` (true/false) | 🔄 | 死亡后封禁该玩家，难度自动锁定 HARD |
| `game.pvp` | 允许玩家 PvP | bool | `true` (true/false) | ✅ | 是否允许玩家间互相伤害 |
| `game.max-build-height` | 最大建筑高度 | int | `256` (64-256) | 🔄 | 玩家可放置方块的最大 Y 坐标 |
| `game.allow-flight` | 允许飞行 | bool | `false` (true/false) | ✅ | 非创造模式是否允许飞行（防作弊检测） |
| `game.allow-nether` | 启用下界 | bool | `true` (true/false) | 🔄 | 是否生成/加载下界维度 |
| `game.allow-end` | 启用末地 | bool | `true` (true/false) | 🔄 | 是否生成/加载末地维度 |
| `game.announce-achievements` | 公告成就 | bool | `true` (true/false) | ✅ | 玩家获得成就时是否全服广播 |
| `game.force-gamemode` | 强制游戏模式 | bool | `false` (true/false) | ✅ | 玩家每次进入都重置为默认模式，覆盖其上次模式 |
| `game.spawn-protection` | 出生点保护半径 | int | `16` (0-2147483647) | ✅ | 出生点周围多少格内非 OP 无法破坏，`0`=关闭保护 |
| `game.villager-trading` | 允许村民交易 | bool | `true` (true/false) | ✅ | 玩家是否可与村民交易 |

### creatures（生物生成设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `creatures.spawn-monsters` | 生成怪物 | bool | `true` (true/false) | ✅ | 是否生成敌对怪物 |
| `creatures.spawn-animals` | 生成动物 | bool | `true` (true/false) | ✅ | 是否生成被动动物 |
| `creatures.spawn-npcs` | 生成 NPC | bool | `true` (true/false) | ✅ | 是否生成村民等 NPC |
| `creatures.monster-limit` | 怪物上限 | int | `70` (0-2147483647) | ✅ | 单个世界怪物实体数量上限 |
| `creatures.animal-limit` | 动物上限 | int | `15` (0-2147483647) | ✅ | 单个世界被动动物数量上限 |
| `creatures.water-animal-limit` | 水生动物上限 | int | `5` (0-2147483647) | ✅ | 单个世界水生动物数量上限 |
| `creatures.ambient-limit` | 环境生物上限 | int | `15` (0-2147483647) | ✅ | 蝙蝠等环境生物上限 |
| `creatures.ticks-per-monster-spawn` | 怪物生成间隔 | int | `1` (1-2147483647) | ✅ | 每多少 tick 尝试一次怪物生成（20 tick=1 秒） |
| `creatures.ticks-per-animal-spawn` | 动物生成间隔 | int | `400` (1-2147483647) | ✅ | 每多少 tick 尝试一次动物生成 |

### folders（目录设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `folders.settings` | 配置目录 | string | `config` | 🔄 | 所有 YAML 配置所在目录 |
| `folders.plugins` | 插件目录 | string | `plugins` | 🔄 | Bukkit 插件 jar 放置目录 |
| `folders.worlds` | 世界目录 | string | `worlds` | 🔄 | 世界存档数据目录 |
| `folders.cache` | 缓存目录 | string | `cache` | 🔄 | 运行时缓存（如皮肤）目录 |
| `folders.updates` | 更新目录 | string | `update` | 🔄 | 插件热更新目录，放入新 jar 重启后替换 |

### files（文件设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `files.whitelist` | 白名单文件 | string | `whitelist.json` | ✅ | 白名单文件名 |
| `files.permissions` | 权限文件 | string | `permissions.yml` | ✅ | 默认权限配置文件名 |
| `files.commands` | 命令文件 | string | `commands.yml` | ✅ | 命令别名配置文件名 |
| `files.operators` | OP 文件 | string | `ops.json` | ✅ | 管理员列表文件名 |
| `files.help` | 帮助文件 | string | `help.yml` | ✅ | 帮助主题配置文件名 |

### advanced（高级设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `advanced.connection-throttle` | 连接节流 | int | `4000` (0-2147483647) | ✅ | 同一玩家两次连接的最小间隔（毫秒），防刷屏 |
| `advanced.idle-timeout` | 空闲超时 | int | `0` (0-2147483647) | ✅ | 玩家无操作多少分钟后踢出，`0`=禁用 |
| `advanced.warn-on-overload` | 过载警告 | bool | `true` (true/false) | ✅ | 服务器 tick 超时时是否在控制台输出警告 |
| `advanced.exact-login-location` | 精确登录位置 | bool | `false` (true/false) | ✅ | 玩家上线时是否精确还原离线时位置 |
| `advanced.plugin-profiling` | 插件性能分析 | bool | `false` (true/false) | ✅ | 启用 `/timings` 命令分析插件性能 |
| `advanced.use-alternative-logger` | 备用日志器 | bool | `false` (true/false) | 🔄 | 使用 JUL 替代默认日志框架，调试用 |
| `advanced.poor-man-listener` | 简易事件监听 | bool | `false` (true/false) | 🔄 | 兼容旧版插件的低性能事件分发，谨慎开启 |

### extras（额外特性设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `extras.tps-display` | 显示 TPS | bool | `false` (true/false) | ✅ | 在控制台定时输出当前 TPS |
| `extras.kick-on-illegal-behavior` | 非法行为踢出 | bool | `true` (true/false) | ✅ | 检测到客户端非法数据包时直接踢出 |
| `extras.auto-save-on-player-quit` | 退出自动保存 | bool | `true` (true/false) | ✅ | 玩家退出时立即保存其数据 |
| `extras.deploy-on-restart` | 重启自动部署 | bool | `true` (true/false) | 🔄 | 重启时自动从 `update` 目录部署新插件 |

### world（世界生成设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `world.name` | 主世界名称 | string | `world` | 🔄 | 主世界存档文件夹名 |
| `world.seed` | 世界种子 | string | ``（空=随机） | 🔄 | 留空随机生成；填入固定种子可复现世界 |
| `world.type` | 世界类型 | string | `DEFAULT` (DEFAULT/FLAT/LARGEBIOMES/AMPLIFIED) | 🔄 | 地形生成器类型 |
| `world.generator-settings` | 生成器参数 | string | ``（空） | 🔄 | 自定义生成参数，例如超平坦层结构 JSON |
| `world.generate-structures` | 生成结构 | bool | `true` (true/false) | 🔄 | 是否生成村庄、神殿等结构 |
| `world.view-distance` | 视野距离 | int | `10` (3-15) | ✅ | 玩家周围加载区块半径，每 +1 增加约 15% 带宽消耗 |
| `world.keep-spawn-loaded` | 保持出生加载 | bool | `true` (true/false) | ✅ | 出生点区块常驻内存 |

### libraries（依赖库设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `libraries.check-library-updates` | 检查库更新 | bool | `true` (true/false) | 🔄 | 启动时检查依赖库是否有新版本 |
| `libraries.use-library-repo` | 使用库仓库 | bool | `true` (true/false) | 🔄 | 从远程仓库下载缺失依赖，关闭则需手动放置 jar |

---

## 配置示例

```yaml
# config/glowstone/glowstone.yml 完整示例
server:
  name: Glowstone Server
  port: 25565
  ip: ""
  max-players: 20
  motd: "§a§l欢迎来到 Glowstone 服务器"
  online-mode: true
  white-list: false
  log-file: server.log
  snooper-enabled: false
  prevent-proxy: false
  network-compression-threshold: 256
  resource-pack: ""
  resource-pack-hash: ""
  resource-pack-prompt: ""
  require-resource-pack: false

console:
  history: true
  prompts: true
  colors: true
  date-format: "HH:mm:ss"

game:
  gamemode: SURVIVAL
  difficulty: NORMAL
  hardcore: false
  pvp: true
  max-build-height: 256
  allow-flight: false
  allow-nether: true
  allow-end: true
  announce-achievements: true
  force-gamemode: false
  spawn-protection: 16
  villager-trading: true

creatures:
  spawn-monsters: true
  spawn-animals: true
  spawn-npcs: true
  monster-limit: 70
  animal-limit: 15
  water-animal-limit: 5
  ambient-limit: 15
  ticks-per-monster-spawn: 1
  ticks-per-animal-spawn: 400

folders:
  settings: config
  plugins: plugins
  worlds: worlds
  cache: cache
  updates: update

files:
  whitelist: whitelist.json
  permissions: permissions.yml
  commands: commands.yml
  operators: ops.json
  help: help.yml

advanced:
  connection-throttle: 4000
  idle-timeout: 0
  warn-on-overload: true
  exact-login-location: false
  plugin-profiling: false
  use-alternative-logger: false
  poor-man-listener: false

extras:
  tps-display: false
  kick-on-illegal-behavior: true
  auto-save-on-player-quit: true
  deploy-on-restart: true

world:
  name: world
  seed: ""
  type: DEFAULT
  generator-settings: ""
  generate-structures: true
  view-distance: 10
  keep-spawn-loaded: true

libraries:
  check-library-updates: true
  use-library-repo: true
```

---

## 优化建议

1. **内存优化（2GB 服务器）**：将 `world.view-distance` 设为 `8`，`creatures.monster-limit` 降到 `50`，`server.network-compression-threshold` 设为 `512`。
2. **降低 CPU 占用**：`creatures.ticks-per-animal-spawn` 调到 `600` 以上，`creatures.ambient-limit` 设为 `5`，可显著减少动物与蝙蝠刷新开销。
3. **网络安全**：生产环境务必保持 `server.online-mode: true`，关闭 `server.prevent-proxy`（误伤率高，建议用反代 IP 黑名单代替）。
4. **大型服务器**：把 `extras.tps-display: true` 与 `advanced.plugin-profiling: true` 同时开启，用 `/timings` 找出卡顿插件。
5. **磁盘 IO**：`extras.auto-save-on-player-quit: true` 保留，但世界级自动保存建议改用插件做定时备份，避免每次退出都触发磁盘写入高峰。
6. **插件兼容性**：若使用大量旧 Bukkit 插件出现事件丢失，可临时开启 `advanced.poor-man-listener: true` 排查，但切勿长期开启（性能差）。
7. **首次开服**：先用 `world.type: FLAT` + 预生成地形做压力测试，跑通后再切回 `DEFAULT` 正式开服。

> 排查清单：玩家进不去 → 查 `server.port` 防火墙 / `server.online-mode`；卡顿 → 查 `view-distance` 与 `creatures.*-limit`；插件不生效 → 查 `folders.plugins` 路径与 `files.*` 文件名是否被改名。
