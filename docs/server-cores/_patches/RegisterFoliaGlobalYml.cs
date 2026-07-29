// -----------------------------------------------------------------------------
// 文件名: RegisterFoliaGlobalYml.cs
// 功能描述: 注册 Folia 配置文件的描述符
//           ⚠️ Folia 不存在独立的 config/folia-global.yml，所有 Folia 新增多线程
//           区域配置（ThreadedRegions）直接追加到 Paper 的 config/paper-global.yml
//           本文件仅注册 Folia 新增的 threaded-regions 节 + Folia 部署高频调优项
// 数据来源: PaperMC/Folia folia-server/paper-patches/features/0001-Region-Threading-Base.patch
//           （commit e48800d，Folia 26.x）+ 官方 FAQ 线程分配建议
// 适用版本: Folia 1.20.4+ / 26.x
// -----------------------------------------------------------------------------

private void RegisterFoliaGlobalYml()
{
    // ⚠️ Folia 配置追加到 paper-global.yml，不存在独立的 folia-global.yml
    const string file = "config/paper-global.yml";

    // ==================== threaded-regions（Folia 新增：多线程核心） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "threaded-regions.threads",
        ConfigFileName = file,
        DisplayName = "区域 tick 线程数",
        Description = "区域 tick 循环所使用的线程池大小\n-1 = 自动（根据可用 CPU 计算）\n手动设置：建议设为「物理核心数 − Netty IO − 区块 IO − 区块工作 − GC 并发」后的剩余值\n⚠️ 所有可配置线程总和不应超过物理核心数的 80%\n例：32 核 / 500 人服可设约 10\nFolia 多线程核心配置，性能调优第一项",
        Category = "线程化区域",
        DefaultValue = "-1",
        MinValue = -1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "threaded-regions.grid-exponent",
        ConfigFileName = file,
        DisplayName = "区域网格指数",
        Description = "控制区域划分的网格粒度\n每个网格单元边长 = 2^gridExponent 个区块\n默认 4 = 16 区块边长（256 区块为一网格单元）\n值越大区域越大、并行度越低；值越小区域越碎、并行度越高但跨区域开销越大\n⚠️ 非高级用户请勿修改，错误值会显著降低性能",
        Category = "线程化区域",
        DefaultValue = "4",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "threaded-regions.scheduler",
        ConfigFileName = file,
        DisplayName = "区域调度算法",
        Description = "区域 tick 任务的调度策略\nEDF = Earliest Deadline First（最早截止期优先），按 tick 截止时间排序优先调度最紧迫的区域\n目前仅 EDF 一种已实现值",
        Category = "线程化区域",
        DefaultValue = "EDF",
        AllowedValues = ["EDF"],
        ValueType = "enum",
        RequiresRestart = true
    });

    // ==================== chunk-system（Paper 继承：Folia 需重新分配预算） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-system.io-threads",
        ConfigFileName = file,
        DisplayName = "区块 IO 线程数",
        Description = "负责从磁盘读写区块文件的线程数\nFolia 官方建议：每 200-300 名玩家约 3 个\n预生成世界后可适当下调\n需计入 80% 总线程预算",
        Category = "区块系统",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "chunk-system.worker-threads",
        ConfigFileName = file,
        DisplayName = "区块工作线程数",
        Description = "负责区块生成 / 装饰计算的线程数\nFolia 官方建议：预生成后每 200-300 名玩家约 2 个\n未预生成时需大幅增加（曾测试 16 线程仍偏慢）\n需计入 80% 总线程预算\n强烈建议上线前用 Chunky 预生成世界",
        Category = "区块系统",
        DefaultValue = "1",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== misc（Paper 继承：杂项） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "misc.region-file-cache-size",
        ConfigFileName = file,
        DisplayName = "区域文件缓存大小",
        Description = "缓存的 Region 文件（.mca）句柄数\n大型世界 / 玩家分散时调大（如 512）可减少磁盘 IO\n但占用更多内存",
        Category = "杂项",
        DefaultValue = "256",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== proxies.velocity（Paper 继承：Velocity 代理） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "proxies.velocity.enabled",
        ConfigFileName = file,
        DisplayName = "启用 Velocity 转发",
        Description = "是否启用 Velocity 现代转发（modern forwarding）\n启用后玩家信息由 Velocity 转发，Folia 侧 server.properties 的 online-mode 应设为 false\n前置 Velocity 代理时开启",
        Category = "代理",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "proxies.velocity.secret",
        ConfigFileName = file,
        DisplayName = "Velocity 共享密钥",
        Description = "与 Velocity forwarding.secret 一致的密钥，用于验证代理身份\n⚠️ 生产环境必须设置强密钥，留空则任何人都可伪造玩家身份\n留空 = 禁用",
        Category = "代理",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "proxies.velocity.online-mode",
        ConfigFileName = file,
        DisplayName = "在线模式（Velocity 侧）",
        Description = "表示 Velocity 是否已做 Mojang 正版验证\n设为 true 时 Folia 信任 Velocity 转发的正版身份",
        Category = "代理",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== spigot.yml（Netty 线程，Folia 调优相关） ====================
    // 注意：此项在 spigot.yml，但与 Folia 线程分配强相关，故在此注册

    Register(new ServerConfigDescriptor
    {
        Key = "settings.netty-threads",
        ConfigFileName = "spigot.yml",
        DisplayName = "Netty IO 线程数",
        Description = "处理玩家网络数据包的 Netty 线程数\nFolia 官方建议：每 200-300 名玩家约 4 个\n500 人服可设 8\n需计入 80% 总线程预算\n⚠️ 注意此项在 spigot.yml 而非 paper-global.yml",
        Category = "网络",
        DefaultValue = "4",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });
}
