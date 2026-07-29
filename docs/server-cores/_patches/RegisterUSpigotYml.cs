// -----------------------------------------------------------------------------
// 文件名: RegisterUSpigotYml.cs
// 功能描述: 注册 USpigot 配置文件的描述符
//           ⚠️ USpigot 无可访问的官方 GitHub 仓库与公开源码，仅在 MineBBS 等国内
//           社区以二进制 jar 分发。本文件所有配置项均为基于 Spigot/Paper 分支惯例
//           的推断项，未经官方源码核实。请勿作为权威依据。
// 数据来源: ⚠️ 无官方源码；基于 NachoSpigot/Pufferfish 等同类分支命名惯例推断
// 适用版本: 未知（社区分发版本不一，无统一版本号）
// -----------------------------------------------------------------------------

private void RegisterUSpigotYml()
{
    // ⚠️ USpigot 实际配置文件名未知，此处按 Spigot/Paper 分支惯例推断为 uspigot.yml
    // 实际可能为 u-spigot.yml、core.yml，或根本无独立配置文件（混入 spigot.yml）
    // 请以核心启动后生成的实际文件为准
    const string file = "uspigot.yml";

    // ==================== settings（基础设置 / 推断） ====================
    // ⚠️ 以下 3 项均为基于同类分支惯例的推断项，可能与实际不符

    Register(new ServerConfigDescriptor
    {
        Key = "settings.brand-name",
        ConfigFileName = file,
        DisplayName = "服务端品牌名",
        Description = "⚠️ 推断项，未经官方源码核实\n发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码定制\n建议改为通用名（如 Paper）以隐藏核心类型\n实际默认值以核心启动后生成的配置为准",
        Category = "基础设置",
        DefaultValue = "USpigot",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.commands.enable-version-command",
        ConfigFileName = file,
        DisplayName = "启用 /version 命令",
        Description = "⚠️ 推断项，未经官方源码核实\n是否允许玩家使用 /version（/ver）查看服务端版本信息\n公网服建议关闭以防信息泄露\n实际默认值以核心启动后生成的配置为准",
        Category = "基础设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "settings.commands.enable-plugins-command",
        ConfigFileName = file,
        DisplayName = "启用 /plugins 命令",
        Description = "⚠️ 推断项，未经官方源码核实\n是否允许玩家使用 /plugins（/pl）查看已加载插件列表\n公网服建议关闭以防泄露插件信息\n实际默认值以核心启动后生成的配置为准",
        Category = "基础设置",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });
}
