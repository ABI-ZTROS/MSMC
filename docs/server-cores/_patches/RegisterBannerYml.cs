// -----------------------------------------------------------------------------
// 文件名: RegisterBannerYml.cs
// 功能描述: 注册 Banner（Fabric 混合端）配置文件的描述符
//           对应 banner.yml
// 数据来源: MohistMC/Banner BannerConfig.java
// 适用版本: Banner 1.20.1（master 分支）
// 注意: Banner 是 Fabric+Bukkit 混合（区别于其他 Forge 系混合端）
//       2025年7月后项目部分分支更名为 Taiyitist，本描述符仍以原始 Banner 为准
// -----------------------------------------------------------------------------

private void RegisterBannerYml()
{
    const string file = "banner.yml";

    // ==================== 通用设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "banner.lang",
        ConfigFileName = file,
        DisplayName = "控制台语言",
        Description = "Banner 启动日志与控制台提示所使用的语言\n仅影响 Banner 自身日志，不影响 Minecraft 原版日志",
        Category = "通用设置",
        DefaultValue = "en_US",
        AllowedValues = ["en_US", "zh_CN", "fr_FR", "es_ES", "de_DE", "ja_JP", "ko_KR", "ru_RU", "pt_BR", "zh_TW"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.check_update",
        ConfigFileName = file,
        DisplayName = "检查 Banner 更新",
        Description = "启动时是否联网检查 Banner 新版本",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.metrics",
        ConfigFileName = file,
        DisplayName = "bStats 统计上报",
        Description = "是否启用 bStats 匿名数据上报\n建议保持开启帮助开发者了解使用情况",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.show_logo",
        ConfigFileName = file,
        DisplayName = "启动显示 Logo",
        Description = "控制台启动时是否打印 Banner ASCII Logo",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.bukkit-version",
        ConfigFileName = file,
        DisplayName = "Bukkit API 版本",
        Description = "Banner 内部使用的 Bukkit API 版本号\n由 Banner 自动写入，请勿手动修改",
        Category = "通用设置",
        DefaultValue = "1.20.1-R0.1-SNAPSHOT",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.bukkit-version-override",
        ConfigFileName = file,
        DisplayName = "强制覆盖 Bukkit 版本",
        Description = "强制覆盖对插件声明的 Bukkit 版本号\n仅在插件因版本检查拒绝加载时使用",
        Category = "通用设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });

    // ==================== 兼容性设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "banner.disable_plugins_blacklist",
        ConfigFileName = file,
        DisplayName = "禁用插件黑名单",
        Description = "Banner 维护了一份已知与混合端不兼容的插件黑名单\n设为 true 跳过该检查（不推荐，可能导致崩溃）",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.disable_mods_blacklist",
        ConfigFileName = file,
        DisplayName = "禁用模组黑名单",
        Description = "跳过 Banner 维护的已知不兼容 Fabric 模组黑名单\n不推荐，可能导致崩溃",
        Category = "兼容性",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.support_non_paper_plugins",
        ConfigFileName = file,
        DisplayName = "允许非 Paper 系插件",
        Description = "是否允许加载仅声明支持 Spigot/CraftBukkit 的插件",
        Category = "兼容性",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 性能优化 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "banner.async-tick",
        ConfigFileName = file,
        DisplayName = "异步 tick 模式",
        Description = "实验性：是否启用异步 tick 模式\n⚠️ 与部分 Fabric 模组（如 Lithium）可能冲突\n强烈不建议开启",
        Category = "性能优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.disable-watchdog",
        ConfigFileName = file,
        DisplayName = "禁用看门狗",
        Description = "是否禁用 watchdog 主线程监控\n⚠️ 不推荐，模组卡死将无报警",
        Category = "性能优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.entity-activation-range",
        ConfigFileName = file,
        DisplayName = "实体激活范围优化",
        Description = "是否启用实体激活范围优化（远离玩家的实体降低 tick 频率）\n与 Lithium 类似模组可能重复优化，建议二选一",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.use-Spark-and-Sync-Timer",
        ConfigFileName = file,
        DisplayName = "Spark 计时器",
        Description = "是否启用 Banner 内置的同步计时器（用于性能分析）\nSpark 插件依赖此功能",
        Category = "性能优化",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 事件桥接 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "banner.event-transformation",
        ConfigFileName = file,
        DisplayName = "事件类型转换",
        Description = "是否启用 Fabric ↔ Bukkit 事件类型自动转换\n关闭后大量 Bukkit 插件将无法响应模组事件\n务必保持 true",
        Category = "事件桥接",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "banner.capture-compound",
        ConfigFileName = file,
        DisplayName = "捕获 NBT 复合事件",
        Description = "是否捕获模组方块的 NBT 复合数据用于 Bukkit 事件\n开启可让 ChestShop 等插件识别模组方块",
        Category = "事件桥接",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });
}
