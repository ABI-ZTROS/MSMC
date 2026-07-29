// -----------------------------------------------------------------------------
// 文件名: RegisterWaterfallYml.cs
// 功能描述: 注册 Waterfall（PaperMC 维护的 BungeeCord 分支，已归档）配置文件的描述符
//           包含 waterfall.yml 日志 + MOTD + 网络 + 限流四大部分
// 数据来源: PaperMC/Waterfall README + 默认 waterfall.yml 模板（最终归档版本）
// 适用版本: Waterfall 1.20.x（项目已归档，停更）
// -----------------------------------------------------------------------------

private void RegisterWaterfallYml()
{
    const string file = "waterfall.yml";

    // ==================== 信息块 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = file,
        DisplayName = "配置版本号",
        Description = "内部使用，不要手动修改\nWaterfall 用它做配置自动升级与兼容性判断",
        Category = "信息",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== log（日志设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "log_initial_handler_logs",
        ConfigFileName = file,
        DisplayName = "初始连接日志",
        Description = "是否记录玩家建立连接时的初始 Netty Handler 日志\ntrue=记录（便于排查握手问题）\nfalse=关闭以减少日志噪音",
        Category = "日志",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "log_pings",
        ConfigFileName = file,
        DisplayName = "Ping 请求日志",
        Description = "是否记录客户端对代理的 ping 请求（即服务器列表刷新触发的 ping）\n关闭可大幅减少日志量",
        Category = "日志",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== motd-sample（MOTD 与玩家样本） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "force_empty_motd",
        ConfigFileName = file,
        DisplayName = "强制空 MOTD",
        Description = "true=忽略 config.yml 中 listeners.motd，服务器列表始终显示空 MOTD\n适合子服列表不希望被外部探测的场景",
        Category = "MOTD与样本",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "force_empty_player_sample",
        ConfigFileName = file,
        DisplayName = "强制空玩家样本",
        Description = "true=服务器列表不再显示在线玩家头像与名字\n可隐藏玩家身份，避免被外挂工具批量探测",
        Category = "MOTD与样本",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "sample_count",
        ConfigFileName = file,
        DisplayName = "玩家样本数量",
        Description = "服务器列表显示的在线玩家头像 / 名字数量\n调小可减少数据包大小\n0=不显示任何玩家",
        Category = "MOTD与样本",
        DefaultValue = "12",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });

    // ==================== network（网络设置） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "disable_tab_list_rewrite",
        ConfigFileName = file,
        DisplayName = "禁用 Tab 重写",
        Description = "是否禁用代理对 Tab 列表的强制重写\ntrue=把 Tab 列表交还给后端子服控制（适合 GLOBAL 模式异常的服）\nfalse=由代理统一管理",
        Category = "网络",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "use_netty_dns_resolver",
        ConfigFileName = file,
        DisplayName = "使用 Netty DNS 解析器",
        Description = "是否使用 Netty 自带的异步 DNS 解析器（而非 JDK 同步解析）\ntrue=解析更快、不阻塞主线程\nfalse=退回 JDK 解析，便于排查 DNS 问题",
        Category = "网络",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    // ==================== throttling（限流） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "throttling.tabcomplete",
        ConfigFileName = file,
        DisplayName = "Tab 补全限流",
        Description = "同一玩家两次 Tab 补全请求之间的最小间隔（毫秒）\n防止恶意客户端通过疯狂 Tab 补全窃取命令列表或刷 CPU",
        Category = "限流",
        DefaultValue = "1000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = false
    });
}
