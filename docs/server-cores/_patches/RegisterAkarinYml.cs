// -----------------------------------------------------------------------------
// 文件名: RegisterAkarinYml.cs
// 功能描述: 注册 Akarin（基于 Paper 的多线程物理分支，已归档）配置文件的描述符
//           包含 akarin.yml 全局 settings + 每世界 world-settings 三大部分
// 数据来源: Akarin-project/Akarin README.md + 默认 akarin.yml 模板（归档版本）
// 适用版本: Akarin 1.12.2 / 1.15.2（项目已 Public archive，停更）
// -----------------------------------------------------------------------------

private void RegisterAkarinYml()
{
    const string file = "akarin.yml";

    // ==================== 信息块 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = file,
        DisplayName = "配置版本号",
        Description = "内部使用，不要手动修改\nAkarin 用它做配置自动升级与兼容性判断",
        Category = "信息",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== settings（全局设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "settings.brand-name",
        ConfigFileName = file,
        DisplayName = "服务端品牌名",
        Description = "发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码，可隐藏真实核心类型",
        Category = "全局",
        DefaultValue = "Akarin",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.enable-multi-thread",
        ConfigFileName = file,
        DisplayName = "启用多线程物理",
        Description = "Akarin 招牌开关\ntrue=启用物理多线程，把区块 ticking 分摊到多核\nfalse=退化为单线程 Paper\n⚠️ 关闭后 Akarin 与普通 Paper 无差异，建议保持 true",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.threads",
        ConfigFileName = file,
        DisplayName = "物理线程数",
        Description = "物理多线程使用的线程数\n0=自动（按 CPU 核心数估算）\n正值=固定值\n建议 ≤ 物理核心数，避免线程切换开销",
        Category = "全局",
        DefaultValue = "0",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== world-settings.default.physics（每世界：物理多线程） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.physics.async-block-physics",
        ConfigFileName = file,
        DisplayName = "异步方块物理",
        Description = "是否异步处理方块物理（沙子掉落、水流等）\ntrue=移出主线程，可省 TPS\nfalse=原版同步\n⚠️ 异步可能与某些依赖物理事件的插件冲突",
        Category = "世界-物理",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.physics.async-entity-physics",
        ConfigFileName = file,
        DisplayName = "异步实体物理",
        Description = "是否异步处理实体物理（实体移动、碰撞等）\ntrue=多线程处理大量实体\nfalse=原版同步\n⚠️ 异步实体可能影响反作弊判定",
        Category = "世界-物理",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.physics.max-async-tasks",
        ConfigFileName = file,
        DisplayName = "最大异步任务数",
        Description = "异步物理任务队列的最大长度\n值越大吞吐越高但延迟上升\n值小延迟低但可能堆积任务\n建议 2-8",
        Category = "世界-物理",
        DefaultValue = "4",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== world-settings.default.optimizations（每世界：优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimizations.disable-piston-physics",
        ConfigFileName = file,
        DisplayName = "禁用活塞物理",
        Description = "是否禁用活塞推拉方块时的物理计算\ntrue=活塞推方块不再触发物理（极省 CPU 但破坏红石机器）\nfalse=原版行为",
        Category = "世界-优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimizations.fast-leaf-decay",
        ConfigFileName = file,
        DisplayName = "快速叶子衰减",
        Description = "是否使用更快的叶子衰减算法\ntrue=省 CPU 但可能与原版叶子农场产量略有差异\nfalse=原版精确计算",
        Category = "世界-优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
