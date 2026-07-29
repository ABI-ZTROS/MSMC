// -----------------------------------------------------------------------------
// 文件名: RegisterPowerNukkitYml.cs
// 功能描述: 注册 PowerNukkitX（基岩版）配置文件的描述符
//           包含 powernukkit.yml 与基岩版 server.properties（用 powernukkit-server.properties 区分）
// 数据来源: PowerNukkitX/PowerNukkitX src/main/java/org/powernukkitx/config/* (master 分支)
// 适用版本: PowerNukkitX 3.0.0（master 分支）
// -----------------------------------------------------------------------------

private void RegisterPowerNukkitYml()
{
    const string file = "powernukkit.yml";

    // ==================== settings（基础设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "settings.ip",
        ConfigFileName = file,
        DisplayName = "服务器监听 IP",
        Description = "服务器绑定的 IPv4 地址\n0.0.0.0 表示监听所有网卡；多网卡环境下可指定具体 IP",
        Category = "基础设置",
        DefaultValue = "0.0.0.0",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.port",
        ConfigFileName = file,
        DisplayName = "服务器端口（UDP）",
        Description = "服务器监听的 UDP 端口\n⚠️ 基岩版使用 UDP，路由器端口转发必须选 UDP 协议\n基岩版默认 19132（Java 版是 25565/TCP）",
        Category = "基础设置",
        DefaultValue = "19132",
        MinValue = 1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.maxplayers",
        ConfigFileName = file,
        DisplayName = "最大玩家数",
        Description = "服务器同时允许的最大玩家数",
        Category = "基础设置",
        DefaultValue = "20",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.defaultlevel",
        ConfigFileName = file,
        DisplayName = "默认世界名",
        Description = "玩家首次进服默认进入的世界名称",
        Category = "基础设置",
        DefaultValue = "world",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.allowlist",
        ConfigFileName = file,
        DisplayName = "启用白名单",
        Description = "是否启用白名单\n启用后仅 allowlist.json 中的玩家可加入",
        Category = "基础设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.allowlist.message",
        ConfigFileName = file,
        DisplayName = "白名单拒绝消息",
        Description = "玩家被白名单拒绝时显示的提示文本",
        Category = "基础设置",
        DefaultValue = "Server is white-listed",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.motd",
        ConfigFileName = file,
        DisplayName = "服务器 MOTD",
        Description = "服务器在客户端服务器列表中显示的名称\n可使用 § 颜色码",
        Category = "基础设置",
        DefaultValue = "PowerNukkitX Server",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.sub-motd",
        ConfigFileName = file,
        DisplayName = "子 MOTD",
        Description = "服务器副标题，部分客户端在 MOTD 下方显示",
        Category = "基础设置",
        DefaultValue = "powernukkitx.org",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.language",
        ConfigFileName = file,
        DisplayName = "服务器语言",
        Description = "控制台与提示消息使用的语言代码\neng 英语 / chs 简中 / cht 繁中 / jpn 日语 / rus 俄语 / spa 西语 / pol 波兰语 / bra 葡语 / kor 韩语 / ukr 乌克语 / deu 德语 / ltu 立陶宛语 / idn 印尼语 / cze 捷克语 / tur 土耳其语 / fin 芬兰语",
        Category = "基础设置",
        DefaultValue = "eng",
        AllowedValues = ["eng", "chs", "cht", "jpn", "rus", "spa", "pol", "bra", "kor", "ukr", "deu", "ltu", "idn", "cze", "tur", "fin"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.forcetranslate",
        ConfigFileName = file,
        DisplayName = "强制使用服务器语言",
        Description = "true 时所有字符串按服务器语言翻译后发送给客户端\nfalse 时让客户端自行处理本地化（推荐）",
        Category = "基础设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.safespawn",
        ConfigFileName = file,
        DisplayName = "安全出生",
        Description = "是否在玩家首次进服时寻找安全位置出生\n防止卡在方块中",
        Category = "基础设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.autosave",
        ConfigFileName = file,
        DisplayName = "自动保存",
        Description = "是否启用自动保存（间隔由 autosaveDelay 控制）",
        Category = "基础设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.autosaveDelay",
        ConfigFileName = file,
        DisplayName = "自动保存间隔",
        Description = "自动保存的间隔（tick）\n6000 = 每 5 分钟保存一次（20 tick = 1 秒）\n0 = 禁用自动保存（不推荐）",
        Category = "基础设置",
        DefaultValue = "6000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.saveunknownblock",
        ConfigFileName = file,
        DisplayName = "保存未知方块",
        Description = "是否在 NBT 中保存 PNX 无法识别的方块\n用于行为包扩展兼容",
        Category = "基础设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.xboxauth",
        ConfigFileName = file,
        DisplayName = "Xbox Live 验证",
        Description = "是否要求所有玩家通过 Xbox Live 认证\n公网服务器强烈建议开启，关闭会导致玩家可伪装身份\n远程（非 LAN）连接无论此设置如何，始终需要 Xbox Live 认证",
        Category = "基础设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== player-settings（玩家设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "player-settings.saveplayerdata",
        ConfigFileName = file,
        DisplayName = "保存玩家数据",
        Description = "true 时玩家数据保存为 players/<UUID>.dat",
        Category = "玩家",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-settings.skinchangecooldown",
        ConfigFileName = file,
        DisplayName = "皮肤更换冷却",
        Description = "玩家两次更换皮肤之间的冷却时间（秒）\n0 = 无冷却\n防止玩家通过频繁换皮肤刷屏或攻击服务器",
        Category = "玩家",
        DefaultValue = "30",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-settings.forceskintrusted",
        ConfigFileName = file,
        DisplayName = "强制可信皮肤",
        Description = "true 时仅使用可信（Xbox Live）的皮肤",
        Category = "玩家",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-settings.checkmovement",
        ConfigFileName = file,
        DisplayName = "校验玩家移动",
        Description = "是否启用服务器端玩家移动校验（反作弊）",
        Category = "玩家",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-settings.rotationupdatethreshold",
        ConfigFileName = file,
        DisplayName = "旋转更新阈值",
        Description = "玩家旋转角度变化超过此值才发送更新\n降低网络包频率",
        Category = "玩家",
        DefaultValue = "1",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-settings.movementdistancethreshold",
        ConfigFileName = file,
        DisplayName = "移动距离阈值",
        Description = "玩家位移超过此值才发送位置更新",
        Category = "玩家",
        DefaultValue = "0.1",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-settings.spawnRadius",
        ConfigFileName = file,
        DisplayName = "出生保护半径",
        Description = "出生点周围此半径内的方块受到保护\n非 OP 玩家无法破坏",
        Category = "玩家",
        DefaultValue = "16",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== gameplay-settings（游戏玩法设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enablecommandblocks",
        ConfigFileName = file,
        DisplayName = "启用命令方块",
        Description = "是否允许使用命令方块",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.allowbeta",
        ConfigFileName = file,
        DisplayName = "允许 Beta 客户端",
        Description = "是否允许 Beta 版本客户端连接",
        Category = "游戏玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableredstone",
        ConfigFileName = file,
        DisplayName = "启用红石",
        Description = "是否启用红石系统",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.tickRedstone",
        ConfigFileName = file,
        DisplayName = "红石每 tick 处理",
        Description = "是否每 tick 都处理红石信号\n关闭后红石仍工作但更新频率降低",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.viewDistance",
        ConfigFileName = file,
        DisplayName = "视野距离",
        Description = "玩家可见的区块半径\n值越大带宽和内存占用越高\n公网服建议 8-12",
        Category = "游戏玩法",
        DefaultValue = "8",
        MinValue = 5,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.achivements",
        ConfigFileName = file,
        DisplayName = "启用成就",
        Description = "是否启用成就/进度系统",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.announceAchievements",
        ConfigFileName = file,
        DisplayName = "广播成就",
        Description = "玩家解锁成就时是否在聊天栏广播",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.spawnProtection",
        ConfigFileName = file,
        DisplayName = "出生保护半径",
        Description = "出生点保护半径（方块）\n非 OP 玩家无法在此范围内破坏",
        Category = "游戏玩法",
        DefaultValue = "16",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.gamemode",
        ConfigFileName = file,
        DisplayName = "默认游戏模式",
        Description = "新玩家默认游戏模式\n0 = 生存 / 1 = 创造 / 2 = 冒险\n⚠️ 基岩版无 spectator 选项！",
        Category = "游戏玩法",
        DefaultValue = "0",
        AllowedValues = ["0", "1", "2"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.forceGamemode",
        ConfigFileName = file,
        DisplayName = "强制游戏模式",
        Description = "true 时玩家进服始终被强制设置为 gamemode 指定的模式",
        Category = "游戏玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.hardcore",
        ConfigFileName = file,
        DisplayName = "极限模式",
        Description = "是否启用极限模式（玩家死亡后封禁）",
        Category = "游戏玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.pvp",
        ConfigFileName = file,
        DisplayName = "启用 PvP",
        Description = "是否允许玩家间伤害",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.difficulty",
        ConfigFileName = file,
        DisplayName = "难度",
        Description = "世界难度\n0 = 和平（不刷怪）/ 1 = 简单 / 2 = 普通 / 3 = 困难（僵尸破门等）",
        Category = "游戏玩法",
        DefaultValue = "1",
        AllowedValues = ["0", "1", "2", "3"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.allowNether",
        ConfigFileName = file,
        DisplayName = "启用下界",
        Description = "是否加载下界维度",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.allowEnd",
        ConfigFileName = file,
        DisplayName = "启用末地",
        Description = "是否加载末地维度",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.forceResources",
        ConfigFileName = file,
        DisplayName = "强制资源包",
        Description = "true 时玩家必须接受服务器资源包才能进服\n拒绝资源包的玩家会被踢出",
        Category = "游戏玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.allowClientPacks",
        ConfigFileName = file,
        DisplayName = "允许客户端资源包",
        Description = "是否允许玩家使用客户端自带资源包",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.allowVibrantVisuals",
        ConfigFileName = file,
        DisplayName = "允许 Vibrant Visuals",
        Description = "是否允许客户端使用「鲜明视觉」图形选项",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.experiments",
        ConfigFileName = file,
        DisplayName = "实验特性",
        Description = "启用的实验性特性 ID 列表\n如 data_driven_vanilla_blocks_and_items、experimental_molang_features 等",
        Category = "游戏玩法",
        DefaultValue = "data_driven_vanilla_blocks_and_items",
        ValueType = "list",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.cacheStructures",
        ConfigFileName = file,
        DisplayName = "缓存结构",
        Description = "是否缓存世界生成结构以加速加载（占用内存）",
        Category = "游戏玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableEdu",
        ConfigFileName = file,
        DisplayName = "教育版特性",
        Description = "是否启用 Minecraft 教育版特性（化学、NPC 等）",
        Category = "游戏玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.muteEmoteAnnouncements",
        ConfigFileName = file,
        DisplayName = "静默表情广播",
        Description = "是否屏蔽玩家使用表情时的聊天栏广播",
        Category = "游戏玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enablemobai",
        ConfigFileName = file,
        DisplayName = "启用生物 AI",
        Description = "是否启用实体 AI（寻路、行为）",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableRecipes",
        ConfigFileName = file,
        DisplayName = "启用配方",
        Description = "是否启用合成配方解锁",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableCreativeInventory",
        ConfigFileName = file,
        DisplayName = "启用创造物品栏",
        Description = "是否启用创造模式物品栏",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableDaylightCycle",
        ConfigFileName = file,
        DisplayName = "启用日夜循环",
        Description = "是否启用日夜循环",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableWeather",
        ConfigFileName = file,
        DisplayName = "启用天气",
        Description = "是否启用天气变化（雨、雷暴）",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableEntitySpawning",
        ConfigFileName = file,
        DisplayName = "启用实体生成",
        Description = "是否允许自然生成实体（怪物、动物）",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableBlockRandomTicking",
        ConfigFileName = file,
        DisplayName = "启用方块随机 tick",
        Description = "是否启用方块随机 tick（作物生长、草地蔓延等）",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableLiquidFlow",
        ConfigFileName = file,
        DisplayName = "启用液体流动",
        Description = "是否启用液体（水、熔岩）流动",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableItemDrops",
        ConfigFileName = file,
        DisplayName = "启用物品掉落",
        Description = "是否启用方块破坏后的物品掉落",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableXpOrbs",
        ConfigFileName = file,
        DisplayName = "启用经验球",
        Description = "是否启用经验球实体",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableExplosionBlockDamage",
        ConfigFileName = file,
        DisplayName = "启用爆炸破坏",
        Description = "爆炸是否对方块造成破坏",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableBlockGravity",
        ConfigFileName = file,
        DisplayName = "启用方块重力",
        Description = "是否启用受重力影响的方块（沙子、砂砾）",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay-settings.enableHunger",
        ConfigFileName = file,
        DisplayName = "启用饥饿值",
        Description = "是否启用玩家饥饿值系统",
        Category = "游戏玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== misc-settings（杂项设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "misc-settings.shutdownmessage",
        ConfigFileName = file,
        DisplayName = "关服提示消息",
        Description = "服务器关闭时踢出玩家显示的提示文本",
        Category = "杂项",
        DefaultValue = "Server closed",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "misc-settings.installspark",
        ConfigFileName = file,
        DisplayName = "安装 Spark",
        Description = "是否自动下载并加载 Spark 性能分析插件",
        Category = "杂项",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "misc-settings.bypassapicheck",
        ConfigFileName = file,
        DisplayName = "跳过 API 版本检查",
        Description = "true 时跳过插件对 PNX API 版本的兼容性检查\n⚠️ 不推荐生产环境使用",
        Category = "杂项",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "misc-settings.overrideserverauthblockbreaking",
        ConfigFileName = file,
        DisplayName = "覆盖服务器权威破坏",
        Description = "true 时覆盖基岩版 server-authoritative-block-breaking 字段\n强制启用服务器权威校验",
        Category = "杂项",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "misc-settings.enablemetrics",
        ConfigFileName = file,
        DisplayName = "启用统计上报",
        Description = "是否向 PNX bStats 上报匿名统计数据",
        Category = "杂项",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== level-settings（世界设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.levelthread",
        ConfigFileName = file,
        DisplayName = "每世界独立线程",
        Description = "true 时每个世界使用独立线程运行（PNX 多线程模型）\n开启可提升多世界性能但可能引发同步问题",
        Category = "世界",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.autotickrate",
        ConfigFileName = file,
        DisplayName = "自动调节 tick 频率",
        Description = "服务器卡顿时自动降低 tick 频率以维持稳定",
        Category = "世界",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.autotickratelimit",
        ConfigFileName = file,
        DisplayName = "自动降频上限",
        Description = "自动降频的最大倍率\n避免服务器 tick 速率被降到不可接受的程度",
        Category = "世界",
        DefaultValue = "20",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.basetickrate",
        ConfigFileName = file,
        DisplayName = "基础 tick 频率",
        Description = "基础 tick 倍率\n1 = 20 TPS（原版）/ 2 = 10 TPS（半速）\n调大可省 CPU 但游戏变卡",
        Category = "世界",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.alwaystickplayers",
        ConfigFileName = file,
        DisplayName = "每 tick 都处理玩家",
        Description = "true 时无论其他设置如何，每个 tick 都处理玩家逻辑\n一般保持 false",
        Category = "世界",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.loadalllevels",
        ConfigFileName = file,
        DisplayName = "加载所有世界",
        Description = "启动时是否加载所有已注册的世界",
        Category = "世界",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.chunkunloaddelay",
        ConfigFileName = file,
        DisplayName = "区块卸载延迟",
        Description = "区块无人引用后多久才卸载（毫秒）",
        Category = "世界",
        DefaultValue = "15000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.entityspawncap",
        ConfigFileName = file,
        DisplayName = "实体生成上限",
        Description = "单个世界实体数量上限",
        Category = "世界",
        DefaultValue = "512",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.fieldofview",
        ConfigFileName = file,
        DisplayName = "视场角",
        Description = "服务器发送给客户端的视场角（FOV）值",
        Category = "世界",
        DefaultValue = "100",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.levelworkerthreads",
        ConfigFileName = file,
        DisplayName = "世界工作线程数",
        Description = "每个世界的工作线程数\n-1 表示自动根据 CPU 核心数决定",
        Category = "世界",
        DefaultValue = "-1",
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== chunk-settings（区块设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.spawnlimit",
        ConfigFileName = file,
        DisplayName = "区块生成上限",
        Description = "每 tick 最多生成多少个区块",
        Category = "区块",
        DefaultValue = "3",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.perticksend",
        ConfigFileName = file,
        DisplayName = "每 tick 发送区块数",
        Description = "每个 tick 向单个玩家发送多少个区块\n值越大玩家加载地形越快但带宽占用越高",
        Category = "区块",
        DefaultValue = "32",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.spawnthreshold",
        ConfigFileName = file,
        DisplayName = "出生前发送区块数",
        Description = "玩家进服前至少需要发送多少个区块才能让其出生",
        Category = "区块",
        DefaultValue = "56",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.chunksperticks",
        ConfigFileName = file,
        DisplayName = "每 tick 处理区块数",
        Description = "每 tick 处理多少个区块的 tick（实体、红石、作物）\n-1 表示自动",
        Category = "区块",
        DefaultValue = "-1",
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.tickRadius",
        ConfigFileName = file,
        DisplayName = "区块 tick 半径",
        Description = "玩家周围多少区块半径内会被 tick\n值越大 CPU 占用越高",
        Category = "区块",
        DefaultValue = "4",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.lightupdates",
        ConfigFileName = file,
        DisplayName = "启用光照更新",
        Description = "是否启用光照计算与更新",
        Category = "区块",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.clearticklist",
        ConfigFileName = file,
        DisplayName = "清空 tick 列表",
        Description = "是否在每次 tick 后清空待处理列表",
        Category = "区块",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.generationqueuesize",
        ConfigFileName = file,
        DisplayName = "生成队列上限",
        Description = "等待生成的区块队列最大长度\n玩家快速移动（如鞘翅飞行）时可适当调大",
        Category = "区块",
        DefaultValue = "8",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.saveGenerated",
        ConfigFileName = file,
        DisplayName = "保存生成的区块",
        Description = "是否将新生成的区块立即保存到磁盘",
        Category = "区块",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.convertBDSChunks",
        ConfigFileName = file,
        DisplayName = "转换 BDS 区块",
        Description = "是否将官方 BDS 服务器生成的区块格式转换为 PNX 格式",
        Category = "区块",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-settings.disableblockticking",
        ConfigFileName = file,
        DisplayName = "禁用方块 tick 列表",
        Description = "不进行随机 tick 的方块 ID 列表（如 minecraft:grass）",
        Category = "区块",
        DefaultValue = "",
        ValueType = "list",
        RequiresRestart = true
    });

    // ==================== network-settings（网络设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.queryplugins",
        ConfigFileName = file,
        DisplayName = "Query 暴露插件列表",
        Description = "true 时允许通过 GameSpy Query 协议列出已加载插件\n公网服务器建议关闭以避免泄露插件信息",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.compressionlevel",
        ConfigFileName = file,
        DisplayName = "Zlib 压缩级别",
        Description = "数据包 Zlib 压缩级别\n值越大 CPU 占用越高、带宽越省\n基岩版推荐 4-6",
        Category = "网络",
        DefaultValue = "4",
        MinValue = 1,
        MaxValue = 9,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.zlibprovider",
        ConfigFileName = file,
        DisplayName = "Zlib 实现提供者",
        Description = "Zlib 压缩库的提供者\n0 = Java / 1 = Native / 2 = JNI / 3 = Netty（默认）/ 4 = System",
        Category = "网络",
        DefaultValue = "3",
        MinValue = 0,
        MaxValue = 4,
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.snappy",
        ConfigFileName = file,
        DisplayName = "启用 Snappy 压缩",
        Description = "实验性：使用 Google Snappy 算法替代 Zlib\n压缩比低但速度极快\n⚠️ 实验功能，可能不兼容旧客户端",
        Category = "网络",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.compressionbuffersize",
        ConfigFileName = file,
        DisplayName = "压缩缓冲区大小",
        Description = "Zlib 压缩缓冲区大小（字节）\n默认 1 MB",
        Category = "网络",
        DefaultValue = "1048576",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.maxdecompresssize",
        ConfigFileName = file,
        DisplayName = "最大解压大小",
        Description = "单个数据包最大解压大小（字节）\n默认 256 MB\n防止恶意超大包攻击",
        Category = "网络",
        DefaultValue = "268435456",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.packetlimit",
        ConfigFileName = file,
        DisplayName = "数据包大小上限",
        Description = "单个数据包最大字节数\n超过此值的包会被拒绝",
        Category = "网络",
        DefaultValue = "8000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.query",
        ConfigFileName = file,
        DisplayName = "启用 Query",
        Description = "是否启用 GameSpy Query 协议（用于服务器列表服务）",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.encryption",
        ConfigFileName = file,
        DisplayName = "启用网络加密",
        Description = "是否启用基岩版网络加密（基于 ECDH 握手）\n强烈建议保持 true，关闭后所有数据明文传输，存在严重安全风险",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.logintime",
        ConfigFileName = file,
        DisplayName = "检查登录时间",
        Description = "是否校验玩家登录用时\n防止登录洪水攻击",
        Category = "网络",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.autoflush",
        ConfigFileName = file,
        DisplayName = "自动刷新发送缓冲",
        Description = "是否自动刷新网络发送缓冲\n关闭可省 CPU 但增加延迟",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.flushinterval",
        ConfigFileName = file,
        DisplayName = "刷新间隔",
        Description = "自动刷新发送缓冲的间隔（tick）",
        Category = "网络",
        DefaultValue = "10",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.maxqueuedbytes",
        ConfigFileName = file,
        DisplayName = "最大排队字节数",
        Description = "单个玩家发送队列最大字节数\n默认 64 MB\n防止慢速客户端拖垮服务器",
        Category = "网络",
        DefaultValue = "67108864",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.cookiemode",
        ConfigFileName = file,
        DisplayName = "Cookie 模式",
        Description = "处理基岩版 1.21+ Cookie 的模式\nACTIVE = 接受并响应 / IGNORE = 忽略",
        Category = "网络",
        DefaultValue = "ACTIVE",
        AllowedValues = ["ACTIVE", "IGNORE"],
        ValueType = "enum",
        RequiresRestart = true
    });

    // ---------- 速率限制（network-settings.rate-limit） ----------

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.rate-limit.enabled",
        ConfigFileName = file,
        DisplayName = "启用速率限制",
        Description = "是否启用网络包速率限制（防洪水攻击）",
        Category = "网络-速率限制",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.rate-limit.maxinboundpersecond",
        ConfigFileName = file,
        DisplayName = "每秒入站包上限",
        Description = "单个玩家每秒可发送的最大数据包数",
        Category = "网络-速率限制",
        DefaultValue = "1500",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.rate-limit.maxpacketspertick",
        ConfigFileName = file,
        DisplayName = "每 tick 包上限",
        Description = "单个玩家每 tick 可发送的最大数据包数",
        Category = "网络-速率限制",
        DefaultValue = "500",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.rate-limit.maxcommandsperplayer",
        ConfigFileName = file,
        DisplayName = "每秒命令上限",
        Description = "单个玩家每秒可执行的命令数",
        Category = "网络-速率限制",
        DefaultValue = "10",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.rate-limit.maxchatperplayer",
        ConfigFileName = file,
        DisplayName = "每秒聊天上限",
        Description = "单个玩家每秒可发送的聊天消息数",
        Category = "网络-速率限制",
        DefaultValue = "2",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.rate-limit.maxformresponsesperplayer",
        ConfigFileName = file,
        DisplayName = "每秒表单响应上限",
        Description = "单个玩家每秒可发送的表单（UI）响应数",
        Category = "网络-速率限制",
        DefaultValue = "20",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.rate-limit.maxmovementperplayer",
        ConfigFileName = file,
        DisplayName = "每秒移动包上限",
        Description = "单个玩家每秒可发送的移动数据包数",
        Category = "网络-速率限制",
        DefaultValue = "40",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    // ---------- 僵尸网络检测（network-settings.botnet） ----------

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.botnet.enabled",
        ConfigFileName = file,
        DisplayName = "启用僵尸网络检测",
        Description = "是否启用基于行为分析的僵尸网络检测",
        Category = "网络-僵尸网络",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.botnet.suspiciousthreshold",
        ConfigFileName = file,
        DisplayName = "可疑阈值",
        Description = "IP 行为评分超过此值视为可疑",
        Category = "网络-僵尸网络",
        DefaultValue = "300",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.botnet.minsuspiciousips",
        ConfigFileName = file,
        DisplayName = "最小可疑 IP 数",
        Description = "触发自动封禁所需的最小可疑 IP 数",
        Category = "网络-僵尸网络",
        DefaultValue = "3",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.botnet.autoblock",
        ConfigFileName = file,
        DisplayName = "自动封禁",
        Description = "是否在检测到僵尸网络时自动封禁可疑 IP",
        Category = "网络-僵尸网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.botnet.autoblockdurationseconds",
        ConfigFileName = file,
        DisplayName = "自动封禁时长",
        Description = "自动封禁的持续时长（秒）",
        Category = "网络-僵尸网络",
        DefaultValue = "60",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-settings.botnet.minscore",
        ConfigFileName = file,
        DisplayName = "最小评分",
        Description = "单个 IP 触发评分的最小行为次数",
        Category = "网络-僵尸网络",
        DefaultValue = "2",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== debug-settings（调试设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "debug-settings.deprecatedverbose",
        ConfigFileName = file,
        DisplayName = "弃用 API 警告",
        Description = "插件使用已弃用的 API 方法时是否在控制台打印警告\n开发环境建议开启，生产环境可关闭以减少日志噪音",
        Category = "调试",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "debug-settings.level",
        ConfigFileName = file,
        DisplayName = "调试日志级别",
        Description = "控制台日志详细程度\nINFO = 正常日志 / DEBUG = 调试信息 / TRACE = 追踪（极大量日志）",
        Category = "调试",
        DefaultValue = "INFO",
        AllowedValues = ["INFO", "DEBUG", "TRACE"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "debug-settings.command",
        ConfigFileName = file,
        DisplayName = "启用调试命令",
        Description = "是否启用 /debug 调试命令",
        Category = "调试",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "debug-settings.packet.mode",
        ConfigFileName = file,
        DisplayName = "数据包调试模式",
        Description = "false = 忽略数据包日志 / true = 记录 packetList 中指定的数据包",
        Category = "调试",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "debug-settings.packetList",
        ConfigFileName = file,
        DisplayName = "数据包白名单",
        Description = "启用 packet.mode 时要记录的数据包 ID 列表",
        Category = "调试",
        DefaultValue = "",
        ValueType = "list",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "debug-settings.disableencodinglimits",
        ConfigFileName = file,
        DisplayName = "禁用编码限制",
        Description = "是否禁用 NBT 编码长度限制\n⚠️ 仅调试用，会带来安全风险",
        Category = "调试",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== performance-settings（性能设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "performance-settings.asyncworkers",
        ConfigFileName = file,
        DisplayName = "异步工作线程数",
        Description = "AsyncTask 的工作线程数\nauto 自动检测 CPU 核心数（至少 4）\n手动设置时建议不超过 CPU 核心数",
        Category = "性能",
        DefaultValue = "auto",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "performance-settings.basetps",
        ConfigFileName = file,
        DisplayName = "基础 TPS",
        Description = "服务器目标 TPS（每秒 tick 数）\n原版为 20",
        Category = "性能",
        DefaultValue = "20",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "performance-settings.registrycache.enable",
        ConfigFileName = file,
        DisplayName = "启用注册表缓存",
        Description = "是否在启动时将方块/物品注册表缓存到磁盘以加速下次启动",
        Category = "性能",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "performance-settings.registrycache.path",
        ConfigFileName = file,
        DisplayName = "缓存文件路径",
        Description = "注册表缓存文件路径",
        Category = "性能",
        DefaultValue = "path/to/your/registry_cache.bin",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "performance-settings.forcegcpercentage",
        ConfigFileName = file,
        DisplayName = "强制 GC 阈值",
        Description = "内存使用率达到此比例时强制触发 GC\n1.0 = 100%（禁用强制 GC）\n0.85 = 85% 触发 GC",
        Category = "性能",
        DefaultValue = "1.0",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "performance-settings.freeze-array.enable",
        ConfigFileName = file,
        DisplayName = "启用冻结数组",
        Description = "是否启用冻结数组优化\n将常量数组包装为不可变版本，便于 JVM 内联优化",
        Category = "性能-冻结数组",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ============================================================
    // 基岩版 server.properties（使用 powernukkit-server.properties 区分）
    // ============================================================

    RegisterPowerNukkitServerProperties();
}

/// <summary>
/// 注册 PowerNukkitX 基岩版 server.properties 的描述符
/// ⚠️ 基岩版字段与 Java 版不同（端口 UDP、无 spectator、online-mode 指 Xbox Live）
/// 使用文件名 powernukkit-server.properties 与 Java 版描述符区分
/// 数据来源：LegacyServerPropertiesKeys.java 枚举
/// </summary>
private void RegisterPowerNukkitServerProperties()
{
    const string file = "powernukkit-server.properties";

    // ---------- 服务器基础信息 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "motd",
        ConfigFileName = file,
        DisplayName = "服务器 MOTD",
        Description = "服务器在客户端列表中显示的名称\n与 powernukkit.yml 中的 motd 同步",
        Category = "基础信息",
        DefaultValue = "PowerNukkitX Server",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "sub-motd",
        ConfigFileName = file,
        DisplayName = "子 MOTD",
        Description = "服务器副标题",
        Category = "基础信息",
        DefaultValue = "powernukkitx.org",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server-port",
        ConfigFileName = file,
        DisplayName = "IPv4 端口（UDP）",
        Description = "服务器监听的 IPv4 UDP 端口\n⚠️ 必须开放 UDP 协议！路由器端口转发也需选 UDP\n基岩版默认 19132（Java 版是 25565/TCP）",
        Category = "网络",
        DefaultValue = "19132",
        MinValue = 1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server-ip",
        ConfigFileName = file,
        DisplayName = "服务器 IP",
        Description = "服务器绑定的 IPv4 地址\n0.0.0.0 表示监听所有网卡",
        Category = "网络",
        DefaultValue = "0.0.0.0",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "view-distance",
        ConfigFileName = file,
        DisplayName = "视野距离",
        Description = "玩家可见的区块半径\n⚠️ 基岩版默认 8，比 Java 版的 10 小\n值越大带宽和内存占用越高",
        Category = "世界",
        DefaultValue = "8",
        MinValue = 5,
        ValueType = "int",
        RequiresRestart = true
    });

    // ---------- 玩家与权限 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "white-list",
        ConfigFileName = file,
        DisplayName = "启用白名单",
        Description = "true 时仅 allowlist.json 中的玩家可加入",
        Category = "玩家",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "max-players",
        ConfigFileName = file,
        DisplayName = "最大玩家数",
        Description = "服务器同时允许的最大玩家数\n值越高对性能影响越大",
        Category = "玩家",
        DefaultValue = "20",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "xbox-auth",
        ConfigFileName = file,
        DisplayName = "Xbox Live 验证",
        Description = "基岩版关键差异：true 时所有玩家必须通过 Xbox Live 认证\n⚠️ 与 Java 版 online-mode 含义不同！\n公网服务器强烈建议开启，关闭会导致玩家可伪装身份\n远程（非 LAN）连接无论此设置如何，始终需要 Xbox Live 认证",
        Category = "玩家",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ---------- 游戏模式与难度 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "gamemode",
        ConfigFileName = file,
        DisplayName = "默认游戏模式",
        Description = "新玩家加入时的默认游戏模式\n⚠️ 基岩版无 spectator 选项！\n0 = 生存 / 1 = 创造 / 2 = 冒险",
        Category = "游戏",
        DefaultValue = "0",
        AllowedValues = ["0", "1", "2"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "force-gamemode",
        ConfigFileName = file,
        DisplayName = "强制游戏模式",
        Description = "true 时玩家进服始终被强制设置为 gamemode 指定的模式\n忽略其上次退出时的模式",
        Category = "游戏",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "difficulty",
        ConfigFileName = file,
        DisplayName = "难度",
        Description = "世界难度\n0 = 和平（不刷怪）/ 1 = 简单 / 2 = 普通 / 3 = 困难（僵尸破门等）",
        Category = "游戏",
        DefaultValue = "1",
        AllowedValues = ["0", "1", "2", "3"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "hardcore",
        ConfigFileName = file,
        DisplayName = "极限模式",
        Description = "是否启用极限模式（玩家死亡后封禁）",
        Category = "游戏",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "pvp",
        ConfigFileName = file,
        DisplayName = "启用 PvP",
        Description = "是否允许玩家间伤害",
        Category = "游戏",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "allow-flight",
        ConfigFileName = file,
        DisplayName = "允许飞行",
        Description = "是否允许玩家在生存模式飞行\n⚠️ 这是反作弊豁免，而非启用飞行能力\n建议关闭以防止飞行作弊",
        Category = "游戏",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "achievements",
        ConfigFileName = file,
        DisplayName = "启用成就",
        Description = "是否启用成就/进度系统",
        Category = "游戏",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "announce-player-achievements",
        ConfigFileName = file,
        DisplayName = "广播成就",
        Description = "玩家解锁成就时是否在聊天栏广播",
        Category = "游戏",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "spawn-protection",
        ConfigFileName = file,
        DisplayName = "出生保护半径",
        Description = "出生点保护半径（方块）\n非 OP 玩家无法在此范围内破坏\n0 = 禁用保护",
        Category = "游戏",
        DefaultValue = "16",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "spawn-animals",
        ConfigFileName = file,
        DisplayName = "生成动物",
        Description = "是否自然生成动物",
        Category = "游戏",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "spawn-mobs",
        ConfigFileName = file,
        DisplayName = "生成怪物",
        Description = "是否自然生成怪物",
        Category = "游戏",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ---------- 世界生成 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "level-name",
        ConfigFileName = file,
        DisplayName = "世界名称",
        Description = "世界文件夹的名称\n每个世界在 worlds/ 下有独立文件夹",
        Category = "世界",
        DefaultValue = "world",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-seed",
        ConfigFileName = file,
        DisplayName = "世界种子",
        Description = "世界生成种子\n留空则随机生成\n相同种子生成相同地形",
        Category = "世界",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "allow-nether",
        ConfigFileName = file,
        DisplayName = "启用下界",
        Description = "是否加载下界维度",
        Category = "世界",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "allow-the_end",
        ConfigFileName = file,
        DisplayName = "启用末地",
        Description = "是否加载末地维度",
        Category = "世界",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "auto-save",
        ConfigFileName = file,
        DisplayName = "自动保存",
        Description = "是否启用自动保存（间隔由 powernukkit.yml 的 autosaveDelay 控制）",
        Category = "维护",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ---------- 资源包 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "force-resources",
        ConfigFileName = file,
        DisplayName = "强制资源包",
        Description = "true 时玩家必须接受服务器资源包才能进服\n拒绝资源包的玩家会被踢出",
        Category = "资源包",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "force-resources-allow-client-packs",
        ConfigFileName = file,
        DisplayName = "允许客户端资源包",
        Description = "是否允许玩家使用客户端自带资源包",
        Category = "资源包",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ---------- 网络与远程管理 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "enable-query",
        ConfigFileName = file,
        DisplayName = "启用 Query",
        Description = "是否启用 GameSpy Query 协议\n用于服务器列表服务",
        Category = "远程管理",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "enable-rcon",
        ConfigFileName = file,
        DisplayName = "启用 RCON",
        Description = "是否启用远程控制台协议（RCON）\n允许通过 TCP 发送命令到服务器\n启用务必设置强密码！",
        Category = "远程管理",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "rcon.password",
        ConfigFileName = file,
        DisplayName = "RCON 密码",
        Description = "RCON 远程管理密码\n启用 RCON 时必须设置，否则任何人都能远程控制服务器",
        Category = "远程管理",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "check-login-time",
        ConfigFileName = file,
        DisplayName = "检查登录时间",
        Description = "是否校验玩家登录用时\n防止登录洪水攻击",
        Category = "反作弊",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network-encryption",
        ConfigFileName = file,
        DisplayName = "网络加密",
        Description = "是否启用基岩版网络加密（基于 ECDH 握手）\n强烈建议保持 true，关闭后所有数据明文传输，存在严重安全风险",
        Category = "反作弊",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });
}
