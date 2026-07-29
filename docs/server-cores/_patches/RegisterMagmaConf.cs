// -----------------------------------------------------------------------------
// 文件名: RegisterMagmaConf.cs
// 功能描述: 注册 Magma（混合端）配置文件的描述符
//           ⚠️ 注意：配置文件实际名为 magma.yml，但内部是 Properties 格式（key=value）
//           不是真正的 YAML！请使用 Properties 语法编辑
// 数据来源: magmamaintainers/Magma MagmaConfig.java
// 适用版本: Magma 1.18.2
// -----------------------------------------------------------------------------

private void RegisterMagmaConf()
{
    // ⚠️ 实际是 Properties 格式（key=value），不是 YAML
    const string file = "magma.yml";

    // ==================== 通用设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "magma.check-update",
        ConfigFileName = file,
        DisplayName = "检查 Magma 更新",
        Description = "启动时是否联网检查 Magma 新版本",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.bukkit-version",
        ConfigFileName = file,
        DisplayName = "Bukkit API 版本",
        Description = "Magma 内部使用的 Bukkit API 版本号\n由 Magma 自动写入，请勿手动修改",
        Category = "通用设置",
        DefaultValue = "1.18.2-R0.1-SNAPSHOT",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.disable-logger",
        ConfigFileName = file,
        DisplayName = "禁用部分日志",
        Description = "是否禁用 Magma 自身的部分调试日志（如启动日志）\n减少日志噪音",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.disable-sentry",
        ConfigFileName = file,
        DisplayName = "禁用 Sentry 错误上报",
        Description = "是否禁用 Sentry 错误自动上报\nMagma 默认会上报崩溃信息到 Sentry 帮助开发",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.remove-blank-line",
        ConfigFileName = file,
        DisplayName = "移除日志空行",
        Description = "是否移除日志中的多余空行，让日志更紧凑",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.remove-errormods",
        ConfigFileName = file,
        DisplayName = "移除报错模组日志",
        Description = "是否在启动失败时移除报错模组的详细日志（仅显示摘要）",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 性能优化（实体） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "magma.use-multi-thread-entity-tick",
        ConfigFileName = file,
        DisplayName = "多线程实体 tick",
        Description = "实验性：是否使用多线程处理实体 tick\n⚠️ 与绝大多数 Forge 模组冲突，强烈不建议开启",
        Category = "性能-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.max-entity-ticks-per-tick",
        ConfigFileName = file,
        DisplayName = "单 tick 实体上限",
        Description = "单次 tick 最多处理的实体数量\n-1 不限制\n模组较多的服务器可设上限防止实体爆炸卡服",
        Category = "性能-实体",
        DefaultValue = "-1",
        MinValue = -1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.entity-tick-limit",
        ConfigFileName = file,
        DisplayName = "实体 tick 限制",
        Description = "类似 max-entity-ticks-per-tick，限制实体 tick 总数\n-1 不限制",
        Category = "性能-实体",
        DefaultValue = "-1",
        MinValue = -1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.enable-real-ticking-entities",
        ConfigFileName = file,
        DisplayName = "真实 tick 实体",
        Description = "是否对所有实体保持真实 tick（原版行为）\n关闭可省性能，但部分模组机器/农场可能失效",
        Category = "性能-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.tick-skip",
        ConfigFileName = file,
        DisplayName = "跳过远实体 tick",
        Description = "是否跳过远离玩家实体的 tick\n开启可省 CPU 但破坏部分模组刷怪塔",
        Category = "性能-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.entity-activation-range",
        ConfigFileName = file,
        DisplayName = "实体激活范围总开关",
        Description = "是否启用实体激活范围机制（远离玩家的实体降低 tick 频率）",
        Category = "性能-实体",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 性能优化（区块与异步） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "magma.enable-asynchronous-chunk",
        ConfigFileName = file,
        DisplayName = "异步区块加载",
        Description = "是否启用异步区块加载/生成\n开启可显著减少主线程卡顿，提升玩家飞行/传送流畅度",
        Category = "性能-区块",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.async-pathfinding",
        ConfigFileName = file,
        DisplayName = "异步寻路",
        Description = "将生物寻路计算转移到异步线程\n⚠️ 部分模组可能与异步寻路冲突",
        Category = "性能-区块",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.async-mob-spawning",
        ConfigFileName = file,
        DisplayName = "异步生物生成",
        Description = "将生物生成计算转移到异步线程\n⚠️ 与 Forge 模组的事件监听可能冲突",
        Category = "性能-区块",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.use-async-thread",
        ConfigFileName = file,
        DisplayName = "启用异步线程",
        Description = "是否启用 Magma 的异步工作线程（用于区块、寻路等）\n建议保持 true",
        Category = "性能-区块",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.print-chunk",
        ConfigFileName = file,
        DisplayName = "打印区块加载信息",
        Description = "是否在日志中打印区块加载/卸载的详细信息\n排查区块问题时开启",
        Category = "性能-区块",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== 性能优化（综合） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "magma.target-tps",
        ConfigFileName = file,
        DisplayName = "目标 TPS",
        Description = "服务器目标 TPS\n一般保持 20（原版）\n降低可省 CPU 但游戏变卡",
        Category = "性能-综合",
        DefaultValue = "20",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.max-tick-time",
        ConfigFileName = file,
        DisplayName = "单 tick 最大耗时",
        Description = "单个 tick 超过此时间触发 watchdog（毫秒）\n-1 禁用看门狗（不推荐）",
        Category = "性能-综合",
        DefaultValue = "60000",
        MinValue = -1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.disable-watchdog",
        ConfigFileName = file,
        DisplayName = "禁用看门狗",
        Description = "是否禁用 watchdog 主线程监控\n⚠️ 不推荐，模组卡死将无报警",
        Category = "性能-综合",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.disable-watcher",
        ConfigFileName = file,
        DisplayName = "禁用文件监视器",
        Description = "是否禁用文件监视器（监视 mods/、plugins/ 等目录变化）\n关闭后无法热检测文件变更",
        Category = "性能-综合",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.optimized-crafting",
        ConfigFileName = file,
        DisplayName = "优化合成",
        Description = "是否启用合成台合成优化（缓存合成结果）\n可提升合成性能",
        Category = "性能-综合",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.fast-rain",
        ConfigFileName = file,
        DisplayName = "快速降雨",
        Description = "是否优化天气变化（降雨/降雪）的处理逻辑\n减少天气切换时的卡顿",
        Category = "性能-综合",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.use-spark",
        ConfigFileName = file,
        DisplayName = "启用 Spark 集成",
        Description = "是否启用与 Spark 性能分析插件的集成\n建议保持 true",
        Category = "性能-综合",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 兼容性与事件 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "magma.allow-fluid-flow",
        ConfigFileName = file,
        DisplayName = "允许流体流动事件",
        Description = "是否允许 Forge 模组的流体流动触发 Bukkit 事件\n关闭可省 CPU，但部分物理/红石插件会失效",
        Category = "兼容性",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.disable-super-vanilla-fallable-block",
        ConfigFileName = file,
        DisplayName = "禁用原版下落方块优化",
        Description = "是否禁用 Magma 对原版下落方块（沙子、砂砾）的优化\n模组下落方块异常时可尝试开启",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.fix-tile-entity",
        ConfigFileName = file,
        DisplayName = "修复方块实体",
        Description = "修复部分 Forge 模组方块实体（TileEntity）与 Bukkit 事件的兼容性\n建议保持 true",
        Category = "兼容性",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.disable-flush",
        ConfigFileName = file,
        DisplayName = "禁用批量刷新",
        Description = "是否禁用网络数据包批量刷新\n开启可能减少延迟但增加带宽",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.disable-book-ban",
        ConfigFileName = file,
        DisplayName = "禁用书本封禁",
        Description = "是否启用书本封禁保护（防止玩家通过恶意 NBT 书本导致客户端/服务器崩溃）\n建议保持 true",
        Category = "兼容性",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "magma.enable-bungee",
        ConfigFileName = file,
        DisplayName = "启用 BungeeCord 支持",
        Description = "是否启用 BungeeCord/Velocity 跨服代理支持\n使用代理服时必须开启，并设置 bungeecord 相关项",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });
}
