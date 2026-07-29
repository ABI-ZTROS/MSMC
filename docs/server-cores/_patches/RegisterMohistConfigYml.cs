// -----------------------------------------------------------------------------
// 文件名: RegisterMohistConfigYml.cs
// 功能描述: 注册 Mohist（混合端）配置文件的描述符
//           对应 mohist-config/mohist.yml（1.20.1+ 路径，早期版本在根目录）
// 数据来源: MohistMC/Mohist src/main/java/com/mohistmc/config/MohistConfig.java
// 适用版本: Mohist 1.20.1（develop 分支）
// -----------------------------------------------------------------------------

private void RegisterMohistConfigYml()
{
    const string file = "mohist-config.yml";

    // ==================== 通用设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.lang",
        ConfigFileName = file,
        DisplayName = "控制台语言",
        Description = "Mohist 启动日志与控制台提示所使用的语言\n仅影响 Mohist 自身日志，不影响 Minecraft 原版日志\n修改后需重启",
        Category = "通用设置",
        DefaultValue = "en_US",
        AllowedValues = ["en_US", "zh_CN", "fr_FR", "es_ES", "de_DE", "ja_JP", "ko_KR", "ru_RU", "pt_BR", "zh_TW"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.check_update",
        ConfigFileName = file,
        DisplayName = "检查 Mohist 更新",
        Description = "启动时是否联网检查 Mohist 新版本\n公网服务器可开启；离线服可关闭以避免启动卡顿",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.check_update_bukkit",
        ConfigFileName = file,
        DisplayName = "检查 Bukkit 兼容性",
        Description = "启动时是否联网检查当前 Mohist 与最新 Bukkit/Spigot API 的兼容性",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.check_libraries_update",
        ConfigFileName = file,
        DisplayName = "检查依赖库更新",
        Description = "启动时是否检查并自动下载缺失的依赖库文件\n首次启动务必开启",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.metrics",
        ConfigFileName = file,
        DisplayName = "bStats 统计上报",
        Description = "是否启用 bStats 匿名数据上报\n无隐私敏感信息，建议保持开启帮助开发者了解使用情况",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.show_logo",
        ConfigFileName = file,
        DisplayName = "启动显示 Logo",
        Description = "控制台启动时是否打印 Mohist ASCII Logo",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.console_name",
        ConfigFileName = file,
        DisplayName = "控制台名称",
        Description = "控制台作为虚拟发送者执行命令时的显示名称",
        Category = "通用设置",
        DefaultValue = "Server",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.only_english",
        ConfigFileName = file,
        DisplayName = "强制仅英文日志",
        Description = "是否强制所有日志输出为英文（即使 lang 设置为其他语言）\n便于向 GitHub 提交 Issue",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 兼容性设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.bukkit_version",
        ConfigFileName = file,
        DisplayName = "Bukkit API 版本",
        Description = "Mohist 内部使用的 Bukkit API 版本号\n通常由 Mohist 自动写入，请勿手动修改",
        Category = "兼容性",
        DefaultValue = "1.20.1-R0.1-SNAPSHOT",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.support_non_paper_plugins",
        ConfigFileName = file,
        DisplayName = "允许非 Paper 系插件",
        Description = "是否允许加载仅声明支持 Spigot/CraftBukkit 的插件\n关闭后只允许加载声明支持 Paper 的插件",
        Category = "兼容性",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.disable_plugins_blacklist",
        ConfigFileName = file,
        DisplayName = "禁用插件黑名单",
        Description = "Mohist 维护了一份已知与混合端不兼容的插件黑名单\n设为 true 跳过该检查（不推荐，可能导致崩溃）",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.disable_mods_blacklist",
        ConfigFileName = file,
        DisplayName = "禁用模组黑名单",
        Description = "跳过 Mohist 维护的已知不兼容 Forge 模组黑名单\n不推荐，可能导致崩溃",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.use_blacklist_extensions",
        ConfigFileName = file,
        DisplayName = "启用扩展黑名单",
        Description = "是否启用更严格的扩展黑名单（包含更多边缘案例）\n开启可能阻止更多模组/插件加载",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.plugins_hot_reload",
        ConfigFileName = file,
        DisplayName = "插件热重载",
        Description = "是否启用插件热重载功能（如 /plugin reload）\n实验性功能，部分插件热重载可能引发内存泄漏",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.disable_warn",
        ConfigFileName = file,
        DisplayName = "禁用兼容性警告",
        Description = "是否在启动日志中禁用 Mohist 对某些不兼容插件/模组的警告信息\n生产环境为减少日志噪音可考虑开启",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== 性能优化（实体/异步） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.max_entities",
        ConfigFileName = file,
        DisplayName = "实体数量上限",
        Description = "单一世界内允许的最大实体数量\n超出则阻止新实体生成；-1 表示不限制\n注意：与 Forge 模组的实体（如机器内的物品）可能冲突",
        Category = "性能-实体",
        DefaultValue = "-1",
        MinValue = -1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.entity_tick",
        ConfigFileName = file,
        DisplayName = "实体 tick 优化级别",
        Description = "实体 tick 优化级别\n值越大越省 CPU 但实体 AI 越迟钝；1 = 原版\n⚠️ 影响模组怪物 AI，建议保持默认",
        Category = "性能-实体",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.entity_tick_skip",
        ConfigFileName = file,
        DisplayName = "跳过远实体 tick",
        Description = "是否跳过远离玩家实体的 tick 计算\n开启可提升性能，但可能破坏部分模组刷怪塔/农场",
        Category = "性能-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.async_pathfinding",
        ConfigFileName = file,
        DisplayName = "异步寻路",
        Description = "将生物寻路计算转移到异步线程\n⚠️ 部分模组（如自定义 AI 模组）可能与异步寻路冲突，开启前请测试",
        Category = "性能-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.async_mob_spawning",
        ConfigFileName = file,
        DisplayName = "异步生物生成",
        Description = "将生物生成计算转移到异步线程\n⚠️ 与 Forge 模组的事件监听可能冲突，模组较多的服务器请谨慎开启",
        Category = "性能-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.enable_real_ticking",
        ConfigFileName = file,
        DisplayName = "真实 tick 远实体",
        Description = "是否对远离玩家的实体也保持真实 tick（原版行为）\n关闭可省性能，但部分模组的机器/农场可能失效",
        Category = "性能-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.runtime_optimizations",
        ConfigFileName = file,
        DisplayName = "运行时优化",
        Description = "是否启用 Mohist 运行时性能优化补丁\n包含若干异步处理与缓存优化\n⚠️ 与高性能需求模组可能冲突",
        Category = "性能-综合",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.tps_real_time",
        ConfigFileName = file,
        DisplayName = "真实 TPS 显示",
        Description = "/tps 命令显示真实 TPS（包含所有线程负载）还是仅主线程 TPS",
        Category = "性能-综合",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.use_Spark_and_Sync_Timer",
        ConfigFileName = file,
        DisplayName = "Spark 计时器",
        Description = "是否启用 Mohist 内置的同步计时器（用于性能分析）\nSpark 插件依赖此功能",
        Category = "性能-综合",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 区块与世界 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.unload_worlds",
        ConfigFileName = file,
        DisplayName = "允许卸载世界",
        Description = "是否允许在无玩家时卸载非主世界（如下界、末地）以节省内存\n多世界服建议开启",
        Category = "区块与世界",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.disable_chunk_unload",
        ConfigFileName = file,
        DisplayName = "禁用区块卸载",
        Description = "是否禁用区块自动卸载（所有加载过的区块常驻内存）\n开启可减少卡顿但极大增加内存占用",
        Category = "区块与世界",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.chunk_unload_delay",
        ConfigFileName = file,
        DisplayName = "区块卸载延迟",
        Description = "玩家离开后多久才卸载对应区块（毫秒）\n值越大越省 CPU 但内存占用越高",
        Category = "区块与世界",
        DefaultValue = "15000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.max-tick-time",
        ConfigFileName = file,
        DisplayName = "单 tick 最大耗时",
        Description = "单个 tick 超过此时间则触发 watchdog 崩服报告（毫秒）\n-1 禁用 watchdog（不推荐，模组卡死将无报警）",
        Category = "区块与世界",
        DefaultValue = "60000",
        MinValue = -1,
        ValueType = "int",
        RequiresRestart = false
    });

    // ==================== 事件桥接 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.fire_MC_ExplosionEvent",
        ConfigFileName = file,
        DisplayName = "转发爆炸事件",
        Description = "是否将 Forge 的爆炸事件转发到 Bukkit 的 EntityExplodeEvent/BlockExplodeEvent\n关闭可省 CPU，但 WorldGuard 等保护插件将无法拦截模组爆炸",
        Category = "事件桥接",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.fire_MC_BlockBreakEvent",
        ConfigFileName = file,
        DisplayName = "转发破坏方块事件",
        Description = "是否将 Forge 的方块破坏事件转发到 Bukkit 的 BlockBreakEvent\n关闭后保护插件将无法拦截模组方块破坏",
        Category = "事件桥接",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.fire_MC_BlockPlaceEvent",
        ConfigFileName = file,
        DisplayName = "转发放置方块事件",
        Description = "是否将 Forge 的方块放置事件转发到 Bukkit 的 BlockPlaceEvent",
        Category = "事件桥接",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.implement_entity_collision_event",
        ConfigFileName = file,
        DisplayName = "实体碰撞事件",
        Description = "是否实现 Bukkit 的实体碰撞事件（EntityInteractEvent 等）\n关闭可提升性能，但部分反作弊/物理插件会失效",
        Category = "事件桥接",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.implement_entity_damage_event",
        ConfigFileName = file,
        DisplayName = "实体伤害事件",
        Description = "是否为 Forge 模组的实体伤害触发 Bukkit 的 EntityDamageEvent\n关闭后 RPG/伤害修改类插件将无法作用于模组伤害",
        Category = "事件桥接",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== 玩家与权限 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.hide_online_players",
        ConfigFileName = file,
        DisplayName = "隐藏在线玩家列表",
        Description = "是否对其他服务器隐藏本服在线玩家列表（用于跨服防止 Tab 自动补全）",
        Category = "玩家与权限",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.disable_op_permissions",
        ConfigFileName = file,
        DisplayName = "禁用 OP 权限",
        Description = "是否禁用原版 OP 权限系统，强制所有权限通过 LuckPerms 等插件管理",
        Category = "玩家与权限",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== 日志与调试 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.log_mods_deaths",
        ConfigFileName = file,
        DisplayName = "记录模组实体死亡",
        Description = "是否在日志中记录所有 Forge 模组实体的死亡事件（用于排查刷怪问题）\n开启会产生大量日志",
        Category = "日志与调试",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.watchdog",
        ConfigFileName = file,
        DisplayName = "启用看门狗",
        Description = "是否启用 watchdog 线程监控主线程卡顿\n生产环境强烈建议开启",
        Category = "日志与调试",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "mohist.use_java_Hoe",
        ConfigFileName = file,
        DisplayName = "Java 优化（实验性）",
        Description = "实验性：启用 Java 内部优化（如向量化运算）\n需要 JDK 17+ 支持\n⚠️ 实验功能，可能不稳定",
        Category = "日志与调试",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });
}
