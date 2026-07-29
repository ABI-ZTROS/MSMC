// -----------------------------------------------------------------------------
// 文件名: RegisterKaiijuYml.cs
// 功能描述: 注册 Kaiiju（基于 Folia 的原版/无政府服分支）配置文件的描述符
//           包含 kaiiju.yml 全局节 + 每世界节
// 数据来源: KaiijuMC/Kaiiju README.md (ver/1.20.1, build #240) + Configuration Wiki
// 适用版本: Kaiiju 1.20.1（项目已 Public archive，停更）
// -----------------------------------------------------------------------------

private void RegisterKaiijuYml()
{
    const string file = "kaiiju.yml";

    // ==================== region-format.linear（全局：线性格式刷新） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "region-format.linear.flush-frequency",
        ConfigFileName = file,
        DisplayName = "线性文件刷新频率",
        Description = "多久将内存中的线性 Region 数据刷新到磁盘一次（秒）\n值越小越频繁、崩服丢数据越少但 IO 越多\n值越大越省 IO 但丢数据风险越高",
        Category = "线性格式",
        DefaultValue = "10",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "region-format.linear.flush-max-threads",
        ConfigFileName = file,
        DisplayName = "刷新最大线程数",
        Description = "刷新线性 Region 文件时使用的最大线程数\n1 = 单线程刷新（安全）\n增大可加快刷新但增加磁盘 IO 争用",
        Category = "线性格式",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== network（全局：网络） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "network.send-null-entity-packets",
        ConfigFileName = file,
        DisplayName = "发送空实体移动包",
        Description = "是否发送空移动实体数据包\n设为 false 可减少网络流量\n除非有插件依赖此行为，否则建议 false",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network.alternate-keepalive",
        ConfigFileName = file,
        DisplayName = "备用心跳机制",
        Description = "沿用 Purpur 的备用心跳：每秒发送一个 keepalive 包\n仅当 30 秒内无任何响应才踢出玩家\n可避免因偶发丢包导致的误踢\n玩家不会因为丢一个心跳包就被踢",
        Category = "网络",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network.kick-player-on-bad-packet",
        ConfigFileName = file,
        DisplayName = "收到坏包踢出玩家",
        Description = "收到损坏 / 非法数据包时是否踢出玩家\n设为 false 不踢（实验性，可能被恶意客户端利用）\n无政府服可考虑 false，正常服保持 true",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== optimization（全局：优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.disable-vanish-api",
        ConfigFileName = file,
        DisplayName = "禁用隐身 API",
        Description = "禁用 Bukkit 的 Player#hidePlayer / showPlayer 隐身 API\n无隐身需求的服务器可设 true 以节省性能",
        Category = "优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.disable-player-stats",
        ConfigFileName = file,
        DisplayName = "禁用玩家统计",
        Description = "禁用玩家统计信息（如走了多少格、挖了多少方块）的记录与持久化\n无政府 / 战斗服通常不需要统计，可设 true 提速",
        Category = "优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.disable-arm-swing-event",
        ConfigFileName = file,
        DisplayName = "禁用手臂挥动事件",
        Description = "不调用 PlayerArmSwingEvent\n若没有插件监听此事件（绝大多数服都没有），可设 true 减少事件开销",
        Category = "优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.async-path-processing.enable",
        ConfigFileName = file,
        DisplayName = "启用异步寻路",
        Description = "是否启用异步寻路处理\n⚠️ 修改必须重启，热重载无效\n开启后实体寻路移至异步线程池，可显著降低主线程负载\nKaiiju 修复并重构了 Petal 的异步寻路",
        Category = "优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.async-path-processing.max-threads",
        ConfigFileName = file,
        DisplayName = "异步寻路最大线程数",
        Description = "寻路线程池最大线程数\n0 = 自动 (max(核心数/4, 1))\n负数 -n = max(核心数 − n, 1)\n正数 = 固定值\n允许线程池在突发负载时临时扩张到该上限",
        Category = "优化",
        DefaultValue = "0",
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.async-path-processing.keepalive",
        ConfigFileName = file,
        DisplayName = "空闲线程存活时间",
        Description = "当线程数超过核心池大小时，多余空闲线程的存活秒数\n短存活时间可快速回收多余线程，长存活时间可应对频繁突发",
        Category = "优化",
        DefaultValue = "60",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "optimization.async-path-processing.queue-capacity",
        ConfigFileName = file,
        DisplayName = "任务队列容量",
        Description = "寻路任务等待队列的最大长度\n队列满后才会创建新线程（直到 max-threads）\n大队列可吸收突发任务而不创建过多线程，但会增加延迟",
        Category = "优化",
        DefaultValue = "4096",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== gameplay（全局：玩法） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay.server-mod-name",
        ConfigFileName = file,
        DisplayName = "服务端名称",
        Description = "发送给客户端的服务端品牌名（F3 界面显示的 Mod 字段）\n可用于品牌定制或隐藏真实核心类型",
        Category = "玩法",
        DefaultValue = "Kaiiju",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "gameplay.shared-random-for-players",
        ConfigFileName = file,
        DisplayName = "玩家共享随机源",
        Description = "玩家共用同一个随机数生成器，而非每个玩家独立 RNG\n这是原版 RNG 操纵（RNG manipulation）的关键\n开启时所有玩家共享 RNG，可被用于预测 / 操纵随机事件（如掉落、生物生成）\n无政府服保持 true 以允许 RNG 控制",
        Category = "玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== unsupported（全局：不安全实验） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "unsupported.disable-ensure-tick-thread-checks",
        ConfigFileName = file,
        DisplayName = "禁用线程检查",
        Description = "禁用 Folia 的「确保在正确 tick 线程」安全检查\n⚠️ 绝对不要开启，会导致数据竞争与崩溃\n仅用于调试",
        Category = "不安全实验",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "unsupported.global-event-synchronization",
        ConfigFileName = file,
        DisplayName = "全局事件同步",
        Description = "启用全局事件同步锁\n会显著降低多线程性能，仅用于排查事件竞态问题",
        Category = "不安全实验",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== world-settings.default.region-format（每世界：区域文件格式） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.region-format.format",
        ConfigFileName = file,
        DisplayName = "区域文件格式",
        Description = "世界在磁盘上使用的 Region 文件格式\nANVIL = Minecraft 原生 .mca 格式（兼容性最好）\nLINEAR = Xymb 线性格式（主世界/下界省 ~50% 磁盘，末地省 ~95%）\n⚠️ Linear 与 ANVIL 不兼容，切换前必须用 LinearRegionFileFormatTools 转换数据，否则世界会丢失",
        Category = "世界-区域格式",
        DefaultValue = "ANVIL",
        AllowedValues = ["ANVIL", "LINEAR"],
        ValueType = "enum",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.region-format.linear.compression-level",
        ConfigFileName = file,
        DisplayName = "Linear 压缩级别",
        Description = "Linear 格式使用的 ZSTD 压缩级别\n推荐 1 / 3 / 6\n级别越高磁盘越省但 CPU 越高\n实测：级别 1 总占用 7.88GB，级别 6 仅 6.59GB（省约 16%）",
        Category = "世界-区域格式",
        DefaultValue = "1",
        MinValue = 1,
        MaxValue = 22,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.region-format.linear.crash-on-broken-symlink",
        ConfigFileName = file,
        DisplayName = "符号链接损坏时崩溃",
        Description = "当 Region 文件的符号链接损坏时是否让服务器崩溃\ntrue（推荐）= 崩溃以暴露问题\nfalse = 静默跳过\n通过 NFS 访问 Region 文件时建议 true",
        Category = "世界-区域格式",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== world-settings.default.optimization（每世界：优化） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimization.shulker-box-drop-contents-when-destroyed",
        ConfigFileName = file,
        DisplayName = "潜影盒被毁掉落内容",
        Description = "潜影盒被熔岩 / 仙人掌等摧毁时，是否掉落其内部物品\ntrue = 原版行为\nfalse = 内容物一并销毁",
        Category = "世界-优化",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimization.optimize-hoppers",
        ConfigFileName = file,
        DisplayName = "漏斗优化",
        Description = "启用 Paper 的漏斗优化\nfalse 可还原 100% 原版漏斗行为，但会破坏大量生电红石机器\n生电服可考虑 false",
        Category = "世界-优化",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimization.tick-when-empty",
        ConfigFileName = file,
        DisplayName = "空世界仍 tick",
        Description = "世界无玩家时是否仍进行 tick（实体、红石等）\nfalse = 无玩家时世界冻结，省 CPU 但红石机器会停",
        Category = "世界-优化",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimization.enable-entity-throttling",
        ConfigFileName = file,
        DisplayName = "实体节流",
        Description = "启用实体数量节流\n开启后超限的实体会被限制 / 移除\n具体限制在 kaiiju-entity-limits.yml 中配置",
        Category = "世界-优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimization.disable-achievements",
        ConfigFileName = file,
        DisplayName = "禁用成就",
        Description = "禁用成就 / 进度系统的触发与记录\n无政府服可设 true 提速",
        Category = "世界-优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimization.disable-creatures-spawn-events",
        ConfigFileName = file,
        DisplayName = "禁用生物生成事件",
        Description = "不触发 CreatureSpawnEvent\n无插件监听时可设 true 减少事件开销\n但反作弊 / 限制类插件会失效",
        Category = "世界-优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.optimization.disable-dolphin-swim-to-treasure",
        ConfigFileName = file,
        DisplayName = "禁用海豚寻宝",
        Description = "禁用海豚引导玩家寻找沉船 / 海底废墟的行为\n可减少海豚寻路计算开销",
        Category = "世界-优化",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== world-settings.default.gameplay（每世界：玩法 / 漏洞开关） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.gameplay.fix-void-trading",
        ConfigFileName = file,
        DisplayName = "修复虚空交易",
        Description = "是否修复虚空交易漏洞\ntrue（默认）= 修复\nfalse = 允许虚空交易\n若关闭，建议安装 Kaiivoid 插件替代",
        Category = "世界-玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.gameplay.break-redstone-on-top-of-trap-doors-early",
        ConfigFileName = file,
        DisplayName = "提前破坏活板门上红石",
        Description = "始终提前破坏活板门上的红石\nfalse 会允许「门切片（portal slicing）」与活板门卡服机器\n生电服可设 false 还原漏洞",
        Category = "世界-玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.gameplay.fix-tripwire-state-inconsistency",
        ConfigFileName = file,
        DisplayName = "修复绊线状态不一致",
        Description = "修复绊线状态不一致\nfalse 会启用线复制漏洞，并允许末地黑曜石平台抑制",
        Category = "世界-玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.gameplay.safe-teleportation",
        ConfigFileName = file,
        DisplayName = "安全传送",
        Description = "true = 末地传送门只传送活着的实体（修复刷沙）\nfalse = 允许末地传送门传送已移除的实体（刷沙前置）\n要开启刷沙必须设为 false",
        Category = "世界-玩法",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.gameplay.sand-duplication",
        ConfigFileName = file,
        DisplayName = "沙子复制",
        Description = "允许沙子复制漏洞\n⚠️ 前置条件：必须同时将 safe-teleportation 设为 false 才能生效\n无政府刷沙服开启",
        Category = "世界-玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.gameplay.teleport-async-on-high-velocity",
        ConfigFileName = file,
        DisplayName = "高速时异步传送",
        Description = "玩家高速移动（高速度）时使用异步传送\n实验性，可能改善高速场景下的传送稳定性",
        Category = "世界-玩法",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
