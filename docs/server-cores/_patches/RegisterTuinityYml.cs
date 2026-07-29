// -----------------------------------------------------------------------------
// 文件名: RegisterTuinityYml.cs
// 功能描述: 注册 Tuinity（基于 Paper 的高性能分支，已合并入上游 Paper）配置文件的描述符
//           包含 tuinity.yml 每世界 chunks + tick-rates + fixes + misc 四大部分
// 数据来源: StarWishsama/Tuinity README.md + 默认 tuinity.yml 模板（社区 fork）
// 适用版本: Tuinity 1.17.1 / 1.18.2（项目已停更，社区 fork 可达 1.20+）
// -----------------------------------------------------------------------------

private void RegisterTuinityYml()
{
    const string file = "tuinity.yml";

    // ==================== 信息块 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = file,
        DisplayName = "配置版本号",
        Description = "内部使用，不要手动修改\nTuinity 用它做配置自动升级与兼容性判断",
        Category = "信息",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== world-settings.default.chunks（每世界：区块加载） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.chunks.chunk-gc",
        ConfigFileName = file,
        DisplayName = "区块垃圾回收间隔",
        Description = "多久回收一次无人观察的区块（tick）\n600 = 30 秒\n调小可更快释放内存\n调大减少 IO 但内存占用高",
        Category = "世界-区块",
        DefaultValue = "600",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.chunks.delay-chunk-unloads-by",
        ConfigFileName = file,
        DisplayName = "延迟区块卸载",
        Description = "玩家离开后多久才真正卸载区块（tick）\n正值=延迟卸载，玩家短时间往返不重复加载\n0=立即卸载",
        Category = "世界-区块",
        DefaultValue = "0",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.chunks.entity-activation-range-strict-mode",
        ConfigFileName = file,
        DisplayName = "实体激活严格模式",
        Description = "是否严格按 Spigot 的实体激活范围判定\ntrue=原版行为\nfalse=使用 Tuinity 优化后的更宽松判定，可省 CPU",
        Category = "世界-区块",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.tick-rates（每世界：tick 频率） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.tick-rates.mob-spawner",
        ConfigFileName = file,
        DisplayName = "刷怪笼 tick 频率",
        Description = "刷怪笼每多少 tick 触发一次生成判定\n1=原版\n2=减半（适合大量刷怪笼的服，可大幅省 CPU）",
        Category = "世界-tick频率",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.tick-rates.sensors.behavior",
        ConfigFileName = file,
        DisplayName = "行为传感器 tick 频率",
        Description = "村民 / 生物 AI 行为传感器（如最近村民、最近玩家）的 tick 频率\n调大可降低村民密集场景的 CPU",
        Category = "世界-tick频率",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.tick-rates.grass-tick",
        ConfigFileName = file,
        DisplayName = "草生长 tick 频率",
        Description = "草方块蔓延生长的 tick 频率\n调大可省 CPU 但草生长变慢，影响自动农场产量",
        Category = "世界-tick频率",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = false
    });

    // ==================== world-settings.default.fixes（每世界：修复） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.fixes.fix-item-merge",
        ConfigFileName = file,
        DisplayName = "修复物品合并",
        Description = "是否修复多个相同物品无法合并的漏洞\ntrue=修复（推荐）\nfalse=原版行为，可能导致掉落物丢失或重复",
        Category = "世界-修复",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.fixes.prevent-moving-into-unloaded-chunks",
        ConfigFileName = file,
        DisplayName = "防止进入未加载区块",
        Description = "是否阻止玩家通过卡墙 / 加速进入未加载区块\ntrue=阻止（防止穿墙与崩溃）\nfalse=原版行为",
        Category = "世界-修复",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.misc（每世界：杂项优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.misc.use-optimized-light",
        ConfigFileName = file,
        DisplayName = "使用优化光照",
        Description = "是否使用 Tuinity 优化的光照计算引擎\ntrue=光照计算更快、内存更省\nfalse=原版光照（仅排查光照 bug 时关）",
        Category = "世界-杂项",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.misc.redstone-implementation",
        ConfigFileName = file,
        DisplayName = "红石实现",
        Description = "红石更新算法选择\nVANILLA=原版（生电兼容）\nALTERNATE=Tuinity 替代实现（更快但可能与生电机器冲突）",
        Category = "世界-杂项",
        DefaultValue = "VANILLA",
        AllowedValues = ["VANILLA", "ALTERNATE"],
        ValueType = "enum",
        RequiresRestart = true
    });
}
