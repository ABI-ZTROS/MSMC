// RegisterSpongeGlobalConf.cs
// 注册 Sponge 全局配置项（config/sponge/global.conf，HOCON 格式）
// 对应手册：docs/server-cores/32-sponge.md
// 配置项约 90 项，25 个子节

private void RegisterSpongeGlobalConf()
{
    const string file = "config/sponge/global.conf";

    // ===== 全局根设置 =====
    Register(new ServerConfigDescriptor
    {
        Key = "sponge.target-server-ip",
        ConfigFileName = file,
        DisplayName = "目标服务器 IP",
        Description = "仅 SpongeForge/SpongeVanilla 嵌入式部署时使用。",
        Category = "全局根设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sponge.target-server-port",
        ConfigFileName = file,
        DisplayName = "目标服务器端口",
        Description = "嵌入式部署端口。",
        Category = "全局根设置",
        DefaultValue = "25565",
        MinValue = 1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sponge.plugins-dir",
        ConfigFileName = file,
        DisplayName = "插件目录",
        Description = "Sponge 插件搜索目录，可自定义。",
        Category = "全局根设置",
        DefaultValue = "mods/plugins",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sponge.enable-plugins",
        ConfigFileName = file,
        DisplayName = "启用插件加载",
        Description = "false=不加载任何 Sponge 插件。",
        Category = "全局根设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sponge.file-watch-enabled",
        ConfigFileName = file,
        DisplayName = "文件监视",
        Description = "监视配置文件变化以支持热重载。",
        Category = "全局根设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== modules（功能模块开关） =====
    Register(new ServerConfigDescriptor
    {
        Key = "modules.block-capturing-control",
        ConfigFileName = file,
        DisplayName = "方块捕获控制",
        Description = "是否启用方块变更追踪（事务），插件 BlockEvent 依赖此。",
        Category = "功能模块",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.bungeecord",
        ConfigFileName = file,
        DisplayName = "BungeeCord 兼容",
        Description = "启用 IP 转发以兼容 BungeeCord/Velocity 代理。",
        Category = "功能模块",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.entity-activation-range",
        ConfigFileName = file,
        DisplayName = "实体活动范围优化",
        Description = "启用按距离降频实体 tick 的优化。",
        Category = "功能模块",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.entity-collisions",
        ConfigFileName = file,
        DisplayName = "实体碰撞优化",
        Description = "启用碰撞频率限制。",
        Category = "功能模块",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.exploits",
        ConfigFileName = file,
        DisplayName = "漏洞修复",
        Description = "修复若干原版漏洞（如附魔/书与笔）。",
        Category = "功能模块",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.game-fixes",
        ConfigFileName = file,
        DisplayName = "游戏修复",
        Description = "一些非紧急的游戏性 bug 修复，默认关闭以保原版行为。",
        Category = "功能模块",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.optimizations",
        ConfigFileName = file,
        DisplayName = "性能优化",
        Description = "总开关，关闭后下属所有优化失效。",
        Category = "功能模块",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.realtime",
        ConfigFileName = file,
        DisplayName = "实时时钟",
        Description = "用现实时间替代 tick，改善低 TPS 下玩家体验，不提升性能。",
        Category = "功能模块",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.tileentity-activation",
        ConfigFileName = file,
        DisplayName = "方块实体活动范围",
        Description = "按距离降频方块实体 tick，谨慎启用可能破坏模组功能。",
        Category = "功能模块",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.timings",
        ConfigFileName = file,
        DisplayName = "性能计时",
        Description = "启用 /sponge timings 性能分析。",
        Category = "功能模块",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "modules.tracking",
        ConfigFileName = file,
        DisplayName = "来源追踪",
        Description = "追踪方块/实体变更的因果来源，权限审计依赖此。",
        Category = "功能模块",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== optimizations（性能优化） =====
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.async-lighting.enabled",
        ConfigFileName = file,
        DisplayName = "异步光照计算",
        Description = "异步线程计算光照，显著降低主线程负担。",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.async-lighting.num-threads",
        ConfigFileName = file,
        DisplayName = "光照线程数",
        Description = "异步光照专用线程数，CPU 核心数较佳。",
        Category = "性能优化",
        DefaultValue = "2",
        MinValue = 1,
        MaxValue = 64,
        ValueType = "int",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.cache-tameable-owners",
        ConfigFileName = file,
        DisplayName = "缓存可驯服主",
        Description = "缓存驯化动物主人 UUID，避免频繁 DataWatcher 查询。",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.drops-pre-merge",
        ConfigFileName = file,
        DisplayName = "掉落物预合并",
        Description = "生成掉落物前先尝试合并，减少实体数量。",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.panda-redstone",
        ConfigFileName = file,
        DisplayName = "Panda 红石算法",
        Description = "替代红石更新算法，减少方块更新次数，可能引入差异。",
        Category = "性能优化",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.chunk-loading",
        ConfigFileName = file,
        DisplayName = "区块加载优化",
        Description = "优化区块加载与排队。",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.eject-from-entity",
        ConfigFileName = file,
        DisplayName = "实体弹出优化",
        Description = "优化矿车/船等载具的弹出逻辑。",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.structured-unused-entries",
        ConfigFileName = file,
        DisplayName = "清理未用条目",
        Description = "清理内部未使用的结构条目。",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.use-partial-block-updates",
        ConfigFileName = file,
        DisplayName = "部分方块更新",
        Description = "仅更新变化部分方块而非整体。",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.vertex-operation-lighting",
        ConfigFileName = file,
        DisplayName = "顶点光照优化",
        Description = "实验性顶点级光照优化。",
        Category = "性能优化",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== block-entity-activation（方块实体活动范围） =====
    Register(new ServerConfigDescriptor
    {
        Key = "block-entity-activation.auto-populate",
        ConfigFileName = file,
        DisplayName = "自动填充",
        Description = "自动把新发现的方块实体加入配置，建议调优后关闭。",
        Category = "方块实体活动范围",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "block-entity-activation.default-block-range",
        ConfigFileName = file,
        DisplayName = "默认方块范围",
        Description = "玩家在此范围内方块实体才 tick。",
        Category = "方块实体活动范围",
        DefaultValue = "256",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "block-entity-activation.default-tick-rate",
        ConfigFileName = file,
        DisplayName = "默认 tick 频率",
        Description = "每多少 tick 给方块实体 1 次 tick，值越大越省 CPU。",
        Category = "方块实体活动范围",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    // ===== entity-activation-range（实体活动范围） =====
    Register(new ServerConfigDescriptor
    {
        Key = "entity-activation-range.auto-populate",
        ConfigFileName = file,
        DisplayName = "自动填充",
        Description = "自动把新发现的实体加入配置。",
        Category = "实体活动范围",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-activation-range.defaults.ambient",
        ConfigFileName = file,
        DisplayName = "环境生物范围",
        Description = "蝙蝠等环境生物激活距离，0=禁用。",
        Category = "实体活动范围",
        DefaultValue = "32",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-activation-range.defaults.aquatic",
        ConfigFileName = file,
        DisplayName = "水生生物范围",
        Description = "鱿鱼等水生生物激活距离。",
        Category = "实体活动范围",
        DefaultValue = "32",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-activation-range.defaults.creature",
        ConfigFileName = file,
        DisplayName = "被动动物范围",
        Description = "牛、羊等被动动物激活距离。",
        Category = "实体活动范围",
        DefaultValue = "32",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-activation-range.defaults.misc",
        ConfigFileName = file,
        DisplayName = "杂项实体范围",
        Description = "掉落物、经验球等杂项实体激活距离。",
        Category = "实体活动范围",
        DefaultValue = "16",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-activation-range.defaults.monster",
        ConfigFileName = file,
        DisplayName = "怪物范围",
        Description = "僵尸、骷髅等怪物激活距离。",
        Category = "实体活动范围",
        DefaultValue = "32",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    // ===== entity-collision（实体碰撞） =====
    Register(new ServerConfigDescriptor
    {
        Key = "entity-collision.auto-populate",
        ConfigFileName = file,
        DisplayName = "自动填充",
        Description = "自动把新发现的实体加入碰撞配置。",
        Category = "实体碰撞",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-collision.defaults.ambient",
        ConfigFileName = file,
        DisplayName = "环境生物碰撞上限",
        Description = "单点同时碰撞的环境生物上限。",
        Category = "实体碰撞",
        DefaultValue = "8",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-collision.defaults.aquatic",
        ConfigFileName = file,
        DisplayName = "水生生物碰撞上限",
        Description = "水生生物碰撞上限。",
        Category = "实体碰撞",
        DefaultValue = "8",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-collision.defaults.creature",
        ConfigFileName = file,
        DisplayName = "被动动物碰撞上限",
        Description = "被动动物碰撞上限。",
        Category = "实体碰撞",
        DefaultValue = "8",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-collision.defaults.misc",
        ConfigFileName = file,
        DisplayName = "杂项实体碰撞上限",
        Description = "杂项实体碰撞上限。",
        Category = "实体碰撞",
        DefaultValue = "8",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity-collision.defaults.monster",
        ConfigFileName = file,
        DisplayName = "怪物碰撞上限",
        Description = "怪物碰撞上限，调小可减少密集卡顿。",
        Category = "实体碰撞",
        DefaultValue = "8",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    // ===== entity（实体行为） =====
    Register(new ServerConfigDescriptor
    {
        Key = "entity.creature-spawn-limit",
        ConfigFileName = file,
        DisplayName = "怪物生成上限",
        Description = "0=沿用原版；正值覆盖原版上限。",
        Category = "实体行为",
        DefaultValue = "0",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity.human-player-list-allow-bypass-on-max-players",
        ConfigFileName = file,
        DisplayName = "玩家列表绕过",
        Description = "BungeeCord 转发时绕过原版 60 上限。",
        Category = "实体行为",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity.max-bounding-box-size",
        ConfigFileName = file,
        DisplayName = "最大包围盒尺寸",
        Description = "实体最大碰撞箱尺寸，过大实体被裁剪，防崩。",
        Category = "实体行为",
        DefaultValue = "2000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity.max-entity-velocity",
        ConfigFileName = file,
        DisplayName = "最大实体速度",
        Description = "实体最大速度上限，防止作弊者用速度卡服。",
        Category = "实体行为",
        DefaultValue = "100.0",
        MinValue = 0,
        ValueType = "double",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity.player-block-reach",
        ConfigFileName = file,
        DisplayName = "玩家方块触达距离",
        Description = "玩家可破坏/交互方块的最远距离。",
        Category = "实体行为",
        DefaultValue = "5.0",
        MinValue = 0,
        ValueType = "double",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity.player-entity-reach",
        ConfigFileName = file,
        DisplayName = "玩家实体触达距离",
        Description = "玩家可攻击/交互实体的最远距离。",
        Category = "实体行为",
        DefaultValue = "5.0",
        MinValue = 0,
        ValueType = "double",
        RequiresRestart = false
    });

    // ===== movement-checks（移动检查） =====
    Register(new ServerConfigDescriptor
    {
        Key = "movement-checks.auto-orientation",
        ConfigFileName = file,
        DisplayName = "自动朝向检查",
        Description = "检测玩家朝向突变（如反作弊）。",
        Category = "移动检查",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "movement-checks.invalid-rotation",
        ConfigFileName = file,
        DisplayName = "非法旋转检查",
        Description = "检查旋转角度是否超出合法范围。",
        Category = "移动检查",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "movement-checks.moved-wrongly",
        ConfigFileName = file,
        DisplayName = "异常移动检查",
        Description = "检查玩家移动距离是否异常。",
        Category = "移动检查",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "movement-checks.moved-too-quickly",
        ConfigFileName = file,
        DisplayName = "快速移动检查",
        Description = "检查玩家移动速度是否过快。",
        Category = "移动检查",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "movement-checks.speed-hack",
        ConfigFileName = file,
        DisplayName = "速度作弊检查",
        Description = "检测加速挂。",
        Category = "移动检查",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== commands（命令设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "commands.multi-world-commands",
        ConfigFileName = file,
        DisplayName = "多世界命令",
        Description = "是否按世界隔离命令权限。",
        Category = "命令设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "commands.notifications.command",
        ConfigFileName = file,
        DisplayName = "命令通知命令名",
        Description = "/sponge 主命令名。",
        Category = "命令设置",
        DefaultValue = "sponge",
        ValueType = "string",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "commands.show-name",
        ConfigFileName = file,
        DisplayName = "显示命令名",
        Description = "帮助列表中是否显示命令名。",
        Category = "命令设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== world（世界设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "world.auto-save-interval",
        ConfigFileName = file,
        DisplayName = "世界自动保存间隔",
        Description = "每多少 tick 保存所有区块，0=禁用，20 tick=1 秒。",
        Category = "世界设置",
        DefaultValue = "900",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.auto-player-save-interval",
        ConfigFileName = file,
        DisplayName = "玩家数据保存间隔",
        Description = "每多少 tick 保存全局玩家数据，0=禁用。",
        Category = "世界设置",
        DefaultValue = "900",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.game-disable-updates",
        ConfigFileName = file,
        DisplayName = "禁用游戏更新",
        Description = "调试用，禁用游戏内部更新。",
        Category = "世界设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.gen-modifiers",
        ConfigFileName = file,
        DisplayName = "生成器修饰符",
        Description = "自定义世界生成修饰符列表。",
        Category = "世界设置",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.load-on-startup",
        ConfigFileName = file,
        DisplayName = "启动时加载",
        Description = "服务端启动时是否预加载所有世界。",
        Category = "世界设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== bungeecord（BungeeCord 代理） =====
    Register(new ServerConfigDescriptor
    {
        Key = "bungeecord.ip-forwarding",
        ConfigFileName = file,
        DisplayName = "IP 转发",
        Description = "启用 BungeeCord/Velocity IP 转发，必须与代理端一致。",
        Category = "BungeeCord 代理",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "bungeecord.online-mode",
        ConfigFileName = file,
        DisplayName = "在线模式",
        Description = "代理模式下是否做正版验证。",
        Category = "BungeeCord 代理",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== permissions（权限设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "permissions.load-on-startup",
        ConfigFileName = file,
        DisplayName = "启动加载权限",
        Description = "启动时加载权限服务。",
        Category = "权限设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "permissions.use-default-permissions",
        ConfigFileName = file,
        DisplayName = "使用默认权限",
        Description = "是否使用 Sponge 内置默认权限。",
        Category = "权限设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "permissions.default-admin-level",
        ConfigFileName = file,
        DisplayName = "默认管理员等级",
        Description = "默认权限等级（4=OP）。",
        Category = "权限设置",
        DefaultValue = "4",
        MinValue = 0,
        MaxValue = 4,
        ValueType = "int",
        RequiresRestart = false
    });

    // ===== sql（SQL 数据库） =====
    Register(new ServerConfigDescriptor
    {
        Key = "sql.enabled",
        ConfigFileName = file,
        DisplayName = "启用 SQL",
        Description = "启用 SQL 数据源。",
        Category = "SQL 数据库",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sql.driver",
        ConfigFileName = file,
        DisplayName = "数据库驱动",
        Description = "JDBC 驱动类全名。",
        Category = "SQL 数据库",
        DefaultValue = "org.h2.Driver",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sql.url",
        ConfigFileName = file,
        DisplayName = "数据库 URL",
        Description = "JDBC 连接 URL。",
        Category = "SQL 数据库",
        DefaultValue = "jdbc:h2:./config/sponge/sponge",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sql.user",
        ConfigFileName = file,
        DisplayName = "数据库用户名",
        Description = "数据库账号。",
        Category = "SQL 数据库",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sql.password",
        ConfigFileName = file,
        DisplayName = "数据库密码",
        Description = "数据库密码，建议用环境变量替代。",
        Category = "SQL 数据库",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "sql.table-prefix",
        ConfigFileName = file,
        DisplayName = "表前缀",
        Description = "数据表名前缀。",
        Category = "SQL 数据库",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });

    // ===== scheduler（调度器） =====
    Register(new ServerConfigDescriptor
    {
        Key = "scheduler.parallel-limit",
        ConfigFileName = file,
        DisplayName = "并发任务上限",
        Description = "异步任务并发上限。",
        Category = "调度器",
        DefaultValue = "8",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "scheduler.max-thread-size",
        ConfigFileName = file,
        DisplayName = "最大线程数",
        Description = "调度线程池最大线程数。",
        Category = "调度器",
        DefaultValue = "4",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    // ===== logging（日志设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "logging.log-block-break",
        ConfigFileName = file,
        DisplayName = "记录方块破坏",
        Description = "控制台输出方块破坏事件。",
        Category = "日志设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "logging.log-block-place",
        ConfigFileName = file,
        DisplayName = "记录方块放置",
        Description = "控制台输出方块放置事件。",
        Category = "日志设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "logging.log-stacktraces",
        ConfigFileName = file,
        DisplayName = "记录堆栈",
        Description = "输出异常堆栈用于调试。",
        Category = "日志设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "logging.debug",
        ConfigFileName = file,
        DisplayName = "调试日志",
        Description = "启用指定调试分类（如 [chunk-load]）。",
        Category = "日志设置",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = false
    });

    // ===== exploits（漏洞修复） =====
    Register(new ServerConfigDescriptor
    {
        Key = "exploits.book-large-size",
        ConfigFileName = file,
        DisplayName = "书本大小限制",
        Description = "限制书本内容大小，防崩服。",
        Category = "漏洞修复",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "exploits.item-signature",
        ConfigFileName = file,
        DisplayName = "物品签名检查",
        Description = "检查物品 NBT 签名是否合法。",
        Category = "漏洞修复",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "exploits.sign-command",
        ConfigFileName = file,
        DisplayName = "告示牌命令限制",
        Description = "限制告示牌可执行的命令。",
        Category = "漏洞修复",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "exploits.sign-long-lines",
        ConfigFileName = file,
        DisplayName = "告示牌长行限制",
        Description = "限制告示牌每行字符数。",
        Category = "漏洞修复",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== general（通用设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "general.disable-warnings",
        ConfigFileName = file,
        DisplayName = "禁用警告",
        Description = "关闭控制台部分警告。",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "general.hide-online-players",
        ConfigFileName = file,
        DisplayName = "隐藏在线玩家",
        Description = "不向客户端发送完整玩家列表。",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "general.disable-flush-saving",
        ConfigFileName = file,
        DisplayName = "禁用刷盘保存",
        Description = "关闭定时全量刷盘，仅增量保存。",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "general.death-message-style",
        ConfigFileName = file,
        DisplayName = "死亡消息风格",
        Description = "死亡消息显示风格。",
        Category = "通用设置",
        DefaultValue = "default",
        AllowedValues = new[] { "default", "none", "raw" },
        ValueType = "string",
        RequiresRestart = false
    });

    // ===== debug（调试设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "debug.thread-contention-monitoring",
        ConfigFileName = file,
        DisplayName = "线程竞争监视",
        Description = "启用线程竞争检测。",
        Category = "调试设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "debug.reload-internal",
        ConfigFileName = file,
        DisplayName = "内部重载",
        Description = "允许 /sponge reload 重载内部状态。",
        Category = "调试设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "debug.synchronize-chunk-writes",
        ConfigFileName = file,
        DisplayName = "同步区块写入",
        Description = "区块写入是否同步。",
        Category = "调试设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== timings（性能计时） =====
    Register(new ServerConfigDescriptor
    {
        Key = "timings.enabled",
        ConfigFileName = file,
        DisplayName = "启用 timings",
        Description = "启用 /sponge timings。",
        Category = "性能计时",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "timings.verbose",
        ConfigFileName = file,
        DisplayName = "详细模式",
        Description = "输出更详细的计时数据。",
        Category = "性能计时",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "timings.cost-ignored",
        ConfigFileName = file,
        DisplayName = "忽略成本",
        Description = "忽略微小成本计时。",
        Category = "性能计时",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "timings.history-interval",
        ConfigFileName = file,
        DisplayName = "历史间隔",
        Description = "多少秒采样一次历史。",
        Category = "性能计时",
        DefaultValue = "300",
        MinValue = 10,
        MaxValue = 3600,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "timings.history-length",
        ConfigFileName = file,
        DisplayName = "历史长度",
        Description = "历史总时长（秒）。",
        Category = "性能计时",
        DefaultValue = "3600",
        MinValue = 60,
        MaxValue = 21600,
        ValueType = "int",
        RequiresRestart = false
    });

    // ===== cause-tracker（因果追踪） =====
    Register(new ServerConfigDescriptor
    {
        Key = "cause-tracker.max-block-processed-per-tick",
        ConfigFileName = file,
        DisplayName = "每 tick 最大处理方块",
        Description = "每 tick 处理的方块事件上限。",
        Category = "因果追踪",
        DefaultValue = "50000",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "cause-tracker.max-block-processed-per-event",
        ConfigFileName = file,
        DisplayName = "每事件最大方块",
        Description = "单个事件处理方块上限。",
        Category = "因果追踪",
        DefaultValue = "50000",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "cause-tracker.report-modified-blocks",
        ConfigFileName = file,
        DisplayName = "报告修改方块",
        Description = "输出修改方块报告。",
        Category = "因果追踪",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
}
