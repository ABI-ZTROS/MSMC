// -----------------------------------------------------------------------------
// 文件名: RegisterFlameCordYml.cs
// 功能描述: 注册 FlameCord（基于 BungeeCord 的反机器人分支）配置文件的描述符
//           包含 flamecord.yml 反机器人 + 防火墙 + 防重连三大部分
// 数据来源: 4drian3d/FlameCord README + 默认 flamecord.yml 模板
// 适用版本: FlameCord（基于 BungeeCord 1.19+ 分支）
// -----------------------------------------------------------------------------

private void RegisterFlameCordYml()
{
    const string file = "flamecord.yml";

    // ==================== 信息块 ====================

    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = file,
        DisplayName = "配置版本号",
        Description = "内部使用，不要手动修改\nFlameCord 用它做配置自动升级与兼容性判断",
        Category = "信息",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== antibot（反机器人模块） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "antibot.enabled",
        ConfigFileName = file,
        DisplayName = "启用反机器人",
        Description = "FlameCord AntiBot 总开关\ntrue=启用内置反机器人\nfalse=完全关闭，退化为普通 BungeeCord\n被攻击时务必 true",
        Category = "反机器人",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "antibot.check-accounts",
        ConfigFileName = file,
        DisplayName = "检查账户爆破",
        Description = "是否启用账户频率检测\ntrue=限制单 IP 在窗口内尝试登录不同账号的次数，可防撞库\nfalse=不检测",
        Category = "反机器人",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "antibot.max-accounts-per-ip",
        ConfigFileName = file,
        DisplayName = "单 IP 最大账号数",
        Description = "同一 IP 在窗口时间内最多尝试登录多少个不同账号\n超过此值会被视为机器人并踢出 / 封禁",
        Category = "反机器人",
        DefaultValue = "3",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "antibot.accounts-per-second",
        ConfigFileName = file,
        DisplayName = "账号请求频率",
        Description = "单 IP 每秒最多尝试登录的账号次数\n值越小越严格，但可能误杀家庭网络共享 IP 的玩家",
        Category = "反机器人",
        DefaultValue = "2",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "antibot.max-connections-per-ip",
        ConfigFileName = file,
        DisplayName = "单 IP 最大连接数",
        Description = "同一 IP 同时允许的未完成握手连接数\n超过此值的连接会被直接丢弃，防止 TCP 连接洪水",
        Category = "反机器人",
        DefaultValue = "5",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "antibot.connections-per-second",
        ConfigFileName = file,
        DisplayName = "连接请求频率",
        Description = "单 IP 每秒最多发起新连接的次数\n建议与正常玩家进入频率匹配，过低会误杀玩家",
        Category = "反机器人",
        DefaultValue = "4",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== firewall（防火墙模块） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "firewall.enabled",
        ConfigFileName = file,
        DisplayName = "启用防火墙",
        Description = "Netty 层流量限速总开关\ntrue=启用 L4 层防护\nfalse=关闭，所有连接直通代理主线程",
        Category = "防火墙",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "firewall.max-rate",
        ConfigFileName = file,
        DisplayName = "最大速率",
        Description = "单 IP 每秒允许通过的最大数据包数\n超过此速率的包会被丢弃，可有效缓解坏包攻击（BadPacket）",
        Category = "防火墙",
        DefaultValue = "10",
        MinValue = 1,
        ValueType = "int",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "firewall.timeout",
        ConfigFileName = file,
        DisplayName = "超时时间",
        Description = "单连接无数据传输的超时时间（毫秒）\n超过此值无响应的连接会被关闭，可释放僵尸连接占用",
        Category = "防火墙",
        DefaultValue = "5000",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });

    // ==================== reconnect-handler（防快速重连模块） ====================

    Register(new ServerConfigDescriptor
    {
        Key = "reconnect-handler.enabled",
        ConfigFileName = file,
        DisplayName = "启用防重连",
        Description = "总开关\ntrue=被踢出后短时间内禁止重连\nfalse=允许立即重连，会被机器人利用绕过 AntiBot",
        Category = "防重连",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = true
    });

    Register(new ServerConfigDescriptor
    {
        Key = "reconnect-handler.time",
        ConfigFileName = file,
        DisplayName = "重连冷却时间",
        Description = "被踢出 / 封禁后再次允许连接的间隔（秒）\n值越大越安全，但正常玩家被误杀后等待越久",
        Category = "防重连",
        DefaultValue = "600",
        MinValue = 0,
        ValueType = "int",
        RequiresRestart = true
    });
}
