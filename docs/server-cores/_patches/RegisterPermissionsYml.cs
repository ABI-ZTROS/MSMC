// -----------------------------------------------------------------------------
// 文件名: RegisterPermissionsYml.cs
// 功能描述: 注册 Bukkit permissions.yml（默认权限组配置）的描述符
//           ⚠️ permissions.yml 不定义具体权限，仅定义"权限组（permission groups）"
//           供插件通过 Permission API 引用聚合，普通权限由各插件自行注册
// 数据来源: Bukkit Wiki - permissions.yml / org.bukkit.permissions.Permission API
// 适用版本: Bukkit 1.13+ / Spigot / Paper / Purpur 等所有 Bukkit 衍生核心
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
//           并在构造函数中调用 RegisterPermissionsYml();
// -----------------------------------------------------------------------------

private void RegisterPermissionsYml()
{
    const string file = "permissions.yml";

    // ==================== 内置默认组（default） ====================
    // Bukkit 启动时自动注入一个名为 "default" 的内置权限组，default.default = true
    // 即所有玩家默认拥有。可在此组下追加权限节点，使其对所有人开放。

    Register(new ServerConfigDescriptor
    {
        Key = "default",
        ConfigFileName = file,
        DisplayName = "内置默认权限组",
        Description = "Bukkit 内置的默认权限组名，所有玩家自动归属此组\n组的 children 列表中的权限会按 default 字段策略赋给玩家\n⚠️ 不建议在此组直接添加高权限节点，应另建自定义组",
        Category = "内置组",
        DefaultValue = "{}",
        ValueType = "map",
        RequiresRestart = false
    });

    // ==================== 通用：自定义权限组字段 ====================
    // 任何自定义权限组（如 server.vip / server.admin）均含以下字段：
    //   - default：默认赋权策略（true / false / op / not-op）
    //   - description：组描述
    //   - children：子权限列表（权限节点 -> true/false）

    Register(new ServerConfigDescriptor
    {
        Key = "<custom-group>.default",
        ConfigFileName = file,
        DisplayName = "组默认赋权策略",
        Description = "此权限组的默认赋权策略\ntrue = 所有人都拥有此组权限\nfalse = 所有人都没有此组权限（需插件显式赋予）\nop = 仅 OP 拥有\nnot-op = 仅非 OP 拥有\n推荐：普通玩家组设 true，特权组设 false（由权限插件管理）",
        Category = "通用字段",
        DefaultValue = "op",
        AllowedValues = ["true", "false", "op", "not-op"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "<custom-group>.description",
        ConfigFileName = file,
        DisplayName = "权限组描述",
        Description = "此权限组的文字描述，便于管理员理解用途\n仅作记录，不影响实际权限判断\n例：\"VIP 玩家基础权限组\"",
        Category = "通用字段",
        DefaultValue = "",
        ValueType = "string",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "<custom-group>.children",
        ConfigFileName = file,
        DisplayName = "子权限列表",
        Description = "此组包含的子权限节点映射\n键为权限节点名（如 bukkit.command.teleport），值为 true/false 表示是否赋予\n⚠️ 可嵌套其他权限组（递归赋权）\n例：\nchildren:\n  bukkit.command.help: true\n  bukkit.command.tell: true\n  server.basics: true   # 嵌套引用其他权限组",
        Category = "通用字段",
        DefaultValue = "{}",
        ValueType = "map",
        RequiresRestart = false
    });

    // ==================== 内置 Bukkit 命令权限节点 ====================
    // Bukkit API 自带的命令权限节点，可在自定义组的 children 中引用。
    // 以下注册几个最常用的内置权限节点供管理员参考。

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.help",
        ConfigFileName = file,
        DisplayName = "Bukkit help 命令权限",
        Description = "允许玩家执行 /help 命令查看帮助页\n所有玩家默认拥有（在 default 组中）",
        Category = "Bukkit 内置权限",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.tell",
        ConfigFileName = file,
        DisplayName = "Bukkit tell 命令权限",
        Description = "允许玩家执行 /tell（/msg）私聊命令\n所有玩家默认拥有",
        Category = "Bukkit 内置权限",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.list",
        ConfigFileName = file,
        DisplayName = "Bukkit list 命令权限",
        Description = "允许玩家执行 /list 查看在线玩家列表\n所有玩家默认拥有",
        Category = "Bukkit 内置权限",
        DefaultValue = "true",
        AllowedValues = ["true", "false"],
        ValueType = "bool",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.teleport",
        ConfigFileName = file,
        DisplayName = "Bukkit teleport 命令权限",
        Description = "允许玩家执行 /tp（/teleport）传送命令\n默认仅 OP 拥有，普通玩家需显式赋予",
        Category = "Bukkit 内置权限",
        DefaultValue = "op",
        AllowedValues = ["true", "false", "op", "not-op"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.gamemode",
        ConfigFileName = file,
        DisplayName = "Bukkit gamemode 命令权限",
        Description = "允许玩家执行 /gamemode 切换游戏模式\n默认仅 OP 拥有\n⚠️ 生存服绝不赋予普通玩家，否则可作弊",
        Category = "Bukkit 内置权限",
        DefaultValue = "op",
        AllowedValues = ["true", "false", "op", "not-op"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.give",
        ConfigFileName = file,
        DisplayName = "Bukkit give 命令权限",
        Description = "允许玩家执行 /give 给自己或其他玩家物品\n默认仅 OP 拥有\n⚠️ 生存服绝不赋予普通玩家",
        Category = "Bukkit 内置权限",
        DefaultValue = "op",
        AllowedValues = ["true", "false", "op", "not-op"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.stop",
        ConfigFileName = file,
        DisplayName = "Bukkit stop 命令权限",
        Description = "允许玩家执行 /stop 关闭服务器\n默认仅 OP 拥有\n⚠️ 生产环境绝不赋予普通玩家",
        Category = "Bukkit 内置权限",
        DefaultValue = "op",
        AllowedValues = ["true", "false", "op", "not-op"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.ban",
        ConfigFileName = file,
        DisplayName = "Bukkit ban 命令权限",
        Description = "允许玩家执行 /ban 封禁玩家\n默认仅 OP 拥有\n管理员组可赋予此权限",
        Category = "Bukkit 内置权限",
        DefaultValue = "op",
        AllowedValues = ["true", "false", "op", "not-op"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.whitelist",
        ConfigFileName = file,
        DisplayName = "Bukkit whitelist 命令权限",
        Description = "允许玩家执行 /whitelist 管理白名单\n默认仅 OP 拥有",
        Category = "Bukkit 内置权限",
        DefaultValue = "op",
        AllowedValues = ["true", "false", "op", "not-op"],
        ValueType = "enum",
        RequiresRestart = false
    });

    Register(new ServerConfigDescriptor
    {
        Key = "children.bukkit.command.op",
        ConfigFileName = file,
        DisplayName = "Bukkit op 命令权限",
        Description = "允许玩家执行 /op /deop 授予/撤销 OP 权限\n默认仅 OP 拥有\n⚠️ 极敏感权限，绝不赋予普通玩家",
        Category = "Bukkit 内置权限",
        DefaultValue = "op",
        AllowedValues = ["true", "false", "op", "not-op"],
        ValueType = "enum",
        RequiresRestart = false
    });
}
