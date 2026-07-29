// -----------------------------------------------------------------------------
// 文件名: RegisterArclightYml.cs
// 功能描述: 注册 Arclight（混合端）配置文件的描述符
//           ⚠️ 注意：实际配置文件名为 arclight.conf（HOCON 格式），不是 .yml
//           方法名沿用 RegisterArclightYml 以保持命名一致性
// 数据来源: IzzelAliz/Arclight arclight-common/src/main/java/io/izzel/arclight/config/ArclightConfig.java
// 适用版本: Arclight 1.20.1（master 分支）
// -----------------------------------------------------------------------------

private void RegisterArclightYml()
{
    // ⚠️ 真实文件名是 arclight.conf（HOCON 格式），不是 yml
    const string file = "arclight.conf";

    // ==================== 通用设置 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.setdefaultlocale",
        ConfigFileName = file,
        DisplayName = "设置默认区域语言",
        Description = "是否强制将服务器的默认区域设置为系统区域（而非 en_US）\n影响部分插件的本地化文本",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.bukkit-version",
        ConfigFileName = file,
        DisplayName = "Bukkit API 版本",
        Description = "Arclight 内部使用的 Bukkit API 版本号\n由 Arclight 自动写入，请勿手动修改",
        Category = "通用设置",
        DefaultValue = "1.20.1-R0.1-SNAPSHOT",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.bukkit-version-override",
        ConfigFileName = file,
        DisplayName = "强制覆盖 Bukkit 版本",
        Description = "强制覆盖对插件声明的 Bukkit 版本号\n仅在插件因版本检查拒绝加载时使用",
        Category = "通用设置",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.api-version-check",
        ConfigFileName = file,
        DisplayName = "API 版本检查",
        Description = "是否对插件进行 Bukkit API 版本兼容性检查\n关闭后所有插件无视版本声明强制加载（可能导致崩溃）",
        Category = "通用设置",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.verbose",
        ConfigFileName = file,
        DisplayName = "详细日志输出",
        Description = "是否启用 Arclight 详细日志（包含 Mixin 注入、事件桥接等调试信息）\n排查兼容性问题时开启",
        Category = "通用设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 性能与并发 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.async-tick.enabled",
        ConfigFileName = file,
        DisplayName = "异步 tick 模式",
        Description = "实验性：是否启用异步 tick 模式（部分世界逻辑异步执行）\n⚠️ 极不稳定，与绝大多数 Forge 模组冲突\n强烈不建议开启",
        Category = "性能与并发",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.disable-flush",
        ConfigFileName = file,
        DisplayName = "禁用批量刷新",
        Description = "是否禁用网络数据包批量刷新\n开启可能减少延迟但增加带宽\n一般保持 false",
        Category = "性能与并发",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.disable-watchdog",
        ConfigFileName = file,
        DisplayName = "禁用看门狗",
        Description = "是否禁用 watchdog 主线程监控\n⚠️ 不推荐，模组卡死将无报警",
        Category = "性能与并发",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.optimize-entity-portal",
        ConfigFileName = file,
        DisplayName = "优化实体传送门",
        Description = "是否优化实体穿越传送门（下界/末地）的处理逻辑\n开启可减少传送门附近的卡顿",
        Category = "性能与并发",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 兼容性与事件桥接 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.capture-compound",
        ConfigFileName = file,
        DisplayName = "捕获 NBT 复合事件",
        Description = "是否捕获模组方块的 NBT 复合数据用于 Bukkit 事件\n开启可让 ChestShop 等插件识别模组方块，但增加少量开销",
        Category = "兼容性",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.event-transformation",
        ConfigFileName = file,
        DisplayName = "事件类型转换",
        Description = "是否启用 Forge ↔ Bukkit 事件类型自动转换\n关闭后大量 Bukkit 插件将无法响应模组事件\n务必保持 true",
        Category = "兼容性",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.entity-spawn.unique-id",
        ConfigFileName = file,
        DisplayName = "实体生成唯一 ID",
        Description = "是否为模组生成的实体分配 Bukkit 兼容的唯一 UUID\n开启可让 RPG/统计类插件识别模组实体",
        Category = "兼容性",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== 命令与权限 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "arclight.command.no-permission-message",
        ConfigFileName = file,
        DisplayName = "无权限提示消息",
        Description = "玩家无权限执行 Arclight 内置命令时显示的提示文本\n支持 & 颜色代码",
        Category = "命令与权限",
        DefaultValue = "You do not have permission to use this command.",
        ValueType = "string",
        RequiresRestart = true
    });
}
