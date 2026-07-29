// -----------------------------------------------------------------------------
// 文件名: RegisterNachoYml.cs
// 功能描述: 注册 NachoSpigot（基于 TacoSpigot 1.8.9）配置文件的描述符
//           包含 nacho.yml 全局 settings 节 + 每世界 world-settings 节（共 56 项）
// 数据来源: CobbleSword/NachoSpigot README.md (master, commit 5655b72) + 社区默认 nacho.yml
// 适用版本: NachoSpigot 1.8.9（项目已停更，2022 年最后构建）
// -----------------------------------------------------------------------------

private void RegisterNachoYml()
{
    const string file = "nacho.yml";

    // ==================== 信息块 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = file,
        DisplayName = "配置版本号",
        Description = "内部使用，不要手动修改\nNachoSpigot 用它做配置自动升级与兼容性判断",
        Category = "信息",
        DefaultValue = "6",
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== settings.chunk（区块线程） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "settings.chunk.threads",
        ConfigFileName = file,
        DisplayName = "区块线程数",
        Description = "用于区块加载 / 生成的线程数\n0 = 禁用多线程区块\n建议 2-4\n值越大区块加载越快但 CPU 越高",
        Category = "区块",
        DefaultValue = "2",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.chunk.players-per-thread",
        ConfigFileName = file,
        DisplayName = "每线程玩家数",
        Description = "每多少名玩家分配 1 个区块线程（与 threads 配合的负载估算参数）",
        Category = "区块",
        DefaultValue = "50",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== settings（全局杂项） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "settings.player-time-statistics-interval",
        ConfigFileName = file,
        DisplayName = "玩家统计间隔",
        Description = "多久统计一次玩家在线时间等数据（tick）\n20 tick = 1 秒，90 = 4.5 秒\n值越大越省 CPU 但统计精度越低",
        Category = "全局",
        DefaultValue = "90",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.panda-wire",
        ConfigFileName = file,
        DisplayName = "Panda 红石线优化",
        Description = "启用 PandaSpigot 的红石线优化\n可显著降低红石密集场景的 CPU 占用\n生电服可能需要 false 还原原版时序",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.brand-name",
        ConfigFileName = file,
        DisplayName = "服务端品牌名",
        Description = "发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码\n可隐藏真实核心类型\n建议改为通用名以防信息泄露",
        Category = "全局",
        DefaultValue = "NachoSpigot",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.anti-malware",
        ConfigFileName = file,
        DisplayName = "反恶意软件扫描",
        Description = "启动时扫描插件 jar 是否包含已知恶意代码特征\n开发 / 测试服可开启\n生产服按需",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.disabled-block-fall-animation",
        ConfigFileName = file,
        DisplayName = "禁用方块下落动画",
        Description = "禁用方块（如沙子、砂砾）下落时的客户端动画\ntrue 可减少网络包但视觉体验下降",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.patch-protocollib",
        ConfigFileName = file,
        DisplayName = "修补 ProtocolLib",
        Description = "应用 ProtocolLib 兼容性补丁\n使用 ProtocolLib 的服建议保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.stop-notify-bungee",
        ConfigFileName = file,
        DisplayName = "停止 Bungee 通知",
        Description = "不向 BungeeCord 发送服务器状态通知\n可减少跨服通信开销",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.anti-crash",
        ConfigFileName = file,
        DisplayName = "反崩溃保护",
        Description = "启用反崩溃机制，捕获并阻止可能导致服务器崩溃的异常操作\n生产服保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.fast-operators",
        ConfigFileName = file,
        DisplayName = "快速 OP 操作",
        Description = "优化 OP 权限检查的性能\nOP 较多的服可开启以加速权限判定",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.save-empty-scoreboard-teams",
        ConfigFileName = file,
        DisplayName = "保存空记分板队伍",
        Description = "是否保存空的记分板队伍到磁盘\nfalse 可减少无意义的队伍数据写入",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.kick-on-illegal-behavior",
        ConfigFileName = file,
        DisplayName = "非法行为踢出",
        Description = "玩家执行非法操作（如发包作弊）时是否踢出\n反作弊相关，生产服保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.stop-decoding-itemstack-on-place",
        ConfigFileName = file,
        DisplayName = "放置时不解码物品",
        Description = "放置方块时跳过 ItemStack 的重复解码\n可减少 CPU 开销，正常服保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.use-tcp-nodelay",
        ConfigFileName = file,
        DisplayName = "启用 TCP_NODELAY",
        Description = "启用 TCP_NODELAY 禁用 Nagle 算法，降低网络延迟\nPvP 服强烈建议 true\n修改需重启",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.faster-cannon-tracker",
        ConfigFileName = file,
        DisplayName = "快速炮弹追踪",
        Description = "优化 TNT / 炮弹实体的追踪性能\nTNT 大炮服保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.fix-eat-while-running",
        ConfigFileName = file,
        DisplayName = "修复跑动进食",
        Description = "修复玩家跑动时进食的漏洞\nPvP 服保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.hide-projectiles-from-hidden-players",
        ConfigFileName = file,
        DisplayName = "隐藏玩家对隐藏玩家发射弹射物",
        Description = "被隐藏的玩家发射的弹射物对其他玩家也不可见\n隐身插件相关",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.lag-compensated-potions",
        ConfigFileName = file,
        DisplayName = "卡顿补偿药水",
        Description = "启用卡顿补偿的药水效果计算\n实验性，可能影响 PvP 平衡",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.smooth-potting",
        ConfigFileName = file,
        DisplayName = "平滑投掷药水",
        Description = "平滑投掷药水的动画 / 时机\nPvP 服保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.anti-enderpearl-glitch",
        ConfigFileName = file,
        DisplayName = "防末影珍珠漏洞",
        Description = "防止末影珍珠传送漏洞\nPvP 服保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.disable-infinisleeper-thread-usage",
        ConfigFileName = file,
        DisplayName = "禁用 Infinisleeper 线程",
        Description = "禁用 Infinisleeper 后台线程\n一般保持 false",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.enable-fastmath",
        ConfigFileName = file,
        DisplayName = "启用 FastMath",
        Description = "使用更快的数学运算库替代原版\n实验性，可能影响某些计算精度",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.tile-entity-ticking-time",
        ConfigFileName = file,
        DisplayName = "方块实体 tick 时间",
        Description = "方块实体（如熔炉、漏斗）的 tick 间隔（tick）\n20 = 每 20 tick（1 秒）处理一次\n值越大越省 CPU 但方块实体变慢",
        Category = "全局",
        DefaultValue = "20",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.item-dirty-ticks",
        ConfigFileName = file,
        DisplayName = "物品脏标记 tick",
        Description = "多久标记一次物品栏为「脏」以同步给客户端\n值越大网络包越少但物品栏更新越慢",
        Category = "全局",
        DefaultValue = "20",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.use-tcp-fastopen",
        ConfigFileName = file,
        DisplayName = "启用 TCP Fast Open",
        Description = "启用 TCP Fast Open（TFO）减少握手延迟\n需操作系统与内核支持\n修改需重启",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.tcp-fastopen-mode",
        ConfigFileName = file,
        DisplayName = "TCP Fast Open 模式",
        Description = "TFO 模式\n0 = 禁用\n1 = 仅客户端模式\n2 = 仅服务端模式\n3 = 双向启用\n修改需重启",
        Category = "全局",
        DefaultValue = "1",
        MinValue = 0,
        MaxValue = 3,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.enable-protocollib-shim",
        ConfigFileName = file,
        DisplayName = "启用 ProtocolLib 垫片",
        Description = "启用 ProtocolLib 兼容垫片\n使用 ProtocolLib 的服保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.instant-interaction",
        ConfigFileName = file,
        DisplayName = "瞬时交互",
        Description = "跳过交互延迟检查\ntrue 可能影响反作弊\n一般保持 false",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.instant-use-entity",
        ConfigFileName = file,
        DisplayName = "瞬时实体使用",
        Description = "跳过实体使用延迟检查\ntrue 可能影响反作弊\n一般保持 false",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== settings.commands（命令开关） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "settings.commands.enable-version-command",
        ConfigFileName = file,
        DisplayName = "启用 /version 命令",
        Description = "是否允许玩家使用 /version（/ver）查看服务端版本信息\n关闭可隐藏核心类型，防信息泄露",
        Category = "命令",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.commands.enable-plugins-command",
        ConfigFileName = file,
        DisplayName = "启用 /plugins 命令",
        Description = "是否允许玩家使用 /plugins（/pl）查看已加载插件列表\n公网服建议关闭以防泄露插件信息",
        Category = "命令",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.commands.enable-reload-command",
        ConfigFileName = file,
        DisplayName = "启用 /reload 命令",
        Description = "是否允许使用 /reload 命令\n/reload 易导致插件状态异常，强烈建议关闭",
        Category = "命令",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== settings.event（事件开关） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "settings.event.fire-entity-explode-event",
        ConfigFileName = file,
        DisplayName = "触发实体爆炸事件",
        Description = "是否触发 EntityExplodeEvent\n无插件监听时可设 false 减少开销，但爆炸保护插件会失效",
        Category = "事件",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.event.fire-player-move-event",
        ConfigFileName = file,
        DisplayName = "触发玩家移动事件",
        Description = "是否触发 PlayerMoveEvent\n⚠️ 设为 false 会破坏大量插件（区域保护、反作弊等）\n仅极度追求性能且无移动相关插件时才可关",
        Category = "事件",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.event.fire-leaf-decay-event",
        ConfigFileName = file,
        DisplayName = "触发树叶凋落事件",
        Description = "是否触发 LeavesDecayEvent\n无插件监听时可设 false 减少开销",
        Category = "事件",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== settings.fixed-pools（固定对象池） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "settings.fixed-pools.use-fixed-pools-for-explosions",
        ConfigFileName = file,
        DisplayName = "爆炸用固定池",
        Description = "爆炸计算使用固定大小的对象池，避免频繁 GC\nTNT 密集服（如 TNT 大炮）可设 true 减少卡顿",
        Category = "对象池",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.fixed-pools.size",
        ConfigFileName = file,
        DisplayName = "固定池大小",
        Description = "固定对象池的容量\n需大于同时进行的爆炸计算数，过小会回退到普通分配",
        Category = "对象池",
        DefaultValue = "500",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    // ==================== world-settings.default（每世界杂项） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.verbose",
        ConfigFileName = file,
        DisplayName = "详细日志",
        Description = "是否在世界启动时输出该世界配置的详细信息\n排查问题可临时开启",
        Category = "世界-杂项",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.enable-lava-to-cobblestone",
        ConfigFileName = file,
        DisplayName = "岩浆变圆石",
        Description = "允许水流接触岩浆生成圆石（原版行为）\nfalse 可禁用以减少圆石农场卡服",
        Category = "世界-杂项",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.infinite-water-sources",
        ConfigFileName = file,
        DisplayName = "无限水源",
        Description = "允许 2x2 水池形成无限水源（原版行为）\nfalse 可禁用以限制水农场",
        Category = "世界-杂项",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.disable-sponge-absorption",
        ConfigFileName = file,
        DisplayName = "禁用海绵吸水",
        Description = "禁用海绵吸水行为\ntrue 可减少大量吸水计算的开销",
        Category = "世界-杂项",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.tick-enchantment-tables",
        ConfigFileName = file,
        DisplayName = "附魔台 tick",
        Description = "是否 tick 附魔台（周围书架的浮动书页动画）\nfalse 跳过此 tick 以省 CPU\n对应补丁 Nacho-0049",
        Category = "世界-杂项",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.block-operations",
        ConfigFileName = file,
        DisplayName = "方块操作",
        Description = "启用方块操作批处理优化\n一般保持 true",
        Category = "世界-杂项",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.unload-chunks",
        ConfigFileName = file,
        DisplayName = "卸载区块",
        Description = "允许自动卸载无玩家附近的区块以释放内存\n内存紧张服保持 true",
        Category = "世界-杂项",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.physics（每世界物理） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.physics.disable-place",
        ConfigFileName = file,
        DisplayName = "禁用放置物理",
        Description = "放置方块时不触发物理更新（如沙子下落、红石更新）\n⚠️ 会影响大量生电机制，仅极限性能服使用\n生电服请改为 false",
        Category = "世界-物理",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.physics.disable-update",
        ConfigFileName = file,
        DisplayName = "禁用更新物理",
        Description = "方块变化时不触发周边物理更新\n⚠️ 与 disable-place 类似，会破坏红石与生电\n生电服请改为 false",
        Category = "世界-物理",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.explosions（每世界爆炸） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.explosions.constant-radius",
        ConfigFileName = file,
        DisplayName = "恒定爆炸半径",
        Description = "爆炸使用恒定半径而非随机半径\ntrue 使爆炸范围可预测，便于 PvP 平衡",
        Category = "世界-爆炸",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.explosions.explode-protected-regions",
        ConfigFileName = file,
        DisplayName = "受保护区域爆炸",
        Description = "是否在受保护区域（如 spawn 保护区）仍计算爆炸\nfalse 可跳过保护区爆炸以省 CPU",
        Category = "世界-爆炸",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.explosions.reduced-density-rays",
        ConfigFileName = file,
        DisplayName = "减少密度射线",
        Description = "减少爆炸密度射线计算量\ntrue 可显著降低 TNT 大量爆炸时的 CPU 占用，但爆炸破坏精度略降",
        Category = "世界-爆炸",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.entity（每世界实体） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity.mob-ai",
        ConfigFileName = file,
        DisplayName = "生物 AI",
        Description = "⚠️ 字段名易误解：\nfalse = 启用原版生物 AI\ntrue = 禁用生物 AI（生物静止不动）\n极限性能服才设 true",
        Category = "世界-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity.mob-sound",
        ConfigFileName = file,
        DisplayName = "生物声音",
        Description = "⚠️ 同上语义反转：\nfalse = 启用生物声音\ntrue = 禁用生物声音以省 CPU",
        Category = "世界-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity.entity-activation",
        ConfigFileName = file,
        DisplayName = "实体激活",
        Description = "⚠️ false = 启用原版实体激活范围\ntrue = 禁用激活范围（所有实体全 tick）\n一般保持 false",
        Category = "世界-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity.endermite-spawning",
        ConfigFileName = file,
        DisplayName = "末影螨生成",
        Description = "是否允许末影螨生成\nfalse 禁用以减少末影珍珠农场产生的实体",
        Category = "世界-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
