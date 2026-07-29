// -----------------------------------------------------------------------------
// 文件名: RegisterCommandsYml.cs
// 功能描述: 注册 Bukkit commands.yml（命令别名与替换配置）的描述符
//           ⚠️ 1.13+ 引入，比 bukkit.yml 的 aliases 更灵活，支持参数转发
//           别名不能与现有命令同名，否则不生效
// 数据来源: Bukkit Wiki - commands.yml / org.bukkit.command.CommandMap
// 适用版本: Bukkit 1.13+ / Spigot / Paper / Purpur 等所有 Bukkit 衍生核心
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
//           并在构造函数中调用 RegisterCommandsYml();
// -----------------------------------------------------------------------------

private void RegisterCommandsYml()
{
    const string file = "commands.yml";

    // ==================== 顶层结构 ====================
    // commands.yml 仅含两个顶层键：command-block-overrides 与 aliases

    Register(new ServerConfigDescriptor
    {
        Key = "command-block-overrides",
        ConfigFileName = file,
        DisplayName = "命令方块覆盖",
        Description = "命令方块执行命令时使用的别名映射\n键为原命令名，值为别名命令\n一般留空 {}，命令方块直接执行原命令\n例：{\"gamemode\": \"minecraft:gamemode\"}",
        Category = "顶层",
        DefaultValue = "{}",
        ValueType = "map",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases",
        ConfigFileName = file,
        DisplayName = "命令别名",
        Description = "全局命令别名映射\n键为别名命令名（玩家输入的命令），值可以是字符串（直接转发到目标命令）或 map（含 i/k/p 等 flags）\n⚠️ 别名不能与现有命令同名，否则不生效\n例：\naliases:\n  gmc:\n    p: \"minecraft:gamemode creative $1\"\n  i:\n    p: \"minecraft:give $1 $2 $3\"\n    i: true",
        Category = "顶层",
        DefaultValue = "{}",
        ValueType = "map",
        RequiresRestart = false
    });

    // ==================== aliases.<别名> 通用字段 ====================
    // 每个别名条目支持三种 flags：i（忽略大小写）、k（保留原命令）、p（参数模板）

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.<alias>.p",
        ConfigFileName = file,
        DisplayName = "参数转发模板",
        Description = "别名转发的目标命令与参数模板\n占位符：\n  $1 = 第一个参数\n  $2 = 第二个参数\n  $1- = 第一个及之后所有参数\n  $@ = 所有参数\n例：\"minecraft:gamemode creative $1\" 将别名参数作为 gamemode 的第二个参数\n⚠️ 目标命令前缀 minecraft: 强制使用 Vanilla 实现，绕过插件 Hook",
        Category = "别名字段",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.<alias>.i",
        ConfigFileName = file,
        DisplayName = "忽略大小写",
        Description = "别名匹配时是否忽略大小写\ntrue = 大小写不敏感（/GMC 与 /gmc 都触发）\nfalse = 严格大小写匹配\n推荐开启以提高容错",
        Category = "别名字段",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.<alias>.k",
        ConfigFileName = file,
        DisplayName = "保留原命令",
        Description = "true = 除执行别名命令外，原命令（如有）仍保留可用\nfalse = 别名完全替换原命令\n⚠️ 仅当别名与现有命令同名时此项有意义",
        Category = "别名字段",
        DefaultValue = "false",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    // ==================== 别名目标命令命名空间前缀 ====================
    // 别名目标命令可加命名空间前缀强制使用特定实现，绕过插件 Hook。

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.<alias>.namespace.minecraft",
        ConfigFileName = file,
        DisplayName = "minecraft: 命名空间前缀",
        Description = "目标命令前缀 minecraft: 强制使用 Vanilla 实现\n例：\"minecraft:gamemode\" 绕过所有插件的 gamemode Hook\n⚠️ 使用此前缀可能导致插件功能失效，仅在确认无插件 Hook 此命令时使用",
        Category = "命名空间",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.<alias>.namespace.bukkit",
        ConfigFileName = file,
        DisplayName = "bukkit: 命名空间前缀",
        Description = "目标命令前缀 bukkit: 强制使用 Bukkit 实现\n例：\"bukkit:gamemode\" 强制使用 Bukkit 版本的 gamemode",
        Category = "命名空间",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    // ==================== 常用别名示例注册（参考项） ====================
    // 以下注册几个常用别名示例，供管理员参考。默认配置中 aliases 为空，需手动添加。
    // 这些示例不会自动生效，需手动复制到 commands.yml 中。

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.gamemode.p",
        ConfigFileName = file,
        DisplayName = "示例：gamemode 别名",
        Description = "示例别名：将 /gamemode 重定向到 /minecraft:gamemode\n配置：\naliases:\n  gamemode:\n    p: \"minecraft:gamemode $1-\"\n效果：玩家输入 /gamemode creative 等价于 /minecraft:gamemode creative\n用意：绕过插件 Hook，强制使用 Vanilla 实现",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.gmc.p",
        ConfigFileName = file,
        DisplayName = "示例：gmc 快捷创造模式",
        Description = "示例别名：/gmc <玩家> 快速切换到创造模式\n配置：\naliases:\n  gmc:\n    p: \"minecraft:gamemode creative $1\"\n效果：/gmc 等价于 /gamemode creative（自己），/gmc PlayerA 等价于 /gamemode creative PlayerA",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.gms.p",
        ConfigFileName = file,
        DisplayName = "示例：gms 快捷生存模式",
        Description = "示例别名：/gms <玩家> 快速切换到生存模式\n配置：\naliases:\n  gms:\n    p: \"minecraft:gamemode survival $1\"",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.i.p",
        ConfigFileName = file,
        DisplayName = "示例：i 快捷 give",
        Description = "示例别名：/i <物品> <数量> <数据> 等价于 /give 自己\n配置：\naliases:\n  i:\n    p: \"minecraft:give <player> $1 $2 $3\"\n    i: true\n效果：/i diamond 64 等价于 /give <自己> diamond 64",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.t.p",
        ConfigFileName = file,
        DisplayName = "示例：t 快捷 teleport",
        Description = "示例别名：/t <玩家> 等价于 /tp\n配置：\naliases:\n  t:\n    p: \"minecraft:teleport $1-\"\n    i: true",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "aliases.heal.p",
        ConfigFileName = file,
        DisplayName = "示例：heal 快捷治疗",
        Description = "示例别名：/heal 通过 effect 命令给自己瞬间治疗\n配置：\naliases:\n  heal:\n    p: \"minecraft:effect give <player> minecraft:instant_health 1 10\"",
        Category = "常用示例",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });
}
