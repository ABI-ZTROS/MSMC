// -----------------------------------------------------------------------------
// 文件名: RegisterHexaCordYml.cs
// 功能描述: 注册 HexaCord（基于 BungeeCord 的基岩版兼容分支）配置文件的描述符
//           包含 hexacord.yml 基岩协议 + 跨版本 + 网络层三大部分
// 数据来源: Hexacord/HexaCord README + 默认 hexacord.yml 模板
// 适用版本: HexaCord（基于 BungeeCord 1.19+ 分支，含基岩协议适配层）
// -----------------------------------------------------------------------------

private void RegisterHexaCordYml()
{
    const string file = "hexacord.yml";

    // ==================== 信息块 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = file,
        DisplayName = "配置版本号",
        Description = "内部使用，不要手动修改\nHexaCord 用它做配置自动升级与兼容性判断",
        Category = "信息",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== bedrock（基岩版协议适配） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "bedrock.enabled",
        ConfigFileName = file,
        DisplayName = "启用基岩版",
        Description = "总开关\ntrue=在 listen-port 上额外监听 UDP 基岩版流量\nfalse=只接受 Java 版连接\n开启后必须重启",
        Category = "基岩协议",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "bedrock.listen-port",
        ConfigFileName = file,
        DisplayName = "基岩版监听端口",
        Description = "基岩版客户端连接的 UDP 端口\n⚠️ 必须与 config.yml 中 Java 版 host 端口不同\n且防火墙需放行 UDP",
        Category = "基岩协议",
        DefaultValue = "19132",
        MinValue = 1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "bedrock.max-players",
        ConfigFileName = file,
        DisplayName = "基岩版玩家上限",
        Description = "同时允许的基岩版连接数上限\n0=不限制\n正数=达上限后拒绝新连接\n建议略小于后端实际承载",
        Category = "基岩协议",
        DefaultValue = "100",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "bedrock.broadcast-port",
        ConfigFileName = file,
        DisplayName = "广播端口",
        Description = "基岩版 LAN 广播与 MOTD 查询使用的端口\n通常与 listen-port 一致\n仅在内网穿透 / 多代理时需调整",
        Category = "基岩协议",
        DefaultValue = "19132",
        MinValue = 1,
        MaxValue = 65535,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "bedrock.motd",
        ConfigFileName = file,
        DisplayName = "基岩版 MOTD",
        Description = "基岩版客户端在服务器列表中看到的 MOTD 文本\n支持 § 颜色码与两行显示（用 \\n 分隔）",
        Category = "基岩协议",
        DefaultValue = "HexaCord Proxy",
        ValueType = "string",
        RequiresRestart = false
    });

    // ==================== protocol（跨版本协议） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "protocol.allow-old-clients",
        ConfigFileName = file,
        DisplayName = "允许旧版客户端",
        Description = "是否允许低于后端子服版本的 Java 客户端通过协议转换进入\ntrue=开启跨版本\nfalse=严格匹配版本",
        Category = "跨版本",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "protocol.min-version",
        ConfigFileName = file,
        DisplayName = "最低客户端版本",
        Description = "允许进入代理的最低 Java 客户端版本\n低于此版本会被直接踢出\n调高可减少协议转换开销",
        Category = "跨版本",
        DefaultValue = "1.7.2",
        ValueType = "string",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "protocol.max-version",
        ConfigFileName = file,
        DisplayName = "最高客户端版本",
        Description = "允许进入代理的最高 Java 客户端版本\n高于此版本的客户端会被踢出\n用于在 MC 新版本发布后等待适配",
        Category = "跨版本",
        DefaultValue = "1.21.x",
        ValueType = "string",
        RequiresRestart = true
    });

    // ==================== network（网络层） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "network.packet-compression-level",
        ConfigFileName = file,
        DisplayName = "数据包压缩级别",
        Description = "Netty Zlib 压缩级别\n0=不压缩（最快、最费带宽）\n9=最高压缩（最省带宽、最费 CPU）\n推荐 6 平衡",
        Category = "网络层",
        DefaultValue = "6",
        MinValue = 0,
        MaxValue = 9,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "network.use-direct-memory",
        ConfigFileName = file,
        DisplayName = "使用堆外内存",
        Description = "是否使用 Netty 堆外内存（Direct Buffer）\ntrue=减少 GC 压力，提升吞吐\nfalse=堆内存，便于调试内存泄漏",
        Category = "网络层",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });
}
