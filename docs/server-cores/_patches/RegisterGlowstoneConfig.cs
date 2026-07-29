// RegisterGlowstoneConfig.cs
// 注册 Glowstone 服务器配置项（config/glowstone/glowstone.yml）
// 对应手册：docs/server-cores/36-glowstone.md
// 配置项约 60 项，10 个分类（server / console / game / creatures / folders / files / advanced / extras / world / libraries）

private void RegisterGlowstoneConfig()
{
    const string file = "config/glowstone/glowstone.yml";

    // ===== server（服务器基础设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "server.name",
        ConfigFileName = file,
        DisplayName = "服务器名称",
        Description = "仅用于日志与部分插件识别，不影响客户端显示。",
        Category = "服务器基础设置",
        DefaultValue = "Glowstone Server",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.port",
        ConfigFileName = file,
        DisplayName = "服务器端口",
        Description = "客户端连接端口，0 表示随机端口。",
        Category = "服务器基础设置",
        DefaultValue = "25565",
        MinValue = 1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.ip",
        ConfigFileName = file,
        DisplayName = "监听 IP",
        Description = "留空监听所有网卡；填入具体 IP 仅监听该网卡。",
        Category = "服务器基础设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.max-players",
        ConfigFileName = file,
        DisplayName = "最大玩家数",
        Description = "同时在线上限，超出的玩家进入排队或被踢。",
        Category = "服务器基础设置",
        DefaultValue = "20",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.motd",
        ConfigFileName = file,
        DisplayName = "服务器描述",
        Description = "客户端服务器列表显示的文字，支持 § 颜色码。",
        Category = "服务器基础设置",
        DefaultValue = "A Glowstone Server",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.online-mode",
        ConfigFileName = file,
        DisplayName = "正版验证",
        Description = "true=只允许正版玩家；false=允许离线/盗版账号，注意皮肤与 UUID 会变。",
        Category = "服务器基础设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.white-list",
        ConfigFileName = file,
        DisplayName = "启用白名单",
        Description = "开启后只有 whitelist.json 中的玩家可进入。",
        Category = "服务器基础设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.log-file",
        ConfigFileName = file,
        DisplayName = "日志文件路径",
        Description = "主日志输出文件。",
        Category = "服务器基础设置",
        DefaultValue = "server.log",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.snooper-enabled",
        ConfigFileName = file,
        DisplayName = "启用信息收集",
        Description = "上报匿名数据到 Mojang，强烈建议保持 false。",
        Category = "服务器基础设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.prevent-proxy",
        ConfigFileName = file,
        DisplayName = "拒绝代理连接",
        Description = "启用后逐个反向解析玩家 IP 防止代理，可能误伤，建议关闭。",
        Category = "服务器基础设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.network-compression-threshold",
        ConfigFileName = file,
        DisplayName = "网络压缩阈值",
        Description = "数据包字节数大于该值才压缩；-1=禁用压缩；0=全部压缩。",
        Category = "服务器基础设置",
        DefaultValue = "256",
        MinValue = -1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.resource-pack",
        ConfigFileName = file,
        DisplayName = "资源包 URL",
        Description = "玩家进服时强制推送的资源包下载地址。",
        Category = "服务器基础设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.resource-pack-hash",
        ConfigFileName = file,
        DisplayName = "资源包哈希",
        Description = "资源包 SHA-1 哈希，用于校验完整性。",
        Category = "服务器基础设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.resource-pack-prompt",
        ConfigFileName = file,
        DisplayName = "资源包提示文本",
        Description = "推送资源包时弹窗显示的提示文字。",
        Category = "服务器基础设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.require-resource-pack",
        ConfigFileName = file,
        DisplayName = "强制资源包",
        Description = "true=拒绝加载资源包的玩家会被踢出。",
        Category = "服务器基础设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== console（控制台设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "console.history",
        ConfigFileName = file,
        DisplayName = "启用命令历史",
        Description = "控制台支持上下方向键翻阅历史命令。",
        Category = "控制台设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "console.prompts",
        ConfigFileName = file,
        DisplayName = "显示提示符",
        Description = "是否显示 > 提示符。",
        Category = "控制台设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "console.colors",
        ConfigFileName = file,
        DisplayName = "控制台彩色输出",
        Description = "日志按级别上色，Windows 旧 cmd 可能显示乱码。",
        Category = "控制台设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "console.date-format",
        ConfigFileName = file,
        DisplayName = "日期格式",
        Description = "日志时间戳格式，遵循 Java SimpleDateFormat 语法。",
        Category = "控制台设置",
        DefaultValue = "HH:mm:ss",
        ValueType = "string",
        RequiresRestart = false
    });

    // ===== game（游戏规则设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "game.gamemode",
        ConfigFileName = file,
        DisplayName = "默认游戏模式",
        Description = "新玩家首次进入的模式。",
        Category = "游戏规则设置",
        DefaultValue = "SURVIVAL",
        AllowedValues = new[] { "SURVIVAL", "CREATIVE", "ADVENTURE", "SPECTATOR" },
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.difficulty",
        ConfigFileName = file,
        DisplayName = "难度",
        Description = "PEACEFUL=和平；HARD=困难，影响刷怪与饥饿。",
        Category = "游戏规则设置",
        DefaultValue = "NORMAL",
        AllowedValues = new[] { "PEACEFUL", "EASY", "NORMAL", "HARD" },
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.hardcore",
        ConfigFileName = file,
        DisplayName = "极限模式",
        Description = "死亡后封禁该玩家，难度自动锁定 HARD。",
        Category = "游戏规则设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.pvp",
        ConfigFileName = file,
        DisplayName = "允许玩家 PvP",
        Description = "是否允许玩家间互相伤害。",
        Category = "游戏规则设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.max-build-height",
        ConfigFileName = file,
        DisplayName = "最大建筑高度",
        Description = "玩家可放置方块的最大 Y 坐标。",
        Category = "游戏规则设置",
        DefaultValue = "256",
        MinValue = 64,
        MaxValue = 256,
        ValueType = "int",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.allow-flight",
        ConfigFileName = file,
        DisplayName = "允许飞行",
        Description = "非创造模式是否允许飞行（防作弊检测）。",
        Category = "游戏规则设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.allow-nether",
        ConfigFileName = file,
        DisplayName = "启用下界",
        Description = "是否生成/加载下界维度。",
        Category = "游戏规则设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.allow-end",
        ConfigFileName = file,
        DisplayName = "启用末地",
        Description = "是否生成/加载末地维度。",
        Category = "游戏规则设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.announce-achievements",
        ConfigFileName = file,
        DisplayName = "公告成就",
        Description = "玩家获得成就时是否全服广播。",
        Category = "游戏规则设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.force-gamemode",
        ConfigFileName = file,
        DisplayName = "强制游戏模式",
        Description = "玩家每次进入都重置为默认模式，覆盖其上次模式。",
        Category = "游戏规则设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.spawn-protection",
        ConfigFileName = file,
        DisplayName = "出生点保护半径",
        Description = "出生点周围多少格内非 OP 无法破坏，0=关闭保护。",
        Category = "游戏规则设置",
        DefaultValue = "16",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.villager-trading",
        ConfigFileName = file,
        DisplayName = "允许村民交易",
        Description = "玩家是否可与村民交易。",
        Category = "游戏规则设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== creatures（生物生成设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.spawn-monsters",
        ConfigFileName = file,
        DisplayName = "生成怪物",
        Description = "是否生成敌对怪物。",
        Category = "生物生成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.spawn-animals",
        ConfigFileName = file,
        DisplayName = "生成动物",
        Description = "是否生成被动动物。",
        Category = "生物生成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.spawn-npcs",
        ConfigFileName = file,
        DisplayName = "生成 NPC",
        Description = "是否生成村民等 NPC。",
        Category = "生物生成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.monster-limit",
        ConfigFileName = file,
        DisplayName = "怪物上限",
        Description = "单个世界怪物实体数量上限。",
        Category = "生物生成设置",
        DefaultValue = "70",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.animal-limit",
        ConfigFileName = file,
        DisplayName = "动物上限",
        Description = "单个世界被动动物数量上限。",
        Category = "生物生成设置",
        DefaultValue = "15",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.water-animal-limit",
        ConfigFileName = file,
        DisplayName = "水生动物上限",
        Description = "单个世界水生动物数量上限。",
        Category = "生物生成设置",
        DefaultValue = "5",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.ambient-limit",
        ConfigFileName = file,
        DisplayName = "环境生物上限",
        Description = "蝙蝠等环境生物上限。",
        Category = "生物生成设置",
        DefaultValue = "15",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.ticks-per-monster-spawn",
        ConfigFileName = file,
        DisplayName = "怪物生成间隔",
        Description = "每多少 tick 尝试一次怪物生成（20 tick=1 秒）。",
        Category = "生物生成设置",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.ticks-per-animal-spawn",
        ConfigFileName = file,
        DisplayName = "动物生成间隔",
        Description = "每多少 tick 尝试一次动物生成。",
        Category = "生物生成设置",
        DefaultValue = "400",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    // ===== folders（目录设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "folders.settings",
        ConfigFileName = file,
        DisplayName = "配置目录",
        Description = "所有 YAML 配置所在目录。",
        Category = "目录设置",
        DefaultValue = "config",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "folders.plugins",
        ConfigFileName = file,
        DisplayName = "插件目录",
        Description = "Bukkit 插件 jar 放置目录。",
        Category = "目录设置",
        DefaultValue = "plugins",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "folders.worlds",
        ConfigFileName = file,
        DisplayName = "世界目录",
        Description = "世界存档数据目录。",
        Category = "目录设置",
        DefaultValue = "worlds",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "folders.cache",
        ConfigFileName = file,
        DisplayName = "缓存目录",
        Description = "运行时缓存（如皮肤）目录。",
        Category = "目录设置",
        DefaultValue = "cache",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "folders.updates",
        ConfigFileName = file,
        DisplayName = "更新目录",
        Description = "插件热更新目录，放入新 jar 重启后替换。",
        Category = "目录设置",
        DefaultValue = "update",
        ValueType = "string",
        RequiresRestart = true
    });

    // ===== files（文件设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "files.whitelist",
        ConfigFileName = file,
        DisplayName = "白名单文件",
        Description = "白名单文件名。",
        Category = "文件设置",
        DefaultValue = "whitelist.json",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "files.permissions",
        ConfigFileName = file,
        DisplayName = "权限文件",
        Description = "默认权限配置文件名。",
        Category = "文件设置",
        DefaultValue = "permissions.yml",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "files.commands",
        ConfigFileName = file,
        DisplayName = "命令文件",
        Description = "命令别名配置文件名。",
        Category = "文件设置",
        DefaultValue = "commands.yml",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "files.operators",
        ConfigFileName = file,
        DisplayName = "OP 文件",
        Description = "管理员列表文件名。",
        Category = "文件设置",
        DefaultValue = "ops.json",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "files.help",
        ConfigFileName = file,
        DisplayName = "帮助文件",
        Description = "帮助主题配置文件名。",
        Category = "文件设置",
        DefaultValue = "help.yml",
        ValueType = "string",
        RequiresRestart = false
    });

    // ===== advanced（高级设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.connection-throttle",
        ConfigFileName = file,
        DisplayName = "连接节流",
        Description = "同一玩家两次连接的最小间隔（毫秒），防刷屏。",
        Category = "高级设置",
        DefaultValue = "4000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.idle-timeout",
        ConfigFileName = file,
        DisplayName = "空闲超时",
        Description = "玩家无操作多少分钟后踢出，0=禁用。",
        Category = "高级设置",
        DefaultValue = "0",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.warn-on-overload",
        ConfigFileName = file,
        DisplayName = "过载警告",
        Description = "服务器 tick 超时时是否在控制台输出警告。",
        Category = "高级设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.exact-login-location",
        ConfigFileName = file,
        DisplayName = "精确登录位置",
        Description = "玩家上线时是否精确还原离线时位置。",
        Category = "高级设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.plugin-profiling",
        ConfigFileName = file,
        DisplayName = "插件性能分析",
        Description = "启用 /timings 命令分析插件性能。",
        Category = "高级设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.use-alternative-logger",
        ConfigFileName = file,
        DisplayName = "备用日志器",
        Description = "使用 JUL 替代默认日志框架，调试用。",
        Category = "高级设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.poor-man-listener",
        ConfigFileName = file,
        DisplayName = "简易事件监听",
        Description = "兼容旧版插件的低性能事件分发，谨慎开启。",
        Category = "高级设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== extras（额外特性设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "extras.tps-display",
        ConfigFileName = file,
        DisplayName = "显示 TPS",
        Description = "在控制台定时输出当前 TPS。",
        Category = "额外特性设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.kick-on-illegal-behavior",
        ConfigFileName = file,
        DisplayName = "非法行为踢出",
        Description = "检测到客户端非法数据包时直接踢出。",
        Category = "额外特性设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.auto-save-on-player-quit",
        ConfigFileName = file,
        DisplayName = "退出自动保存",
        Description = "玩家退出时立即保存其数据。",
        Category = "额外特性设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.deploy-on-restart",
        ConfigFileName = file,
        DisplayName = "重启自动部署",
        Description = "重启时自动从 update 目录部署新插件。",
        Category = "额外特性设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== world（世界生成设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "world.name",
        ConfigFileName = file,
        DisplayName = "主世界名称",
        Description = "主世界存档文件夹名。",
        Category = "世界生成设置",
        DefaultValue = "world",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.seed",
        ConfigFileName = file,
        DisplayName = "世界种子",
        Description = "留空随机生成；填入固定种子可复现世界。",
        Category = "世界生成设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.type",
        ConfigFileName = file,
        DisplayName = "世界类型",
        Description = "地形生成器类型。",
        Category = "世界生成设置",
        DefaultValue = "DEFAULT",
        AllowedValues = new[] { "DEFAULT", "FLAT", "LARGEBIOMES", "AMPLIFIED" },
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.generator-settings",
        ConfigFileName = file,
        DisplayName = "生成器参数",
        Description = "自定义生成参数，例如超平坦层结构 JSON。",
        Category = "世界生成设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.generate-structures",
        ConfigFileName = file,
        DisplayName = "生成结构",
        Description = "是否生成村庄、神殿等结构。",
        Category = "世界生成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.view-distance",
        ConfigFileName = file,
        DisplayName = "视野距离",
        Description = "玩家周围加载区块半径，每 +1 增加约 15% 带宽消耗。",
        Category = "世界生成设置",
        DefaultValue = "10",
        MinValue = 3,
        MaxValue = 15,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.keep-spawn-loaded",
        ConfigFileName = file,
        DisplayName = "保持出生加载",
        Description = "出生点区块常驻内存。",
        Category = "世界生成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== libraries（依赖库设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "libraries.check-library-updates",
        ConfigFileName = file,
        DisplayName = "检查库更新",
        Description = "启动时检查依赖库是否有新版本。",
        Category = "依赖库设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "libraries.use-library-repo",
        ConfigFileName = file,
        DisplayName = "使用库仓库",
        Description = "从远程仓库下载缺失依赖，关闭则需手动放置 jar。",
        Category = "依赖库设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
}
