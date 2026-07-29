// -----------------------------------------------------------------------------
// 文件名: RegisterYatopiaYml.cs
// 功能描述: 注册 Yatopia（基于 Tuinity 的极限优化分支，已停更）配置文件的描述符
//           包含 yatopia.yml 全局 settings + 每世界 world-settings 三大部分
// 数据来源: YatopiaMC/Yatopia README.md + 默认 yatopia.yml 模板
// 适用版本: Yatopia 1.17.1 / 1.18.2（项目已停更）
// -----------------------------------------------------------------------------

private void RegisterYatopiaYml()
{
    const string file = "yatopia.yml";

    // ==================== 信息块 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = file,
        DisplayName = "配置版本号",
        Description = "内部使用，不要手动修改\nYatopia 用它做配置自动升级与兼容性判断",
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
        DefaultValue = "Yatopia",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.disable-connection-messages",
        ConfigFileName = file,
        DisplayName = "禁用连接消息",
        Description = "是否关闭玩家加入 / 退出的全服广播\ntrue=不再显示 XXX joined the game 类消息\nfalse=原版行为",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.use-player-luck-perms",
        ConfigFileName = file,
        DisplayName = "使用 LuckPerms 玩家缓存",
        Description = "是否直接读取 LuckPerms 玩家对象缓存（绕过 Bukkit API）\ntrue=权限查询更快\nfalse=走标准 API，兼容性更好\n未安装 LuckPerms 时务必 false",
        Category = "全局",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.fix-bridging",
        ConfigFileName = file,
        DisplayName = "修复速桥",
        Description = "是否修复速桥（Bridging）时方块放置位置异常\ntrue=修复\nfalse=还原原版时序，部分玩家可能更顺手",
        Category = "全局",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.entities（每世界：实体优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entities.disable-skeleton-ai",
        ConfigFileName = file,
        DisplayName = "禁用骷髅 AI",
        Description = "true=骷髅不再主动寻路 / 射箭，只保持原地待机\n可显著降低骷髅密集场景的 CPU 占用，但破坏玩法",
        Category = "世界-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entities.disable-zombie-ai",
        ConfigFileName = file,
        DisplayName = "禁用僵尸 AI",
        Description = "true=僵尸不再主动追击玩家 / 拆门\n同上，仅适合刷怪塔或测试服",
        Category = "世界-实体",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entities.fast-velocity-calc",
        ConfigFileName = file,
        DisplayName = "快速速度计算",
        Description = "是否使用更快的实体速度计算算法\ntrue=省 CPU，可能与原版物理略有差异\nfalse=原版精确计算",
        Category = "世界-实体",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.ticks（每世界：tick 优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.ticks.disable-tick-scheduler",
        ConfigFileName = file,
        DisplayName = "禁用 tick 调度器",
        Description = "是否禁用原版 tick 调度器改用简化实现\ntrue=省 CPU 但部分依赖调度的红石机器可能失效\nfalse=原版调度",
        Category = "世界-tick",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.ticks.optimize-hopper",
        ConfigFileName = file,
        DisplayName = "漏斗优化",
        Description = "启用 Paper 的漏斗优化\nfalse 可还原 100% 原版漏斗行为，但会破坏大量生电红石机器\n生电服可考虑 false",
        Category = "世界-tick",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.fixes（每世界：漏洞修复） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.fixes.fix-player-z-fighting",
        ConfigFileName = file,
        DisplayName = "修复玩家 Z 闪烁",
        Description = "是否修复玩家在低 Y 高速移动时的 Z 轴闪烁问题\ntrue=修复（推荐）\nfalse=原版行为",
        Category = "世界-修复",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.fixes.disable-void-fishing",
        ConfigFileName = file,
        DisplayName = "禁用虚空钓鱼",
        Description = "是否禁用虚空钓鱼漏洞\ntrue=禁用（钓鱼浮标在虚空时不再生效）\nfalse=原版行为",
        Category = "世界-修复",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
