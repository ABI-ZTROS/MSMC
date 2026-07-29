// RegisterSpongeForgeConf.cs
// 注册 SpongeForge 专属差异配置项（config/sponge/spongeforge-global.conf，HOCON 格式）
// 对应手册：docs/server-cores/33-spongeforge.md
// 仅注册与原版 Sponge 的差异项（约 30 项 Forge 专属设置），通用配置见 RegisterSpongeGlobalConf.cs

private void RegisterSpongeForgeConf()
{
    const string file = "config/sponge/spongeforge-global.conf";

    // ===== general（Forge 通用差异） =====
    Register(new ServerConfigDescriptor
    {
        Key = "general.inject-permission-into-forged-commands",
        ConfigFileName = file,
        DisplayName = "注入权限到 Forge 命令",
        Description = "是否把 Sponge 权限注入 Forge 模组注册的命令，使权限插件可管控模组命令。",
        Category = "Forge 通用差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "general.use-mod-message-channel",
        ConfigFileName = file,
        DisplayName = "使用模组消息通道",
        Description = "启用 Forge 模组消息通道以兼容 Forge 客户端模组。",
        Category = "Forge 通用差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "general.use-mod-detected-permission-for-command",
        ConfigFileName = file,
        DisplayName = "模组命令权限检测",
        Description = "检测模组命令所需权限等级（4=OP，0=所有人）。",
        Category = "Forge 通用差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "general.allow-sync-chunk-writes",
        ConfigFileName = file,
        DisplayName = "允许同步区块写入",
        Description = "Forge 模组可能强制同步写入，开启以兼容部分老模组。",
        Category = "Forge 通用差异",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "general.deobfuscate-stacktraces",
        ConfigFileName = file,
        DisplayName = "反混淆堆栈",
        Description = "异常堆栈输出时把混淆名还原为可读名，便于排查 Forge 模组问题。",
        Category = "Forge 通用差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== forge（Forge 集成设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "forge.load-early",
        ConfigFileName = file,
        DisplayName = "早期加载",
        Description = "让 SpongeForge 在 Forge 模组加载之前初始化，解决 Mixin 顺序问题，强烈建议保持 true。",
        Category = "Forge 集成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge.optimize-mod-tileentity-tracking",
        ConfigFileName = file,
        DisplayName = "优化模组方块实体追踪",
        Description = "优化 Forge 模组方块实体的因果追踪性能。",
        Category = "Forge 集成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge.use-forge-event-for-block-modification",
        ConfigFileName = file,
        DisplayName = "使用 Forge 事件处理方块修改",
        Description = "用 Forge 的 NeighborNotify 事件而非 Sponge 事件处理方块变更通知，提升模组兼容性。",
        Category = "Forge 集成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge.use-forge-player-interaction",
        ConfigFileName = file,
        DisplayName = "使用 Forge 玩家交互",
        Description = "用 Forge 玩家交互事件桥接 Sponge 事件。",
        Category = "Forge 集成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge.convert-mod-item-attributes",
        ConfigFileName = file,
        DisplayName = "转换模组物品属性",
        Description = "把 Forge 物品 NBT 属性转换为 Sponge Data API。",
        Category = "Forge 集成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge.bridge-event-bus",
        ConfigFileName = file,
        DisplayName = "桥接事件总线",
        Description = "Forge EventBus 与 Sponge EventManager 双向转发事件。",
        Category = "Forge 集成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge.convert-forge-data",
        ConfigFileName = file,
        DisplayName = "转换 Forge 数据",
        Description = "Forge NBT 数据与 Sponge DataContainer 互转。",
        Category = "Forge 集成设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== forge-mod-compatibility（模组兼容性） =====
    Register(new ServerConfigDescriptor
    {
        Key = "forge-mod-compatibility.auto-populate",
        ConfigFileName = file,
        DisplayName = "自动填充模组兼容项",
        Description = "自动为加载到的模组生成兼容性配置项。",
        Category = "模组兼容性",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge-mod-compatibility.<modid>.enabled",
        ConfigFileName = file,
        DisplayName = "启用模组兼容",
        Description = "是否对该模组启用 Sponge 桥接处理，关闭可能提升性能但失去事件。",
        Category = "模组兼容性",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge-mod-compatibility.<modid>.force-restore",
        ConfigFileName = file,
        DisplayName = "强制还原",
        Description = "模组崩溃后是否强制还原状态（高风险，调试用）。",
        Category = "模组兼容性",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== mixin（Mixin 加载设置） =====
    Register(new ServerConfigDescriptor
    {
        Key = "mixin.force-mixin-early",
        ConfigFileName = file,
        DisplayName = "强制 Mixin 早期加载",
        Description = "让 Sponge 的 Mixin 优先于其他 Coremod，解决 old mixins 警告。",
        Category = "Mixin 加载设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "mixin.ignore-mod-mixins",
        ConfigFileName = file,
        DisplayName = "忽略模组 Mixin",
        Description = "指定要忽略的模组 Mixin 配置 JSON，避免冲突。",
        Category = "Mixin 加载设置",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "mixin.debug",
        ConfigFileName = file,
        DisplayName = "Mixin 调试",
        Description = "输出 Mixin 注入详细日志。",
        Category = "Mixin 加载设置",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "mixin.env.refmap",
        ConfigFileName = file,
        DisplayName = "引用映射",
        Description = "启用 Mixin refmap，影响混淆名映射。",
        Category = "Mixin 加载设置",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== forge-permissions（Forge 权限） =====
    Register(new ServerConfigDescriptor
    {
        Key = "forge-permissions.enabled",
        ConfigFileName = file,
        DisplayName = "启用 Forge 权限桥接",
        Description = "把 Forge 注册的权限转给 Sponge 权限系统，让权限插件可管理。",
        Category = "Forge 权限",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge-permissions.default-level",
        ConfigFileName = file,
        DisplayName = "默认权限等级",
        Description = "模组未声明权限时的默认等级（4=OP 专属，0=所有人）。",
        Category = "Forge 权限",
        DefaultValue = "4",
        MinValue = 0,
        MaxValue = 4,
        ValueType = "int",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge-permissions.strict-mode",
        ConfigFileName = file,
        DisplayName = "严格模式",
        Description = "严格模式下未声明权限的模组命令一律禁止。",
        Category = "Forge 权限",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== forge-events（Forge 事件桥接） =====
    Register(new ServerConfigDescriptor
    {
        Key = "forge-events.fire-cancelable",
        ConfigFileName = file,
        DisplayName = "触发可取消事件",
        Description = "把 Forge 事件转成可取消的 Sponge 事件。",
        Category = "Forge 事件桥接",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge-events.async-events",
        ConfigFileName = file,
        DisplayName = "异步事件",
        Description = "指定哪些 Forge 事件允许异步分发，谨慎使用。",
        Category = "Forge 事件桥接",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "forge-events.coalesce",
        ConfigFileName = file,
        DisplayName = "事件合并",
        Description = "合并连续相同事件以减少分发次数。",
        Category = "Forge 事件桥接",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== phase-tracking（Forge 阶段追踪差异） =====
    Register(new ServerConfigDescriptor
    {
        Key = "phase-tracking.track-forge-block-creation",
        ConfigFileName = file,
        DisplayName = "追踪 Forge 方块创建",
        Description = "追踪 Forge 模组创建方块的因果链，开启略增开销。",
        Category = "Forge 阶段追踪",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "phase-tracking.track-forge-entity-creation",
        ConfigFileName = file,
        DisplayName = "追踪 Forge 实体创建",
        Description = "追踪 Forge 模组创建实体的因果链。",
        Category = "Forge 阶段追踪",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "phase-tracking.verbose-forge-phases",
        ConfigFileName = file,
        DisplayName = "详细 Forge 阶段日志",
        Description = "输出 Forge 阶段切换详细日志，调试用。",
        Category = "Forge 阶段追踪",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== optimizations（Forge 专属优化差异） =====
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.use-forge-lighting-fallback",
        ConfigFileName = file,
        DisplayName = "使用 Forge 光照回退",
        Description = "与 Phosphor 等光照模组冲突时回退到 Forge 光照。",
        Category = "Forge 专属优化",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.skip-mod-tick-on-overload",
        ConfigFileName = file,
        DisplayName = "过载时跳过模组 tick",
        Description = "TPS 低时跳过非关键模组的 tick，谨慎启用。",
        Category = "Forge 专属优化",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.cache-forge-capabilities",
        ConfigFileName = file,
        DisplayName = "缓存 Forge 能力",
        Description = "缓存 Forge Capability 查询结果，提升模组交互性能。",
        Category = "Forge 专属优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "optimizations.batch-forge-block-updates",
        ConfigFileName = file,
        DisplayName = "批量 Forge 方块更新",
        Description = "批量处理 Forge 模组的方块更新通知。",
        Category = "Forge 专属优化",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== entity（Forge 实体差异） =====
    Register(new ServerConfigDescriptor
    {
        Key = "entity.convert-forge-entity-data",
        ConfigFileName = file,
        DisplayName = "转换 Forge 实体数据",
        Description = "把 Forge 模组实体 NBT 转为 Sponge Data API。",
        Category = "Forge 实体差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity.use-forge-spawn-rules",
        ConfigFileName = file,
        DisplayName = "使用 Forge 生成规则",
        Description = "尊重 Forge 模组的 canSpawn 规则，关闭可能让某些模组怪物刷不出来。",
        Category = "Forge 实体差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "entity.max-mod-entity-per-chunk",
        ConfigFileName = file,
        DisplayName = "单区块模组实体上限",
        Description = "每区块 Forge 模组实体上限，0=禁用上限。",
        Category = "Forge 实体差异",
        DefaultValue = "100",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    // ===== commands（Forge 命令差异） =====
    Register(new ServerConfigDescriptor
    {
        Key = "commands.register-forge-commands",
        ConfigFileName = file,
        DisplayName = "注册 Forge 命令",
        Description = "把 Forge 模组的命令注册到 Sponge 命令系统。",
        Category = "Forge 命令差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "commands.tab-complete-forge-commands",
        ConfigFileName = file,
        DisplayName = "Forge 命令 Tab 补全",
        Description = "启用 Forge 模组命令的 Tab 自动补全。",
        Category = "Forge 命令差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "commands.legacy-forge-command-prefix",
        ConfigFileName = file,
        DisplayName = "旧版 Forge 命令前缀",
        Description = "兼容旧版用 /forge: 前缀调用模组命令。",
        Category = "Forge 命令差异",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });

    // ===== bungeecord（Forge 代理差异） =====
    Register(new ServerConfigDescriptor
    {
        Key = "bungeecord.forward-forge-mods",
        ConfigFileName = file,
        DisplayName = "转发 Forge 模组列表",
        Description = "通过 BungeeCord 转发 Forge 客户端模组列表，跨服模组必需。",
        Category = "Forge 代理差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });
    Register(new ServerConfigDescriptor
    {
        Key = "bungeecord.verify-forge-mods",
        ConfigFileName = file,
        DisplayName = "验证 Forge 模组",
        Description = "跨服时验证客户端 Forge 模组列表，防作弊。",
        Category = "Forge 代理差异",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = true
    });

    // ===== logging（Forge 日志差异） =====
    Register(new ServerConfigDescriptor
    {
        Key = "logging.log-forge-event-mismatch",
        ConfigFileName = file,
        DisplayName = "记录 Forge 事件不匹配",
        Description = "Forge 与 Sponge 事件桥接失败时输出警告。",
        Category = "Forge 日志差异",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "logging.log-mixin-failures",
        ConfigFileName = file,
        DisplayName = "记录 Mixin 失败",
        Description = "Mixin 注入失败时输出详细错误。",
        Category = "Forge 日志差异",
        DefaultValue = "true",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
    Register(new ServerConfigDescriptor
    {
        Key = "logging.log-forge-permission-misses",
        ConfigFileName = file,
        DisplayName = "记录 Forge 权限缺失",
        Description = "模组权限未声明时输出警告。",
        Category = "Forge 日志差异",
        DefaultValue = "false",
        AllowedValues = new[] { "true", "false" },
        ValueType = "bool",
        RequiresRestart = false
    });
}
