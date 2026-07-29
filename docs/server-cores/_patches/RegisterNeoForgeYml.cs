// =============================================================================
// 文件名: RegisterNeoForgeYml.cs
// 功能描述: NeoForge 服务端配置文件 neoforge-server.toml 的描述符注册方法
// 配置文件: config/neoforge-server.toml (TOML 格式；任务原名 neoforge.yml，实际为 TOML)
// 来源核心: NeoForge (https://github.com/neoforged/NeoForge)
// 适用版本: NeoForge 1.20.2 ~ 1.21.x
// 数据来源: NeoForge 1.21.x 源码 NeoForgeConfig.java
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
//           并在构造函数中调用 RegisterNeoForgeYml();
// =============================================================================

private void RegisterNeoForgeYml()
{
    const string file = "neoforge-server.toml";

    // ==================== [server] 节 —— 服务端配置 ====================
    // 注意：NeoForge 1.20.4+ 的 neoforge-server.toml 位于服务器根目录 config/ 下
    // （不同于 Forge 1.20.x 把 forge-server.toml 放在 <世界>/serverconfig/ 下）。
    // NeoForge 文件中配置项直接位于文件顶级（无 [server] 表头），但语义上仍属于服务端配置。

    Register(new ServerConfigDescriptor
    {
        Key = "removeErroringBlockEntities",
        ConfigFileName = file,
        DisplayName = "删除报错方块实体",
        Description = "设为 true 时，当某个方块实体（BlockEntity，即 TileEntity，如箱子/熔炉/模组机器）在其更新方法中抛出异常，NeoForge 会直接删除该方块实体，而不是关闭服务器并打印崩溃日志。\n⚠️ 危险选项：可能导致机器内物品丢失、方块状态错乱。\n仅作为排查「Ticking Block Entity」崩溃的应急手段临时开启，处理完务必改回 false！\nNeoForge 官方明确声明对此造成的损失不负责。",
        Category = "服务端 / 故障修复",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "removeErroringEntities",
        ConfigFileName = file,
        DisplayName = "删除报错实体",
        Description = "设为 true 时，当某个实体（Entity，如僵尸、掉落物、矿车等，不包括方块实体）在其 tick 方法中抛出异常，NeoForge 会直接删除该实体，而不是关闭服务器并打印崩溃日志。\n⚠️ 危险选项：可能导致玩家丢失骑乘的坐骑、农场中的关键生物等。\n仅作为排查「Ticking Entity」崩溃的应急手段临时开启，处理完务必改回 false！",
        Category = "服务端 / 故障修复",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "fullBoundingBoxLadders",
        ConfigFileName = file,
        DisplayName = "完整碰撞盒爬梯检测",
        Description = "设为 true 时，检测实体是否在爬梯子会检查整个实体碰撞盒所覆盖的方块，而不仅限于实体当前所在的方块。\n会带来明显的机制差异（更高的爬梯判定范围），默认保持原版行为。\n仅在你确知某些模组需要此特性时才开启。",
        Category = "服务端 / 游戏机制",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "permissionHandler",
        ConfigFileName = file,
        DisplayName = "权限处理器",
        Description = "服务器使用的权限处理器 ID。默认为 neoforge:default_handler（NeoForge 内置的默认权限处理器）。\n仅当服务器中安装了提供自定义权限系统的模组时才需要修改。\n普通开服玩家保持默认即可。错误的值会导致服务器启动失败。",
        Category = "服务端 / 权限",
        DefaultValue = "neoforge:default_handler",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "advertiseDedicatedServerToLan",
        ConfigFileName = file,
        DisplayName = "向局域网广播服务器",
        Description = "设为 true 时，专用服务端会向本地局域网广播自身存在，使同局域网下的客户端能在「多人游戏」界面自动看到这台服务器。\n公网/VPS 部署时无实际意义；本地测试时不希望他人自动看到可关闭。",
        Category = "服务端 / 网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== neoforge-common.toml —— 通用配置（同时影响客户端与服务端） ====================
    // 此处将通用配置也注册到 neoforge-server.toml 的文件名下，便于服务端管理员查阅。
    // 若需精确区分文件，可改用 ConfigFileName = "neoforge-common.toml"。

    const string commonFile = "neoforge-common.toml";

    Register(new ServerConfigDescriptor
    {
        Key = "logUntranslatedItemTagWarnings",
        ConfigFileName = commonFile,
        DisplayName = "未翻译物品标签警告模式",
        Description = "主要面向开发者：在内置服务器运行时，记录缺少翻译键（tag.item.<命名空间>.<路径>）的模组物品标签。\nSILENCED（静默，默认）= 不记录\nDEV_SHORT / DEV_LONG = 仅在开发环境中以短/长格式记录\nENABLED = 任何环境都记录\n普通开服者保持 SILENCED。",
        Category = "通用 / 开发者调试",
        DefaultValue = "SILENCED",
        AllowedValues = ["SILENCED", "DEV_SHORT", "DEV_LONG", "ENABLED"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "logLegacyTagWarnings",
        ConfigFileName = commonFile,
        DisplayName = "旧命名空间标签警告模式",
        Description = "主要面向开发者：在内置服务器运行时，记录仍在使用旧的 forge: 命名空间的模组标签。\nDEV_SHORT（默认）= 仅在开发环境中以短格式记录\nSILENCED = 不记录\nDEV_LONG = 长格式\nENABLED = 任何环境都记录\n普通开服者可改为 SILENCED 减少日志噪音。",
        Category = "通用 / 开发者调试",
        DefaultValue = "DEV_SHORT",
        AllowedValues = ["SILENCED", "DEV_SHORT", "DEV_LONG", "ENABLED"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "attributeAdvancedTooltipDebugInfo",
        ConfigFileName = commonFile,
        DisplayName = "属性高级工具提示调试",
        Description = "设为 true 时，开启「高级工具提示」（按 F3+H）后会在物品上额外显示其属性的调试信息。\n开服端一般不显示 tooltip，此项对服务端运行无影响，保持默认即可。",
        Category = "通用 / 开发者调试",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
