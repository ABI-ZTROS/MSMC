// -----------------------------------------------------------------------------
// 文件名: RegisterNukkitYml.cs
// 功能描述: 注册 Nukkit（基岩版）配置文件的描述符
//           包含 nukkit.yml 与基岩版 server.properties（用 nukkit-server.properties 区分）
// 数据来源: CloudburstMC/Nukkit src/main/resources/lang/eng/nukkit.yml + 基岩版 BDS 文档
// 适用版本: Nukkit 1.0（master 分支，commit dbbb7ca）
// -----------------------------------------------------------------------------

private void RegisterNukkitYml()
{
    const string file = "nukkit.yml";

    // ==================== settings（基础设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "settings.language",
        ConfigFileName = file,
        DisplayName = "服务器语言",
        Description = "服务器控制台与提示消息使用的语言\n可选: eng 英语 / chs 简中 / cht 繁中 / jpn 日语 / rus 俄语 / spa 西语 / pol 波兰语 / bra 葡语 / kor 韩语 / ukr 乌克语 / deu 德语 / ltu 立陶宛语 / idn 印尼语 / cze 捷克语 / tur 土耳其语 / fin 芬兰语",
        Category = "基础设置",
        DefaultValue = "eng",
        AllowedValues = ["eng", "chs", "cht", "jpn", "rus", "spa", "pol", "bra", "kor", "ukr", "deu", "ltu", "idn", "cze", "tur", "fin"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.force-language",
        ConfigFileName = file,
        DisplayName = "强制使用服务器语言",
        Description = "true 时所有字符串按服务器语言翻译后发送给客户端\nfalse 时让客户端设备自行处理本地化（推荐）",
        Category = "基础设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.shutdown-message",
        ConfigFileName = file,
        DisplayName = "关服提示消息",
        Description = "服务器关闭时踢出玩家显示的提示文本",
        Category = "基础设置",
        DefaultValue = "Server closed",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.query-plugins",
        ConfigFileName = file,
        DisplayName = "Query 暴露插件列表",
        Description = "true 时允许通过 GameSpy Query 协议列出已加载插件\n公网服务器建议关闭以避免泄露插件信息",
        Category = "基础设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.deprecated-verbose",
        ConfigFileName = file,
        DisplayName = "弃用 API 警告",
        Description = "插件使用已弃用的 API 方法时是否在控制台打印警告\n开发环境建议开启，生产环境可关闭以减少日志噪音",
        Category = "基础设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.async-workers",
        ConfigFileName = file,
        DisplayName = "异步工作线程数",
        Description = "AsyncTask 的工作线程数\nauto 自动检测 CPU 核心数（至少 4）\n手动设置时建议不超过 CPU 核心数",
        Category = "基础设置",
        DefaultValue = "auto",
        ValueType = "string",
        RequiresRestart = true
    });

    // ==================== network（网络设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "network.batch-threshold",
        ConfigFileName = file,
        DisplayName = "批处理字节阈值",
        Description = "数据包累积到此字节数才进行批处理压缩\n0 = 压缩所有包；-1 = 完全禁用压缩\n降低此值减少延迟但增加 CPU 负担",
        Category = "网络",
        DefaultValue = "256",
        MinValue = -1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network.compression-level",
        ConfigFileName = file,
        DisplayName = "Zlib 压缩级别",
        Description = "批处理包的 Zlib 压缩级别\n值越大 CPU 占用越高、带宽越省\n基岩版推荐 5-7",
        Category = "网络",
        DefaultValue = "5",
        MinValue = 1,
        MaxValue = 9,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network.compression-use-snappy",
        ConfigFileName = file,
        DisplayName = "启用 Snappy 压缩",
        Description = "实验性：使用 Google Snappy 算法替代 Zlib\n压缩比低但速度极快，CPU 紧张的服务器可尝试\n⚠️ 实验功能，可能不兼容旧客户端",
        Category = "网络",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network.encryption",
        ConfigFileName = file,
        DisplayName = "启用网络加密",
        Description = "是否启用基岩版网络加密（基于 ECDH 握手）\n强烈建议保持 true，关闭后所有数据明文传输，存在严重安全风险",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== debug（调试设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "debug.level",
        ConfigFileName = file,
        DisplayName = "调试日志级别",
        Description = "控制台调试信息详细程度\n1 = 仅正常日志；2 = 显示调试信息；3 = 显示所有数据包详情（极大量日志）",
        Category = "调试",
        DefaultValue = "1",
        MinValue = 1,
        MaxValue = 3,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== level-settings（世界设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.default-format",
        ConfigFileName = file,
        DisplayName = "默认世界存储格式",
        Description = "新建世界使用的存储格式\nleveldb = 基岩版原生（推荐）；mcbeta = 旧版兼容；anvil = Java 版格式（实验性，不推荐）",
        Category = "世界",
        DefaultValue = "leveldb",
        AllowedValues = ["leveldb", "mcbeta", "anvil"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.auto-tick-rate",
        ConfigFileName = file,
        DisplayName = "自动调节 tick 频率",
        Description = "服务器卡顿时自动降低 tick 频率以维持稳定\n开启后服务器会动态调整以维持 20 TPS",
        Category = "世界",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.auto-tick-rate-limit",
        ConfigFileName = file,
        DisplayName = "自动降频上限",
        Description = "自动降频的最大倍率，避免服务器 tick 速率被降到不可接受的程度",
        Category = "世界",
        DefaultValue = "20",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.base-tick-rate",
        ConfigFileName = file,
        DisplayName = "基础 tick 频率",
        Description = "基础 tick 倍率\n1 = 20 TPS（原版）；2 = 10 TPS（半速）；3 = 约 6.7 TPS\n调大可省 CPU 但游戏变卡",
        Category = "世界",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "level-settings.always-tick-players",
        ConfigFileName = file,
        DisplayName = "每 tick 都处理玩家",
        Description = "true 时无论其他设置如何，每个 tick 都处理玩家逻辑\n一般保持 false",
        Category = "世界",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== chunk-sending（区块发送） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-sending.per-tick",
        ConfigFileName = file,
        DisplayName = "每 tick 发送区块数",
        Description = "每个 tick（1/20 秒）向单个玩家发送多少个区块\n值越大玩家加载地形越快，但带宽和 CPU 占用越高\n低配服建议 4，高配可调到 8-16",
        Category = "区块发送",
        DefaultValue = "4",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-sending.spawn-threshold",
        ConfigFileName = file,
        DisplayName = "出生前发送区块数",
        Description = "玩家进服前至少需要发送多少个区块才能让其出生\n过低会导致玩家悬空或掉入未加载地形；过高会增加登录等待时间",
        Category = "区块发送",
        DefaultValue = "56",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-sending.cache-chunks",
        ConfigFileName = file,
        DisplayName = "缓存区块序列化数据",
        Description = "true 时在内存中保存区块的序列化副本，加快向多个玩家发送同一区块的速度\n适合玩家密集的静态世界（如大厅服）\n动态生存服建议关闭以省内存",
        Category = "区块发送",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== chunk-ticking（区块 tick 处理） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-ticking.per-tick",
        ConfigFileName = file,
        DisplayName = "每 tick 处理区块上限",
        Description = "每 tick 最多处理多少个区块（实体的 AI、红石、作物生长等）\n降低此值可缓解实体密集时的卡顿，但作物生长和红石会变慢",
        Category = "区块 tick",
        DefaultValue = "40",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-ticking.tick-radius",
        ConfigFileName = file,
        DisplayName = "区块 tick 半径",
        Description = "玩家周围多少区块半径内会被 tick\n3 = 3 个区块半径（7x7 范围）\n值越大玩家附近活动越流畅，但 CPU 占用越高",
        Category = "区块 tick",
        DefaultValue = "3",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-ticking.clear-tick-list",
        ConfigFileName = file,
        DisplayName = "清空 tick 列表",
        Description = "是否在每次 tick 后清空待处理列表\n开启可防止列表累积但可能影响连续的红石/作物逻辑\n一般保持 false",
        Category = "区块 tick",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== chunk-generation（区块生成） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-generation.queue-size",
        ConfigFileName = file,
        DisplayName = "生成队列上限",
        Description = "等待生成的区块队列最大长度\n队列满时新请求会被丢弃\n玩家快速移动（如鞘翅飞行）时可适当调大",
        Category = "区块生成",
        DefaultValue = "8",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-generation.population-queue-size",
        ConfigFileName = file,
        DisplayName = "装饰队列上限",
        Description = "等待装饰（放置花草、矿物、结构等）的区块队列最大长度\n值过小会导致地形装饰滞后",
        Category = "区块生成",
        DefaultValue = "8",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== leveldb（LevelDB 存储） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "leveldb.use-native",
        ConfigFileName = file,
        DisplayName = "使用原生 LevelDB",
        Description = "true 时使用 C++ 原生 LevelDB 实现以获得更高性能\n需服务器安装对应 native 库，否则回退到 Java 实现",
        Category = "LevelDB",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "leveldb.cache-size-mb",
        ConfigFileName = file,
        DisplayName = "LevelDB 缓存大小",
        Description = "LevelDB 内存缓存大小（MB）\n值越大读取越快但占用内存越多\n大型世界建议 128-256 MB",
        Category = "LevelDB",
        DefaultValue = "80",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== ticks-per ====================

    Register(new ServerConfigDescriptor
    {
        Key = "ticks-per.autosave",
        ConfigFileName = file,
        DisplayName = "自动保存间隔",
        Description = "服务器自动保存世界与玩家数据的间隔（tick）\n6000 = 每 5 分钟保存一次（20 tick = 1 秒）\n0 = 禁用自动保存（不推荐，崩服会丢失进度）",
        Category = "Tick 间隔",
        DefaultValue = "6000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== player（玩家设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "player.save-player-data",
        ConfigFileName = file,
        DisplayName = "保存玩家数据",
        Description = "true 时玩家数据保存为 players/<玩家名>.dat\nfalse 时不保存，便于插件完全接管玩家数据\n一般保持 true",
        Category = "玩家",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player.skin-change-cooldown",
        ConfigFileName = file,
        DisplayName = "皮肤更换冷却",
        Description = "玩家两次更换皮肤之间的冷却时间（秒）\n0 = 无冷却\n防止玩家通过频繁换皮肤刷屏或攻击服务器",
        Category = "玩家",
        DefaultValue = "15",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player.attack-stop-sprint",
        ConfigFileName = file,
        DisplayName = "攻击停止冲刺",
        Description = "true 时玩家攻击实体后会停止冲刺（原版行为）\nfalse 时攻击不会打断冲刺（类似 1.8 PVP 手感）",
        Category = "玩家",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ============================================================
    // 基岩版 server.properties（使用 nukkit-server.properties 区分）
    // ============================================================

    RegisterNukkitServerProperties();
}

/// <summary>
/// 注册 Nukkit 基岩版 server.properties 的描述符
/// ⚠️ 基岩版字段与 Java 版不同（端口 UDP、无 spectator、online-mode 指 Xbox Live）
/// 使用文件名 nukkit-server.properties 与 Java 版描述符区分
/// </summary>
private void RegisterNukkitServerProperties()
{
    const string file = "nukkit-server.properties";

    // ---------- 网络与端口 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "server-name",
        ConfigFileName = file,
        DisplayName = "服务器名称（MOTD）",
        Description = "服务器在客户端服务器列表中显示的名称\n基岩版对 § 颜色码支持有限，建议使用纯文本或简单颜色\n不能包含分号",
        Category = "网络",
        DefaultValue = "Dedicated Server",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server-port",
        ConfigFileName = file,
        DisplayName = "IPv4 端口（UDP）",
        Description = "服务器监听的 IPv4 UDP 端口\n⚠️ 必须开放 UDP 协议，不是 TCP！\n路由器端口转发也需选 UDP\n基岩版默认 19132（Java 版是 25565/TCP）",
        Category = "网络",
        DefaultValue = "19132",
        MinValue = 1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server-portv6",
        ConfigFileName = file,
        DisplayName = "IPv6 端口（UDP）",
        Description = "服务器监听的 IPv6 UDP 端口\n不需要 IPv6 时可设为 0 禁用",
        Category = "网络",
        DefaultValue = "19133",
        MinValue = 0,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "enable-lan-visibility",
        ConfigFileName = file,
        DisplayName = "局域网可见性",
        Description = "true 时监听并响应局域网服务器发现请求\n同一台机器跑多个 Nukkit 时建议关闭以避免端口冲突",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ---------- 玩家与权限 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "max-players",
        ConfigFileName = file,
        DisplayName = "最大玩家数",
        Description = "服务器同时允许的最大玩家数\n值越高对性能影响越大",
        Category = "玩家",
        DefaultValue = "10",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "online-mode",
        ConfigFileName = file,
        DisplayName = "Xbox Live 验证",
        Description = "基岩版关键差异：true 时所有玩家必须通过 Xbox Live 认证\n公网服务器强烈建议开启，关闭会导致玩家可伪装身份\n远程（非 LAN）连接无论此设置如何，始终需要 Xbox Live 认证",
        Category = "玩家",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

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
        Key = "default-player-permission-level",
        ConfigFileName = file,
        DisplayName = "新玩家权限等级",
        Description = "首次加入的玩家默认权限等级\nvisitor = 访客（仅参观，不能交互）\nmember = 成员（正常游玩，推荐）\noperator = 管理员（OP 权限，⚠️ 生产环境绝不使用！）",
        Category = "玩家",
        DefaultValue = "member",
        AllowedValues = ["visitor", "member", "operator"],
        ValueType = "enum",
        RequiresRestart = true
    });

    // ---------- 游戏模式与难度 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "gamemode",
        ConfigFileName = file,
        DisplayName = "默认游戏模式",
        Description = "新玩家加入时的默认游戏模式\n⚠️ 基岩版无 spectator 选项！\nsurvival = 生存；creative = 创造；adventure = 冒险",
        Category = "游戏",
        DefaultValue = "survival",
        AllowedValues = ["survival", "creative", "adventure"],
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
        Description = "世界难度\npeaceful = 和平（不刷怪）；easy = 简单；normal = 普通；hard = 困难（僵尸破门等）",
        Category = "游戏",
        DefaultValue = "easy",
        AllowedValues = ["peaceful", "easy", "normal", "hard"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "allow-cheats",
        ConfigFileName = file,
        DisplayName = "允许作弊",
        Description = "true 时允许使用 /gamemode、/give 等作弊命令\n生存服建议 false，创造/测试服可设 true",
        Category = "游戏",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "texturepack-required",
        ConfigFileName = file,
        DisplayName = "强制资源包",
        Description = "true 时玩家必须接受服务器资源包才能进服\n拒绝资源包的玩家会被踢出",
        Category = "游戏",
        DefaultValue = "false",
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
        Description = "世界文件夹的名称\n每个世界在 worlds/ 下有独立文件夹\n改名为新世界，原世界保留但不再加载",
        Category = "世界",
        DefaultValue = "Bedrock level",
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
        Key = "level-type",
        ConfigFileName = file,
        DisplayName = "世界类型",
        Description = "地形类型\nDEFAULT = 标准地形；FLAT = 超平坦；LEGACY = 旧版地形\n⚠️ 与 Java 版选项不同（无 amplified、largeBiomes）",
        Category = "世界",
        DefaultValue = "DEFAULT",
        AllowedValues = ["DEFAULT", "FLAT", "LEGACY"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "view-distance",
        ConfigFileName = file,
        DisplayName = "视野距离",
        Description = "玩家可见的区块半径\n⚠️ 基岩版默认 32，比 Java 版的 10 大很多！\n值越大带宽和内存占用越高，公网服建议 10-16",
        Category = "世界",
        DefaultValue = "32",
        MinValue = 5,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "tick-distance",
        ConfigFileName = file,
        DisplayName = "tick 距离",
        Description = "玩家周围多少区块半径内会被服务器 tick（处理实体、红石等）\n基岩版独有字段，Java 版无此项\n值越大 CPU 占用越高",
        Category = "世界",
        DefaultValue = "4",
        MinValue = 4,
        MaxValue = 12,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "generate-structures",
        ConfigFileName = file,
        DisplayName = "生成结构",
        Description = "是否生成村庄、神殿、废弃矿井等结构",
        Category = "世界",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ---------- 安全与反作弊（基岩版独有） ----------

    Register(new ServerConfigDescriptor
    {
        Key = "server-authoritative-movement",
        ConfigFileName = file,
        DisplayName = "服务器权威移动",
        Description = "基岩版反作弊关键字段！\nserver-auth = 服务器校验玩家移动，发现异常回滚\nserver-auth-with-rewind = 同上但允许客户端预测\nclient-auth = 客户端权威（不推荐，易被作弊）",
        Category = "反作弊",
        DefaultValue = "server-auth",
        AllowedValues = ["client-auth", "server-auth", "server-auth-with-rewind"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "server-authoritative-block-breaking",
        ConfigFileName = file,
        DisplayName = "服务器权威破坏方块",
        Description = "true 时服务器校验玩家破坏方块的合法性\n防加速挖矿作弊",
        Category = "反作弊",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-movement-action-direction-threshold",
        ConfigFileName = file,
        DisplayName = "移动方向阈值",
        Description = "玩家移动方向与视线方向的偏差阈值\n超过此值视为可疑移动",
        Category = "反作弊",
        DefaultValue = "0.65",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-movement-distance-threshold",
        ConfigFileName = file,
        DisplayName = "移动距离阈值",
        Description = "单 tick 内玩家移动距离超过此值视为可疑\n可能在使用加速/飞行作弊",
        Category = "反作弊",
        DefaultValue = "0.5",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-movement-duration-threshold-in-ms",
        ConfigFileName = file,
        DisplayName = "异常持续时间阈值",
        Description = "玩家移动异常持续多久才视为作弊并触发回滚（毫秒）",
        Category = "反作弊",
        DefaultValue = "500",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "correct-player-movement",
        ConfigFileName = file,
        DisplayName = "纠正玩家移动",
        Description = "true 时服务器主动纠正玩家可疑的移动（强制回滚到合法位置）",
        Category = "反作弊",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ---------- 性能与维护 ----------

    Register(new ServerConfigDescriptor
    {
        Key = "max-threads",
        ConfigFileName = file,
        DisplayName = "最大线程数",
        Description = "服务器最大使用的线程数\n0 = 自动检测使用尽可能多的线程",
        Category = "性能",
        DefaultValue = "8",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "player-idle-timeout",
        ConfigFileName = file,
        DisplayName = "玩家挂机踢出",
        Description = "玩家挂机多少分钟后被踢出\n0 = 永不踢出",
        Category = "性能",
        DefaultValue = "30",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "content-log-file-enabled",
        ConfigFileName = file,
        DisplayName = "内容日志写文件",
        Description = "true 时将内容错误（如资源包解析失败）写入日志文件\n便于排查问题",
        Category = "性能",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "compression-threshold",
        ConfigFileName = file,
        DisplayName = "压缩阈值",
        Description = "网络数据包压缩的最小原始载荷大小（字节）\n值越大 CPU 越省但带宽越费\n基岩版默认 1（几乎全压缩）",
        Category = "性能",
        DefaultValue = "1",
        MinValue = 0,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "compression-algorithm",
        ConfigFileName = file,
        DisplayName = "压缩算法",
        Description = "网络压缩算法\nzlib = 标准压缩（兼容性好）\nsnappy = Google Snappy（速度更快但压缩比低）",
        Category = "性能",
        DefaultValue = "zlib",
        AllowedValues = ["zlib", "snappy"],
        ValueType = "enum",
        RequiresRestart = true
    });

    // ---------- 远程管理 ----------

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
        Key = "rcon.port",
        ConfigFileName = file,
        DisplayName = "RCON 端口",
        Description = "RCON 监听的 TCP 端口\n⚠️ 注意不要与 server-port（UDP）冲突",
        Category = "远程管理",
        DefaultValue = "19132",
        MinValue = 1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });
}
