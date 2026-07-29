// -----------------------------------------------------------------------------
// 文件名: RegisterAirplaneYml.cs
// 功能描述: 注册 Airplane（基于 Paper 的优化分支，已停更）配置文件的描述符
//           包含 airplane.yml 全局 airplane + 每世界 world-settings 三大部分
// 数据来源: TECHNOVE/Airplane README.md + 默认 airplane.yml 模板
// 适用版本: Airplane 1.17.1 / 1.18.2（项目已停更）
// -----------------------------------------------------------------------------

private void RegisterAirplaneYml()
{
    const string file = "airplane.yml";

    // ==================== 信息块 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = file,
        DisplayName = "配置版本号",
        Description = "内部使用，不要手动修改\nAirplane 用它做配置自动升级与兼容性判断",
        Category = "信息",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== airplane（全局优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "airplane.brand-name",
        ConfigFileName = file,
        DisplayName = "服务端品牌名",
        Description = "发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码，可隐藏真实核心类型",
        Category = "全局",
        DefaultValue = "Airplane",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "airplane.allow-unsafe-commands",
        ConfigFileName = file,
        DisplayName = "允许不安全命令",
        Description = "是否允许执行可能引发性能问题或不安全的内置调试命令\ntrue=允许（仅适合开发 / 测试服）\nfalse=禁用（生产服保持）",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.chunks（每世界：区块优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.chunks.chunk-load-cooldown",
        ConfigFileName = file,
        DisplayName = "区块加载冷却",
        Description = "玩家触发区块加载后再次允许加载的间隔（tick）\n0=无冷却\n正值=降低区块加载频率，可缓解突发加载导致的卡顿",
        Category = "世界-区块",
        DefaultValue = "0",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.chunks.autosave-period",
        ConfigFileName = file,
        DisplayName = "自动保存周期",
        Description = "自动保存世界数据的间隔（tick）\n6000 = 5 分钟\n调大省 IO 但崩服丢数据更多；调小反之",
        Category = "世界-区块",
        DefaultValue = "6000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.chunks.max-chunk-sends-per-tick",
        ConfigFileName = file,
        DisplayName = "每 tick 最大区块发送数",
        Description = "每 tick 向玩家发送的最大区块包数\n0=不限制\n正值=限速，可避免进服时网络尖峰",
        Category = "世界-区块",
        DefaultValue = "0",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    // ==================== world-settings.default.entities（每世界：实体优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entities.spawn-packet-queue",
        ConfigFileName = file,
        DisplayName = "生成包排队",
        Description = "是否把实体生成数据包排队发送\ntrue=平滑网络峰值，避免一次性发送大量实体导致客户端卡顿\nfalse=原版行为",
        Category = "世界-实体",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entities.dab.enabled",
        ConfigFileName = file,
        DisplayName = "启用 DAB 实体激活",
        Description = "是否启用 Airplane 改进的动态实体激活（DAB）\ntrue=远离玩家的实体降低 tick 频率以省 CPU\nfalse=原版固定激活范围",
        Category = "世界-实体",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== world-settings.default.fixes（每世界：修复） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.fixes.fix-coordinate-exploit",
        ConfigFileName = file,
        DisplayName = "修复坐标泄露漏洞",
        Description = "是否修复通过传送包反推远处坐标的漏洞\ntrue=修复（推荐）\nfalse=允许玩家通过特定客户端作弊获取远距离方块位置",
        Category = "世界-修复",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.fixes.prevent-double-pistons",
        ConfigFileName = file,
        DisplayName = "防止双活塞卡服",
        Description = "是否防止双活塞同时激活导致的卡服机器\ntrue=防止（推荐）\nfalse=原版行为，可能被用于恶意卡服",
        Category = "世界-修复",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
